namespace DeploymentTool.Models.Patch;

public class PatchManifestModel
{
    public string PatchName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string GitBranch { get; set; } = string.Empty;
    public string GitCommitSha { get; set; } = string.Empty;
    public string GitCommitMessage { get; set; } = string.Empty;
    public string BuildConfiguration { get; set; } = string.Empty;
    public string MainCodePath { get; set; } = string.Empty;
    public string MiniCodePath { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public List<PatchFileEntry> Files { get; set; } = [];
}

public class PatchFileEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string LastWriteTime { get; set; } = string.Empty;
}
