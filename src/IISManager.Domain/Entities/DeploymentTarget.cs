using IISManager.Domain.Enums;

namespace IISManager.Domain.Entities;

public class DeploymentTarget
{
    public int Id { get; set; }
    public int DeploymentId { get; set; }
    public int ServerId { get; set; }
    public string? ServerName { get; set; }
    public string WebsiteName { get; set; } = string.Empty;
    public string AppPoolName { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Queued;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}
