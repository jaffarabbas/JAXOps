using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DeploymentTool.Helpers;
using DeploymentTool.Models.Git;
using DeploymentTool.Services.Git;

namespace DeploymentTool.ViewModels.Tabs;

public class RepositoriesTabViewModel : BaseViewModel
{
    private readonly IGitService _git;
    private readonly Action<string> _log;
    private readonly Func<IEnumerable<GitRepositoryConfig>, Task>? _onSave;
    private CancellationTokenSource? _cts;

    private GitRepositoryConfig? _selectedRepo;
    private GitBranchModel? _selectedBranch;
    private GitStatusModel? _currentStatus;
    private bool _isWorking;
    private bool _isRepoListExpanded = true;
    private string _statusText = "Select a repository";
    private string _gitOutput  = string.Empty;
    private string _customCommand = string.Empty;

    public ObservableCollection<GitRepositoryConfig> Repositories { get; } = [];
    public ObservableCollection<GitBranchModel>      Branches     { get; } = [];
    public ObservableCollection<string>              CommitLog    { get; } = [];

    // ── Selected repository ───────────────────────────────────────────────────

    public GitRepositoryConfig? SelectedRepository
    {
        get => _selectedRepo;
        set
        {
            if (!SetField(ref _selectedRepo, value)) return;
            Branches.Clear();
            CommitLog.Clear();
            CurrentStatus = null;
            GitOutput     = string.Empty;
            InvalidateCommands();
            if (value != null)
                _ = RefreshAsync();
        }
    }

    public GitBranchModel? SelectedBranch
    {
        get => _selectedBranch;
        set { if (SetField(ref _selectedBranch, value)) InvalidateCommands(); }
    }

    // ── Status ───────────────────────────────────────────────────────────────

    public GitStatusModel? CurrentStatus
    {
        get => _currentStatus;
        private set
        {
            if (SetField(ref _currentStatus, value))
            {
                OnPropertyChanged(nameof(StatusSummary));
                OnPropertyChanged(nameof(StatusChipColor));
                OnPropertyChanged(nameof(IsClean));
            }
        }
    }

    public string StatusSummary => _currentStatus == null
        ? string.Empty
        : _currentStatus.IsClean
            ? $"Clean  ·  {_currentStatus.CurrentBranch}  ·  {_currentStatus.LastCommitShort}"
            : $"Dirty  ·  {_currentStatus.ModifiedCount} modified, {_currentStatus.UntrackedCount} untracked  ·  {_currentStatus.CurrentBranch}";

    public string StatusChipColor => _currentStatus?.IsClean == true ? "#059669" : "#D97706";

    public bool IsClean => _currentStatus?.IsClean ?? true;

    public bool IsRepoListExpanded
    {
        get => _isRepoListExpanded;
        set => SetField(ref _isRepoListExpanded, value);
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

    public string GitOutput
    {
        get => _gitOutput;
        private set => SetField(ref _gitOutput, value);
    }

    public string CustomCommand
    {
        get => _customCommand;
        set { if (SetField(ref _customCommand, value)) InvalidateCommands(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand ToggleRepoListCommand { get; }
    public ICommand AddRepoCommand        { get; }
    public ICommand RemoveRepoCommand     { get; }
    public ICommand BrowseRepoPathCommand { get; }
    public ICommand SaveReposCommand      { get; }
    public ICommand RefreshCommand        { get; }
    public ICommand PullCommand           { get; }
    public ICommand FetchCommand          { get; }
    public ICommand CheckoutCommand       { get; }
    public ICommand RunCustomCommand      { get; }
    public ICommand CancelCommand         { get; }

    public RepositoriesTabViewModel(IGitService git, Action<string> log,
        Func<IEnumerable<GitRepositoryConfig>, Task>? onSave = null)
    {
        _git    = git;
        _log    = log;
        _onSave = onSave;

        ToggleRepoListCommand = new RelayCommand(
            () => IsRepoListExpanded = !IsRepoListExpanded);

        AddRepoCommand = new RelayCommand(() =>
        {
            var repo = new GitRepositoryConfig { Name = $"Repo {Repositories.Count + 1}", DefaultBranch = "main" };
            Repositories.Add(repo);
            SelectedRepository = repo;
            InvalidateCommands();
        });

        RemoveRepoCommand = new RelayCommand(() =>
        {
            if (_selectedRepo == null) return;
            var idx = Repositories.IndexOf(_selectedRepo);
            Repositories.Remove(_selectedRepo);
            SelectedRepository = Repositories.Count > 0
                ? Repositories[Math.Max(0, idx - 1)]
                : null;
            InvalidateCommands();
        }, () => _selectedRepo != null);

        BrowseRepoPathCommand = new RelayCommand(() =>
        {
            if (_selectedRepo == null) return;
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select Repository Root Folder" };
            if (dlg.ShowDialog() == true)
                _selectedRepo.LocalPath = dlg.FolderName;
        }, () => _selectedRepo != null);

        SaveReposCommand = new RelayCommand(
            async () =>
            {
                if (_onSave != null)
                {
                    try   { await _onSave(Repositories); }
                    catch (Exception ex) { _log($"Save repos failed: {ex.Message}"); }
                }
            });

        RefreshCommand = new RelayCommand(
            async () => { try { await RefreshAsync(); } catch (Exception ex) { Err(ex); } },
            () => !IsWorking && _selectedRepo != null);

        PullCommand = new RelayCommand(
            async () => { try { await PullAsync(); } catch (Exception ex) { Err(ex); } },
            () => !IsWorking && _selectedRepo != null);

        FetchCommand = new RelayCommand(
            async () => { try { await FetchAsync(); } catch (Exception ex) { Err(ex); } },
            () => !IsWorking && _selectedRepo != null);

        CheckoutCommand = new RelayCommand(
            async () => { try { await CheckoutAsync(); } catch (Exception ex) { Err(ex); } },
            () => !IsWorking && _selectedRepo != null && _selectedBranch != null && !_selectedBranch.IsCurrentBranch);

        RunCustomCommand = new RelayCommand(
            async () => { try { await RunCustomAsync(); } catch (Exception ex) { Err(ex); } },
            () => !IsWorking && _selectedRepo != null && !string.IsNullOrWhiteSpace(_customCommand));

        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsWorking);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void LoadRepositories(IEnumerable<GitRepositoryConfig> repos)
    {
        Repositories.Clear();
        foreach (var r in repos) Repositories.Add(r);
    }

    // ── Private operations ────────────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        if (_selectedRepo == null) return;
        using var scope = BeginWork("Refreshing…");

        try
        {
            var path    = _selectedRepo.LocalPath;
            var isValid = await _git.IsValidRepositoryAsync(path, scope.Token);

            if (!isValid)
            {
                StatusText = "Not a valid git repository";
                _log($"Not a valid git repo: {path}");
                return;
            }

            var status   = await _git.GetStatusAsync(path, scope.Token);
            var branches = await _git.GetBranchesAsync(path, scope.Token);
            var commits  = await _git.GetCommitLogAsync(path, 20, scope.Token);

            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentStatus = status;
                StatusText    = StatusSummary;

                Branches.Clear();
                foreach (var b in branches) Branches.Add(b);
                SelectedBranch = Branches.FirstOrDefault(b => b.IsCurrentBranch);

                CommitLog.Clear();
                foreach (var c in commits) CommitLog.Add(c);
            });

            _log($"Refreshed [{_selectedRepo.Name}]: {status.CurrentBranch}  ({(status.IsClean ? "clean" : "dirty")})");
        }
        catch (OperationCanceledException)
        {
            UI(() => StatusText = "Cancelled");
        }
        catch (Exception ex)
        {
            UI(() => StatusText = $"Error: {ex.Message}");
            _log($"ERROR: {ex.Message}");
        }
    }

    private async Task PullAsync()
    {
        if (_selectedRepo == null) return;
        using var scope = BeginWork("Pulling…");
        GitOutput = string.Empty;

        try
        {
            var progress = LineProgress();
            var success  = await _git.PullAsync(_selectedRepo.LocalPath, progress, scope.Token);

            UI(() => StatusText = success ? "Pull complete" : "Pull failed");
            _log(success ? $"Pull succeeded [{_selectedRepo.Name}]." : "Pull failed — see log.");

            if (success) await RefreshAsync();
        }
        catch (OperationCanceledException) { UI(() => StatusText = "Cancelled"); }
        catch (Exception ex)               { UI(() => StatusText = $"Error: {ex.Message}"); _log($"ERROR: {ex.Message}"); }
    }

    private async Task FetchAsync()
    {
        if (_selectedRepo == null) return;
        using var scope = BeginWork("Fetching…");
        GitOutput = string.Empty;

        try
        {
            var progress = LineProgress();
            var success  = await _git.FetchAsync(_selectedRepo.LocalPath, progress, scope.Token);

            UI(() => StatusText = success ? "Fetch complete" : "Fetch failed");
            if (success) await RefreshAsync();
        }
        catch (OperationCanceledException) { UI(() => StatusText = "Cancelled"); }
        catch (Exception ex)               { UI(() => StatusText = $"Error: {ex.Message}"); _log($"ERROR: {ex.Message}"); }
    }

    private async Task CheckoutAsync()
    {
        if (_selectedRepo == null || _selectedBranch == null) return;
        var branchName = _selectedBranch.IsRemote
            ? _selectedBranch.Name.Replace("origin/", string.Empty)
            : _selectedBranch.Name;

        using var scope = BeginWork($"Checking out {branchName}…");

        try
        {
            var success = await _git.CheckoutBranchAsync(_selectedRepo.LocalPath, branchName, scope.Token);
            UI(() => StatusText = success ? $"Checked out: {branchName}" : "Checkout failed");
            _log(success ? $"Checked out: {branchName}" : $"Checkout failed: {branchName}");
            if (success) await RefreshAsync();
        }
        catch (OperationCanceledException) { UI(() => StatusText = "Cancelled"); }
        catch (Exception ex)               { UI(() => StatusText = $"Error: {ex.Message}"); _log($"ERROR: {ex.Message}"); }
    }

    private async Task RunCustomAsync()
    {
        if (_selectedRepo == null) return;
        using var scope = BeginWork($"Running: git {_customCommand}…");

        try
        {
            var output = await _git.RunRawCommandAsync(_selectedRepo.LocalPath, _customCommand, scope.Token);
            UI(() =>
            {
                GitOutput  += $"\n$ git {_customCommand}\n{output}";
                StatusText  = "Command complete";
            });
            _log(output);
        }
        catch (OperationCanceledException) { UI(() => StatusText = "Cancelled"); }
        catch (Exception ex)               { UI(() => StatusText = $"Error: {ex.Message}"); _log($"ERROR: {ex.Message}"); }
    }

    // ── Infrastructure helpers ────────────────────────────────────────────────

    private WorkScope BeginWork(string status)
    {
        _cts       = new CancellationTokenSource();
        IsWorking  = true;
        StatusText = status;
        return new WorkScope(_cts, () => UI(() => { IsWorking = false; _cts?.Dispose(); _cts = null; }));
    }

    private IProgress<string> LineProgress() => new Progress<string>(line =>
        UI(() => { GitOutput += line + "\n"; _log(line); }));

    private static void UI(Action a) => Application.Current.Dispatcher.Invoke(a);

    private void Err(Exception ex) { _log($"FATAL: {ex.Message}"); }

    private void InvalidateCommands()
        => Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);

    // ── WorkScope: ensures IsWorking resets even on exception ────────────────
    private sealed class WorkScope(CancellationTokenSource cts, Action cleanup) : IDisposable
    {
        public CancellationToken Token => cts.Token;
        public void Dispose() => cleanup();
    }
}
