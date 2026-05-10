using System.Diagnostics;
using System.IO;
using System.Text;

namespace DeploymentTool.Helpers;

/// <summary>
/// Writes a text file, automatically re-attempting with UAC elevation if the
/// direct write fails due to insufficient permissions (e.g. IIS publish folders).
/// </summary>
public static class ElevatedFileWriter
{
    /// <summary>
    /// Tries a normal async write first. On UnauthorizedAccess / access-denied
    /// IOException, writes to a temp file then copies it to the target via an
    /// elevated PowerShell process (triggers a UAC prompt).
    /// </summary>
    public static async Task WriteAllTextAsync(
        string path,
        string content,
        Encoding encoding,
        CancellationToken ct = default)
    {
        try
        {
            await File.WriteAllTextAsync(path, content, encoding, ct);
        }
        catch (Exception ex) when (IsAccessDenied(ex))
        {
            await WriteElevatedAsync(path, content, encoding, ct);
        }
    }

    // ── Internals ──────────────────────────────────────────────────────────────

    private static async Task WriteElevatedAsync(
        string path,
        string content,
        Encoding encoding,
        CancellationToken ct)
    {
        // 1. Write content to a temp file the current user owns
        var tempFile   = Path.Combine(Path.GetTempPath(), $"jaxops_{Guid.NewGuid():N}.tmp");
        var scriptFile = Path.Combine(Path.GetTempPath(), $"jaxops_{Guid.NewGuid():N}.ps1");

        await File.WriteAllTextAsync(tempFile, content, encoding, ct);

        // 2. Build a tiny PowerShell script to move the temp file into place.
        //    Using a script file avoids any quoting issues with paths that
        //    contain spaces or special characters.
        var script = new StringBuilder();
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        script.AppendLine($"Copy-Item \"{EscapePs(tempFile)}\" \"{EscapePs(path)}\" -Force");
        script.AppendLine($"Remove-Item \"{EscapePs(tempFile)}\" -Force -ErrorAction SilentlyContinue");
        await File.WriteAllTextAsync(scriptFile, script.ToString(), Encoding.UTF8, ct);

        try
        {
            // 3. Launch PowerShell with 'runas' verb → triggers UAC prompt
            var psi = new ProcessStartInfo("powershell.exe")
            {
                Arguments       = $"-ExecutionPolicy Bypass -NonInteractive -File \"{scriptFile}\"",
                Verb            = "runas",
                UseShellExecute = true,  // required for runas verb
                CreateNoWindow  = false
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start elevated PowerShell process.");

            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
                throw new IOException(
                    $"Elevated save failed (PowerShell exit code {proc.ExitCode}). " +
                    "Make sure you accepted the UAC prompt.");
        }
        finally
        {
            try { File.Delete(scriptFile); } catch { /* best-effort */ }
            try { File.Delete(tempFile);   } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Escapes double-quote characters inside a path used within a PowerShell
    /// double-quoted string. Backtick is the PS escape character.
    /// </summary>
    private static string EscapePs(string path) => path.Replace("\"", "`\"");

    private static bool IsAccessDenied(Exception ex)
    {
        if (ex is UnauthorizedAccessException) return true;
        if (ex is IOException io)
        {
            const int ERROR_ACCESS_DENIED  = unchecked((int)0x80070005);
            const int ERROR_SHARING        = unchecked((int)0x80070020);
            return io.HResult == ERROR_ACCESS_DENIED || io.HResult == ERROR_SHARING;
        }
        return false;
    }
}
