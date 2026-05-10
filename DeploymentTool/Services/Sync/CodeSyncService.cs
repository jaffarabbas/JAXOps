using System.IO;
using DeploymentTool.Models.Sync;

namespace DeploymentTool.Services.Sync;

public class CodeSyncService : ICodeSyncService
{
    private static readonly HashSet<string> DefaultExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", "node_modules", "packages", ".vs", ".vscode", "dist", "build", ".idea"
    };

    public async Task<List<ChangedFileModel>> ScanChangedFilesAsync(
        string mainPath,
        string miniPath,
        DateTime? fromDate,
        DateTime? toDate,
        IEnumerable<string> excludePatterns,
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var results  = new List<ChangedFileModel>();
            var mainDir  = new DirectoryInfo(mainPath);
            if (!mainDir.Exists) return results;

            var extraExcludes = new HashSet<string>(excludePatterns ?? [], StringComparer.OrdinalIgnoreCase);

            // Date range uses local time (same timezone the user sees in Explorer / date pickers).
            // from = start of fromDate day, to = end of toDate day (inclusive).
            bool useDateRange = fromDate.HasValue || toDate.HasValue;
            DateTime? from = fromDate?.Date;
            DateTime? to   = toDate?.Date.AddDays(1).AddTicks(-1); // 23:59:59.999… of toDate

            if (useDateRange)
            {
                var rangeLabel = $"{(from.HasValue ? from.Value.ToString("yyyy-MM-dd") : "∞")} → {(to.HasValue ? toDate!.Value.ToString("yyyy-MM-dd") : "∞")}";
                progress?.Report($"Date range filter: {rangeLabel}");
            }

            progress?.Report($"Scanning {mainPath}…");
            int scanned = 0;

            foreach (var mainFile in mainDir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                if (IsExcluded(mainFile.FullName, mainPath, extraExcludes)) continue;

                scanned++;
                if (scanned % 500 == 0)
                    progress?.Report($"  Scanned {scanned} files…");

                // ── Date-range pre-filter (uses LOCAL last-write time) ────────
                if (useDateRange)
                {
                    var localWrite = mainFile.LastWriteTime; // local time
                    if (from.HasValue && localWrite < from.Value) continue;
                    if (to.HasValue   && localWrite > to.Value)   continue;
                }

                var relative = Path.GetRelativePath(mainPath, mainFile.FullName);
                var miniFile = new FileInfo(Path.Combine(miniPath, relative));

                // ── Determine status ─────────────────────────────────────────
                string status;
                if (!miniFile.Exists)
                {
                    // Not in mini at all → New
                    status = "New";
                }
                else if (useDateRange)
                {
                    // File falls in the requested date range → treat as Modified
                    // (the user asked to see everything touched in this period)
                    status = "Modified";
                }
                else if (mainFile.LastWriteTime > miniFile.LastWriteTime
                      || mainFile.Length != miniFile.Length)
                {
                    // No date range: compare actual file state
                    status = "Modified";
                }
                else
                {
                    continue; // files are identical — skip
                }

                results.Add(new ChangedFileModel
                {
                    RelativePath  = relative,
                    FullPath      = mainFile.FullName,
                    Status        = status,
                    LastWriteTime = mainFile.LastWriteTime, // store local time for display
                    FileSizeBytes = mainFile.Length,
                    IsSelected    = true
                });
            }

            progress?.Report($"Scan complete — {results.Count} file(s) found in {scanned} total scanned.");
            return [.. results.OrderBy(f => f.RelativePath)];
        }, ct);
    }

    public async Task CopyChangedFilesAsync(
        IEnumerable<ChangedFileModel> files,
        string mainPath,
        string destPath,
        IProgress<(int Done, int Total, string File)> progress,
        Action<string> log,
        CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            var list = files.ToList();
            int done = 0;

            foreach (var f in list)
            {
                ct.ThrowIfCancellationRequested();

                var src = !string.IsNullOrEmpty(f.FullPath) && File.Exists(f.FullPath)
                    ? f.FullPath
                    : Path.Combine(mainPath, f.RelativePath);

                var dst = Path.Combine(destPath, f.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, overwrite: true);

                done++;
                log($"[{f.Status}] {f.RelativePath}");
                progress.Report((done, list.Count, f.RelativePath));
            }
        }, ct);
    }

    private static bool IsExcluded(string fullPath, string rootPath, HashSet<string> extra)
    {
        var relative = Path.GetRelativePath(rootPath, fullPath);
        var parts    = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (DefaultExcludedFolders.Contains(part)) return true;
            if (extra.Contains(part)) return true;
        }

        return false;
    }
}
