using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeploymentTool.Models.Git;

public class GitRepositoryConfig : INotifyPropertyChanged
{
    private string _name          = string.Empty;
    private string _localPath     = string.Empty;
    private string _remoteUrl     = string.Empty;
    private string _defaultBranch = "main";

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public string LocalPath
    {
        get => _localPath;
        set { if (_localPath != value) { _localPath = value; OnPropertyChanged(); } }
    }

    public string RemoteUrl
    {
        get => _remoteUrl;
        set { if (_remoteUrl != value) { _remoteUrl = value; OnPropertyChanged(); } }
    }

    public string DefaultBranch
    {
        get => _defaultBranch;
        set { if (_defaultBranch != value) { _defaultBranch = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
