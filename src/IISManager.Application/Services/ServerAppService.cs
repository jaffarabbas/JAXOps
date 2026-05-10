using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using IISManager.Contracts.Commands;
using IISManager.Contracts.Enums;
using IISManager.Domain.Common;
using IISManager.Domain.Entities;
using IISManager.Domain.Enums;
using IISManager.Domain.Interfaces;
using IISManager.Shared.Extensions;
using Microsoft.Extensions.Logging;

namespace IISManager.Application.Services;

public class ServerAppService : IServerAppService
{
    private readonly IServerRepository _servers;
    private readonly IWebsiteRepository _websites;
    private readonly IApplicationPoolRepository _appPools;
    private readonly IAgentCommunicationService _agentComm;
    private readonly IAuditAppService _audit;
    private readonly ILogger<ServerAppService> _logger;

    public ServerAppService(
        IServerRepository servers,
        IWebsiteRepository websites,
        IApplicationPoolRepository appPools,
        IAgentCommunicationService agentComm,
        IAuditAppService audit,
        ILogger<ServerAppService> logger)
    {
        _servers = servers;
        _websites = websites;
        _appPools = appPools;
        _agentComm = agentComm;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IEnumerable<ServerDto>> GetAllAsync()
    {
        var servers = await _servers.GetAllActiveAsync();
        return servers.Select(MapToDto);
    }

    public async Task<ServerDto?> GetByIdAsync(int id)
    {
        var server = await _servers.GetByIdAsync(id);
        return server is null ? null : MapToDto(server);
    }

    public async Task<Result<(int Id, string ApiKey)>> CreateAsync(CreateServerDto dto, string createdBy)
    {
        var apiKey = Guid.NewGuid().ToString("N");
        var server = new Server
        {
            Name = dto.Name,
            Hostname = dto.Hostname,
            IpAddress = dto.IpAddress,
            Environment = dto.Environment,
            Group = dto.Group,
            Description = dto.Description,
            AgentApiKey = apiKey.ToSha256Hash(),
            Status = ServerStatus.Unknown,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        var id = await _servers.InsertAsync(server);
        await _audit.LogAsync(AuditAction.ServerRegistered, "Server", id.ToString(), $"Server '{dto.Name}' registered", createdBy);

        _logger.LogInformation("Server {ServerName} registered with ID {ServerId}", dto.Name, id);
        return Result<(int Id, string ApiKey)>.Ok((id, apiKey));
    }

    public async Task<Result> UpdateAsync(UpdateServerDto dto)
    {
        var server = await _servers.GetByIdAsync(dto.Id);
        if (server is null) return Result.Fail("Server not found");

        server.Name = dto.Name;
        server.Hostname = dto.Hostname;
        server.IpAddress = dto.IpAddress;
        server.Environment = dto.Environment;
        server.Group = dto.Group;
        server.Description = dto.Description;
        server.UpdatedAt = DateTime.UtcNow;

        await _servers.UpdateAsync(server);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var server = await _servers.GetByIdAsync(id);
        if (server is null) return Result.Fail("Server not found");
        await _servers.DeleteAsync(id);
        return Result.Ok();
    }

    public async Task<Result<string>> RegenerateApiKeyAsync(int id)
    {
        var server = await _servers.GetByIdAsync(id);
        if (server is null) return Result<string>.Fail("Server not found");

        var newKey = Guid.NewGuid().ToString("N");
        server.AgentApiKey = newKey.ToSha256Hash();
        server.UpdatedAt = DateTime.UtcNow;
        await _servers.UpdateAsync(server);
        return Result<string>.Ok(newKey);
    }

    public async Task<IEnumerable<WebsiteDto>> GetWebsitesAsync(int serverId)
    {
        var sites = await _websites.GetByServerIdAsync(serverId);
        return sites.Select(s => new WebsiteDto
        {
            Id = s.Id,
            ServerId = s.ServerId,
            IISId = s.IISId,
            Name = s.Name,
            PhysicalPath = s.PhysicalPath,
            Status = s.Status.ToString(),
            DefaultAppPool = s.DefaultAppPool,
            BindingsJson = s.BindingsJson,
            LastSyncedAt = s.LastSyncedAt
        });
    }

    public async Task<IEnumerable<AppPoolDto>> GetAppPoolsAsync(int serverId)
    {
        var pools = await _appPools.GetByServerIdAsync(serverId);
        return pools.Select(p => new AppPoolDto
        {
            Id = p.Id,
            ServerId = p.ServerId,
            Name = p.Name,
            Status = p.Status.ToString(),
            RuntimeVersion = p.RuntimeVersion,
            PipelineMode = p.PipelineMode,
            AutoStart = p.AutoStart,
            LastSyncedAt = p.LastSyncedAt
        });
    }

    public async Task<Result> SyncIISStateAsync(int serverId)
    {
        var cmd = new AppPoolCommand
        {
            CommandType = CommandType.SyncIISState,
            ServerId = serverId,
            AppPoolName = string.Empty
        };
        return await _agentComm.SendCommandAsync(serverId, cmd);
    }

    public async Task<Result> StartWebsiteAsync(int serverId, string siteName)
    {
        var cmd = new WebsiteCommand { CommandType = CommandType.StartWebsite, ServerId = serverId, WebsiteName = siteName };
        return await _agentComm.SendCommandAsync(serverId, cmd);
    }

    public async Task<Result> StopWebsiteAsync(int serverId, string siteName)
    {
        var cmd = new WebsiteCommand { CommandType = CommandType.StopWebsite, ServerId = serverId, WebsiteName = siteName };
        return await _agentComm.SendCommandAsync(serverId, cmd);
    }

    public async Task<Result> StartAppPoolAsync(int serverId, string poolName)
    {
        var cmd = new AppPoolCommand { CommandType = CommandType.StartApplicationPool, ServerId = serverId, AppPoolName = poolName };
        return await _agentComm.SendCommandAsync(serverId, cmd);
    }

    public async Task<Result> StopAppPoolAsync(int serverId, string poolName)
    {
        var cmd = new AppPoolCommand { CommandType = CommandType.StopApplicationPool, ServerId = serverId, AppPoolName = poolName };
        return await _agentComm.SendCommandAsync(serverId, cmd);
    }

    public async Task<Result> RecycleAppPoolAsync(int serverId, string poolName)
    {
        var cmd = new AppPoolCommand { CommandType = CommandType.RecycleApplicationPool, ServerId = serverId, AppPoolName = poolName };
        return await _agentComm.SendCommandAsync(serverId, cmd);
    }

    public async Task<Result> CreateWebsiteAsync(int serverId, CreateWebsiteDto dto)
    {
        var cmd = new WebsiteCommand
        {
            CommandType = CommandType.CreateWebsite,
            ServerId = serverId,
            WebsiteName = dto.Name,
            PhysicalPath = dto.PhysicalPath,
            AppPoolName = dto.AppPoolName,
            Bindings = new List<BindingInfo>
            {
                new() { Protocol = dto.Protocol, IpAddress = dto.IpAddress, Port = dto.Port, HostName = dto.HostName }
            }
        };
        return await _agentComm.SendCommandAsync(serverId, cmd);
    }

    public async Task<Result> CreateAppPoolAsync(int serverId, CreateAppPoolDto dto)
    {
        var cmd = new AppPoolCommand
        {
            CommandType = CommandType.CreateApplicationPool,
            ServerId = serverId,
            AppPoolName = dto.Name,
            RuntimeVersion = dto.RuntimeVersion,
            PipelineMode = dto.PipelineMode
        };
        return await _agentComm.SendCommandAsync(serverId, cmd);
    }

    private static ServerDto MapToDto(Server s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Hostname = s.Hostname,
        IpAddress = s.IpAddress,
        Environment = s.Environment,
        Group = s.Group,
        Description = s.Description,
        Status = s.Status,
        LastHeartbeat = s.LastHeartbeat,
        AgentVersion = s.AgentVersion,
        CreatedAt = s.CreatedAt,
        CreatedBy = s.CreatedBy
    };
}
