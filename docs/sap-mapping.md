# MEMS ↔ SAP B1 Relationship

> Status: **Decided — no live SAP integration.** This document previously tracked open
> questions for an automated posting design (SAP object type, GL accounts, idempotency UDF).
> That design was scrapped in favour of manual posting. Retained for history; see CLAUDE.md
> §7 for the current rule.

## What actually happens

1. A claim reaches **Finance-approved** status inside MEMS (after Line Manager → Admin/HR →
   Finance, and any extra approval for claims above the value threshold — see
   `docs/entitlement-rules.md`).
2. **Finance re-enters the claim into SAP B1 manually**, using their own judgement on SAP
   object type, GL accounts, and posting mechanics. MEMS has no opinion on any of this and
   no code touches SAP.
3. Finance returns to MEMS and marks the claim **Posted**, entering the **SAP reference
   number** (DocEntry/DocNum or equivalent) against it.
4. MEMS stores that reference number as the audit trail. A claim cannot be marked Posted
   without one.

## What MEMS does NOT do

- No Service Layer (REST) client.
- No DI-API client.
- No GL account codes anywhere in MEMS — Finance's own SAP setup owns that entirely.
- No idempotent-posting logic — there's nothing being posted, so there's nothing to
  double-post. (Claim submission idempotency, via the client-generated UUID, is unrelated
  and still applies — see CLAUDE.md §9.)

## If this changes later

If a future phase decides to add live SAP integration, that's a new architectural decision,
not a resumption of the old design in this file's git history. Revisit CLAUDE.md §4 and §7,
and treat the object-type/GL-account/idempotency questions as open again from scratch —
SAP B1 configuration may have moved on by then.



