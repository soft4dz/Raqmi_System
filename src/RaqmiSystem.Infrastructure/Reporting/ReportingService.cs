using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Application.Reporting;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Reporting;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Reporting;

/// <summary>
/// Executes the reports of the code-defined catalog (<see cref="ReportCatalog"/>) and keeps the
/// execution journal (reporting.report_executions).
///
/// DESIGN RULE - every report DELEGATES to the service of the module that owns the figures, so
/// the business rules (validated revenue only, confirmed receipts only, issued invoices only,
/// occupancy from blocking reservations) are applied exactly once, in the module that defines
/// them. This service only reshapes the delegated results into the uniform columns/rows payload
/// and never re-implements a scope rule. Its own table is the journal, nothing else.
/// </summary>
public sealed class ReportingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IDailyRevenueService dailyRevenueService,
    ITreasuryService treasuryService,
    IReceivablesService receivablesService,
    IBillingService billingService,
    ILodgingService lodgingService) : IReportingService
{
    private const int ExecutionListCap = 200;

    public IReadOnlyCollection<ReportDefinitionResponse> GetCatalog()
    {
        return ReportCatalog.All
            .Select(definition => new ReportDefinitionResponse(
                definition.Code,
                definition.Title,
                definition.Description,
                definition.Parameters
                    .Select(parameter => new ReportParameterResponse(
                        parameter.Key,
                        parameter.Label,
                        DescribeParameterType(parameter.Type),
                        parameter.Required))
                    .ToArray()))
            .ToArray();
    }

    public async Task<ApplicationResult<ReportResultResponse>> RunAsync(
        RunReportRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var definition = ReportCatalog.Find(request.Code);

        if (definition is null)
        {
            return ApplicationResult<ReportResultResponse>.NotFound("Report was not found in the catalog.");
        }

        var parsed = ParseParameters(definition, request.Parameters, out var parameterError);

        if (parsed is null)
        {
            return ApplicationResult<ReportResultResponse>.Validation(parameterError!);
        }

        var stopwatch = Stopwatch.StartNew();

        var tableResult = definition.Code switch
        {
            ReportCatalog.RevenueByUnit => await BuildRevenueByUnitAsync(parsed, cancellationToken),
            ReportCatalog.ReceiptsByMethod => await BuildReceiptsByMethodAsync(parsed, cancellationToken),
            ReportCatalog.AgedBalance => await BuildAgedBalanceAsync(parsed, cancellationToken),
            ReportCatalog.InvoicedVat => await BuildInvoicedVatAsync(parsed, cancellationToken),
            ReportCatalog.OccupancyByUnit => await BuildOccupancyByUnitAsync(parsed, cancellationToken),
            _ => ApplicationResult<ReportTable>.Validation("Report execution is not implemented.")
        };

        stopwatch.Stop();

        if (!tableResult.Succeeded || tableResult.Value is null)
        {
            return Propagate(tableResult);
        }

        var table = tableResult.Value;
        var now = DateTimeOffset.UtcNow;

        // The journal answers "who pulled which figures and when": one row per successful
        // execution, carrying the normalized parameters, the duration and the row count. The
        // audit trail gets the same event, like every other sensitive read/write in the system.
        var execution = new ReportExecution(
            definition.Code,
            parsed.NormalizedParametersJson,
            table.Rows.Count,
            stopwatch.ElapsedMilliseconds);

        execution.MarkCreated(context.UserName, now);
        dbContext.Set<ReportExecution>().Add(execution);

        await WriteAuditAsync(
            "reporting.report.executed",
            "reporting.report_executions",
            execution.Id,
            context,
            new { definition.Code, execution.RowCount, execution.DurationMilliseconds },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ReportResultResponse>.Success(new ReportResultResponse(
            definition.Code,
            definition.Title,
            now,
            table.Columns,
            table.Rows,
            table.TotalRow,
            table.Rows.Count,
            execution.DurationMilliseconds));
    }

    public async Task<IReadOnlyCollection<ReportExecutionResponse>> ListExecutionsAsync(
        string? reportCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<ReportExecution>().AsNoTracking();

        var normalizedCode = string.IsNullOrWhiteSpace(reportCode)
            ? null
            : reportCode.Trim().ToLowerInvariant();

        if (normalizedCode is not null)
        {
            query = query.Where(execution => execution.ReportCode == normalizedCode);
        }

        // Ordered and capped in memory, deliberately. The SQLite provider of the test harness
        // refuses ORDER BY on a DateTimeOffset column, and the cap is only meaningful once the
        // rows are sorted: capping in the database WITHOUT the ordering would return an
        // arbitrary 200 executions instead of the 200 most recent ones, which is precisely the
        // kind of silently-wrong answer this journal must not give. The database still applies
        // the report-code filter, so the materialized set is one report's journal, not the
        // whole table.
        var executions = await query.ToArrayAsync(cancellationToken);

        return executions
            .OrderByDescending(execution => execution.CreatedAt)
            .Take(ExecutionListCap)
            .Select(execution => new ReportExecutionResponse(
                execution.Id,
                execution.ReportCode,
                ReportCatalog.Find(execution.ReportCode)?.Title,
                execution.ParametersJson,
                execution.CreatedBy,
                execution.CreatedAt,
                execution.DurationMilliseconds,
                execution.RowCount))
            .ToArray();
    }

    // ------------------------------------------------------------------ report builders

    /// <summary>
    /// Validated daily revenue aggregated by unit and category. The "validated only" rule is
    /// applied by delegating to the revenue module with the Validated status filter - drafts,
    /// submitted and rejected entries never reach this report.
    /// </summary>
    private async Task<ApplicationResult<ReportTable>> BuildRevenueByUnitAsync(
        ParsedParameters parameters,
        CancellationToken cancellationToken)
    {
        var entries = await dailyRevenueService.ListAsync(
            parameters.From,
            parameters.To,
            parameters.UnitCode,
            DailyRevenueStatus.Validated,
            cancellationToken);

        var columns = new[]
        {
            new ReportColumnResponse("unitCode", "Unité", ReportColumnResponse.Text),
            new ReportColumnResponse("unitName", "Désignation", ReportColumnResponse.Text),
            new ReportColumnResponse("accommodation", "Hébergement", ReportColumnResponse.Money),
            new ReportColumnResponse("food", "Restauration", ReportColumnResponse.Money),
            new ReportColumnResponse("beverage", "Boissons", ReportColumnResponse.Money),
            new ReportColumnResponse("other", "Autres", ReportColumnResponse.Money),
            new ReportColumnResponse("total", "Total", ReportColumnResponse.Money)
        };

        var rows = entries
            .GroupBy(entry => entry.HotelUnitCode)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new string?[]
            {
                group.Key,
                group.Select(entry => entry.HotelUnitName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                FormatMoney(group.Sum(entry => entry.Accommodation)),
                FormatMoney(group.Sum(entry => entry.Food)),
                FormatMoney(group.Sum(entry => entry.Beverage)),
                FormatMoney(group.Sum(entry => entry.Other)),
                FormatMoney(group.Sum(entry => entry.Total))
            })
            .ToList();

        var totalRow = new string?[]
        {
            "Total",
            null,
            FormatMoney(entries.Sum(entry => entry.Accommodation)),
            FormatMoney(entries.Sum(entry => entry.Food)),
            FormatMoney(entries.Sum(entry => entry.Beverage)),
            FormatMoney(entries.Sum(entry => entry.Other)),
            FormatMoney(entries.Sum(entry => entry.Total))
        };

        return ApplicationResult<ReportTable>.Success(new ReportTable(columns, rows, totalRow));
    }

    /// <summary>
    /// Confirmed cash receipts aggregated by payment method. The "confirmed only" rule is
    /// applied by delegating to the treasury module with the Confirmed status filter - drafts
    /// and cancelled receipts never reach this report.
    /// </summary>
    private async Task<ApplicationResult<ReportTable>> BuildReceiptsByMethodAsync(
        ParsedParameters parameters,
        CancellationToken cancellationToken)
    {
        var receipts = await treasuryService.ListReceiptsAsync(
            parameters.From,
            parameters.To,
            parameters.UnitCode,
            method: null,
            ReceiptStatus.Confirmed,
            cancellationToken);

        var columns = new[]
        {
            new ReportColumnResponse("method", "Mode de paiement", ReportColumnResponse.Text),
            new ReportColumnResponse("count", "Nombre", ReportColumnResponse.Number),
            new ReportColumnResponse("amount", "Montant encaissé", ReportColumnResponse.Money)
        };

        var rows = receipts
            .GroupBy(receipt => receipt.Method)
            .OrderBy(group => group.Key)
            .Select(group => new string?[]
            {
                DescribePaymentMethod(group.Key),
                FormatCount(group.Count()),
                FormatMoney(group.Sum(receipt => receipt.Amount))
            })
            .ToList();

        var totalRow = new string?[]
        {
            "Total",
            FormatCount(receipts.Count),
            FormatMoney(receipts.Sum(receipt => receipt.Amount))
        };

        return ApplicationResult<ReportTable>.Success(new ReportTable(columns, rows, totalRow));
    }

    /// <summary>
    /// The aged balance is computed entirely by the receivables module (issued unpaid invoices,
    /// aged from the invoice date); this report only lays its customers out as grid rows.
    /// </summary>
    private async Task<ApplicationResult<ReportTable>> BuildAgedBalanceAsync(
        ParsedParameters parameters,
        CancellationToken cancellationToken)
    {
        var aging = await receivablesService.GetAgingBalanceAsync(
            parameters.AsOfDate!.Value,
            customerCode: null,
            cancellationToken);

        var columns = new[]
        {
            new ReportColumnResponse("customerCode", "Client", ReportColumnResponse.Text),
            new ReportColumnResponse("customerName", "Nom", ReportColumnResponse.Text),
            new ReportColumnResponse("invoiceCount", "Factures", ReportColumnResponse.Number),
            new ReportColumnResponse("notDue", "Non échu", ReportColumnResponse.Money),
            new ReportColumnResponse("days1To30", "1–30 jours", ReportColumnResponse.Money),
            new ReportColumnResponse("days31To60", "31–60 jours", ReportColumnResponse.Money),
            new ReportColumnResponse("days61To90", "61–90 jours", ReportColumnResponse.Money),
            new ReportColumnResponse("over90", "Plus de 90 jours", ReportColumnResponse.Money),
            new ReportColumnResponse("total", "Total dû", ReportColumnResponse.Money)
        };

        var rows = aging.Customers
            .Select(customer => new string?[]
            {
                customer.CustomerCode,
                customer.CustomerName,
                FormatCount(customer.InvoiceCount),
                FormatMoney(customer.Buckets.NotDue),
                FormatMoney(customer.Buckets.Days1To30),
                FormatMoney(customer.Buckets.Days31To60),
                FormatMoney(customer.Buckets.Days61To90),
                FormatMoney(customer.Buckets.Over90),
                FormatMoney(customer.Buckets.Total)
            })
            .ToList();

        var totalRow = new string?[]
        {
            "Total",
            null,
            FormatCount(aging.Customers.Sum(customer => customer.InvoiceCount)),
            FormatMoney(aging.Total.NotDue),
            FormatMoney(aging.Total.Days1To30),
            FormatMoney(aging.Total.Days31To60),
            FormatMoney(aging.Total.Days61To90),
            FormatMoney(aging.Total.Over90),
            FormatMoney(aging.Total.Total)
        };

        return ApplicationResult<ReportTable>.Success(new ReportTable(columns, rows, totalRow));
    }

    /// <summary>
    /// VAT base and collected VAT per rate over issued and paid invoices of the period. Drafts
    /// never enter (they are not commercial documents yet) and cancelled invoices never enter
    /// (commercially void): both exclusions come from querying the billing module by status,
    /// not from re-deciding the rule here. Amounts are the line-level figures computed by the
    /// billing domain (LineTotalExclVat / VatAmount), never recomputed.
    /// </summary>
    private async Task<ApplicationResult<ReportTable>> BuildInvoicedVatAsync(
        ParsedParameters parameters,
        CancellationToken cancellationToken)
    {
        var issued = await billingService.ListInvoicesAsync(
            parameters.From,
            parameters.To,
            customerCode: null,
            parameters.UnitCode,
            InvoiceStatus.Issued,
            cancellationToken);

        // A paid invoice was issued first: it stays part of the invoiced VAT of its period.
        var paid = await billingService.ListInvoicesAsync(
            parameters.From,
            parameters.To,
            customerCode: null,
            parameters.UnitCode,
            InvoiceStatus.Paid,
            cancellationToken);

        var invoices = issued.Concat(paid).ToArray();

        var linesByRate = invoices
            .SelectMany(invoice => invoice.Lines.Select(line => (Invoice: invoice, Line: line)))
            .GroupBy(item => item.Line.VatRate)
            .OrderBy(group => group.Key)
            .ToArray();

        var columns = new[]
        {
            new ReportColumnResponse("vatRate", "Taux de TVA (%)", ReportColumnResponse.Number),
            new ReportColumnResponse("invoiceCount", "Factures", ReportColumnResponse.Number),
            new ReportColumnResponse("baseExclVat", "Base hors taxes", ReportColumnResponse.Money),
            new ReportColumnResponse("vatAmount", "TVA collectée", ReportColumnResponse.Money),
            new ReportColumnResponse("totalInclVat", "Total TTC", ReportColumnResponse.Money)
        };

        var rows = linesByRate
            .Select(group => new string?[]
            {
                FormatRate(group.Key),
                FormatCount(group.Select(item => item.Invoice.Id).Distinct().Count()),
                FormatMoney(group.Sum(item => item.Line.LineTotalExclVat)),
                FormatMoney(group.Sum(item => item.Line.VatAmount)),
                FormatMoney(group.Sum(item => item.Line.LineTotalInclVat))
            })
            .ToList();

        var totalRow = new string?[]
        {
            "Total",
            FormatCount(invoices.Length),
            FormatMoney(invoices.Sum(invoice => invoice.TotalExclVat)),
            FormatMoney(invoices.Sum(invoice => invoice.TotalVat)),
            FormatMoney(invoices.Sum(invoice => invoice.TotalInclVat))
        };

        return ApplicationResult<ReportTable>.Success(new ReportTable(columns, rows, totalRow));
    }

    /// <summary>
    /// Day-by-day occupancy of one unit, computed entirely by the lodging module (active rooms,
    /// blocking reservations covering the night). A lodging refusal (unknown unit, inverted
    /// period) is propagated as-is: the reporting module never softens another module's answer.
    /// </summary>
    private async Task<ApplicationResult<ReportTable>> BuildOccupancyByUnitAsync(
        ParsedParameters parameters,
        CancellationToken cancellationToken)
    {
        var occupancyResult = await lodgingService.GetOccupancyAsync(
            parameters.UnitCode!,
            parameters.From!.Value,
            parameters.To!.Value,
            cancellationToken);

        if (!occupancyResult.Succeeded || occupancyResult.Value is null)
        {
            return Propagate<OccupancyResponse, ReportTable>(occupancyResult);
        }

        var occupancy = occupancyResult.Value;

        var columns = new[]
        {
            new ReportColumnResponse("date", "Date", ReportColumnResponse.Date),
            new ReportColumnResponse("activeRooms", "Chambres actives", ReportColumnResponse.Number),
            new ReportColumnResponse("occupiedRooms", "Chambres occupées", ReportColumnResponse.Number),
            new ReportColumnResponse("occupancyRate", "Taux d'occupation (%)", ReportColumnResponse.Number)
        };

        var rows = occupancy.Days
            .Select(day => new string?[]
            {
                FormatDate(day.Date),
                FormatCount(day.TotalActiveRooms),
                FormatCount(day.OccupiedRooms),
                FormatRate(day.OccupancyRatePercent)
            })
            .ToList();

        // The period total weights each night equally: total occupied room-nights over total
        // available room-nights (0 when the unit offers none).
        var totalActive = occupancy.Days.Sum(day => day.TotalActiveRooms);
        var totalOccupied = occupancy.Days.Sum(day => day.OccupiedRooms);

        var totalRow = new string?[]
        {
            "Total",
            FormatCount(totalActive),
            FormatCount(totalOccupied),
            FormatRate(totalActive == 0 ? 0m : Math.Round(totalOccupied * 100m / totalActive, 2, MidpointRounding.AwayFromZero))
        };

        return ApplicationResult<ReportTable>.Success(new ReportTable(columns, rows, totalRow));
    }

    // ------------------------------------------------------------------ parameter parsing

    /// <summary>
    /// Parses and validates the raw parameter values against the report's definition. Unknown
    /// keys are refused (a misspelled filter must never silently widen a report), required
    /// parameters must be present, dates must be yyyy-MM-dd, and an inverted period is refused
    /// here once for every report rather than in each builder.
    /// </summary>
    private static ParsedParameters? ParseParameters(
        ReportDefinition definition,
        IReadOnlyDictionary<string, string?>? raw,
        out string? error)
    {
        error = null;

        var provided = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in raw ?? new Dictionary<string, string?>())
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            if (!definition.Parameters.Any(parameter =>
                    string.Equals(parameter.Key, pair.Key, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"Unknown parameter '{pair.Key}' for report '{definition.Code}'.";
                return null;
            }

            provided[pair.Key] = pair.Value.Trim();
        }

        DateOnly? from = null;
        DateOnly? to = null;
        DateOnly? asOfDate = null;
        string? unitCode = null;

        var normalized = new Dictionary<string, string>();

        foreach (var parameter in definition.Parameters)
        {
            provided.TryGetValue(parameter.Key, out var value);

            if (value is null)
            {
                if (parameter.Required)
                {
                    error = $"Parameter '{parameter.Key}' is required for report '{definition.Code}'.";
                    return null;
                }

                continue;
            }

            switch (parameter.Type)
            {
                case ReportParameterType.Date:
                    if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        error = $"Parameter '{parameter.Key}' must be a date in yyyy-MM-dd format.";
                        return null;
                    }

                    normalized[parameter.Key] = FormatDate(date);

                    switch (parameter.Key)
                    {
                        case ReportCatalog.FromParameter:
                            from = date;
                            break;
                        case ReportCatalog.ToParameter:
                            to = date;
                            break;
                        case ReportCatalog.AsOfDateParameter:
                            asOfDate = date;
                            break;
                    }

                    break;

                case ReportParameterType.HotelUnit:
                    unitCode = value.ToUpperInvariant();
                    normalized[parameter.Key] = unitCode;
                    break;
            }
        }

        if (from.HasValue && to.HasValue && from > to)
        {
            error = "The from date cannot be after the to date.";
            return null;
        }

        return new ParsedParameters(
            from,
            to,
            asOfDate,
            unitCode,
            JsonSerializer.Serialize(normalized));
    }

    // ------------------------------------------------------------------ helpers

    private static string DescribeParameterType(ReportParameterType type)
    {
        return type switch
        {
            ReportParameterType.HotelUnit => ReportParameterResponse.Unit,
            _ => ReportParameterResponse.Date
        };
    }

    /// <summary>
    /// French labels of the payment methods, written once server-side so the grid, the CSV and
    /// the journal all carry the same word (the enum value itself never reaches the screen).
    /// </summary>
    private static string DescribePaymentMethod(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.Cash => "Espèces",
            PaymentMethod.Card => "Carte bancaire",
            PaymentMethod.Cheque => "Chèque",
            PaymentMethod.BankTransfer => "Virement bancaire",
            _ => method.ToString()
        };
    }

    // Raw cell values are culture-invariant on purpose (dates yyyy-MM-dd, decimals with a dot,
    // no thousands separator): the client formats money as N2 in the current culture for the
    // screen and keeps the raw machine values for the CSV export, exactly like the existing
    // CsvExportHelper convention.
    private static string FormatMoney(decimal value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string FormatRate(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatCount(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static ApplicationResult<ReportResultResponse> Propagate(ApplicationResult<ReportTable> result)
    {
        return Propagate<ReportTable, ReportResultResponse>(result);
    }

    /// <summary>Re-types a failed result without changing its error type or message.</summary>
    private static ApplicationResult<TTarget> Propagate<TSource, TTarget>(ApplicationResult<TSource> result)
    {
        var message = result.Error ?? "The delegated module refused the report execution.";

        return result.ErrorType switch
        {
            ApplicationErrorType.NotFound => ApplicationResult<TTarget>.NotFound(message),
            ApplicationErrorType.Conflict => ApplicationResult<TTarget>.Conflict(message),
            _ => ApplicationResult<TTarget>.Validation(message)
        };
    }

    /// <summary>
    /// Explicit flush after the audit write. AuditLogWriter.WriteAsync already calls
    /// SaveChangesAsync internally (persisting the pending entity changes together with the
    /// audit row), so this call is usually a no-op - it exists so persistence never silently
    /// depends on the audit writer's internals.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }

    /// <summary>Intermediate shape shared by every report builder.</summary>
    private sealed record ReportTable(
        IReadOnlyList<ReportColumnResponse> Columns,
        List<string?[]> Rows,
        string?[]? TotalRow);

    /// <summary>
    /// The validated parameters of one execution, in the shape the builders consume. Each report
    /// reads only the slots its own definition declares (a report without a unit parameter simply
    /// leaves <see cref="UnitCode"/> null, which the delegated services read as "no filter").
    /// <see cref="NormalizedParametersJson"/> is the canonical rendering stored in the journal, so
    /// two executions written the same way are recorded identically whatever the client sent.
    /// </summary>
    private sealed record ParsedParameters(
        DateOnly? From,
        DateOnly? To,
        DateOnly? AsOfDate,
        string? UnitCode,
        string NormalizedParametersJson);
}
