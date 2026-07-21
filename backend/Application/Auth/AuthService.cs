using System.Text.RegularExpressions;

namespace Mems.Application.Auth;

/// <summary>
/// MOCK email-OTP auth for the demo. The real design (docs/CLAUDE.md §10) emails a one-time code
/// and validates it server-side; here any 6-digit code succeeds and the bearer token is simply the
/// email. Clearly NOT production — no code is sent, no token is signed.
/// </summary>
public sealed partial class AuthService
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
        return DemoDirectory.Resolve(email.Trim());
    }

    /// <summary>Resolve a bearer token (the email) back to an identity.</summary>
    public AuthUser? Resolve(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : DemoDirectory.Resolve(token.Trim());
}
