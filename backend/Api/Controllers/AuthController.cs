using Microsoft.AspNetCore.Mvc;
using Mems.Application.Auth;
using Mems.Application.Workflow;

namespace Mems.Api.Controllers;

/// <summary>Mock email-OTP endpoints (see <see cref="AuthService"/>). Not production auth.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("request-code")]
    public IActionResult RequestCode(RequestCodeRequest request)
        => auth.RequestCode(request.Email)
            ? Ok(new { sent = true })
            : BadRequest(new { detail = "A valid work email is required." });

    [HttpPost("verify")]
    public IActionResult Verify(VerifyRequest request)
    {
        var user = auth.Verify(request.Email, request.Code);
        if (user is null) return Unauthorized(new { detail = "Invalid email or code." });

        // Demo token = the email. A real build returns a signed, expiring token.
        var dto = new UserDto(user.Email, user.DisplayName, user.Role.ToString(), user.EmployeeId);
        return Ok(new AuthResponse(user.Email, dto));
    }
}
