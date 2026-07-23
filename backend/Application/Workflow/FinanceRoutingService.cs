using Mems.Application.Entitlement;
using Mems.Domain.Employees;

namespace Mems.Application.Workflow;

/// <summary>
/// Routes the Finance approval stage to one of two authorities by the claiming employee's grade
/// (decision 2026-07-23): M-grade (MSenior/MStandard) claims → Approval:FinanceMGradeEmail;
/// everything else (Executive, or an unknown profile) → Approval:FinanceOtherEmail. Both emails
/// are configuration, changeable without a code change.
/// </summary>
public sealed class FinanceRoutingService(IEmployeeEntitlementProfileProvider profiles, ApprovalOptions options)
{
    /// <summary>The Finance sign-in email responsible for this employee's claims.</summary>
    public async Task<string> RequiredFinanceEmailAsync(string employeeId, CancellationToken ct = default)
    {
        var profile = await profiles.GetProfileAsync(employeeId, ct);
        var isMGrade = profile?.Band is GradeBand.MSenior or GradeBand.MStandard;
        return isMGrade ? options.FinanceMGradeEmail : options.FinanceOtherEmail;
    }

    /// <summary>Whether this Finance actor is the authority for this employee's claims.</summary>
    public async Task<bool> IsRequiredFinanceAsync(string employeeId, string actorEmail, CancellationToken ct = default)
        => (await RequiredFinanceEmailAsync(employeeId, ct)).Equals(actorEmail, StringComparison.OrdinalIgnoreCase);
}
