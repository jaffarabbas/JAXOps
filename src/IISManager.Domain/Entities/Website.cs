using IISManager.Domain.Enums;

namespace IISManager.Domain.Entities;

public class Website
{
    public int Id { get; set; }
    public int ServerId { get; set; }
    public long IISId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public WebsiteStatus Status { get; set; }
    public string DefaultAppPool { get; set; } = string.Empty;
    public string BindingsJson { get; set; } = "[]";
    public DateTime LastSyncedAt { get; set; }
}
