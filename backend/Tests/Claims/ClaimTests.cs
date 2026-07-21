using Mems.Domain.Claims;
using Mems.Domain.Common;

namespace Mems.Tests.Claims;

public sealed class ClaimTests
{
    private static ClaimLine Line(decimal claimed = 1_000m)
        => new(Guid.NewGuid(), Beneficiary.Self, ClaimCategory.Opd, new DateOnly(2026, 7, 10), Money.FromRupees(claimed));

    private static Claim ClaimWith(params ClaimLine[] lines)
        => new(Guid.NewGuid(), "E-001", new DateOnly(2026, 7, 21), lines);

    // ---- Totals ----

    [Fact]
    public void TotalClaimed_sums_all_lines_and_TotalApproved_ignores_pending_lines()
    {
        var approved = Line(1_000m);
        approved.ApproveInFull();
        var pending = Line(500m);

        var claim = ClaimWith(approved, pending);

        Assert.Equal(1_500m, claim.TotalClaimed.Amount);
        Assert.Equal(1_000m, claim.TotalApproved.Amount); // pending line contributes nothing
    }

    // ---- Overall status derivation (precedence matters) ----

    [Fact]
    public void Any_rejected_line_makes_the_claim_ActionNeeded_even_if_others_are_approved()
    {
        var approved = Line(); approved.ApproveInFull();
        var rejected = Line(); rejected.Reject("No receipt attached.");

        Assert.Equal(ClaimStatus.ActionNeeded, ClaimWith(approved, rejected).OverallStatus);
    }

    [Fact]
    public void All_lines_fully_approved_makes_the_claim_Approved()
    {
        var a = Line(); a.ApproveInFull();
        var b = Line(); b.ApproveInFull();

        Assert.Equal(ClaimStatus.Approved, ClaimWith(a, b).OverallStatus);
    }

    [Fact]
    public void A_pending_line_with_no_rejections_makes_the_claim_InReview()
    {
        var approved = Line(); approved.ApproveInFull();
        var pending = Line();

        Assert.Equal(ClaimStatus.InReview, ClaimWith(approved, pending).OverallStatus);
    }

    [Fact]
    public void Mix_of_full_and_partial_with_none_pending_or_rejected_is_PartiallyApproved()
    {
        var full = Line(1_000m); full.ApproveInFull();
        var partial = Line(1_000m); partial.ApprovePartial(Money.FromRupees(400m));

        Assert.Equal(ClaimStatus.PartiallyApproved, ClaimWith(full, partial).OverallStatus);
    }

    // ---- Editing rules ----

    [Fact]
    public void A_fully_pending_claim_is_editable()
    {
        Assert.True(ClaimWith(Line(), Line()).CanBeEditedByEmployee);
    }

    [Fact]
    public void A_claim_with_a_rejection_and_no_approvals_is_editable()
    {
        var rejected = Line(); rejected.Reject("Fix the amount.");
        var pending = Line();
        Assert.True(ClaimWith(rejected, pending).CanBeEditedByEmployee);
    }

    [Fact]
    public void A_rejection_reopens_the_claim_for_editing_even_when_other_lines_are_approved()
    {
        // Updated policy (2026-07-21): resubmission restarts the whole claim at the Line Manager,
        // so a rejection makes the claim editable even alongside approved lines.
        var approved = Line(); approved.ApproveInFull();
        var rejected = Line(); rejected.Reject("Bad receipt.");
        var pending = Line();

        Assert.True(ClaimWith(approved, rejected, pending).CanBeEditedByEmployee);
    }

    [Fact]
    public void An_approval_with_no_rejections_locks_the_claim()
    {
        var approved = Line(); approved.ApproveInFull();
        var pending = Line();

        Assert.False(ClaimWith(approved, pending).CanBeEditedByEmployee);
    }

    // ---- Invariants ----

    [Fact]
    public void A_claim_must_have_at_least_one_line()
    {
        Assert.Throws<ArgumentException>(() =>
            new Claim(Guid.NewGuid(), "E-001", new DateOnly(2026, 7, 21), Array.Empty<ClaimLine>()));
    }
}
