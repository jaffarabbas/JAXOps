using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IISManager.WebPortal.Pages.Servers;

public class ServerDetailsModel : PageModel
{
    private readonly IServerAppService _service;
    public ServerDetailsModel(IServerAppService service) => _service = service;

    public ServerDto Server { get; set; } = null!;
    public IEnumerable<WebsiteDto> Websites { get; set; } = [];
    public IEnumerable<AppPoolDto> AppPools { get; set; } = [];
    public string? NewApiKey { get; set; }

    [BindProperty] public CreateSiteInput NewSite { get; set; } = new();
    [BindProperty] public CreatePoolInput NewPool { get; set; } = new();

    public class CreateSiteInput
    {
        public string Name { get; set; } = string.Empty;
        public string PhysicalPath { get; set; } = string.Empty;
        public string AppPoolName { get; set; } = string.Empty;
        public string Protocol { get; set; } = "http";
        public int Port { get; set; } = 80;
        public string? HostName { get; set; }
    }

    public class CreatePoolInput
    {
        public string Name { get; set; } = string.Empty;
        public string RuntimeVersion { get; set; } = "v4.0";
        public string PipelineMode { get; set; } = "Integrated";
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var server = await _service.GetByIdAsync(id);
        if (server is null) return NotFound();
        Server = server;
        NewApiKey = TempData["NewApiKey"]?.ToString();
        var wsTask = _service.GetWebsitesAsync(id);
        var apTask = _service.GetAppPoolsAsync(id);
        await Task.WhenAll(wsTask, apTask);
        Websites = wsTask.Result;
        AppPools = apTask.Result;
        return Page();
    }

    public async Task<IActionResult> OnPostRegenerateKeyAsync(int id)
    {
        var result = await _service.RegenerateApiKeyAsync(id);
        if (result.IsSuccess)
        {
            TempData["NewApiKey"] = result.Value;
            TempData["Success"] = "API key regenerated. Copy it now — it won't be shown again.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSyncAsync(int id)
    {
        var result = await _service.SyncIISStateAsync(id);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "IIS sync requested — data will update shortly." : result.Error;
        return RedirectToPage(new { id });
    }

    // ── Website actions ───────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostStartSiteAsync(int id, string siteName)
    {
        var result = await _service.StartWebsiteAsync(id, siteName);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Start command sent to '{siteName}'." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStopSiteAsync(int id, string siteName)
    {
        var result = await _service.StopWebsiteAsync(id, siteName);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Stop command sent to '{siteName}'." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRestartSiteAsync(int id, string siteName)
    {
        var result = await _service.RestartWebsiteAsync(id, siteName);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Restart command sent to '{siteName}'." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteSiteAsync(int id, string siteName)
    {
        var result = await _service.DeleteWebsiteAsync(id, siteName);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Delete command sent for '{siteName}'." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreateSiteAsync(int id)
    {
        if (string.IsNullOrWhiteSpace(NewSite.Name) || string.IsNullOrWhiteSpace(NewSite.PhysicalPath))
        {
            TempData["Error"] = "Site name and physical path are required.";
            return RedirectToPage(new { id });
        }

        var result = await _service.CreateWebsiteAsync(id, new CreateWebsiteDto
        {
            Name = NewSite.Name.Trim(),
            PhysicalPath = NewSite.PhysicalPath.Trim(),
            AppPoolName = NewSite.AppPoolName?.Trim() ?? string.Empty,
            Protocol = NewSite.Protocol ?? "http",
            Port = NewSite.Port,
            HostName = string.IsNullOrWhiteSpace(NewSite.HostName) ? null : NewSite.HostName.Trim()
        });

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Create site '{NewSite.Name}' command sent." : result.Error;
        return RedirectToPage(new { id });
    }

    // ── App pool actions ──────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostStartPoolAsync(int id, string poolName)
    {
        var result = await _service.StartAppPoolAsync(id, poolName);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Start command sent to pool '{poolName}'." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStopPoolAsync(int id, string poolName)
    {
        var result = await _service.StopAppPoolAsync(id, poolName);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Stop command sent to pool '{poolName}'." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRecyclePoolAsync(int id, string poolName)
    {
        var result = await _service.RecycleAppPoolAsync(id, poolName);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Recycle command sent to pool '{poolName}'." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeletePoolAsync(int id, string poolName)
    {
        var result = await _service.DeleteAppPoolAsync(id, poolName);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Delete command sent for pool '{poolName}'." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreatePoolAsync(int id)
    {
        if (string.IsNullOrWhiteSpace(NewPool.Name))
        {
            TempData["Error"] = "Pool name is required.";
            return RedirectToPage(new { id });
        }

        var result = await _service.CreateAppPoolAsync(id, new CreateAppPoolDto
        {
            Name = NewPool.Name.Trim(),
            RuntimeVersion = NewPool.RuntimeVersion ?? "v4.0",
            PipelineMode = NewPool.PipelineMode ?? "Integrated"
        });

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Create pool '{NewPool.Name}' command sent." : result.Error;
        return RedirectToPage(new { id });
    }
}
