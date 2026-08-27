using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Revenue;

public sealed class DailyRevenue : AuditableEntity
{
    private DailyRevenue()
    {
    }

    public DailyRevenue(
        DateOnly businessDate,
        string hotelUnitCode,
        decimal accommodation,
        decimal food,
        decimal beverage,
        decimal other,
        string? notes = null)
    {
        BusinessDate = businessDate;
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Accommodation = RequirePositiveOrZero(accommodation, nameof(accommodation));
        Food = RequirePositiveOrZero(food, nameof(food));
        Beverage = RequirePositiveOrZero(beverage, nameof(beverage));
        Other = RequirePositiveOrZero(other, nameof(other));
        Notes = NormalizeOptional(notes, nameof(notes), 1000);
        Status = DailyRevenueStatus.Draft;
    }

    public DateOnly BusinessDate { get; private set; }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public decimal Accommodation { get; private set; }

    public decimal Food { get; private set; }

    public decimal Beverage { get; private set; }

    public decimal Other { get; private set; }

    public string? Notes { get; private set; }

    public DailyRevenueStatus Status { get; private set; } = DailyRevenueStatus.Draft;

    public DateTimeOffset? SubmittedAt { get; private set; }

    public string? SubmittedBy { get; private set; }

    public DateTimeOffset? ValidatedAt { get; private set; }

    public string? ValidatedBy { get; private set; }

    public string? RejectionReason { get; private set; }

    public decimal Total => Accommodation + Food + Beverage + Other;

    public bool CanEdit => Status is DailyRevenueStatus.Draft or DailyRevenueStatus.Rejected;

    public void UpdateAmounts(
        decimal accommodation,
        decimal food,
        decimal beverage,
        decimal other,
        string? notes)
    {
        if (!CanEdit)
        {
            throw new InvalidOperationException("Only draft or rejected revenue entries can be edited.");
        }

        Accommodation = RequirePositiveOrZero(accommodation, nameof(accommodation));
        Food = RequirePositiveOrZero(food, nameof(food));
        Beverage = RequirePositiveOrZero(beverage, nameof(beverage));
        Other = RequirePositiveOrZero(other, nameof(other));
        Notes = NormalizeOptional(notes, nameof(notes), 1000);

        if (Status == DailyRevenueStatus.Rejected)
        {
            Status = DailyRevenueStatus.Draft;
            SubmittedAt = null;
            SubmittedBy = null;
            ValidatedAt = null;
            ValidatedBy = null;
            RejectionReason = null;
        }
    }

    public void Submit(string userName, DateTimeOffset utcNow)
    {
        if (Status != DailyRevenueStatus.Draft)
        {
            throw new InvalidOperationException("Only draft revenue entries can be submitted.");
        }

        Status = DailyRevenueStatus.Submitted;
        SubmittedAt = utcNow;
        SubmittedBy = RequireActor(userName);
    }

    public void Validate(string userName, DateTimeOffset utcNow)
    {
        if (Status != DailyRevenueStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted revenue entries can be validated.");
        }

        Status = DailyRevenueStatus.Validated;
        ValidatedAt = utcNow;
        ValidatedBy = RequireActor(userName);
        RejectionReason = null;
    }

    public void Reject(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status != DailyRevenueStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted revenue entries can be rejected.");
        }

        Status = DailyRevenueStatus.Rejected;
        ValidatedAt = utcNow;
        ValidatedBy = RequireActor(userName);
        RejectionReason = RequireValue(reason, nameof(reason), 500);
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
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

    private static string? NormalizeOptional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }

    private static decimal RequirePositiveOrZero(decimal value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be negative.");
        }

        return value;
    }
}
