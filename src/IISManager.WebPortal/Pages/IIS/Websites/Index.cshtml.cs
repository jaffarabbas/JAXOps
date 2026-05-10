using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages.IIS.Websites;

public class WebsitesIndexModel : PageModel
{
    private readonly IServerAppService _service;
    public WebsitesIndexModel(IServerAppService service) => _service = service;

    public IEnumerable<ServerDto> Servers { get; set; } = [];
    public ServerDto? SelectedServer { get; set; }
    public IEnumerable<WebsiteDto> Websites { get; set; } = [];
    public IEnumerable<AppPoolDto> AppPools { get; set; } = [];

    public async Task OnGetAsync(int? serverId)
    {
        Servers = await _service.GetAllAsync();
        if (serverId.HasValue)
        {
            SelectedServer = Servers.FirstOrDefault(s => s.Id == serverId.Value);
            if (SelectedServer is not null)
            {
                var wsTask = _service.GetWebsitesAsync(serverId.Value);
                var apTask = _service.GetAppPoolsAsync(serverId.Value);
                await Task.WhenAll(wsTask, apTask);
                Websites = wsTask.Result;
                AppPools = apTask.Result;
            }
        }
    }
}
