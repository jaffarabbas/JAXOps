using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages.Servers;

public class CreateServerModel : PageModel
{
    private readonly IServerAppService _service;
    public CreateServerModel(IServerAppService service) => _service = service;

    [BindProperty]
    public CreateServerDto Input { get; set; } = new();
    public string? CreatedApiKey { get; set; }

    public void OnGet()
    {
        CreatedApiKey = TempData["CreatedApiKey"]?.ToString();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = await _service.CreateAsync(Input, User.Identity?.Name ?? "system");
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        TempData["Success"] = $"Server '{Input.Name}' registered. Copy the API key — it won't be shown again.";
        TempData["CreatedApiKey"] = result.Value.ApiKey;
        return RedirectToPage();
    }
}
