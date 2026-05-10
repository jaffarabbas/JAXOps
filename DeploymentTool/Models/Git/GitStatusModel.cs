namespace DeploymentTool.Models.Git;

public class GitStatusModel
{
    public string CurrentBranch { get; set; } = string.Empty;
    public bool IsClean { get; set; }
    public int ModifiedCount { get; set; }
    public int UntrackedCount { get; set; }
    public string LastCommitSha { get; set; } = string.Empty;
    public string LastCommitShort => LastCommitSha.Length >= 7 ? LastCommitSha[..7] : LastCommitSha;
    public string LastCommitMessage { get; set; } = string.Empty;
    public DateTime LastCommitDate { get; set; }
    public List<string> ModifiedFiles { get; set; } = [];
}
