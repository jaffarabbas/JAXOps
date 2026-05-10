using IISManager.Domain.Entities;

namespace IISManager.Domain.Interfaces;

public interface IServerHealthRepository
{
    Task<ServerHealth?> GetLatestAsync(int serverId);
    Task<IEnumerable<ServerHealth>> GetHistoryAsync(int serverId, DateTime from, DateTime to);
    Task InsertAsync(ServerHealth health);
    Task PurgeOlderThanAsync(DateTime cutoff);
}
