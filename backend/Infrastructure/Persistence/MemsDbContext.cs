using Microsoft.EntityFrameworkCore;
using Mems.Application.Workflow;

namespace Mems.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the MEMS claim workflow. The demo runs on the InMemory provider (see
/// Program.cs) — production points the same context at SQL Server 2019 (CLAUDE.md §3) with no
/// model changes. Money columns are DECIMAL(18,4); audit columns are on <see cref="ClaimRecord"/>.
/// </summary>
public sealed class MemsDbContext(DbContextOptions<MemsDbContext> options) : DbContext(options)
{
    public DbSet<ClaimRecord> Claims => Set<ClaimRecord>();
    public DbSet<ClaimLineRecord> ClaimLines => Set<ClaimLineRecord>();
    public DbSet<ApprovalEvent> ApprovalEvents => Set<ApprovalEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ClaimRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ClaimId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.History).WithOne().HasForeignKey(h => h.ClaimId).OnDelete(DeleteBehavior.Cascade);
        });

        // NOTE: money columns are DECIMAL(18,4) under the SQL Server provider (configured there via
        // HasColumnType / HasPrecision). The InMemory demo provider is non-relational, so no column
        // type is set here — decimals are exact in memory regardless.
        b.Entity<ClaimLineRecord>(e => e.HasKey(x => x.Id));

        b.Entity<ApprovalEvent>(e => e.HasKey(x => x.Id));
    }
}
