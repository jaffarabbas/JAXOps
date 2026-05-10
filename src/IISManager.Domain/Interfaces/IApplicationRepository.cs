using IISManager.Domain.Entities;

namespace IISManager.Domain.Interfaces;

public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(int id);
    Task<IEnumerable<Application>> GetAllActiveAsync();
    Task<int> InsertAsync(Application application);
    Task UpdateAsync(Application application);
    Task DeleteAsync(int id);
}
