using IISManager.Domain.Enums;

namespace IISManager.Domain.Entities;

public class Deployment
{
    public int Id { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public int ApplicationId { get; set; }
    public string? ApplicationName { get; set; }
    public int PackageId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Queued;
    public DeploymentMode Mode { get; set; } = DeploymentMode.Sequential;
    public string DeployedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsRollback { get; set; }
    public int? RollbackTargetDeploymentId { get; set; }
    public string? FailureReason { get; set; }
}
