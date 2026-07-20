# MEMS — Medical Entitlement Management System

A medical-entitlement claim system for employees, built **around** SAP Business One 10.00 FP2102.
Staff submit medical claims; MEMS validates them against entitlement rules, routes them for
approval, and posts the approved financial entries into SAP B1. Claim entry works offline and
syncs when connectivity returns.

> **Status: scaffold.** The architecture is wired and runs end-to-end. No entitlement logic,
> no database, and no SAP posting exist yet — those are blocked on business decisions listed
> in [`/docs`](docs/).

## Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API (C#), .NET 8 LTS |
| Frontend | React + TypeScript PWA (Vite) |
| Offline store | Dexie.js over IndexedDB |
| MEMS DB | SQL Server 2019 (its own instance) |
| SAP B1 | Service Layer (REST) primary, DI-API fallback |

## Quick start

```bash
# Prerequisites: .NET SDK 8.0.x  (winget install Microsoft.DotNet.SDK.8)
#                Node.js 20+

# Terminal 1 — API on https://localhost:7112
dotnet run --project backend/Api --launch-profile https

# Terminal 2 — PWA on http://localhost:5173
cd frontend && npm install && npm run dev
```

Open <http://localhost:5173>. The status page reports whether the PWA shell, the network,
the API, and the offline queue are each live. Swagger is at <https://localhost:7112/swagger>.

If the browser rejects the API certificate, run `dotnet dev-certs https --trust`.

## Layout

See [`CLAUDE.md` §4](claude.md) for the full tree and the project reference graph.

```
/backend    ASP.NET Core Web API — the only component that talks to SAP B1 or any database
/frontend   React + TypeScript PWA — talks only to the MEMS API
/docs       architecture, SAP mapping, entitlement rules
```

## What's decided vs. open

**Decided:** .NET 8 LTS, layered backend, Service-Layer-first SAP integration, Dexie offline
queue keyed by client-generated UUID (the idempotency key that prevents double-posting).

**Open — these block real work:**

| Question | Blocks | Tracked in |
|---|---|---|
| Entitlement rules (caps, categories, pro-rating, approval flow) | All claim logic | [docs/entitlement-rules.md](docs/entitlement-rules.md) |
| Which SAP object an approved claim becomes; GL accounts | All SAP posting | [docs/sap-mapping.md](docs/sap-mapping.md) |
| Dapper vs. EF Core | All persistence | [backend/Migrations/README.md](backend/Migrations/README.md) |
| Authentication method (AD / Entra ID / local) | All authorization | [docs/architecture.md](docs/architecture.md) |

Nothing above is guessed. Per `CLAUDE.md` §1.6, entitlement amounts, GL accounts, and
approval flows are decisions, not inferences — a placeholder number in a medical entitlement
system is a defect waiting to ship.

## Ground rules

Read [`CLAUDE.md`](claude.md) before contributing. The short version:

- T-SQL only (SQL Server 2019). Never HANA SQL.
- Never write directly to the SAP B1 database — Service Layer only.
- Never hardcode secrets or GL account codes.
- Never put real medical/employee data in code, tests, or fixtures.
- Never post a claim to SAP twice — guard every write with the idempotency key.
- Never assume the network is reliable.
