using Dapper;
using IISManager.Domain.Entities;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;

namespace IISManager.Infrastructure.Repositories;

public class ServerRepository : IServerRepository
{
    private readonly IDatabaseFactory _db;
    public ServerRepository(IDatabaseFactory db) => _db = db;

    public async Task<Server?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Server>(
            "SELECT * FROM Servers WHERE Id = @Id AND IsActive = 1", new { Id = id });
    }

    public async Task<IEnumerable<Server>> GetAllActiveAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Server>(
            "SELECT * FROM Servers WHERE IsActive = 1 ORDER BY Environment, Name");
    }

    public async Task<Server?> GetByApiKeyHashAsync(string apiKeyHash)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Server>(
            "SELECT * FROM Servers WHERE AgentApiKey = @ApiKeyHash AND IsActive = 1",
            new { ApiKeyHash = apiKeyHash });
    }

    public async Task<int> InsertAsync(Server server)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>("""
            INSERT INTO Servers (Name, Hostname, IpAddress, Environment, [Group], Description,
                AgentApiKey, Status, IsActive, CreatedAt, UpdatedAt, CreatedBy)
            VALUES (@Name, @Hostname, @IpAddress, @Environment, @Group, @Description,
                @AgentApiKey, @Status, @IsActive, @CreatedAt, @UpdatedAt, @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, server);
    }

    public async Task UpdateAsync(Server server)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE Servers SET
                Name = @Name, Hostname = @Hostname, IpAddress = @IpAddress,
                Environment = @Environment, [Group] = @Group, Description = @Description,
                AgentApiKey = @AgentApiKey, UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """, server);
    }

    public async Task UpdateStatusAsync(int id, string status, string? connectionId, DateTime? lastHeartbeat)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE Servers SET
                Status = @Status,
                AgentConnectionId = @ConnectionId,
                LastHeartbeat = @LastHeartbeat,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
            """, new { Id = id, Status = status, ConnectionId = connectionId, LastHeartbeat = lastHeartbeat });
    }

    public async Task MarkStaleServersOfflineAsync(DateTime cutoff)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE Servers SET
                Status = 'Offline',
                AgentConnectionId = NULL,
                UpdatedAt = GETUTCDATE()
            WHERE IsActive = 1
              AND Status = 'Online'
              AND (LastHeartbeat IS NULL OR LastHeartbeat < @Cutoff)
            """, new { Cutoff = cutoff });
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Servers SET IsActive = 0, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { Id = id });
    }
}
