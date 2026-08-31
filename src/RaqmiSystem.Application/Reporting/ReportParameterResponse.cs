namespace RaqmiSystem.Application.Reporting;

/// <summary>
/// One parameter of a catalog report as exposed to clients. <see cref="Type"/> is "date" or
/// "unit"; the desktop renders the matching input control from it.
/// </summary>
public sealed record ReportParameterResponse(
    string Key,
    string Label,
    string Type,
    bool Required)
{
    public const string Date = "date";

    public const string Unit = "unit";
}
