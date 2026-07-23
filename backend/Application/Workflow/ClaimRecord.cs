namespace Mems.Application.Workflow;

/// <summary>
/// Persisted claim (the demo workflow's write model). Mutable POCO so EF Core can materialise and
/// track it. Money is <c>decimal</c> to mirror the DB's DECIMAL(18,4) (CLAUDE.md §8).
/// </summary>
public sealed class ClaimRecord
{
    public Guid Id { get; set; } // client-generated UUID = idempotency key
    public string EmployeeId { get; set; } = "";
    public DateOnly SubmissionDate { get; set; }
    public ApprovalStage Stage { get; set; }
    public bool Posted { get; set; }
    public string? SapReference { get; set; }

    /// <summary>
    /// Admin/HR forwarded this claim directly to the Top-Level Approver, skipping Finance review
    /// (Finance still posts). Re-assigned on every advancing Admin/HR decision so a stale
    /// escalation never survives a return-and-resubmit round.
    /// </summary>
    public bool EscalatedToTopLevel { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ClaimLineRecord> Lines { get; set; } = new();
    public List<ApprovalEvent> History { get; set; } = new();
    public List<ClaimLineEventRecord> LineEvents { get; set; } = new();
}

/// <summary>One line of a claim, carrying its amount as it moves through the stages.</summary>
public sealed class ClaimLineRecord
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }

    public string BeneficiaryKind { get; set; } = "Self"; // "Self" | "Dependant"
    public string? DependantId { get; set; }
    public string? DependantRelationship { get; set; }
    public string Category { get; set; } = "Opd";
    public DateOnly ExpenseDate { get; set; }
    /// <summary>Display string for the attached receipts (joined original filenames).</summary>
    public string? ReceiptReference { get; set; }

    /// <summary>
    /// The stored receipt images (Receipts table), 1–5 per line. Plain Guids, not navigation
    /// properties, so claim/queue queries never pull image bytes. Empty on claims from before
    /// image upload existed.
    /// </summary>
    public List<Guid> ReceiptIds { get; set; } = new();

    /// <summary>What the employee originally claimed. Never changes.</summary>
    public decimal ClaimedAmount { get; set; }

    /// <summary>The amount in play at the current stage (starts at claimed; a reduction carries forward).</summary>
    public decimal CurrentAmount { get; set; }

    /// <summary>The current stage's decided amount; null while Pending at this stage.</summary>
    public decimal? ApprovedAmount { get; set; }

    public LineDecisionStatus Status { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Append-only per-LINE audit entry: what happened to one claim line at one stage — submitted,
/// resubmitted, approved, reduced (with the amount), or rejected (with the reason) — plus the
/// optional free-text comment the actor attached. This is what lets the employee and every
/// approver see line-wise changes and comments through the whole chain.
/// </summary>
public sealed class ClaimLineEventRecord
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }

    /// <summary>Plain Guid, no FK: lines are mutated in place on resubmit, so linkage survives.</summary>
    public Guid LineId { get; set; }

    public ApprovalStage Stage { get; set; } // LineManager for Submitted/Resubmitted (chain entry)
    public string Action { get; set; } = ""; // "Submitted" | "Resubmitted" | "Approved" | "Reduced" | "Rejected"
    public string ActorRole { get; set; } = "";
    public string ActorName { get; set; } = "";

    /// <summary>Claimed amount on submit/resubmit; decided amount on approve/reduce.</summary>
    public decimal? Amount { get; set; }

    public string? Reason { get; set; } // rejection reason
    public string? Comment { get; set; } // optional free text from the actor
    public DateTime At { get; set; }
}

/// <summary>An append-only audit entry: who acted, at which stage, and what they did.</summary>
public sealed class ApprovalEvent
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public ApprovalStage Stage { get; set; }
    public string ActorRole { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime At { get; set; }
}
