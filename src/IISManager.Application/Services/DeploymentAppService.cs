using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using IISManager.Contracts.Commands;
using IISManager.Domain.Common;
using IISManager.Domain.Entities;
using IISManager.Domain.Enums;
using IISManager.Domain.Interfaces;
using IISManager.Shared.Models;
using Microsoft.Extensions.Logging;

namespace IISManager.Application.Services;

public class DeploymentAppService : IDeploymentAppService
{
    private readonly IDeploymentRepository _deployments;
    private readonly IPackageRepository _packages;
    private readonly IServerRepository _servers;
    private readonly IAgentCommunicationService _agentComm;
    private readonly IAuditAppService _audit;
    private readonly ILogger<DeploymentAppService> _logger;

    public DeploymentAppService(
        IDeploymentRepository deployments,
        IPackageRepository packages,
        IServerRepository servers,
        IAgentCommunicationService agentComm,
        IAuditAppService audit,
        ILogger<DeploymentAppService> logger)
    {
        _deployments = deployments;
        _packages = packages;
        _servers = servers;
        _agentComm = agentComm;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PagedResult<DeploymentDto>> GetPagedAsync(int page, int pageSize, int? applicationId = null, DeploymentStatus? status = null)
    {
        var items = await _deployments.GetPagedAsync(page, pageSize, applicationId, status);
        var count = await _deployments.CountAsync(applicationId, status);
        var dtos = new List<DeploymentDto>();
        foreach (var d in items)
        {
            var targets = await _deployments.GetTargetsAsync(d.Id);
            dtos.Add(MapToDto(d, targets));
        }
        return PagedResult<DeploymentDto>.Create(dtos, count, page, pageSize);
    }

    public async Task<DeploymentDto?> GetByIdAsync(int id)
    {
        var deployment = await _deployments.GetByIdAsync(id);
        if (deployment is null) return null;
        var targets = await _deployments.GetTargetsAsync(id);
        return MapToDto(deployment, targets);
    }

    public async Task<IEnumerable<DeploymentLogDto>> GetLogsAsync(int deploymentId)
    {
        var logs = await _deployments.GetLogsAsync(deploymentId);
        return logs.Select(l => new DeploymentLogDto
        {
            Id = l.Id,
            Message = l.Message,
            Level = l.Level,
            Timestamp = l.Timestamp,
            ServerId = l.ServerId
        });
    }

    public async Task<Result<int>> StartDeploymentAsync(CreateDeploymentDto dto, string initiatedBy)
    {
        var package = await _packages.GetByIdAsync(dto.PackageId);
        if (package is null) return Result<int>.Fail("Package not found");

        var deployment = new Deployment
        {
            CorrelationId = Guid.NewGuid(),
            ApplicationId = dto.ApplicationId,
            PackageId = dto.PackageId,
            Version = dto.Version,
            Status = DeploymentStatus.Queued,
            Mode = dto.Mode,
            DeployedBy = initiatedBy,
            CreatedAt = DateTime.UtcNow,
            Notes = dto.Notes
        };

        var deploymentId = await _deployments.InsertAsync(deployment);

        foreach (var t in dto.Targets)
        {
            await _deployments.InsertTargetAsync(new DeploymentTarget
            {
                DeploymentId = deploymentId,
                ServerId = t.ServerId,
                WebsiteName = t.WebsiteName,
                AppPoolName = t.AppPoolName,
                PhysicalPath = t.PhysicalPath,
                Status = DeploymentStatus.Queued
            });
        }

        await _audit.LogAsync(AuditAction.DeploymentStarted, "Deployment", deploymentId.ToString(),
            $"Deployment v{dto.Version} started for application {dto.ApplicationId}", initiatedBy);

        // Dispatch commands to agents (fire-and-forget — agents stream results back via SignalR)
        _ = Task.Run(() => DispatchDeploymentCommandsAsync(deploymentId, deployment.CorrelationId, package, dto));

        return Result<int>.Ok(deploymentId);
    }

    public async Task<Result<int>> StartRollbackAsync(int deploymentId, string initiatedBy)
    {
        var original = await _deployments.GetByIdAsync(deploymentId);
        if (original is null) return Result<int>.Fail("Original deployment not found");

        var targets = await _deployments.GetTargetsAsync(deploymentId);
        var successfulTarget = targets.FirstOrDefault(t => t.Status == DeploymentStatus.Succeeded);
        if (successfulTarget is null) return Result<int>.Fail("No successful deployment target to roll back from");

        var rollback = new Deployment
        {
            CorrelationId = Guid.NewGuid(),
            ApplicationId = original.ApplicationId,
            PackageId = original.PackageId,
            Version = original.Version + "-rollback",
            Status = DeploymentStatus.Queued,
            Mode = DeploymentMode.Sequential,
            DeployedBy = initiatedBy,
            CreatedAt = DateTime.UtcNow,
            IsRollback = true,
            RollbackTargetDeploymentId = deploymentId,
            Notes = $"Rollback of deployment #{deploymentId}"
        };

        var rollbackId = await _deployments.InsertAsync(rollback);

        await _audit.LogAsync(AuditAction.DeploymentRolledBack, "Deployment", rollbackId.ToString(),
            $"Rollback initiated for deployment #{deploymentId}", initiatedBy);

        return Result<int>.Ok(rollbackId);
    }

    public async Task<Result> CancelDeploymentAsync(int id, string cancelledBy)
    {
        var deployment = await _deployments.GetByIdAsync(id);
        if (deployment is null) return Result.Fail("Deployment not found");
        if (deployment.Status != DeploymentStatus.Queued) return Result.Fail("Only queued deployments can be cancelled");

        await _deployments.UpdateStatusAsync(id, DeploymentStatus.Cancelled, completedAt: DateTime.UtcNow);
        return Result.Ok();
    }

    private async Task DispatchDeploymentCommandsAsync(int deploymentId, Guid correlationId, DeploymentPackage package, CreateDeploymentDto dto)
    {
        try
        {
            await _deployments.UpdateStatusAsync(deploymentId, DeploymentStatus.InProgress, startedAt: DateTime.UtcNow);
            var targets = await _deployments.GetTargetsAsync(deploymentId);

            if (dto.Mode == DeploymentMode.Sequential)
            {
                foreach (var target in targets)
                    await SendDeployCommandAsync(target, correlationId, package);
            }
            else
            {
                var tasks = targets.Select(t => SendDeployCommandAsync(t, correlationId, package));
                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch deployment commands for deployment {DeploymentId}", deploymentId);
            await _deployments.UpdateStatusAsync(deploymentId, DeploymentStatus.Failed,
                completedAt: DateTime.UtcNow, failureReason: ex.Message);
        }
    }

    private async Task SendDeployCommandAsync(DeploymentTarget target, Guid correlationId, DeploymentPackage package)
    {
        var cmd = new DeployApplicationCommand
        {
            ServerId = target.ServerId,
            DeploymentId = target.DeploymentId,
            DeploymentTargetId = target.Id,
            CorrelationId = correlationId,
            PackageUrl = $"/api/packages/{package.Id}/download",
            PackageSha256 = package.Sha256Hash,
            WebsiteName = target.WebsiteName,
            AppPoolName = target.AppPoolName,
            PhysicalPath = target.PhysicalPath,
            Version = package.Version,
            CreateBackup = true
        };
        await _agentComm.SendCommandAsync(target.ServerId, cmd);
    }

    private static DeploymentDto MapToDto(Deployment d, IEnumerable<DeploymentTarget> targets) => new()
    {
        Id = d.Id,
        CorrelationId = d.CorrelationId,
        ApplicationId = d.ApplicationId,
        ApplicationName = d.ApplicationName ?? string.Empty,
        Version = d.Version,
        Status = d.Status,
        Mode = d.Mode,
        DeployedBy = d.DeployedBy,
        CreatedAt = d.CreatedAt,
        StartedAt = d.StartedAt,
        CompletedAt = d.CompletedAt,
        Notes = d.Notes,
        IsRollback = d.IsRollback,
        FailureReason = d.FailureReason,
        Targets = targets.Select(t => new DeploymentTargetDto
        {
            Id = t.Id,
            ServerId = t.ServerId,
            ServerName = t.ServerName ?? string.Empty,
            WebsiteName = t.WebsiteName,
            AppPoolName = t.AppPoolName,
            Status = t.Status,
            StartedAt = t.StartedAt,
            CompletedAt = t.CompletedAt,
            FailureReason = t.FailureReason
        }).ToList()
    };
}
