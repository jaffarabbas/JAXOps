namespace IISManager.AgentService;

public class AgentConfiguration
{
    public int ServerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string PortalUrl { get; set; } = string.Empty;
    public string[] AllowedDeployPaths { get; set; } = Array.Empty<string>();
    public string BackupBasePath { get; set; } = @"C:\IISManagerBackups";
    public string LockFileDirectory { get; set; } = @"C:\IISManagerLocks";
    public int MaxConcurrentDeployments { get; set; } = 2;
    public bool AllowInsecureSsl { get; set; } = false;
}
