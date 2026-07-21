using Mems.Domain.Employees;

namespace Mems.Domain.Claims;

/// <summary>Who a claim line's treatment was for.</summary>
public enum BeneficiaryKind
{
    Self = 1,
    Dependant = 2,
}

/// <summary>
/// The beneficiary of a single claim line: the employee themselves, or one specific dependant
/// (docs/entitlement-rules.md § Claim structure). Different lines in one claim may be for
/// different dependants. Whether a named dependant is actually <em>covered</em> is validated in
/// the Application layer against the employee's dependant list — this value object only records
/// who the line is for.
/// </summary>
public sealed record Beneficiary
{
    public BeneficiaryKind Kind { get; }

    /// <summary>Identifier of the dependant when <see cref="Kind"/> is Dependant; otherwise null.</summary>
    public string? DependantId { get; }

    /// <summary>Relationship of the dependant; null when the line is for the employee.</summary>
    public DependantRelationship? Relationship { get; }

    private Beneficiary(BeneficiaryKind kind, string? dependantId, DependantRelationship? relationship)
    {
        Kind = kind;
        DependantId = dependantId;
        Relationship = relationship;
    }

    public static Beneficiary Self { get; } = new(BeneficiaryKind.Self, null, null);

    public static Beneficiary ForDependant(string dependantId, DependantRelationship relationship)
    {
        if (string.IsNullOrWhiteSpace(dependantId))
            throw new ArgumentException("A dependant beneficiary needs an id.", nameof(dependantId));
        return new Beneficiary(BeneficiaryKind.Dependant, dependantId, relationship);
    }
}
