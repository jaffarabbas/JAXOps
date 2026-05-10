using IISManager.Domain.Enums;

namespace IISManager.Application.DTOs;

public class DeploymentDto
{
    public int Id { get; set; }
    public Guid CorrelationId { get; set; }
    public int ApplicationId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DeploymentStatus Status { get; set; }
    public string StatusLabel => Status.ToString();
    public DeploymentMode Mode { get; set; }
    public string DeployedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsRollback { get; set; }
    public string? FailureReason { get; set; }
    public List<DeploymentTargetDto> Targets { get; set; } = new();
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;
}

public class DeploymentTargetDto
{
    public int Id { get; set; }
    public int ServerId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string WebsiteName { get; set; } = string.Empty;
    public string AppPoolName { get; set; } = string.Empty;
    public DeploymentStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}

public class CreateDeploymentDto
{
    public int ApplicationId { get; set; }
    public int PackageId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DeploymentMode Mode { get; set; } = DeploymentMode.Sequential;
    public string? Notes { get; set; }
    public List<DeploymentTargetInput> Targets { get; set; } = new();
}

public class DeploymentTargetInput
{
    public int ServerId { get; set; }
    public string WebsiteName { get; set; } = string.Empty;
    public string AppPoolName { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
}

public class DeploymentLogDto
{
    public long Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
    public DateTime Timestamp { get; set; }
    public int? ServerId { get; set; }
}
