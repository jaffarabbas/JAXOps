using System.Diagnostics;
using System.IO;
using System.Text;

namespace DeploymentTool.Helpers;

public static class PowerShellExecutor
{
    public static async Task<bool> RunRemoteScriptAsync(
        string hostname,
        string username,
        string password,
        string scriptBody,
        Action<string> log,
        CancellationToken ct = default)
    {
        var tempScript = Path.Combine(Path.GetTempPath(), $"deploy_{Guid.NewGuid():N}.ps1");

        // Single-quoted password prevents PowerShell from interpreting special chars in the ConvertTo-SecureString call.
        // Apostrophes inside the password are doubled to escape them.
        var escaped = password.Replace("'", "''");
        var wrapper = new StringBuilder();
        wrapper.AppendLine("$ErrorActionPreference = 'Stop'");
        wrapper.AppendLine($"$_secPwd = ConvertTo-SecureString '{escaped}' -AsPlainText -Force");
        wrapper.AppendLine($"$_cred   = New-Object System.Management.Automation.PSCredential('{username.Replace("'", "''")}', $_secPwd)");
        wrapper.AppendLine($"Invoke-Command -ComputerName '{hostname.Replace("'", "''")}' -Credential $_cred -ScriptBlock {{");
        wrapper.AppendLine(scriptBody);
        wrapper.AppendLine("} -ErrorAction Stop");

        await File.WriteAllTextAsync(tempScript, wrapper.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);

        try
        {
            return await RunScriptFileAsync(tempScript, log, ct).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(tempScript); } catch { /* best-effort cleanup */ }
        }
    }

    public static async Task<bool> RunScriptFileAsync(
        string scriptPath,
        Action<string> log,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            Arguments = $"-ExecutionPolicy Bypass -NonInteractive -File \"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data != null) log($"PS-ERR: {e.Data}"); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode == 0;
    }
}
