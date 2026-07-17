# MEMS → SAP B1 Mapping

> Status: **UNANSWERED — do not implement posting until this document is filled in.**
>
> CLAUDE.md §1.6: *"When unsure about business rules, entitlement logic, or SAP mapping — stop and ask."*
> This file is deliberately empty of guesses. Every `?` below is a decision needed from Umer / Finance.

## Target: SAP B1 10.00 FP2102, SQL Server 2019, on-prem

## 1. What object does an approved claim become?

| MEMS concept | SAP B1 object | Service Layer endpoint | Decided? |
|---|---|---|---|
| Approved medical claim | ? — Journal Entry? A/P Invoice? Outgoing Payment? Purchase Request? | ? | ❌ |
| Claim reversal / rejection after posting | ? — Credit Memo? Reversing JE? | ? | ❌ |
| Employee | ? — Business Partner? Employee master (`OHEM`)? | ? | ❌ |

**Why this matters:** the object choice drives the whole posting module. A Journal Entry
(`/JournalEntries`) and an A/P Invoice (`/PurchaseInvoices`) have completely different
payloads, approval implications, and reversal semantics.

## 2. GL accounts

**No account codes are to appear in source.** Once decided, they live in configuration
(`appsettings.json`, git-ignored overrides per environment) and are referenced by name.

| Purpose | GL account | Decided? |
|---|---|---|
| Medical expense (debit) | ? | ❌ |
| Employee payable / clearing (credit) | ? | ❌ |
| Cost centre / dimension per employee's department | ? | ❌ |

## 3. Posting identity & idempotency

- Client-generated claim UUID is the idempotency key end-to-end.
- Where is it stored on the SAP document so a retry can detect a prior post?
  - Candidate: a UDF (e.g. `U_MEMS_ClaimId`) on the target object. **Needs creating in SAP — approved?** ❌
- MEMS stores the returned `DocEntry` / `DocNum` against the claim and checks it before re-posting.

## 4. Numbering & series

- Which SAP document series should MEMS post into? ? ❌
- Does Finance require a dedicated series so MEMS postings are separable in reporting? ? ❌

## 5. Open questions

- [ ] Which SAP object represents an approved claim? *(blocks all posting work)*
- [ ] GL accounts + cost-centre/dimension rules.
- [ ] Is a `U_MEMS_ClaimId` UDF acceptable, or must idempotency be tracked MEMS-side only?
- [ ] Posting timing — immediately on final approval, or batched (e.g. nightly / with payroll)?
- [ ] Who is the SAP Service Layer service account, and what are its minimum permissions?
