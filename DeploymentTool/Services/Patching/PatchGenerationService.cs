using System.IO;
using System.Text.Json;
using DeploymentTool.Models.Patch;

namespace DeploymentTool.Services.Patching;

public class PatchGenerationService : IPatchGenerationService
{
    public async Task<string> GeneratePatchAsync(
        PatchGenerationRequest request,
        IProgress<(int Done, int Total, string File)> progress,
        Action<string> log,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var patchFolder = Path.Combine(request.PatchOutputRoot, request.PatchName);
            Directory.CreateDirectory(patchFolder);
            log($"Patch folder: {patchFolder}");

            var selected       = request.Files.Where(f => f.IsSelected).ToList();
            var manifestFiles  = new List<PatchFileEntry>(selected.Count);
            int done           = 0;

            foreach (var f in selected)
            {
                ct.ThrowIfCancellationRequested();

                // Resolve source: prefer FullPath, fall back to SourcePath/RelativePath
                var src = !string.IsNullOrEmpty(f.FullPath) && File.Exists(f.FullPath)
                    ? f.FullPath
                    : Path.Combine(request.SourcePath, f.RelativePath);

                if (!File.Exists(src))
                {
                    log($"SKIP (not found): {f.RelativePath}");
                    done++;
                    progress.Report((done, selected.Count, f.RelativePath));
                    continue;
                }

                var dst = Path.Combine(patchFolder, f.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, overwrite: true);

                manifestFiles.Add(new PatchFileEntry
                {
                    RelativePath  = f.RelativePath,
                    Status        = f.Status,
                    FileSizeBytes = f.FileSizeBytes,
                    LastWriteTime = f.LastWriteTime.ToString("O")
                });

                done++;
                log($"Patched: {f.RelativePath}");
                progress.Report((done, selected.Count, f.RelativePath));
            }

            if (request.IncludeManifest)
                WriteManifest(patchFolder, request, manifestFiles, log);

            return patchFolder;
        }, ct);
    }

    private static void WriteManifest(
        string patchFolder,
        PatchGenerationRequest req,
        List<PatchFileEntry> files,
        Action<string> log)
    {
        var manifest = new PatchManifestModel
        {
            PatchName          = req.PatchName,
            Version            = req.Version,
            CreatedAt          = DateTime.UtcNow,
            GitBranch          = req.GitBranch,
            GitCommitSha       = req.GitCommitSha,
            GitCommitMessage   = req.GitCommitMessage,
            BuildConfiguration = req.BuildConfiguration,
            MainCodePath       = req.MainCodePath,
            MiniCodePath       = req.MiniCodePath,
            TotalFiles         = files.Count,
            Files              = files
        };

        var opts = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(manifest, opts);
        var path = Path.Combine(patchFolder, "metadata.json");
        File.WriteAllText(path, json);
        log($"Manifest written: {path}");
    }
}
