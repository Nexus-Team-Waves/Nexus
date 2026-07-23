namespace Mems.Application.Receipts;

/// <summary>Returned by the upload endpoint; the client carries ReceiptId into the claim lines.</summary>
public sealed record ReceiptUploadResponse(Guid ReceiptId, string FileName, long SizeBytes);

/// <summary>An image ready to stream back to the browser.</summary>
public sealed record ReceiptContent(byte[] Content, string ContentType, string FileName);

/// <summary>A rendered per-line receipt PDF ready to download.</summary>
public sealed record LineReceiptPdf(byte[] Content, string FileName);
