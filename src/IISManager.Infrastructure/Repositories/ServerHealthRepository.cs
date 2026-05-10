using Dapper;
using IISManager.Domain.Entities;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;

namespace IISManager.Infrastructure.Repositories;

public class ServerHealthRepository : IServerHealthRepository
{
    private readonly IDatabaseFactory _db;
    public ServerHealthRepository(IDatabaseFactory db) => _db = db;

    public async Task<ServerHealth?> GetLatestAsync(int serverId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ServerHealth>("""
            SELECT TOP 1 * FROM ServerHealth
            WHERE ServerId = @ServerId
            ORDER BY RecordedAt DESC
            """, new { ServerId = serverId });
    }

    public async Task<IEnumerable<ServerHealth>> GetHistoryAsync(int serverId, DateTime from, DateTime to)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ServerHealth>("""
            SELECT * FROM ServerHealth
            WHERE ServerId = @ServerId AND RecordedAt BETWEEN @From AND @To
            ORDER BY RecordedAt ASC
            """, new { ServerId = serverId, From = from, To = to });
    }

    public async Task InsertAsync(ServerHealth health)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO ServerHealth (ServerId, CpuPercent, RamUsedMB, RamTotalMB, DiskUsedGB, DiskTotalGB,
                IISRunning, RunningSites, RunningAppPools, AgentVersion, RecordedAt)
            VALUES (@ServerId, @CpuPercent, @RamUsedMB, @RamTotalMB, @DiskUsedGB, @DiskTotalGB,
                @IISRunning, @RunningSites, @RunningAppPools, @AgentVersion, @RecordedAt)
            """, health);
    }

    public async Task PurgeOlderThanAsync(DateTime cutoff)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM ServerHealth WHERE RecordedAt < @Cutoff",
            new { Cutoff = cutoff });
    }
}
