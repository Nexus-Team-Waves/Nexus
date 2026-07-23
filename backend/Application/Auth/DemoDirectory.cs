using Mems.Domain.Common;
using Mems.Domain.Employees;
using Mems.Application.Workflow;

namespace Mems.Application.Auth;

/// <summary>
/// DEMO identities and employee master data — clearly fictional (CLAUDE.md §1.3). Stands in for the
/// Attendance/Payroll directory + the real OTP identity provider until those are wired. The five
/// accounts let a live demo walk a claim through every approval stage.
/// </summary>
public static class DemoDirectory
{
    public sealed record DemoDependant(string Id, string Label, string Relationship);

    public sealed record DemoEmployee(
        string EmployeeId,
        string FirstName,
        string GradeLabel,
        EmployeeEntitlementProfile Profile,
        IReadOnlyList<DemoDependant> Dependants);

    /// <summary>Sign-in accounts, keyed by email (case-insensitive).</summary>
    public static readonly IReadOnlyDictionary<string, AuthUser> Users =
        new Dictionary<string, AuthUser>(StringComparer.OrdinalIgnoreCase)
        {
            ["ayesha@waves.com.pk"] = new("ayesha@waves.com.pk", "Ayesha", UserRole.Employee, "DEMO-M-002"),
            ["khalid@waves.com.pk"] = new("khalid@waves.com.pk", "Khalid", UserRole.Employee, "DEMO-E-001"),
            ["manager@waves.com.pk"] = new("manager@waves.com.pk", "Bilal (Line Manager)", UserRole.LineManager, null),
            ["hr@waves.com.pk"] = new("hr@waves.com.pk", "Sana (Admin/HR)", UserRole.AdminHr, null),
            // NOTE: Finance identities are NOT listed here — the two grade-routed Finance emails
            // come from configuration (Approval:FinanceMGradeEmail / FinanceOtherEmail) and are
            // resolved by AuthService BEFORE this directory (decision 2026-07-23).
            ["ed@waves.com.pk"] = new("ed@waves.com.pk", "Director (ED)", UserRole.TopLevel, null),
        };

    /// <summary>
    /// First directory account holding a role — used to route notifications to the approver of a
    /// stage. Finance is config-routed and deliberately never found here.
    /// </summary>
    public static AuthUser? UserForRole(UserRole role)
        => Users.Values.FirstOrDefault(u => u.Role == role);

    /// <summary>Employee master data, keyed by employee id.</summary>
    public static readonly IReadOnlyDictionary<string, DemoEmployee> Employees =
        new Dictionary<string, DemoEmployee>(StringComparer.OrdinalIgnoreCase)
        {
            ["DEMO-M-002"] = new(
                "DEMO-M-002", "Ayesha", "Grade M2",
                // Basic Monthly × 1.5 (M1–M3) ≈ 50,000 annual cap, matching the product mockup.
                new EmployeeEntitlementProfile("DEMO-M-002", GradeBand.MStandard, Money.FromRupees(33_333.33m), new DateOnly(2019, 3, 1)),
                new[]
                {
                    new DemoDependant("DEMO-DEP-1", "Spouse", "Spouse"),
                    new DemoDependant("DEMO-DEP-2", "Child — Sample", "Child"),
                }),
            ["DEMO-E-001"] = new(
                "DEMO-E-001", "Khalid", "Grade E3",
                // Executive band → his claims route to the "other" Finance authority (not M-grade).
                new EmployeeEntitlementProfile("DEMO-E-001", GradeBand.Executive, Money.FromRupees(150_000m), new DateOnly(2015, 1, 10)),
                Array.Empty<DemoDependant>()),
        };

    /// <summary>
    /// Resolve an email to an identity. Known accounts return their role; any other work email is
    /// treated as the single demo employee (Ayesha) so the demo is frictionless. NOT production auth.
    /// </summary>
    public static AuthUser Resolve(string email)
        => Users.TryGetValue(email, out var user)
            ? user
            : new AuthUser(email, "Ayesha", UserRole.Employee, "DEMO-M-002");
}
