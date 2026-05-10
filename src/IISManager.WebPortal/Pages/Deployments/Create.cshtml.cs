using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using IISManager.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ApplicationEntity = IISManager.Domain.Entities.Application;

namespace IISManager.WebPortal.Pages.Deployments;

public class CreateDeploymentModel : PageModel
{
    private readonly IDeploymentAppService _deployments;
    private readonly IApplicationRepository _applications;
    private readonly IPackageAppService _packages;
    private readonly IServerAppService _servers;

    public CreateDeploymentModel(
        IDeploymentAppService deployments,
        IApplicationRepository applications,
        IPackageAppService packages,
        IServerAppService servers)
    {
        _deployments = deployments;
        _applications = applications;
        _packages = packages;
        _servers = servers;
    }

    [BindProperty]
    public CreateDeploymentDto Input { get; set; } = new() { Targets = [new()] };

    public IEnumerable<ApplicationEntity> Applications { get; set; } = [];
    public IEnumerable<ServerDto> Servers { get; set; } = [];

    public async Task OnGetAsync()
    {
        Applications = await _applications.GetAllActiveAsync();
        Servers = await _servers.GetAllAsync();
    }

    // AJAX: GET /Deployments/Create?handler=Packages&applicationId=X
    public async Task<IActionResult> OnGetPackagesAsync(int applicationId)
    {
        var packages = await _packages.GetByApplicationAsync(applicationId);
        return new JsonResult(packages.Select(p => new { p.Id, p.Version, p.FileName }));
    }

    // AJAX: GET /Deployments/Create?handler=Sites&serverId=X
    public async Task<IActionResult> OnGetSitesAsync(int serverId)
    {
        var sites = await _servers.GetWebsitesAsync(serverId);
        return new JsonResult(sites.Select(s => new
        {
            s.Name,
            s.PhysicalPath,
            appPool = s.DefaultAppPool
        }));
    }

    // AJAX: POST /Deployments/Create?handler=CreateApplication
    public async Task<IActionResult> OnPostCreateApplicationAsync(
        [FromForm] string name,
        [FromForm] string? description,
        [FromForm] string? defaultWebsiteName,
        [FromForm] string? defaultAppPoolName,
        [FromForm] string? physicalPath)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new JsonResult(new { error = "Name is required" }) { StatusCode = 400 };

        var app = new ApplicationEntity
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            DefaultWebsiteName = defaultWebsiteName?.Trim() ?? string.Empty,
            DefaultAppPoolName = defaultAppPoolName?.Trim() ?? string.Empty,
            PhysicalPath = physicalPath?.Trim() ?? string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "system"
        };

        var id = await _applications.InsertAsync(app);
        return new JsonResult(new
        {
            id,
            name = app.Name,
            site = app.DefaultWebsiteName,
            pool = app.DefaultAppPoolName,
            path = app.PhysicalPath
        });
    }

    // AJAX: POST /Deployments/Create?handler=UploadPackage
    public async Task<IActionResult> OnPostUploadPackageAsync(
        IFormFile? packageFile,
        [FromForm] string version,
        [FromForm] int applicationId)
    {
        if (packageFile is null || packageFile.Length == 0)
            return new JsonResult(new { error = "Package file is required" }) { StatusCode = 400 };
        if (string.IsNullOrWhiteSpace(version))
            return new JsonResult(new { error = "Version is required" }) { StatusCode = 400 };
        if (applicationId <= 0)
            return new JsonResult(new { error = "Select an application before uploading" }) { StatusCode = 400 };

        using var stream = packageFile.OpenReadStream();
        var result = await _packages.UploadAsync(
            applicationId, version.Trim(), stream, packageFile.FileName,
            User.Identity?.Name ?? "system");

        if (!result.IsSuccess)
            return new JsonResult(new { error = result.Error }) { StatusCode = 400 };

        return new JsonResult(new
        {
            id = result.Value.Id,
            version = result.Value.Version,
            fileName = result.Value.FileName
        });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.Targets.Count == 0)
            ModelState.AddModelError(string.Empty, "At least one deployment target is required.");

        if (!ModelState.IsValid)
        {
            Applications = await _applications.GetAllActiveAsync();
            Servers = await _servers.GetAllAsync();
            return Page();
        }

        var result = await _deployments.StartDeploymentAsync(Input, User.Identity?.Name ?? "system");
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            Applications = await _applications.GetAllActiveAsync();
            Servers = await _servers.GetAllAsync();
            return Page();
        }

        TempData["Success"] = $"Deployment #{result.Value} queued successfully.";
        return RedirectToPage("Console", new { id = result.Value });
    }
}
