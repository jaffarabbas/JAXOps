namespace IISManager.Domain.Enums;

public enum DeploymentStatus
{
    Queued = 0,
    InProgress = 1,
    Succeeded = 2,
    Failed = 3,
    RolledBack = 4,
    Cancelled = 5,
    PartialSuccess = 6
}
