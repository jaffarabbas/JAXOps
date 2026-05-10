using Dapper;
using IISManager.Domain.Entities;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;

namespace IISManager.Infrastructure.Repositories;

public class PackageRepository : IPackageRepository
{
    private readonly IDatabaseFactory _db;
    public PackageRepository(IDatabaseFactory db) => _db = db;

    public async Task<DeploymentPackage?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<DeploymentPackage>(
            "SELECT * FROM DeploymentPackages WHERE Id = @Id AND IsDeleted = 0", new { Id = id });
    }

    public async Task<IEnumerable<DeploymentPackage>> GetByApplicationIdAsync(int applicationId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<DeploymentPackage>("""
            SELECT * FROM DeploymentPackages
            WHERE ApplicationId = @ApplicationId AND IsDeleted = 0
            ORDER BY UploadedAt DESC
            """, new { ApplicationId = applicationId });
    }

    public async Task<int> InsertAsync(DeploymentPackage package)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>("""
            INSERT INTO DeploymentPackages (FileName, StoredPath, Sha256Hash, SizeBytes, Version,
                ApplicationId, UploadedAt, UploadedBy, IsDeleted)
            VALUES (@FileName, @StoredPath, @Sha256Hash, @SizeBytes, @Version,
                @ApplicationId, @UploadedAt, @UploadedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, package);
    }

    public async Task MarkDeletedAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("UPDATE DeploymentPackages SET IsDeleted = 1 WHERE Id = @Id", new { Id = id });
    }
}
