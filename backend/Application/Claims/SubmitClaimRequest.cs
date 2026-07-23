using Mems.Domain.Claims;
using Mems.Domain.Employees;

namespace Mems.Application.Claims;

/// <summary>
/// Inbound payload for submitting a claim. This is the wire/DTO shape (primitive types,
/// nullable, untrusted) — it is validated by <c>SubmitClaimRequestValidator</c> and then mapped
/// into the trusted <see cref="Domain.Claims.Claim"/> aggregate by <c>ClaimAssembler</c>.
/// </summary>
/// <param name="ClaimId">
/// Client-generated UUID. It is the offline idempotency key (docs/CLAUDE.md §9): the same id
/// retried after a dropped connection must not create a second claim.
/// </param>
/// <param name="EmployeeId">The submitting employee.</param>
/// <param name="SubmissionDate">Date the employee submitted (client local date).</param>
/// <param name="Lines">One or more claim lines. A claim with no lines is invalid.</param>
public sealed record SubmitClaimRequest(
    Guid ClaimId,
    string EmployeeId,
    DateOnly SubmissionDate,
    IReadOnlyList<SubmitClaimLine> Lines);

/// <summary>One line of a <see cref="SubmitClaimRequest"/>.</summary>
/// <param name="LineId">Optional client UUID for the line; assembler generates one if empty.</param>
/// <param name="BeneficiaryKind">Self or Dependant.</param>
/// <param name="DependantId">Required when <paramref name="BeneficiaryKind"/> is Dependant.</param>
/// <param name="DependantRelationship">Required when the line is for a dependant.</param>
/// <param name="Category">The claim category for this line.</param>
/// <param name="ExpenseDate">When the expense/treatment was incurred.</param>
/// <param name="ClaimedAmount">Amount claimed, in PKR. Must be greater than zero.</param>
/// <param name="ReceiptReference">Display string for the receipts (joined original filenames).</param>
/// <param name="ReceiptIds">
/// Ids returned by POST /api/receipts for this line's uploaded images (1–5 per line, enforced
/// by the validator). Trailing defaults keep older positional constructions compiling.
/// </param>
/// <param name="Comment">Optional initiator note on this line, visible to every approver.</param>
public sealed record SubmitClaimLine(
    Guid LineId,
    BeneficiaryKind BeneficiaryKind,
    string? DependantId,
    DependantRelationship? DependantRelationship,
    ClaimCategory Category,
    DateOnly ExpenseDate,
    decimal ClaimedAmount,
    string? ReceiptReference,
    IReadOnlyList<Guid>? ReceiptIds = null,
    string? Comment = null);
