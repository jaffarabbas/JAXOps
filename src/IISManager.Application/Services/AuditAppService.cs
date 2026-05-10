using IISManager.Application.Interfaces;
using IISManager.Domain.Entities;
using IISManager.Domain.Enums;
using IISManager.Domain.Interfaces;
using IISManager.Shared.Models;

namespace IISManager.Application.Services;

public class AuditAppService : IAuditAppService
{
    private readonly IAuditLogRepository _auditLogs;

    public AuditAppService(IAuditLogRepository auditLogs) => _auditLogs = auditLogs;

    public async Task<PagedResult<AuditLog>> GetPagedAsync(int page, int pageSize, AuditAction? action = null, string? user = null)
    {
        var items = await _auditLogs.GetPagedAsync(page, pageSize, action, user);
        var count = await _auditLogs.CountAsync(action, user);
        return PagedResult<AuditLog>.Create(items, count, page, pageSize);
    }

    public async Task LogAsync(AuditAction action, string entityType, string? entityId, string description,
        string performedBy, string? ipAddress = null, string? oldValues = null, string? newValues = null)
    {
        await _auditLogs.InsertAsync(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            OldValues = oldValues,
            NewValues = newValues,
            PerformedBy = performedBy,
            IpAddress = ipAddress,
            PerformedAt = DateTime.UtcNow
        });
    }
}
