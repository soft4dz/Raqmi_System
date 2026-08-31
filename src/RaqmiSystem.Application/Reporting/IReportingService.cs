using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Reporting;

/// <summary>
/// Automatic reports: the code-defined catalog, parameterized executions returning structured
/// data (columns + rows rendered by a single dynamic grid), and the execution journal.
///
/// This module creates NO figures of its own: every report delegates to the service of the
/// module that owns the numbers (validated daily revenue, confirmed cash receipts, the
/// receivables aging balance, issued invoices, lodging occupancy), so the business rules are
/// applied exactly once, where they live. PDF and Excel exports are out of scope - the desktop
/// exports the structured result to CSV locally.
/// </summary>
public interface IReportingService
{
    /// <summary>The report catalog, in the order it is defined in code. Never touches the database.</summary>
    IReadOnlyCollection<ReportDefinitionResponse> GetCatalog();

    /// <summary>
    /// Runs one catalog report with the given raw parameters. On success the execution is
    /// journalized (report, parameters, author, timestamp, duration, row count) and audited.
    /// Unknown codes, unknown parameter keys, missing required parameters and malformed dates
    /// are refused without writing anything.
    /// </summary>
    Task<ApplicationResult<ReportResultResponse>> RunAsync(
        RunReportRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// The most recent executions (newest first, capped), optionally filtered by report code.
    /// </summary>
    Task<IReadOnlyCollection<ReportExecutionResponse>> ListExecutionsAsync(
        string? reportCode,
        CancellationToken cancellationToken);
}
