namespace Mems.Application.Notifications;

/// <summary>
/// Web Push transport port — implemented in Infrastructure (WebPush/VAPID) so the Application
/// layer carries no push dependency.
/// </summary>
public interface IPushSender
{
    /// <summary>The VAPID public key the browser needs to subscribe.</summary>
    string PublicKey { get; }

    /// <summary>
    /// Try to deliver. Returns false when the subscription is dead (push service says 404/410)
    /// so the caller can delete it; transport errors are logged and swallowed (returns true).
    /// </summary>
    Task<bool> TrySendAsync(PushSubscriptionRecord subscription, string title, string body, Guid claimId, CancellationToken ct = default);
}
