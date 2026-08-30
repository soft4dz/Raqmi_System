using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Tariffs;

/// <summary>
/// A pricing plan owned by one hotel unit. Each unit designates at most ONE default active plan
/// (the plan nightly-rate resolution falls back to when no customer convention applies); the
/// invariant is guaranteed by the filtered unique index ux_rate_plans_default_per_unit (see
/// <c>RatePlanConfiguration</c>), which only constrains rows where is_default AND is_active.
/// </summary>
public sealed class RatePlan : AuditableEntity
{
    private RatePlan()
    {
    }

    public RatePlan(string code, string label, string hotelUnitCode, bool isDefault = false)
    {
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), 160);
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        IsDefault = isDefault;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>
    /// True when this plan is THE default plan of its unit. Only meaningful together with
    /// <see cref="IsActive"/>: an inactive plan keeps its flag as dormant history, but the
    /// filtered unique index only enforces uniqueness among ACTIVE defaults, and resolution
    /// only ever selects an active default.
    /// </summary>
    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(string label)
    {
        Label = RequireValue(label, nameof(label), 160);
    }

    public void SetAsDefault()
    {
        IsDefault = true;
    }

    public void ClearDefault()
    {
        IsDefault = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string value)
    {
        return RequireValue(value, nameof(value), 40).ToUpperInvariant();
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
