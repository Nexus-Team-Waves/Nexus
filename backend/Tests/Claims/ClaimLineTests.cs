using Mems.Domain.Claims;
using Mems.Domain.Common;

namespace Mems.Tests.Claims;

public sealed class ClaimLineTests
{
    private static ClaimLine Line(decimal claimed = 1_000m)
        => new(Guid.NewGuid(), Beneficiary.Self, ClaimCategory.Opd, new DateOnly(2026, 7, 10), Money.FromRupees(claimed));

    [Fact]
    public void A_new_line_is_pending_with_no_approved_amount()
    {
        var line = Line();
        Assert.Equal(ClaimLineStatus.Pending, line.Status);
        Assert.Null(line.ApprovedAmount);
    }

    [Fact]
    public void ApproveInFull_sets_approved_equal_to_claimed()
    {
        var line = Line(1_000m);
        line.ApproveInFull();
        Assert.Equal(ClaimLineStatus.Approved, line.Status);
        Assert.Equal(1_000m, line.ApprovedAmount!.Value.Amount);
    }

    [Fact]
    public void ApprovePartial_must_be_between_zero_and_claimed()
    {
        Assert.Throws<ArgumentException>(() => Line(1_000m).ApprovePartial(Money.FromRupees(1_000m))); // == claimed
        Assert.Throws<ArgumentException>(() => Line(1_000m).ApprovePartial(Money.FromRupees(1_200m))); // > claimed
        Assert.Throws<ArgumentException>(() => Line(1_000m).ApprovePartial(Money.Zero));               // zero
    }

    [Fact]
    public void ApprovePartial_records_the_reduced_amount()
    {
        var line = Line(1_000m);
        line.ApprovePartial(Money.FromRupees(400m));
        Assert.Equal(ClaimLineStatus.PartiallyApproved, line.Status);
        Assert.Equal(400m, line.ApprovedAmount!.Value.Amount);
    }

    [Fact]
    public void Reject_requires_a_reason_and_zeroes_the_approved_amount()
    {
        Assert.Throws<ArgumentException>(() => Line().Reject("  "));

        var line = Line();
        line.Reject("Receipt illegible.");
        Assert.Equal(ClaimLineStatus.Rejected, line.Status);
        Assert.Equal("Receipt illegible.", line.RejectionReason);
        Assert.Equal(0m, line.ApprovedAmount!.Value.Amount);
    }

    [Fact]
    public void A_decided_line_cannot_be_acted_on_again()
    {
        var line = Line();
        line.ApproveInFull();
        Assert.Throws<InvalidOperationException>(() => line.Reject("changed my mind"));
    }

    [Fact]
    public void Claimed_amount_must_be_greater_than_zero()
    {
        Assert.Throws<ArgumentException>(() =>
            new ClaimLine(Guid.NewGuid(), Beneficiary.Self, ClaimCategory.Opd, new DateOnly(2026, 7, 10), Money.Zero));
    }
}
