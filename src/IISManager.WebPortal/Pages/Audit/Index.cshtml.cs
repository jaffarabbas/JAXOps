using IISManager.Application.Interfaces;
using IISManager.Domain.Entities;
using IISManager.Shared.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages.Audit;

public class AuditIndexModel : PageModel
{
    private readonly IAuditAppService _service;
    public AuditIndexModel(IAuditAppService service) => _service = service;

    public PagedResult<AuditLog> Logs { get; set; } = PagedResult<AuditLog>.Create([], 0, 1, 50);

    public async Task OnGetAsync(int page = 1)
        => Logs = await _service.GetPagedAsync(page, 50);
}
