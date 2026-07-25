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
(approve in full / reduce the amount / reject with a reason — except the Line Manager,
who cannot reduce):

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
- **Line Manager cannot reduce** (decision 2026-07-23): the HOD/Line Manager stage approves in
  full or rejects with a reason; the Reduce option first appears at Admin/HR. Enforced
  server-side (403) and hidden in the Line Manager's UI.
- **Admin/HR can escalate**: a checkbox on the Admin/HR review forwards the claim **directly to
  the Top-Level Approver, skipping Finance review** (Finance still posts). Applies per round.
- **Finance is two grade-routed authorities** (config `Approval:FinanceMGradeEmail` /
  `FinanceOtherEmail`): sign in as **mapproval@waves.com.pk** for M-grade employees' claims
  (Ayesha) or **otherapproval@waves.com.pk** for others (Khalid, Grade E3). Each sees only its
  own review + posting queue. The old `finance@` account is retired.
- **Resubmit keeps approvals**: on a returned claim only the rejected items are editable
  (approved items show locked); approvers then decide only the resubmitted items.
- **Per-line timeline & comments**: every line shows "Changes & comments" — who approved,
  reduced (with amounts), or rejected (with reason) at each stage, plus optional notes from the
  employee and approvers. Reducing/adjusting an amount REQUIRES a comment.
- **Multi-image receipts**: each item takes 1–5 receipt images (chips with remove ×); the
  per-line PDF carries all of them (extra images on their own pages).
- **Bill date + duplicate warning**: the form asks for the bill's own date; submitting an item
  whose date+amount+category match an existing claim pops a "submit anyway?" confirmation.
- **ED (Top-Level) is final**: ED can ADJUST any amount up to the original claim (even undoing
  an earlier reduction), and ED rejections never return to the employee — rejected items are
  closed for good (all-rejected → claim shows "Closed — rejected (final)"); approved items
  still go to Finance for posting.
- **Desktop layout**: at ≥900px the employee app gets a left sidebar nav and card grids; below
  900px the phone layout is unchanged. Same build serves both.
- **Notifications**: approvers get Web Push (works with the browser closed — see the demo step
  below); everyone gets the in-app bell + toasts (30-second poll).
- **Receipts.** Every claim line carries an uploaded receipt image (JPEG/PNG ≤ 5 MB). Each
  approver stage can view the image inline and download a **per-line PDF** (line details +
  image). Lines seeded/submitted before image upload show "Receipt image not stored (legacy
  claim)" and still yield a details-only PDF.
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
2. **Line Manager** (`manager@`): open the queued claim — view the receipt image, download the
   per-line PDF, note there is **no Reduce button** at this stage — approve, submit → it leaves
   your queue.
3. **Admin/HR** (`hr@`): approve (can also reduce) — optionally tick "Forward directly to the
   Top-Level Approver" to skip Finance review. Then **Finance** (`mapproval@` for Ayesha's
   claims / `otherapproval@` for Khalid's): approve.
4. **Finance**: the completed claim now appears "To post" — enter a SAP reference and mark it Posted.
5. Back as the **Employee**: the claim now shows **Posted to SAP** with the reference and the
   complete approval history.

## Demo shortcuts (NOT production)

These are deliberate, isolated shortcuts so the demo runs anywhere with no setup. Each has a clear
production replacement:

| Demo | Production |
|---|---|
| **SQLite file DB** (`backend/Api/App_Data/mems-demo.db`, created via `EnsureCreated` — no migrations, so **schema changes require deleting the file**; it reseeds on start) | SQL Server 2019 via the SqlServer provider + migrations (CLAUDE.md §3). The DbContext/repositories are provider-agnostic. |
| **Receipt images stored as blobs in the DB**, orphaned uploads never cleaned up | File/blob storage outside the DB + a retention/cleanup job. |
| **Mock OTP** (any 6-digit code; bearer token = email) | Real email OTP + signed, expiring tokens (CLAUDE.md §10). |
| **Seeded fictional employee** (Ayesha, Grade M2) and demo approvers | Attendance/Payroll directory feed + real identities. |
| **Online-only submission** | Re-enable the offline Dexie queue + sync engine (already scaffolded) for unstable-network capture. |
| **VAPID keys auto-generated** into `App_Data/vapid-keys.json` (git-ignored) | Set `Push:PublicKey`/`Push:PrivateKey` via user-secrets/env. |

### Demoing push notifications (browser closed)

Web Push needs the built service worker — it is off in `npm run dev`. Either use the
API-served app (https://localhost:7112, after copying `frontend/dist` to `backend/Api/wwwroot`)
or `npm run build && npm run preview` (http://localhost:4173). Steps: sign in as `manager@`,
allow notifications when prompted, close the browser completely, then submit a claim as
`ayesha@` from another browser/curl — a Windows notification appears; clicking it opens the
app. Caveats: Chrome must be allowed to run in the background, Windows Focus Assist off, and
the push service needs outbound internet.

## Still open (policy, not code)

Unchanged from `docs/entitlement-rules.md`: the exact extra-approval threshold figure and whether
it is evaluated per line or per claim total; M-grade consultation cap; submission deadline; whether
the monthly-vs-annual Basic base is correct. The demo uses reasonable placeholders for these and
flags them.
