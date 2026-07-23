namespace Mems.Application.Workflow;

/// <summary>
/// The shape of the approval chain: which role owns which stage, and what comes next. Kept in one
/// place so the sequence (Line Manager → Admin/HR → Finance → Top-Level) and the value-threshold
/// rule are not scattered through the code (docs/entitlement-rules.md § Approval flow).
/// </summary>
public static class ApprovalStagePolicy
{
    /// <summary>The role that acts at a given stage. Terminal stages have no acting role.</summary>
    public static UserRole? RoleForStage(ApprovalStage stage) => stage switch
    {
        ApprovalStage.LineManager => UserRole.LineManager,
        ApprovalStage.AdminHr => UserRole.AdminHr,
        ApprovalStage.Finance => UserRole.Finance,
        ApprovalStage.TopLevel => UserRole.TopLevel,
        _ => null,
    };

    /// <summary>The stage an approver role owns (inverse of <see cref="RoleForStage"/>).</summary>
    public static ApprovalStage? StageForRole(UserRole role) => role switch
    {
        UserRole.LineManager => ApprovalStage.LineManager,
        UserRole.AdminHr => ApprovalStage.AdminHr,
        UserRole.Finance => ApprovalStage.Finance,
        UserRole.TopLevel => ApprovalStage.TopLevel,
        _ => null,
    };

    /// <summary>
    /// The next stage after the current one approves everything. Finance branches: claims whose
    /// approved total exceeds the (admin-configurable) threshold need Top-Level sign-off; otherwise
    /// they complete. Admin/HR may escalate a claim DIRECTLY to Top-Level, skipping Finance review
    /// (decision 2026-07-23; Finance still records the SAP posting afterwards). NOTE: the policy
    /// leaves "threshold per line or per claim total" OPEN — this demo evaluates the claim total.
    /// </summary>
    public static ApprovalStage NextStage(ApprovalStage current, decimal approvedTotal, decimal threshold, bool escalatedToTopLevel)
        => current switch
        {
            ApprovalStage.LineManager => ApprovalStage.AdminHr,
            ApprovalStage.AdminHr => escalatedToTopLevel ? ApprovalStage.TopLevel : ApprovalStage.Finance,
            ApprovalStage.Finance => approvedTotal > threshold ? ApprovalStage.TopLevel : ApprovalStage.Completed,
            ApprovalStage.TopLevel => ApprovalStage.Completed,
            _ => ApprovalStage.Completed,
        };

    /// <summary>
    /// Whether this stage may reduce (partially approve) a line. The Line Manager approves in
    /// full or rejects only — reduction is available from Admin/HR onward (decision 2026-07-23).
    /// </summary>
    public static bool CanReduce(ApprovalStage stage) => stage != ApprovalStage.LineManager;

    public static string RoleLabel(UserRole role) => role switch
    {
        UserRole.LineManager => "Line Manager",
        UserRole.AdminHr => "Admin/HR",
        UserRole.Finance => "Finance",
        UserRole.TopLevel => "Top-Level Approver",
        _ => "Employee",
    };

    public static string StageLabel(ApprovalStage stage) => stage switch
    {
        ApprovalStage.LineManager => "Line Manager",
        ApprovalStage.AdminHr => "Admin/HR",
        ApprovalStage.Finance => "Finance",
        ApprovalStage.TopLevel => "Top-Level Approver",
        ApprovalStage.Completed => "Completed",
        ApprovalStage.ReturnedToEmployee => "Returned to employee",
        ApprovalStage.Closed => "Closed — rejected (final)",
        _ => stage.ToString(),
    };
}
