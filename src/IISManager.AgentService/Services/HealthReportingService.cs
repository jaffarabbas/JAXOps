using IISManager.Contracts.Events;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IISManager.AgentService.Services;

public class HealthReportingService
{
    private readonly AgentConfiguration _config;
    private readonly IISManagementService _iis;
    private readonly ILogger<HealthReportingService> _logger;

    public HealthReportingService(AgentConfiguration config, IISManagementService iis, ILogger<HealthReportingService> logger)
    {
        _config = config;
        _iis = iis;
        _logger = logger;
    }

    public Task<AgentHeartbeatEvent> BuildHeartbeatAsync()
    {
        var cpu = GetCpuPercent();
        var (ramUsed, ramTotal) = GetRamMB();
        var (diskUsed, diskTotal) = GetDiskGB();
        var iisRunning = _iis.IsIISRunning();
        var (sites, pools) = iisRunning ? _iis.GetIISCounts() : (0, 0);

        return Task.FromResult(new AgentHeartbeatEvent
        {
            ServerId = _config.ServerId,
            MachineName = Environment.MachineName,
            AgentVersion = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0",
            CpuPercent = cpu,
            RamUsedMB = ramUsed,
            RamTotalMB = ramTotal,
            DiskUsedGB = diskUsed,
            DiskTotalGB = diskTotal,
            IISRunning = iisRunning,
            RunningSites = sites,
            RunningAppPools = pools
        });
    }

    private static double GetCpuPercent()
    {
        try
        {
            using var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            counter.NextValue(); // first sample always returns 0
            Thread.Sleep(100);
            return Math.Round(counter.NextValue(), 1);
        }
        catch { return 0; }
    }

    // Uses GlobalMemoryStatusEx via P/Invoke — available on all Windows versions,
    // no extra package required, replaces Microsoft.VisualBasic.Devices.ComputerInfo.
    private static (long usedMB, long totalMB) GetRamMB()
    {
        try
        {
            var status = new MEMORYSTATUSEX();
            if (!GlobalMemoryStatusEx(ref status)) return (0, 0);
            var total = (long)(status.ullTotalPhys / 1024 / 1024);
            var available = (long)(status.ullAvailPhys / 1024 / 1024);
            return (total - available, total);
        }
        catch { return (0, 0); }
    }

    private static (long usedGB, long totalGB) GetDiskGB()
    {
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
            if (drive is null) return (0, 0);
            var total = drive.TotalSize / 1024 / 1024 / 1024;
            var free = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
            return (total - free, total);
        }
        catch { return (0, 0); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
