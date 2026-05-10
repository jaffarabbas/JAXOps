using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DeploymentTool.Helpers;
using DeploymentTool.Models;
using DeploymentTool.Services;

namespace DeploymentTool.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ConfigService _config;
    private readonly ProjectScannerService _scanner;
    private readonly PublishService _publisher;
    private readonly FileCompareService _comparer;
    private readonly FileCopyService _copier;
    private readonly AppSettingsService _appSettingsService;

    private Codebase? _selectedCodebase;
    private string _codebaseSearchText = string.Empty;
    private string _publishOutputFolder = string.Empty;
    private string _logSearchText = string.Empty;
    private string _editorSearchText = string.Empty;
    private bool _isBusy;
    private bool _isComparing;
    private string _newCompareFolder = string.Empty;
    private string _oldCompareFolder = string.Empty;
    private string _singleCompareFolder = string.Empty;
    private DateTime _sinceDate = DateTime.Today.AddDays(-7);
    private CompareModeOption _selectedCompareModeOption = null!;
    private string _searchText = string.Empty;
    private string _patchName = string.Empty;
    private FilterOption _selectedFilterOption;
    private ConfigFileItem? _selectedConfigFile;
    private string _editorContent = string.Empty;
    private bool _isEditorDirty;
    private bool _loadingFile;
    private int _validationErrorLineNumber;
    private readonly HashSet<int> _validationErrorLines = [];
    private string _globalKey = string.Empty;
    private string _globalValue = string.Empty;
    private readonly Dictionary<string, string> _fileContents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ICollectionView _logEntriesView;
    private readonly ICollectionView _codebasesView;
    private readonly ICollectionView _settingsCodebasesView;
    private Codebase? _selectedSettingsCodebase;
    private string _settingsCodebaseSearchText = string.Empty;
    private string _settingsPublishOutputRoot = string.Empty;
    private string _settingsPatchOutputRoot = string.Empty;
    private bool _isDarkMode;
    private bool _isCodebasesExpanded = true;

    // ── Transfer tab sub-viewmodel ─────────────────────────────────────────────
    public TransferViewModel Transfer { get; }

    // ── Deploy tab sub-viewmodel ────────────────────────────────────────────────
    public DeployViewModel Deploy { get; }

    // ── VB Publisher tab sub-viewmodel ─────────────────────────────────────────
    public VbPublisherViewModel VbPublisher { get; }

    // ── Collections ────────────────────────────────────────────────────────────
    public ObservableCollection<Codebase>      Codebases   { get; } = [];
    public ObservableCollection<ProjectItem>   Projects    { get; } = [];
    public ObservableCollection<FileChange>    FileChanges { get; } = [];
    public ObservableCollection<FileTreeNode>  FileTree    { get; } = [];
    public ObservableCollection<ConfigFileItem> ConfigFiles { get; } = [];
    public ObservableCollection<Codebase> SettingsCodebases { get; } = [];
    public ObservableCollection<LogEntry> LogEntries { get; } = [];
    public ObservableCollection<EditorLineInfo> EditorLines { get; } = [];

    // ── Properties ─────────────────────────────────────────────────────────────
    public Codebase? SelectedCodebase
    {
        get => _selectedCodebase;
        set
        {
            if (SetField(ref _selectedCodebase, value) && value != null)
                _ = LoadProjectsAsync();
        }
    }

    public string CodebaseSearchText
    {
        get => _codebaseSearchText;
        set
        {
            if (SetField(ref _codebaseSearchText, value))
                _codebasesView.Refresh();
        }
    }

    public ICollectionView FilteredCodebases => _codebasesView;

    public string PublishOutputFolder
    {
        get => _publishOutputFolder;
        set => SetField(ref _publishOutputFolder, value);
    }

    public string LogSearchText
    {
        get => _logSearchText;
        set
        {
            if (SetField(ref _logSearchText, value))
                _logEntriesView.Refresh();
        }
    }

    public ICollectionView FilteredLogEntries => _logEntriesView;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    public bool IsComparing
    {
        get => _isComparing;
        private set => SetField(ref _isComparing, value);
    }

    public string NewCompareFolder
    {
        get => _newCompareFolder;
        set
        {
            if (SetField(ref _newCompareFolder, value))
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    public string OldCompareFolder
    {
        get => _oldCompareFolder;
        set
        {
            if (SetField(ref _oldCompareFolder, value))
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    // ── Compare mode ───────────────────────────────────────────────────────────
    public IReadOnlyList<CompareModeOption> CompareModeOptions { get; }

    public CompareModeOption SelectedCompareModeOption
    {
        get => _selectedCompareModeOption;
        set
        {
            if (SetField(ref _selectedCompareModeOption, value))
            {
                OnPropertyChanged(nameof(IsTwoFolderMode));
                OnPropertyChanged(nameof(IsSingleFolderMode));
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
            }
        }
    }

    public bool IsTwoFolderMode  => _selectedCompareModeOption?.Mode == CompareMode.TwoFolder;
    public bool IsSingleFolderMode => _selectedCompareModeOption?.Mode == CompareMode.SingleFolder;

    public string SingleCompareFolder
    {
        get => _singleCompareFolder;
        set
        {
            if (SetField(ref _singleCompareFolder, value))
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    public DateTime SinceDate
    {
        get => _sinceDate;
        set => SetField(ref _sinceDate, value);
    }

    // ── Filter & Search ────────────────────────────────────────────────────────
    public IReadOnlyList<FilterOption> FilterOptions { get; }

    public FilterOption SelectedFilterOption
    {
        get => _selectedFilterOption;
        set
        {
            if (SetField(ref _selectedFilterOption, value) && FileChanges.Count > 0)
                BuildFileTree();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value) && FileChanges.Count > 0)
                BuildFileTree();
        }
    }

    // ── Patch config ───────────────────────────────────────────────────────────
    public string PatchOutputRoot => _config.Settings.PatchOutputRoot;

    public string PatchName
    {
        get => _patchName;
        set
        {
            if (SetField(ref _patchName, value))
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    // ── Settings tab properties ───────────────────────────────────────────────
    public Codebase? SelectedSettingsCodebase
    {
        get => _selectedSettingsCodebase;
        set
        {
            if (SetField(ref _selectedSettingsCodebase, value))
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    public string SettingsCodebaseSearchText
    {
        get => _settingsCodebaseSearchText;
        set
        {
            if (SetField(ref _settingsCodebaseSearchText, value))
                _settingsCodebasesView.Refresh();
        }
    }

    public ICollectionView FilteredSettingsCodebases => _settingsCodebasesView;

    public string SettingsPublishOutputRoot
    {
        get => _settingsPublishOutputRoot;
        set => SetField(ref _settingsPublishOutputRoot, value);
    }

    public string SettingsPatchOutputRoot
    {
        get => _settingsPatchOutputRoot;
        set => SetField(ref _settingsPatchOutputRoot, value);
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetField(ref _isDarkMode, value) && Application.Current is App app)
                app.ApplyTheme(value);
        }
    }

    public bool IsCodebasesExpanded
    {
        get => _isCodebasesExpanded;
        set => SetField(ref _isCodebasesExpanded, value);
    }

    // ── AppSettings tab properties ─────────────────────────────────────────────
    public ConfigFileItem? SelectedConfigFile
    {
        get => _selectedConfigFile;
        set
        {
            if (_selectedConfigFile == value) return;

            // Persist any unsaved editor edits to the in-memory cache before switching
            if (_selectedConfigFile != null && _isEditorDirty)
            {
                _fileContents[_selectedConfigFile.FilePath] = _editorContent;
                _selectedConfigFile.IsModified = true;
            }

            SetField(ref _selectedConfigFile, value);
            OnPropertyChanged(nameof(EditorTitle));
            Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
            if (value != null)
                _ = LoadFileContentAsync(value);
        }
    }

    public string GlobalKey
    {
        get => _globalKey;
        set
        {
            if (SetField(ref _globalKey, value))
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    public string GlobalValue
    {
        get => _globalValue;
        set => SetField(ref _globalValue, value);
    }

    public string EditorContent
    {
        get => _editorContent;
        set
        {
            if (SetField(ref _editorContent, value))
            {
                UpdateEditorLineNumbers();

                if (!_loadingFile && !_isEditorDirty)
                {
                    _isEditorDirty = true;
                    OnPropertyChanged(nameof(IsEditorDirty));
                    Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
                }

                if (!_loadingFile && _validationErrorLineNumber != 0)
                {
                    SetValidationErrors([]);
                }
            }
        }
    }

    public int ValidationErrorCount => _validationErrorLines.Count;

    public string ValidationErrorLinesSummary => _validationErrorLines.Count == 0
        ? string.Empty
        : $"Error lines: {string.Join(", ", _validationErrorLines.OrderBy(x => x))}";

    public int ValidationErrorLineNumber
    {
        get => _validationErrorLineNumber;
        private set
        {
            if (SetField(ref _validationErrorLineNumber, value))
                UpdateEditorLineNumbers();
        }
    }

    public string EditorSearchText
    {
        get => _editorSearchText;
        set => SetField(ref _editorSearchText, value);
    }

    public bool IsEditorDirty
    {
        get => _isEditorDirty;
        private set
        {
            if (SetField(ref _isEditorDirty, value))
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
        }
    }

    public string EditorTitle => _selectedConfigFile?.FileName ?? "(no file selected)";

    // ── Counts ─────────────────────────────────────────────────────────────────
    public int ModifiedCount => FileChanges.Count(f => f.Status == ChangeStatus.Modified);
    public int NewCount      => FileChanges.Count(f => f.Status == ChangeStatus.New);
    public int TotalCount    => FileChanges.Count;

    // ── Commands ────────────────────────────────────────────────────────────────
    public ICommand BrowseOutputFolderCommand      { get; }
    public ICommand BrowseNewCompareFolderCommand  { get; }
    public ICommand BrowseOldCompareFolderCommand  { get; }
    public ICommand BrowseSingleFolderCommand      { get; }
    public ICommand SelectAllProjectsCommand      { get; }
    public ICommand ClearProjectsCommand          { get; }
    public ICommand PublishCommand                { get; }
    public ICommand CompareCommand                { get; }
    public ICommand SelectAllChangesCommand       { get; }
    public ICommand ClearChangesCommand           { get; }
    public ICommand CreatePatchCommand            { get; }

    // AppSettings tab
    public ICommand LoadConfigFilesCommand   { get; }
    public ICommand SelectAllFilesCommand    { get; }
    public ICommand ClearFilesCommand        { get; }
    public ICommand ApplyGlobalChangeCommand { get; }
    public ICommand SaveFileCommand          { get; }
    public ICommand SaveAllModifiedCommand   { get; }
    public ICommand AddSettingsCodebaseCommand { get; }
    public ICommand RemoveSettingsCodebaseCommand { get; }
    public ICommand RemoveAllSettingsCodebasesCommand { get; }
    public ICommand BrowseSettingsCodebasePathCommand { get; }
    public ICommand AddMultipleCodebasesCommand { get; }
    public ICommand BrowseSettingsPublishOutputCommand { get; }
    public ICommand BrowseSettingsPatchOutputCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand ValidateSelectedJsonCommand   { get; }
    public ICommand ToggleCodebasesCommand        { get; }

    // ── Constructor ─────────────────────────────────────────────────────────────
    public MainViewModel()
    {
        _config              = new ConfigService();
        _scanner             = new ProjectScannerService();
        _publisher           = new PublishService();
        _comparer            = new FileCompareService();
        _copier              = new FileCopyService();
        _appSettingsService  = new AppSettingsService();

        FilterOptions = [
            new FilterOption(FileFilterMode.All,          "All Files"),
            new FilterOption(FileFilterMode.ModifiedOnly, "Modified Only"),
            new FilterOption(FileFilterMode.NewOnly,      "New Files Only"),
        ];
        _selectedFilterOption = FilterOptions[0];

        CompareModeOptions = [
            new CompareModeOption(CompareMode.TwoFolder,    "Two Folders"),
            new CompareModeOption(CompareMode.SingleFolder, "Single Folder"),
        ];
        _selectedCompareModeOption = CompareModeOptions[0];
        _patchName = $"Patch_{DateTime.Now:yyyyMMdd_HHmm}";

        Transfer    = new TransferViewModel();
        VbPublisher = new VbPublisherViewModel(
            AppendLog,
            _config.Settings.GitRepositories,
            SaveGitRepositoriesAsync,
            publishPath =>
            {
                SingleCompareFolder = publishPath;
                SelectedCompareModeOption = CompareModeOptions.First(
                    o => o.Mode == CompareMode.SingleFolder);
            });

        Deploy = new DeployViewModel(
            () => Projects.Where(p => p.IsSelected).ToList(),
            () => PublishOutputFolder,
            AppendLog);
        _logEntriesView      = CollectionViewSource.GetDefaultView(LogEntries);
        _logEntriesView.Filter = item =>
            item is LogEntry entry &&
            (string.IsNullOrWhiteSpace(_logSearchText) ||
             entry.DisplayText.Contains(_logSearchText, StringComparison.OrdinalIgnoreCase));

        _codebasesView = CollectionViewSource.GetDefaultView(Codebases);
        _codebasesView.Filter = item =>
            item is Codebase cb &&
            (string.IsNullOrWhiteSpace(_codebaseSearchText)
             || cb.Name.Contains(_codebaseSearchText, StringComparison.OrdinalIgnoreCase)
             || cb.Path.Contains(_codebaseSearchText, StringComparison.OrdinalIgnoreCase));

        _settingsCodebasesView = CollectionViewSource.GetDefaultView(SettingsCodebases);
        _settingsCodebasesView.Filter = item =>
            item is Codebase cb &&
            (string.IsNullOrWhiteSpace(_settingsCodebaseSearchText)
             || cb.Name.Contains(_settingsCodebaseSearchText, StringComparison.OrdinalIgnoreCase)
             || cb.Path.Contains(_settingsCodebaseSearchText, StringComparison.OrdinalIgnoreCase));

        _publishOutputFolder = _config.Settings.PublishOutputRoot;
        _settingsPublishOutputRoot = _config.Settings.PublishOutputRoot;
        _settingsPatchOutputRoot = _config.Settings.PatchOutputRoot;
        _isDarkMode = _config.Settings.IsDarkMode;

        if (Application.Current is App app)
            app.ApplyTheme(_isDarkMode);

        FileChanges.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ModifiedCount));
            OnPropertyChanged(nameof(NewCount));
            OnPropertyChanged(nameof(TotalCount));
        };

        ConfigFiles.CollectionChanged += (_, _) =>
            Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);

        BrowseOutputFolderCommand     = new RelayCommand(BrowseOutputFolder);
        BrowseNewCompareFolderCommand = new RelayCommand(() => BrowseCompareFolder(isNew: true));
        BrowseOldCompareFolderCommand = new RelayCommand(() => BrowseCompareFolder(isNew: false));
        BrowseSingleFolderCommand     = new RelayCommand(BrowseSingleFolder);
        SelectAllProjectsCommand      = new RelayCommand(() => SetAllProjects(true));
        ClearProjectsCommand          = new RelayCommand(() => SetAllProjects(false));
        SelectAllChangesCommand       = new RelayCommand(() => SetAllChanges(true));
        ClearChangesCommand           = new RelayCommand(() => SetAllChanges(false));

        PublishCommand = new RelayCommand(
            async () =>
            {
                try { await PublishAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}"); IsBusy = false; }
            },
            () => !IsBusy);

        CompareCommand = new RelayCommand(
            async () =>
            {
                try { await CompareAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}"); IsBusy = false; }
            },
            () => !IsBusy && (
                (IsTwoFolderMode  && !string.IsNullOrWhiteSpace(_newCompareFolder) && !string.IsNullOrWhiteSpace(_oldCompareFolder)) ||
                (IsSingleFolderMode && !string.IsNullOrWhiteSpace(_singleCompareFolder))
            ));

        CreatePatchCommand = new RelayCommand(
            async () =>
            {
                try { await CreatePatchAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}"); IsBusy = false; }
            },
            () => !IsBusy && !string.IsNullOrWhiteSpace(_patchName) && FileChanges.Any(f => f.IsSelected));

        LoadConfigFilesCommand = new RelayCommand(
            async () =>
            {
                try { await LoadConfigFilesAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}"); IsBusy = false; }
            },
            () => !IsBusy);

        SelectAllFilesCommand = new RelayCommand(
            () => { foreach (var f in ConfigFiles) f.IsSelected = true; },
            () => ConfigFiles.Count > 0);

        ClearFilesCommand = new RelayCommand(
            () => { foreach (var f in ConfigFiles) f.IsSelected = false; },
            () => ConfigFiles.Count > 0);

        ApplyGlobalChangeCommand = new RelayCommand(
            async () =>
            {
                try { await ApplyGlobalChangeAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}"); IsBusy = false; }
            },
            () => !IsBusy && !string.IsNullOrWhiteSpace(_globalKey) && ConfigFiles.Any(f => f.IsSelected));

        SaveFileCommand = new RelayCommand(
            async () =>
            {
                try { await SaveFileAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}"); IsBusy = false; }
            },
            () => !IsBusy && _selectedConfigFile != null && (_isEditorDirty || _selectedConfigFile?.IsModified == true));

        SaveAllModifiedCommand = new RelayCommand(
            async () =>
            {
                try { await SaveAllModifiedAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}"); IsBusy = false; }
            },
            () => !IsBusy && ConfigFiles.Any(f => f.IsModified));

        AddSettingsCodebaseCommand = new RelayCommand(AddSettingsCodebase, () => !IsBusy);
        RemoveSettingsCodebaseCommand = new RelayCommand(
            RemoveSelectedSettingsCodebase,
            () => !IsBusy && SelectedSettingsCodebase != null);
        RemoveAllSettingsCodebasesCommand = new RelayCommand(
            RemoveAllSettingsCodebases,
            () => !IsBusy && SettingsCodebases.Count > 0);
        BrowseSettingsCodebasePathCommand = new RelayCommand(
            BrowseSelectedSettingsCodebasePath,
            () => !IsBusy && SelectedSettingsCodebase != null);
        AddMultipleCodebasesCommand = new RelayCommand(AddMultipleCodebases, () => !IsBusy);
        BrowseSettingsPublishOutputCommand = new RelayCommand(BrowseSettingsPublishOutput, () => !IsBusy);
        BrowseSettingsPatchOutputCommand = new RelayCommand(BrowseSettingsPatchOutput, () => !IsBusy);
        SaveSettingsCommand = new RelayCommand(
            async () =>
            {
                try { await SaveSettingsAsync(); }
                catch (Exception ex) { AppendLog($"FATAL: {ex.Message}"); IsBusy = false; }
            },
            () => !IsBusy);

        foreach (var cb in _config.Settings.Codebases)
            Codebases.Add(cb);

        foreach (var cb in _config.Settings.Codebases)
        {
            var editable = new Codebase { Name = cb.Name, Path = cb.Path };
            SettingsCodebases.Add(editable);
        }

        if (SettingsCodebases.Count > 0)
            SelectedSettingsCodebase = SettingsCodebases[0];

        SettingsCodebases.CollectionChanged += (_, _) =>
            Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);

        UpdateEditorLineNumbers();

        ValidateSelectedJsonCommand = new RelayCommand(
            ValidateSelectedConfig,
            () => !IsBusy && SelectedConfigFile != null &&
                  (string.Equals(SelectedConfigFile.FileType, "JSON", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(SelectedConfigFile.FileType, "XML", StringComparison.OrdinalIgnoreCase)));

        ToggleCodebasesCommand = new RelayCommand(
            () => IsCodebasesExpanded = !IsCodebasesExpanded);
    }

    // ── Private methods ─────────────────────────────────────────────────────────
    private async Task LoadProjectsAsync()
    {
        if (SelectedCodebase == null) return;

        IsBusy = true;
        Projects.Clear();
        AppendLog($"Scanning: {SelectedCodebase.Path}");

        try
        {
            var found = await _scanner.ScanAsync(SelectedCodebase.Path);
            foreach (var p in found)
                Projects.Add(p);
            AppendLog($"Found {found.Count} project(s).");
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR scanning: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PublishAsync()
    {
        var selected = Projects.Where(p => p.IsSelected).ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one project.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PublishOutputFolder))
        {
            MessageBox.Show("Specify a publish output folder.", "Missing Folder",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        FileChanges.Clear();
        FileTree.Clear();
        AppendLog($"Publishing {selected.Count} project(s) to: {PublishOutputFolder}");

        try
        {
            await _publisher.PublishAsync(selected, PublishOutputFolder, AppendLog);
            NewCompareFolder = PublishOutputFolder;
            AppendLog("Publish completed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CompareAsync()
    {
        if (IsSingleFolderMode)
        {
            await CompareSingleFolderAsync();
            return;
        }

        if (!Directory.Exists(NewCompareFolder))
        {
            MessageBox.Show(
                "Set the NEW folder path first (the folder you just published to).",
                "New Folder Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(OldCompareFolder))
        {
            MessageBox.Show(
                "Set the OLD folder path first (the previous version you are comparing against).",
                "Old Folder Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        IsComparing = true;
        FileChanges.Clear();
        FileTree.Clear();
        AppendLog($"Comparing:\n  OLD: {OldCompareFolder}\n  NEW: {NewCompareFolder}");

        try
        {
            var changes = await _comparer.CompareAsync(OldCompareFolder, NewCompareFolder);
            foreach (var c in changes)
                FileChanges.Add(c);

            var modCount = changes.Count(c => c.Status == ChangeStatus.Modified);
            var newCount = changes.Count(c => c.Status == ChangeStatus.New);
            AppendLog($"Compare done — {modCount} modified  |  {newCount} new  |  {changes.Count} total");

            PatchName = $"Patch_{DateTime.Now:yyyyMMdd_HHmm}";
            BuildFileTree();
        }
        finally
        {
            IsBusy = false;
            IsComparing = false;
        }
    }

    private async Task CompareSingleFolderAsync()
    {
        if (!Directory.Exists(SingleCompareFolder))
        {
            MessageBox.Show(
                "Set the folder path first.",
                "Folder Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        IsComparing = true;
        FileChanges.Clear();
        FileTree.Clear();
        AppendLog($"Scanning single folder since {SinceDate:yyyy-MM-dd}:\n  {SingleCompareFolder}");

        try
        {
            var changes = await _comparer.ScanSingleFolderAsync(SingleCompareFolder, SinceDate.Date);
            foreach (var c in changes)
                FileChanges.Add(c);

            var modCount = changes.Count(c => c.Status == ChangeStatus.Modified);
            var newCount = changes.Count(c => c.Status == ChangeStatus.New);
            AppendLog($"Scan done — {modCount} modified  |  {newCount} new  |  {changes.Count} total");

            PatchName = $"Patch_{DateTime.Now:yyyyMMdd_HHmm}";
            BuildFileTree();
        }
        finally
        {
            IsBusy = false;
            IsComparing = false;
        }
    }

    private void BuildFileTree()
    {
        FileTree.Clear();

        var changes = _selectedFilterOption.Mode switch
        {
            FileFilterMode.ModifiedOnly => FileChanges.Where(f => f.Status == ChangeStatus.Modified).ToList(),
            FileFilterMode.NewOnly      => FileChanges.Where(f => f.Status == ChangeStatus.New).ToList(),
            _                           => FileChanges.ToList()
        };

        if (!string.IsNullOrWhiteSpace(_searchText))
            changes = changes
                .Where(f => f.RelativePath.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var nodeMap = new Dictionary<string, FileTreeNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var fc in changes)
        {
            fc.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FileChange.IsSelected))
                    Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
            };

            var parts = fc.RelativePath.Replace('\\', '/').Split('/');
            FileTreeNode? parent = null;

            for (int i = 0; i < parts.Length; i++)
            {
                bool isLeaf = i == parts.Length - 1;
                string key  = string.Join("/", parts[..(i + 1)]);

                if (!nodeMap.TryGetValue(key, out var node))
                {
                    node = new FileTreeNode
                    {
                        Name       = parts[i],
                        IsFolder   = !isLeaf,
                        FileChange = isLeaf ? fc : null,
                        Parent     = parent
                    };
                    nodeMap[key] = node;

                    if (parent == null) FileTree.Add(node);
                    else                parent.Children.Add(node);
                }

                parent = node;
            }
        }

        foreach (var root in FileTree)
            SyncFolderState(root);

        Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
    }

    private static void SyncFolderState(FileTreeNode node)
    {
        if (!node.IsFolder) return;
        foreach (var child in node.Children)
            SyncFolderState(child);
        node.UpdateFromChildren();
    }

    private async Task CreatePatchAsync()
    {
        var selected = FileChanges.Where(f => f.IsSelected).ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("No files selected for the patch.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var patchFolder = Path.Combine(_config.Settings.PatchOutputRoot, PatchName.Trim());

        IsBusy = true;
        AppendLog($"Creating patch: {patchFolder}");

        try
        {
            await _copier.CreatePatchAsync(selected, patchFolder);
            AppendLog($"Patch created with {selected.Count} file(s):\n  {patchFolder}");
            MessageBox.Show($"Patch ready:\n{patchFolder}", "Patch Created",
                MessageBoxButton.OK, MessageBoxImage.Information);
            PatchName = $"Patch_{DateTime.Now:yyyyMMdd_HHmm}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BrowseOutputFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select Publish Output Folder" };
        if (dlg.ShowDialog() == true)
            PublishOutputFolder = dlg.FolderName;
    }

    private void BrowseCompareFolder(bool isNew)
    {
        var title = isNew
            ? "Select NEW Publish Folder (what you just built)"
            : "Select OLD Folder (previous version to compare against)";

        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = title };
        if (dlg.ShowDialog() != true) return;

        if (isNew) NewCompareFolder = dlg.FolderName;
        else       OldCompareFolder = dlg.FolderName;
    }

    private void BrowseSingleFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select folder to scan for changes" };
        if (dlg.ShowDialog() == true)
            SingleCompareFolder = dlg.FolderName;
    }

    private void AddSettingsCodebase()
    {
        var name = $"Codebase_{SettingsCodebases.Count + 1}";
        var item = new Codebase { Name = name, Path = string.Empty };
        SettingsCodebases.Add(item);
        SelectedSettingsCodebase = item;
    }

    private void RemoveSelectedSettingsCodebase()
    {
        if (SelectedSettingsCodebase == null) return;

        var toRemove = SelectedSettingsCodebase;
        var index = SettingsCodebases.IndexOf(toRemove);
        if (index < 0) return;

        SettingsCodebases.RemoveAt(index);

        if (SettingsCodebases.Count == 0)
            SelectedSettingsCodebase = null;
        else
            SelectedSettingsCodebase = SettingsCodebases[Math.Min(index, SettingsCodebases.Count - 1)];
    }

    private void RemoveAllSettingsCodebases()
    {
        if (SettingsCodebases.Count == 0) return;

        var result = MessageBox.Show(
            $"Remove all {SettingsCodebases.Count} codebase(s)? This cannot be undone until you save settings.",
            "Remove All Codebases",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        SettingsCodebases.Clear();
        SelectedSettingsCodebase = null;
    }

    private void BrowseSelectedSettingsCodebasePath()
    {
        if (SelectedSettingsCodebase == null) return;

        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select codebase root folder" };
        if (dlg.ShowDialog() == true)
            SelectedSettingsCodebase.Path = dlg.FolderName;
    }

    private void AddMultipleCodebases()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select parent folder — all subfolders will be added as codebases" };
        if (dlg.ShowDialog() != true) return;

        var subfolders = Directory.GetDirectories(dlg.FolderName, "*", SearchOption.TopDirectoryOnly);
        foreach (var folder in subfolders)
        {
            var name = Path.GetFileName(folder);
            if (SettingsCodebases.Any(c => string.Equals(c.Path, folder, StringComparison.OrdinalIgnoreCase)))
                continue;
            SettingsCodebases.Add(new Codebase { Name = name, Path = folder });
        }

        if (SettingsCodebases.Count > 0 && SelectedSettingsCodebase == null)
            SelectedSettingsCodebase = SettingsCodebases[0];
    }

    private void BrowseSettingsPublishOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select publish output root" };
        if (dlg.ShowDialog() == true)
            SettingsPublishOutputRoot = dlg.FolderName;
    }

    private void BrowseSettingsPatchOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select patch output root" };
        if (dlg.ShowDialog() == true)
            SettingsPatchOutputRoot = dlg.FolderName;
    }

    private async Task SaveSettingsAsync()
    {
        var cleaned = SettingsCodebases
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Path))
            .Select(c => new Codebase { Name = c.Name.Trim(), Path = c.Path.Trim() })
            .ToList();

        if (cleaned.Count == 0)
        {
            MessageBox.Show("Add at least one codebase with both Name and Path.", "Settings Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SettingsPublishOutputRoot) || string.IsNullOrWhiteSpace(SettingsPatchOutputRoot))
        {
            MessageBox.Show("Publish Output Root and Patch Output Root are required.", "Settings Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settings = new AppSettings
        {
            Codebases         = cleaned,
            PublishOutputRoot = SettingsPublishOutputRoot.Trim(),
            PatchOutputRoot   = SettingsPatchOutputRoot.Trim(),
            IsDarkMode        = IsDarkMode,
            GitRepositories   = _config.Settings.GitRepositories
        };

        IsBusy = true;
        AppendLog("Saving settings to appsettings.json...");

        try
        {
            await _config.SaveSettingsAsync(settings);

            Codebases.Clear();
            foreach (var cb in settings.Codebases)
                Codebases.Add(new Codebase { Name = cb.Name, Path = cb.Path });

            if (Codebases.Count > 0)
                SelectedCodebase = Codebases[0];

            PublishOutputFolder = settings.PublishOutputRoot;
            OnPropertyChanged(nameof(PatchOutputRoot));

            AppendLog($"Settings saved. Codebases: {settings.Codebases.Count}");
            MessageBox.Show("Settings saved to appsettings.json.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveGitRepositoriesAsync(IEnumerable<Models.Git.GitRepositoryConfig> repos)
    {
        var cleaned = repos
            .Where(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.LocalPath))
            .ToList();

        var settings = new Models.AppSettings
        {
            Codebases         = _config.Settings.Codebases,
            PublishOutputRoot = _config.Settings.PublishOutputRoot,
            PatchOutputRoot   = _config.Settings.PatchOutputRoot,
            IsDarkMode        = _config.Settings.IsDarkMode,
            GitRepositories   = cleaned
        };

        try
        {
            await _config.SaveSettingsAsync(settings);
            AppendLog($"Git repositories saved ({cleaned.Count}).");
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR saving git repositories: {ex.Message}");
        }
    }

    private void SetAllProjects(bool selected)
    {
        foreach (var p in Projects) p.IsSelected = selected;
    }

    private void SetAllChanges(bool selected)
    {
        foreach (var root in FileTree)
            root.IsChecked = selected;
        Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
    }

    // ── AppSettings tab ─────────────────────────────────────────────────────────

    private async Task LoadConfigFilesAsync()
    {
        var selected = Projects.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one project on the Publish tab first.",
                "No Projects Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;

        foreach (var f in ConfigFiles)
            f.PropertyChanged -= OnConfigFileItemChanged;

        ConfigFiles.Clear();
        _fileContents.Clear();

        // Reset editor
        _loadingFile  = true;
        EditorContent = string.Empty;
        _loadingFile  = false;
        SelectedConfigFile = null;
        IsEditorDirty = false;

        AppendLog($"Scanning config files in {selected.Count} project(s)…");

        try
        {
            var files = await _appSettingsService.LoadConfigFilesAsync(selected);
            foreach (var f in files)
            {
                f.PropertyChanged += OnConfigFileItemChanged;
                ConfigFiles.Add(f);
            }

            if (files.Count == 0)
            {
                AppendLog("No appsettings.json or web.config files found.");
                MessageBox.Show("No config files were found in the selected projects.",
                    "Nothing Found", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                AppendLog($"Found {files.Count} config file(s).");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFileContentAsync(ConfigFileItem file)
    {
        IsBusy = true;
        try
        {
            // Use cached content if available (preserves in-memory edits / global changes)
            if (!_fileContents.TryGetValue(file.FilePath, out var content))
            {
                content = await File.ReadAllTextAsync(file.FilePath, System.Text.Encoding.UTF8);
                _fileContents[file.FilePath] = content;
            }

            _loadingFile  = true;
            EditorContent = content;
            _loadingFile  = false;
            IsEditorDirty = false;
            SetValidationErrors([]);

            AppendLog($"Opened: {file.FilePath}");
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR opening {file.FileName}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveFileAsync()
    {
        var file = _selectedConfigFile;
        if (file == null) return;

        // Push current editor content to cache before saving
        var content = _editorContent;
        _fileContents[file.FilePath] = content;

        IsBusy = true;
        AppendLog($"Saving {file.FileName}…");

        try
        {
            await ElevatedFileWriter.WriteAllTextAsync(file.FilePath, content,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            file.IsModified = false;
            IsEditorDirty   = false;
            AppendLog($"Saved: {file.FilePath}");
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR saving {file.FileName}: {ex.Message}");
            MessageBox.Show($"Could not save file:\n{ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyGlobalChangeAsync()
    {
        var targets = ConfigFiles.Where(f => f.IsSelected).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("Check at least one file in the list before applying.",
                "No Files Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_globalKey))
        {
            MessageBox.Show("Enter a Global Key to search for.",
                "Missing Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Persist any in-progress editor edits to cache first
        if (_selectedConfigFile != null && _isEditorDirty)
        {
            _fileContents[_selectedConfigFile.FilePath] = _editorContent;
            _selectedConfigFile.IsModified = true;
        }

        IsBusy = true;
        int changedFiles = 0;
        int skippedFiles = 0;
        AppendLog($"Applying '{_globalKey}' = '{_globalValue}' to {targets.Count} selected file(s)…");

        try
        {
            foreach (var file in targets)
            {
                if (!_fileContents.TryGetValue(file.FilePath, out var content))
                {
                    try
                    {
                        content = await File.ReadAllTextAsync(file.FilePath, System.Text.Encoding.UTF8);
                        _fileContents[file.FilePath] = content;
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"ERROR reading {file.FileName}: {ex.Message}");
                        continue;
                    }
                }

                var updated = ApplyKeyChange(file, content, _globalKey, _globalValue);
                if (updated == content)
                {
                    skippedFiles++;
                    continue;
                }

                _fileContents[file.FilePath] = updated;
                file.IsModified = true;
                changedFiles++;

                // Refresh editor if this is the currently open file
                if (string.Equals(_selectedConfigFile?.FilePath, file.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    _loadingFile  = true;
                    EditorContent = updated;
                    _loadingFile  = false;
                    IsEditorDirty = false;
                }
            }

            AppendLog($"Global change complete. Updated: {changedFiles}, skipped (key not found): {skippedFiles}. Use Save File / Save All to persist.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAllModifiedAsync()
    {
        // Flush editor edits to cache first
        if (_selectedConfigFile != null && _isEditorDirty)
        {
            _fileContents[_selectedConfigFile.FilePath] = _editorContent;
            _selectedConfigFile.IsModified = true;
        }

        var modified = ConfigFiles.Where(f => f.IsModified).ToList();
        if (modified.Count == 0)
        {
            MessageBox.Show("No modified files to save.", "Nothing to Save",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsBusy = true;
        AppendLog($"Saving {modified.Count} modified file(s)…");
        int saved = 0;

        try
        {
            foreach (var file in modified)
            {
                if (!_fileContents.TryGetValue(file.FilePath, out var content))
                    continue;

                try
                {
                    await ElevatedFileWriter.WriteAllTextAsync(file.FilePath, content,
                        new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    file.IsModified = false;
                    saved++;
                }
                catch (Exception ex)
                {
                    AppendLog($"ERROR saving {file.FileName}: {ex.Message}");
                }
            }

            // If the open file was just saved, clear the dirty flag
            if (_selectedConfigFile != null && !_selectedConfigFile.IsModified)
                IsEditorDirty = false;

            AppendLog($"Saved {saved}/{modified.Count} file(s).");
            MessageBox.Show($"Saved {saved} file(s) successfully.", "Save Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ValidateSelectedConfig()
    {
        if (SelectedConfigFile == null)
        {
            AppendLog("WARNING: Select a config file first before validation.");
            return;
        }

        var content = _editorContent;
        if (string.IsNullOrWhiteSpace(content) && _fileContents.TryGetValue(SelectedConfigFile.FilePath, out var cached))
            content = cached;

        var isJson = string.Equals(SelectedConfigFile.FileType, "JSON", StringComparison.OrdinalIgnoreCase);
        var isXml = string.Equals(SelectedConfigFile.FileType, "XML", StringComparison.OrdinalIgnoreCase);

        if (!isJson && !isXml)
        {
            AppendLog($"WARNING: {SelectedConfigFile.FileName} is not a supported config type.");
            return;
        }

        var issues = isJson ? GetJsonValidationIssues(content) : GetXmlValidationIssues(content);
        var label = isJson ? "JSON" : "XML";

        if (issues.Count == 0)
        {
            SetValidationErrors([]);
            AppendLog($"{label} validation passed: {SelectedConfigFile.FileName}");
            MessageBox.Show($"No {label} syntax errors found in {SelectedConfigFile.FileName}.", $"{label} Validation",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var lines = issues.Select(i => i.Line).Distinct().OrderBy(x => x).ToList();
        SetValidationErrors(lines);

        foreach (var issue in issues)
            AppendLog($"ERROR: {label} validation failed in {SelectedConfigFile.FileName} at line {issue.Line}, column {issue.Column}: {issue.Message}");

        MessageBox.Show(
            $"Found {issues.Count} {label} issue(s) across {lines.Count} line(s).\nError lines are highlighted in light red in the editor gutter.",
            $"{label} Validation Errors",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void UpdateEditorLineNumbers()
    {
        var text = _editorContent ?? string.Empty;
        var lineCount = 1;

        if (text.Length > 0)
            lineCount = text.Count(c => c == '\n') + 1;

        EditorLines.Clear();
        for (var i = 1; i <= lineCount; i++)
        {
            EditorLines.Add(new EditorLineInfo
            {
                Number = i,
                IsError = _validationErrorLines.Contains(i)
            });
        }
    }

    private void SetValidationErrors(IEnumerable<int> lines)
    {
        _validationErrorLines.Clear();
        foreach (var line in lines.Where(l => l > 0))
            _validationErrorLines.Add(line);

        ValidationErrorLineNumber = _validationErrorLines.OrderBy(x => x).FirstOrDefault();
        OnPropertyChanged(nameof(ValidationErrorCount));
        OnPropertyChanged(nameof(ValidationErrorLinesSummary));
        UpdateEditorLineNumbers();
    }

    private static List<JsonValidationIssue> GetJsonValidationIssues(string content)
    {
        var issues = new List<JsonValidationIssue>();

        try
        {
            using var _ = JsonDocument.Parse(content, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            return issues;
        }
        catch (JsonException ex)
        {
            issues.Add(new JsonValidationIssue(
                (int)ex.LineNumber.GetValueOrDefault() + 1,
                (int)ex.BytePositionInLine.GetValueOrDefault() + 1,
                ex.Message));
        }

        // Heuristic scan to report additional likely missing comma lines in one pass.
        var lines = content.Replace("\r\n", "\n").Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            var prev = StripJsonLineComment(lines[i - 1]).Trim();
            var curr = StripJsonLineComment(lines[i]).Trim();
            if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(curr))
                continue;

            var currStartsProperty = curr.StartsWith('"');
            var prevEndsValue =
                prev.EndsWith('"') || prev.EndsWith("true", StringComparison.OrdinalIgnoreCase) ||
                prev.EndsWith("false", StringComparison.OrdinalIgnoreCase) || prev.EndsWith("null", StringComparison.OrdinalIgnoreCase) ||
                char.IsDigit(prev[^1]) || prev.EndsWith('}') || prev.EndsWith(']');

            var prevAllowsNoComma = prev.EndsWith('{') || prev.EndsWith('[') || prev.EndsWith(',') || prev.EndsWith(':');

            if (currStartsProperty && prevEndsValue && !prevAllowsNoComma)
            {
                issues.Add(new JsonValidationIssue(
                    i,
                    Math.Max(1, lines[i - 1].Length),
                    "Likely missing comma before next property."));
            }
        }

        return issues
            .GroupBy(x => new { x.Line, x.Column, x.Message })
            .Select(g => g.First())
            .OrderBy(x => x.Line)
            .ThenBy(x => x.Column)
            .ToList();
    }

    private static List<JsonValidationIssue> GetXmlValidationIssues(string content)
    {
        var issues = new List<JsonValidationIssue>();

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = false,
                IgnoreWhitespace = false
            };

            using var sr = new StringReader(content);
            using var reader = XmlReader.Create(sr, settings);
            while (reader.Read()) { }

            return issues;
        }
        catch (XmlException ex)
        {
            issues.Add(new JsonValidationIssue(
                Math.Max(1, ex.LineNumber),
                Math.Max(1, ex.LinePosition),
                ex.Message));
            return issues;
        }
    }

    private static string StripJsonLineComment(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;

        var sb = new StringBuilder();
        var inString = false;
        var escaped = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (!inString && ch == '/' && i + 1 < line.Length && line[i + 1] == '/')
                break;

            sb.Append(ch);

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
                inString = !inString;
        }

        return sb.ToString();
    }

    private readonly record struct JsonValidationIssue(int Line, int Column, string Message);

    private static string ApplyKeyChange(ConfigFileItem file, string content, string key, string value)
        => file.FileType switch
        {
            "JSON" => JsonFlattenHelper.ApplyChange(content, key, value),
            "XML"  => XmlConfigHelper.ApplyChange(content, key, value),
            _      => content
        };

    private void OnConfigFileItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConfigFileItem.IsModified) or nameof(ConfigFileItem.IsSelected))
            Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
    }

    private void AppendLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var upper = message.ToUpperInvariant();
            var severity = upper.Contains("ERROR") || upper.Contains("FATAL")
                ? LogSeverity.Error
                : upper.Contains("WARN")
                    ? LogSeverity.Warning
                    : LogSeverity.Info;

            LogEntries.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = message,
                Severity = severity
            });
        });
    }

    public void ClearLogs()
    {
        LogEntries.Clear();
        OnPropertyChanged(nameof(FilteredLogEntries));
    }
}
