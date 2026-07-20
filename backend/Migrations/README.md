# Migrations

Schema changes to the **MEMS DB** (SQL Server 2019, its own instance) go here.
Never apply ad-hoc manual edits to a database — every change is a migration, reviewed
and committed, so environments can be rebuilt identically (CLAUDE.md §8).

## Not yet decided

The migration tool is **undecided** because the data-access choice is undecided:

| Option | Migration approach |
|---|---|
| EF Core | `dotnet ef migrations add <Name>` — C# migrations, generated from the model |
| Dapper | Hand-written T-SQL migrations run by a tool (DbUp, Grate, Flyway) |

CLAUDE.md §6 says "prefer Dapper or EF Core (whichever the repo already uses)". The repo
does not use either yet — this is a greenfield decision that needs Umer's call, and it is
not one to make silently, since it shapes every repository class and the audit-trail design.

Considerations for the discussion:
- The team is strong in T-SQL, which favours Dapper + hand-written migrations (full control,
  no ORM surprises, reads naturally to this team).
- EF Core gives change-tracking and concurrency handling (`RowVersion`, §8) more cheaply.
- Whichever is chosen: **one ORM only** — do not add a second (§13).

## Standing rules for every migration

- T-SQL, SQL Server 2019 dialect. Never HANA SQL (§1.1).
- Money columns are `DECIMAL(18,4)` — never `FLOAT`/`REAL` (§8).
- Claim-affecting tables carry `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt`,
  and `RowVersion` where concurrency matters (§8).
- Claim status changes and approvals get an append-only audit trail (§8).
- Parameterised only. No string-concatenated or unjustified dynamic SQL (§8).
