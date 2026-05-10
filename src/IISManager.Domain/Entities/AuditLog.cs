using IISManager.Domain.Enums;

namespace IISManager.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Description { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime PerformedAt { get; set; }
}
