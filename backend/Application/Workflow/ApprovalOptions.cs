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
}
