using DeploymentTool.Helpers;

namespace DeploymentTool.Services;

public interface IIISDeploymentService
{
    Task CreateAppPoolAsync(string hostname, string username, string password,
        string poolName, string clrVersion, string pipelineMode, bool enable32Bit,
        Action<string> log, CancellationToken ct = default);

    Task StartAppPoolAsync(string hostname, string username, string password,
        string poolName, Action<string> log, CancellationToken ct = default);

    Task CreateIisApplicationAsync(string hostname, string username, string password,
        string siteName, string alias, string physicalPath, string poolName,
        Action<string> log, CancellationToken ct = default);

    Task CreateIisSiteAsync(string hostname, string username, string password,
        string siteName, string physicalPath, string poolName, int port,
        Action<string> log, CancellationToken ct = default);
}

public class IISDeploymentService : IIISDeploymentService
{
    public Task CreateAppPoolAsync(
        string hostname, string username, string password,
        string poolName, string clrVersion, string pipelineMode, bool enable32Bit,
        Action<string> log, CancellationToken ct = default)
    {
        var script = $@"
Import-Module WebAdministration -ErrorAction SilentlyContinue
$poolName = '{Ps(poolName)}'
if (-not (Test-Path ""IIS:\AppPools\$poolName"")) {{
    New-WebAppPool -Name $poolName | Out-Null
    Write-Output ""Created app pool: $poolName""
}} else {{
    Write-Output ""App pool already exists: $poolName""
}}
Set-ItemProperty ""IIS:\AppPools\$poolName"" managedRuntimeVersion '{Ps(clrVersion)}'
Set-ItemProperty ""IIS:\AppPools\$poolName"" managedPipelineMode    '{Ps(pipelineMode)}'
Set-ItemProperty ""IIS:\AppPools\$poolName"" enable32BitAppOnWin64   {(enable32Bit ? "$true" : "$false")}
Write-Output ""App pool configured: runtime={clrVersion}, pipeline={pipelineMode}, 32-bit={enable32Bit}""
";
        return PowerShellExecutor.RunRemoteScriptAsync(hostname, username, password, script, log, ct);
    }

    public Task StartAppPoolAsync(
        string hostname, string username, string password,
        string poolName, Action<string> log, CancellationToken ct = default)
    {
        var script = $@"
Import-Module WebAdministration -ErrorAction SilentlyContinue
$poolName = '{Ps(poolName)}'
$state = (Get-WebConfigurationProperty system.applicationHost/applicationPools/add[@name='$poolName'] -Name state).Value
if ($state -ne 'Started') {{
    Start-WebAppPool -Name $poolName
    Write-Output ""Started app pool: $poolName""
}} else {{
    Write-Output ""App pool already running: $poolName""
}}
";
        return PowerShellExecutor.RunRemoteScriptAsync(hostname, username, password, script, log, ct);
    }

    public Task CreateIisApplicationAsync(
        string hostname, string username, string password,
        string siteName, string alias, string physicalPath, string poolName,
        Action<string> log, CancellationToken ct = default)
    {
        var script = $@"
Import-Module WebAdministration -ErrorAction SilentlyContinue
$existing = Get-WebApplication -Site '{Ps(siteName)}' | Where-Object {{ $_.path -eq '/{Ps(alias)}' }}
if ($existing) {{
    Write-Output 'IIS Application already exists: /{Ps(alias)}'
}} else {{
    New-WebApplication -Name '{Ps(alias)}' -Site '{Ps(siteName)}' `
        -PhysicalPath '{Ps(physicalPath)}' -ApplicationPool '{Ps(poolName)}' -Force | Out-Null
    Write-Output 'Created IIS Application: /{Ps(alias)} under {Ps(siteName)}'
}}
";
        return PowerShellExecutor.RunRemoteScriptAsync(hostname, username, password, script, log, ct);
    }

    public Task CreateIisSiteAsync(
        string hostname, string username, string password,
        string siteName, string physicalPath, string poolName, int port,
        Action<string> log, CancellationToken ct = default)
    {
        var script = $@"
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (Get-Website -Name '{Ps(siteName)}') {{
    Write-Output 'IIS Site already exists: {Ps(siteName)}'
}} else {{
    New-Website -Name '{Ps(siteName)}' -PhysicalPath '{Ps(physicalPath)}' `
        -ApplicationPool '{Ps(poolName)}' -Port {port} -Force | Out-Null
    Write-Output 'Created IIS Site: {Ps(siteName)} on port {port}'
}}
";
        return PowerShellExecutor.RunRemoteScriptAsync(hostname, username, password, script, log, ct);
    }

    // Escape single quotes for PowerShell single-quoted strings.
    private static string Ps(string value) => value.Replace("'", "''");
}
