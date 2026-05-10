namespace IISManager.Application.DTOs;

public class CreateWebsiteDto
{
    public string Name { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public string AppPoolName { get; set; } = string.Empty;
    public string Protocol { get; set; } = "http";
    public string IpAddress { get; set; } = "*";
    public int Port { get; set; } = 80;
    public string? HostName { get; set; }
}

public class CreateAppPoolDto
{
    public string Name { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = "v4.0";
    public string PipelineMode { get; set; } = "Integrated";
}

public class ServerHealthDto
{
    public int ServerId { get; set; }
    public double CpuPercent { get; set; }
    public long RamUsedMB { get; set; }
    public long RamTotalMB { get; set; }
    public double RamPercent => RamTotalMB > 0 ? Math.Round(RamUsedMB * 100.0 / RamTotalMB, 1) : 0;
    public long DiskUsedGB { get; set; }
    public long DiskTotalGB { get; set; }
    public double DiskPercent => DiskTotalGB > 0 ? Math.Round(DiskUsedGB * 100.0 / DiskTotalGB, 1) : 0;
    public bool IISRunning { get; set; }
    public int RunningSites { get; set; }
    public int RunningAppPools { get; set; }
    public string? AgentVersion { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class WebsiteDto
{
    public int Id { get; set; }
    public int ServerId { get; set; }
    public long IISId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DefaultAppPool { get; set; } = string.Empty;
    public string BindingsJson { get; set; } = "[]";
    public DateTime LastSyncedAt { get; set; }
}

public class AppPoolDto
{
    public int Id { get; set; }
    public int ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string PipelineMode { get; set; } = string.Empty;
    public bool AutoStart { get; set; }
    public DateTime LastSyncedAt { get; set; }
}
