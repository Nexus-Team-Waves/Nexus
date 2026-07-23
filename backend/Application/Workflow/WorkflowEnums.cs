namespace Mems.Application.Workflow;

/// <summary>Who a signed-in user is, for routing and authorization.</summary>
public enum UserRole
{
    Employee = 0,
    LineManager = 1,
    AdminHr = 2,
    Finance = 3,
    TopLevel = 4, // ED / final approver
}

/// <summary>
/// Where a claim currently sits in the sequential approval chain
/// (docs/entitlement-rules.md § Approval flow): Line Manager → Admin/HR → Finance →
/// Top-Level (only when over the value threshold). Two terminal-ish states: Completed
/// (fully decided, may still need SAP posting) and ReturnedToEmployee (a line was rejected).
/// </summary>
public enum ApprovalStage
{
    LineManager = 0,
    AdminHr = 1,
    Finance = 2,
    TopLevel = 3,
    Completed = 4,
    ReturnedToEmployee = 5,

    /// <summary>
    /// The Top-Level Approver rejected every item — final. Not editable, never postable
    /// (decision 2026-07-23). Values are persisted ints: append only, never renumber.
    /// </summary>
    Closed = 6,
}

/// <summary>The decision on a single line at the current stage.</summary>
public enum LineDecisionStatus
{
    Pending = 0,
    Approved = 1,   // approved for the full amount in play at this stage
    Reduced = 2,    // approved for a lower amount
    Rejected = 3,
}

/// <summary>Derived, claim-level status shown to users (never stored — computed from stage/lines).</summary>
public enum ClaimOverallStatus
{
    InReview = 0,
    PartiallyApproved = 1,
    Approved = 2,
    ActionNeeded = 3,
    Posted = 4,
    /// <summary>Closed by the Top-Level Approver with every item rejected — final.</summary>
    Rejected = 5,
}
