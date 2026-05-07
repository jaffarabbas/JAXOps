using System.Windows.Media;

namespace DeploymentTool.Models;

public enum DeployLogSeverity { Info, Warning, Error, Success }

public class DeploymentLogItem
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Message { get; init; } = string.Empty;
    public DeployLogSeverity Severity { get; init; } = DeployLogSeverity.Info;

    public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";

    public Brush Foreground => Severity switch
    {
        DeployLogSeverity.Error   => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171")),
        DeployLogSeverity.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")),
        DeployLogSeverity.Success => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399")),
        _                         => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#93C5FD"))
    };
}
