using System.Windows.Media;

namespace DeploymentTool.Models;

public enum LogSeverity
{
    Info,
    Warning,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; init; }
    public string Message { get; init; } = string.Empty;
    public LogSeverity Severity { get; init; }

    public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";

    public Brush Foreground => Severity switch
    {
        LogSeverity.Error => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171")),
        LogSeverity.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")),
        _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"))
    };
}
