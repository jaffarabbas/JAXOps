using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages.Agents;

public class AgentLogsModel : PageModel
{
    private readonly IServerAppService _servers;
    public AgentLogsModel(IServerAppService servers) => _servers = servers;

    public ServerDto? Server { get; set; }

    public async Task<IActionResult> OnGetAsync(int serverId)
    {
        Server = await _servers.GetByIdAsync(serverId);
        if (Server is null) return NotFound();
        return Page();
    }
}
