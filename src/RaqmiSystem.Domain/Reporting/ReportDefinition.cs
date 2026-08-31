namespace RaqmiSystem.Domain.Reporting;

/// <summary>
/// One report of the code-defined catalog: a stable code (the wire identifier), the French title
/// and description the catalog screen displays, and the typed parameters an execution accepts.
/// </summary>
public sealed record ReportDefinition(
    string Code,
    string Title,
    string Description,
    IReadOnlyCollection<ReportParameterDefinition> Parameters);
