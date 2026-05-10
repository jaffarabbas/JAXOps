namespace IISManager.Domain.Enums;

public enum AuditAction
{
    ServerRegistered,
    ServerDeleted,
    ServerUpdated,
    DeploymentStarted,
    DeploymentSucceeded,
    DeploymentFailed,
    DeploymentRolledBack,
    WebsiteStarted,
    WebsiteStopped,
    WebsiteRestarted,
    WebsiteCreated,
    WebsiteDeleted,
    AppPoolStarted,
    AppPoolStopped,
    AppPoolRecycled,
    AppPoolCreated,
    AppPoolDeleted,
    UserLoggedIn,
    UserLoggedOut,
    PackageUploaded,
    AgentConnected,
    AgentDisconnected
}
