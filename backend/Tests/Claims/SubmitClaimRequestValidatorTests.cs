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
        => new(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, amount, "receipt-1.jpg", new[] { Guid.NewGuid() });

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
        var missing = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Dependant, null, null, ClaimCategory.Dental, Incurred, 500m, "r.jpg", new[] { Guid.NewGuid() });
        Assert.False(_validator.Validate(Request(missing)).IsValid);

        var ok = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Dependant, "D-1", DependantRelationship.Child, ClaimCategory.Dental, Incurred, 500m, "r.jpg", new[] { Guid.NewGuid() });
        Assert.True(_validator.Validate(Request(ok)).IsValid);
    }

    [Fact]
    public void A_self_line_must_not_carry_dependant_fields()
    {
        var bad = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, "D-1", DependantRelationship.Spouse, ClaimCategory.Opd, Incurred, 500m, "r.jpg", new[] { Guid.NewGuid() });
        Assert.False(_validator.Validate(Request(bad)).IsValid);
    }

    [Fact]
    public void An_expense_date_after_the_submission_date_fails()
    {
        var future = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Submitted.AddDays(1), 500m, "r.jpg", new[] { Guid.NewGuid() });
        Assert.False(_validator.Validate(Request(future)).IsValid);
    }

    [Fact]
    public void A_line_without_a_receipt_fails()
    {
        var noReceipt = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, 500m, null, new[] { Guid.NewGuid() });
        Assert.False(_validator.Validate(Request(noReceipt)).IsValid);

        var emptyReceipt = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, 500m, "", new[] { Guid.NewGuid() });
        Assert.False(_validator.Validate(Request(emptyReceipt)).IsValid);
    }

    [Fact]
    public void A_line_without_uploaded_receipt_images_fails()
    {
        var nullList = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, 500m, "r.jpg");
        Assert.False(_validator.Validate(Request(nullList)).IsValid);

        var emptyList = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, 500m, "r.jpg", Array.Empty<Guid>());
        Assert.False(_validator.Validate(Request(emptyList)).IsValid);

        var zeroGuid = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, 500m, "r.jpg", new[] { Guid.Empty });
        Assert.False(_validator.Validate(Request(zeroGuid)).IsValid);
    }

    [Fact]
    public void Receipt_image_lists_are_capped_and_deduplicated()
    {
        var six = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var tooMany = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, 500m, "r.jpg", six);
        Assert.False(_validator.Validate(Request(tooMany)).IsValid);

        var dupe = Guid.NewGuid();
        var duplicated = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, 500m, "r.jpg", new[] { dupe, dupe });
        Assert.False(_validator.Validate(Request(duplicated)).IsValid);

        var five = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var maxed = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, Incurred, 500m, "r.jpg", five);
        Assert.True(_validator.Validate(Request(maxed)).IsValid);
    }

    [Fact]
    public void A_missing_expense_date_fails()
    {
        var noDate = new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd, default, 500m, "r.jpg", new[] { Guid.NewGuid() });
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
