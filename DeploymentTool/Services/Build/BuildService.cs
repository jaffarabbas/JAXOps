using System.Diagnostics;
using System.IO;
using System.Text;
using DeploymentTool.Models.Build;

namespace DeploymentTool.Services.Build;

public class BuildService : IBuildService
{
    public async Task<BuildResultModel> BuildAsync(
        string solutionOrProjectPath,
        string configuration,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        var workingDir = File.Exists(solutionOrProjectPath)
            ? Path.GetDirectoryName(solutionOrProjectPath)!
            : solutionOrProjectPath;

        var args   = $"build \"{solutionOrProjectPath}\" -c {configuration} --no-incremental";
        var result = await RunProcessAsync("dotnet", args, workingDir, progress, ct);
        result.Configuration = configuration;
        result.ProjectPath   = solutionOrProjectPath;
        return result;
    }

    public async Task<BuildResultModel> PublishAsync(
        string projectPath,
        string outputPath,
        string configuration,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        var workingDir = File.Exists(projectPath)
            ? Path.GetDirectoryName(projectPath)!
            : projectPath;

        Directory.CreateDirectory(outputPath);
        var args   = $"publish \"{projectPath}\" -c {configuration} -o \"{outputPath}\"";
        var result = await RunProcessAsync("dotnet", args, workingDir, progress, ct);
        result.Configuration = configuration;
        result.ProjectPath   = projectPath;
        return result;
    }

    public async Task<BuildResultModel> PublishVbWebFormsAsync(
        string sourcePath,
        string outputPath,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputPath);

        var webRoot  = ResolveWebRoot(sourcePath, progress);
        var compiler = FindAspNetCompiler();

        progress.Report($"Web root: {webRoot}");
        progress.Report($"Compiler: {compiler}");
        EnsureCodeDomProvider(webRoot, progress);

        var args   = $"-v / -p \"{webRoot}\" -f \"{outputPath}\"";
        var result = await RunProcessAsync(compiler, args, webRoot, progress, ct);
        result.Configuration = "VB WebForms";
        result.ProjectPath   = sourcePath;
        return result;
    }

    public async Task<BuildResultModel> BuildVbWebFormsAsync(
        string sourcePath,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        var webRoot  = ResolveWebRoot(sourcePath, progress);
        var compiler = FindAspNetCompiler();

        progress.Report($"Web root: {webRoot}");
        progress.Report($"Compiler: {compiler}");
        EnsureCodeDomProvider(webRoot, progress);
        progress.Report("Running aspnet_compiler in-place (compile check, no output folder)…");

        var args   = $"-v / -p \"{webRoot}\"";
        var result = await RunProcessAsync(compiler, args, webRoot, progress, ct);
        result.Configuration = "VB WebForms (build check)";
        result.ProjectPath   = sourcePath;
        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<BuildResultModel> RunProcessAsync(
        string executable,
        string arguments,
        string workingDir,
        IProgress<string> progress,
        CancellationToken ct)
    {
        var model     = new BuildResultModel();
        var stopwatch = Stopwatch.StartNew();

        var psi = new ProcessStartInfo(executable)
        {
            Arguments              = arguments,
            WorkingDirectory       = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi };

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            model.LogLines.Add(e.Data);
            progress.Report(e.Data);
            if (ContainsError(e.Data))   model.ErrorCount++;
            if (ContainsWarning(e.Data)) model.WarningCount++;
        };

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            model.LogLines.Add($"[stderr] {e.Data}");
            progress.Report($"[stderr] {e.Data}");
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(ct);

        stopwatch.Stop();
        model.Duration = stopwatch.Elapsed;
        model.Success  = proc.ExitCode == 0;

        if (!model.Success)
            model.ErrorSummary = string.Join("\n", model.LogLines
                .Where(l => ContainsError(l) && !l.Contains("0 Error(s)", StringComparison.OrdinalIgnoreCase))
                .Take(5));

        return model;
    }

    private static bool ContainsError(string line)
        => line.Contains(": error ", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Error(s)", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsWarning(string line)
        => line.Contains(": warning ", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Warning(s)", StringComparison.OrdinalIgnoreCase);

    private static string ResolveWebRoot(string path, IProgress<string> progress)
    {
        if (File.Exists(Path.Combine(path, "web.config"))) return path;

        var match = Directory.EnumerateDirectories(path)
            .FirstOrDefault(d => File.Exists(Path.Combine(d, "web.config")));

        if (match != null)
        {
            progress.Report($"web.config found in subfolder: {match}");
            return match;
        }

        progress.Report("WARNING: web.config not found; using path as-is.");
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

        return "aspnet_compiler.exe";
    }

    // ── CodeDom provider auto-restore ────────────────────────────────────────
    // aspnet_compiler fails with ASPCONFIG when the DLL referenced in web.config
    // isn't in the project's bin folder. We search for it in the NuGet package
    // cache and old-style packages folders, then copy it to bin.

    private static void EnsureCodeDomProvider(string webRoot, IProgress<string> progress)
    {
        const string dll     = "Microsoft.CodeDom.Providers.DotNetCompilerPlatform.dll";
        const string pkgId   = "microsoft.codedom.providers.dotnetcompilerplatform";
        const string pkgGlob = "Microsoft.CodeDom.Providers.DotNetCompilerPlatform.*";

        var binDir = Path.Combine(webRoot, "bin");
        if (File.Exists(Path.Combine(binDir, dll))) return;

        progress.Report($"CodeDom provider DLL not found in bin — resolving…");

        // 1. Walk up up to 4 levels looking for an old-style packages folder
        var dir = new DirectoryInfo(webRoot);
        for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent)
        {
            var pkgRoot = Path.Combine(dir.FullName, "packages");
            if (!Directory.Exists(pkgRoot)) continue;

            var match = Directory.EnumerateDirectories(pkgRoot, pkgGlob, SearchOption.TopDirectoryOnly)
                .OrderByDescending(d => d)
                .FirstOrDefault();
            if (match != null && TryCopyCodeDomProvider(match, binDir, progress)) return;
        }

        // 2. NuGet user-level cache (~/.nuget/packages/…)
        var nugetCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages", pkgId);

        if (Directory.Exists(nugetCache))
        {
            var vdir = Directory.EnumerateDirectories(nugetCache)
                .OrderByDescending(d => d)
                .FirstOrDefault();
            if (vdir != null && TryCopyCodeDomProvider(vdir, binDir, progress)) return;
        }

        progress.Report(
            "WARNING: Microsoft.CodeDom.Providers.DotNetCompilerPlatform package not found. " +
            "Run 'nuget restore' on the solution, or copy the DLL manually to the project's bin folder.");
    }

    private static bool TryCopyCodeDomProvider(string pkgDir, string binDir, IProgress<string> progress)
    {
        const string dllName = "Microsoft.CodeDom.Providers.DotNetCompilerPlatform.dll";

        // Find the provider DLL under lib/net* (e.g. lib\net45\)
        string? srcDll = null;
        var libDir = Path.Combine(pkgDir, "lib");
        if (Directory.Exists(libDir))
        {
            srcDll = Directory
                .EnumerateDirectories(libDir, "net*", SearchOption.TopDirectoryOnly)
                .Select(d => Path.Combine(d, dllName))
                .FirstOrDefault(File.Exists);
        }

        // Some older packages place the DLL directly in lib/
        if (srcDll == null)
        {
            var direct = Path.Combine(pkgDir, "lib", dllName);
            if (File.Exists(direct)) srcDll = direct;
        }

        if (srcDll == null) return false;

        Directory.CreateDirectory(binDir);
        File.Copy(srcDll, Path.Combine(binDir, dllName), overwrite: true);
        progress.Report($"Copied {dllName} → bin\\ (from {Path.GetFileName(pkgDir)})");

        // Copy Roslyn compilers to bin\Roslyn (vbc.exe etc.)
        // Package structure varies: tools\Roslyn45\, tools\Roslyn\, content\bin\Roslyn\
        string? roslynSrc = new[]
        {
            Path.Combine(pkgDir, "tools", "Roslyn45"),
            Path.Combine(pkgDir, "tools", "Roslyn"),
            Path.Combine(pkgDir, "content", "bin", "Roslyn"),
        }.FirstOrDefault(Directory.Exists);

        if (roslynSrc != null)
        {
            var roslynDst = Path.Combine(binDir, "Roslyn");
            Directory.CreateDirectory(roslynDst);
            foreach (var f in Directory.EnumerateFiles(roslynSrc))
                File.Copy(f, Path.Combine(roslynDst, Path.GetFileName(f)), overwrite: true);
            progress.Report($"Copied Roslyn compilers → bin\\Roslyn\\");
        }

        return true;
    }
}
