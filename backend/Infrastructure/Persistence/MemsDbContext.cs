using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Mems.Application.Notifications;
using Mems.Application.Receipts;
using Mems.Application.Workflow;

namespace Mems.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the MEMS claim workflow. The demo runs on a SQLite file database (see
/// Program.cs) — production points the same context at SQL Server 2019 (CLAUDE.md §3) with no
/// model changes. Money columns are DECIMAL(18,4); audit columns are on <see cref="ClaimRecord"/>.
/// </summary>
public sealed class MemsDbContext(DbContextOptions<MemsDbContext> options) : DbContext(options)
{
    public DbSet<ClaimRecord> Claims => Set<ClaimRecord>();
    public DbSet<ClaimLineRecord> ClaimLines => Set<ClaimLineRecord>();
    public DbSet<ApprovalEvent> ApprovalEvents => Set<ApprovalEvent>();
    public DbSet<ClaimLineEventRecord> ClaimLineEvents => Set<ClaimLineEventRecord>();
    public DbSet<ReceiptRecord> Receipts => Set<ReceiptRecord>();
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<PushSubscriptionRecord> PushSubscriptions => Set<PushSubscriptionRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ClaimRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ClaimId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.History).WithOne().HasForeignKey(h => h.ClaimId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.LineEvents).WithOne().HasForeignKey(le => le.ClaimId).OnDelete(DeleteBehavior.Cascade);
        });

        // NOTE: money columns are DECIMAL(18,4) under the SQL Server provider (configured there via
        // HasColumnType / HasPrecision).
        b.Entity<ClaimLineRecord>(e =>
        {
            e.HasKey(x => x.Id);
            // A line's receipt images are a small list of Guids stored as one comma-joined
            // column. The ValueComparer is REQUIRED: without it EF cannot detect list changes
            // on the returned-edit path and re-attached receipts silently would not persist.
            e.Property(x => x.ReceiptIds).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList(),
                new ValueComparer<List<Guid>>(
                    (a, b2) => a!.SequenceEqual(b2!),
                    v => v.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
                    v => v.ToList()));
        });

        b.Entity<ApprovalEvent>(e => e.HasKey(x => x.Id));

        b.Entity<ClaimLineEventRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Comment).HasMaxLength(500);
        });

        b.Entity<NotificationRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RecipientKey).HasMaxLength(200);
            e.HasIndex(x => x.RecipientKey);
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Body).HasMaxLength(500);
        });

        b.Entity<PushSubscriptionRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Endpoint).HasMaxLength(2048);
            e.HasIndex(x => x.Endpoint).IsUnique();
        });

        // Receipt blobs are standalone on purpose: ClaimLineRecord.ReceiptId is a plain Guid, not
        // a relationship, so eager-loaded claim queries can never drag megabytes of image bytes.
        b.Entity<ReceiptRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.ContentType).HasMaxLength(100);
        });
    }
}
