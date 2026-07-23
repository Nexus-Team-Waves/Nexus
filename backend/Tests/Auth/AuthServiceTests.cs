using Mems.Application.Auth;
using Mems.Application.Workflow;

namespace Mems.Tests.Auth;

/// <summary>
/// The two grade-routed Finance emails come from configuration and MUST resolve before the demo
/// directory's unknown-email fallback (which maps anything to the demo employee) — otherwise both
/// Finance queues silently appear empty.
/// </summary>
public sealed class AuthServiceTests
{
    private readonly AuthService _auth = new(new ApprovalOptions());

    [Fact]
    public void Configured_finance_emails_resolve_to_finance_identities()
    {
        var mGrade = _auth.Resolve("mapproval@waves.com.pk");
        Assert.Equal(UserRole.Finance, mGrade!.Role);

        var other = _auth.Resolve("OTHERAPPROVAL@waves.com.pk"); // case-insensitive
        Assert.Equal(UserRole.Finance, other!.Role);
    }

    [Fact]
    public void Custom_configured_finance_emails_are_honoured()
    {
        var auth = new AuthService(new ApprovalOptions
        {
            FinanceMGradeEmail = "mg-finance@example.test",
            FinanceOtherEmail = "corp-finance@example.test",
        });

        Assert.Equal(UserRole.Finance, auth.Resolve("mg-finance@example.test")!.Role);
        Assert.Equal(UserRole.Finance, auth.Resolve("corp-finance@example.test")!.Role);
        // The defaults are no longer finance identities once config changes them.
        Assert.Equal(UserRole.Employee, auth.Resolve("mapproval@waves.com.pk")!.Role);
    }

    [Fact]
    public void The_retired_finance_account_falls_back_to_the_demo_employee()
    {
        var user = _auth.Resolve("finance@waves.com.pk");
        Assert.Equal(UserRole.Employee, user!.Role);
    }

    [Fact]
    public void Directory_accounts_still_resolve()
    {
        Assert.Equal(UserRole.LineManager, _auth.Resolve("manager@waves.com.pk")!.Role);
        Assert.Equal(UserRole.AdminHr, _auth.Resolve("hr@waves.com.pk")!.Role);
        Assert.Equal(UserRole.TopLevel, _auth.Resolve("ed@waves.com.pk")!.Role);
        Assert.Equal(UserRole.Employee, _auth.Resolve("khalid@waves.com.pk")!.Role);
    }
}
