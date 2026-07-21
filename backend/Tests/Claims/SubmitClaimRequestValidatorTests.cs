using FluentValidation;
using Mems.Application.Claims;
using Mems.Domain.Claims;
using Mems.Domain.Employees;

namespace Mems.Tests.Claims;

public sealed class SubmitClaimRequestValidatorTests
{
    private readonly SubmitClaimRequestValidator _validator = new();

    private static readonly DateOnly Submitted = new(2026, 7, 21);
    private static readonly DateOnly Incurred = new(2026, 7, 10);

    private static SubmitClaimLine SelfLine(decimal amount = 1_000m)
        => new(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, amount, "receipt-1.jpg");

    private static SubmitClaimRequest Request(params SubmitClaimLine[] lines)
        => new(Guid.NewGuid(), "E-001", Submitted, lines);

    [Fact]
    public void A_well_formed_self_claim_passes()
    {
        Assert.True(_validator.Validate(Request(SelfLine())).IsValid);
    }

    [Fact]
    public void A_claim_with_no_lines_fails()
    {
        Assert.False(_validator.Validate(Request()).IsValid);
    }

    [Fact]
    public void A_zero_or_negative_amount_fails()
    {
        Assert.False(_validator.Validate(Request(SelfLine(0m))).IsValid);
        Assert.False(_validator.Validate(Request(SelfLine(-5m))).IsValid);
    }

    [Fact]
    public void A_dependant_line_must_carry_id_and_relationship()
    {
        var missing = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Dependant, null, null, ClaimCategory.Dental, Incurred, 500m, "r.jpg");
        Assert.False(_validator.Validate(Request(missing)).IsValid);

        var ok = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Dependant, "D-1", DependantRelationship.Child, ClaimCategory.Dental, Incurred, 500m, "r.jpg");
        Assert.True(_validator.Validate(Request(ok)).IsValid);
    }

    [Fact]
    public void A_self_line_must_not_carry_dependant_fields()
    {
        var bad = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, "D-1", DependantRelationship.Spouse, ClaimCategory.Opd, Incurred, 500m, "r.jpg");
        Assert.False(_validator.Validate(Request(bad)).IsValid);
    }

    [Fact]
    public void An_expense_date_after_the_submission_date_fails()
    {
        var future = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Submitted.AddDays(1), 500m, "r.jpg");
        Assert.False(_validator.Validate(Request(future)).IsValid);
    }

    [Fact]
    public void A_missing_expense_date_fails()
    {
        var noDate = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, default, 500m, "r.jpg");
        Assert.False(_validator.Validate(Request(noDate)).IsValid);
    }

    [Fact]
    public void Assembler_builds_a_domain_claim_from_a_valid_request()
    {
        var assembler = new ClaimAssembler(_validator);
        var claim = assembler.Assemble(Request(SelfLine(1_000m), SelfLine(500m)));

        Assert.Equal(2, claim.Lines.Count);
        Assert.Equal(1_500m, claim.TotalClaimed.Amount);
        Assert.Equal(ClaimStatus.InReview, claim.OverallStatus);
    }

    [Fact]
    public void Assembler_throws_on_an_invalid_request()
    {
        var assembler = new ClaimAssembler(_validator);
        Assert.Throws<ValidationException>(() => assembler.Assemble(Request()));
    }
}
