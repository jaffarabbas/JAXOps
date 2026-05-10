using DeploymentTool.Models.Sync;

namespace DeploymentTool.Models.Patch;

public class PatchGenerationRequest
{
    public string PatchName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string PatchOutputRoot { get; set; } = string.Empty;
    public string GitBranch { get; set; } = string.Empty;
    public string GitCommitSha { get; set; } = string.Empty;
    public string GitCommitMessage { get; set; } = string.Empty;
    public string BuildConfiguration { get; set; } = string.Empty;
    public string MainCodePath { get; set; } = string.Empty;
    public string MiniCodePath { get; set; } = string.Empty;
    public bool IncludeManifest { get; set; } = true;
    public List<ChangedFileModel> Files { get; set; } = [];
}
