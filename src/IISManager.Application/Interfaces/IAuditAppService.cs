using IISManager.Domain.Entities;
using IISManager.Domain.Enums;
using IISManager.Shared.Models;

namespace IISManager.Application.Interfaces;

public interface IAuditAppService
{
    Task<PagedResult<AuditLog>> GetPagedAsync(int page, int pageSize, AuditAction? action = null, string? user = null);
    Task LogAsync(AuditAction action, string entityType, string? entityId, string description, string performedBy, string? ipAddress = null, string? oldValues = null, string? newValues = null);
}
