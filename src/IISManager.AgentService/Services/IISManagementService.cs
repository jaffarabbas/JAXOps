using IISManager.Contracts.Commands;
using IISManager.Contracts.Events;
using IISManager.Domain.Common;
using Microsoft.Web.Administration;

namespace IISManager.AgentService.Services;

public class IISManagementService
{
    private readonly ILogger<IISManagementService> _logger;

    public IISManagementService(ILogger<IISManagementService> logger) => _logger = logger;

    public IISStateSnapshotEvent GetSnapshot()
    {
        try
        {
            using var mgr = new ServerManager();
            var sites = mgr.Sites.Select(s =>
            {
                var rootApp = s.Applications.FirstOrDefault(a => a.Path == "/");
                var rootVdir = rootApp?.VirtualDirectories.FirstOrDefault(v => v.Path == "/");
                return new SiteInfo
                {
                    IISId = s.Id,
                    Name = s.Name,
                    PhysicalPath = rootVdir?.PhysicalPath ?? string.Empty,
                    Status = s.State.ToString(),
                    DefaultAppPool = rootApp?.ApplicationPoolName ?? string.Empty,
                    Bindings = s.Bindings.Select(b => new BindingData
                    {
                        Protocol = b.Protocol,
                        BindingInformation = b.BindingInformation,
                        HostName = b.Host
                    }).ToList()
                };
            }).ToList();

            var pools = mgr.ApplicationPools.Select(p => new AppPoolInfo
            {
                Name = p.Name,
                Status = p.State.ToString(),
                RuntimeVersion = p.ManagedRuntimeVersion,
                PipelineMode = p.ManagedPipelineMode.ToString(),
                AutoStart = p.AutoStart
            }).ToList();

            return new IISStateSnapshotEvent { Sites = sites, AppPools = pools };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get IIS snapshot");
            return new IISStateSnapshotEvent();
        }
    }

    public Result CreateSite(WebsiteCommand cmd)
    {
        try
        {
            using var mgr = new ServerManager();
            if (mgr.Sites[cmd.WebsiteName] != null)
                return Result.Fail($"Site '{cmd.WebsiteName}' already exists");

            var first = cmd.Bindings?.FirstOrDefault();
            var protocol = first?.Protocol ?? "http";
            var bindingInfo = first != null
                ? $"{first.IpAddress}:{first.Port}:{first.HostName ?? ""}"
                : "*:80:";
            var physPath = cmd.PhysicalPath ?? @"C:\inetpub\wwwroot";

            var site = mgr.Sites.Add(cmd.WebsiteName, protocol, bindingInfo, physPath);

            if (!string.IsNullOrEmpty(cmd.AppPoolName))
                site.Applications["/"].ApplicationPoolName = cmd.AppPoolName;

            if (cmd.Bindings?.Count > 1)
            {
                foreach (var b in cmd.Bindings.Skip(1))
                    site.Bindings.Add($"{b.IpAddress}:{b.Port}:{b.HostName ?? ""}", b.Protocol);
            }

            mgr.CommitChanges();
            _logger.LogInformation("Site {SiteName} created", cmd.WebsiteName);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create site {SiteName}", cmd.WebsiteName);
            return Result.Fail(ex.Message);
        }
    }

    public Result DeleteSite(string siteName)
    {
        try
        {
            using var mgr = new ServerManager();
            var site = mgr.Sites[siteName];
            if (site is null) return Result.Fail($"Site '{siteName}' not found");
            mgr.Sites.Remove(site);
            mgr.CommitChanges();
            _logger.LogInformation("Site {SiteName} deleted", siteName);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete site {SiteName}", siteName);
            return Result.Fail(ex.Message);
        }
    }

    public Result CreateAppPool(AppPoolCommand cmd)
    {
        try
        {
            using var mgr = new ServerManager();
            if (mgr.ApplicationPools[cmd.AppPoolName] != null)
                return Result.Fail($"App pool '{cmd.AppPoolName}' already exists");

            var pool = mgr.ApplicationPools.Add(cmd.AppPoolName);
            pool.ManagedRuntimeVersion = cmd.RuntimeVersion ?? "v4.0";
            pool.ManagedPipelineMode = cmd.PipelineMode == "Classic"
                ? ManagedPipelineMode.Classic
                : ManagedPipelineMode.Integrated;
            mgr.CommitChanges();
            _logger.LogInformation("App pool {PoolName} created", cmd.AppPoolName);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create app pool {PoolName}", cmd.AppPoolName);
            return Result.Fail(ex.Message);
        }
    }

    public Result DeleteAppPool(string poolName)
    {
        try
        {
            using var mgr = new ServerManager();
            var pool = mgr.ApplicationPools[poolName];
            if (pool is null) return Result.Fail($"App pool '{poolName}' not found");
            mgr.ApplicationPools.Remove(pool);
            mgr.CommitChanges();
            _logger.LogInformation("App pool {PoolName} deleted", poolName);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete app pool {PoolName}", poolName);
            return Result.Fail(ex.Message);
        }
    }

    public Result<bool> StopSite(string siteName)
    {
        try
        {
            using var mgr = new ServerManager();
            var site = mgr.Sites[siteName];
            if (site is null) return Result<bool>.Fail($"Site '{siteName}' not found");
            if (site.State == ObjectState.Stopped) return Result<bool>.Ok(false);
            site.Stop();
            mgr.CommitChanges();
            _logger.LogInformation("Site {SiteName} stopped", siteName);
            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop site {SiteName}", siteName);
            return Result<bool>.Fail(ex.Message);
        }
    }

    public Result<bool> StartSite(string siteName)
    {
        try
        {
            using var mgr = new ServerManager();
            var site = mgr.Sites[siteName];
            if (site is null) return Result<bool>.Fail($"Site '{siteName}' not found");
            if (site.State == ObjectState.Started) return Result<bool>.Ok(false);
            site.Start();
            mgr.CommitChanges();
            _logger.LogInformation("Site {SiteName} started", siteName);
            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start site {SiteName}", siteName);
            return Result<bool>.Fail(ex.Message);
        }
    }

    public Result StopAppPool(string poolName)
    {
        try
        {
            using var mgr = new ServerManager();
            var pool = mgr.ApplicationPools[poolName];
            if (pool is null) return Result.Fail($"App pool '{poolName}' not found");
            if (pool.State == ObjectState.Stopped) return Result.Ok();
            pool.Stop();
            mgr.CommitChanges();
            _logger.LogInformation("App pool {PoolName} stopped", poolName);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop app pool {PoolName}", poolName);
            return Result.Fail(ex.Message);
        }
    }

    public Result StartAppPool(string poolName)
    {
        try
        {
            using var mgr = new ServerManager();
            var pool = mgr.ApplicationPools[poolName];
            if (pool is null) return Result.Fail($"App pool '{poolName}' not found");
            if (pool.State == ObjectState.Started) return Result.Ok();
            pool.Start();
            mgr.CommitChanges();
            _logger.LogInformation("App pool {PoolName} started", poolName);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start app pool {PoolName}", poolName);
            return Result.Fail(ex.Message);
        }
    }

    public Result RecycleAppPool(string poolName)
    {
        try
        {
            using var mgr = new ServerManager();
            var pool = mgr.ApplicationPools[poolName];
            if (pool is null) return Result.Fail($"App pool '{poolName}' not found");
            pool.Recycle();
            _logger.LogInformation("App pool {PoolName} recycled", poolName);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recycle app pool {PoolName}", poolName);
            return Result.Fail(ex.Message);
        }
    }

    public bool IsIISRunning()
    {
        try
        {
            using var mgr = new ServerManager();
            _ = mgr.Sites.Count;
            return true;
        }
        catch { return false; }
    }

    public (int runningSites, int runningPools) GetIISCounts()
    {
        try
        {
            using var mgr = new ServerManager();
            var sites = mgr.Sites.Count(s => s.State == ObjectState.Started);
            var pools = mgr.ApplicationPools.Count(p => p.State == ObjectState.Started);
            return (sites, pools);
        }
        catch { return (0, 0); }
    }
}
