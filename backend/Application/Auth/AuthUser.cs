using Mems.Application.Workflow;

namespace Mems.Application.Auth;

/// <summary>A signed-in identity. Employees carry an <see cref="EmployeeId"/>; approvers do not.</summary>
public sealed record AuthUser(string Email, string DisplayName, UserRole Role, string? EmployeeId);

/// <summary>Per-request accessor for the current user, populated by the auth middleware.</summary>
public interface ICurrentUser
{
    AuthUser? User { get; }
    void Set(AuthUser user);
}

/// <summary>Default scoped implementation.</summary>
public sealed class CurrentUser : ICurrentUser
{
    public AuthUser? User { get; private set; }
    public void Set(AuthUser user) => User = user;
}
