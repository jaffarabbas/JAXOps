using System.Diagnostics;
using System.IO;
using DeploymentTool.Models;

namespace DeploymentTool.Services;

public class VbPublisherService
{
    // ── Scan ──────────────────────────────────────────────────────────────────
    // Returns files that are new-in-main or newer/different-in-main vs mini.
    public async Task<List<VbPublishedFile>> ScanChangedFilesAsync(
        string mainPath,
        string miniPath,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var result  = new List<VbPublishedFile>();
            var mainDir = new DirectoryInfo(mainPath);
            if (!mainDir.Exists) return result;

            foreach (var mainFile in mainDir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var relative = Path.GetRelativePath(mainPath, mainFile.FullName);
                var miniFile = new FileInfo(Path.Combine(miniPath, relative));

                string status;
                if (!miniFile.Exists)
                    status = "New";
                else if (mainFile.LastWriteTimeUtc > miniFile.LastWriteTimeUtc
                      || mainFile.Length != miniFile.Length)
                    status = "Modified";
                else
                    continue;

                result.Add(new VbPublishedFile
                {
                    RelativePath  = relative,
                    Status        = status,
                    FileSizeBytes = mainFile.Length
                });
            }

            return result;
        }, ct);
    }

    // ── Copy to Mini ─────────────────────────────────────────────────────────
    public async Task CopyToMiniAsync(
        IEnumerable<VbPublishedFile>                    files,
        string                                          mainPath,
        string                                          miniPath,
        IProgress<(int done, int total, string file)>  progress,
        Action<string, DeployLogSeverity>               log,
        CancellationToken                               ct = default)
    {
        await Task.Run(() =>
        {
            var list = files.ToList();
            int done = 0;

            foreach (var f in list)
            {
                ct.ThrowIfCancellationRequested();

                var src = Path.Combine(mainPath, f.RelativePath);
                var dst = Path.Combine(miniPath, f.RelativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, overwrite: true);

                done++;
                log($"[{f.Status}] {f.RelativePath}", DeployLogSeverity.Info);
                progress.Report((done, list.Count, f.RelativePath));
            }
        }, ct);
    }

    // ── Compile & Publish VB WebForms (Web Site — no .vbproj) ────────────────
    // Uses aspnet_compiler.exe to precompile the VB Web Site folder to a
    // file-system output directory. Works without a project file.
    public async Task<bool> PublishVbAsync(
        string                            sourcePath,
        string                            publishOutputPath,
        Action<string, DeployLogSeverity> log,
        CancellationToken                 ct = default)
    {
        Directory.CreateDirectory(publishOutputPath);

        // Resolve the actual web app root — the folder that contains web.config directly.
        // If the user pointed at a parent folder (e.g. the solution root), walk one level
        // deep to find the subfolder with web.config so aspnet_compiler doesn't choke on
        // "allowDefinition='MachineToApplication' beyond application level".
        var webRoot = ResolveWebRoot(sourcePath, log);
        log($"Web root: {webRoot}", DeployLogSeverity.Info);

        var compiler = FindAspNetCompiler();
        log($"aspnet_compiler: {compiler}", DeployLogSeverity.Info);

        // -v /  = virtual path (root)
        // -p    = physical source path
        // -f    = overwrite existing output files
        var args = $"-v / -p \"{webRoot}\" -f \"{publishOutputPath}\"";
        log($"Args: {args}", DeployLogSeverity.Info);

        var psi = new ProcessStartInfo(compiler)
        {
            Arguments              = args,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) log(e.Data, DeployLogSeverity.Info); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data != null) log($"ERR: {e.Data}", DeployLogSeverity.Error); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0;
    }

    private static string ResolveWebRoot(string path, Action<string, DeployLogSeverity> log)
    {
        // Already the web root — web.config is right here
        if (File.Exists(Path.Combine(path, "web.config")))
            return path;

        // Search one level deep for a subfolder containing web.config
        var match = Directory.EnumerateDirectories(path)
            .FirstOrDefault(d => File.Exists(Path.Combine(d, "web.config")));

        if (match != null)
        {
            log($"web.config found in subfolder — using: {match}", DeployLogSeverity.Info);
            return match;
        }

        // Nothing found — pass the original path and let aspnet_compiler report the real error
        log("WARNING: web.config not found directly or one level deep. Using path as-is.", DeployLogSeverity.Warning);
        return path;
    }

    private static string FindAspNetCompiler()
    {
        var fx64 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe");
        if (File.Exists(fx64)) return fx64;

        var fx32 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"Microsoft.NET\Framework\v4.0.30319\aspnet_compiler.exe");
        if (File.Exists(fx32)) return fx32;

        return "aspnet_compiler.exe"; // fall back to PATH
    }

    // ── Create Patch ─────────────────────────────────────────────────────────
    // Copies selected files from sourcePath (mini or publish output) to a
    // named sub-folder under patchOutputRoot, preserving folder structure.
    public async Task CreatePatchAsync(
        IEnumerable<VbPublishedFile>                    files,
        string                                          sourcePath,
        string                                          patchOutputRoot,
        string                                          patchName,
        IProgress<(int done, int total, string file)>  progress,
        Action<string, DeployLogSeverity>               log,
        CancellationToken                               ct = default)
    {
        await Task.Run(() =>
        {
            var patchFolder = Path.Combine(patchOutputRoot, patchName);
            Directory.CreateDirectory(patchFolder);
            log($"Patch folder: {patchFolder}", DeployLogSeverity.Info);

            var list = files.ToList();
            int done = 0;

            foreach (var f in list)
            {
                ct.ThrowIfCancellationRequested();

                var src = Path.Combine(sourcePath, f.RelativePath);
                if (!File.Exists(src))
                {
                    log($"SKIP (not in source): {f.RelativePath}", DeployLogSeverity.Warning);
                    done++;
                    progress.Report((done, list.Count, f.RelativePath));
                    continue;
                }

                var dst = Path.Combine(patchFolder, f.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, overwrite: true);

                done++;
                log($"Patched: {f.RelativePath}", DeployLogSeverity.Info);
                progress.Report((done, list.Count, f.RelativePath));
            }
        }, ct);
    }
}
