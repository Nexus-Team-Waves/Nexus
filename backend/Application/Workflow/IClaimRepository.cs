namespace Mems.Application.Workflow;

/// <summary>
/// Persistence port for claims. Implemented in Infrastructure (EF Core). The workflow service
/// depends only on this abstraction so the store can be swapped (CLAUDE.md §6).
/// </summary>
public interface IClaimRepository
{
    Task<ClaimRecord?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ClaimRecord>> GetByEmployeeAsync(string employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<ClaimRecord>> GetByStageAsync(ApprovalStage stage, CancellationToken ct = default);

    /// <summary>Completed claims not yet posted to SAP — Finance's posting worklist.</summary>
    Task<IReadOnlyList<ClaimRecord>> GetPostableAsync(CancellationToken ct = default);
    Task AddAsync(ClaimRecord claim, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
