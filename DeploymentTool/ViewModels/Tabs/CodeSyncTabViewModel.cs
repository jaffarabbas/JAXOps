using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DeploymentTool.Helpers;
using DeploymentTool.Models;
using DeploymentTool.Models.Sync;
using DeploymentTool.Services.Sync;

namespace DeploymentTool.ViewModels.Tabs;

public class CodeSyncTabViewModel : BaseViewModel
{
    private readonly ICodeSyncService _sync;
    private readonly Action<string>   _log;
    private CancellationTokenSource?  _cts;

    private string    _mainCodePath = string.Empty;
    private string    _miniCodePath = string.Empty;
    private DateTime? _fromDate     = null;
    private DateTime? _toDate       = null;
    private bool      _useDateRange = false;
    private bool      _isWorking;
    private string    _statusText   = "Ready";
    private double    _progress;
    private string    _searchText   = string.Empty;

    public ObservableCollection<ChangedFileModel> ChangedFiles  { get; } = [];
    public ObservableCollection<FileTreeNode>     SyncFileTree  { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public string MainCodePath
    {
        get => _mainCodePath;
        set { if (SetField(ref _mainCodePath, value)) InvalidateCommands(); }
    }

    public string MiniCodePath
    {
        get => _miniCodePath;
        set { if (SetField(ref _miniCodePath, value)) InvalidateCommands(); }
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set => SetField(ref _fromDate, value);
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set => SetField(ref _toDate, value);
    }

    public bool UseDateRange
    {
        get => _useDateRange;
        set
        {
            if (SetField(ref _useDateRange, value))
                OnPropertyChanged(nameof(DatePickersEnabled));
        }
    }

    public bool DatePickersEnabled => _useDateRange;

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

    public string SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value);
    }

    public int SelectedCount => ChangedFiles.Count(f => f.IsSelected);
    public int NewCount      => ChangedFiles.Count(f => f.Status == "New");
    public int ModifiedCount => ChangedFiles.Count(f => f.Status == "Modified");

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand BrowseMainCodeCommand { get; }
    public ICommand BrowseMiniCodeCommand { get; }
    public ICommand ScanCommand           { get; }
    public ICommand CopyToMiniCommand     { get; }
    public ICommand SelectAllCommand      { get; }
    public ICommand SelectNoneCommand     { get; }
    public ICommand CancelCommand         { get; }

    public CodeSyncTabViewModel(ICodeSyncService sync, Action<string> log)
    {
        _sync = sync;
        _log  = log;

        BrowseMainCodeCommand = new RelayCommand(
            () => BrowseFolder("Select Main Code Folder", v => MainCodePath = v));

        BrowseMiniCodeCommand = new RelayCommand(
            () => BrowseFolder("Select Mini Code Folder", v => MiniCodePath = v));

        ScanCommand = new RelayCommand(
            async () => { try { await RunScanAsync(); } catch (Exception ex) { _log($"FATAL: {ex.Message}"); } },
            () => !IsWorking && Directory.Exists(_mainCodePath) && Directory.Exists(_miniCodePath));

        CopyToMiniCommand = new RelayCommand(
            async () => { try { await RunCopyAsync(); } catch (Exception ex) { _log($"FATAL: {ex.Message}"); } },
            () => !IsWorking && ChangedFiles.Any(f => f.IsSelected) && Directory.Exists(_miniCodePath));

        SelectAllCommand = new RelayCommand(() =>
        {
            foreach (var root in SyncFileTree) root.IsChecked = true;
            NotifyCounts();
            InvalidateCommands();
        });

        SelectNoneCommand = new RelayCommand(() =>
        {
            foreach (var root in SyncFileTree) root.IsChecked = false;
            NotifyCounts();
            InvalidateCommands();
        });

        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsWorking);
    }

    // ── Operations ────────────────────────────────────────────────────────────

    private async Task RunScanAsync()
    {
        _cts       = new CancellationTokenSource();
        IsWorking  = true;
        Progress   = 0;
        StatusText = "Scanning for changes…";

        UI(() => { ChangedFiles.Clear(); SyncFileTree.Clear(); });

        try
        {
            _log($"Main: {_mainCodePath}");
            _log($"Mini: {_miniCodePath}");

            DateTime? from = _useDateRange ? _fromDate : null;
            DateTime? to   = _useDateRange ? _toDate   : null;

            var progress = new Progress<string>(line => UI(() => _log(line)));

            var files = await _sync.ScanChangedFilesAsync(
                _mainCodePath, _miniCodePath, from, to, [], progress, _cts.Token);

            UI(() =>
            {
                foreach (var f in files) ChangedFiles.Add(f);
                BuildSyncFileTree();
                Progress   = 100;
                StatusText = files.Count == 0
                    ? "No changes found in the specified range"
                    : $"Found {files.Count} file(s)";
                NotifyCounts();
                InvalidateCommands();
                _log($"Scan complete: {files.Count} file(s).");
            });
        }
        catch (OperationCanceledException) { UI(() => StatusText = "Cancelled"); }
        catch (Exception ex)               { UI(() => StatusText = $"Error: {ex.Message}"); _log($"ERROR: {ex.Message}"); }
        finally                            { UI(() => { IsWorking = false; _cts?.Dispose(); _cts = null; }); }
    }

    private async Task RunCopyAsync()
    {
        _cts       = new CancellationTokenSource();
        IsWorking  = true;
        Progress   = 0;
        var selected = ChangedFiles.Where(f => f.IsSelected).ToList();
        StatusText = $"Copying {selected.Count} file(s) to mini…";

        try
        {
            _log($"Copying {selected.Count} file(s) → {_miniCodePath}");

            var progress = new Progress<(int Done, int Total, string File)>(p =>
                UI(() =>
                {
                    Progress   = p.Done * 100.0 / Math.Max(p.Total, 1);
                    StatusText = $"Copying {p.Done}/{p.Total}: {Path.GetFileName(p.File)}";
                }));

            await _sync.CopyChangedFilesAsync(selected, _mainCodePath, _miniCodePath, progress, _log, _cts.Token);

            UI(() =>
            {
                Progress   = 100;
                StatusText = $"Done — {selected.Count} file(s) copied to mini";
                _log($"Copy complete: {selected.Count} file(s).");
            });
        }
        catch (OperationCanceledException) { UI(() => StatusText = "Cancelled"); }
        catch (Exception ex)               { UI(() => StatusText = $"Error: {ex.Message}"); _log($"ERROR: {ex.Message}"); }
        finally                            { UI(() => { IsWorking = false; _cts?.Dispose(); _cts = null; }); }
    }

    // ── Tree building ─────────────────────────────────────────────────────────

    private void BuildSyncFileTree()
    {
        SyncFileTree.Clear();

        var nodeMap = new Dictionary<string, FileTreeNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var cf in ChangedFiles)
        {
            var status = cf.Status == "New" ? ChangeStatus.New : ChangeStatus.Modified;
            var fc = new FileChange
            {
                RelativePath  = cf.RelativePath,
                FullPath      = cf.FullPath,
                Status        = status,
                LastWriteTime = cf.LastWriteTime,
                IsSelected    = cf.IsSelected
            };

            // FileTreeNode.IsChecked → FileChange.IsSelected → ChangedFileModel.IsSelected
            var localCf = cf;
            fc.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(FileChange.IsSelected)) return;
                localCf.IsSelected = fc.IsSelected;
                UI(() => { NotifyCounts(); InvalidateCommands(); });
            };

            var parts  = cf.RelativePath.Replace('\\', '/').Split('/');
            FileTreeNode? parent = null;

            for (int i = 0; i < parts.Length; i++)
            {
                bool   isLeaf = i == parts.Length - 1;
                string key    = string.Join("/", parts[..(i + 1)]);

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

                    if (parent == null) SyncFileTree.Add(node);
                    else                parent.Children.Add(node);
                }

                parent = node;
            }
        }

        foreach (var root in SyncFileTree)
            SyncFolderState(root);
    }

    private static void SyncFolderState(FileTreeNode node)
    {
        if (!node.IsFolder) return;
        foreach (var child in node.Children)
            SyncFolderState(child);
        node.UpdateFromChildren();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(NewCount));
        OnPropertyChanged(nameof(ModifiedCount));
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
