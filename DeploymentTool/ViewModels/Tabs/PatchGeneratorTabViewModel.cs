using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DeploymentTool.Helpers;
using DeploymentTool.Models.Patch;
using DeploymentTool.Models.Sync;
using DeploymentTool.Services.Patching;

namespace DeploymentTool.ViewModels.Tabs;

public class PatchGeneratorTabViewModel : BaseViewModel
{
    private readonly IPatchGenerationService _patchSvc;
    private readonly Action<string>          _log;
    private CancellationTokenSource?         _cts;

    private string _patchName          = $"Patch_{DateTime.Now:yyyy_MM_dd}";
    private string _patchVersion       = string.Empty;
    private string _patchOutputRoot    = string.Empty;
    private string _sourcePath         = string.Empty;
    private string _mainCodePath       = string.Empty;
    private string _miniCodePath       = string.Empty;
    private string _gitBranch          = string.Empty;
    private string _gitCommitSha       = string.Empty;
    private string _gitCommitMessage   = string.Empty;
    private string _buildConfiguration = "Release";
    private bool   _includeManifest    = true;
    private bool   _isWorking;
    private string _statusText         = "Ready";
    private double _progress;
    private string _lastPatchPath      = string.Empty;

    public ObservableCollection<ChangedFileModel> Files { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public string PatchName
    {
        get => _patchName;
        set { if (SetField(ref _patchName, value)) InvalidateCommands(); }
    }

    public string PatchVersion
    {
        get => _patchVersion;
        set => SetField(ref _patchVersion, value);
    }

    public string PatchOutputRoot
    {
        get => _patchOutputRoot;
        set { if (SetField(ref _patchOutputRoot, value)) InvalidateCommands(); }
    }

    public string SourcePath
    {
        get => _sourcePath;
        set { if (SetField(ref _sourcePath, value)) InvalidateCommands(); }
    }

    public string MainCodePath
    {
        get => _mainCodePath;
        set => SetField(ref _mainCodePath, value);
    }

    public string MiniCodePath
    {
        get => _miniCodePath;
        set => SetField(ref _miniCodePath, value);
    }

    public string GitBranch
    {
        get => _gitBranch;
        set => SetField(ref _gitBranch, value);
    }

    public string GitCommitSha
    {
        get => _gitCommitSha;
        set => SetField(ref _gitCommitSha, value);
    }

    public string GitCommitMessage
    {
        get => _gitCommitMessage;
        set => SetField(ref _gitCommitMessage, value);
    }

    public string BuildConfiguration
    {
        get => _buildConfiguration;
        set => SetField(ref _buildConfiguration, value);
    }

    public bool IncludeManifest
    {
        get => _includeManifest;
        set => SetField(ref _includeManifest, value);
    }

    public bool IsWorking
    {
        get => _isWorking;
        private set { if (SetField(ref _isWorking, value)) InvalidateCommands(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public double Progress
    {
        get => _progress;
        private set => SetField(ref _progress, value);
    }

    public string LastPatchPath
    {
        get => _lastPatchPath;
        private set { if (SetField(ref _lastPatchPath, value)) InvalidateCommands(); }
    }

    public int SelectedCount => Files.Count(f => f.IsSelected);
    public int TotalFiles    => Files.Count;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand BrowseSourceCommand      { get; }
    public ICommand BrowsePatchOutputCommand { get; }
    public ICommand AutoNameCommand          { get; }
    public ICommand CreatePatchCommand       { get; }
    public ICommand SelectAllCommand         { get; }
    public ICommand SelectNoneCommand        { get; }
    public ICommand OpenFolderCommand        { get; }
    public ICommand CancelCommand            { get; }

    public PatchGeneratorTabViewModel(IPatchGenerationService patchService, Action<string> log)
    {
        _patchSvc = patchService;
        _log      = log;

        BrowseSourceCommand = new RelayCommand(
            () => BrowseFolder("Select Source Folder (publish output or mini code)", v => SourcePath = v));

        BrowsePatchOutputCommand = new RelayCommand(
            () => BrowseFolder("Select Patch Output Root", v => PatchOutputRoot = v));

        AutoNameCommand = new RelayCommand(
            () => PatchName = $"Patch_{DateTime.Now:yyyy_MM_dd_HHmm}");

        CreatePatchCommand = new RelayCommand(
            async () => { try { await RunCreatePatchAsync(); } catch (Exception ex) { _log($"FATAL: {ex.Message}"); } },
            () => !IsWorking
               && !string.IsNullOrWhiteSpace(_patchName)
               && !string.IsNullOrWhiteSpace(_patchOutputRoot)
               && Files.Any(f => f.IsSelected));

        SelectAllCommand = new RelayCommand(() =>
        {
            foreach (var f in Files) f.IsSelected = true;
            NotifyCounts();
            InvalidateCommands();
        });

        SelectNoneCommand = new RelayCommand(() =>
        {
            foreach (var f in Files) f.IsSelected = false;
            NotifyCounts();
            InvalidateCommands();
        });

        OpenFolderCommand = new RelayCommand(
            () =>
            {
                if (Directory.Exists(_lastPatchPath))
                    System.Diagnostics.Process.Start("explorer.exe", _lastPatchPath);
            },
            () => Directory.Exists(_lastPatchPath));

        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsWorking);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void LoadFilesFromSync(
        IEnumerable<ChangedFileModel> syncedFiles,
        string sourcePath,
        string mainPath = "",
        string miniPath = "")
    {
        Files.Clear();
        foreach (var f in syncedFiles) Files.Add(f);

        if (!string.IsNullOrEmpty(sourcePath))   SourcePath   = sourcePath;
        if (!string.IsNullOrEmpty(mainPath))      MainCodePath = mainPath;
        if (!string.IsNullOrEmpty(miniPath))      MiniCodePath = miniPath;

        NotifyCounts();
        InvalidateCommands();
    }

    public void ApplyGitInfo(string branch, string sha, string message)
    {
        GitBranch        = branch;
        GitCommitSha     = sha;
        GitCommitMessage = message;
    }

    // ── Operations ────────────────────────────────────────────────────────────

    private async Task RunCreatePatchAsync()
    {
        _cts       = new CancellationTokenSource();
        IsWorking  = true;
        Progress   = 0;
        StatusText = "Creating patch…";

        var request = new PatchGenerationRequest
        {
            PatchName          = _patchName,
            Version            = _patchVersion,
            SourcePath         = _sourcePath,
            PatchOutputRoot    = _patchOutputRoot,
            GitBranch          = _gitBranch,
            GitCommitSha       = _gitCommitSha,
            GitCommitMessage   = _gitCommitMessage,
            BuildConfiguration = _buildConfiguration,
            MainCodePath       = _mainCodePath,
            MiniCodePath       = _miniCodePath,
            IncludeManifest    = _includeManifest,
            Files              = [.. Files]
        };

        try
        {
            _log($"Creating patch: {_patchName}  ({Files.Count(f => f.IsSelected)} file(s))");

            var progress = new Progress<(int Done, int Total, string File)>(p =>
                UI(() =>
                {
                    Progress   = p.Done * 100.0 / Math.Max(p.Total, 1);
                    StatusText = $"Patching {p.Done}/{p.Total}: {Path.GetFileName(p.File)}";
                }));

            var path = await _patchSvc.GeneratePatchAsync(request, progress, _log, _cts.Token);

            UI(() =>
            {
                Progress       = 100;
                LastPatchPath  = path;
                StatusText     = $"Patch ready: {_patchName}";
                _log($"Patch created: {path}");
                InvalidateCommands();
                MessageBox.Show($"Patch created successfully:\n{path}",
                    "Patch Created", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
        catch (OperationCanceledException) { UI(() => StatusText = "Cancelled"); }
        catch (Exception ex)
        {
            UI(() =>
            {
                StatusText = $"Error: {ex.Message}";
                MessageBox.Show($"Patch creation failed:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
            _log($"ERROR: {ex.Message}");
        }
        finally { UI(() => { IsWorking = false; _cts?.Dispose(); _cts = null; }); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(TotalFiles));
    }

    private static void BrowseFolder(string title, Action<string> setter)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = title };
        if (dlg.ShowDialog() == true) setter(dlg.FolderName);
    }

    private static void UI(Action a) => Application.Current.Dispatcher.Invoke(a);

    private void InvalidateCommands()
        => Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
}
