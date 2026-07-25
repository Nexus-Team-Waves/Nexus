# CLAUDE.md — Medical Entitlement Management System (MEMS)

> Project-context file for Claude Code. Read this fully before touching any file.
> Owner: Umer Ghauri (CIO). In-house team, Waves. This is a **utility app built around SAP Business One**, not a change to SAP B1 itself.

---

## 1. Non-negotiable rules (read first)

1. **SQL is always T-SQL for Microsoft SQL Server 2019.** NEVER write SAP HANA / HANA-SQL syntax. No `SELECT ... FROM DUMMY`, no HANA-specific functions. Use `TOP`, `ISNULL`, `GETDATE()`, `TRY_CAST`, window functions, etc.
2. **Never hardcode secrets.** Connection strings, SAP Service Layer credentials, API keys, and tokens live in `appsettings.*.json` (git-ignored) / user-secrets / environment variables — never in source, never in comments, never in commits.
3. **Never invent or paste live data.** No real patient/employee/medical/financial data in code, tests, or fixtures. Use clearly fake, anonymized samples.
4. **Do not modify the SAP B1 company database directly.** All SAP B1 reads/writes go through the **Service Layer (REST)**, with **DI-API as fallback only**. Never issue raw `INSERT/UPDATE/DELETE` against SAP B1 tables. Read-only T-SQL against SAP B1 is acceptable for reporting queries when explicitly asked.
5. **MEMS has its own database.** It is a **separate SQL Server 2019 instance**, distinct from both the SAP B1 company DB and the Attendance/Payroll DB. Do not couple schemas across instances; integrate via API/queries, not cross-DB joins unless explicitly approved.
6. When unsure about business rules, entitlement logic, or SAP mapping — **stop and ask.** Do not guess entitlement amounts, GL accounts, or approval flows.

---

## 2. What this project is

A medical-entitlement claim system for employees. Staff submit medical claims; the app validates them against entitlement rules, routes them for approval, and posts the approved financial entries into SAP B1. Frontline usage may happen on unstable network / mobile, so the claim-entry surface must work **offline** and sync later.

**Primary goals:** correctness of entitlement calculation, reliable offline capture, clean and auditable SAP B1 posting, delegate-ready code the in-house team can maintain.

---

## 3. Tech stack (authoritative)

| Layer | Technology | Notes |
|---|---|---|
| Backend | **ASP.NET Core Web API (C#)** | REST API, the only thing that talks to SAP B1 and the MEMS DB |
| Frontend | **React + TypeScript**, **PWA** | Offline-capable; net-new for the team — keep it mainstream and well-commented |
| Offline store | **Dexie.js over IndexedDB** | Offline claim queue; sync to backend when online |
| MEMS DB | **SQL Server 2019**, own instance | Separate from SAP B1 DB and Attendance/Payroll DB |
| SAP B1 integration | **Service Layer (REST)** primary, **DI-API** fallback | SAP B1 10.00 FP2102 on SQL Server 2019, on-prem |
| Reporting | Power BI, Crystal Reports, T-SQL | Existing house tooling |

**Team context that should shape your output:** the team is strong in C#/.NET, T-SQL, Power BI, and networking, but **React/TypeScript is new to them**. Favour clear, conventional React patterns over clever ones. Comment non-obvious frontend code. Prefer readability over brevity.

---

## 4. Repository layout

> Scaffolded 2026-07-17. Structure below is real and matches the repo.

```
global.json     pins the .NET SDK to 8.0.x — see §6
/backend
  Mems.sln
  /Api            Mems.Api            — controllers / endpoints (thin)
  /Domain         Mems.Domain         — entities, entitlement rules, value objects (no deps)
  /Application    Mems.Application    — services, DTOs, validators, use-cases
  /Infrastructure Mems.Infrastructure — persistence, SAP B1 clients (ServiceLayer + DiApi), repositories
  /Tests          Mems.Tests          — xUnit
  /Migrations     DB schema migrations (tool undecided — see backend/Migrations/README.md)
/frontend       React + TypeScript PWA (Vite)
  /src
    /features     feature-sliced (claims, approvals, entitlements)
    /components   shared UI
    /offline      Dexie schema + sync engine
    /api          typed API client
    /hooks
/docs           architecture, SAP mapping, entitlement rules, SOPs
```

**Project reference graph** (enforces §6 layering — `Domain` depends on nothing):

```
Api ──► Application ──► Domain
 └────► Infrastructure ──► Application, Domain
Tests ──► Api, Application, Domain
```

---

## 5. Build / run commands

> Verified working 2026-07-17.

```bash
# --- Backend (from repo root) ---
dotnet restore backend/Mems.sln
dotnet build   backend/Mems.sln
dotnet test    backend/Mems.sln
dotnet run --project backend/Api --launch-profile https   # https://localhost:7112
dotnet run --project backend/Api --launch-profile lan     # binds 0.0.0.0 — reachable on the LAN, e.g. http://<machine-ip>:5046 (needs inbound firewall rules for 5046/7112 once, as admin)

# Swagger UI:   https://localhost:7112/swagger
# Health check: https://localhost:7112/api/health

# --- Frontend (from /frontend) ---
npm install
npm run dev       # http://localhost:5173 — proxies /api to https://localhost:7112
npm run build     # tsc -b && vite build (also emits the service worker)
npm run preview   # serve the production build — REQUIRED to test PWA/offline behaviour and Web Push notifications
npm run lint      # oxlint
```

**Run both:** start the API first, then `npm run dev`. The app shell shows API reachability,
so a red "MEMS API — unreachable" card means the backend isn't up.

**First-run notes:**
- Requires **.NET SDK 8.0.x** (`winget install Microsoft.DotNet.SDK.8`). `global.json` pins it,
  so a machine with only .NET 9 will fail fast with a clear error rather than silently building
  on the wrong SDK.
- If the browser rejects the API's dev certificate: `dotnet dev-certs https --trust`.
- There is **no frontend test runner yet** — adding one (Vitest) is a dependency decision for Umer (§13).

When you add a new command, update this section so the next session knows it.

---

## 6. Backend conventions (C# / ASP.NET Core)

- Target the LTS .NET version already in the repo; do not bump framework versions without asking.
- Layered/clean separation: controllers thin, business logic in Application/Domain, no SAP or DB calls from controllers.
- **Data access:** prefer Dapper or EF Core (whichever the repo already uses — check `csproj` before adding a new one). Do not introduce a second ORM.
- All external calls (SAP Service Layer, DI-API) go behind an interface (e.g. `ISapClient`) so they can be mocked and swapped.
- Use `async/await` end-to-end for I/O. No blocking `.Result` / `.Wait()`.
- Validate all input with a validator (FluentValidation or explicit checks). Never trust client-side validation alone.
- Return typed problem details on errors; never leak stack traces or SAP internals to the client.
- Log with structured logging (ILogger). Log SAP posting attempts, successes, and failures with a correlation id.

## 7. SAP B1 integration rules

- **Service Layer (REST) is primary.** Authenticate via `/Login`, reuse the session cookie, handle session expiry (401 → re-login → retry once).
- **DI-API is fallback only**, used when Service Layer cannot be enabled or for operations Service Layer doesn't support. Keep DI-API code isolated so it can be disabled.
- Every write to SAP B1 must be **idempotent-safe**: guard against double-posting a claim (store the SAP DocEntry/DocNum returned, and check before re-posting on retry/sync).
- Map MEMS concepts → SAP objects explicitly in `/docs/sap-mapping.md`. Do not scatter magic account codes or object types through the code; centralise them in config.
- Read-only reporting queries against SAP B1 DB must be **T-SQL** and must not lock or block SAP transactions (use `WITH (NOLOCK)` only where dirty reads are acceptable for reporting, and say so).
- Never call the SAP DB or Service Layer from the frontend. Frontend → MEMS API → SAP.

## 8. Database conventions (MEMS DB — SQL Server 2019)

- All SQL is **T-SQL**. Schema changes go through migrations, never manual ad-hoc edits.
- Every claim-affecting table carries audit columns: `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt`, and where relevant `RowVersion` (rowversion/timestamp) for concurrency.
- Money is `DECIMAL(18,4)` (or match SAP precision) — never `FLOAT`/`REAL` for currency.
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

## 10. Security, privacy & compliance

- Medical/employee data is sensitive. Enforce authorization on every endpoint (an employee sees only their claims; approvers see their queue; admins scoped by role).
- No PII/medical data in logs, error messages, or client-side storage beyond what the offline queue strictly needs.
- Operate under Pakistani regulatory context (data handling, SBP where financial flows are involved). Flag anything that looks like it needs a compliance decision rather than silently deciding.
- Encrypt secrets at rest (user-secrets/env). Use HTTPS for all API traffic.

## 11. Documentation expectations

This project values **development documentation**. When you add a feature:
- Update `/docs` (architecture note, SAP mapping, or entitlement rule) alongside the code.
- Keep this CLAUDE.md current — commands, structure, and decisions.
- Write commit messages that explain *why*, not just *what*.

---

## 12. How to work with me (Umer)

- I manage and delegate — give **decision-level, delegate-ready output**: working code, migrations, and short rationale, not long essays.
- **Push back** if my instruction has a flaw or a simpler path exists. I want honest critique.
- **Ask clarifying questions before important changes.** Do not guess entitlement logic, GL mappings, or approval flows.
- Default to concise. Go deep only when I ask.
- Match effort to the task — don't over-engineer a small utility.

---

## 13. Things NOT to do

- ❌ Write HANA SQL.
- ❌ Write directly to the SAP B1 database.
- ❌ Hardcode connection strings, credentials, or account codes.
- ❌ Put real medical/employee/financial data in code, tests, or fixtures.
- ❌ Add heavyweight dependencies, a second ORM, or a state library without asking.
- ❌ Call SAP or the database from the frontend.
- ❌ Assume the network is reliable.
- ❌ Post a claim to SAP twice — always guard against duplicate/retry posting.