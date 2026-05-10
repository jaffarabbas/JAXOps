using System.Collections.Concurrent;
using IISManager.Application.Interfaces;

namespace IISManager.Application.Services;

public class AgentConnectionRegistry : IAgentConnectionRegistry
{
    // serverId → connectionId
    private readonly ConcurrentDictionary<int, string> _serverToConnection = new();
    // connectionId → serverId
    private readonly ConcurrentDictionary<string, int> _connectionToServer = new();

    public void Register(int serverId, string connectionId)
    {
        // Remove any stale connection for this server
        if (_serverToConnection.TryGetValue(serverId, out var old))
            _connectionToServer.TryRemove(old, out _);

        _serverToConnection[serverId] = connectionId;
        _connectionToServer[connectionId] = serverId;
    }

    public void Unregister(string connectionId)
    {
        if (_connectionToServer.TryRemove(connectionId, out var serverId))
            _serverToConnection.TryRemove(serverId, out _);
    }

    public string? GetConnectionId(int serverId)
        => _serverToConnection.TryGetValue(serverId, out var id) ? id : null;

    public int? GetServerId(string connectionId)
        => _connectionToServer.TryGetValue(connectionId, out var id) ? id : null;

    public IEnumerable<int> GetOnlineServerIds() => _serverToConnection.Keys;
}
