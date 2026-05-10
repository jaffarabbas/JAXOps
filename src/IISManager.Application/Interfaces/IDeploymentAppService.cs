using IISManager.Application.DTOs;
using IISManager.Domain.Common;
using IISManager.Domain.Enums;
using IISManager.Shared.Models;

namespace IISManager.Application.Interfaces;

public interface IDeploymentAppService
{
    Task<PagedResult<DeploymentDto>> GetPagedAsync(int page, int pageSize, int? applicationId = null, DeploymentStatus? status = null);
    Task<DeploymentDto?> GetByIdAsync(int id);
    Task<IEnumerable<DeploymentLogDto>> GetLogsAsync(int deploymentId);
    Task<Result<int>> StartDeploymentAsync(CreateDeploymentDto dto, string initiatedBy);
    Task<Result<int>> StartRollbackAsync(int deploymentId, string initiatedBy);
    Task<Result> CancelDeploymentAsync(int id, string cancelledBy);
}
