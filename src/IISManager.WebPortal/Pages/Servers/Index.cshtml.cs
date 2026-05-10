using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages.Servers;

public class ServersIndexModel : PageModel
{
    private readonly IServerAppService _service;
    public ServersIndexModel(IServerAppService service) => _service = service;

    public IEnumerable<ServerDto> Servers { get; set; } = Enumerable.Empty<ServerDto>();

    public async Task OnGetAsync() => Servers = await _service.GetAllAsync();

    public async Task<IActionResult> OnPostDeleteAsync(int serverId)
    {
        var result = await _service.DeleteAsync(serverId);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Server deleted" : result.Error;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSyncIISAsync(int serverId)
    {
        var result = await _service.SyncIISStateAsync(serverId);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "IIS sync requested" : result.Error;
        return RedirectToPage();
    }
}
