namespace IISManager.Shared.Constants;

public static class AgentConstants
{
    public const string ServiceName = "IISManagerAgent";
    public const string ServiceDisplayName = "IIS Manager Agent";
    public const string ServiceDescription = "Deployment and IIS management agent for IIS Manager Platform";

    public const string AppOfflineFileName = "app_offline.htm";
    public const string AppOfflineContent = """
        <!DOCTYPE html>
        <html><head><title>Maintenance</title></head>
        <body><h1>Application is being updated. Please try again in a few minutes.</h1></body>
        </html>
        """;

    public const string LockFileExtension = ".lock";
    public const string BackupFolderName = "_iismanager_backups";
    public const int DeploymentTimeoutMinutes = 30;
    public const int HealthCheckRetries = 3;
    public const int HealthCheckDelaySeconds = 10;
    public const int MaxBackupAgedays = 30;
}
