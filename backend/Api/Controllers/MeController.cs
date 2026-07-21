using Microsoft.AspNetCore.Mvc;
using Mems.Application.Auth;
using Mems.Application.Entitlement;
using Mems.Application.Workflow;

namespace Mems.Api.Controllers;

/// <summary>The signed-in user and their entitlement summary.</summary>
[ApiController]
[Route("api")]
public sealed class MeController(ICurrentUser current, EntitlementService entitlement) : ControllerBase
{
    // Demo default: the seeded data is for policy year 2026.
    private const int DefaultYear = 2026;

    [HttpGet("me")]
    public IActionResult Me()
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        return Ok(new UserDto(u.Email, u.DisplayName, u.Role.ToString(), u.EmployeeId));
    }

    [HttpGet("entitlement")]
    public async Task<IActionResult> Entitlement([FromQuery] int? year)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        if (u.EmployeeId is null) return BadRequest(new { detail = "Only employees have an entitlement." });

        var y = year ?? DefaultYear;
        var balance = await entitlement.GetBalanceAsync(u.EmployeeId, y);
        var employee = DemoDirectory.Employees[u.EmployeeId];
        var dependants = employee.Dependants
            .Select(d => new DependantDto(d.Id, d.Label, d.Relationship))
            .ToList();

        return Ok(new EntitlementDto(
            y, employee.FirstName, employee.GradeLabel,
            balance.Cap.Amount, balance.Consumed.Amount, balance.Remaining.Amount,
            dependants));
    }
}
