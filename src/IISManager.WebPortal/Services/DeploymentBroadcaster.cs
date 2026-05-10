using IISManager.Application.Interfaces;
using IISManager.Shared.Constants;
using IISManager.WebPortal.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IISManager.WebPortal.Services;

public class DeploymentBroadcaster : IDeploymentBroadcaster
{
    private readonly IHubContext<DeploymentHub> _hub;

    public DeploymentBroadcaster(IHubContext<DeploymentHub> hub) => _hub = hub;

    public Task BroadcastLogLineAsync(int deploymentId, int? serverId, string message, string level)
        => _hub.Clients
            .Group(SignalRConstants.Groups.Deployment(deploymentId))
            .SendAsync(SignalRConstants.ClientMethods.OnLogLine, new
            {
                deploymentId,
                serverId,
                message,
                level,
                timestamp = DateTime.UtcNow
            });

    public Task BroadcastStatusChangeAsync(int deploymentId, string status, int? serverId = null)
        => _hub.Clients
            .Group(SignalRConstants.Groups.Deployment(deploymentId))
            .SendAsync(SignalRConstants.ClientMethods.OnStatusChange, new
            {
                deploymentId,
                serverId,
                status
            });
}
