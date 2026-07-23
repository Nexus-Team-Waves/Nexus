using Microsoft.AspNetCore.Mvc;
using Mems.Application.Auth;
using Mems.Application.Notifications;
using Mems.Application.Workflow;

namespace Mems.Api.Controllers;

/// <summary>The signed-in user's in-app notification feed (bell + toasts).</summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(ICurrentUser current, INotificationRepository notifications) : ControllerBase
{
    public sealed record NotificationDto(Guid Id, Guid ClaimId, string Title, string Body, DateTime CreatedAt, bool Read);

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var items = await notifications.GetForRecipientAsync(RecipientKey(u));
        return Ok(items.Select(n => new NotificationDto(n.Id, n.ClaimId, n.Title, n.Body, n.CreatedAt, n.ReadAt is not null)));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        await notifications.MarkAllReadAsync(RecipientKey(u));
        await notifications.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    /// <summary>Employees are addressed by employee id; approvers by sign-in email (lower-case).</summary>
    private static string RecipientKey(AuthUser u)
        => u.Role == UserRole.Employee && u.EmployeeId is not null
            ? u.EmployeeId.ToLowerInvariant()
            : u.Email.ToLowerInvariant();
}
