using System.Text.RegularExpressions;
using Mems.Application.Workflow;

namespace Mems.Application.Auth;

/// <summary>
/// MOCK email-OTP auth for the demo. The real design (docs/CLAUDE.md §10) emails a one-time code
/// and validates it server-side; here any 6-digit code succeeds and the bearer token is simply the
/// email. Clearly NOT production — no code is sent, no token is signed.
/// </summary>
public sealed partial class AuthService(ApprovalOptions options)
{
    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex SixDigits();

    /// <summary>"Send" a code. A no-op in the mock; always reports success for a plausible email.</summary>
    public bool RequestCode(string email) => !string.IsNullOrWhiteSpace(email) && email.Contains('@');

    /// <summary>Verify a code and return the identity, or null if the input is not acceptable.</summary>
    public AuthUser? Verify(string email, string code)
    {
        if (string.IsNullOrWhiteSpace(email) || code is null || !SixDigits().IsMatch(code))
            return null;
        return ResolveEmail(email.Trim());
    }

    /// <summary>Resolve a bearer token (the email) back to an identity.</summary>
    public AuthUser? Resolve(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : ResolveEmail(token.Trim());

    /// <summary>
    /// The two grade-routed Finance emails come from configuration and MUST be checked before
    /// DemoDirectory — its unknown-email fallback (any email → demo employee) would otherwise
    /// silently swallow them and both Finance queues would appear empty.
    /// </summary>
    private AuthUser ResolveEmail(string email)
    {
        if (email.Equals(options.FinanceMGradeEmail, StringComparison.OrdinalIgnoreCase))
            return new AuthUser(options.FinanceMGradeEmail, "Finance (M-Grade)", UserRole.Finance, null);
        if (email.Equals(options.FinanceOtherEmail, StringComparison.OrdinalIgnoreCase))
            return new AuthUser(options.FinanceOtherEmail, "Finance (Corporate)", UserRole.Finance, null);
        return DemoDirectory.Resolve(email);
    }
}
