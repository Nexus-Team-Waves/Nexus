# MEMS — Architecture

> Status: **scaffold**. Structure is real; business logic is not yet implemented.

## System context

MEMS is a standalone medical-entitlement claim system. It does **not** integrate with SAP
Business One. Employees submit medical claims; MEMS validates them against entitlement
rules and routes them through approval. Once Finance gives final approval, Finance re-enters
the claim into SAP B1 manually and records the resulting SAP reference number back in MEMS
as the audit trail.

```
┌─────────────────────┐
│  React + TS PWA     │  offline-capable claim entry
│  (Dexie/IndexedDB)  │  ── never talks to the DB directly ──┐
└──────────┬──────────┘                                       │
           │ HTTPS (typed API client)                         │
           ▼                                                  │
┌─────────────────────┐                                       │
│  MEMS Web API       │  the ONLY component that talks to the │
│  (ASP.NET Core, C#) │  MEMS DB. No SAP client of any kind.  │
└──────────┬──────────┘                                       │
           │                                                  │
           ▼                                                  │
┌─────────────────────┐         ┌───────────────────────────┐ │
│  MEMS DB            │         │  Attendance/Payroll DB     │◄┘
│  (SQL Server 2019)  │         │  authoritative source for  │
│  own instance       │         │  employee master data      │
└─────────────────────┘         │  (grade, Basic salary,     │
                                 │  official email for OTP)   │
                                 └───────────────────────────┘

Finance re-enters Finance-approved claims into SAP B1 manually (outside this system),
then marks the claim "Posted" in MEMS with the SAP reference number.
```

## Projects

| Project               | Type                 | Responsibility                                                               |
| --------------------- | -------------------- | ---------------------------------------------------------------------------- |
| `Mems.Api`            | ASP.NET Core Web API | Controllers/endpoints. Thin — no business logic, no DB calls.                |
| `Mems.Application`    | Class library        | Use-cases, services, DTOs, validators. Orchestrates Domain + Infrastructure. |
| `Mems.Domain`         | Class library        | Entities, entitlement rules, value objects. No external dependencies.        |
| `Mems.Infrastructure` | Class library        | EF Core (DbContext, repositories), OTP email sender. No SAP client.         |
| `frontend`            | React + TS PWA       | Offline claim entry, approvals, entitlement views.                          |

**Dependency direction:** `Api → Application → Domain`, with `Infrastructure → Application/Domain` and `Api` composing Infrastructure at startup via DI. Domain depends on nothing.

## Key architectural constraints (from CLAUDE.md)

- **Separate instances.** MEMS DB and Attendance/Payroll DB are distinct SQL Server
  instances. No cross-DB joins without explicit approval; integrate via API.
- **No SAP integration.** MEMS does not read from or write to SAP B1. Finance handles SAP
  posting manually and records the reference number back in MEMS (§7 of CLAUDE.md).
- **Employee master data source: Attendance/Payroll DB** — confirmed authoritative for
  grade, Basic salary, and official email (used for OTP login). This also feeds the
  entitlement eligibility lookup described in `docs/entitlement-rules.md`.
- **Data access: EF Core.** Migrations via `dotnet ef migrations add`.
- **Authentication: email OTP.** Code sent to the employee's official email (sourced from
  Attendance/Payroll DB); 5-minute validity, 5 attempts, standard defaults.
- **T-SQL only.** SQL Server 2019 dialect. Never HANA SQL.
- **Offline-first.** Unstable network is the default assumption for claim entry, not an
  edge case. (OTP login itself requires connectivity — that's expected and separate from
  the offline claim queue.)

## Open questions

- [ ] Hosting target for the API (IIS on-prem? Kestrel + reverse proxy? Container?).
- [ ] OTP email delivery mechanism — existing company SMTP relay, or a transactional email
      service (e.g. SendGrid)? Still needs an answer.
