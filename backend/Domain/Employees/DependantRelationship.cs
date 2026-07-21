namespace Mems.Domain.Employees;

/// <summary>
/// Who a dependant is to the employee. Only spouse and children are covered dependants
/// (docs/entitlement-rules.md § Coverage — Dependants).
/// </summary>
public enum DependantRelationship
{
    Spouse = 1,
    Child = 2,
}
