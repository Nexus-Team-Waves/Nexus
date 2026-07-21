using Mems.Domain.Common;
using Mems.Domain.Employees;

namespace Mems.Domain.Entitlement;

/// <summary>
/// The decided entitlement figures, transcribed verbatim from the Waves Medical Policy
/// (QR/HR/POL-002) as recorded in docs/entitlement-rules.md. This is the single place the
/// policy numbers live — do not scatter these constants through the code.
/// </summary>
/// <remarks>
/// Everything here is marked <em>decided</em> in the policy doc. Items still open (the M-grade
/// consultation-fee cap, the extra-approval value threshold, the submission deadline, the
/// resubmission re-entry point) are deliberately NOT given values here — a guessed number in a
/// medical entitlement system is a defect. Open caps are represented as <c>null</c>.
/// </remarks>
public static class EntitlementRule
{
    /// <summary>
    /// Annual entitlement multiplier applied to Basic salary, by band
    /// (docs/entitlement-rules.md § Entitlement definition).
    /// </summary>
    public static decimal AnnualMultiplier(GradeBand band) => band switch
    {
        GradeBand.MSenior => 1.0m,    // M4 & above
        GradeBand.MStandard => 1.5m,  // M1–M3
        GradeBand.Executive => 2.0m,  // E1–E5
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown grade band."),
    };

    /// <summary>
    /// Fixed denominator for daily pro-rating of mid-year joiners. The policy states the
    /// formula as "(days employed ÷ 365)", so 365 is used as a constant even in leap years —
    /// matching the policy text rather than the calendar.
    /// </summary>
    public const int ProRataDaysInYear = 365;

    /// <summary>
    /// Per-day room-rate cap for hospitalization, by band
    /// (docs/entitlement-rules.md § Coverage — Sub-limits).
    /// </summary>
    public static Money RoomRateCapPerDay(GradeBand band) => band switch
    {
        GradeBand.MSenior => Money.FromRupees(8_000m),   // M4+
        GradeBand.MStandard => Money.FromRupees(6_000m), // M1–M3
        GradeBand.Executive => Money.FromRupees(3_000m), // E1–E5
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown grade band."),
    };

    // NOTE: the per-day/per-patient consultation-fee cap is deliberately NOT a constant here.
    // As of the 2026-07-21 rules it is admin-configured per grade (with effective date and an
    // active flag) and is a SOFT limit surfaced at approval time — approvers may still approve
    // over-limit claims (docs/entitlement-rules.md § Coverage). The configured value is read
    // through Mems.Application's IConsultationFeeLimitProvider, not from policy source here.
    // Even E1–E5's Rs 200/day/patient is only the initial configured value, not a hardcoded rule.

    /// <summary>
    /// Bills at or below this amount do not require a doctor's prescription — all grades
    /// (docs/entitlement-rules.md § Coverage — Sub-limits).
    /// </summary>
    public static readonly Money PrescriptionExemptThreshold = Money.FromRupees(1_000m);

    /// <summary>
    /// There is no co-pay/deductible: covered expenses are reimbursed at 100% up to the cap
    /// (docs/entitlement-rules.md § Coverage — Co-pay).
    /// </summary>
    public const decimal CoPayRate = 0m;
}
