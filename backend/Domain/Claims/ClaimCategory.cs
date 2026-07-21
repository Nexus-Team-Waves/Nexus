namespace Mems.Domain.Claims;

/// <summary>
/// The covered claim-line categories (docs/entitlement-rules.md § Coverage). Each line in a
/// claim carries exactly one category; a claim may mix categories across its lines.
/// </summary>
public enum ClaimCategory
{
    /// <summary>Out-patient department.</summary>
    Opd = 1,
    Hospitalization = 2,
    Maternity = 3,

    /// <summary>Limited dental — cosmetic dental work is excluded (see policy exclusion list).</summary>
    Dental = 4,

    /// <summary>Doctor / Hakim / homeopath consultation.</summary>
    Consultation = 5,
}
