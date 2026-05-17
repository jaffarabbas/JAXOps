using IISManager.Domain.Entities;

namespace IISManager.Domain.Interfaces;

public interface IServerRepository
{
    Task<Server?> GetByIdAsync(int id);
    Task<IEnumerable<Server>> GetAllActiveAsync();
    Task<Server?> GetByApiKeyHashAsync(string apiKeyHash);
    Task<int> InsertAsync(Server server);
    Task UpdateAsync(Server server);
    Task UpdateStatusAsync(int id, string status, string? connectionId, DateTime? lastHeartbeat);
    Task MarkStaleServersOfflineAsync(DateTime cutoff);
    Task DeleteAsync(int id);
}
