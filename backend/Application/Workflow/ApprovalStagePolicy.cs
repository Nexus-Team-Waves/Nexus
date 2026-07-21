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
    /// they complete. NOTE: the policy leaves "threshold per line or per claim total" OPEN — this
    /// demo evaluates it on the claim total.
    /// </summary>
    public static ApprovalStage NextStage(ApprovalStage current, decimal approvedTotal, decimal threshold)
        => current switch
        {
            ApprovalStage.LineManager => ApprovalStage.AdminHr,
            ApprovalStage.AdminHr => ApprovalStage.Finance,
            ApprovalStage.Finance => approvedTotal > threshold ? ApprovalStage.TopLevel : ApprovalStage.Completed,
            ApprovalStage.TopLevel => ApprovalStage.Completed,
            _ => ApprovalStage.Completed,
        };

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
        _ => stage.ToString(),
    };
}
