using System.IO;
using DeploymentTool.Models;

namespace DeploymentTool.Services;

public class ProjectScannerService
{
    public async Task<List<ProjectItem>> ScanAsync(string rootPath)
    {
        return await Task.Run(() =>
        {
            var projects = new List<ProjectItem>();

            // Try source-code scan first (look for .csproj files)
            var csprojFiles = Directory
                .EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories)
                .ToList();

            if (csprojFiles.Count > 0)
            {
                foreach (var file in csprojFiles)
                    projects.Add(new ProjectItem
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Path = file
                    });
                return projects;
            }

            // No .csproj files found — try to detect published output folders.
            // Case 1: root itself is a published folder (single-project publish output)
            if (IsPublishedFolder(rootPath))
            {
                projects.Add(new ProjectItem
                {
                    Name             = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    Path             = rootPath,
                    IsPublishedFolder = true
                });
                return projects;
            }

            // Case 2: root contains subdirectories each of which is a published project/microservice
            foreach (var subDir in Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsPublishedFolder(subDir))
                    projects.Add(new ProjectItem
                    {
                        Name             = Path.GetFileName(subDir),
                        Path             = subDir,
                        IsPublishedFolder = true
                    });
            }

            return projects;
        });
    }

    /// <summary>
    /// Returns true when <paramref name="dir"/> looks like a .NET published output folder.
    /// Markers: *.runtimeconfig.json, *.deps.json, *.exe, or appsettings.json directly in the folder.
    /// </summary>
    private static bool IsPublishedFolder(string dir)
    {
        if (!Directory.Exists(dir)) return false;

        return Directory.EnumerateFiles(dir, "*.runtimeconfig.json", SearchOption.TopDirectoryOnly).Any()
            || Directory.EnumerateFiles(dir, "*.deps.json",          SearchOption.TopDirectoryOnly).Any()
            || Directory.EnumerateFiles(dir, "*.exe",                SearchOption.TopDirectoryOnly).Any()
            || File.Exists(Path.Combine(dir, "appsettings.json"));
    }
}
