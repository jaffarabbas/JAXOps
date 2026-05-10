using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeploymentTool.Models.Git;

public class GitBranchModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public bool IsRemote { get; set; }
    public bool IsCurrentBranch { get; set; }

    public string ShortName => IsRemote
        ? Name.Replace("origin/", string.Empty)
        : Name;

    public string DisplayName => IsCurrentBranch ? $"* {Name}" : Name;

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
