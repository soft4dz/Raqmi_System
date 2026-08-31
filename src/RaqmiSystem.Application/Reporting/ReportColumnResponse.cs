namespace RaqmiSystem.Application.Reporting;

/// <summary>
/// One column of a report result. <see cref="Type"/> is one of "text", "number", "money" or
/// "date": the desktop grid derives alignment and display formatting from it, and the raw cell
/// values stay culture-invariant (dates yyyy-MM-dd, decimals with a dot) so the same payload
/// feeds the screen and the CSV export.
/// </summary>
public sealed record ReportColumnResponse(
    string Key,
    string Label,
    string Type)
{
    public const string Text = "text";

    public const string Number = "number";

    public const string Money = "money";

    public const string Date = "date";
}
