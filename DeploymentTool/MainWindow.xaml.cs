using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using DeploymentTool.ViewModels;

namespace DeploymentTool;

public partial class MainWindow : Window
{
    private int _editorSearchStart = -1;
    private string _lastSearchTerm = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        if (DataContext is MainViewModel vm)
        {
            vm.LogEntries.CollectionChanged               += OnLogEntriesChanged;
            vm.Deploy.DeployLog.CollectionChanged         += OnDeployLogChanged;
            vm.Transfer.TransferLog.CollectionChanged     += OnTransferLogChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel vm) return;
        if (e.PropertyName != nameof(MainViewModel.ValidationErrorLineNumber)) return;

        if (vm.ValidationErrorLineNumber > 0)
            HighlightEditorLine(vm.ValidationErrorLineNumber);
    }

    private void OnDeployLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not NotifyCollectionChangedAction.Add)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            if (DeployLogList.Items.Count > 0)
                DeployLogList.ScrollIntoView(DeployLogList.Items[^1]);
        }, DispatcherPriority.Background);
    }

    private void OnTransferLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not NotifyCollectionChangedAction.Add)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            if (TransferLogList.Items.Count > 0)
                TransferLogList.ScrollIntoView(TransferLogList.Items[^1]);
        }, DispatcherPriority.Background);
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not NotifyCollectionChangedAction.Add)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            if (DataContext is not MainViewModel vm)
                return;

            var lastVisible = vm.FilteredLogEntries.Cast<object>().LastOrDefault();
            if (lastVisible != null)
                LogList.ScrollIntoView(lastVisible);
        }, DispatcherPriority.Background);
    }

    private void CodebaseComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.CodebaseSearchText = string.Empty;
    }

    private void FindInEditor_Click(object sender, RoutedEventArgs e)
    {
        _ = TrySelectEditorMatch(EditorSearchBox.Text, findNext: true, focusEditor: false);
    }

    private void EditorSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var term = EditorSearchBox.Text;

        if (string.IsNullOrWhiteSpace(term))
        {
            _editorSearchStart = -1;
            _lastSearchTerm = string.Empty;
            return;
        }

        _ = TrySelectEditorMatch(term, findNext: false, focusEditor: false);
    }

    private void HighlightEditorLine(int oneBasedLine)
    {
        if (oneBasedLine <= 0) return;

        var zeroBasedLine = oneBasedLine - 1;
        if (zeroBasedLine >= EditorTextBox.LineCount) return;

        var start = EditorTextBox.GetCharacterIndexFromLineIndex(zeroBasedLine);
        if (start < 0) return;

        var lineText = EditorTextBox.GetLineText(zeroBasedLine);
        var length = Math.Max(1, lineText.TrimEnd('\r', '\n').Length);

        EditorTextBox.Focus();
        EditorTextBox.Select(start, length);
        EditorTextBox.ScrollToLine(zeroBasedLine);
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ClearLogs();
    }

    private void DeployPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox pb)
            vm.Deploy.Password = pb.Password;
    }

    private void TransferPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox pb)
            vm.Transfer.Password = pb.Password;
    }

    private bool TrySelectEditorMatch(string term, bool findNext, bool focusEditor)
    {
        var text = EditorTextBox.Text;
        if (string.IsNullOrWhiteSpace(term) || string.IsNullOrEmpty(text))
            return false;

        if (!string.Equals(_lastSearchTerm, term, StringComparison.OrdinalIgnoreCase))
        {
            _editorSearchStart = -1;
            _lastSearchTerm = term;
        }

        var start = findNext && _editorSearchStart >= 0
            ? _editorSearchStart + 1
            : 0;

        var index = text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);
        if (index < 0 && findNext)
            index = text.IndexOf(term, 0, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
            return false;

        _editorSearchStart = index;
        var searchCaret = EditorSearchBox.CaretIndex;
        EditorTextBox.Focus();
        EditorTextBox.Select(index, term.Length);

        var lineIndex = EditorTextBox.GetLineIndexFromCharacterIndex(index);
        ScrollEditorToLine(lineIndex);
        Dispatcher.BeginInvoke(() => ScrollEditorToLine(lineIndex), DispatcherPriority.Background);

        if (!focusEditor)
        {
            EditorSearchBox.Focus();
            EditorSearchBox.CaretIndex = Math.Clamp(searchCaret, 0, EditorSearchBox.Text.Length);
        }

        return true;
    }

    private void ScrollEditorToLine(int zeroBasedLine)
    {
        if (zeroBasedLine < 0)
            return;

        const double lineHeight = 18.0;
        const double topPadding = 10.0;
        var lineTop = topPadding + (zeroBasedLine * lineHeight);
        var targetOffset = Math.Max(0, lineTop - (EditorScrollViewer.ViewportHeight / 2.0));

        EditorScrollViewer.ScrollToVerticalOffset(targetOffset);
    }
}
