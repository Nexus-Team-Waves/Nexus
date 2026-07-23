namespace Mems.Application.Receipts;

/// <summary>Persistence port for receipt images. Blobs are loaded by id only, never joined.</summary>
public interface IReceiptRepository
{
    Task AddAsync(ReceiptRecord receipt, CancellationToken ct = default);

    Task<ReceiptRecord?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Which of these ids exist AND were uploaded by this employee. Used at claim submit/edit to
    /// verify every referenced receipt is real and owned — a projection query, no bytes loaded.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetOwnedExistingIdsAsync(
        IReadOnlyCollection<Guid> ids, string employeeId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
