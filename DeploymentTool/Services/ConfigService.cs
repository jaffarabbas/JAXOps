using DeploymentTool.Models;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

namespace DeploymentTool.Services;

public class ConfigService
{
    private readonly string _settingsPath;

    public AppSettings Settings { get; }

    public ConfigService()
    {
        _settingsPath = ResolveSettingsPath();

        var config = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(_settingsPath)!)
            .AddJsonFile(Path.GetFileName(_settingsPath), optional: false, reloadOnChange: false)
            .Build();

        Settings = config.Get<AppSettings>() ?? new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_settingsPath, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Settings.Codebases = settings.Codebases
            .Select(c => new Codebase { Name = c.Name, Path = c.Path })
            .ToList();
        Settings.PublishOutputRoot = settings.PublishOutputRoot;
        Settings.PatchOutputRoot = settings.PatchOutputRoot;
        Settings.IsDarkMode = settings.IsDarkMode;
    }

    private static string ResolveSettingsPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var current = new DirectoryInfo(baseDir);

        while (current != null)
        {
            var csprojPath = Path.Combine(current.FullName, "DeploymentTool.csproj");
            var appsettingsPath = Path.Combine(current.FullName, "appsettings.json");

            if (File.Exists(csprojPath) && File.Exists(appsettingsPath))
                return appsettingsPath;

            current = current.Parent;
        }

        return Path.Combine(baseDir, "appsettings.json");
    }
}
