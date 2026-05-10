using IISManager.Domain.Entities;
using IISManager.Domain.Enums;

namespace IISManager.Domain.Interfaces;

public interface IDeploymentRepository
{
    Task<Deployment?> GetByIdAsync(int id);
    Task<Deployment?> GetByCorrelationIdAsync(Guid correlationId);
    Task<IEnumerable<Deployment>> GetPagedAsync(int page, int pageSize, int? applicationId = null, DeploymentStatus? status = null);
    Task<int> CountAsync(int? applicationId = null, DeploymentStatus? status = null);
    Task<int> InsertAsync(Deployment deployment);
    Task UpdateStatusAsync(int id, DeploymentStatus status, DateTime? startedAt = null, DateTime? completedAt = null, string? failureReason = null);
    Task<IEnumerable<DeploymentTarget>> GetTargetsAsync(int deploymentId);
    Task InsertTargetAsync(DeploymentTarget target);
    Task UpdateTargetStatusAsync(int targetId, DeploymentStatus status, DateTime? startedAt = null, DateTime? completedAt = null, string? failureReason = null);
    Task<IEnumerable<DeploymentLog>> GetLogsAsync(int deploymentId, int? serverId = null);
    Task InsertLogAsync(DeploymentLog log);
    Task<Deployment?> GetLastSuccessfulAsync(int applicationId, int serverId);
}
