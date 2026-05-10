namespace IISManager.Domain.Entities;

public class ServerHealth
{
    public long Id { get; set; }
    public int ServerId { get; set; }
    public double CpuPercent { get; set; }
    public long RamUsedMB { get; set; }
    public long RamTotalMB { get; set; }
    public long DiskUsedGB { get; set; }
    public long DiskTotalGB { get; set; }
    public bool IISRunning { get; set; }
    public int RunningSites { get; set; }
    public int RunningAppPools { get; set; }
    public string? AgentVersion { get; set; }
    public DateTime RecordedAt { get; set; }
}
