namespace RaqmiSystem.Application.Reporting;

public sealed record ReportDefinitionResponse(
    string Code,
    string Title,
    string Description,
    IReadOnlyList<ReportParameterResponse> Parameters);
