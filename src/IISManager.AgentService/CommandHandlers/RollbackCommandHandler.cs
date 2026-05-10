using IISManager.AgentService.Services;
using IISManager.Contracts.Commands;
using IISManager.Contracts.Enums;
using IISManager.Contracts.Events;
using IISManager.Shared.Constants;

namespace IISManager.AgentService.CommandHandlers;

public class RollbackCommandHandler : ICommandHandler
{
    private readonly BackupService _backup;
    private readonly IISManagementService _iis;
    private readonly AgentSignalRClient _client;
    private readonly ILogger<RollbackCommandHandler> _logger;

    public RollbackCommandHandler(BackupService backup, IISManagementService iis, AgentSignalRClient client, ILogger<RollbackCommandHandler> logger)
    {
        _backup = backup;
        _iis = iis;
        _client = client;
        _logger = logger;
    }

    public bool CanHandle(AgentCommandBase command) => command.CommandType == CommandType.RollbackDeployment;

    public async Task HandleAsync(AgentCommandBase command, CancellationToken ct)
    {
        var cmd = (RollbackCommand)command;
        var start = DateTime.UtcNow;

        async Task Log(string msg) =>
            await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportLogLine,
                new DeploymentLogLineEvent
                {
                    ServerId = cmd.ServerId,
                    DeploymentId = cmd.DeploymentId,
                    DeploymentTargetId = cmd.DeploymentTargetId,
                    Message = msg
                }, ct);

        try
        {
            await Log($"[{DateTime.UtcNow:HH:mm:ss}] Rollback started");
            _iis.StopAppPool(cmd.AppPoolName);
            await Log($"[{DateTime.UtcNow:HH:mm:ss}] App pool stopped");

            var result = _backup.RestoreBackup(cmd.BackupPath, cmd.PhysicalPath);
            if (!result.IsSuccess)
            {
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Rollback FAILED: {result.Error}");
                await ReportCompleted(cmd, false, result.Error, start, ct);
                return;
            }

            _iis.StartAppPool(cmd.AppPoolName);
            await Log($"[{DateTime.UtcNow:HH:mm:ss}] Rollback completed successfully");
            await ReportCompleted(cmd, true, null, start, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed for deployment {DeploymentId}", cmd.DeploymentId);
            await Log($"Rollback error: {ex.Message}");
            await ReportCompleted(cmd, false, ex.Message, start, ct);
        }
    }

    private async Task ReportCompleted(RollbackCommand cmd, bool success, string? error, DateTime start, CancellationToken ct) =>
        await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportDeploymentCompleted,
            new DeploymentCompletedEvent
            {
                ServerId = cmd.ServerId,
                DeploymentId = cmd.DeploymentId,
                DeploymentTargetId = cmd.DeploymentTargetId,
                Success = success,
                FailureReason = error,
                Duration = DateTime.UtcNow - start
            }, ct);
}
