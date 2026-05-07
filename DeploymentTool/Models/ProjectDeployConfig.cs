using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeploymentTool.Models;

/// <summary>
/// Per-project deployment configuration — one instance per microservice.
/// Holds the IIS application alias, dedicated app pool, and pool settings.
/// </summary>
public class ProjectDeployConfig : INotifyPropertyChanged
{
    private bool   _isSelected       = true;
    private string _appPoolName      = string.Empty;
    private string _applicationAlias = string.Empty;
    private string _dotNetClrVersion = "v4.0";
    private string _pipelineMode     = "Integrated";
    private bool   _enable32Bit      = false;

    public string ProjectName { get; init; } = string.Empty;

    // When set, this path is used directly as the copy source instead of
    // combining PublishOutputFolder + ProjectName.
    public string SourceOverridePath { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; Notify(); } }
    }

    public string AppPoolName
    {
        get => _appPoolName;
        set { if (_appPoolName != value) { _appPoolName = value; Notify(); } }
    }

    public string ApplicationAlias
    {
        get => _applicationAlias;
        set { if (_applicationAlias != value) { _applicationAlias = value; Notify(); } }
    }

    public string DotNetClrVersion
    {
        get => _dotNetClrVersion;
        set { if (_dotNetClrVersion != value) { _dotNetClrVersion = value; Notify(); } }
    }

    public string PipelineMode
    {
        get => _pipelineMode;
        set { if (_pipelineMode != value) { _pipelineMode = value; Notify(); } }
    }

    public bool Enable32Bit
    {
        get => _enable32Bit;
        set { if (_enable32Bit != value) { _enable32Bit = value; Notify(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
