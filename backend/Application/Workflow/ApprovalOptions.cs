namespace Mems.Application.Workflow;

/// <summary>
/// Approval tuning read from configuration. The extra-approval threshold is admin-configurable
/// (docs/entitlement-rules.md § Approval flow) — a claim whose approved total exceeds it needs
/// Top-Level sign-off after Finance.
/// </summary>
public sealed class ApprovalOptions
{
    public const string SectionName = "Approval";

    /// <summary>Claims with an approved total above this (PKR) require the Top-Level Approver.</summary>
    public decimal TopLevelThreshold { get; set; } = 25_000m;

    /// <summary>
    /// Sign-in email of the Finance authority for M-grade employees' claims. Changeable in
    /// config (Approval:FinanceMGradeEmail) without a code change — decision 2026-07-23.
    /// </summary>
    public string FinanceMGradeEmail { get; set; } = "mapproval@waves.com.pk";

    /// <summary>Sign-in email of the Finance authority for all other (non-M-grade) claims.</summary>
    public string FinanceOtherEmail { get; set; } = "otherapproval@waves.com.pk";
}
