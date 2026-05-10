using Dapper;
using IISManager.Domain.Entities;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;

namespace IISManager.Infrastructure.Repositories;

public class ApplicationPoolRepository : IApplicationPoolRepository
{
    private readonly IDatabaseFactory _db;
    public ApplicationPoolRepository(IDatabaseFactory db) => _db = db;

    public async Task<IEnumerable<ApplicationPool>> GetByServerIdAsync(int serverId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ApplicationPool>(
            "SELECT * FROM ApplicationPools WHERE ServerId = @ServerId ORDER BY Name",
            new { ServerId = serverId });
    }

    public async Task<ApplicationPool?> GetByServerAndNameAsync(int serverId, string name)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ApplicationPool>(
            "SELECT * FROM ApplicationPools WHERE ServerId = @ServerId AND Name = @Name",
            new { ServerId = serverId, Name = name });
    }

    public async Task UpsertAsync(ApplicationPool appPool)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            MERGE INTO ApplicationPools AS target
            USING (SELECT @ServerId AS ServerId, @Name AS Name) AS source
            ON target.ServerId = source.ServerId AND target.Name = source.Name
            WHEN MATCHED THEN UPDATE SET
                Status = @Status, RuntimeVersion = @RuntimeVersion,
                PipelineMode = @PipelineMode, AutoStart = @AutoStart, LastSyncedAt = @LastSyncedAt
            WHEN NOT MATCHED THEN INSERT
                (ServerId, Name, Status, RuntimeVersion, PipelineMode, AutoStart, LastSyncedAt)
            VALUES
                (@ServerId, @Name, @Status, @RuntimeVersion, @PipelineMode, @AutoStart, @LastSyncedAt);
            """, appPool);
    }

    public async Task DeleteByServerIdAsync(int serverId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM ApplicationPools WHERE ServerId = @ServerId", new { ServerId = serverId });
    }

    public async Task DeleteStaleAsync(int serverId, IEnumerable<string> activeNames)
    {
        using var conn = _db.CreateConnection();
        var names = activeNames.ToList();
        if (names.Count == 0)
        {
            await conn.ExecuteAsync("DELETE FROM ApplicationPools WHERE ServerId = @ServerId", new { ServerId = serverId });
        }
        else
        {
            await conn.ExecuteAsync(
                "DELETE FROM ApplicationPools WHERE ServerId = @ServerId AND Name NOT IN @Names",
                new { ServerId = serverId, Names = names });
        }
    }
}
