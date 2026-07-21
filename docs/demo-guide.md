# MEMS — Demo Guide (management preview)

> Status: **runnable end-to-end demo.** Real API + real persistence + the full approval
> hierarchy, wired to the mobile PWA. Some pieces are deliberately mocked for the demo — see
> "Demo shortcuts" at the bottom. Nothing here is production-ready auth or storage yet.

## Run it

```bash
# 1) Backend API (http://localhost:5046)
dotnet run --project backend/Api --launch-profile http

# 2) Frontend PWA (Vite dev server; proxies /api → :5046)
cd frontend
npm install
npm run dev            # http://localhost:5173
```

Open the frontend URL. On the sign-in screen, tap a **demo account** chip (or type the email and
any 6-digit code):

| Role | Email | What they see |
|---|---|---|
| Employee | `ayesha@waves.com.pk` | Home / Submit / Claims |
| Line Manager | `manager@waves.com.pk` | Approvals queue (stage 1) |
| Admin/HR | `hr@waves.com.pk` | Approvals queue (stage 2) |
| Finance | `finance@waves.com.pk` | Approvals queue (stage 3) + SAP posting |
| Director (ED) | `ed@waves.com.pk` | Top-Level queue (over-threshold claims) |

The OTP code is not emailed — **any 6 digits** work in the demo.

## The approval hierarchy

A claim moves through a sequential chain; each stage decides **every line** individually
(approve in full / reduce the amount / reject with a reason):

```
Employee submits
      │
      ▼
Line Manager → Admin/HR → Finance ─┬─(approved total ≤ Rs 25,000)→ Completed
      ▲                            └─(approved total >  Rs 25,000)→ Top-Level → Completed
      │                                                                 │
   (any line rejected → returned to employee to edit & resubmit,        ▼
    which restarts the whole chain — prior history is kept)      Finance records the
                                                                 SAP reference → Posted
```

- **Per-line decisions.** A stage can approve, reduce, or reject each line; a reduction carries
  forward as the amount the next stage sees.
- **Value threshold.** Claims whose approved total exceeds the configurable threshold
  (`Approval:TopLevelThreshold`, default **Rs 25,000**) require the Top-Level Approver after Finance.
- **Rejection** sends the claim back to the employee; editing & resubmitting restarts at the Line
  Manager and **keeps the prior history** (audit requirement).
- **Manual SAP posting.** MEMS never posts to SAP itself (docs/CLAUDE.md §7). Once a claim is
  approved, Finance re-enters it into SAP by hand and records the reference here → status **Posted**.
- **Audit trail.** Every stage action is an append-only history entry (who, when, what), visible on
  each claim.

## Suggested demo script (2 minutes)

1. **Employee** (`ayesha@`): show the dashboard (Rs 34,500 remaining of Rs 50,000) and the claims
   list — one claim is already Posted with its SAP reference and full history. Submit a new claim.
2. **Line Manager** (`manager@`): open the queued claim, approve/reduce a line, submit → it leaves
   your queue.
3. **Admin/HR** (`hr@`) then **Finance** (`finance@`): approve down the chain.
4. **Finance**: the completed claim now appears "To post" — enter a SAP reference and mark it Posted.
5. Back as the **Employee**: the claim now shows **Posted to SAP** with the reference and the
   complete approval history.

## Demo shortcuts (NOT production)

These are deliberate, isolated shortcuts so the demo runs anywhere with no setup. Each has a clear
production replacement:

| Demo | Production |
|---|---|
| **EF Core InMemory** database (resets on API restart) | SQL Server 2019 via the SqlServer provider + migrations (CLAUDE.md §3). The DbContext/repositories are provider-agnostic. |
| **Mock OTP** (any 6-digit code; bearer token = email) | Real email OTP + signed, expiring tokens (CLAUDE.md §10). |
| **Seeded fictional employee** (Ayesha, Grade M2) and demo approvers | Attendance/Payroll directory feed + real identities. |
| **Online-only submission** | Re-enable the offline Dexie queue + sync engine (already scaffolded) for unstable-network capture. |

## Still open (policy, not code)

Unchanged from `docs/entitlement-rules.md`: the exact extra-approval threshold figure and whether
it is evaluated per line or per claim total; M-grade consultation cap; submission deadline; whether
the monthly-vs-annual Basic base is correct. The demo uses reasonable placeholders for these and
flags them.
