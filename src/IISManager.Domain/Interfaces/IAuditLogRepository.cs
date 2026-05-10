using IISManager.Domain.Entities;
using IISManager.Domain.Enums;

namespace IISManager.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task<IEnumerable<AuditLog>> GetPagedAsync(int page, int pageSize, AuditAction? action = null, string? user = null);
    Task<int> CountAsync(AuditAction? action = null, string? user = null);
    Task InsertAsync(AuditLog log);
}
