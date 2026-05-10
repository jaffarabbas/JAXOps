using IISManager.Contracts.Enums;

namespace IISManager.Contracts.Events;

public class DeploymentProgressEvent : AgentEventBase
{
    public DeploymentProgressEvent() => EventType = EventType.DeploymentProgress;

    public int DeploymentId { get; set; }
    public int DeploymentTargetId { get; set; }
    public Guid CorrelationId { get; set; }
    public int PercentComplete { get; set; }
    public string Step { get; set; } = string.Empty;
    public string Status { get; set; } = "InProgress";
}

public class DeploymentLogLineEvent : AgentEventBase
{
    public DeploymentLogLineEvent() => EventType = EventType.DeploymentLogLine;

    public int DeploymentId { get; set; }
    public int DeploymentTargetId { get; set; }
    public Guid CorrelationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
}

public class DeploymentCompletedEvent : AgentEventBase
{
    public DeploymentCompletedEvent() => EventType = EventType.DeploymentCompleted;

    public int DeploymentId { get; set; }
    public int DeploymentTargetId { get; set; }
    public Guid CorrelationId { get; set; }
    public bool Success { get; set; }
    public string? BackupPath { get; set; }
    public string? FailureReason { get; set; }
    public TimeSpan Duration { get; set; }
}
