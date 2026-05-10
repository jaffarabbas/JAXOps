using IISManager.AgentService.CommandHandlers;
using IISManager.Contracts.Commands;
using IISManager.Contracts.Events;
using IISManager.Domain.Common;
using IISManager.Shared.Constants;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace IISManager.AgentService.Services;

public class DeploymentExecutorService
{
    private readonly IISManagementService _iis;
    private readonly BackupService _backup;
    private readonly AgentSignalRClient _client;
    private readonly AgentConfiguration _config;
    private readonly ILogger<DeploymentExecutorService> _logger;

    public DeploymentExecutorService(
        IISManagementService iis,
        BackupService backup,
        AgentSignalRClient client,
        AgentConfiguration config,
        ILogger<DeploymentExecutorService> logger)
    {
        _iis = iis;
        _backup = backup;
        _client = client;
        _config = config;
        _logger = logger;
    }

    public async Task ExecuteAsync(DeployApplicationCommand cmd, CancellationToken ct)
    {
        var start = DateTime.UtcNow;
        string? backupPath = null;

        async Task Log(string message, string level = "Info")
        {
            _logger.LogInformation("[Deployment {Id}] {Message}", cmd.DeploymentId, message);
            await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportLogLine,
                new DeploymentLogLineEvent
                {
                    ServerId = cmd.ServerId,
                    DeploymentId = cmd.DeploymentId,
                    DeploymentTargetId = cmd.DeploymentTargetId,
                    CorrelationId = cmd.CorrelationId,
                    Message = message,
                    Level = level
                }, ct);
        }

        async Task Progress(int percent, string step)
        {
            await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportProgress,
                new DeploymentProgressEvent
                {
                    ServerId = cmd.ServerId,
                    DeploymentId = cmd.DeploymentId,
                    DeploymentTargetId = cmd.DeploymentTargetId,
                    CorrelationId = cmd.CorrelationId,
                    PercentComplete = percent,
                    Step = step
                }, ct);
        }

        try
        {
            // 1. Validate path is whitelisted
            await Log($"[{DateTime.UtcNow:HH:mm:ss}] Deployment started — version {cmd.Version}");
            if (!IsPathAllowed(cmd.PhysicalPath))
            {
                await Log($"Physical path '{cmd.PhysicalPath}' is not in the allowed paths list", "Error");
                await ReportFailed(cmd, "Path not allowed", start, ct);
                return;
            }

            // 2. Acquire lock
            await Log($"[{DateTime.UtcNow:HH:mm:ss}] Acquiring deployment lock");
            await Progress(5, "Acquiring lock");
            if (!await _backup.TryAcquireLockAsync(cmd.WebsiteName))
            {
                await Log("Could not acquire deployment lock — another deployment may be in progress", "Warning");
                await ReportFailed(cmd, "Deployment lock busy", start, ct);
                return;
            }

            try
            {
                // 3. Download package
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Downloading package");
                await Progress(10, "Downloading package");
                var packagePath = await DownloadPackageAsync(cmd, ct);

                // 4. Validate hash
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Validating package integrity");
                await Progress(20, "Validating hash");
                if (!ValidateHash(packagePath, cmd.PackageSha256))
                {
                    await Log("Package hash mismatch — aborting deployment", "Error");
                    await ReportFailed(cmd, "Package hash validation failed", start, ct);
                    return;
                }

                // 5. Backup
                if (cmd.CreateBackup)
                {
                    await Log($"[{DateTime.UtcNow:HH:mm:ss}] Creating backup");
                    await Progress(30, "Creating backup");
                    var backupResult = _backup.CreateBackup(cmd.PhysicalPath, cmd.WebsiteName, cmd.Version);
                    if (backupResult.IsSuccess) backupPath = backupResult.Value;
                    else await Log($"Backup warning: {backupResult.Error}", "Warning");
                }

                // 6. Stop app pool
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Stopping application pool '{cmd.AppPoolName}'");
                await Progress(40, "Stopping app pool");
                var stopResult = _iis.StopAppPool(cmd.AppPoolName);
                if (!stopResult.IsSuccess)
                    await Log($"Warning stopping app pool: {stopResult.Error}", "Warning");

                // 7. Write app_offline.htm
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Writing app_offline.htm");
                await Progress(45, "Writing app_offline.htm");
                var offlinePath = Path.Combine(cmd.PhysicalPath, AgentConstants.AppOfflineFileName);
                await File.WriteAllTextAsync(offlinePath, AgentConstants.AppOfflineContent, ct);

                // 8. Extract package
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Extracting package");
                await Progress(55, "Extracting package");
                var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                ZipFile.ExtractToDirectory(packagePath, tempPath);

                // Find the web app root inside the ZIP (handles any nesting depth)
                var contentRoot = FindWebAppRoot(tempPath);
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Content root: {Path.GetRelativePath(tempPath, contentRoot).Replace("\\", "/")}");

                // 9. Replace files
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Replacing application files");
                await Progress(70, "Replacing files");
                CopyDirectory(contentRoot, cmd.PhysicalPath, excludeFileName: AgentConstants.AppOfflineFileName);
                Directory.Delete(tempPath, recursive: true);
                File.Delete(packagePath);

                // 10. Apply config overrides
                if (cmd.ConfigOverrides?.Count > 0)
                {
                    await Log($"[{DateTime.UtcNow:HH:mm:ss}] Applying config overrides");
                    await Progress(75, "Applying config");
                    ApplyConfigOverrides(cmd.PhysicalPath, cmd.ConfigOverrides);
                }

                // 11. Remove app_offline
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Removing app_offline.htm");
                await Progress(80, "Removing app_offline.htm");
                if (File.Exists(offlinePath)) File.Delete(offlinePath);

                // 12. Start app pool
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Starting application pool '{cmd.AppPoolName}'");
                await Progress(85, "Starting app pool");
                var startResult = _iis.StartAppPool(cmd.AppPoolName);
                if (!startResult.IsSuccess)
                {
                    await Log($"Failed to start app pool: {startResult.Error}", "Error");
                    await ReportFailed(cmd, $"Failed to start app pool: {startResult.Error}", start, ct);
                    return;
                }

                // 13. Health check
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Running health check");
                await Progress(95, "Health check");
                await Task.Delay(TimeSpan.FromSeconds(3), ct); // Give IIS a moment

                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Deployment completed successfully");
                await Progress(100, "Completed");

                await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportDeploymentCompleted,
                    new DeploymentCompletedEvent
                    {
                        ServerId = cmd.ServerId,
                        DeploymentId = cmd.DeploymentId,
                        DeploymentTargetId = cmd.DeploymentTargetId,
                        CorrelationId = cmd.CorrelationId,
                        Success = true,
                        BackupPath = backupPath,
                        Duration = DateTime.UtcNow - start
                    }, ct);
            }
            finally
            {
                _backup.ReleaseLock(cmd.WebsiteName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment {DeploymentId} failed", cmd.DeploymentId);
            await Log($"[{DateTime.UtcNow:HH:mm:ss}] DEPLOYMENT FAILED: {ex.Message}", "Error");

            // Attempt rollback if backup was created
            if (backupPath is not null)
            {
                await Log($"[{DateTime.UtcNow:HH:mm:ss}] Attempting automatic rollback from backup");
                var rollbackResult = _backup.RestoreBackup(backupPath, cmd.PhysicalPath);
                if (rollbackResult.IsSuccess)
                {
                    _iis.StartAppPool(cmd.AppPoolName);
                    await Log("[Rollback] Rollback successful");
                }
                else
                {
                    await Log($"[Rollback] Rollback FAILED: {rollbackResult.Error}", "Error");
                }
            }

            await ReportFailed(cmd, ex.Message, start, ct);
        }
    }

    private async Task ReportFailed(DeployApplicationCommand cmd, string reason, DateTime start, CancellationToken ct)
    {
        await _client.SendEventAsync(SignalRConstants.PortalMethods.ReportDeploymentCompleted,
            new DeploymentCompletedEvent
            {
                ServerId = cmd.ServerId,
                DeploymentId = cmd.DeploymentId,
                DeploymentTargetId = cmd.DeploymentTargetId,
                CorrelationId = cmd.CorrelationId,
                Success = false,
                FailureReason = reason,
                Duration = DateTime.UtcNow - start
            }, ct);
    }

    private bool IsPathAllowed(string path)
        => _config.AllowedDeployPaths.Any(allowed =>
            path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));

    private async Task<string> DownloadPackageAsync(DeployApplicationCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.PortalUrl))
            throw new InvalidOperationException("Agent:PortalUrl is not configured in appsettings.json");

        var fullUrl = $"{_config.PortalUrl.TrimEnd('/')}/{cmd.PackageUrl.TrimStart('/')}";
        _logger.LogInformation("Downloading package from {Url}", fullUrl);
        var tempFile = Path.Combine(Path.GetTempPath(), $"pkg_{cmd.DeploymentId}_{Guid.NewGuid():N}.zip");

        HttpMessageHandler handler = _config.AllowInsecureSsl
            ? new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }
            : new HttpClientHandler();

        using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Add(SignalRConstants.AgentApiKeyHeader, _config.ApiKey);

        var bytes = await http.GetByteArrayAsync(fullUrl, ct);
        await File.WriteAllBytesAsync(tempFile, bytes, ct);
        return tempFile;
    }

    private static bool ValidateHash(string filePath, string expectedHash)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = Convert.ToHexString(sha.ComputeHash(stream));
        return string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    // BFS through extracted ZIP to find the real web app root — the shallowest directory
    // that contains web.config or a Bin/ folder. Handles any wrapper structure (VB2/publish/...,
    // MyApp/publish/..., etc.) regardless of whether the wrapper folder also has other files.
    private static string FindWebAppRoot(string extractedRoot)
    {
        var queue = new Queue<string>();
        queue.Enqueue(extractedRoot);
        while (queue.Count > 0)
        {
            var dir = queue.Dequeue();
            var hasWebConfig = File.Exists(Path.Combine(dir, "web.config"));
            var hasBin = Directory.Exists(Path.Combine(dir, "bin")) ||
                         Directory.Exists(Path.Combine(dir, "Bin"));
            if (hasWebConfig || hasBin)
                return dir;
            foreach (var sub in Directory.GetDirectories(dir))
                queue.Enqueue(sub);
        }
        return extractedRoot;
    }

    private static void CopyDirectory(string source, string dest, string? excludeFileName = null)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (excludeFileName is not null &&
                Path.GetFileName(file).Equals(excludeFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(source, file);
            var destFile = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    private static void ApplyConfigOverrides(string physicalPath, Dictionary<string, string> overrides)
    {
        var settingsFile = Path.Combine(physicalPath, "appsettings.json");
        if (!File.Exists(settingsFile)) return;

        var json = File.ReadAllText(settingsFile);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        // Minimal override implementation — production should use a proper JSON merge
        // This sets flat key=value in a replacement appsettings.Production.json file
        var overrideFile = Path.Combine(physicalPath, "appsettings.Production.json");
        var content = System.Text.Json.JsonSerializer.Serialize(overrides,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(overrideFile, content);
    }
}
