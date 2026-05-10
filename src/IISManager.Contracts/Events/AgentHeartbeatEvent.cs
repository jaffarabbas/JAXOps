using IISManager.Contracts.Enums;

namespace IISManager.Contracts.Events;

public class AgentHeartbeatEvent : AgentEventBase
{
    public AgentHeartbeatEvent() => EventType = EventType.Heartbeat;

    public string MachineName { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public long RamUsedMB { get; set; }
    public long RamTotalMB { get; set; }
    public long DiskUsedGB { get; set; }
    public long DiskTotalGB { get; set; }
    public bool IISRunning { get; set; }
    public int RunningSites { get; set; }
    public int RunningAppPools { get; set; }
}
