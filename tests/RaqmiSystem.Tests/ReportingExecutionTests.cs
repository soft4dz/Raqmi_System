using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Reporting;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Reporting;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Execution of the five catalog reports and of the execution journal.
///
/// The point of these tests is NOT that a report returns some rows: it is that each report
/// counts exactly what the module owning the figures counts. A report that totals something
/// other than its source module is a silent lie on a management screen, so the scope rules are
/// pinned here with data deliberately built to violate them - a draft and a rejected revenue
/// entry, a draft and a cancelled receipt - which must never reach a total.
/// </summary>
public sealed class ReportingExecutionTests : IClassFixture<RaqmiApiFactory>
{
    private const string UnitCode = "RPT-UNIT";

    private const string OtherUnitCode = "RPT-OTHER";

    private static readonly DateOnly PeriodFrom = new(2026, 3, 1);

    private static readonly DateOnly PeriodTo = new(2026, 3, 31);

    private readonly RaqmiApiFactory _factory;

    public ReportingExecutionTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void The_catalog_is_served_without_touching_the_database()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportingService>();

        var catalog = service.GetCatalog();

        Assert.Equal(5, catalog.Count);
        Assert.All(catalog, definition => Assert.False(string.IsNullOrWhiteSpace(definition.Title)));

        // Every parameter is typed for the client's editor: a date picker or a unit picker.
        Assert.All(
            catalog.SelectMany(definition => definition.Parameters),
            parameter => Assert.Contains(
                parameter.Type,
                new[] { ReportParameterResponse.Date, ReportParameterResponse.Unit }));
    }

    /// <summary>
    /// recettes-par-unite must count VALIDATED revenue only - the revenue module's own rule.
    /// The unit below also carries a draft and a rejected entry; if either leaked into the
    /// report, the total would not match the revenue screen and the figure would be worthless.
    /// </summary>
    [Fact]
    public async Task Revenue_by_unit_totals_validated_entries_only()
    {
        await SeedRevenueAsync();

        var result = await RunAsync(ReportCatalog.RevenueByUnit, new Dictionary<string, string?>
        {
            [ReportCatalog.FromParameter] = Format(PeriodFrom),
            [ReportCatalog.ToParameter] = Format(PeriodTo),
            [ReportCatalog.UnitCodeParameter] = UnitCode
        });

        var row = Assert.Single(result.Rows);

        Assert.Equal(UnitCode, row[0]);

        // 1000 + 2000 validated. The 500 draft and the 900 rejected entries are excluded.
        Assert.Equal("3000.00", row[6]);

        Assert.NotNull(result.TotalRow);
        Assert.Equal("3000.00", result.TotalRow![6]);

        // Raw cells stay culture-invariant so the grid and the CSV share one payload.
        Assert.Equal("1000.00", row[2]);
        Assert.DoesNotContain(",", row[6]);
    }

    /// <summary>
    /// encaissements-par-mode must count CONFIRMED receipts only - the treasury module's rule.
    /// A draft and a cancelled receipt sit in the same period to prove the exclusion.
    /// </summary>
    [Fact]
    public async Task Receipts_by_method_totals_confirmed_receipts_only()
    {
        await SeedReceiptsAsync();

        var result = await RunAsync(ReportCatalog.ReceiptsByMethod, new Dictionary<string, string?>
        {
            [ReportCatalog.FromParameter] = Format(PeriodFrom),
            [ReportCatalog.ToParameter] = Format(PeriodTo),
            [ReportCatalog.UnitCodeParameter] = OtherUnitCode
        });

        // Two confirmed receipts, on two different payment methods.
        Assert.Equal(2, result.Rows.Count);

        Assert.NotNull(result.TotalRow);
        Assert.Equal("2", result.TotalRow![1]);

        // 300 cash + 700 card confirmed. The 400 draft and the 800 cancelled are excluded.
        Assert.Equal("1000.00", result.TotalRow[2]);

        // The payment method reaches the screen in French, never as the raw enum name.
        Assert.Contains(result.Rows, row => row[0] == "Espèces");
        Assert.Contains(result.Rows, row => row[0] == "Carte bancaire");
    }

    [Fact]
    public async Task The_aged_balance_and_the_invoiced_vat_run_and_answer_with_their_own_columns()
    {
        // Both delegate their whole computation (issued unpaid invoices aged from the invoice
        // date; issued and paid invoices grouped by VAT rate). With no invoice in this database
        // they must answer an EMPTY, well-formed report rather than fail: an empty period is a
        // legitimate answer, not an error.
        var aging = await RunAsync(ReportCatalog.AgedBalance, new Dictionary<string, string?>
        {
            [ReportCatalog.AsOfDateParameter] = Format(PeriodTo)
        });

        Assert.Equal(9, aging.Columns.Count);
        Assert.Equal("Total dû", aging.Columns[8].Label);
        Assert.Empty(aging.Rows);
        Assert.NotNull(aging.TotalRow);

        var vat = await RunAsync(ReportCatalog.InvoicedVat, new Dictionary<string, string?>
        {
            [ReportCatalog.FromParameter] = Format(PeriodFrom),
            [ReportCatalog.ToParameter] = Format(PeriodTo)
        });

        Assert.Equal(5, vat.Columns.Count);
        Assert.Equal("TVA collectée", vat.Columns[3].Label);
        Assert.Empty(vat.Rows);
        Assert.NotNull(vat.TotalRow);
        Assert.Equal("0", vat.TotalRow![1]);
    }

    /// <summary>
    /// occupation-par-unite delegates entirely to the lodging module, INCLUDING its refusals: an
    /// unknown unit must surface the lodging answer, never a softened empty report.
    /// </summary>
    [Fact]
    public async Task Occupancy_delegates_to_lodging_and_propagates_its_refusal()
    {
        await EnsureUnitsAsync();

        var result = await RunAsync(ReportCatalog.OccupancyByUnit, new Dictionary<string, string?>
        {
            [ReportCatalog.FromParameter] = Format(PeriodFrom),
            [ReportCatalog.ToParameter] = Format(new DateOnly(2026, 3, 3)),
            [ReportCatalog.UnitCodeParameter] = UnitCode
        });

        // One row per night of the period, with no room configured: 0 % occupancy, not a crash.
        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(Format(PeriodFrom), result.Rows[0][0]);
        Assert.Equal("0", result.TotalRow![3]);

        var refused = await RunRawAsync(ReportCatalog.OccupancyByUnit, new Dictionary<string, string?>
        {
            [ReportCatalog.FromParameter] = Format(PeriodFrom),
            [ReportCatalog.ToParameter] = Format(PeriodTo),
            [ReportCatalog.UnitCodeParameter] = "NO-SUCH-UNIT"
        });

        Assert.False(refused.Succeeded);
    }

    [Fact]
    public async Task Parameters_are_validated_before_anything_is_journalized()
    {
        var unknownReport = await RunRawAsync("pas-un-rapport", null);

        Assert.False(unknownReport.Succeeded);
        Assert.Equal(ApplicationErrorType.NotFound, unknownReport.ErrorType);

        // A missing REQUIRED parameter is refused.
        var missing = await RunRawAsync(ReportCatalog.AgedBalance, new Dictionary<string, string?>());

        Assert.False(missing.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, missing.ErrorType);
        Assert.Contains(ReportCatalog.AsOfDateParameter, missing.Error);

        // An UNKNOWN key is refused rather than ignored: a misspelled filter must never
        // silently widen a report.
        var unknownKey = await RunRawAsync(ReportCatalog.AgedBalance, new Dictionary<string, string?>
        {
            [ReportCatalog.AsOfDateParameter] = Format(PeriodTo),
            ["unite"] = UnitCode
        });

        Assert.False(unknownKey.Succeeded);
        Assert.Contains("unite", unknownKey.Error);

        // A malformed date, and an inverted period, are refused once for every report.
        var badDate = await RunRawAsync(ReportCatalog.AgedBalance, new Dictionary<string, string?>
        {
            [ReportCatalog.AsOfDateParameter] = "31/03/2026"
        });

        Assert.False(badDate.Succeeded);

        var inverted = await RunRawAsync(ReportCatalog.RevenueByUnit, new Dictionary<string, string?>
        {
            [ReportCatalog.FromParameter] = Format(PeriodTo),
            [ReportCatalog.ToParameter] = Format(PeriodFrom)
        });

        Assert.False(inverted.Succeeded);

        // None of those refusals may have written a journal row.
        var journal = await ListExecutionsAsync(ReportCatalog.AgedBalance);

        Assert.DoesNotContain(journal, execution => execution.ParametersJson.Contains("31/03/2026"));
    }

    /// <summary>
    /// The journal answers "who pulled which figures, when, and how many rows came back". It is
    /// written on SUCCESS only, and read back newest-first.
    /// </summary>
    [Fact]
    public async Task Successful_executions_are_journalized_newest_first_with_their_normalized_parameters()
    {
        var before = (await ListExecutionsAsync(ReportCatalog.InvoicedVat)).Count;

        await RunAsync(ReportCatalog.InvoicedVat, new Dictionary<string, string?>
        {
            [ReportCatalog.FromParameter] = Format(PeriodFrom),
            [ReportCatalog.ToParameter] = Format(PeriodTo),
            // Lower case on the wire: the journal stores the NORMALIZED value.
            [ReportCatalog.UnitCodeParameter] = OtherUnitCode.ToLowerInvariant()
        });

        await RunAsync(ReportCatalog.InvoicedVat, new Dictionary<string, string?>
        {
            [ReportCatalog.FromParameter] = Format(PeriodFrom),
            [ReportCatalog.ToParameter] = Format(PeriodTo)
        });

        var journal = await ListExecutionsAsync(ReportCatalog.InvoicedVat);

        Assert.Equal(before + 2, journal.Count);

        var newest = journal.First();

        Assert.Equal(ReportCatalog.InvoicedVat, newest.ReportCode);
        Assert.Equal("TVA facturée par taux", newest.ReportTitle);
        Assert.Equal("reporting.tests", newest.ExecutedBy);
        Assert.True(newest.DurationMilliseconds >= 0);

        // Newest first.
        Assert.True(journal.First().ExecutedAt >= journal.Last().ExecutedAt);

        // The normalized parameters were stored, with the unit code upper-cased.
        Assert.Contains(journal, execution => execution.ParametersJson.Contains(OtherUnitCode));

        // Filtering by report code isolates one report's journal.
        Assert.All(journal, execution => Assert.Equal(ReportCatalog.InvoicedVat, execution.ReportCode));
    }

    // ---------------------------------------------------------------- helpers

    private static string Format(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private async Task<ReportResultResponse> RunAsync(
        string code,
        IReadOnlyDictionary<string, string?>? parameters)
    {
        var result = await RunRawAsync(code, parameters);

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Value);

        return result.Value!;
    }

    private async Task<ApplicationResult<ReportResultResponse>> RunRawAsync(
        string code,
        IReadOnlyDictionary<string, string?>? parameters)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportingService>();

        return await service.RunAsync(
            new RunReportRequest(code, parameters),
            new OperationContext(Guid.NewGuid(), "reporting.tests", "127.0.0.1"),
            CancellationToken.None);
    }

    private async Task<IReadOnlyList<ReportExecutionResponse>> ListExecutionsAsync(string? code)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportingService>();

        return (await service.ListExecutionsAsync(code, CancellationToken.None)).ToArray();
    }

    private async Task EnsureUnitsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        if (dbContext.HotelUnits.Any(unit => unit.Code == UnitCode))
        {
            return;
        }

        await _factory.CreateHotelUnitAsync(UnitCode, "Unité rapports");
        await _factory.CreateHotelUnitAsync(OtherUnitCode, "Unité encaissements");
    }

    /// <summary>
    /// Four revenue entries in the period: two VALIDATED (1000 and 2000), one left DRAFT (500)
    /// and one REJECTED (900). Only the first two may ever appear in the report.
    /// </summary>
    private async Task SeedRevenueAsync()
    {
        await EnsureUnitsAsync();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        if (dbContext.DailyRevenues.Any(revenue => revenue.HotelUnitCode == UnitCode))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var validatedOne = new DailyRevenue(new DateOnly(2026, 3, 2), UnitCode, 1000m, 0m, 0m, 0m);
        validatedOne.MarkCreated("tests", now);
        validatedOne.Submit("tests", now);
        validatedOne.Validate("controle", now);

        var validatedTwo = new DailyRevenue(new DateOnly(2026, 3, 3), UnitCode, 0m, 2000m, 0m, 0m);
        validatedTwo.MarkCreated("tests", now);
        validatedTwo.Submit("tests", now);
        validatedTwo.Validate("controle", now);

        var draft = new DailyRevenue(new DateOnly(2026, 3, 4), UnitCode, 500m, 0m, 0m, 0m);
        draft.MarkCreated("tests", now);

        var rejected = new DailyRevenue(new DateOnly(2026, 3, 5), UnitCode, 900m, 0m, 0m, 0m);
        rejected.MarkCreated("tests", now);
        rejected.Submit("tests", now);
        rejected.Reject("Justificatifs manquants.", "controle", now);

        dbContext.DailyRevenues.AddRange(validatedOne, validatedTwo, draft, rejected);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Four receipts in the period: two CONFIRMED (300 cash, 700 card), one left DRAFT (400) and
    /// one CANCELLED (800). Only the confirmed pair may ever be counted.
    /// </summary>
    private async Task SeedReceiptsAsync()
    {
        await EnsureUnitsAsync();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        if (dbContext.CashReceipts.Any(receipt => receipt.HotelUnitCode == OtherUnitCode))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // Card, cheque and bank-transfer receipts carry a bank account (treasury domain rule).
        var account = new BankAccount("RPT-BNA", "Compte rapports", "BNA", "00100888000012345678");
        account.MarkCreated("tests", now);
        dbContext.Set<BankAccount>().Add(account);

        var confirmedCash = new CashReceipt(new DateOnly(2026, 3, 6), OtherUnitCode, PaymentMethod.Cash, 300m);
        confirmedCash.MarkCreated("tests", now);
        confirmedCash.Confirm("caisse", now);

        var confirmedCard = new CashReceipt(
            new DateOnly(2026, 3, 7), OtherUnitCode, PaymentMethod.Card, 700m, bankAccountCode: account.Code);
        confirmedCard.MarkCreated("tests", now);
        confirmedCard.Confirm("caisse", now);

        var draft = new CashReceipt(new DateOnly(2026, 3, 8), OtherUnitCode, PaymentMethod.Cash, 400m);
        draft.MarkCreated("tests", now);

        // A cheque also carries its reference (treasury domain rule).
        var cancelled = new CashReceipt(
            new DateOnly(2026, 3, 9),
            OtherUnitCode,
            PaymentMethod.Cheque,
            800m,
            reference: "CHQ-004512",
            bankAccountCode: account.Code);
        cancelled.MarkCreated("tests", now);
        cancelled.Confirm("caisse", now);
        cancelled.Cancel("Chèque sans provision.", "caisse", now);

        dbContext.CashReceipts.AddRange(confirmedCash, confirmedCard, draft, cancelled);
        await dbContext.SaveChangesAsync();
    }
}
