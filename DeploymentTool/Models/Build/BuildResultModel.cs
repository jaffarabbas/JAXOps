namespace DeploymentTool.Models.Build;

public class BuildResultModel
{
    public bool Success { get; set; }
    public string Configuration { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> LogLines { get; set; } = [];
    public string ErrorSummary { get; set; } = string.Empty;

    public string DurationDisplay => Duration.TotalSeconds < 60
        ? $"{Duration.TotalSeconds:F1}s"
        : $"{Duration.TotalMinutes:F1}m";
}
