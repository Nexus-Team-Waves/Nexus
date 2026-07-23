using Microsoft.EntityFrameworkCore;
using Mems.Application.Receipts;

namespace Mems.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IReceiptRepository"/>. Blobs load one at a time
/// by id; the ownership check is a key-only projection so submit never reads image bytes.</summary>
public sealed class ReceiptRepository(MemsDbContext db) : IReceiptRepository
{
    public async Task AddAsync(ReceiptRecord receipt, CancellationToken ct = default)
        => await db.Receipts.AddAsync(receipt, ct);

    public async Task<ReceiptRecord?> GetAsync(Guid id, CancellationToken ct = default)
        => await db.Receipts.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Guid>> GetOwnedExistingIdsAsync(
        IReadOnlyCollection<Guid> ids, string employeeId, CancellationToken ct = default)
        => await db.Receipts
            .Where(r => ids.Contains(r.Id) && r.UploadedByEmployeeId == employeeId)
            .Select(r => r.Id)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
