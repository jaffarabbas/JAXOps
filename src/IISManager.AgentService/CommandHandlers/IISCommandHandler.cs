using IISManager.AgentService.Services;
using IISManager.Contracts.Commands;
using IISManager.Contracts.Enums;
using IISManager.Contracts.Events;
using IISManager.Shared.Constants;

namespace IISManager.AgentService.CommandHandlers;

public class IISCommandHandler : ICommandHandler
{
    private readonly IISManagementService _iis;
    private readonly HealthReportingService _health;
    private readonly AgentSignalRClient _client;
    private readonly ILogger<IISCommandHandler> _logger;

    private static readonly CommandType[] HandledTypes = {
        CommandType.StartWebsite, CommandType.StopWebsite, CommandType.RestartWebsite,
        CommandType.CreateWebsite, CommandType.DeleteWebsite,
        CommandType.RestartApplicationPool, CommandType.StopApplicationPool, CommandType.StartApplicationPool,
        CommandType.CreateApplicationPool, CommandType.DeleteApplicationPool,
        CommandType.RecycleApplicationPool, CommandType.GetIISStatus, CommandType.GetServerHealth,
        CommandType.SyncIISState
    };

    public IISCommandHandler(IISManagementService iis, HealthReportingService health, AgentSignalRClient client, ILogger<IISCommandHandler> logger)
    {
        _iis = iis;
        _health = health;
        _client = client;
        _logger = logger;
    }

    public bool CanHandle(AgentCommandBase command) => HandledTypes.Contains(command.CommandType);

    public async Task HandleAsync(AgentCommandBase command, CancellationToken ct)
    {
        bool success;
        string? error = null;

        switch (command.CommandType)
        {
            case CommandType.StartWebsite:
            {
                var cmd = (WebsiteCommand)command;
                var r = _iis.StartSite(cmd.WebsiteName);
                success = r.IsSuccess;
                error = r.Error;
                break;
            }
            case CommandType.StopWebsite:
            {
                var cmd = (WebsiteCommand)command;
                var r = _iis.StopSite(cmd.WebsiteName);
                success = r.IsSuccess;
                error = r.Error;
                break;
            }
            case CommandType.StartApplicationPool:
            {
                var cmd = (AppPoolCommand)command;
                var r = _iis.StartAppPool(cmd.AppPoolName);
                success = r.IsSuccess;
                error = r.Error;
                break;
            }
            case CommandType.StopApplicationPool:
            {
                var cmd = (AppPoolCommand)command;
                var r = _iis.StopAppPool(cmd.AppPoolName);
                success = r.IsSuccess;
                error = r.Error;
                break;
            }
            case CommandType.RecycleApplicationPool:
            case CommandType.RestartApplicationPool:
            {
                var cmd = (AppPoolCommand)command;
                var r = _iis.RecycleAppPool(cmd.AppPoolName);
                success = r.IsSuccess;
                error = r.Error;
                break;
            }
            case CommandType.CreateWebsite:
            {
                var cmd = (WebsiteCommand)command;
                var r = _iis.CreateSite(cmd);
                success = r.IsSuccess;
                error = r.Error;
                break;
            }
            case CommandType.DeleteWebsite:
            {
                var cmd = (WebsiteCommand)command;
                var r = _iis.DeleteSite(cmd.WebsiteName);
                success = r.IsSuccess;
                error = r.Error;
                break;
            }
            case CommandType.CreateApplicationPool:
            {
                var cmd = (AppPoolCommand)command;
                var r = _iis.CreateAppPool(cmd);
                success = r.IsSuccess;
                error = r.Error;
                break;
            }
            case CommandType.DeleteApplicationPool:
            {
                var cmd = (AppPoolCommand)command;
                var r = _iis.DeleteAppPool(cmd.AppPoolName);
                success = r.IsSuccess;
                error = r.Error;
                break;
            }
            case CommandType.GetServerHealth:
            {
                var heartbeat = await _health.BuildHeartbeatAsync();
                await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportHeartbeat, heartbeat, ct);
                success = true;
                break;
            }
            case CommandType.SyncIISState:
            {
                var heartbeat = await _health.BuildHeartbeatAsync();
                await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportHeartbeat, heartbeat, ct);
                var snapshot = _iis.GetSnapshot();
                await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportIISState, snapshot, ct);
                success = true;
                break;
            }
            default:
                _logger.LogWarning("Unhandled command type {Type}", command.CommandType);
                success = false;
                error = "Not implemented";
                break;
        }

        await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportCommandResult,
            new CommandResultEvent
            {
                ServerId = command.ServerId,
                CommandId = command.CommandId,
                CommandType = command.CommandType,
                Success = success,
                ErrorDetail = error
            }, ct);
    }
}
