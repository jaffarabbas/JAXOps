using DeploymentTool.Models.Patch;

namespace DeploymentTool.Services.Patching;

public interface IPatchGenerationService
{
    Task<string> GeneratePatchAsync(
        PatchGenerationRequest request,
        IProgress<(int Done, int Total, string File)> progress,
        Action<string> log,
        CancellationToken ct = default);
}
