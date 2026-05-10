using Dapper;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;
using ApplicationEntity = IISManager.Domain.Entities.Application;

namespace IISManager.Infrastructure.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly IDatabaseFactory _db;
    public ApplicationRepository(IDatabaseFactory db) => _db = db;

    public async Task<ApplicationEntity?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ApplicationEntity>(
            "SELECT * FROM Applications WHERE Id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<ApplicationEntity>> GetAllActiveAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ApplicationEntity>(
            "SELECT * FROM Applications WHERE IsActive = 1 ORDER BY Name");
    }

    public async Task<int> InsertAsync(ApplicationEntity application)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>("""
            INSERT INTO Applications
                (Name, Description, DefaultServerId, DefaultWebsiteName, DefaultAppPoolName, PhysicalPath, IsActive, CreatedAt, CreatedBy)
            VALUES
                (@Name, @Description, @DefaultServerId, @DefaultWebsiteName, @DefaultAppPoolName, @PhysicalPath, @IsActive, @CreatedAt, @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, application);
    }

    public async Task UpdateAsync(ApplicationEntity application)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE Applications SET
                Name = @Name, Description = @Description,
                DefaultServerId = @DefaultServerId, DefaultWebsiteName = @DefaultWebsiteName,
                DefaultAppPoolName = @DefaultAppPoolName, PhysicalPath = @PhysicalPath,
                IsActive = @IsActive
            WHERE Id = @Id
            """, application);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("UPDATE Applications SET IsActive = 0 WHERE Id = @Id", new { Id = id });
    }
}
