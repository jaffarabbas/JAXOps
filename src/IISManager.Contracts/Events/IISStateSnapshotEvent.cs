using IISManager.Contracts.Enums;

namespace IISManager.Contracts.Events;

public class IISStateSnapshotEvent : AgentEventBase
{
    public IISStateSnapshotEvent() => EventType = EventType.IISStateSnapshot;

    public List<SiteInfo> Sites { get; set; } = new();
    public List<AppPoolInfo> AppPools { get; set; } = new();
}

public class SiteInfo
{
    public long IISId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DefaultAppPool { get; set; } = string.Empty;
    public List<BindingData> Bindings { get; set; } = new();
}

public class AppPoolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string PipelineMode { get; set; } = string.Empty;
    public bool AutoStart { get; set; }
}

public class BindingData
{
    public string Protocol { get; set; } = string.Empty;
    public string BindingInformation { get; set; } = string.Empty;
    public string? HostName { get; set; }
}
