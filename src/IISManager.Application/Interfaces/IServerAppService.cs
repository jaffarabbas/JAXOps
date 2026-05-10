using IISManager.Application.DTOs;
using IISManager.Domain.Common;

namespace IISManager.Application.Interfaces;

public interface IServerAppService
{
    Task<IEnumerable<ServerDto>> GetAllAsync();
    Task<ServerDto?> GetByIdAsync(int id);
    Task<Result<(int Id, string ApiKey)>> CreateAsync(CreateServerDto dto, string createdBy);
    Task<Result> UpdateAsync(UpdateServerDto dto);
    Task<Result> DeleteAsync(int id);
    Task<Result<string>> RegenerateApiKeyAsync(int id);
    Task<IEnumerable<WebsiteDto>> GetWebsitesAsync(int serverId);
    Task<IEnumerable<AppPoolDto>> GetAppPoolsAsync(int serverId);
    Task<Result> SyncIISStateAsync(int serverId);
    Task<Result> StartWebsiteAsync(int serverId, string siteName);
    Task<Result> StopWebsiteAsync(int serverId, string siteName);
    Task<Result> StartAppPoolAsync(int serverId, string poolName);
    Task<Result> StopAppPoolAsync(int serverId, string poolName);
    Task<Result> RecycleAppPoolAsync(int serverId, string poolName);
    Task<Result> CreateWebsiteAsync(int serverId, CreateWebsiteDto dto);
    Task<Result> CreateAppPoolAsync(int serverId, CreateAppPoolDto dto);
}
