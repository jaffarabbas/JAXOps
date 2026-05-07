namespace DeploymentTool.Models;

public class DeploymentProfile
{
    public string ServerHostname { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DeploymentRootPath { get; set; } = string.Empty;
    // Optional custom SMB share name (e.g. "wwwroot"). When set, the UNC path
    // becomes \\SERVER\ShareName instead of the default \\SERVER\C$ admin share.
    public string ShareName { get; set; } = string.Empty;
    public string IisSiteName { get; set; } = string.Empty;
    public string AppPoolName { get; set; } = string.Empty;
    public string ApplicationAlias { get; set; } = string.Empty;
    public bool CreateAppPool { get; set; } = true;
    public bool CreateIisApplication { get; set; } = true;
    public bool StartAppPoolAfterDeploy { get; set; } = true;
    public bool OverwriteExistingFiles { get; set; } = true;
    public string DotNetClrVersion { get; set; } = "v4.0";
    public string PipelineMode { get; set; } = "Integrated";
    public bool Enable32Bit { get; set; } = false;

    // When true the orchestrator uses per-project pool settings from ProjectDeployConfig.
    // When false it falls back to the single AppPoolName / CLR / pipeline fields above.
    public bool UsePerProjectPools { get; set; } = false;

    // ── FTP transfer mode ────────────────────────────────────────────────────
    public bool   UseFtp         { get; set; } = false;
    public int    FtpPort        { get; set; } = 21;
    // FTP path that maps to DeploymentRootPath on the server (e.g. /wwwroot).
    // The orchestrator appends the app alias to build the upload URL.
    public string FtpRootPath    { get; set; } = "/";
    public bool   FtpPassiveMode { get; set; } = true;
}
