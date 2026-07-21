namespace Mems.Application.Workflow;

/// <summary>Base for expected workflow errors, so the API can map them to HTTP status codes.</summary>
public abstract class WorkflowException(string message) : Exception(message);

/// <summary>The claim does not exist → 404.</summary>
public sealed class ClaimNotFoundException(Guid id)
    : WorkflowException($"Claim '{id}' was not found.");

/// <summary>The caller may not perform this action (wrong role/owner) → 403.</summary>
public sealed class WorkflowForbiddenException(string message) : WorkflowException(message);

/// <summary>The action is invalid for the claim's current state → 409.</summary>
public sealed class WorkflowConflictException(string message) : WorkflowException(message);
