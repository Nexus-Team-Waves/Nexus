using Microsoft.AspNetCore.Mvc;
using Mems.Application.Auth;
using Mems.Application.Claims;
using Mems.Application.Workflow;

namespace Mems.Api.Controllers;

/// <summary>Employee-facing claim endpoints: submit, list mine, view, edit.</summary>
[ApiController]
[Route("api/claims")]
public sealed class ClaimsController(ICurrentUser current, ClaimWorkflowService workflow) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit(SubmitClaimRequest request)
    {
        var u = current.User;
        if (u?.EmployeeId is null) return Unauthorized();

        // Trust the authenticated identity, not the body, for the owning employee.
        var claim = await workflow.SubmitAsync(u.EmployeeId, request with { EmployeeId = u.EmployeeId });
        return Ok(ClaimMapper.ToDto(claim));
    }

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var u = current.User;
        if (u?.EmployeeId is null) return Unauthorized();
        var claims = await workflow.GetForEmployeeAsync(u.EmployeeId);
        return Ok(claims.Select(ClaimMapper.ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var claim = await workflow.GetAsync(id);
        if (claim is null) return NotFound();
        if (u.Role == UserRole.Employee && claim.EmployeeId != u.EmployeeId)
            return StatusCode(StatusCodes.Status403Forbidden);
        return Ok(ClaimMapper.ToDto(claim));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, SubmitClaimRequest request)
    {
        var u = current.User;
        if (u?.EmployeeId is null) return Unauthorized();
        var claim = await workflow.EditAsync(id, u.EmployeeId, request with { ClaimId = id, EmployeeId = u.EmployeeId });
        return Ok(ClaimMapper.ToDto(claim));
    }
}
