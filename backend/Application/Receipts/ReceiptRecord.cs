namespace Mems.Application.Receipts;

/// <summary>
/// Persisted receipt image (mutable POCO for EF Core, like <see cref="Workflow.ClaimRecord"/>).
/// Deliberately NOT navigable from <see cref="Workflow.ClaimLineRecord"/> — the line stores only
/// the Guid — so claim/queue queries never drag image bytes along; blobs load one at a time via
/// <see cref="IReceiptRepository.GetAsync"/>.
/// </summary>
public sealed class ReceiptRecord
{
    public Guid Id { get; set; } // server-generated

    /// <summary>Original filename, for display only. May identify a person — never log it (CLAUDE.md §10).</summary>
    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = ""; // "image/jpeg" | "image/png"
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>Uploader — receipts are only readable by this employee and approvers.</summary>
    public string UploadedByEmployeeId { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}
