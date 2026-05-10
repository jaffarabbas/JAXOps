using IISManager.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IISManager.WebPortal.Hubs;

[Authorize]
public class MonitoringHub : Hub
{
    private readonly ILogger<MonitoringHub> _logger;
    public MonitoringHub(ILogger<MonitoringHub> logger) => _logger = logger;

    public async Task SubscribeToServer(int serverId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRConstants.Groups.Server(serverId));
        _logger.LogDebug("User {User} subscribed to server {ServerId}",
            Context.User?.Identity?.Name, serverId);
    }

    public async Task UnsubscribeFromServer(int serverId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRConstants.Groups.Server(serverId));

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRConstants.Groups.AllUsers);
        await base.OnConnectedAsync();
    }
}
