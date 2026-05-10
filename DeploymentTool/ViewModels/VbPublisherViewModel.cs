using System.Windows.Input;
using DeploymentTool.Helpers;
using DeploymentTool.Models.Git;
using DeploymentTool.Services.Build;
using DeploymentTool.Services.Git;
using DeploymentTool.Services.Sync;
using DeploymentTool.ViewModels.Tabs;

namespace DeploymentTool.ViewModels;

/// <summary>
/// Container view model for the advanced VB Publisher workflow.
/// Owns three sub-tab VMs (Repositories, Code Sync, Build Pipeline)
/// and exposes workflow-level cross-tab commands.
/// The Patch step reuses the main window Patch tab (MainViewModel bindings).
/// </summary>
public class VbPublisherViewModel : BaseViewModel
{
    // ── Sub-tab view models ───────────────────────────────────────────────────

    public RepositoriesTabViewModel  Repositories  { get; }
    public CodeSyncTabViewModel      CodeSync      { get; }
    public BuildPipelineTabViewModel BuildPipeline { get; }

    // ── Navigation ────────────────────────────────────────────────────────────

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetField(ref _selectedTabIndex, value);
    }

    // ── Cross-tab workflow commands ───────────────────────────────────────────

    /// <summary>Copies mini code path from Code Sync into Build Pipeline and navigates there.</summary>
    public ICommand PushMiniToBuildCommand { get; }

    /// <summary>Sends publish output path to the main Patch tab (single-folder mode) and navigates to Patch tab.</summary>
    public ICommand PushBuildToPatchCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public VbPublisherViewModel(Action<string> log,
        IEnumerable<GitRepositoryConfig>? gitRepos = null,
        Func<IEnumerable<GitRepositoryConfig>, Task>? onSaveRepos = null,
        Action<string>? onPushBuildToPatch = null)
    {
        var gitService   = new GitService();
        var buildService = new BuildService();
        var syncService  = new CodeSyncService();

        Repositories  = new RepositoriesTabViewModel(gitService, log, onSaveRepos);
        CodeSync      = new CodeSyncTabViewModel(syncService, log);
        BuildPipeline = new BuildPipelineTabViewModel(buildService, log);

        if (gitRepos != null)
            Repositories.LoadRepositories(gitRepos);

        // Repo selection → auto-fill Code Sync main path
        Repositories.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Repositories.SelectedRepository)
                && Repositories.SelectedRepository is { } repo
                && !string.IsNullOrWhiteSpace(repo.LocalPath))
                CodeSync.MainCodePath = repo.LocalPath;
        };

        // Code Sync mini path → Build Pipeline mini path
        CodeSync.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CodeSync.MiniCodePath))
                BuildPipeline.MiniCodePath = CodeSync.MiniCodePath;
        };

        PushMiniToBuildCommand = new RelayCommand(() =>
        {
            BuildPipeline.MiniCodePath = CodeSync.MiniCodePath;
            BuildPipeline.IsVbWebForms = true;
            SelectedTabIndex = 2; // Build Pipeline tab
        }, () => !string.IsNullOrWhiteSpace(CodeSync.MiniCodePath));

        PushBuildToPatchCommand = new RelayCommand(() =>
        {
            onPushBuildToPatch?.Invoke(BuildPipeline.PublishOutputPath);
            SelectedTabIndex = 3; // Patch Generator tab
        }, () => !string.IsNullOrWhiteSpace(BuildPipeline.PublishOutputPath));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void LoadGitRepositories(IEnumerable<GitRepositoryConfig> repos)
        => Repositories.LoadRepositories(repos);
}
