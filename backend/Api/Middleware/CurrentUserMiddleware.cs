using Mems.Application.Auth;

namespace Mems.Api.Middleware;

/// <summary>
/// Resolves the bearer token (the user's email, in this demo) to an <see cref="AuthUser"/> and
/// stashes it on the scoped <see cref="ICurrentUser"/>. Real auth (signed OTP tokens) slots in here
/// without touching the controllers.
/// </summary>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, AuthService auth)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = header["Bearer ".Length..].Trim();
            var user = auth.Resolve(token);
            if (user is not null) currentUser.Set(user);
        }

        await next(context);
    }
}
