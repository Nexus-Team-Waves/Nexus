using FluentValidation;
using Mems.Domain.Claims;
using Mems.Domain.Common;

namespace Mems.Application.Claims;

/// <summary>
/// Turns a validated <see cref="SubmitClaimRequest"/> DTO into the trusted domain
/// <see cref="Claim"/> aggregate. Validation runs first — an invalid request never reaches the
/// domain constructors, which assume their inputs are already sane.
/// </summary>
public sealed class ClaimAssembler
{
    private readonly IValidator<SubmitClaimRequest> _validator;

    public ClaimAssembler(IValidator<SubmitClaimRequest> validator)
        => _validator = validator;

    /// <summary>
    /// Validate and build. Throws <see cref="ValidationException"/> if the request is invalid.
    /// </summary>
    public Claim Assemble(SubmitClaimRequest request)
    {
        _validator.ValidateAndThrow(request);

        var lines = request.Lines.Select(ToLine);
        return new Claim(request.ClaimId, request.EmployeeId, request.SubmissionDate, lines);
    }

    private static ClaimLine ToLine(SubmitClaimLine input)
    {
        var beneficiary = input.BeneficiaryKind == BeneficiaryKind.Self
            ? Beneficiary.Self
            // DependantId / Relationship are guaranteed present here by the validator.
            : Beneficiary.ForDependant(input.DependantId!, input.DependantRelationship!.Value);

        // Accept a client-supplied line id for offline traceability; mint one if absent.
        var lineId = input.LineId == Guid.Empty ? Guid.NewGuid() : input.LineId;

        return new ClaimLine(
            lineId,
            beneficiary,
            input.Category,
            input.ExpenseDate,
            Money.FromRupees(input.ClaimedAmount),
            input.ReceiptReference);
    }
}
