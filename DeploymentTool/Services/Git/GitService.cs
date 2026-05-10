using System.Diagnostics;
using System.IO;
using System.Text;
using DeploymentTool.Models.Git;

namespace DeploymentTool.Services.Git;

public class GitService : IGitService
{
    private static async Task<(string Output, string Error, int ExitCode)> RunAsync(
        string workingDir,
        string arguments,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo("git")
        {
            Arguments              = arguments,
            WorkingDirectory       = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error  = new StringBuilder();

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(ct);
        return (output.ToString().Trim(), error.ToString().Trim(), proc.ExitCode);
    }

    public async Task<bool> IsValidRepositoryAsync(string path, CancellationToken ct = default)
    {
        if (!Directory.Exists(path)) return false;
        var (_, _, exit) = await RunAsync(path, "rev-parse --git-dir", ct);
        return exit == 0;
    }

    public async Task<GitStatusModel> GetStatusAsync(string path, CancellationToken ct = default)
    {
        var model = new GitStatusModel();

        var (branch, _, _) = await RunAsync(path, "rev-parse --abbrev-ref HEAD", ct);
        model.CurrentBranch = branch.Trim();

        var (statusOut, _, _) = await RunAsync(path, "status --porcelain", ct);
        if (!string.IsNullOrWhiteSpace(statusOut))
        {
            var lines = statusOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            model.IsClean        = false;
            model.ModifiedFiles  = lines.Select(l => l.Length > 3 ? l[3..].Trim() : l.Trim()).ToList();
            model.ModifiedCount  = lines.Count(l => !l.StartsWith("??"));
            model.UntrackedCount = lines.Count(l => l.StartsWith("??"));
        }
        else
        {
            model.IsClean = true;
        }

        var (logOut, _, _) = await RunAsync(path, "log -1 --format=%H%x09%s%x09%cd --date=short", ct);
        if (!string.IsNullOrWhiteSpace(logOut))
        {
            var parts = logOut.Split('\t', 3);
            if (parts.Length >= 1) model.LastCommitSha     = parts[0].Trim();
            if (parts.Length >= 2) model.LastCommitMessage = parts[1].Trim();
            if (parts.Length >= 3 && DateTime.TryParse(parts[2].Trim(), out var d))
                model.LastCommitDate = d;
        }

        return model;
    }

    public async Task<List<GitBranchModel>> GetBranchesAsync(string path, CancellationToken ct = default)
    {
        var (currentBranch, _, _) = await RunAsync(path, "rev-parse --abbrev-ref HEAD", ct);
        currentBranch = currentBranch.Trim();

        var (localOut, _, _)  = await RunAsync(path, "branch --format=%(refname:short)", ct);
        var (remoteOut, _, _) = await RunAsync(path, "branch -r --format=%(refname:short)", ct);

        var result = new List<GitBranchModel>();

        foreach (var line in localOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = line.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            result.Add(new GitBranchModel
            {
                Name            = name,
                IsRemote        = false,
                IsCurrentBranch = name == currentBranch
            });
        }

        foreach (var line in remoteOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = line.Trim();
            if (string.IsNullOrEmpty(name) || name.Contains("->")) continue;
            result.Add(new GitBranchModel
            {
                Name     = name,
                IsRemote = true,
                IsCurrentBranch = false
            });
        }

        return result;
    }

    public async Task<bool> PullAsync(string path, IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Pulling latest changes…");
        var (output, stderr, exit) = await RunAsync(path, "pull --rebase=false", ct);
        ReportLines(output, progress);
        ReportLines(stderr, progress, "[stderr] ");
        return exit == 0;
    }

    public async Task<bool> FetchAsync(string path, IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report("Fetching all remotes…");
        var (output, stderr, exit) = await RunAsync(path, "fetch --all --prune", ct);
        ReportLines(output, progress);
        ReportLines(stderr, progress);
        return exit == 0;
    }

    public async Task<bool> CheckoutBranchAsync(string path, string branchName, CancellationToken ct = default)
    {
        var (_, _, exit) = await RunAsync(path, $"checkout {branchName}", ct);
        if (exit == 0) return true;

        var (_, _, exit2) = await RunAsync(path, $"checkout --track origin/{branchName}", ct);
        return exit2 == 0;
    }

    public async Task<List<string>> GetCommitLogAsync(string path, int count = 10, CancellationToken ct = default)
    {
        var (output, _, _) = await RunAsync(path, $"log -{count} --oneline --no-decorate", ct);
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
    }

    public async Task<string> RunRawCommandAsync(string path, string arguments, CancellationToken ct = default)
    {
        var (output, error, _) = await RunAsync(path, arguments, ct);
        return string.IsNullOrEmpty(error) ? output : $"{output}\n{error}".Trim();
    }

    private static void ReportLines(string text, IProgress<string> progress, string prefix = "")
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            progress.Report(prefix + line);
    }
}
