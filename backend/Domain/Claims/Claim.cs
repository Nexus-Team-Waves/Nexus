using Mems.Domain.Common;

namespace Mems.Domain.Claims;

/// <summary>
/// A medical claim: a header for one employee with one or more <see cref="ClaimLine"/>s
/// (docs/entitlement-rules.md § Claim structure). The overall status and the claimed/approved
/// totals are <em>derived</em> from the lines — they are computed here, never stored as
/// independent, drift-prone fields.
/// </summary>
public sealed class Claim
{
    private readonly List<ClaimLine> _lines;

    /// <summary>
    /// Client-generated UUID. Doubles as the idempotency key for offline sync so a claim
    /// captured offline and retried is not posted twice (docs/CLAUDE.md §9).
    /// </summary>
    public Guid Id { get; }

    public string EmployeeId { get; }
    public DateOnly SubmissionDate { get; }

    public IReadOnlyList<ClaimLine> Lines => _lines;

    public Claim(Guid id, string employeeId, DateOnly submissionDate, IEnumerable<ClaimLine> lines)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Claim needs a non-empty id (client UUID).", nameof(id));
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Claim needs an employee id.", nameof(employeeId));

        _lines = lines?.ToList() ?? throw new ArgumentNullException(nameof(lines));
        if (_lines.Count == 0)
            throw new ArgumentException("A claim must have at least one line.", nameof(lines));

        Id = id;
        EmployeeId = employeeId;
        SubmissionDate = submissionDate;
    }

    /// <summary>Sum of every line's claimed amount.</summary>
    public Money TotalClaimed
        => _lines.Aggregate(Money.Zero, (running, line) => running + line.ClaimedAmount);

    /// <summary>
    /// Sum of every line's approved amount. Lines still Pending contribute nothing (their
    /// approved amount is null) — this is NOT the same as "the claim is fully decided".
    /// </summary>
    public Money TotalApproved
        => _lines.Aggregate(Money.Zero, (running, line) => running + (line.ApprovedAmount ?? Money.Zero));

    /// <summary>
    /// The derived overall status. Precedence matters and follows the policy exactly
    /// (docs/entitlement-rules.md § Claim structure — Overall claim status):
    /// <list type="number">
    ///   <item>any Rejected line → <see cref="ClaimStatus.ActionNeeded"/>;</item>
    ///   <item>otherwise all lines fully Approved → <see cref="ClaimStatus.Approved"/>;</item>
    ///   <item>otherwise any Pending line → <see cref="ClaimStatus.InReview"/>;</item>
    ///   <item>otherwise (mix of full/partial approvals, none pending, none rejected)
    ///         → <see cref="ClaimStatus.PartiallyApproved"/>.</item>
    /// </list>
    /// </summary>
    public ClaimStatus OverallStatus
    {
        get
        {
            if (_lines.Any(l => l.Status == ClaimLineStatus.Rejected))
                return ClaimStatus.ActionNeeded;
            if (_lines.All(l => l.Status == ClaimLineStatus.Approved))
                return ClaimStatus.Approved;
            if (_lines.Any(l => l.Status == ClaimLineStatus.Pending))
                return ClaimStatus.InReview;
            return ClaimStatus.PartiallyApproved;
        }
    }

    /// <summary>
    /// Whether the employee may still edit this claim (docs/entitlement-rules.md § Editing rules).
    /// Editable when it has <b>no approvals yet</b> (still entirely Pending) OR when <b>any line
    /// has been rejected</b> — a rejection re-opens the whole claim for edit-and-resubmit even if
    /// other lines were already approved, because resubmission restarts the entire hierarchy at
    /// the Line Manager (including the already-approved lines). The claim locks only once a line
    /// has been approved and <b>no</b> line is rejected.
    /// </summary>
    public bool CanBeEditedByEmployee
    {
        get
        {
            var hasRejection = _lines.Any(l => l.Status == ClaimLineStatus.Rejected);
            var hasApproval = _lines.Any(l =>
                l.Status is ClaimLineStatus.Approved or ClaimLineStatus.PartiallyApproved);

            return hasRejection || !hasApproval;
        }
    }
}
