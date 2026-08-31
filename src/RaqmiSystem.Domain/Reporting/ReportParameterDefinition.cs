namespace RaqmiSystem.Domain.Reporting;

/// <summary>
/// One typed parameter of a catalog report. <see cref="Key"/> is the wire identifier sent by the
/// client in the run request (ASCII, no accents); <see cref="Label"/> is the French label the
/// screen displays above the input.
/// </summary>
public sealed record ReportParameterDefinition(
    string Key,
    string Label,
    ReportParameterType Type,
    bool Required);
