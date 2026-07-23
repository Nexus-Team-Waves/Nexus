using Microsoft.Extensions.Logging;
using Mems.Application.Auth;
using Mems.Application.Workflow;

namespace Mems.Application.Notifications;

/// <summary>
/// Writes stage-wise notifications and pushes them to approvers. Recipients: an approval stage
/// maps to its approver's sign-in email (Finance via grade routing); the employee is addressed
/// by employee id. Only email recipients (approvers) get Web Push — employees see in-app only.
///
/// Notification bodies carry ids and amounts, never medical detail (CLAUDE.md §10).
/// NOTE: notifications are written after the claim's own save (a separate transaction) — a crash
/// in between loses a notification, never claim state. Acceptable for this demo stage.
/// </summary>
public sealed class NotificationService(
    INotificationRepository repo,
    IPushSender push,
    FinanceRoutingService financeRouting,
    ILogger<NotificationService> logger) : IClaimNotifier
{
    public Task ClaimAwaitingStageAsync(ClaimRecord claim, ApprovalStage stage, CancellationToken ct = default)
        => Guarded(async () =>
        {
            var email = await ApproverEmailForStageAsync(claim, stage, ct);
            if (email is null) return;
            await NotifyAsync(email, claim,
                "Claim awaiting your review",
                $"Claim {Short(claim.Id)} from {claim.EmployeeId} (Rs {claim.Lines.Sum(l => l.CurrentAmount):N0}) is awaiting {ApprovalStagePolicy.StageLabel(stage)}.",
                ct);
        });

    public Task ClaimReturnedAsync(ClaimRecord claim, CancellationToken ct = default)
        => Guarded(() => NotifyAsync(claim.EmployeeId, claim,
            "Claim returned — action needed",
            $"Claim {Short(claim.Id)} was returned. A line was rejected — open the claim to see the reason, fix it, and resubmit.",
            ct));

    public Task ClaimClosedAsync(ClaimRecord claim, CancellationToken ct = default)
        => Guarded(() => NotifyAsync(claim.EmployeeId, claim,
            "Claim closed",
            $"Claim {Short(claim.Id)} was closed by the Top-Level Approver — all items were rejected (final). Nothing will be posted.",
            ct));

    public Task ClaimCompletedAsync(ClaimRecord claim, CancellationToken ct = default)
        => Guarded(async () =>
        {
            var rejectedCount = claim.Lines.Count(l => l.Status == LineDecisionStatus.Rejected);
            await NotifyAsync(claim.EmployeeId, claim,
                rejectedCount > 0 ? "Claim decided" : "Claim approved",
                $"Claim {Short(claim.Id)} is approved for Rs {claim.Lines.Sum(l => l.ApprovedAmount ?? 0m):N0}."
                + (rejectedCount > 0 ? $" {rejectedCount} item(s) were rejected — that decision is final." : "")
                + " Finance will record the SAP posting.",
                ct);
            var financeEmail = await financeRouting.RequiredFinanceEmailAsync(claim.EmployeeId, ct);
            await NotifyAsync(financeEmail, claim,
                "Claim ready to post",
                $"Claim {Short(claim.Id)} from {claim.EmployeeId} is approved and awaiting its SAP posting reference.",
                ct);
        });

    public Task ClaimPostedAsync(ClaimRecord claim, CancellationToken ct = default)
        => Guarded(() => NotifyAsync(claim.EmployeeId, claim,
            "Claim posted to SAP",
            $"Claim {Short(claim.Id)} was posted to SAP (ref {claim.SapReference}).",
            ct));

    // ---- internals ----

    private async Task<string?> ApproverEmailForStageAsync(ClaimRecord claim, ApprovalStage stage, CancellationToken ct)
    {
        if (stage == ApprovalStage.Finance)
            return await financeRouting.RequiredFinanceEmailAsync(claim.EmployeeId, ct);

        var role = ApprovalStagePolicy.RoleForStage(stage);
        if (role is null) return null;
        return DemoDirectory.UserForRole(role.Value)?.Email;
    }

    private async Task NotifyAsync(string recipientKey, ClaimRecord claim, string title, string body, CancellationToken ct)
    {
        var key = recipientKey.ToLowerInvariant();
        await repo.AddAsync(new NotificationRecord
        {
            Id = Guid.NewGuid(),
            RecipientKey = key,
            ClaimId = claim.Id,
            Title = title,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        }, ct);
        await repo.SaveChangesAsync(ct);

        // Approvers (email recipients) also get Web Push, so they hear even with the browser
        // closed. Employees are keyed by employee id → in-app only.
        if (!key.Contains('@')) return;
        var subscriptions = await repo.GetSubscriptionsAsync(key, ct);
        var removedAny = false;
        foreach (var subscription in subscriptions)
        {
            var alive = await push.TrySendAsync(subscription, title, body, claim.Id, ct);
            if (alive) continue;
            await repo.RemoveSubscriptionAsync(subscription.Endpoint, ct);
            removedAny = true;
            logger.LogInformation("Pruned dead push subscription {SubscriptionId}", subscription.Id);
        }
        if (removedAny) await repo.SaveChangesAsync(ct);
    }

    /// <summary>A notification failure must never fail the claim action that triggered it.</summary>
    private async Task Guarded(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to emit claim notification");
        }
    }

    private static string Short(Guid id) => id.ToString("N")[..8];
}
