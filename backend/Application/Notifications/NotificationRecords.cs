namespace Mems.Application.Notifications;

/// <summary>
/// One notification for one recipient. RecipientKey is either an approver's sign-in email
/// (lower-cased) or an employee id — resolved back to the signed-in user by the API.
/// </summary>
public sealed class NotificationRecord
{
    public Guid Id { get; set; }
    public string RecipientKey { get; set; } = "";
    public Guid ClaimId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// A browser's Web Push subscription for one signed-in approver. Endpoint/keys come from the
/// browser's PushManager; a dead endpoint (push service returns 404/410) is deleted on next send.
/// </summary>
public sealed class PushSubscriptionRecord
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
