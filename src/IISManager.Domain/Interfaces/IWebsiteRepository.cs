using IISManager.Domain.Entities;

namespace IISManager.Domain.Interfaces;

public interface IWebsiteRepository
{
    Task<IEnumerable<Website>> GetByServerIdAsync(int serverId);
    Task<Website?> GetByServerAndNameAsync(int serverId, string name);
    Task UpsertAsync(Website website);
    Task DeleteByServerIdAsync(int serverId);
}
