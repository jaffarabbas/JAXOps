using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using IISManager.Domain.Enums;
using IISManager.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages;

public class IndexModel : PageModel
{
    private readonly IServerAppService _servers;
    private readonly IDeploymentAppService _deployments;
    private readonly IServerHealthRepository _health;

    public IndexModel(IServerAppService servers, IDeploymentAppService deployments, IServerHealthRepository health)
    {
        _servers = servers;
        _deployments = deployments;
        _health = health;
    }

    public IEnumerable<ServerDto> Servers { get; set; } = Enumerable.Empty<ServerDto>();
    public Dictionary<int, ServerHealthDto> HealthByServer { get; set; } = new();
    public int OnlineServers { get; set; }
    public int OfflineServers { get; set; }
    public int TodayDeployments { get; set; }
    public int ActiveDeployments { get; set; }
    public IEnumerable<DeploymentDto> RecentDeployments { get; set; } = Enumerable.Empty<DeploymentDto>();

    public async Task OnGetAsync()
    {
        Servers = await _servers.GetAllAsync();
        OnlineServers = Servers.Count(s => s.Status == ServerStatus.Online);
        OfflineServers = Servers.Count(s => s.Status == ServerStatus.Offline);

        foreach (var server in Servers)
        {
            var h = await _health.GetLatestAsync(server.Id);
            if (h is not null)
            {
                HealthByServer[server.Id] = new ServerHealthDto
                {
                    ServerId = h.ServerId,
                    CpuPercent = h.CpuPercent,
                    RamUsedMB = h.RamUsedMB,
                    RamTotalMB = h.RamTotalMB,
                    DiskUsedGB = h.DiskUsedGB,
                    DiskTotalGB = h.DiskTotalGB,
                    IISRunning = h.IISRunning,
                    RunningSites = h.RunningSites,
                    RunningAppPools = h.RunningAppPools,
                    AgentVersion = h.AgentVersion,
                    RecordedAt = h.RecordedAt
                };
            }
        }

        var paged = await _deployments.GetPagedAsync(1, 10);
        RecentDeployments = paged.Items;
        TodayDeployments = paged.Items.Count(d => d.CreatedAt.Date == DateTime.UtcNow.Date);
        ActiveDeployments = paged.Items.Count(d => d.Status == DeploymentStatus.InProgress);
    }
}
