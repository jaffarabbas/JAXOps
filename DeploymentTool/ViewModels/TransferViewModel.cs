using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Input;
using DeploymentTool.Helpers;
using DeploymentTool.Models;
using DeploymentTool.Services;

namespace DeploymentTool.ViewModels;

public class TransferViewModel : BaseViewModel
{
    private readonly IFtpDeploymentService    _ftpService;
    private readonly IRemoteConnectionService _connectionService;
    private readonly IFileDeploymentService   _fileService;
    private CancellationTokenSource? _cts;

    // ── Connection ─────────────────────────────────────────────────────────
    private string _serverHostname = string.Empty;
    private string _username       = string.Empty;
    private string _password       = string.Empty;

    // ── Transfer mode ──────────────────────────────────────────────────────
    private bool _useFtp = true;

    // ── FTP ────────────────────────────────────────────────────────────────
    private string _ftpRootPath    = "/";
    private int    _ftpPort        = 21;
    private bool   _ftpPassiveMode = true;

    // ── SMB ────────────────────────────────────────────────────────────────
    private string _shareName      = string.Empty;
    private string _deploymentRoot = string.Empty;

    // ── Source ────────────────────────────────────────────────────────────
    private string _sourceFolderPath = string.Empty;

    // ── State ─────────────────────────────────────────────────────────────
    private bool   _isTransferring   = false;
    private string _currentStatus    = "Ready";
    private double _transferProgress = 0;

    // ── Collections ───────────────────────────────────────────────────────
    public ObservableCollection<DeploymentLogItem> TransferLog { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────
    public ICommand TestConnectionCommand { get; }
    public ICommand ZipAndTransferCommand { get; }
    public ICommand CancelCommand         { get; }
    public ICommand BrowseSourceCommand   { get; }
    public ICommand ClearLogCommand       { get; }

    // ── Connection properties ──────────────────────────────────────────────
    public string ServerHostname
    {
        get => _serverHostname;
        set { if (SetField(ref _serverHostname, value)) InvalidateCommands(); }
    }

    public string Username
    {
        get => _username;
        set => SetField(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetField(ref _password, value);
    }

    // ── Transfer mode ──────────────────────────────────────────────────────
    public bool UseFtp
    {
        get => _useFtp;
        set
        {
            if (SetField(ref _useFtp, value))
            {
                OnPropertyChanged(nameof(UseSmb));
                InvalidateCommands();
            }
        }
    }

    public bool UseSmb
    {
        get => !_useFtp;
        set { if (value) UseFtp = false; }
    }

    // ── FTP properties ─────────────────────────────────────────────────────
    public string FtpRootPath
    {
        get => _ftpRootPath;
        set => SetField(ref _ftpRootPath, value);
    }

    public int FtpPort
    {
        get => _ftpPort;
        set => SetField(ref _ftpPort, value);
    }

    public bool FtpPassiveMode
    {
        get => _ftpPassiveMode;
        set => SetField(ref _ftpPassiveMode, value);
    }

    // ── SMB properties ─────────────────────────────────────────────────────
    public string ShareName
    {
        get => _shareName;
        set => SetField(ref _shareName, value);
    }

    public string DeploymentRoot
    {
        get => _deploymentRoot;
        set { if (SetField(ref _deploymentRoot, value)) InvalidateCommands(); }
    }

    // ── Source ─────────────────────────────────────────────────────────────
    public string SourceFolderPath
    {
        get => _sourceFolderPath;
        set { if (SetField(ref _sourceFolderPath, value)) InvalidateCommands(); }
    }

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsTransferring
    {
        get => _isTransferring;
        private set { if (SetField(ref _isTransferring, value)) InvalidateCommands(); }
    }

    public string CurrentStatus
    {
        get => _currentStatus;
        private set => SetField(ref _currentStatus, value);
    }

    public double TransferProgress
    {
        get => _transferProgress;
        private set => SetField(ref _transferProgress, value);
    }

    // ── Constructor ────────────────────────────────────────────────────────
    public TransferViewModel()
    {
        _ftpService        = new FtpDeploymentService();
        _connectionService = new RemoteConnectionService();
        _fileService       = new FileDeploymentService();

        TestConnectionCommand = new RelayCommand(
            async () =>
            {
                try { await TestConnectionAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}", DeployLogSeverity.Error); }
            },
            () => !IsTransferring && !string.IsNullOrWhiteSpace(_serverHostname));

        ZipAndTransferCommand = new RelayCommand(
            async () =>
            {
                try { await RunZipAndTransferAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}", DeployLogSeverity.Error); }
            },
            () => !IsTransferring && CanTransfer());

        CancelCommand = new RelayCommand(
            () => _cts?.Cancel(),
            () => IsTransferring);

        BrowseSourceCommand = new RelayCommand(BrowseSourceFolder, () => !IsTransferring);

        ClearLogCommand = new RelayCommand(() => TransferLog.Clear());
    }

    // ── Command implementations ────────────────────────────────────────────

    private async Task TestConnectionAsync()
    {
        IsTransferring = true;
        CurrentStatus  = "Testing connection…";
        AppendLog($"Testing connection to {_serverHostname} ({(_useFtp ? "FTP" : "SMB")})…");

        try
        {
            bool ok;
            if (_useFtp)
                ok = await _ftpService.TestConnectionAsync(_serverHostname, _ftpPort, _username, _password, _ftpPassiveMode).ConfigureAwait(false);
            else
                ok = await _connectionService.TestConnectionAsync(_serverHostname, _username, _password).ConfigureAwait(false);

            CurrentStatus = ok ? $"Connected to {_serverHostname}" : "Connection failed";
            AppendLog(
                ok ? $"Connection successful: {_serverHostname}" : "Connection failed — check hostname, credentials, and firewall.",
                ok ? DeployLogSeverity.Success : DeployLogSeverity.Error);
        }
        catch (Exception ex)
        {
            CurrentStatus = "Connection error";
            AppendLog($"Connection error: {ex.Message}", DeployLogSeverity.Error);
        }
        finally
        {
            IsTransferring = false;
        }
    }

    private async Task RunZipAndTransferAsync()
    {
        _cts             = new CancellationTokenSource();
        IsTransferring   = true;
        TransferProgress = 0;
        CurrentStatus    = "Starting transfer…";

        var folderName = Path.GetFileName(_sourceFolderPath.TrimEnd('\\', '/'));
        var zipName    = $"{folderName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        var zipPath    = Path.Combine(Path.GetTempPath(), zipName);

        try
        {
            // ── Step 1: ZIP the folder ─────────────────────────────────────
            CurrentStatus = "Zipping folder…";
            AppendLog($"Zipping: {_sourceFolderPath}");
            TransferProgress = 10;

            await Task.Run(() =>
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(_sourceFolderPath, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }, _cts.Token).ConfigureAwait(false);

            _cts.Token.ThrowIfCancellationRequested();

            var zipSize = new FileInfo(zipPath).Length;
            AppendLog($"ZIP created: {zipName}  ({zipSize / 1024.0:F1} KB)", DeployLogSeverity.Success);
            TransferProgress = 40;

            // ── Step 2: Upload or copy the ZIP ─────────────────────────────
            if (_useFtp)
            {
                CurrentStatus = "Uploading ZIP via FTP…";
                var ftpFolder = $"ftp://{_serverHostname}:{_ftpPort}{_ftpRootPath.TrimEnd('/')}";
                AppendLog($"FTP target: {ftpFolder}/{zipName}");

                await _ftpService.EnsureFolderAsync(ftpFolder, _username, _password, _ftpPassiveMode, _cts.Token).ConfigureAwait(false);

                var ftpFileUrl = $"{ftpFolder}/{zipName}";
                AppendLog($"Uploading {zipName}…");
                await _ftpService.UploadSingleFileAsync(zipPath, ftpFileUrl, _username, _password, _ftpPassiveMode, _cts.Token).ConfigureAwait(false);
            }
            else
            {
                // SMB: copy ZIP to the share
                CurrentStatus = "Copying ZIP via SMB…";
                var uncRoot = string.IsNullOrWhiteSpace(_shareName)
                    ? BuildUncPath(_serverHostname, _deploymentRoot)
                    : $@"\\{_serverHostname}\{_shareName.TrimStart('\\')}";

                AppendLog($"Connecting to {uncRoot}…");
                await _connectionService.ConnectAsync(uncRoot, _username, _password, _cts.Token).ConfigureAwait(false);

                await _fileService.CreateFolderStructureAsync(uncRoot, _cts.Token).ConfigureAwait(false);
                var destPath = Path.Combine(uncRoot, zipName);

                AppendLog($"Copying {zipName} → {destPath}");
                await Task.Run(() => File.Copy(zipPath, destPath, overwrite: true), _cts.Token).ConfigureAwait(false);

                try { await _connectionService.DisconnectAsync(uncRoot, _cts.Token).ConfigureAwait(false); }
                catch { /* best-effort */ }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                TransferProgress = 100;
                CurrentStatus    = "Transfer complete";
                AppendLog($"Completed: {zipName}", DeployLogSeverity.Success);
                MessageBox.Show($"ZIP uploaded successfully:\n{zipName}", "Transfer Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
        catch (OperationCanceledException)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentStatus = "Transfer cancelled";
                AppendLog("Transfer was cancelled.", DeployLogSeverity.Warning);
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentStatus = "Transfer failed";
                AppendLog($"ERROR: {ex.Message}", DeployLogSeverity.Error);
                MessageBox.Show($"Transfer failed:\n{ex.Message}", "Transfer Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
        finally
        {
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            Application.Current.Dispatcher.Invoke(() => IsTransferring = false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanTransfer()
        => !string.IsNullOrWhiteSpace(_serverHostname)
        && !string.IsNullOrWhiteSpace(_username)
        && !string.IsNullOrWhiteSpace(_sourceFolderPath)
        && Directory.Exists(_sourceFolderPath)
        && (_useFtp || !string.IsNullOrWhiteSpace(_deploymentRoot));

    private static string BuildUncPath(string hostname, string rootPath)
    {
        if (rootPath.StartsWith(@"\\")) return rootPath;
        var drive = rootPath[0];
        var rest  = rootPath.Length > 3 ? rootPath[3..] : string.Empty;
        return string.IsNullOrEmpty(rest)
            ? $@"\\{hostname}\{drive}$"
            : $@"\\{hostname}\{drive}$\{rest}";
    }

    private void BrowseSourceFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Patch / Publish Folder to Transfer"
        };
        if (dlg.ShowDialog() == true)
            SourceFolderPath = dlg.FolderName;
    }

    private void AppendLog(string message, DeployLogSeverity severity = DeployLogSeverity.Info)
        => Application.Current.Dispatcher.Invoke(() =>
            TransferLog.Add(new DeploymentLogItem
            {
                Timestamp = DateTime.Now,
                Message   = message,
                Severity  = severity
            }));

    private void InvalidateCommands()
        => Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
}
