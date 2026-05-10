using System.IO;
using DeploymentTool.Models;

namespace DeploymentTool.Services;

public class DeploymentOrchestratorService
{
    private readonly IRemoteConnectionService _connection;
    private readonly IFileDeploymentService   _files;
    private readonly IIISDeploymentService    _iis;
    private readonly IFtpDeploymentService    _ftp;

    public DeploymentOrchestratorService(
        IRemoteConnectionService connection,
        IFileDeploymentService   files,
        IIISDeploymentService    iis,
        IFtpDeploymentService    ftp)
    {
        _connection = connection;
        _files      = files;
        _iis        = iis;
        _ftp        = ftp;
    }

    /// <summary>
    /// Runs the full deployment pipeline for a list of project configs.
    /// Each config carries its own pool name, alias, CLR version, and pipeline mode,
    /// so single-pool and per-project-pool modes both use this same path.
    /// </summary>
    public async Task DeployAsync(
        DeploymentProfile                  sharedProfile,
        IReadOnlyList<ProjectDeployConfig> configs,
        string                             publishOutputFolder,
        IProgress<DeploymentProgress>      progress,
        Action<string>                     log,
        CancellationToken                  ct = default)
    {
        const int stepsPerProject = 6;
        var active     = configs.Where(c => c.IsSelected).ToList();
        var totalSteps = active.Count * stepsPerProject;
        var step       = 0;

        void Report(string stepName)
        {
            step++;
            progress.Report(new DeploymentProgress { Current = step, Total = totalSteps, StepName = stepName });
        }

        foreach (var config in active)
        {
            ct.ThrowIfCancellationRequested();

            var alias         = string.IsNullOrWhiteSpace(config.ApplicationAlias) ? config.ProjectName : config.ApplicationAlias;
            var sourceDir     = string.IsNullOrWhiteSpace(config.SourceOverridePath)
                                    ? Path.Combine(publishOutputFolder, config.ProjectName)
                                    : config.SourceOverridePath;
            var targetWinPath = Path.Combine(sharedProfile.DeploymentRootPath, alias);

            var fileProgress  = new Progress<(int copied, int total, string file)>(p =>
                progress.Report(new DeploymentProgress
                {
                    Current  = step,
                    Total    = totalSteps,
                    StepName = $"Uploading {p.copied}/{p.total}: {p.file}"
                }));

            if (sharedProfile.SkipFileTransfer)
            {
                // ── IIS-ONLY MODE — file transfer handled separately ────────
                log($"[{config.ProjectName}] Skipping file transfer (IIS-only mode).");
                Report("Skip transfer");
                Report("Skip folder");
                Report("Skip copy");
            }
            else if (sharedProfile.UseFtp)
            {
                // ── FTP MODE ────────────────────────────────────────────────

                var ftpRoot      = sharedProfile.FtpRootPath.TrimEnd('/');
                var ftpFolderUrl = $"ftp://{sharedProfile.ServerHostname}:{sharedProfile.FtpPort}{ftpRoot}/{alias}";

                // ── 1: (no SMB session needed) ─────────────────────────────
                log($"[{config.ProjectName}] FTP mode → {ftpFolderUrl}");
                Report("FTP connect");

                // ── 2: Create FTP folder ───────────────────────────────────
                log($"[{config.ProjectName}] Creating FTP folder…");
                Report("Create folder");
                await _ftp.EnsureFolderAsync(ftpFolderUrl, sharedProfile.Username, sharedProfile.Password, sharedProfile.FtpPassiveMode, ct).ConfigureAwait(false);
                log($"[{config.ProjectName}] FTP folder ready.");

                // ── 3: Upload files ────────────────────────────────────────
                log($"[{config.ProjectName}] Uploading from: {sourceDir}");
                Report("Upload files");
                await _ftp.UploadFolderAsync(sourceDir, ftpFolderUrl, sharedProfile.Username, sharedProfile.Password, sharedProfile.FtpPassiveMode, sharedProfile.OverwriteExistingFiles, fileProgress, log, ct).ConfigureAwait(false);
                log($"[{config.ProjectName}] Upload complete.");
            }
            else
            {
                // ── SMB MODE ────────────────────────────────────────────────

                var uncRoot       = !string.IsNullOrWhiteSpace(sharedProfile.ShareName)
                                        ? $@"\\{sharedProfile.ServerHostname}\{sharedProfile.ShareName}"
                                        : BuildUncPath(sharedProfile.ServerHostname, sharedProfile.DeploymentRootPath);
                var targetUncPath = Path.Combine(uncRoot, alias);

                // ── 1: Connect ─────────────────────────────────────────────
                log($"[{config.ProjectName}] Connecting to {sharedProfile.ServerHostname}…");
                Report("Connect");
                await _connection.ConnectAsync(uncRoot, sharedProfile.Username, sharedProfile.Password, ct).ConfigureAwait(false);
                log($"[{config.ProjectName}] Connected.");

                // ── 2: Create deployment folder ────────────────────────────
                log($"[{config.ProjectName}] Creating folder: {targetUncPath}");
                Report("Create folder");
                await _files.CreateFolderStructureAsync(targetUncPath, ct).ConfigureAwait(false);
                log($"[{config.ProjectName}] Folder ready.");

                // ── 3: Copy published files ────────────────────────────────
                log($"[{config.ProjectName}] Copying from: {sourceDir}");
                Report("Copy files");
                await _files.CopyPublishAsync(sourceDir, targetUncPath, sharedProfile.OverwriteExistingFiles, fileProgress, log, ct).ConfigureAwait(false);
                log($"[{config.ProjectName}] Copy complete.");

                try { await _connection.DisconnectAsync(uncRoot, ct).ConfigureAwait(false); }
                catch { /* best-effort */ }
            }

            // ── 4: Create / configure App Pool ─────────────────────────────
            if (sharedProfile.CreateAppPool)
            {
                log($"[{config.ProjectName}] Creating app pool: {config.AppPoolName}");
                Report("Create app pool");
                await _iis.CreateAppPoolAsync(
                    sharedProfile.ServerHostname, sharedProfile.Username, sharedProfile.Password,
                    config.AppPoolName, config.DotNetClrVersion, config.PipelineMode, config.Enable32Bit,
                    log, ct).ConfigureAwait(false);
                log($"[{config.ProjectName}] App pool ready: {config.AppPoolName}");
            }
            else
            {
                Report("Create app pool (skipped)");
            }

            // ── 5: Create IIS Application ───────────────────────────────────
            if (sharedProfile.CreateIisApplication)
            {
                log($"[{config.ProjectName}] Creating IIS application: /{alias} → pool: {config.AppPoolName}");
                Report("Create IIS application");
                await _iis.CreateIisApplicationAsync(
                    sharedProfile.ServerHostname, sharedProfile.Username, sharedProfile.Password,
                    sharedProfile.IisSiteName, alias, targetWinPath, config.AppPoolName,
                    log, ct).ConfigureAwait(false);
                log($"[{config.ProjectName}] IIS application ready.");
            }
            else
            {
                Report("Create IIS application (skipped)");
            }

            // ── 6: Start App Pool ────────────────────────────────────────────
            if (sharedProfile.StartAppPoolAfterDeploy)
            {
                log($"[{config.ProjectName}] Starting pool: {config.AppPoolName}");
                Report("Start app pool");
                await _iis.StartAppPoolAsync(
                    sharedProfile.ServerHostname, sharedProfile.Username, sharedProfile.Password,
                    config.AppPoolName, log, ct).ConfigureAwait(false);
                log($"[{config.ProjectName}] Pool started.");
            }
            else
            {
                Report("Start app pool (skipped)");
            }

            log($"[{config.ProjectName}] Deployment complete.");
        }
    }

    private static string BuildUncPath(string hostname, string rootPath)
    {
        if (rootPath.StartsWith(@"\\")) return rootPath;
        var drive = rootPath[0];
        var rest  = rootPath.Length > 3 ? rootPath[3..] : string.Empty;
        return string.IsNullOrEmpty(rest)
            ? $@"\\{hostname}\{drive}$"
            : $@"\\{hostname}\{drive}$\{rest}";
    }
}
