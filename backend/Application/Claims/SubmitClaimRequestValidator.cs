using FluentValidation;
using Mems.Domain.Claims;

namespace Mems.Application.Claims;

/// <summary>
/// Structural validation of an inbound claim submission — the rules that are fully decided and
/// need no external data (docs/entitlement-rules.md § Claim structure). Never trust the client;
/// this runs server-side regardless of any front-end checks (CLAUDE.md §6).
/// </summary>
/// <remarks>
/// Deliberately out of scope here (they need data or decisions this layer doesn't have yet):
/// dependant <em>coverage</em> (age/student limits — needs the employee's dependant list),
/// per-line sub-limit enforcement (room rate/consultation need day counts), and the submission
/// deadline (an OPEN policy item). Those are enforced elsewhere / once decided.
/// </remarks>
public sealed class SubmitClaimRequestValidator : AbstractValidator<SubmitClaimRequest>
{
    public SubmitClaimRequestValidator()
    {
        RuleFor(r => r.ClaimId)
            .NotEmpty().WithMessage("A client-generated claim id (idempotency key) is required.");

        RuleFor(r => r.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.");

        RuleFor(r => r.Lines)
            .NotNull().Must(l => l is { Count: > 0 })
            .WithMessage("A claim must have at least one line.");

        RuleForEach(r => r.Lines).SetValidator(new SubmitClaimLineValidator());

        // A line's expense date cannot be in the future relative to submission. This is a
        // data-integrity check, NOT a lateness gate — late submissions are shown to the approver
        // (days elapsed) and never auto-rejected (docs/entitlement-rules.md § Claim submission timing).
        RuleForEach(r => r.Lines)
            .Must((request, line) => line.ExpenseDate <= request.SubmissionDate)
            .WithMessage("A line's expense date cannot be after the claim's submission date.");
    }
}

/// <summary>Per-line structural rules.</summary>
public sealed class SubmitClaimLineValidator : AbstractValidator<SubmitClaimLine>
{
    public SubmitClaimLineValidator()
    {
        RuleFor(l => l.ClaimedAmount)
            .GreaterThan(0m).WithMessage("Claimed amount must be greater than zero.");

        RuleFor(l => l.Category)
            .IsInEnum().WithMessage("Unknown claim category.");

        RuleFor(l => l.ExpenseDate)
            .NotEqual(default(DateOnly)).WithMessage("Expense date is required.");

        RuleFor(l => l.BeneficiaryKind)
            .IsInEnum().WithMessage("Unknown beneficiary kind.");

        RuleFor(l => l.ReceiptReference)
            .NotEmpty().WithMessage("Each claim line must include a receipt attachment.");

        RuleFor(l => l.ReceiptIds)
            .Cascade(CascadeMode.Stop) // report the first receipt problem only
            .NotNull().WithMessage("Each claim line must include at least one uploaded receipt image.")
            .Must(ids => ids is { Count: >= 1 and <= 5 })
                .WithMessage("Each claim line needs 1 to 5 uploaded receipt images.")
            .Must(ids => ids!.All(id => id != Guid.Empty))
                .WithMessage("Each claim line must include valid uploaded receipt images.")
            .Must(ids => ids!.Distinct().Count() == ids!.Count)
                .WithMessage("A claim line lists the same receipt image twice.");

        RuleFor(l => l.Comment)
            .MaximumLength(500).WithMessage("A line note must be 500 characters or fewer.");

        // When the line is for a dependant, it must identify which one and how they are related.
        When(l => l.BeneficiaryKind == BeneficiaryKind.Dependant, () =>
        {
            RuleFor(l => l.DependantId)
                .NotEmpty().WithMessage("A dependant line must identify the dependant.");
            RuleFor(l => l.DependantRelationship)
                .NotNull().WithMessage("A dependant line must state the relationship.")
                .IsInEnum().WithMessage("Unknown dependant relationship.");
        });

        // A "self" line must not carry dependant fields — keeps the payload unambiguous.
        When(l => l.BeneficiaryKind == BeneficiaryKind.Self, () =>
        {
            RuleFor(l => l.DependantId)
                .Empty().WithMessage("A self line must not carry a dependant id.");
            RuleFor(l => l.DependantRelationship)
                .Null().WithMessage("A self line must not carry a dependant relationship.");
        });
    }
}
