using System.IO;

namespace DeploymentTool.Services;

public interface IFileDeploymentService
{
    Task CreateFolderStructureAsync(string targetPath, CancellationToken ct = default);

    Task CopyPublishAsync(
        string sourcePath,
        string targetPath,
        bool overwrite,
        IProgress<(int copied, int total, string file)>? progress,
        Action<string> log,
        CancellationToken ct = default);
}

public class FileDeploymentService : IFileDeploymentService
{
    private const int BufferSize = 81_920;

    public Task CreateFolderStructureAsync(string targetPath, CancellationToken ct = default)
        => Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);
        }, ct);

    public async Task CopyPublishAsync(
        string sourcePath,
        string targetPath,
        bool overwrite,
        IProgress<(int copied, int total, string file)>? progress,
        Action<string> log,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Source path not found: {sourcePath}");

        // Enumerate up-front so we can report accurate total without loading file data.
        var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).ToList();
        var total  = files.Count;
        var copied = 0;

        foreach (var sourceFile in files)
        {
            ct.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourcePath, sourceFile);
            var destFile = Path.Combine(targetPath, relative);
            var destDir  = Path.GetDirectoryName(destFile)!;

            try
            {
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                if (!overwrite && File.Exists(destFile))
                {
                    log($"Skipped (exists): {relative}");
                    copied++;
                    progress?.Report((copied, total, relative));
                    continue;
                }

                await CopyFileAsync(sourceFile, destFile, ct).ConfigureAwait(false);
                log($"Copied: {relative}");
            }
            catch (IOException ex) when (IsFileLocked(ex))
            {
                log($"WARN: Locked file skipped: {relative}");
            }

            copied++;
            progress?.Report((copied, total, relative));
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken ct)
    {
        await using var src  = new FileStream(source,      FileMode.Open,   FileAccess.Read,  FileShare.ReadWrite, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var dest = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None,      BufferSize, FileOptions.Asynchronous);
        await src.CopyToAsync(dest, BufferSize, ct).ConfigureAwait(false);
    }

    // HRESULT -2147024864 = ERROR_SHARING_VIOLATION, -2147024843 = ERROR_LOCK_VIOLATION
    private static bool IsFileLocked(IOException ex) => ex.HResult is -2147024864 or -2147024843;
}
