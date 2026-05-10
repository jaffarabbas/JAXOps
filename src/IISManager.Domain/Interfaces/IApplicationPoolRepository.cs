using IISManager.Domain.Entities;

namespace IISManager.Domain.Interfaces;

public interface IApplicationPoolRepository
{
    Task<IEnumerable<ApplicationPool>> GetByServerIdAsync(int serverId);
    Task<ApplicationPool?> GetByServerAndNameAsync(int serverId, string name);
    Task UpsertAsync(ApplicationPool appPool);
    Task DeleteByServerIdAsync(int serverId);
}
