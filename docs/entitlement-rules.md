# Entitlement Rules

> Status: **mostly decided.** Source: Waves Medical Policy (QR/HR/POL-002, dated 01-06-2017,
> confirmed current as of July 2026), plus a detailed business-rules questionnaire answered
> 2026-07-21. A handful of small implementation details remain open — see bottom.
> Per CLAUDE.md §1.6, nothing below is inferred; every figure traces to the policy document
> or an explicit decision from Umer.

## Scope

- MEMS covers **Grade M and Grade E (E+ Plus other) employees only.**
- Worker & Staff up to Grade S are covered by Social Security, not company reimbursement —
  **out of scope for MEMS.**
- Eligibility is sourced from a maintained **entitlement lookup** (grade, join date, dependant
  info), fed by the **Attendance/Payroll DB — confirmed authoritative for employee master
  data.** Salary figures specifically follow the separate rules below (§ Salary & entitlement
  base), since M grade and non-M grades are sourced differently.
- M1–M3 / M4+: **permanent employees only.**
- E1–E5: **includes probation/contract staff** (extends beyond the literal 2017 policy
  wording, which only states "permanent" for the M-grade section — recorded as an explicit
  decision, not a policy quote).

## Salary & entitlement base

**The system never calculates entitlement from payroll's monthly salary feed directly.**
Entitlement is always: `Basic Monthly Salary × Entitlement Factor = Annual Entitlement`.

| Grade | Entitlement factor |
|---|---|
| M4 & above | 1× Basic Monthly Salary |
| M1 – M3 | 1.5× Basic Monthly Salary |
| E1 – E5 | 2× Basic Monthly Salary |

*(This resolves the earlier monthly-vs-annual ambiguity: the base is explicitly the*
*employee's **monthly** Basic salary, multiplied by the factor to produce the **annual***
*entitlement.)*

### M grade — Entitled Salary mechanism

- M grade employees don't have their Basic Monthly Salary uploaded directly. Instead, the
  **Application Administrator manually enters an "Entitled Salary"** — a single annual figure,
  valid for the whole policy year.
- `Basic Monthly Salary = Entitled Salary × (2 / 3)`, then multiplied by the grade's factor
  above to get the annual entitlement.
- **Confidentiality — enforced at both API and UI layers:**
  - Entitled Salary is **encrypted at rest.**
  - Viewable only by: **the employee (claimant)** and **their Line Manager and Finance
    approvers.**
  - **The Admin/HR approval stage must never be able to view it** — confirmed this is the
    same "Admin Department" restriction, i.e. the Admin/HR step in the approval workflow.
  - **Design consequence:** the Admin/HR approval screen must let that stage decide on a
    claim (approve / reduce / reject) using computed values only — remaining balance,
    whether the claim exceeds the cap — **without exposing the Entitled Salary or the
    derived Basic Monthly Salary figure.** Line Manager and Finance screens can show the
    real number; Admin/HR's screen cannot.

### Non-M grade — Excel-based salary management

- Basic Monthly Salary is **uploaded via Excel** and used as-is (no 2/3 conversion — that
  conversion is an M-grade-only mechanism). *(Assumption — Section 3 of the questionnaire
  reuses the term "Entitled Salary" generically for increments across all grades; I'm reading
  that as loose terminology rather than a rule that non-M grades also route through the 2/3
  conversion. Flag if that's wrong.)*
- UI needed: upload screen, view-uploaded-salary-history screen, validation for duplicate
  uploads and invalid employee codes.

### Salary increments (all grades)

- An employee can receive **multiple increments in the same policy year.**
- Each increment: employee, new salary figure, effective date.
- On an increment: recalculate Basic Monthly Salary (via the 2/3 conversion if M grade, or
  directly if not) → recalculate annual entitlement → **prorate the remaining entitlement**
  from the effective date to year-end, using the same daily-basis method as joiners:
  `(days remaining ÷ 365) × new annual entitlement`.
- **Entitlement already consumed before the increment is preserved** — the revised
  (higher or lower) entitlement applies only to the remaining balance, not retroactively.
- UI needed: manual increment-entry screen, Excel upload for increments, increment history,
  effective-date validation, support for multiple increments per employee per year.

This is a **third proration scenario**, alongside:
1. Mid-year joiner — `(days employed ÷ 365) × annual entitlement`
2. Mid-year grade change — recalculates immediately at the new grade (netting mechanism vs.
   already-used amount still to work out at build time)
3. Mid-year salary increment — as above

## Claim structure

A claim is a header with one or more line items:

- **Claim (header):** employee, submission date, overall status (derived from its lines),
  total claimed, total approved.
- **Claim line (repeatable, one or more per claim):**
  - **For:** self, or a specific dependant. Lines in the same claim can be for different
    dependants.
  - **Category:** OPD, hospitalization, maternity, dental, or doctor/Hakim/homeopath
    consultation. Lines in the same claim can be different categories.
  - **Claimed amount** and **approved amount** (null until a stage acts on this line).
  - **Line status:** Pending → Approved / Partially approved / Rejected.
  - **Rejection reason:** required text when a line is rejected.
  - **Receipt attachments:** photos (device camera) or uploaded files — **1 to 5 images per
    line, at least one required** (decision 2026-07-23); enforced client-side and in
    `SubmitClaimRequestValidator`. Image bytes (JPEG/PNG, ≤ 5 MB each) are uploaded and
    stored; the claim line carries the stored receipt ids. Visible to the owner and every
    approver; approvers can export each line as a PDF (first image with the details, one
    page per additional image). Claims predating image upload degrade gracefully
    (details-only PDF, "no stored image" note).
  - **Bill date (2026-07-23):** the employee must enter the DATE OF THE BILL per item —
    distinct from the submission date; cannot be in the future (client) or after the
    submission date (server).
  - **Duplicate-bill warning (2026-07-23):** if an item's bill date + amount + category
    match a line on any of the employee's other claims, the app warns and asks "submit
    anyway?" — a confirmation, never a block (two identical bills can be legitimate).
  - **Days-elapsed indicator:** show days between expense date and submission date (see
    Claim submission timing below) — informational, not a validation gate.
- **Claim-level totals:** `Total claimed` = sum of claimed amounts. `Total approved` = sum of
  approved amounts (Pending lines contribute nothing yet).
- **Overall claim status** (derived): any line Rejected → **Action needed**; all lines
  Approved → **Approved**; any line Pending (none rejected) → **In review**; otherwise
  → **Partially approved**.

## Coverage

- **Categories:** OPD, hospitalization, maternity, limited dental, doctor/Hakim/homeopath
  consultation.
- **Explicitly excluded:** spectacles/contact lenses (except cataract-surgery lenses), and a
  long exclusion list — cosmetics, supplements, energy drinks, toiletries, dietary items,
  medical equipment, dentures, cosmetic dental work.
- **Sub-limits:**
  | Item | M1–M3 | M4+ | E1–E5 |
  |---|---|---|---|
  | Room rate cap | Rs 6,000/day | Rs 8,000/day | Rs 3,000/day |
  | Consultation fee cap | **Configurable — see below** | **Configurable — see below** | **Configurable — see below** |
  | Medicine purchase limit | 15 days' supply (1 month for chronic conditions) — all grades | | |
  | Prescription-exempt threshold | Bills ≤ Rs 1,000 don't require a prescription — all grades | | |
- **Consultation fee limits are now config-driven, not hardcoded, for every grade** —
  including E1–E5, whose Rs 200/day/patient becomes the initial configured value rather than
  a fixed constant. Admin screen defines: Employee Grade, Consultation Fee Limit, Effective
  Date, Active/Inactive status. The approval screen shows the configured limit, the claimed
  amount, and whether the claim exceeds it — approvers can still approve over-limit claims
  per their workflow permissions (soft limit, not a hard block).
- **Dependants:** spouse + children ≤19 (≤23 if unmarried full-time student). Shared pool
  with the employee, not separate.
- **Co-pay:** none — 100% reimbursement of actual expense up to the cap.
- Sub-limits apply **per claim line**, based on that line's category.

## Claim submission timing

- **No automatic rejection based on submission date.** The system displays the number of
  days elapsed between expense date and submission date to the approver; the approver
  decides whether to approve or reject a late submission. This replaces the earlier
  15-days-vs-following-month conflict in the source policy — it's now informational only,
  not a validation gate.

## Approval flow

- **Standard claim:** sequential — **Line Manager → Admin/HR → Finance.**
- **Approval is per line, not per claim.** At each stage, the approver evaluates every line
  individually: approve in full, reduce (partial approval), or reject with a reason.
- **Stage capability — Line Manager cannot reduce (decision 2026-07-23):** the Line Manager
  (HOD) stage approves in full or rejects with a reason only. Reduction is available from
  Admin/HR onward. Enforced server-side; the Reduce option is hidden in the Line Manager UI.
- **Reducing requires a comment (2026-07-23):** any amount change (reduce, or a Top-Level
  adjustment) must carry a comment — enforced server-side and in the UI.
- **Top-Level can adjust up to the claim (2026-07-23):** the Top-Level Approver may set a
  line's amount to anything from just above zero up to the ORIGINALLY CLAIMED amount —
  including restoring an amount an earlier stage reduced. An adjustment back to the full
  claim reads as Approved; anything below reads as Reduced.
- **Top-Level decisions are final (2026-07-23):** a claim never returns to the employee from
  the Top-Level stage. Rejected items are closed permanently (no resubmit); approved items
  complete normally and Finance posts them. If every item is rejected the claim moves to a
  terminal **Closed** stage — not editable, never postable.
- **Receipts visible to every approver:** each line's receipt image can be viewed inline and
  downloaded as a **per-line PDF** (line details + image) at every approval stage.
- **Per-line audit trail & comments (2026-07-23):** every submit/resubmit and every per-line
  decision (approved/reduced amount, rejection reason) is recorded as an append-only line
  event, with an optional free-text comment from the initiator or the approver. The full
  trail is visible to the employee and to every stage.
- **Admin/HR escalation (2026-07-23):** Admin/HR may forward a claim **directly to the
  Top-Level Approver, skipping Finance review** (regardless of the threshold). Finance still
  records the SAP posting after completion. The flag applies per decision round — a claim
  returned and resubmitted must be forwarded again or it follows the normal path.
- **Finance is split by grade (2026-07-23):** claims of **M-grade** employees are reviewed and
  posted by the authority signed in as `Approval:FinanceMGradeEmail` (default
  mapproval@waves.com.pk); all other claims by `Approval:FinanceOtherEmail` (default
  otherapproval@waves.com.pk). Both addresses are configuration — changeable without a code
  change. Each authority sees only its own queue.
- **Resubmit keeps approvals (2026-07-23):** when a claim is returned because a line was
  rejected, the lines already approved/reduced are **locked** — the employee cannot change
  them, lines cannot be added or removed, and no stage decides them again. Only the edited
  (previously rejected) lines restart at Line Manager. *Accepted consequence:* a line approved
  at the same stage where a sibling was rejected does not revisit the later stages.
- **Stage-wise notifications (2026-07-23):** the approver whose queue a claim enters is
  notified by Web Push (arrives even with the browser closed); the employee and all signed-in
  users see in-app bell/toast notifications for returns, approvals, and postings.
- **Extra approval for large claims:** no fixed Rs threshold. The **threshold amount is
  admin-configurable** (read from config at runtime, changeable without a code deploy).
  Claims exceeding it require an additional stage: **Top-Level Approver** (also referred to
  as "Final Approval" — confirmed these are the same role/step) after Finance.
  *(Still open: whether the threshold is evaluated per line or per claim total.)*
- **Approver on leave:** pending claims are **manually reassigned by admin** — no
  auto-escalation logic needed.

### Medical Advance from Next Year's Entitlement

Replaces/expands the original policy's brief "emergency advance" clause:

- If a claim is approved (by the Top-Level Approver) for more than the employee's **available
  entitlement for the current year**, the excess is recorded as an **Advance Against Next
  Year's Medical Entitlement** — a distinct, tracked amount linked to the next policy year.
- At the start of the next policy year, the employee's available entitlement is
  **automatically reduced** by the outstanding advance.
- Employee and all approvers must be able to see, clearly: **Current Year's Entitlement**,
  **Advance Consumed from Next Year**, and **Remaining Balance After Deduction.**
- **Audit trail required:** original approval, advance amount, the year borrowed from, and
  date of adjustment.
- **No carry-forward of unused entitlement** — the only thing that crosses a policy-year
  boundary is an approved advance.

**Worked example:**
| | Amount |
|---|---|
| 2026 entitlement | Rs 300,000 |
| Employee's approved claims in 2026 | Rs 340,000 |
| Advance approved from 2027 | Rs 40,000 |
| 2027 entitlement | Rs 300,000 |
| **Available balance at start of 2027** | **Rs 260,000** |

*(Minor open point, not blocking: the original 2017 policy required ED approval **with the
consent of HOD** for this kind of advance. The new rules describe approval by the "highest-
level approver" without restating the HOD-consent step. Working assumption: HOD consent is
still required alongside Top-Level Approver sign-off, since nothing said to drop it — flag
if that's wrong.)*

## Editing rules

- **Editable while Pending:** an employee can edit a claim before the Line Manager has acted
  on it.
- **Editable if any line is rejected:** the employee can edit the claim to address the
  rejection reason (rejections most often happen at the Admin/HR stage) and resubmit.
- **Resubmission restarts the entire claim at Line Manager** — confirmed: this applies to
  every line in the claim, **including lines that were already approved at Admin/HR or
  Finance**, not just the rejected line. The whole hierarchy runs again:
  `Employee → Line Manager → Admin/HR → Finance → (Top-Level Approver if over threshold)`.
- **Locked once approved with no rejections:** if no line has been rejected, the claim is
  locked for editing once the Line Manager has approved any of its lines.
- **Audit requirement:** the approval history of previous (pre-resubmission) submissions must
  remain available for audit — resubmission doesn't erase prior history, it adds a new cycle.

## Money

- **Currency:** PKR only.
- **Storage:** `DECIMAL(18,4)` per CLAUDE.md §8 — this also comfortably handles the M-grade
  2/3 conversion, which can produce non-round figures.
- **Rounding at cap boundary:** round to nearest.
- **M-grade Entitled Salary is encrypted at rest** — see Salary & entitlement base above.
  Worth mirroring this requirement in CLAUDE.md §10 (Security) since it's a data-protection
  rule, not just an entitlement rule — say if you'd like that file updated too.

## Open — still small implementation details, not blocking

- [ ] **Extra-approval / advance threshold:** evaluated per line or per claim total?
- [ ] **Non-M-grade increments:** confirm they set Basic Monthly Salary directly, with no 2/3
      conversion (see assumption flagged above).
- [ ] **HOD consent on advances:** confirm still required alongside Top-Level Approver
      sign-off (see assumption flagged above).
- [ ] **Mid-year grade change netting:** how amount already used under the old grade nets
      against the new entitlement (build-time detail, not blocking).

## Notes

- Source: Waves Medical Policy, QR/HR/POL-002, 01-06-2017, confirmed current as of July 2026,
  plus the business-rules questionnaire answered 2026-07-21.
- `Mems.Domain` entitlement logic can be built against everything above marked decided. The
  four small open items above should block only their specific sub-feature, not the rest of
  the claim/entitlement engine.

### Implementation status (2026-07-21, updated for the questionnaire)

Decided rules are implemented in **`Mems.Domain`** + **`Mems.Application`** (52 passing tests).
Only decided rules are encoded; open items are left as explicit, un-guessed gaps.

- **Salary base** — resolved: annual entitlement = `Basic Monthly Salary × factor`
  (`EntitlementCalculator`). M-grade `Entitled Salary → Basic Monthly = ×2/3` in `SalaryPolicy`;
  non-M uses the uploaded monthly figure as-is.
- **Proration** — one daily-basis helper (`÷365`) serves both joiners and salary increments
  (`EntitlementCalculator.ProratedEntitlementForIncrement`). Grade-change netting still open.
- **Advance against next year** — `EntitlementBalance.Allocate` splits an approved amount into
  current-year vs. advance (matches the worked example); cross-year carry/adjustment is a
  persistence concern, not yet built.
- **Consultation-fee cap** — now config-driven & soft for *all* grades: removed the hardcoded
  constant; reads through `IConsultationFeeLimitProvider` (adapter + admin screen not yet built).
- **Claims** — `Claim`/`ClaimLine` with per-line approve/partial/reject, derived overall status,
  expense date + days-elapsed data, and the **updated** editing rule (a rejection re-opens the
  whole claim for edit-and-resubmit, even alongside approved lines).
- **Application** — submit DTOs + FluentValidation (incl. expense-date sanity, not a lateness
  gate), `ClaimAssembler`, `EntitlementService` over ports whose adapters live in Infrastructure.

**Not yet built (need approver-side workflow / Infrastructure):** the Top-Level Approver stage
and advance flow, increment/advance ledgers, M-grade Entitled-Salary encryption + the Admin/HR
"never see the salary" screen restriction, and enforcement (vs. display) of sub-limits and
dependant-coverage at approval time. **Still open in policy:** the four items listed above.


