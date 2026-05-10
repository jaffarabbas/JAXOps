using System.IO;
using DeploymentTool.Models;

namespace DeploymentTool.Services;

public class AppSettingsService
{
    private static readonly string[] Patterns = ["appsettings.json", "web.config"];

    public Task<List<ConfigFileItem>> LoadConfigFilesAsync(IList<ProjectItem> projects)
        => Task.Run(() => Discover(projects));

    private static List<ConfigFileItem> Discover(IList<ProjectItem> projects)
    {
        var result = new List<ConfigFileItem>();

        foreach (var project in projects)
        {
            // For published-folder items the Path is already the directory.
            // For source projects the Path is the .csproj file so we need its parent.
            var dir = project.IsPublishedFolder
                ? project.Path
                : Path.GetDirectoryName(project.Path);

            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                continue;

            foreach (var pattern in Patterns)
            foreach (var file in FindFiles(dir, pattern, skipBuildDirs: !project.IsPublishedFolder))
                result.Add(new ConfigFileItem(file, project.Name, dir));
        }

        return result;
    }

    private static IEnumerable<string> FindFiles(string dir, string pattern, bool skipBuildDirs = true)
    {
        return Directory
            .EnumerateFiles(dir, pattern, new EnumerationOptions
            {
                IgnoreInaccessible    = true,
                RecurseSubdirectories = true,
                MaxRecursionDepth     = 4
            })
            .Where(f =>
            {
                if (!skipBuildDirs) return true;
                var rel = Path.GetRelativePath(dir, f);
                return !rel.StartsWith("bin", StringComparison.OrdinalIgnoreCase)
                    && !rel.StartsWith("obj", StringComparison.OrdinalIgnoreCase);
            });
    }
}
