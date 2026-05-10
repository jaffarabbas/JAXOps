using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeploymentTool.Models;

public class VbPublishedFile : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string RelativePath  { get; init; } = string.Empty;
    public string Status        { get; init; } = string.Empty;   // "New" | "Modified"
    public long   FileSizeBytes { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string SizeDisplay => FileSizeBytes switch
    {
        < 1024        => $"{FileSizeBytes} B",
        < 1_048_576   => $"{FileSizeBytes / 1024.0:F1} KB",
        _             => $"{FileSizeBytes / 1_048_576.0:F1} MB"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
