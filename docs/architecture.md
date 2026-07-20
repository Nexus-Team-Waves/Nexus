# MEMS — Architecture

> Status: **scaffold**. Structure is real; business logic is not yet implemented.

## System context

MEMS is a utility application built *around* SAP Business One 10.00 FP2102 — it does not
modify SAP B1 itself. Employees submit medical claims; MEMS validates them against
entitlement rules, routes them for approval, and posts approved financial entries to SAP B1.

```
┌─────────────────────┐
│  React + TS PWA     │  offline-capable claim entry
│  (Dexie/IndexedDB)  │  ── never talks to SAP or any DB directly ──┐
└──────────┬──────────┘                                             │
           │ HTTPS (typed API client)                               │
           ▼                                                        │
┌─────────────────────┐                                             │
│  MEMS Web API       │  the ONLY component that talks to SAP / DBs │
│  (ASP.NET Core, C#) │                                             │
└──────┬───────┬──────┘                                             │
       │       │                                                    │
       │       └──────────────► SAP B1 Service Layer (REST)  ◄──────┘  primary
       │                        SAP B1 DI-API                          fallback only
       ▼
┌─────────────────────┐
│  MEMS DB            │  SQL Server 2019 — its OWN instance,
│  (SQL Server 2019)  │  separate from SAP B1 company DB and Attendance/Payroll DB
└─────────────────────┘
```

## Projects

| Project | Type | Responsibility |
|---|---|---|
| `Mems.Api` | ASP.NET Core Web API | Controllers/endpoints. Thin — no business logic, no SAP or DB calls. |
| `Mems.Application` | Class library | Use-cases, services, DTOs, validators. Orchestrates Domain + Infrastructure. |
| `Mems.Domain` | Class library | Entities, entitlement rules, value objects. No external dependencies. |
| `Mems.Infrastructure` | Class library | Data access, SAP B1 clients (Service Layer + DI-API), repositories. |
| `frontend` | React + TS PWA | Offline claim entry, approvals, entitlement views. |

**Dependency direction:** `Api → Application → Domain`, with `Infrastructure → Application/Domain`
and `Api` composing Infrastructure at startup via DI. Domain depends on nothing.

## Key architectural constraints (from CLAUDE.md)

- **Separate instances.** MEMS DB, SAP B1 company DB, and Attendance/Payroll DB are distinct
  SQL Server instances. No cross-DB joins without explicit approval; integrate via API.
- **SAP writes go through the Service Layer.** DI-API is isolated behind the same interface
  so it can be disabled. Never raw `INSERT/UPDATE/DELETE` against SAP B1 tables.
- **Idempotent posting.** Every SAP write is guarded against double-posting. The client-generated
  claim UUID is the idempotency key; the returned SAP `DocEntry`/`DocNum` is stored and checked
  before any re-post on retry or offline sync.
- **T-SQL only.** SQL Server 2019 dialect. Never HANA SQL.
- **Offline-first.** Unstable network is the default assumption, not an edge case.

## Open questions

- [ ] Authentication method — AD / Entra ID / local accounts? Drives the authorization model in §10.
- [ ] Is the Attendance/Payroll DB the source of truth for employee master data, or is SAP B1?
- [ ] Hosting target for the API (IIS on-prem? Kestrel + reverse proxy? Container?).
