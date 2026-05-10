using IISManager.Application.Interfaces;
using IISManager.Contracts.Commands;
using IISManager.Domain.Common;
using IISManager.Shared.Constants;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace IISManager.Infrastructure.Services;

// Requires IHubContext<AgentHub> — hub is defined in WebPortal.
// To avoid circular reference, the hub context is injected via a thin interface.
public interface IAgentHubContext
{
    Task SendToClientAsync(string connectionId, string method, object arg, CancellationToken ct = default);
}

public class AgentCommunicationService : IAgentCommunicationService
{
    private readonly IAgentConnectionRegistry _registry;
    private readonly IAgentHubContext _hubContext;
    private readonly ILogger<AgentCommunicationService> _logger;

    public AgentCommunicationService(
        IAgentConnectionRegistry registry,
        IAgentHubContext hubContext,
        ILogger<AgentCommunicationService> logger)
    {
        _registry = registry;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<Result> SendCommandAsync(int serverId, AgentCommandBase command, CancellationToken ct = default)
    {
        var connectionId = _registry.GetConnectionId(serverId);
        if (connectionId is null)
        {
            _logger.LogWarning("Server {ServerId} is not connected — cannot send command {CommandType}",
                serverId, command.CommandType);
            return Result.Fail($"Server {serverId} is not online");
        }

        try
        {
            await _hubContext.SendToClientAsync(connectionId, SignalRConstants.AgentMethods.ExecuteCommand, command, ct);
            _logger.LogInformation("Command {CommandType} sent to server {ServerId}", command.CommandType, serverId);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command {CommandType} to server {ServerId}", command.CommandType, serverId);
            return Result.Fail(ex.Message);
        }
    }

    public Task<bool> IsServerOnlineAsync(int serverId)
        => Task.FromResult(_registry.GetConnectionId(serverId) is not null);
}
