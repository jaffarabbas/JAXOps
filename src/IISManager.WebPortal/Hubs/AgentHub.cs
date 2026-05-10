using System.Text.Json;
using IISManager.Application.Interfaces;
using IISManager.Contracts.Events;
using IISManager.Domain.Enums;
using IISManager.Domain.Interfaces;
using IISManager.Shared.Constants;
using IISManager.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IISManager.WebPortal.Hubs;

// Agents connect here (not browser clients).
// Auth: pre-shared API key in X-Agent-ApiKey header.
[AllowAnonymous]
public class AgentHub : Hub
{
    private readonly IAgentConnectionRegistry _registry;
    private readonly IServerRepository _servers;
    private readonly IServerHealthRepository _healthRepo;
    private readonly IWebsiteRepository _websiteRepo;
    private readonly IApplicationPoolRepository _poolRepo;
    private readonly IDeploymentRepository _deployments;
    private readonly IHubContext<DeploymentHub> _deployHub;
    private readonly IHubContext<MonitoringHub> _monitorHub;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(
        IAgentConnectionRegistry registry,
        IServerRepository servers,
        IServerHealthRepository healthRepo,
        IWebsiteRepository websiteRepo,
        IApplicationPoolRepository poolRepo,
        IDeploymentRepository deployments,
        IHubContext<DeploymentHub> deployHub,
        IHubContext<MonitoringHub> monitorHub,
        ILogger<AgentHub> logger)
    {
        _registry = registry;
        _servers = servers;
        _healthRepo = healthRepo;
        _websiteRepo = websiteRepo;
        _poolRepo = poolRepo;
        _deployments = deployments;
        _deployHub = deployHub;
        _monitorHub = monitorHub;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpCtx = Context.GetHttpContext();
        var apiKey = httpCtx?.Request.Headers[SignalRConstants.AgentApiKeyHeader].FirstOrDefault();
        var serverIdStr = httpCtx?.Request.Headers[SignalRConstants.ServerIdHeader].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey) || !int.TryParse(serverIdStr, out var serverId))
        {
            _logger.LogWarning("Agent connection rejected — missing API key or server ID");
            Context.Abort();
            return;
        }

        var keyHash = apiKey.ToSha256Hash();
        var server = await _servers.GetByApiKeyHashAsync(keyHash);
        if (server is null || server.Id != serverId)
        {
            _logger.LogWarning("Agent connection rejected — invalid API key for server {ServerId}", serverId);
            Context.Abort();
            return;
        }

        Context.Items["ServerId"] = serverId;
        _registry.Register(serverId, Context.ConnectionId);

        await _servers.UpdateStatusAsync(serverId, ServerStatus.Online.ToString(), Context.ConnectionId, DateTime.UtcNow);
        _logger.LogInformation("Agent connected: Server {ServerId} ({ServerName}), ConnectionId={ConnectionId}",
            serverId, server.Name, Context.ConnectionId);

        // Notify monitoring hub that this server came online
        await _monitorHub.Clients
            .Group(SignalRConstants.Groups.AllUsers)
            .SendAsync(SignalRConstants.ClientMethods.OnServerStatusChange,
                new { serverId, status = "Online", serverName = server.Name });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var serverId = Context.Items.TryGetValue("ServerId", out var id) ? (int?)id : null;
        if (serverId.HasValue)
        {
            _registry.Unregister(Context.ConnectionId);
            await _servers.UpdateStatusAsync(serverId.Value, ServerStatus.Offline.ToString(), null, null);

            _logger.LogInformation("Agent disconnected: Server {ServerId}", serverId);

            await _monitorHub.Clients
                .Group(SignalRConstants.Groups.AllUsers)
                .SendAsync(SignalRConstants.ClientMethods.OnServerStatusChange,
                    new { serverId = serverId.Value, status = "Offline" });
        }
        await base.OnDisconnectedAsync(exception);
    }

    // ── Called by agents to report heartbeat ──────────────────────────────────

    public async Task ReportHeartbeat(AgentHeartbeatEvent evt)
    {
        if (!TryGetServerId(out var serverId)) return;

        await _healthRepo.InsertAsync(new Domain.Entities.ServerHealth
        {
            ServerId = serverId,
            CpuPercent = evt.CpuPercent,
            RamUsedMB = evt.RamUsedMB,
            RamTotalMB = evt.RamTotalMB,
            DiskUsedGB = evt.DiskUsedGB,
            DiskTotalGB = evt.DiskTotalGB,
            IISRunning = evt.IISRunning,
            RunningSites = evt.RunningSites,
            RunningAppPools = evt.RunningAppPools,
            AgentVersion = evt.AgentVersion,
            RecordedAt = DateTime.UtcNow
        });

        await _servers.UpdateStatusAsync(serverId, ServerStatus.Online.ToString(),
            Context.ConnectionId, DateTime.UtcNow);

        // Forward health data to browser monitoring hub
        await _monitorHub.Clients
            .Group(SignalRConstants.Groups.Server(serverId))
            .SendAsync(SignalRConstants.ClientMethods.OnServerHealthUpdate, new
            {
                serverId,
                cpuPercent = evt.CpuPercent,
                ramUsedMB = evt.RamUsedMB,
                ramTotalMB = evt.RamTotalMB,
                diskUsedGB = evt.DiskUsedGB,
                diskTotalGB = evt.DiskTotalGB,
                iisRunning = evt.IISRunning,
                runningSites = evt.RunningSites,
                runningAppPools = evt.RunningAppPools,
                timestamp = evt.Timestamp
            });
    }

    // ── Called by agents to report full IIS state snapshot ───────────────────

    public async Task ReportIISState(IISStateSnapshotEvent evt)
    {
        if (!TryGetServerId(out var serverId)) return;

        var now = DateTime.UtcNow;

        foreach (var s in evt.Sites)
        {
            await _websiteRepo.UpsertAsync(new Domain.Entities.Website
            {
                ServerId = serverId,
                IISId = s.IISId,
                Name = s.Name,
                PhysicalPath = s.PhysicalPath,
                Status = Enum.TryParse<WebsiteStatus>(s.Status, true, out var ws) ? ws : WebsiteStatus.Unknown,
                DefaultAppPool = s.DefaultAppPool,
                BindingsJson = JsonSerializer.Serialize(s.Bindings),
                LastSyncedAt = now
            });
        }

        foreach (var p in evt.AppPools)
        {
            await _poolRepo.UpsertAsync(new Domain.Entities.ApplicationPool
            {
                ServerId = serverId,
                Name = p.Name,
                Status = Enum.TryParse<AppPoolStatus>(p.Status, true, out var ps) ? ps : AppPoolStatus.Unknown,
                RuntimeVersion = p.RuntimeVersion,
                PipelineMode = p.PipelineMode,
                AutoStart = p.AutoStart,
                LastSyncedAt = now
            });
        }

        _logger.LogInformation("IIS state synced for server {ServerId}: {Sites} sites, {Pools} pools",
            serverId, evt.Sites.Count, evt.AppPools.Count);

        await _monitorHub.Clients
            .Group(SignalRConstants.Groups.Server(serverId))
            .SendAsync(SignalRConstants.ClientMethods.OnIISStateUpdate, new
            {
                serverId,
                siteCount = evt.Sites.Count,
                poolCount = evt.AppPools.Count,
                timestamp = now
            });
    }

    // ── Called by agents to report deployment progress ───────────────────────

    public async Task ReportProgress(DeploymentProgressEvent evt)
    {
        if (!TryGetServerId(out _)) return;

        await _deployHub.Clients
            .Group(SignalRConstants.Groups.Deployment(evt.DeploymentId))
            .SendAsync(SignalRConstants.ClientMethods.OnProgress, new
            {
                deploymentId = evt.DeploymentId,
                serverId = evt.ServerId,
                percent = evt.PercentComplete,
                step = evt.Step,
                status = evt.Status
            });
    }

    public async Task ReportLogLine(DeploymentLogLineEvent evt)
    {
        if (!TryGetServerId(out var serverId)) return;

        // Persist log line
        await _deployments.InsertLogAsync(new Domain.Entities.DeploymentLog
        {
            DeploymentId = evt.DeploymentId,
            ServerId = serverId,
            Message = evt.Message,
            Level = evt.Level,
            Timestamp = DateTime.UtcNow
        });

        // Stream to browser
        await _deployHub.Clients
            .Group(SignalRConstants.Groups.Deployment(evt.DeploymentId))
            .SendAsync(SignalRConstants.ClientMethods.OnLogLine, new
            {
                deploymentId = evt.DeploymentId,
                serverId,
                message = evt.Message,
                level = evt.Level,
                timestamp = DateTime.UtcNow
            });
    }

    public async Task ReportDeploymentCompleted(DeploymentCompletedEvent evt)
    {
        if (!TryGetServerId(out var serverId)) return;

        var status = evt.Success ? DeploymentStatus.Succeeded : DeploymentStatus.Failed;

        await _deployments.UpdateTargetStatusAsync(evt.DeploymentTargetId, status,
            completedAt: DateTime.UtcNow, failureReason: evt.FailureReason);

        // Check if all targets done → update parent deployment
        var targets = await _deployments.GetTargetsAsync(evt.DeploymentId);
        var allDone = targets.All(t => t.Status is DeploymentStatus.Succeeded
            or DeploymentStatus.Failed or DeploymentStatus.RolledBack);

        if (allDone)
        {
            var overallStatus = targets.All(t => t.Status == DeploymentStatus.Succeeded)
                ? DeploymentStatus.Succeeded
                : targets.Any(t => t.Status == DeploymentStatus.Succeeded)
                    ? DeploymentStatus.PartialSuccess
                    : DeploymentStatus.Failed;

            await _deployments.UpdateStatusAsync(evt.DeploymentId, overallStatus,
                completedAt: DateTime.UtcNow, failureReason: evt.FailureReason);
        }

        await _deployHub.Clients
            .Group(SignalRConstants.Groups.Deployment(evt.DeploymentId))
            .SendAsync(SignalRConstants.ClientMethods.OnStatusChange, new
            {
                deploymentId = evt.DeploymentId,
                serverId,
                success = evt.Success,
                status = status.ToString(),
                failureReason = evt.FailureReason,
                durationSeconds = (int)evt.Duration.TotalSeconds
            });
    }

    public async Task ReportCommandResult(CommandResultEvent evt)
    {
        if (!TryGetServerId(out _)) return;
        _logger.LogInformation("Command {CommandType} result from server {ServerId}: success={Success}",
            evt.CommandType, evt.ServerId, evt.Success);

        await _monitorHub.Clients
            .Group(SignalRConstants.Groups.Server(evt.ServerId))
            .SendAsync("OnCommandResult", new
            {
                commandId = evt.CommandId,
                commandType = evt.CommandType.ToString(),
                success = evt.Success,
                message = evt.Message,
                error = evt.ErrorDetail
            });
    }

    private bool TryGetServerId(out int serverId)
    {
        if (Context.Items.TryGetValue("ServerId", out var id) && id is int sid)
        {
            serverId = sid;
            return true;
        }
        serverId = 0;
        return false;
    }
}
