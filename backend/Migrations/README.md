# Migrations

Schema changes to the **MEMS DB** (SQL Server 2019, its own instance) go here.
Never apply ad-hoc manual edits to a database — every change is a migration, reviewed
and committed, so environments can be rebuilt identically (CLAUDE.md §8).

## Tool: EF Core (decided)

Migrations are C#, generated from the EF Core model:

```bash
dotnet ef migrations add <Name> --project backend/Api
dotnet ef database update --project backend/Api
```

Do not add Dapper or any second data-access library (CLAUDE.md §13).

## Standing rules for every migration

- T-SQL, SQL Server 2019 dialect. Never HANA SQL (§1.1).
- Money columns are `DECIMAL(18,4)` — never `FLOAT`/`REAL` (§8).
- Claim-affecting tables carry `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt`,
  and a `RowVersion` concurrency token where concurrency matters (§8).
- Claim status changes and approvals get an append-only audit trail (§8).
- The claim table needs `Posted` (bool) and `SapReferenceNumber` (nullable until posted) —
  see CLAUDE.md §7. Enforce at the application layer that `Posted = true` requires a
  non-empty `SapReferenceNumber`.
