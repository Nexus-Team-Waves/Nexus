namespace Mems.Application.Workflow;

/// <summary>Response DTOs and inbound decision payloads for the claim/approval API.</summary>

public sealed record ClaimDto(
    Guid Id,
    string EmployeeId,
    DateOnly SubmissionDate,
    string Stage,
    string StageLabel,
    string OverallStatus,
    bool Posted,
    string? SapReference,
    decimal TotalClaimed,
    decimal TotalApproved,
    bool Editable,
    IReadOnlyList<ClaimLineDto> Lines,
    IReadOnlyList<ApprovalEventDto> History);

public sealed record ClaimLineDto(
    Guid LineId,
    string BeneficiaryKind,
    string? DependantId,
    string? DependantRelationship,
    string Category,
    DateOnly ExpenseDate,
    decimal ClaimedAmount,
    decimal CurrentAmount,
    decimal? ApprovedAmount,
    string Status,
    string? RejectionReason,
    string? ReceiptReference);

public sealed record ApprovalEventDto(
    string Stage,
    string ActorRole,
    string ActorName,
    string Summary,
    DateTime At);

/// <summary>A stage's decision on every line of a claim (the approver submits all at once).</summary>
public sealed record DecideRequest(IReadOnlyList<LineDecisionInput> Lines);

/// <summary>Action is "approve" | "reduce" | "reject". Amount required for reduce; Reason for reject.</summary>
public sealed record LineDecisionInput(Guid LineId, string Action, decimal? Amount, string? Reason);

/// <summary>Finance records the manual SAP posting reference (docs/CLAUDE.md §7).</summary>
public sealed record PostRequest(string SapReference);

// ---- Auth / identity ----

public sealed record RequestCodeRequest(string Email);
public sealed record VerifyRequest(string Email, string Code);
public sealed record AuthResponse(string Token, UserDto User);
public sealed record UserDto(string Email, string DisplayName, string Role, string? EmployeeId);

public sealed record EntitlementDto(
    int Year,
    string FirstName,
    string GradeLabel,
    decimal Cap,
    decimal Consumed,
    decimal Remaining,
    IReadOnlyList<DependantDto> Dependants);

public sealed record DependantDto(string Id, string Label, string Relationship);
