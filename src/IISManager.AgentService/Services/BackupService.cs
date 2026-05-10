using IISManager.Domain.Common;
using IISManager.Shared.Constants;
using System.IO.Compression;

namespace IISManager.AgentService.Services;

public class BackupService
{
    private readonly AgentConfiguration _config;
    private readonly ILogger<BackupService> _logger;

    public BackupService(AgentConfiguration config, ILogger<BackupService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Result<string> CreateBackup(string physicalPath, string siteName, string version)
    {
        try
        {
            Directory.CreateDirectory(_config.BackupBasePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupName = $"{siteName}_{version}_{timestamp}.zip";
            var backupPath = Path.Combine(_config.BackupBasePath, backupName);

            if (!Directory.Exists(physicalPath))
                return Result<string>.Fail($"Physical path '{physicalPath}' does not exist");

            ZipFile.CreateFromDirectory(physicalPath, backupPath, CompressionLevel.Fastest, includeBaseDirectory: false);
            _logger.LogInformation("Backup created at {BackupPath}", backupPath);
            return Result<string>.Ok(backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup for site {SiteName}", siteName);
            return Result<string>.Fail(ex.Message);
        }
    }

    public Result RestoreBackup(string backupPath, string physicalPath)
    {
        try
        {
            if (!File.Exists(backupPath))
                return Result.Fail($"Backup file '{backupPath}' not found");

            if (Directory.Exists(physicalPath))
            {
                foreach (var file in Directory.GetFiles(physicalPath, "*", SearchOption.AllDirectories))
                    File.Delete(file);
            }

            ZipFile.ExtractToDirectory(backupPath, physicalPath, overwriteFiles: true);
            _logger.LogInformation("Backup restored from {BackupPath} to {PhysicalPath}", backupPath, physicalPath);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup from {BackupPath}", backupPath);
            return Result.Fail(ex.Message);
        }
    }

    public async Task<bool> TryAcquireLockAsync(string siteName)
    {
        Directory.CreateDirectory(_config.LockFileDirectory);
        var lockFile = Path.Combine(_config.LockFileDirectory, siteName + AgentConstants.LockFileExtension);
        try
        {
            await File.WriteAllTextAsync(lockFile, DateTime.UtcNow.ToString("O"));
            return true;
        }
        catch { return false; }
    }

    public void ReleaseLock(string siteName)
    {
        var lockFile = Path.Combine(_config.LockFileDirectory, siteName + AgentConstants.LockFileExtension);
        try { File.Delete(lockFile); } catch { }
    }

    public void PurgeOldBackups()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-AgentConstants.MaxBackupAgedays);
            foreach (var file in Directory.GetFiles(_config.BackupBasePath, "*.zip"))
            {
                if (File.GetCreationTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to purge old backups");
        }
    }
}
