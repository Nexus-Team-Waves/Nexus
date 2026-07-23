using Microsoft.AspNetCore.Mvc;
using Mems.Application.Auth;
using Mems.Application.Receipts;
using Mems.Application.Workflow;

namespace Mems.Api.Controllers;

/// <summary>Approver-facing endpoints: the current role's queue, per-line decisions, and SAP posting.</summary>
[ApiController]
[Route("api/approvals")]
public sealed class ApprovalsController(
    ICurrentUser current, ClaimWorkflowService workflow, ReceiptService receipts) : ControllerBase
{
    /// <summary>Claims awaiting the signed-in approver's stage.</summary>
    [HttpGet]
    public async Task<IActionResult> Queue()
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var claims = await workflow.GetQueueForRoleAsync(u); // throws 403 for non-approvers
        return Ok(claims.Select(ClaimMapper.ToDto));
    }

    /// <summary>Apply this stage's decision to every line, advancing or returning the claim.</summary>
    [HttpPost("{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, DecideRequest request)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var claim = await workflow.DecideAsync(id, u, request.Lines, request.ForwardToTopLevel);
        return Ok(ClaimMapper.ToDto(claim));
    }

    /// <summary>One claim line's details + receipt image as a downloadable PDF (approvers only).</summary>
    [HttpGet("{id:guid}/lines/{lineId:guid}/receipt.pdf")]
    public async Task<IActionResult> LineReceiptPdf(Guid id, Guid lineId)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var pdf = await receipts.GetLineReceiptPdfAsync(id, lineId, u);
        return File(pdf.Content, "application/pdf", pdf.FileName);
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
