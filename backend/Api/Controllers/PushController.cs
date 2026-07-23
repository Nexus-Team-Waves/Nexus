using Microsoft.AspNetCore.Mvc;
using Mems.Application.Auth;
using Mems.Application.Notifications;

namespace Mems.Api.Controllers;

/// <summary>Web Push subscription management for approver notifications.</summary>
[ApiController]
[Route("api/push")]
public sealed class PushController(ICurrentUser current, INotificationRepository notifications, IPushSender push) : ControllerBase
{
    public sealed record SubscribeRequest(string Endpoint, string P256dh, string Auth);
    public sealed record UnsubscribeRequest(string Endpoint);

    /// <summary>The VAPID public key the browser needs to create a push subscription.</summary>
    [HttpGet("vapid-public-key")]
    public IActionResult VapidPublicKey() => Ok(new { publicKey = push.PublicKey });

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe(SubscribeRequest request)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Endpoint) || string.IsNullOrWhiteSpace(request.P256dh) || string.IsNullOrWhiteSpace(request.Auth))
            return BadRequest(new { detail = "A push subscription needs endpoint, p256dh, and auth." });

        await notifications.UpsertSubscriptionAsync(new PushSubscriptionRecord
        {
            Id = Guid.NewGuid(),
            Email = u.Email.ToLowerInvariant(),
            Endpoint = request.Endpoint,
            P256dh = request.P256dh,
            Auth = request.Auth,
            CreatedAt = DateTime.UtcNow,
        });
        await notifications.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpDelete("subscribe")]
    public async Task<IActionResult> Unsubscribe(UnsubscribeRequest request)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        await notifications.RemoveSubscriptionAsync(request.Endpoint);
        await notifications.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
