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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ClaimLineRecord> Lines { get; set; } = new();
    public List<ApprovalEvent> History { get; set; } = new();
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
    public string? ReceiptReference { get; set; }

    /// <summary>What the employee originally claimed. Never changes.</summary>
    public decimal ClaimedAmount { get; set; }

    /// <summary>The amount in play at the current stage (starts at claimed; a reduction carries forward).</summary>
    public decimal CurrentAmount { get; set; }

    /// <summary>The current stage's decided amount; null while Pending at this stage.</summary>
    public decimal? ApprovedAmount { get; set; }

    public LineDecisionStatus Status { get; set; }
    public string? RejectionReason { get; set; }
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
