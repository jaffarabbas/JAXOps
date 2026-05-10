using IISManager.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IISManager.WebPortal.Hubs;

[Authorize]
public class DeploymentHub : Hub
{
    private readonly ILogger<DeploymentHub> _logger;
    public DeploymentHub(ILogger<DeploymentHub> logger) => _logger = logger;

    public async Task SubscribeToDeployment(int deploymentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRConstants.Groups.Deployment(deploymentId));
        _logger.LogDebug("User {User} subscribed to deployment {DeploymentId}",
            Context.User?.Identity?.Name, deploymentId);
    }

    public async Task UnsubscribeFromDeployment(int deploymentId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRConstants.Groups.Deployment(deploymentId));

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRConstants.Groups.AllUsers);
        await base.OnConnectedAsync();
    }
}
