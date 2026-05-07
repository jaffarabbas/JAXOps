using System.Runtime.InteropServices;

namespace DeploymentTool.Services;

public interface IRemoteConnectionService
{
    Task<bool> TestConnectionAsync(string hostname, string username, string password, CancellationToken ct = default);
    Task ConnectAsync(string uncPath, string username, string password, CancellationToken ct = default);
    Task DisconnectAsync(string uncPath, CancellationToken ct = default);
}

public class RemoteConnectionService : IRemoteConnectionService
{
    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2W(ref NETRESOURCE lpNetResource, string? lpPassword, string? lpUserName, uint dwFlags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2W(string lpName, uint dwFlags, bool bForce);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NETRESOURCE
    {
        public uint dwScope;
        public uint dwType;
        public uint dwDisplayType;
        public uint dwUsage;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpLocalName;
        [MarshalAs(UnmanagedType.LPWStr)] public string  lpRemoteName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpComment;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpProvider;
    }

    private const uint RESOURCETYPE_DISK = 1;
    private const int  ERROR_SESSION_CREDENTIAL_CONFLICT = 1219;
    private const int  ERROR_ALREADY_ASSIGNED            = 85;

    // ── Public API ────────────────────────────────────────────────────────────

    public Task<bool> TestConnectionAsync(string hostname, string username, string password, CancellationToken ct = default)
        => Task.Run(() => TryConnect($@"\\{hostname}\IPC$", username, password, probe: true), ct);

    public Task ConnectAsync(string uncPath, string username, string password, CancellationToken ct = default)
        => Task.Run(() =>
        {
            // WNetAddConnection2 only accepts a share root (\\SERVER\SHARE),
            // not a subdirectory (\\SERVER\SHARE\folder).  Extract it first.
            var shareRoot = ExtractShareRoot(uncPath);
            bool ok = TryConnect(shareRoot, username, password, probe: false);
            if (!ok)
                throw new InvalidOperationException(
                    $"Failed to connect to {shareRoot}.\n" +
                    "Check: credentials, File & Printer Sharing firewall rule, and that C$ admin share is enabled.");
        }, ct);

    public Task DisconnectAsync(string uncPath, CancellationToken ct = default)
        => Task.Run(() => WNetCancelConnection2W(ExtractShareRoot(uncPath), 0, true), ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    // \\SERVER\C$\inetpub\wwwroot  →  \\SERVER\C$
    private static string ExtractShareRoot(string uncPath)
    {
        if (!uncPath.StartsWith(@"\\"))
            return uncPath;

        var parts = uncPath.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $@"\\{parts[0]}\{parts[1]}" : uncPath;
    }

    private static bool TryConnect(string shareRoot, string username, string password, bool probe)
    {
        var resource = new NETRESOURCE
        {
            dwType       = RESOURCETYPE_DISK,
            lpRemoteName = shareRoot
        };

        // Pass null for empty password so WNet uses an empty string correctly.
        string? pwd = string.IsNullOrEmpty(password) ? null : password;

        int result = WNetAddConnection2W(ref resource, pwd, username, 0);

        switch (result)
        {
            case 0:
                if (probe) WNetCancelConnection2W(shareRoot, 0, true);
                return true;

            // Already connected with the same credentials — success.
            case ERROR_SESSION_CREDENTIAL_CONFLICT:
            case ERROR_ALREADY_ASSIGNED:
                return true;

            default:
                return false;
        }
    }
}
