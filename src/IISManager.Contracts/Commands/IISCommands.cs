using IISManager.Contracts.Enums;

namespace IISManager.Contracts.Commands;

public class WebsiteCommand : AgentCommandBase
{
    public string WebsiteName { get; set; } = string.Empty;
    public string? PhysicalPath { get; set; }
    public string? AppPoolName { get; set; }
    public List<BindingInfo>? Bindings { get; set; }
}

public class AppPoolCommand : AgentCommandBase
{
    public string AppPoolName { get; set; } = string.Empty;
    public string? RuntimeVersion { get; set; }
    public string? PipelineMode { get; set; }
}

public class RollbackCommand : AgentCommandBase
{
    public RollbackCommand() => CommandType = CommandType.RollbackDeployment;

    public int DeploymentId { get; set; }
    public int DeploymentTargetId { get; set; }
    public string WebsiteName { get; set; } = string.Empty;
    public string AppPoolName { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
}

public class BindingInfo
{
    public string Protocol { get; set; } = "http";
    public string IpAddress { get; set; } = "*";
    public int Port { get; set; } = 80;
    public string? HostName { get; set; }
    public string? CertificateThumbprint { get; set; }
}
