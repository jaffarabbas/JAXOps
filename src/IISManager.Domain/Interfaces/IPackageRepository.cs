using IISManager.Domain.Entities;

namespace IISManager.Domain.Interfaces;

public interface IPackageRepository
{
    Task<DeploymentPackage?> GetByIdAsync(int id);
    Task<IEnumerable<DeploymentPackage>> GetByApplicationIdAsync(int applicationId);
    Task<int> InsertAsync(DeploymentPackage package);
    Task MarkDeletedAsync(int id);
}
