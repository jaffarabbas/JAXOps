using Dapper;
using IISManager.Domain.Entities;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Data;

namespace IISManager.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IDatabaseFactory _db;
    public NotificationRepository(IDatabaseFactory db) => _db = db;

    public async Task<IEnumerable<Notification>> GetUnreadAsync(string? username)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Notification>("""
            SELECT TOP 20 * FROM Notifications
            WHERE IsRead = 0 AND (TargetUser IS NULL OR TargetUser = @Username)
            ORDER BY CreatedAt DESC
            """, new { Username = username });
    }

    public async Task<long> InsertAsync(Notification notification)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<long>("""
            INSERT INTO Notifications (Title, Message, Type, ActionUrl, TargetUser, IsRead, CreatedAt)
            VALUES (@Title, @Message, @Type, @ActionUrl, @TargetUser, 0, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """, notification);
    }

    public async Task MarkReadAsync(long id, string username)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Notifications SET IsRead = 1 WHERE Id = @Id AND (TargetUser IS NULL OR TargetUser = @Username)",
            new { Id = id, Username = username });
    }

    public async Task MarkAllReadAsync(string username)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Notifications SET IsRead = 1 WHERE IsRead = 0 AND (TargetUser IS NULL OR TargetUser = @Username)",
            new { Username = username });
    }
}
