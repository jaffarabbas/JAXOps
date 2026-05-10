using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DeploymentTool.Helpers;
using DeploymentTool.Models.Build;
using DeploymentTool.Services.Build;

namespace DeploymentTool.ViewModels.Tabs;

public class BuildPipelineTabViewModel : BaseViewModel
{
    private readonly IBuildService _build;
    private readonly Action<string> _log;
    private CancellationTokenSource? _cts;

    private string _solutionPath        = string.Empty;
    private string _miniCodePath        = string.Empty;
    private string _publishOutputPath   = string.Empty;
    private string _selectedConfig      = "Release";
    private bool   _isVbWebForms        = false;
    private bool   _isWorking;
    private string _statusText          = "Ready";
    private double _progress;
    private BuildResultModel? _lastResult;

    public ObservableCollection<string> Configurations { get; } = ["Debug", "Release"];
    public ObservableCollection<string> BuildLog       { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public string SolutionPath
    {
        get => _solutionPath;
        set { if (SetField(ref _solutionPath, value)) InvalidateCommands(); }
    }

    public string MiniCodePath
    {
        get => _miniCodePath;
        set { if (SetField(ref _miniCodePath, value)) InvalidateCommands(); }
    }

    public string PublishOutputPath
    {
        get => _publishOutputPath;
        set { if (SetField(ref _publishOutputPath, value)) InvalidateCommands(); }
    }

    public string SelectedConfiguration
    {
        get => _selectedConfig;
        set => SetField(ref _selectedConfig, value);
    }

    public bool IsVbWebForms
    {
        get => _isVbWebForms;
        set => SetField(ref _isVbWebForms, value);
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

    public BuildResultModel? LastResult
    {
        get => _lastResult;
        private set
        {
            if (SetField(ref _lastResult, value))
            {
                OnPropertyChanged(nameof(LastResultSummary));
                OnPropertyChanged(nameof(LastBuildSuccess));
                OnPropertyChanged(nameof(ResultColor));
            }
        }
    }

    public string LastResultSummary => _lastResult == null
        ? string.Empty
        : _lastResult.Success
            ? $"Succeeded — {_lastResult.WarningCount} warning(s)  ·  {_lastResult.DurationDisplay}"
            : $"FAILED — {_lastResult.ErrorCount} error(s), {_lastResult.WarningCount} warning(s)";

    public bool LastBuildSuccess => _lastResult?.Success ?? false;

    public string ResultColor => _lastResult?.Success == true ? "#059669"
                               : _lastResult == null          ? "#6B7280"
                               :                               "#DC2626";

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand BrowseSolutionCommand      { get; }
    public ICommand BrowseMiniCodePathCommand  { get; }
    public ICommand BrowsePublishOutputCommand { get; }
    public ICommand BuildCommand               { get; }
    public ICommand PublishCommand             { get; }
    public ICommand ClearLogCommand            { get; }
    public ICommand CancelCommand              { get; }

    public BuildPipelineTabViewModel(IBuildService buildService, Action<string> log)
    {
        _build = buildService;
        _log   = log;

        BrowseSolutionCommand = new RelayCommand(
            () => BrowseFile("Select Solution or Project",
                "Solutions & Projects|*.sln;*.csproj;*.vbproj|All Files|*.*",
                v => SolutionPath = v));

        BrowseMiniCodePathCommand = new RelayCommand(
            () => BrowseFolder("Select Mini Code Folder (VB WebForms source)", v => MiniCodePath = v));

        BrowsePublishOutputCommand = new RelayCommand(
            () => BrowseFolder("Select Publish Output Folder", v => PublishOutputPath = v));

        BuildCommand = new RelayCommand(
            async () => { try { await RunBuildAsync(); } catch (Exception ex) { _log($"FATAL: {ex.Message}"); } },
            () => !IsWorking && (_isVbWebForms
                ? !string.IsNullOrWhiteSpace(_miniCodePath)
                : !string.IsNullOrWhiteSpace(_solutionPath)));

        PublishCommand = new RelayCommand(
            async () => { try { await RunPublishAsync(); } catch (Exception ex) { _log($"FATAL: {ex.Message}"); } },
            () => !IsWorking
               && !string.IsNullOrWhiteSpace(_publishOutputPath)
               && (_isVbWebForms
                   ? !string.IsNullOrWhiteSpace(_miniCodePath)
                   : !string.IsNullOrWhiteSpace(_solutionPath)));

        ClearLogCommand = new RelayCommand(() => BuildLog.Clear());

        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsWorking);
    }

    // ── Operations ────────────────────────────────────────────────────────────

    private async Task RunBuildAsync()
    {
        _cts      = new CancellationTokenSource();
        IsWorking = true;
        Progress  = 0;
        StatusText = "Building…";
        BuildLog.Clear();

        try
        {
            var progress = LineProgress();
            BuildResultModel result;

            if (_isVbWebForms)
                result = await _build.BuildVbWebFormsAsync(_miniCodePath, progress, _cts.Token);
            else
                result = await _build.BuildAsync(_solutionPath, _selectedConfig, progress, _cts.Token);

            UI(() =>
            {
                LastResult = result;
                Progress   = 100;
                StatusText = result.Success ? "Build succeeded" : "Build FAILED";
                _log(LastResultSummary);
                InvalidateCommands();
            });
        }
        catch (OperationCanceledException) { UI(() => StatusText = "Cancelled"); }
        catch (Exception ex)               { UI(() => StatusText = $"Error: {ex.Message}"); _log($"ERROR: {ex.Message}"); }
        finally                            { UI(() => { IsWorking = false; _cts?.Dispose(); _cts = null; }); }
    }

    private async Task RunPublishAsync()
    {
        _cts      = new CancellationTokenSource();
        IsWorking = true;
        Progress  = 0;
        StatusText = "Publishing…";
        BuildLog.Clear();

        try
        {
            var progress = LineProgress();
            BuildResultModel result;

            if (_isVbWebForms)
                result = await _build.PublishVbWebFormsAsync(_miniCodePath, _publishOutputPath, progress, _cts.Token);
            else
                result = await _build.PublishAsync(_solutionPath, _publishOutputPath, _selectedConfig, progress, _cts.Token);

            UI(() =>
            {
                LastResult = result;
                Progress   = 100;
                StatusText = result.Success ? "Publish succeeded" : "Publish FAILED";
                _log(LastResultSummary);
                InvalidateCommands();
            });
        }
        catch (OperationCanceledException) { UI(() => StatusText = "Cancelled"); }
        catch (Exception ex)               { UI(() => StatusText = $"Error: {ex.Message}"); _log($"ERROR: {ex.Message}"); }
        finally                            { UI(() => { IsWorking = false; _cts?.Dispose(); _cts = null; }); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IProgress<string> LineProgress() => new Progress<string>(line =>
        UI(() => { BuildLog.Add(line); _log(line); }));

    private static void BrowseFile(string title, string filter, Action<string> setter)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = title, Filter = filter };
        if (dlg.ShowDialog() == true) setter(dlg.FileName);
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
