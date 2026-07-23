using FluentValidation;
using FluentValidation.Results;
using Mems.Application.Auth;
using Mems.Application.Workflow;

namespace Mems.Application.Receipts;

/// <summary>
/// Receipt use-cases: upload (employee), view (owner or any approver), and the per-line PDF
/// (approvers only). Authorization lives here so the controllers stay thin (CLAUDE.md §6) and the
/// rules are unit-testable.
/// </summary>
public sealed class ReceiptService(
    IReceiptRepository receipts,
    IClaimRepository claims,
    IReceiptPdfRenderer pdf)
{
    public const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MB per image

    // JPEG and PNG only — the shortlist PdfSharp's Core build can embed (WebP/HEIC cannot).
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png" };

    /// <summary>Store an uploaded receipt image for the signed-in employee.</summary>
    public async Task<ReceiptUploadResponse> UploadAsync(
        AuthUser actor, string fileName, string? contentType, Stream content, long length,
        CancellationToken ct = default)
    {
        if (actor.EmployeeId is null)
            throw new WorkflowForbiddenException("Only employees upload receipts.");

        if (length <= 0)
            throw Invalid("The uploaded file is empty.");
        if (length > MaxSizeBytes)
            throw Invalid("The receipt image must be 5 MB or smaller.");
        var normalizedType = contentType?.Trim().ToLowerInvariant() ?? "";
        if (!AllowedContentTypes.Contains(normalizedType))
            throw Invalid("Only JPEG or PNG receipt images are accepted.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        // Sniff the magic bytes so the stored blob really is what its content type claims —
        // the declared type comes from the client and cannot be trusted.
        if (!LooksLike(normalizedType, bytes))
            throw Invalid("The uploaded file does not look like a valid JPEG or PNG image.");

        var record = new ReceiptRecord
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = normalizedType,
            SizeBytes = bytes.LongLength,
            Content = bytes,
            UploadedByEmployeeId = actor.EmployeeId,
            CreatedAt = DateTime.UtcNow,
        };
        await receipts.AddAsync(record, ct);
        await receipts.SaveChangesAsync(ct);

        return new ReceiptUploadResponse(record.Id, record.FileName, record.SizeBytes);
    }

    /// <summary>The image itself — visible to its uploader and to every approver role.</summary>
    public async Task<ReceiptContent> GetForViewAsync(Guid receiptId, AuthUser actor, CancellationToken ct = default)
    {
        var record = await receipts.GetAsync(receiptId, ct) ?? throw new ReceiptNotFoundException(receiptId);

        var isApprover = actor.Role != UserRole.Employee;
        if (!isApprover && !record.UploadedByEmployeeId.Equals(actor.EmployeeId, StringComparison.OrdinalIgnoreCase))
            throw new WorkflowForbiddenException("You can only view your own receipts.");

        return new ReceiptContent(record.Content, record.ContentType, record.FileName);
    }

    /// <summary>
    /// One PDF per claim line: the line's details plus its receipt image. Approver roles only.
    /// Lines submitted before image upload existed (no stored bytes) still get a details-only PDF.
    /// </summary>
    public async Task<LineReceiptPdf> GetLineReceiptPdfAsync(
        Guid claimId, Guid lineId, AuthUser actor, CancellationToken ct = default)
    {
        if (actor.Role == UserRole.Employee)
            throw new WorkflowForbiddenException("Only approvers download receipt PDFs.");

        var claim = await claims.GetAsync(claimId, ct) ?? throw new ClaimNotFoundException(claimId);

        // Same ordering as ClaimMapper.ToDto, so "Item n" here matches the approver's screen.
        var ordered = claim.Lines.OrderBy(l => l.ExpenseDate).ToList();
        var index = ordered.FindIndex(l => l.Id == lineId);
        if (index < 0)
            throw new WorkflowConflictException("This claim has no such line.");
        var line = ordered[index];

        // Load each image blob one by one (≤5); a missing blob is tolerated and skipped so a
        // partially broken line still yields a usable PDF.
        var images = new List<ReceiptPdfImage>();
        foreach (var receiptId in line.ReceiptIds)
        {
            var receipt = await receipts.GetAsync(receiptId, ct);
            if (receipt is not null)
                images.Add(new ReceiptPdfImage(receipt.FileName, receipt.Content, receipt.ContentType));
        }

        var beneficiary = line.BeneficiaryKind == "Dependant"
            ? $"Dependant ({line.DependantRelationship ?? "unspecified"})"
            : "Self";

        var content = pdf.Render(new ReceiptPdfModel(
            claim.Id, claim.EmployeeId, index + 1, beneficiary, line.Category,
            line.ExpenseDate, line.ClaimedAmount, line.CurrentAmount,
            line.ReceiptReference ?? images.FirstOrDefault()?.FileName, images));

        var shortId = claim.Id.ToString("N")[..8];
        return new LineReceiptPdf(content, $"claim-{shortId}-item-{index + 1}-receipt.pdf");
    }

    /// <summary>400 via the existing ValidationException handling in the API middleware.</summary>
    private static ValidationException Invalid(string message)
        => new(new[] { new ValidationFailure("file", message) });

    private static bool LooksLike(string contentType, byte[] bytes) => contentType switch
    {
        "image/jpeg" => bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
        _ => false,
    };
}
