using Mems.Application.Workflow;

namespace Mems.Application.Notifications;

/// <summary>
/// Emits stage-wise notifications as the claim moves: to the approver whose queue it enters
/// (in-app + Web Push, so they hear even with the browser closed), and to the owning employee
/// on return/completion/posting (in-app). Implementations must never throw into the workflow —
/// a notification failure must not fail the claim action.
/// </summary>
public interface IClaimNotifier
{
    /// <summary>The claim just entered this approval stage — tell that stage's approver.</summary>
    Task ClaimAwaitingStageAsync(ClaimRecord claim, ApprovalStage stage, CancellationToken ct = default);

    /// <summary>The claim was returned to the employee (a line was rejected before Top-Level).</summary>
    Task ClaimReturnedAsync(ClaimRecord claim, CancellationToken ct = default);

    /// <summary>The Top-Level Approver rejected every item — the claim is closed for good.</summary>
    Task ClaimClosedAsync(ClaimRecord claim, CancellationToken ct = default);

    /// <summary>Fully approved — tell the employee, and the routed Finance authority to post it.</summary>
    Task ClaimCompletedAsync(ClaimRecord claim, CancellationToken ct = default);

    /// <summary>Finance recorded the SAP posting — tell the employee.</summary>
    Task ClaimPostedAsync(ClaimRecord claim, CancellationToken ct = default);
}
