using Dapper;
using IISManager.Domain.Entities;
using IISManager.Domain.Enums;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;

namespace IISManager.Infrastructure.Repositories;

public class DeploymentRepository : IDeploymentRepository
{
    private readonly IDatabaseFactory _db;
    public DeploymentRepository(IDatabaseFactory db) => _db = db;

    public async Task<Deployment?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Deployment>("""
            SELECT d.*, a.Name AS ApplicationName
            FROM Deployments d
            LEFT JOIN Applications a ON a.Id = d.ApplicationId
            WHERE d.Id = @Id
            """, new { Id = id });
    }

    public async Task<Deployment?> GetByCorrelationIdAsync(Guid correlationId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Deployment>(
            "SELECT * FROM Deployments WHERE CorrelationId = @CorrelationId",
            new { CorrelationId = correlationId });
    }

    public async Task<IEnumerable<Deployment>> GetPagedAsync(int page, int pageSize, int? applicationId = null, DeploymentStatus? status = null)
    {
        using var conn = _db.CreateConnection();
        var where = BuildWhereClause(applicationId, status);
        return await conn.QueryAsync<Deployment>($"""
            SELECT d.*, a.Name AS ApplicationName
            FROM Deployments d
            LEFT JOIN Applications a ON a.Id = d.ApplicationId
            {where}
            ORDER BY d.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, new { Offset = (page - 1) * pageSize, PageSize = pageSize, ApplicationId = applicationId, Status = status?.ToString() });
    }

    public async Task<int> CountAsync(int? applicationId = null, DeploymentStatus? status = null)
    {
        using var conn = _db.CreateConnection();
        var where = BuildWhereClause(applicationId, status);
        return await conn.QuerySingleAsync<int>($"SELECT COUNT(1) FROM Deployments d {where}",
            new { ApplicationId = applicationId, Status = status?.ToString() });
    }

    public async Task<int> InsertAsync(Deployment deployment)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>("""
            INSERT INTO Deployments (CorrelationId, ApplicationId, PackageId, Version, Status, Mode,
                DeployedBy, CreatedAt, Notes, IsRollback, RollbackTargetDeploymentId)
            VALUES (@CorrelationId, @ApplicationId, @PackageId, @Version, @Status, @Mode,
                @DeployedBy, @CreatedAt, @Notes, @IsRollback, @RollbackTargetDeploymentId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, deployment);
    }

    public async Task UpdateStatusAsync(int id, DeploymentStatus status, DateTime? startedAt = null,
        DateTime? completedAt = null, string? failureReason = null)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE Deployments SET
                Status = @Status,
                StartedAt = COALESCE(@StartedAt, StartedAt),
                CompletedAt = COALESCE(@CompletedAt, CompletedAt),
                FailureReason = COALESCE(@FailureReason, FailureReason)
            WHERE Id = @Id
            """, new { Id = id, Status = status.ToString(), StartedAt = startedAt, CompletedAt = completedAt, FailureReason = failureReason });
    }

    public async Task<IEnumerable<DeploymentTarget>> GetTargetsAsync(int deploymentId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<DeploymentTarget>("""
            SELECT dt.*, s.Name AS ServerName
            FROM DeploymentTargets dt
            LEFT JOIN Servers s ON s.Id = dt.ServerId
            WHERE dt.DeploymentId = @DeploymentId
            """, new { DeploymentId = deploymentId });
    }

    public async Task InsertTargetAsync(DeploymentTarget target)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO DeploymentTargets (DeploymentId, ServerId, WebsiteName, AppPoolName, PhysicalPath, Status)
            VALUES (@DeploymentId, @ServerId, @WebsiteName, @AppPoolName, @PhysicalPath, @Status)
            """, target);
    }

    public async Task UpdateTargetStatusAsync(int targetId, DeploymentStatus status, DateTime? startedAt = null,
        DateTime? completedAt = null, string? failureReason = null)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE DeploymentTargets SET
                Status = @Status,
                StartedAt = COALESCE(@StartedAt, StartedAt),
                CompletedAt = COALESCE(@CompletedAt, CompletedAt),
                FailureReason = COALESCE(@FailureReason, FailureReason)
            WHERE Id = @Id
            """, new { Id = targetId, Status = status.ToString(), StartedAt = startedAt, CompletedAt = completedAt, FailureReason = failureReason });
    }

    public async Task<IEnumerable<DeploymentLog>> GetLogsAsync(int deploymentId, int? serverId = null)
    {
        using var conn = _db.CreateConnection();
        var filter = serverId.HasValue ? "AND ServerId = @ServerId" : string.Empty;
        return await conn.QueryAsync<DeploymentLog>($"""
            SELECT * FROM DeploymentLogs
            WHERE DeploymentId = @DeploymentId {filter}
            ORDER BY Timestamp ASC
            """, new { DeploymentId = deploymentId, ServerId = serverId });
    }

    public async Task InsertLogAsync(DeploymentLog log)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO DeploymentLogs (DeploymentId, ServerId, Message, Level, Timestamp)
            VALUES (@DeploymentId, @ServerId, @Message, @Level, @Timestamp)
            """, log);
    }

    public async Task<Deployment?> GetLastSuccessfulAsync(int applicationId, int serverId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Deployment>("""
            SELECT TOP 1 d.*
            FROM Deployments d
            JOIN DeploymentTargets dt ON dt.DeploymentId = d.Id
            WHERE d.ApplicationId = @ApplicationId
              AND dt.ServerId = @ServerId
              AND dt.Status = 'Succeeded'
            ORDER BY d.CompletedAt DESC
            """, new { ApplicationId = applicationId, ServerId = serverId });
    }

    private static string BuildWhereClause(int? applicationId, DeploymentStatus? status)
    {
        var conditions = new List<string>();
        if (applicationId.HasValue) conditions.Add("d.ApplicationId = @ApplicationId");
        if (status.HasValue) conditions.Add("d.Status = @Status");
        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
    }
}
