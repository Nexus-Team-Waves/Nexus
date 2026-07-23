using Microsoft.EntityFrameworkCore;
using Mems.Application.Workflow;

namespace Mems.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IClaimRepository"/>. Eager-loads lines and history
/// (the aggregate is always used whole) and returns tracked entities so the workflow service's
/// mutations persist on save.</summary>
public sealed class ClaimRepository(MemsDbContext db) : IClaimRepository
{
    private IQueryable<ClaimRecord> Full =>
        db.Claims.Include(c => c.Lines).Include(c => c.History).Include(c => c.LineEvents);

    public async Task<ClaimRecord?> GetAsync(Guid id, CancellationToken ct = default)
        => await Full.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ClaimRecord>> GetByEmployeeAsync(string employeeId, CancellationToken ct = default)
        => await Full.Where(c => c.EmployeeId == employeeId)
                     .OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<ClaimRecord>> GetByStageAsync(ApprovalStage stage, CancellationToken ct = default)
        => await Full.Where(c => c.Stage == stage)
                     .OrderBy(c => c.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<ClaimRecord>> GetPostableAsync(CancellationToken ct = default)
        => await Full.Where(c => c.Stage == ApprovalStage.Completed && !c.Posted)
                     .OrderBy(c => c.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(ClaimRecord claim, CancellationToken ct = default)
        => await db.Claims.AddAsync(claim, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
