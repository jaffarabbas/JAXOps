using IISManager.Contracts.Enums;

namespace IISManager.Contracts.Commands;

public class DeployApplicationCommand : AgentCommandBase
{
    public DeployApplicationCommand() => CommandType = CommandType.DeployApplication;

    public int DeploymentId { get; set; }
    public int DeploymentTargetId { get; set; }
    public Guid CorrelationId { get; set; }
    public string PackageUrl { get; set; } = string.Empty;
    public string PackageSha256 { get; set; } = string.Empty;
    public string WebsiteName { get; set; } = string.Empty;
    public string AppPoolName { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, string>? ConfigOverrides { get; set; }
    public bool CreateBackup { get; set; } = true;
}
