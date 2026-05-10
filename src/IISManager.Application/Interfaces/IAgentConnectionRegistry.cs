namespace IISManager.Application.Interfaces;

public interface IAgentConnectionRegistry
{
    void Register(int serverId, string connectionId);
    void Unregister(string connectionId);
    string? GetConnectionId(int serverId);
    int? GetServerId(string connectionId);
    IEnumerable<int> GetOnlineServerIds();
}
