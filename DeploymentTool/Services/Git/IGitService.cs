using DeploymentTool.Models.Git;

namespace DeploymentTool.Services.Git;

public interface IGitService
{
    Task<bool> IsValidRepositoryAsync(string path, CancellationToken ct = default);
    Task<GitStatusModel> GetStatusAsync(string path, CancellationToken ct = default);
    Task<List<GitBranchModel>> GetBranchesAsync(string path, CancellationToken ct = default);
    Task<bool> PullAsync(string path, IProgress<string> progress, CancellationToken ct = default);
    Task<bool> FetchAsync(string path, IProgress<string> progress, CancellationToken ct = default);
    Task<bool> CheckoutBranchAsync(string path, string branchName, CancellationToken ct = default);
    Task<List<string>> GetCommitLogAsync(string path, int count = 10, CancellationToken ct = default);
    Task<string> RunRawCommandAsync(string path, string arguments, CancellationToken ct = default);
}
