using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages.Deployments;

public class DeploymentConsoleModel : PageModel
{
    private readonly IDeploymentAppService _service;
    public DeploymentConsoleModel(IDeploymentAppService service) => _service = service;

    public DeploymentDto? Deployment { get; set; }
    public IEnumerable<DeploymentLogDto> ExistingLogs { get; set; } = Enumerable.Empty<DeploymentLogDto>();

    public async Task OnGetAsync(int id)
    {
        Deployment = await _service.GetByIdAsync(id);
        if (Deployment is not null)
            ExistingLogs = await _service.GetLogsAsync(id);
    }
}
