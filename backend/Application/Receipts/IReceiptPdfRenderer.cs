namespace Mems.Application.Receipts;

/// <summary>One receipt image, ready to embed.</summary>
public sealed record ReceiptPdfImage(string FileName, byte[] Content, string ContentType);

/// <summary>
/// Everything the PDF needs, pre-resolved — the renderer does no data access.
/// An empty <paramref name="Images"/> list means a legacy line submitted before image upload
/// existed; the renderer prints a note instead of images. The first image renders under the
/// details block; each additional image gets its own page.
/// </summary>
public sealed record ReceiptPdfModel(
    Guid ClaimId,
    string EmployeeId,
    int ItemNumber,
    string Beneficiary,
    string Category,
    DateOnly ExpenseDate,
    decimal ClaimedAmount,
    decimal CurrentAmount,
    string? ReceiptFileName,
    IReadOnlyList<ReceiptPdfImage> Images);

/// <summary>
/// Port for the per-line receipt PDF. Implemented in Infrastructure (PdfSharp) so the
/// Application layer stays free of the PDF dependency (CLAUDE.md §6).
/// </summary>
public interface IReceiptPdfRenderer
{
    byte[] Render(ReceiptPdfModel model);
}
