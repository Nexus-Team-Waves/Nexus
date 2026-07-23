using Microsoft.EntityFrameworkCore;
using Mems.Application.Notifications;

namespace Mems.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="INotificationRepository"/>.</summary>
public sealed class NotificationRepository(MemsDbContext db) : INotificationRepository
{
    public async Task AddAsync(NotificationRecord notification, CancellationToken ct = default)
        => await db.Notifications.AddAsync(notification, ct);

    public async Task<IReadOnlyList<NotificationRecord>> GetForRecipientAsync(
        string recipientKey, int limit = 50, CancellationToken ct = default)
        => await db.Notifications
            .Where(n => n.RecipientKey == recipientKey)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task MarkAllReadAsync(string recipientKey, CancellationToken ct = default)
    {
        var unread = await db.Notifications
            .Where(n => n.RecipientKey == recipientKey && n.ReadAt == null)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var notification in unread) notification.ReadAt = now;
    }

    public async Task UpsertSubscriptionAsync(PushSubscriptionRecord subscription, CancellationToken ct = default)
    {
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == subscription.Endpoint, ct);
        if (existing is null)
        {
            await db.PushSubscriptions.AddAsync(subscription, ct);
            return;
        }
        // Same browser endpoint, possibly a different signed-in user or refreshed keys.
        existing.Email = subscription.Email;
        existing.P256dh = subscription.P256dh;
        existing.Auth = subscription.Auth;
    }

    public async Task RemoveSubscriptionAsync(string endpoint, CancellationToken ct = default)
    {
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (existing is not null) db.PushSubscriptions.Remove(existing);
    }

    public async Task<IReadOnlyList<PushSubscriptionRecord>> GetSubscriptionsAsync(string email, CancellationToken ct = default)
        => await db.PushSubscriptions.Where(s => s.Email == email).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
