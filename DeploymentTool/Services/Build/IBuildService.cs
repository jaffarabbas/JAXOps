using DeploymentTool.Models.Build;

namespace DeploymentTool.Services.Build;

public interface IBuildService
{
    Task<BuildResultModel> BuildAsync(
        string solutionOrProjectPath,
        string configuration,
        IProgress<string> progress,
        CancellationToken ct = default);

    Task<BuildResultModel> PublishAsync(
        string projectPath,
        string outputPath,
        string configuration,
        IProgress<string> progress,
        CancellationToken ct = default);

    Task<BuildResultModel> PublishVbWebFormsAsync(
        string sourcePath,
        string outputPath,
        IProgress<string> progress,
        CancellationToken ct = default);

    Task<BuildResultModel> BuildVbWebFormsAsync(
        string sourcePath,
        IProgress<string> progress,
        CancellationToken ct = default);
}
