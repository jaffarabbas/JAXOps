using Dapper;
using IISManager.Domain.Entities;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;

namespace IISManager.Infrastructure.Repositories;

public class WebsiteRepository : IWebsiteRepository
{
    private readonly IDatabaseFactory _db;
    public WebsiteRepository(IDatabaseFactory db) => _db = db;

    public async Task<IEnumerable<Website>> GetByServerIdAsync(int serverId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Website>(
            "SELECT * FROM Websites WHERE ServerId = @ServerId ORDER BY Name",
            new { ServerId = serverId });
    }

    public async Task<Website?> GetByServerAndNameAsync(int serverId, string name)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Website>(
            "SELECT * FROM Websites WHERE ServerId = @ServerId AND Name = @Name",
            new { ServerId = serverId, Name = name });
    }

    public async Task UpsertAsync(Website website)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            MERGE INTO Websites AS target
            USING (SELECT @ServerId AS ServerId, @IISId AS IISId) AS source
            ON target.ServerId = source.ServerId AND target.IISId = source.IISId
            WHEN MATCHED THEN UPDATE SET
                Name = @Name, PhysicalPath = @PhysicalPath, Status = @Status,
                DefaultAppPool = @DefaultAppPool, BindingsJson = @BindingsJson, LastSyncedAt = @LastSyncedAt
            WHEN NOT MATCHED THEN INSERT
                (ServerId, IISId, Name, PhysicalPath, Status, DefaultAppPool, BindingsJson, LastSyncedAt)
            VALUES
                (@ServerId, @IISId, @Name, @PhysicalPath, @Status, @DefaultAppPool, @BindingsJson, @LastSyncedAt);
            """, website);
    }

    public async Task DeleteByServerIdAsync(int serverId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Websites WHERE ServerId = @ServerId", new { ServerId = serverId });
    }
}
