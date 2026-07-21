using Mems.Application.Workflow;

namespace Mems.Infrastructure.Persistence;

/// <summary>
/// Seeds a fresh (InMemory) database with clearly-fictional claims for the demo employee so the
/// dashboard, queues and history all have something to show on first run. Two already-decided
/// claims total Rs 15,500 (→ Rs 34,500 remaining of the Rs 50,000 cap, matching the mockup), and
/// one 2-item claim waits in the Line Manager's queue.
/// </summary>
public static class DemoSeed
{
    private const string Emp = "DEMO-M-002";

    public static void Seed(MemsDbContext db)
    {
        if (db.Claims.Any()) return;

        db.Claims.AddRange(
            PostedOpd(),
            CompletedHospitalisation(),
            AwaitingLineManager());

        db.SaveChanges();
    }

    // Approved + posted OPD (Rs 3,200) — shows the full lifecycle incl. the SAP reference.
    private static ClaimRecord PostedOpd()
    {
        var id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var claim = new ClaimRecord
        {
            Id = id,
            EmployeeId = Emp,
            SubmissionDate = new DateOnly(2026, 6, 12),
            Stage = ApprovalStage.Completed,
            Posted = true,
            SapReference = "AR-2026-000042",
            CreatedAt = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 6, 18, 15, 0, 0, DateTimeKind.Utc),
            Lines = { Decided(id, "Opd", 3_200m, new DateOnly(2026, 6, 10)) },
        };
        claim.History.AddRange(new[]
        {
            Evt(id, ApprovalStage.LineManager, "Line Manager", "Bilal (Line Manager)", "Approved 1, reduced 0, rejected 0. → Admin/HR.", new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc)),
            Evt(id, ApprovalStage.AdminHr, "Admin/HR", "Sana (Admin/HR)", "Approved 1, reduced 0, rejected 0. → Finance.", new DateTime(2026, 6, 15, 11, 0, 0, DateTimeKind.Utc)),
            Evt(id, ApprovalStage.Finance, "Finance", "Kamran (Finance)", "Approved 1, reduced 0, rejected 0. → Completed.", new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc)),
            Evt(id, ApprovalStage.Completed, "Finance", "Kamran (Finance)", "Recorded SAP posting (ref AR-2026-000042).", new DateTime(2026, 6, 18, 15, 0, 0, DateTimeKind.Utc)),
        });
        return claim;
    }

    // Approved hospitalisation (Rs 12,300), completed but not yet posted.
    private static ClaimRecord CompletedHospitalisation()
    {
        var id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        return new ClaimRecord
        {
            Id = id,
            EmployeeId = Emp,
            SubmissionDate = new DateOnly(2026, 7, 2),
            Stage = ApprovalStage.Completed,
            CreatedAt = new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc),
            Lines = { Decided(id, "Hospitalization", 12_300m, new DateOnly(2026, 6, 28)) },
        };
    }

    // Two-item claim (Rs 7,800) waiting for the Line Manager — the live demo starting point.
    private static ClaimRecord AwaitingLineManager()
    {
        var id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        return new ClaimRecord
        {
            Id = id,
            EmployeeId = Emp,
            SubmissionDate = new DateOnly(2026, 7, 14),
            Stage = ApprovalStage.LineManager,
            CreatedAt = new DateTime(2026, 7, 14, 9, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 7, 14, 9, 0, 0, DateTimeKind.Utc),
            Lines =
            {
                Pending(id, "Dental", 5_000m, new DateOnly(2026, 7, 11), "DEMO-DEP-2", "Child"),
                Pending(id, "Consultation", 2_800m, new DateOnly(2026, 7, 12)),
            },
        };
    }

    private static ClaimLineRecord Decided(Guid claimId, string category, decimal amount, DateOnly date) => new()
    {
        Id = Guid.NewGuid(),
        ClaimId = claimId,
        BeneficiaryKind = "Self",
        Category = category,
        ExpenseDate = date,
        ClaimedAmount = amount,
        CurrentAmount = amount,
        ApprovedAmount = amount,
        Status = LineDecisionStatus.Approved,
    };

    private static ClaimLineRecord Pending(Guid claimId, string category, decimal amount, DateOnly date, string? dependantId = null, string? relationship = null) => new()
    {
        Id = Guid.NewGuid(),
        ClaimId = claimId,
        BeneficiaryKind = dependantId is null ? "Self" : "Dependant",
        DependantId = dependantId,
        DependantRelationship = relationship,
        Category = category,
        ExpenseDate = date,
        ClaimedAmount = amount,
        CurrentAmount = amount,
        ApprovedAmount = null,
        Status = LineDecisionStatus.Pending,
    };

    private static ApprovalEvent Evt(Guid claimId, ApprovalStage stage, string role, string name, string summary, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        ClaimId = claimId,
        Stage = stage,
        ActorRole = role,
        ActorName = name,
        Summary = summary,
        At = at,
    };
}
