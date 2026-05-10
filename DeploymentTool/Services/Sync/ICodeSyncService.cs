using DeploymentTool.Models.Sync;

namespace DeploymentTool.Services.Sync;

public interface ICodeSyncService
{
    Task<List<ChangedFileModel>> ScanChangedFilesAsync(
        string mainPath,
        string miniPath,
        DateTime? fromDate,
        DateTime? toDate,
        IEnumerable<string> excludePatterns,
        IProgress<string>? progress,
        CancellationToken ct = default);

    Task CopyChangedFilesAsync(
        IEnumerable<ChangedFileModel> files,
        string mainPath,
        string destPath,
        IProgress<(int Done, int Total, string File)> progress,
        Action<string> log,
        CancellationToken ct = default);
}
