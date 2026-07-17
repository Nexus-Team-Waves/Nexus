# Entitlement Rules

> Status: **UNANSWERED — no rules implemented.**
>
> CLAUDE.md §1.6 / §12: do not guess entitlement amounts or approval flows.
> Correctness of entitlement calculation is the project's stated #1 goal, so every
> rule here must come from policy, not inference.

## Open questions

### Entitlement definition
- [ ] What determines an employee's entitlement — grade/band, tenure, department, contract type?
- [ ] Is entitlement an annual cap, per-claim cap, per-dependant cap, or a combination?
- [ ] What is the entitlement period? Calendar year, fiscal year, or employment anniversary?
- [ ] Do unused amounts carry over? If so, capped at what, and expiring when?

### Coverage
- [ ] Which claim categories exist (consultation, medicine, hospitalisation, dental, optical, …)?
- [ ] Are there per-category sub-limits?
- [ ] Are dependants covered? If so, who counts as a dependant, and is their pool shared or separate?
- [ ] Co-pay / deductible — percentage or fixed? Applied before or after the cap?

### Lifecycle edge cases
- [ ] Mid-year joiners — pro-rated or full entitlement?
- [ ] Leavers — are in-flight claims honoured? What happens to submitted-but-unapproved claims?
- [ ] Grade change mid-period — recalculate, or entitlement locked at period start?
- [ ] Backdated claims — how old can a claim be and still be valid?

### Approval flow
- [ ] Who approves, and in what sequence (line manager → HR → Finance)?
- [ ] Are there value thresholds that change the approval path?
- [ ] Can an approver reject partially (approve PKR X of a PKR Y claim)?
- [ ] What happens when an approver is on leave — delegation? auto-escalation after N days?

### Money
- [ ] Currency — PKR only, or multi-currency? (Storage is `DECIMAL(18,4)` per §8 regardless.)
- [ ] Rounding rule at the cap boundary.

## Notes

- Nothing in `Mems.Domain` implements entitlement logic yet. The scaffold intentionally
  contains no placeholder amounts, because a placeholder number in a medical entitlement
  system is a defect waiting to be shipped.
