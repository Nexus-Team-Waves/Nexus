using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mems.Application.Notifications;
using WebPush;

namespace Mems.Infrastructure.Push;

/// <summary>
/// Web Push delivery via the WebPush library (VAPID). Payload is a small JSON the service
/// worker's 'push' handler turns into a system notification. Errors never propagate: a dead
/// subscription reports false (caller deletes it); anything else is logged and swallowed.
/// </summary>
public sealed class WebPushSender(VapidKeys keys, ILogger<WebPushSender> logger) : IPushSender
{
    private readonly VapidDetails _vapid = new(keys.Subject, keys.PublicKey, keys.PrivateKey);

    public string PublicKey => keys.PublicKey;

    public async Task<bool> TrySendAsync(
        PushSubscriptionRecord subscription, string title, string body, Guid claimId, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { title, body, claimId });
        using var client = new WebPushClient();
        try
        {
            await client.SendNotificationAsync(
                new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth),
                payload, _vapid, ct);
            return true;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return false; // browser unsubscribed / endpoint expired — caller prunes it
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Web Push delivery failed for subscription {SubscriptionId}", subscription.Id);
            return true; // transient — keep the subscription
        }
    }
}
