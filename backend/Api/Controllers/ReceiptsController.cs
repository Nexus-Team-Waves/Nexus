using Microsoft.AspNetCore.Mvc;
using Mems.Application.Auth;
using Mems.Application.Receipts;

namespace Mems.Api.Controllers;

/// <summary>Receipt image upload (employees) and retrieval (owner or approvers).</summary>
[ApiController]
[Route("api/receipts")]
public sealed class ReceiptsController(ICurrentUser current, ReceiptService receipts) : ControllerBase
{
    /// <summary>Upload one receipt image; the returned id goes into the claim line on submit.</summary>
    [HttpPost]
    // Transport-level cap just above the 5 MB policy cap so oversized uploads fail fast; the
    // service enforces the real limit with a friendly message.
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var result = await receipts.UploadAsync(u, file.FileName, file.ContentType, file.OpenReadStream(), file.Length);
        return Ok(result);
    }

    /// <summary>The stored image, streamed inline for viewing.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var u = current.User;
        if (u is null) return Unauthorized();
        var receipt = await receipts.GetForViewAsync(id, u);
        // nosniff + no filename in the disposition: the browser must treat this strictly as the
        // declared image type, and the original filename (possible PII) stays out of headers.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(receipt.Content, receipt.ContentType);
    }
}
