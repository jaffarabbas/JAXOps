using IISManager.Infrastructure.Services;
using IISManager.WebPortal.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IISManager.WebPortal.Services;

// Adapts IHubContext<AgentHub> to the IAgentHubContext interface defined in Infrastructure
// so Infrastructure can send to agents without depending on WebPortal.
public class AgentHubContextAdapter : IAgentHubContext
{
    private readonly IHubContext<AgentHub> _hub;

    public AgentHubContextAdapter(IHubContext<AgentHub> hub) => _hub = hub;

    public async Task SendToClientAsync(string connectionId, string method, object arg, CancellationToken ct = default)
        => await _hub.Clients.Client(connectionId).SendAsync(method, arg, ct);
}
