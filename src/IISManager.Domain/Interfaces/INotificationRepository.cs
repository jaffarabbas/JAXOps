using IISManager.Domain.Entities;

namespace IISManager.Domain.Interfaces;

public interface INotificationRepository
{
    Task<IEnumerable<Notification>> GetUnreadAsync(string? username);
    Task<long> InsertAsync(Notification notification);
    Task MarkReadAsync(long id, string username);
    Task MarkAllReadAsync(string username);
}
