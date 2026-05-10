using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages.Agents;

public class AgentsIndexModel : PageModel
{
    private readonly IServerAppService _servers;
    public AgentsIndexModel(IServerAppService servers) => _servers = servers;

    public IEnumerable<ServerDto> Servers { get; set; } = [];

    public async Task OnGetAsync() => Servers = await _servers.GetAllAsync();
}
