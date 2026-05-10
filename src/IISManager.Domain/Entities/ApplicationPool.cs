using IISManager.Domain.Enums;

namespace IISManager.Domain.Entities;

public class ApplicationPool
{
    public int Id { get; set; }
    public int ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AppPoolStatus Status { get; set; }
    public string RuntimeVersion { get; set; } = "v4.0";
    public string PipelineMode { get; set; } = "Integrated";
    public bool AutoStart { get; set; } = true;
    public DateTime LastSyncedAt { get; set; }
}
