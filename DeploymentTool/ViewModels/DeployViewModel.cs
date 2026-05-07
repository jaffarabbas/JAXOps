using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DeploymentTool.Helpers;
using DeploymentTool.Models;
using DeploymentTool.Services;

namespace DeploymentTool.ViewModels;

public class DeployViewModel : BaseViewModel
{
    private readonly Func<IReadOnlyList<ProjectItem>>  _getSelectedProjects;
    private readonly Func<string>                      _getPublishOutputFolder;
    private readonly Action<string>                    _mainLog;
    private readonly IRemoteConnectionService          _connectionService;
    private readonly IFileDeploymentService            _fileService;
    private readonly IIISDeploymentService             _iisService;
    private readonly IFtpDeploymentService             _ftpService;
    private readonly DeploymentOrchestratorService     _orchestrator;

    private CancellationTokenSource? _cts;

    // ── Shared connection ────────────────────────────────────────────────────
    private string _serverHostname = string.Empty;
    private string _username       = string.Empty;
    private string _password       = string.Empty;
    private string _deploymentRoot = string.Empty;

    // ── Optional custom share name (replaces C$ admin share) ────────────────
    private string _shareName = string.Empty;

    // ── FTP mode ─────────────────────────────────────────────────────────────
    private bool   _useFtp         = false;
    private string _ftpRootPath    = "/";
    private int    _ftpPort        = 21;
    private bool   _ftpPassiveMode = true;

    // ── Shared IIS config ────────────────────────────────────────────────────
    private string _iisSiteName = string.Empty;

    // ── Single-pool fields (used when UsePerProjectPools = false) ────────────
    private string _appPoolName      = string.Empty;
    private string _applicationAlias = string.Empty;
    private string _dotNetClrVersion = "v4.0";
    private string _pipelineMode     = "Integrated";
    private bool   _enable32Bit      = false;

    // ── Shared deploy options ────────────────────────────────────────────────
    private bool _createAppPool           = true;
    private bool _createIisApplication    = true;
    private bool _startAppPoolAfterDeploy = true;
    private bool _overwriteExistingFiles  = true;

    // ── Custom source path (bypasses Publish tab project selection) ─────────
    private string _customPublishPath = string.Empty;

    // ── Pool mode ────────────────────────────────────────────────────────────
    private bool   _usePerProjectPools = false;
    private string _poolNamingPrefix   = string.Empty;

    // ── State ────────────────────────────────────────────────────────────────
    private bool   _isDeploying    = false;
    private string _currentStatus  = "Ready";
    private double _deployProgress = 0;

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<DeploymentLogItem>  DeployLog           { get; } = [];
    public ObservableCollection<ProjectDeployConfig> ProjectDeployConfigs { get; } = [];

    public IReadOnlyList<string> ClrVersionOptions   { get; } = ["v4.0", "v2.0", "No Managed Code"];
    public IReadOnlyList<string> PipelineModeOptions { get; } = ["Integrated", "Classic"];

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand TestConnectionCommand          { get; }
    public ICommand DeployCommand                  { get; }
    public ICommand CancelDeployCommand            { get; }
    public ICommand ClearDeployLogCommand          { get; }
    public ICommand RefreshProjectConfigsCommand   { get; }
    public ICommand SelectAllConfigsCommand        { get; }
    public ICommand ClearAllConfigsCommand         { get; }
    public ICommand BrowseCustomPublishPathCommand { get; }

    // ── Shared connection properties ──────────────────────────────────────────
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

    public string DeploymentRoot
    {
        get => _deploymentRoot;
        set { if (SetField(ref _deploymentRoot, value)) InvalidateCommands(); }
    }

    public string ShareName
    {
        get => _shareName;
        set => SetField(ref _shareName, value);
    }

    // ── FTP properties ────────────────────────────────────────────────────────
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

    public string IisSiteName
    {
        get => _iisSiteName;
        set => SetField(ref _iisSiteName, value);
    }

    // ── Custom publish source path ────────────────────────────────────────────
    public string CustomPublishPath
    {
        get => _customPublishPath;
        set { if (SetField(ref _customPublishPath, value)) InvalidateCommands(); }
    }

    // ── Single-pool properties ────────────────────────────────────────────────
    public string AppPoolName
    {
        get => _appPoolName;
        set { if (SetField(ref _appPoolName, value)) InvalidateCommands(); }
    }

    public string ApplicationAlias
    {
        get => _applicationAlias;
        set => SetField(ref _applicationAlias, value);
    }

    public string DotNetClrVersion
    {
        get => _dotNetClrVersion;
        set => SetField(ref _dotNetClrVersion, value);
    }

    public string PipelineMode
    {
        get => _pipelineMode;
        set => SetField(ref _pipelineMode, value);
    }

    public bool Enable32Bit
    {
        get => _enable32Bit;
        set => SetField(ref _enable32Bit, value);
    }

    // ── Shared deploy option properties ──────────────────────────────────────
    public bool CreateAppPool
    {
        get => _createAppPool;
        set => SetField(ref _createAppPool, value);
    }

    public bool CreateIisApplication
    {
        get => _createIisApplication;
        set => SetField(ref _createIisApplication, value);
    }

    public bool StartAppPoolAfterDeploy
    {
        get => _startAppPoolAfterDeploy;
        set => SetField(ref _startAppPoolAfterDeploy, value);
    }

    public bool OverwriteExistingFiles
    {
        get => _overwriteExistingFiles;
        set => SetField(ref _overwriteExistingFiles, value);
    }

    // ── Pool mode properties ──────────────────────────────────────────────────
    public bool UsePerProjectPools
    {
        get => _usePerProjectPools;
        set
        {
            if (SetField(ref _usePerProjectPools, value))
            {
                OnPropertyChanged(nameof(UseSinglePool));
                InvalidateCommands();
            }
        }
    }

    // Inverse of UsePerProjectPools — bound to the "Single Pool" RadioButton.
    public bool UseSinglePool
    {
        get => !_usePerProjectPools;
        set { if (value) UsePerProjectPools = false; }
    }

    public string PoolNamingPrefix
    {
        get => _poolNamingPrefix;
        set => SetField(ref _poolNamingPrefix, value);
    }

    // ── State properties ──────────────────────────────────────────────────────
    public bool IsDeploying
    {
        get => _isDeploying;
        private set { if (SetField(ref _isDeploying, value)) InvalidateCommands(); }
    }

    public string CurrentStatus
    {
        get => _currentStatus;
        private set => SetField(ref _currentStatus, value);
    }

    public double DeployProgress
    {
        get => _deployProgress;
        private set => SetField(ref _deployProgress, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public DeployViewModel(
        Func<IReadOnlyList<ProjectItem>> getSelectedProjects,
        Func<string>                     getPublishOutputFolder,
        Action<string>                   mainLog)
    {
        _getSelectedProjects    = getSelectedProjects;
        _getPublishOutputFolder = getPublishOutputFolder;
        _mainLog                = mainLog;

        _connectionService = new RemoteConnectionService();
        _fileService       = new FileDeploymentService();
        _iisService        = new IISDeploymentService();
        _ftpService        = new FtpDeploymentService();
        _orchestrator      = new DeploymentOrchestratorService(_connectionService, _fileService, _iisService, _ftpService);

        TestConnectionCommand = new RelayCommand(
            async () =>
            {
                try { await TestConnectionAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}", DeployLogSeverity.Error); }
            },
            () => !IsDeploying && !string.IsNullOrWhiteSpace(_serverHostname));

        DeployCommand = new RelayCommand(
            async () =>
            {
                try { await RunDeployAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}", DeployLogSeverity.Error); }
            },
            () => !IsDeploying && CanDeploy());

        CancelDeployCommand = new RelayCommand(
            () => _cts?.Cancel(),
            () => IsDeploying);

        ClearDeployLogCommand = new RelayCommand(() => DeployLog.Clear());

        RefreshProjectConfigsCommand = new RelayCommand(
            RefreshProjectConfigs,
            () => !IsDeploying);

        SelectAllConfigsCommand = new RelayCommand(
            () => { foreach (var c in ProjectDeployConfigs) c.IsSelected = true; },
            () => ProjectDeployConfigs.Count > 0);

        ClearAllConfigsCommand = new RelayCommand(
            () => { foreach (var c in ProjectDeployConfigs) c.IsSelected = false; },
            () => ProjectDeployConfigs.Count > 0);

        BrowseCustomPublishPathCommand = new RelayCommand(BrowseCustomPublishPath, () => !IsDeploying);
    }

    // ── Commands implementation ───────────────────────────────────────────────

    private void RefreshProjectConfigs()
    {
        var selected = _getSelectedProjects();

        if (selected.Count == 0)
        {
            MessageBox.Show(
                "Select at least one project on the Publish tab first, then refresh.",
                "No Projects Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Preserve any existing user edits by keying on ProjectName.
        var existing = ProjectDeployConfigs.ToDictionary(c => c.ProjectName, StringComparer.OrdinalIgnoreCase);

        ProjectDeployConfigs.Clear();

        foreach (var project in selected)
        {
            if (existing.TryGetValue(project.Name, out var prev))
            {
                // Re-add the already-configured entry unchanged.
                ProjectDeployConfigs.Add(prev);
            }
            else
            {
                var prefix   = _poolNamingPrefix.Trim();
                var poolName = string.IsNullOrEmpty(prefix) ? project.Name : $"{prefix}{project.Name}";

                ProjectDeployConfigs.Add(new ProjectDeployConfig
                {
                    ProjectName      = project.Name,
                    AppPoolName      = poolName,
                    ApplicationAlias = project.Name,
                    DotNetClrVersion = _dotNetClrVersion,
                    PipelineMode     = _pipelineMode,
                    Enable32Bit      = _enable32Bit,
                    IsSelected       = true
                });
            }
        }

        AppendLog($"Refreshed {ProjectDeployConfigs.Count} project pool config(s).", DeployLogSeverity.Info);
        InvalidateCommands();
    }

    private async Task TestConnectionAsync()
    {
        IsDeploying   = true;
        CurrentStatus = "Testing connection…";
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
            IsDeploying = false;
        }
    }

    private async Task RunDeployAsync()
    {
        // Build the per-project config list for whichever mode is active.
        IReadOnlyList<ProjectDeployConfig> configs;

        if (!string.IsNullOrWhiteSpace(_customPublishPath))
        {
            // Custom path mode — deploy the specified folder directly, no project selection needed.
            var folderName = System.IO.Path.GetFileName(_customPublishPath.TrimEnd('\\', '/'));
            var alias      = string.IsNullOrWhiteSpace(_applicationAlias) ? folderName : _applicationAlias;
            var poolName   = string.IsNullOrWhiteSpace(_appPoolName)      ? alias      : _appPoolName;

            configs = new[]
            {
                new ProjectDeployConfig
                {
                    ProjectName        = alias,
                    SourceOverridePath = _customPublishPath,
                    AppPoolName        = poolName,
                    ApplicationAlias   = alias,
                    DotNetClrVersion   = _dotNetClrVersion,
                    PipelineMode       = _pipelineMode,
                    Enable32Bit        = _enable32Bit,
                    IsSelected         = true
                }
            };
        }
        else if (_usePerProjectPools)
        {
            configs = ProjectDeployConfigs.Where(c => c.IsSelected).ToList();
            if (configs.Count == 0)
            {
                MessageBox.Show(
                    "No projects are selected in the Per-Project Pool grid.\nClick \"Refresh from Projects\" to populate it.",
                    "Nothing to Deploy",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            var selected = _getSelectedProjects();
            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "Select at least one project on the Publish tab first, or set a Custom Publish Path on the Deploy tab.",
                    "No Source Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            configs = selected.Select(p => new ProjectDeployConfig
            {
                ProjectName      = p.Name,
                AppPoolName      = _appPoolName,
                ApplicationAlias = string.IsNullOrWhiteSpace(_applicationAlias) ? p.Name : _applicationAlias,
                DotNetClrVersion = _dotNetClrVersion,
                PipelineMode     = _pipelineMode,
                Enable32Bit      = _enable32Bit,
                IsSelected       = true
            }).ToList();
        }

        var publishOutput = _getPublishOutputFolder();
        if (string.IsNullOrWhiteSpace(publishOutput))
        {
            MessageBox.Show(
                "Set a Publish Output Folder on the Publish tab before deploying.",
                "Missing Publish Output",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cts          = new CancellationTokenSource();
        IsDeploying   = true;
        DeployProgress = 0;
        CurrentStatus  = "Starting deployment…";

        var profile = BuildSharedProfile();
        var mode    = _usePerProjectPools ? "per-project pools" : "single pool";
        AppendLog($"Deploying {configs.Count} project(s) → {_serverHostname}:{_deploymentRoot}  [{mode}]");
        _mainLog($"[Deploy] Starting {configs.Count} project(s) → {_serverHostname}  [{mode}]");

        var progress = new Progress<DeploymentProgress>(p =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                DeployProgress = p.Percentage;
                CurrentStatus  = p.StepName;
            }));

        try
        {
            await _orchestrator
                .DeployAsync(profile, configs, publishOutput, progress, LogLine, _cts.Token)
                .ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                DeployProgress = 100;
                CurrentStatus  = "Deployment complete";
                AppendLog("All deployments succeeded.", DeployLogSeverity.Success);
                _mainLog($"[Deploy] Completed successfully for {configs.Count} project(s).");
                MessageBox.Show("Deployment completed successfully.", "Deploy",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
        catch (OperationCanceledException)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentStatus = "Deployment cancelled";
                AppendLog("Deployment was cancelled.", DeployLogSeverity.Warning);
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentStatus = "Deployment failed";
                AppendLog($"ERROR: {ex.Message}", DeployLogSeverity.Error);
                _mainLog($"[Deploy] FAILED: {ex.Message}");
                MessageBox.Show($"Deployment failed:\n{ex.Message}", "Deploy Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() => IsDeploying = false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanDeploy()
        => !string.IsNullOrWhiteSpace(_serverHostname)
        && !string.IsNullOrWhiteSpace(_username)
        && !string.IsNullOrWhiteSpace(_deploymentRoot)
        && (_usePerProjectPools || !string.IsNullOrWhiteSpace(_appPoolName))
        // either projects are selected on Publish tab OR a custom path is provided
        && (!string.IsNullOrWhiteSpace(_customPublishPath) || _getSelectedProjects().Count > 0 || _usePerProjectPools);

    private DeploymentProfile BuildSharedProfile() => new()
    {
        ServerHostname          = _serverHostname,
        Username                = _username,
        Password                = _password,
        DeploymentRootPath      = _deploymentRoot,
        ShareName               = _shareName,
        IisSiteName             = _iisSiteName,
        CreateAppPool           = _createAppPool,
        CreateIisApplication    = _createIisApplication,
        StartAppPoolAfterDeploy = _startAppPoolAfterDeploy,
        OverwriteExistingFiles  = _overwriteExistingFiles,
        UsePerProjectPools      = _usePerProjectPools,
        UseFtp                  = _useFtp,
        FtpPort                 = _ftpPort,
        FtpRootPath             = _ftpRootPath,
        FtpPassiveMode          = _ftpPassiveMode
    };

    private void BrowseCustomPublishPath()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Published Application Folder"
        };
        if (dlg.ShowDialog() == true)
            CustomPublishPath = dlg.FolderName;
    }

    private void LogLine(string message)
    {
        var upper    = message.ToUpperInvariant();
        var severity = upper.Contains("ERROR") || upper.Contains("FATAL")   ? DeployLogSeverity.Error
                     : upper.Contains("WARN")                               ? DeployLogSeverity.Warning
                     : upper.Contains("COMPLETE") || upper.Contains("SUCCESS") || upper.Contains("STARTED") || upper.Contains("READY")
                                                                             ? DeployLogSeverity.Success
                     : DeployLogSeverity.Info;

        AppendLog(message, severity);
    }

    private void AppendLog(string message, DeployLogSeverity severity = DeployLogSeverity.Info)
        => Application.Current.Dispatcher.Invoke(() =>
            DeployLog.Add(new DeploymentLogItem
            {
                Timestamp = DateTime.Now,
                Message   = message,
                Severity  = severity
            }));

    private void InvalidateCommands()
        => Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
}
