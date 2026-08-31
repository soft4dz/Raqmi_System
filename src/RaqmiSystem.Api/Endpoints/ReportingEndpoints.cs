using RaqmiSystem.Application.Reporting;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Automatic reports: the code-defined catalog, parameterized executions and the execution
/// journal.
///
/// A single permission guards the whole module: "reports.read". Running a report IS a read (it
/// creates no business data - the journal line is a trace, like an audit row), and the journal
/// itself answers "who pulled what", which the same population that runs reports must be able
/// to consult. No superfluous admin key.
///
/// NOTE FOR THE INTEGRATOR: the permission is referenced as a string literal here; replace it
/// with the PermissionCatalog constant once "reports.read" is added to the catalog.
/// </summary>
internal static class ReportingEndpoints
{
    public static RouteGroupBuilder MapReportingEndpoints(this RouteGroupBuilder api)
    {
        var reporting = api.MapGroup("/reporting")
            .WithTags("Reporting");

        reporting.MapGet("/catalog", (IReportingService service) =>
        {
            return Results.Ok(service.GetCatalog());
        }).RequireAuthorization(PermissionCatalog.ReportsRead);

        reporting.MapPost("/run", async (
            RunReportRequest request,
            IReportingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RunAsync(request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.ReportsRead);

        reporting.MapGet("/executions", async (
            string? reportCode,
            IReportingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListExecutionsAsync(reportCode, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.ReportsRead);

        return api;
    }
}
