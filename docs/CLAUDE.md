# CLAUDE.md — Medical Entitlement Management System (MEMS)

> Project-context file for Claude Code. Read this fully before touching any file.
> Owner: Umer Ghauri (CIO). In-house team, Waves. Standalone application — MEMS does not
> integrate with SAP Business One. See §7.

---

## 1. Non-negotiable rules (read first)

1. **SQL is always T-SQL for Microsoft SQL Server 2019.** NEVER write SAP HANA / HANA-SQL syntax. No `SELECT ... FROM DUMMY`, no HANA-specific functions. Use `TOP`, `ISNULL`, `GETDATE()`, `TRY_CAST`, window functions, etc.
2. **Never hardcode secrets.** Connection strings, OTP email-sender credentials, API keys, and tokens live in `appsettings.*.json` (git-ignored) / user-secrets / environment variables — never in source, never in comments, never in commits.
3. **Never invent or paste live data.** No real patient/employee/medical/financial data in code, tests, or fixtures. Use clearly fake, anonymized samples.
4. **MEMS does not read from or write to SAP B1.** There is no Service Layer client, no DI-API client, no automated posting. Finance re-enters each Finance-approved claim into SAP B1 manually, outside MEMS, and records the resulting SAP reference number back in MEMS (§7). If a future phase adds live SAP integration, that is a scope change requiring a new decision — do not build toward it speculatively.
5. **MEMS has its own database.** It is a **separate SQL Server 2019 instance**, distinct from both the SAP B1 company DB and the Attendance/Payroll DB. Do not couple schemas across instances; integrate via API/queries, not cross-DB joins unless explicitly approved.
6. When unsure about business rules, entitlement logic, or approval flow — **stop and ask.** Do not guess entitlement amounts or approval routing.
7. This application must be scalable, flexible, easy to maintain.

---

## 2. What this project is

A medical-entitlement claim system for employees. Staff submit medical claims; the app
validates them against entitlement rules and routes them through a sequential approval
chain: **Line Manager → Admin/HR → Finance**, with an additional approval step for claims
above a value threshold (exact figure TBD — see `docs/entitlement-rules.md`).

Once Finance gives final approval, **Finance re-enters the claim into SAP B1 manually**,
outside MEMS. Finance then marks the claim **"Posted"** in MEMS and enters the SAP document
reference number against it — this is the audit trail linking a MEMS claim to its SAP entry.
MEMS itself never talks to SAP B1.

Frontline usage may happen on unstable network / mobile, so the claim-entry surface must
work **offline** and sync later.

**Primary goals:** correctness of entitlement calculation, reliable offline capture, a clean
auditable trail from submission through approval and manual SAP posting, delegate-ready code
the in-house team can maintain.

---

## 3. Tech stack (authoritative)

| Layer | Technology | Notes |
|---|---|---|
| Backend | **ASP.NET Core Web API (C#)**, .NET 8 LTS | REST API, the only thing that talks to the MEMS DB |
| Frontend | **React + TypeScript**, **PWA** (Vite) | Offline-capable; net-new for the team — keep it mainstream and well-commented |
| Offline store | **Dexie.js over IndexedDB** | Offline claim queue; sync to backend when online |
| MEMS DB | **SQL Server 2019**, own instance | Separate from SAP B1 DB and Attendance/Payroll DB |
| Data access | **EF Core** | Decided — do not introduce Dapper or a second ORM |
| Authentication | **Email OTP** | Employee enters official email (sourced from Attendance/Payroll DB) → time-limited one-time code sent to that email → code entry logs them in. No passwords stored. |
| SAP B1 | **None — manual.** | Finance re-enters approved claims into SAP by hand and records the SAP reference number in MEMS. |
| Reporting | Power BI, Crystal Reports, T-SQL | Existing house tooling, unaffected by MEMS |

**Team context that should shape your output:** the team is strong in C#/.NET, T-SQL, Power BI, and networking, but **React/TypeScript is new to them**. Favour clear, conventional React patterns over clever ones. Comment non-obvious frontend code. Prefer readability over brevity.

---

## 4. Repository layout

```
/backend        ASP.NET Core Web API (C#)
  /Api            controllers / endpoints
  /Domain         entities, entitlement rules, value objects
  /Application    services, DTOs, validators, use-cases
  /Infrastructure EF Core (DbContext, repositories), OTP email sender
  /Migrations     EF Core migrations (dotnet ef migrations add <Name>)
/frontend       React + TypeScript PWA
  /src
    /features     feature-sliced (claims, approvals, entitlements)
    /components   shared UI
    /offline      Dexie schema + sync engine
    /api          typed API client
    /hooks
/docs           architecture, entitlement rules, SOPs
```

---

## 5. Build / run commands

```bash
# Backend
dotnet restore
dotnet build
dotnet run --project backend/Api --launch-profile https
dotnet ef migrations add <Name> --project backend/Api
dotnet test

# Frontend
cd frontend
npm install
npm run dev
npm run build
npm run lint
npm run test
```

When you add a new command, update this section so the next session knows it.

---

## 6. Backend conventions (C# / ASP.NET Core)

- Target .NET 8 LTS; do not bump framework versions without asking.
- Layered/clean separation: controllers thin, business logic in Application/Domain, no DB calls from controllers.
- **Data access: EF Core.** Migrations via `dotnet ef migrations add`. One ORM only.
- External calls that aren't the MEMS DB — currently just the OTP email sender — go behind an interface (e.g. `IOtpEmailSender`) so they can be mocked and swapped.
- Use `async/await` end-to-end for I/O. No blocking `.Result` / `.Wait()`.
- Validate all input with a validator (FluentValidation or explicit checks). Never trust client-side validation alone.
- Return typed problem details on errors; never leak stack traces to the client.
- Log with structured logging (ILogger). Log OTP issuance/verification attempts and claim status transitions with a correlation id — no PII/medical content in logs.

## 7. SAP B1 relationship (manual — no integration)

- MEMS has **no Service Layer client, no DI-API client, and no automated posting.** This is a deliberate scope decision, not a placeholder for a future phase.
- Once a claim reaches **Finance-approved** status, Finance re-enters it into SAP B1 themselves, outside MEMS.
- Finance then marks the claim **Posted** in MEMS and supplies the **SAP reference number** (DocEntry/DocNum or equivalent). MEMS stores this against the claim as the audit trail — this satisfies the project's auditability goal without any live SAP dependency.
- A claim cannot be marked Posted without a reference number — don't allow an empty/placeholder value here; it defeats the audit trail.
- `docs/sap-mapping.md` is retained for history but is no longer an active blocker — see that file for what it now says.

## 8. Database conventions (MEMS DB — SQL Server 2019)

- All SQL is **T-SQL**. Schema changes go through EF Core migrations, never manual ad-hoc edits.
- Every claim-affecting table carries audit columns: `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt`, and `RowVersion` (EF Core concurrency token) where relevant.
- Money is `DECIMAL(18,4)` — never `FLOAT`/`REAL` for currency.
- Keep an append-only audit/history trail for claim status changes and approvals — this system is auditable by design.
- Parameterise every query. No string-concatenated SQL. No dynamic SQL unless explicitly justified and parameterised.

## 9. Frontend conventions (React + TypeScript PWA)

- **Strict TypeScript.** No `any` without a written reason. Prefer explicit types on API boundaries.
- Functional components + hooks. Keep components small; lift shared logic into hooks.
- State: local state + a lightweight store (whatever the repo already uses); don't add Redux etc. without asking.
- **Offline-first claim entry (Dexie/IndexedDB):**
  - A claim submitted offline is written to the Dexie queue with a client-generated UUID and status `pending`.
  - A sync engine flushes the queue when connectivity returns; on success it reconciles the server id and marks `synced`; on conflict it surfaces the claim for review.
  - The server must treat the client UUID as an idempotency key to avoid duplicate claims from retries.
  - Never assume the network is stable — this is an unstable-network environment by default.
- PWA: keep the service worker cache versioned; don't cache API responses that must be fresh (entitlement balances, approval status).
- Comment the offline/sync code generously — this is the riskiest and least-familiar area for the team.
- OTP login requires connectivity (email round-trip); this is expected and separate from the offline claim-entry queue.

## 10. Security, privacy & compliance

- Medical/employee data is sensitive. Enforce authorization on every endpoint (an employee sees only their claims; approvers see their queue; admins scoped by role).
- **Authentication:** email OTP. Employee's official email is sourced from the Attendance/Payroll DB (authoritative source for employee master data — confirmed). Code validity: 5 minutes, 5 attempts, standard defaults. No passwords are stored anywhere in MEMS.
- No PII/medical data in logs, error messages, or client-side storage beyond what the offline queue strictly needs.
- Operate under Pakistani regulatory context (data handling, SBP where financial flows are involved). Flag anything that looks like it needs a compliance decision rather than silently deciding.
- Encrypt secrets at rest (user-secrets/env). Use HTTPS for all API traffic.

## 11. Documentation expectations

This project values **development documentation**. When you add a feature:
- Update `/docs` (architecture note or entitlement rule) alongside the code.
- Keep this CLAUDE.md current — commands, structure, and decisions.
- Write commit messages that explain *why*, not just *what*.

---

## 12. How to work with me (Umer)

- I manage and delegate — give **decision-level, delegate-ready output**: working code, migrations, and short rationale, not long essays.
- **Push back** if my instruction has a flaw or a simpler path exists. I want honest critique.
- **Ask clarifying questions before important changes.** Do not guess entitlement logic or approval flows.
- Default to concise. Go deep only when I ask.
- Match effort to the task — don't over-engineer a small utility.

---

## 13. Things NOT to do

- ❌ Write HANA SQL.
- ❌ Build any SAP Service Layer / DI-API client — that integration was explicitly decided against; don't add it speculatively.
- ❌ Hardcode connection strings or credentials.
- ❌ Put real medical/employee/financial data in code, tests, or fixtures.
- ❌ Add heavyweight dependencies, a second ORM, or a state library without asking.
- ❌ Call the MEMS database directly from the frontend — always go through the API.
- ❌ Assume the network is reliable.
- ❌ Let a claim be marked "Posted" without a Finance-entered SAP reference number.
