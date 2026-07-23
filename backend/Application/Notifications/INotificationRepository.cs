namespace Mems.Application.Notifications;

/// <summary>Persistence port for notifications and Web Push subscriptions.</summary>
public interface INotificationRepository
{
    Task AddAsync(NotificationRecord notification, CancellationToken ct = default);

    /// <summary>Newest first, capped (the bell shows recent activity, not an archive).</summary>
    Task<IReadOnlyList<NotificationRecord>> GetForRecipientAsync(string recipientKey, int limit = 50, CancellationToken ct = default);

    Task MarkAllReadAsync(string recipientKey, CancellationToken ct = default);

    /// <summary>Insert or update by Endpoint (a browser re-subscribing must not duplicate).</summary>
    Task UpsertSubscriptionAsync(PushSubscriptionRecord subscription, CancellationToken ct = default);

    Task RemoveSubscriptionAsync(string endpoint, CancellationToken ct = default);

    Task<IReadOnlyList<PushSubscriptionRecord>> GetSubscriptionsAsync(string email, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
