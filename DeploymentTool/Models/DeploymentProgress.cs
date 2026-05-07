namespace DeploymentTool.Models;

public class DeploymentProgress
{
    public int Current { get; init; }
    public int Total { get; init; }
    public string StepName { get; init; } = string.Empty;

    public double Percentage => Total == 0 ? 0 : (double)Current / Total * 100.0;
}
