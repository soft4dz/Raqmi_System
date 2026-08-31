namespace RaqmiSystem.Domain.Reporting;

/// <summary>
/// Kind of value a report parameter accepts. The desktop client renders the input control from
/// this type (date picker for Date, hotel-unit picker for HotelUnit), and the reporting service
/// parses the raw string value accordingly.
/// </summary>
public enum ReportParameterType
{
    Date = 1,
    HotelUnit = 2
}
