namespace RaqmiSystem.Application.Revenue;

public sealed record DailyRevenueDraft(
    DateOnly BusinessDate,
    string HotelUnitCode,
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other)
{
    public decimal Total => Accommodation + Food + Beverage + Other;
}
