using Dapper;
using IISManager.Domain.Entities;
using IISManager.Domain.Enums;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;

namespace IISManager.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly IDatabaseFactory _db;
    public AuditLogRepository(IDatabaseFactory db) => _db = db;

    public async Task<IEnumerable<AuditLog>> GetPagedAsync(int page, int pageSize, AuditAction? action = null, string? user = null)
    {
        using var conn = _db.CreateConnection();
        var where = BuildWhere(action, user);
        return await conn.QueryAsync<AuditLog>($"""
            SELECT * FROM AuditLogs {where}
            ORDER BY PerformedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, new { Offset = (page - 1) * pageSize, PageSize = pageSize, Action = action?.ToString(), User = user });
    }

    public async Task<int> CountAsync(AuditAction? action = null, string? user = null)
    {
        using var conn = _db.CreateConnection();
        var where = BuildWhere(action, user);
        return await conn.QuerySingleAsync<int>($"SELECT COUNT(1) FROM AuditLogs {where}",
            new { Action = action?.ToString(), User = user });
    }

    public async Task InsertAsync(AuditLog log)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO AuditLogs (Action, EntityType, EntityId, Description, OldValues, NewValues,
                PerformedBy, IpAddress, PerformedAt)
            VALUES (@Action, @EntityType, @EntityId, @Description, @OldValues, @NewValues,
                @PerformedBy, @IpAddress, @PerformedAt)
            """, log);
    }

    private static string BuildWhere(AuditAction? action, string? user)
    {
        var conditions = new List<string>();
        if (action.HasValue) conditions.Add("Action = @Action");
        if (!string.IsNullOrEmpty(user)) conditions.Add("PerformedBy = @User");
        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
    }
}
