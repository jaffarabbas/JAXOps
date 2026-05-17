using System.IO;
using DeploymentTool.Models.Git;

namespace DeploymentTool.Services;

public class GitRepoScannerService
{
    private static readonly string[] ProjectPatterns = ["*.sln", "*.vbproj", "*.csproj"];

    public Task<List<GitRepositoryConfig>> ScanAsync(string rootPath)
        => Task.Run(() => Discover(rootPath));

    private static List<GitRepositoryConfig> Discover(string rootPath)
    {
        var result = new List<GitRepositoryConfig>();
        if (!Directory.Exists(rootPath)) return result;

        foreach (var dir in Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
        {
            if (!IsEligible(dir)) continue;
            result.Add(new GitRepositoryConfig
            {
                Name          = Path.GetFileName(dir),
                LocalPath     = dir,
                DefaultBranch = "main"
            });
        }

        return result;
    }

    private static bool IsEligible(string dir)
    {
        if (Directory.Exists(Path.Combine(dir, ".git"))) return true;

        foreach (var pattern in ProjectPatterns)
            if (Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly).Any())
                return true;

        return false;
    }
}
