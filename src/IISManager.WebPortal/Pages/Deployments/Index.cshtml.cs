using IISManager.Application.Interfaces;
using IISManager.Domain.Enums;
using IISManager.Shared.Models;
using IISManager.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages.Deployments;

public class DeploymentsIndexModel : PageModel
{
    private readonly IDeploymentAppService _service;
    public DeploymentsIndexModel(IDeploymentAppService service) => _service = service;

    public PagedResult<DeploymentDto> Deployments { get; set; } = PagedResult<DeploymentDto>.Create([], 0, 1, 20);
    public string? StatusFilter { get; set; }

    public async Task OnGetAsync(string? status, int page = 1)
    {
        StatusFilter = status;
        DeploymentStatus? statusEnum = Enum.TryParse<DeploymentStatus>(status, out var s) ? s : null;
        Deployments = await _service.GetPagedAsync(page, 20, status: statusEnum);
    }

    public async Task<IActionResult> OnPostRollbackAsync(int deploymentId)
    {
        var result = await _service.StartRollbackAsync(deploymentId, User.Identity?.Name ?? "system");
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Rollback initiated (#{result.Value})" : result.Error;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAsync(int deploymentId)
    {
        var result = await _service.CancelDeploymentAsync(deploymentId, User.Identity?.Name ?? "system");
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Deployment #{deploymentId} cancelled." : result.Error;
        return RedirectToPage();
    }
}
