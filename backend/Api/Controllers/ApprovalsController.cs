using Microsoft.AspNetCore.Mvc;
using Mems.Application.Auth;
using Mems.Application.Workflow;

namespace Mems.Api.Controllers;

/// <summary>Approver-facing endpoints: the current role's queue, per-line decisions, and SAP posting.</summary>
[ApiController]
[Route("api/approvals")]
public sealed class ApprovalsController(ICurrentUser current, ClaimWorkflowService workflow) : ControllerBase
{
    /// <summary>Claims awaiting the signed-in approver's stage.</summary>
    [HttpGet]
    public async Task<IActionResult> Queue()
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var claims = await workflow.GetQueueForRoleAsync(u.Role); // throws 403 for non-approvers
        return Ok(claims.Select(ClaimMapper.ToDto));
    }

    /// <summary>Apply this stage's decision to every line, advancing or returning the claim.</summary>
    [HttpPost("{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, DecideRequest request)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var claim = await workflow.DecideAsync(id, u, request.Lines);
        return Ok(ClaimMapper.ToDto(claim));
    }

    /// <summary>Finance records the manual SAP posting reference on a completed claim.</summary>
    [HttpPost("{id:guid}/post")]
    public async Task<IActionResult> Post(Guid id, PostRequest request)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var claim = await workflow.PostAsync(id, u, request.SapReference);
        return Ok(ClaimMapper.ToDto(claim));
    }
}
