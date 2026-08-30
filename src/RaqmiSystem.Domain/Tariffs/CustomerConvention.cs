using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Tariffs;

/// <summary>
/// A negotiated agreement binding a customer to a <see cref="RatePlan"/> over a validity window,
/// optionally with a percentage discount on the plan's nightly rates.
///
/// <para>
/// INVARIANT (enforced by <c>TariffService</c>, because it spans rows): a customer has at most
/// one ACTIVE convention covering any given day - two active conventions of the same customer
/// must never have intersecting validity windows (bounds inclusive), or resolution would have
/// two answers for the same night. Deactivated conventions are kept as history and do not count.
/// </para>
/// </summary>
public sealed class CustomerConvention : AuditableEntity
{
    private CustomerConvention()
    {
    }

    public CustomerConvention(
        string customerCode,
        string ratePlanCode,
        decimal? discountPercent,
        DateOnly fromDate,
        DateOnly toDate)
    {
        CustomerCode = Customer.NormalizeCode(customerCode);
        ApplyTerms(ratePlanCode, discountPercent, fromDate, toDate);
        IsActive = true;
    }

    public string CustomerCode { get; private set; } = string.Empty;

    public string RatePlanCode { get; private set; } = string.Empty;

    /// <summary>
    /// Optional discount in percent (0..100, at most 2 decimal places) applied to the nightly
    /// rate resolved from the convention's plan. Null means the convention only selects the
    /// plan, at its full price.
    /// </summary>
    public decimal? DiscountPercent { get; private set; }

    public DateOnly FromDate { get; private set; }

    public DateOnly ToDate { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateTerms(string ratePlanCode, decimal? discountPercent, DateOnly fromDate, DateOnly toDate)
    {
        ApplyTerms(ratePlanCode, discountPercent, fromDate, toDate);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public bool Covers(DateOnly night)
    {
        return FromDate <= night && night <= ToDate;
    }

    /// <summary>
    /// True when both conventions belong to the same customer and are valid on at least one
    /// common day (bounds inclusive - same semantics as <see cref="RatePeriod.Overlaps"/>).
    /// </summary>
    public bool OverlapsValidity(CustomerConvention other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return CustomerCode == other.CustomerCode
            && FromDate <= other.ToDate
            && other.FromDate <= ToDate;
    }

    private void ApplyTerms(string ratePlanCode, decimal? discountPercent, DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException("The convention's from date cannot be after its to date.", nameof(fromDate));
        }

        RatePlanCode = RatePlan.NormalizeCode(ratePlanCode);
        DiscountPercent = RequireDiscountPercent(discountPercent);
        FromDate = fromDate;
        ToDate = toDate;
    }

    private static decimal? RequireDiscountPercent(decimal? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value.Value, "Discount percent must be between 0 and 100.");
        }

        if (decimal.Round(value.Value, 2) != value.Value)
        {
            throw new ArgumentException("Discount percent cannot carry more than 2 decimal places.", nameof(value));
        }

        return value.Value;
    }
}
