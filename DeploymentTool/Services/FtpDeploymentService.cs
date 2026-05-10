using System.IO;
using System.Net;

namespace DeploymentTool.Services;

public interface IFtpDeploymentService
{
    Task<bool> TestConnectionAsync(string host, int port, string username, string password, bool passive, CancellationToken ct = default);
    Task EnsureFolderAsync(string ftpFolderUrl, string username, string password, bool passive, CancellationToken ct = default);
    Task UploadFolderAsync(string localFolder, string ftpFolderUrl, string username, string password, bool passive, bool overwrite, IProgress<(int copied, int total, string file)> progress, Action<string> log, CancellationToken ct = default);
    Task UploadSingleFileAsync(string localFilePath, string ftpFileUrl, string username, string password, bool passive, CancellationToken ct = default);
}

public class FtpDeploymentService : IFtpDeploymentService
{
    public async Task<bool> TestConnectionAsync(string host, int port, string username, string password, bool passive, CancellationToken ct = default)
    {
        try
        {
            var req = CreateRequest($"ftp://{host}:{port}/", WebRequestMethods.Ftp.ListDirectory, username, password, passive);
            using var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false);
            return true;
        }
        catch { return false; }
    }

    public async Task EnsureFolderAsync(string ftpFolderUrl, string username, string password, bool passive, CancellationToken ct = default)
    {
        var uri   = new Uri(ftpFolderUrl);
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var base_ = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        var cur   = base_;

        foreach (var part in parts)
        {
            cur += "/" + part;
            await TryMakeDirectoryAsync(cur, username, password, passive).ConfigureAwait(false);
        }
    }

    public async Task UploadFolderAsync(
        string localFolder, string ftpFolderUrl,
        string username, string password, bool passive, bool overwrite,
        IProgress<(int copied, int total, string file)> progress,
        Action<string> log, CancellationToken ct = default)
    {
        var files = Directory.EnumerateFiles(localFolder, "*", SearchOption.AllDirectories).ToList();
        var total  = files.Count;
        var copied = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var rel    = Path.GetRelativePath(localFolder, file).Replace('\\', '/');
            var subDir = Path.GetDirectoryName(rel.Replace('/', Path.DirectorySeparatorChar))
                             ?.Replace(Path.DirectorySeparatorChar, '/');

            if (!string.IsNullOrEmpty(subDir))
                await EnsureFolderAsync($"{ftpFolderUrl.TrimEnd('/')}/{subDir}", username, password, passive, ct)
                    .ConfigureAwait(false);

            var ftpUrl = $"{ftpFolderUrl.TrimEnd('/')}/{rel}";
            log($"FTP upload: {rel}");
            await UploadFileAsync(file, ftpUrl, username, password, passive, ct).ConfigureAwait(false);

            copied++;
            progress.Report((copied, total, (string)rel));
        }
    }

    public Task UploadSingleFileAsync(string localFilePath, string ftpFileUrl, string username, string password, bool passive, CancellationToken ct = default)
        => UploadFileAsync(localFilePath, ftpFileUrl, username, password, passive, ct);

    private static async Task UploadFileAsync(string localPath, string ftpUrl, string username, string password, bool passive, CancellationToken ct)
    {
        var req = CreateRequest(ftpUrl, WebRequestMethods.Ftp.UploadFile, username, password, passive);
        using var fs  = File.OpenRead(localPath);
        using var req_ = await req.GetRequestStreamAsync().ConfigureAwait(false);
        await fs.CopyToAsync(req_, 81920, ct).ConfigureAwait(false);
        using var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false);
    }

    private static async Task TryMakeDirectoryAsync(string ftpUrl, string username, string password, bool passive)
    {
        try
        {
            var req = CreateRequest(ftpUrl, WebRequestMethods.Ftp.MakeDirectory, username, password, passive);
            using var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false);
        }
        catch (WebException ex) when (ex.Response is FtpWebResponse r &&
            (r.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable ||
             r.StatusCode == FtpStatusCode.ActionNotTakenFilenameNotAllowed))
        {
            // Already exists — ok
        }
    }

    private static FtpWebRequest CreateRequest(string url, string method, string username, string password, bool passive)
    {
        var req = (FtpWebRequest)WebRequest.Create(url);
        req.Method      = method;
        req.Credentials = new NetworkCredential(username, password);
        req.UsePassive  = passive;
        req.UseBinary   = true;
        req.KeepAlive   = false;
        return req;
    }
}
