using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace DeploymentTool.Models.Sync;

public class ChangedFileModel : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string RelativePath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;  // "New" | "Modified"
    public DateTime LastWriteTime { get; set; }
    public long FileSizeBytes { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public string SizeDisplay => FileSizeBytes < 1024
        ? $"{FileSizeBytes} B"
        : FileSizeBytes < 1_048_576
            ? $"{FileSizeBytes / 1024.0:F1} KB"
            : $"{FileSizeBytes / 1_048_576.0:F1} MB";

    public string FileName => Path.GetFileName(RelativePath);
    public string FileDirectory => Path.GetDirectoryName(RelativePath) ?? string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
