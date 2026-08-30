using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Receivables;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Receivables;

namespace RaqmiSystem.Tests;

/// <summary>
/// Covers ReceivablesService against a real relational provider (SQLite ":memory:", the same
/// technique as RefreshTokenRotationTests): the aged balance is an aggregation query, the
/// duplicate-level rule is backed by a unique index, and neither can be honestly exercised
/// against hand-built in-memory lists.
///
/// The tests deliberately drive the service directly rather than over HTTP: the
/// "receivables.read"/"receivables.write" authorization policies are wired by the integration
/// pass (PermissionCatalog + Program.cs), which this module does not own.
/// </summary>
public sealed class ReceivablesServiceTests
{
    private static readonly OperationContext Context = new(null, "tests", "127.0.0.1");

    [Fact]
    public async Task Aging_balance_splits_outstanding_invoices_into_their_exact_brackets()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        var asOfDate = new DateOnly(2026, 6, 30);

        await SeedOrganizationAsync(dbContext, "CLI-A", "Client A", "CLI-B", "Client B");

        // One invoice per bracket, dated on the exact boundary day of each one.
        dbContext.Add(IssuedInvoice("CLI-A", new DateOnly(2026, 6, 30), 1_000m, 1));   // age 0   -> not due
        dbContext.Add(IssuedInvoice("CLI-A", new DateOnly(2026, 5, 31), 2_000m, 2));   // age 30  -> 1-30
        dbContext.Add(IssuedInvoice("CLI-A", new DateOnly(2026, 5, 1), 3_000m, 3));    // age 60  -> 31-60
        dbContext.Add(IssuedInvoice("CLI-A", new DateOnly(2026, 4, 1), 4_000m, 4));    // age 90  -> 61-90
        dbContext.Add(IssuedInvoice("CLI-A", new DateOnly(2026, 3, 31), 5_000m, 5));   // age 91  -> over 90

        dbContext.Add(IssuedInvoice("CLI-B", new DateOnly(2026, 6, 10), 700m, 6));     // age 20  -> 1-30

        await dbContext.SaveChangesAsync();

        var balance = await service.GetAgingBalanceAsync(asOfDate, customerCode: null, CancellationToken.None);

        Assert.Equal(asOfDate, balance.AsOfDate);
        Assert.Equal(2, balance.Customers.Count);

        var clientA = balance.Customers.Single(line => line.CustomerCode == "CLI-A");

        Assert.Equal("Client A", clientA.CustomerName);
        Assert.Equal(5, clientA.InvoiceCount);
        Assert.Equal(new DateOnly(2026, 3, 31), clientA.OldestInvoiceDate);
        Assert.Equal(91, clientA.OldestInvoiceAgeInDays);
        Assert.Equal(1_000m, clientA.Buckets.NotDue);
        Assert.Equal(2_000m, clientA.Buckets.Days1To30);
        Assert.Equal(3_000m, clientA.Buckets.Days31To60);
        Assert.Equal(4_000m, clientA.Buckets.Days61To90);
        Assert.Equal(5_000m, clientA.Buckets.Over90);
        Assert.Equal(15_000m, clientA.Buckets.Total);

        // The grand total spans every customer.
        Assert.Equal(15_700m, balance.Total.Total);
        Assert.Equal(2_700m, balance.Total.Days1To30);

        // Filtering narrows the report to a single customer, code normalization included.
        var filtered = await service.GetAgingBalanceAsync(asOfDate, "  cli-b  ", CancellationToken.None);

        Assert.Equal("CLI-B", filtered.Customers.Single().CustomerCode);
        Assert.Equal(700m, filtered.Total.Total);
    }

    [Fact]
    public async Task Aging_balance_counts_issued_invoices_only_and_ignores_invoices_dated_after_the_reporting_date()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        var asOfDate = new DateOnly(2026, 6, 30);

        await SeedOrganizationAsync(dbContext, "CLI-A", "Client A");

        var counted = IssuedInvoice("CLI-A", new DateOnly(2026, 6, 1), 1_500m, 1);

        var draft = new Invoice("CLI-A", HotelUnitCode, new DateOnly(2026, 6, 1));
        draft.ReplaceLines(new[] { new InvoiceLine("Hebergement", 1m, 9_999m, 0m) });

        var paid = IssuedInvoice("CLI-A", new DateOnly(2026, 6, 2), 8_888m, 2);
        paid.MarkPaid("tests", DateTimeOffset.UtcNow);

        var cancelled = IssuedInvoice("CLI-A", new DateOnly(2026, 6, 3), 7_777m, 3);
        cancelled.Cancel("Erreur de saisie", "tests", DateTimeOffset.UtcNow);

        // Issued, but dated after the reporting date: it did not exist yet on that date.
        var afterTheReportingDate = IssuedInvoice("CLI-A", new DateOnly(2026, 7, 5), 6_666m, 4);

        dbContext.AddRange(counted, draft, paid, cancelled, afterTheReportingDate);
        await dbContext.SaveChangesAsync();

        var balance = await service.GetAgingBalanceAsync(asOfDate, customerCode: null, CancellationToken.None);

        var line = balance.Customers.Single();

        Assert.Equal(1, line.InvoiceCount);
        Assert.Equal(1_500m, line.Buckets.Total);
        Assert.Equal(1_500m, balance.Total.Total);

        // The exclusions are not folklore the reader has to know: the payload says them.
        Assert.Contains("Issued", balance.Scope, StringComparison.Ordinal);
        Assert.Contains("Draft", balance.Scope, StringComparison.Ordinal);
        Assert.Contains("Paid", balance.Scope, StringComparison.Ordinal);
        Assert.Contains("Cancelled", balance.Scope, StringComparison.Ordinal);
        Assert.Contains("invoice date", balance.AgingBasis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recording_the_same_reminder_level_twice_for_one_invoice_is_refused()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        await SeedOrganizationAsync(dbContext, "CLI-A", "Client A");

        var invoice = IssuedInvoice("CLI-A", today.AddDays(-60), 4_200m, 1);
        dbContext.Add(invoice);
        await dbContext.SaveChangesAsync();

        var invoiceNumber = invoice.Number!;

        var first = await service.CreateReminderAsync(
            new CreateReminderRequest(invoiceNumber, ReminderLevel.First, today.AddDays(-30), ReminderChannel.Phone),
            Context,
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.Equal("CLI-A", first.Value!.CustomerCode);
        Assert.Equal("Client A", first.Value.CustomerName);

        // Same level, same invoice: the escalation ladder is climbed once per rung.
        var duplicate = await service.CreateReminderAsync(
            new CreateReminderRequest(invoiceNumber, ReminderLevel.First, today.AddDays(-20), ReminderChannel.Email),
            Context,
            CancellationToken.None);

        Assert.False(duplicate.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, duplicate.ErrorType);

        // The next rung is accepted.
        var second = await service.CreateReminderAsync(
            new CreateReminderRequest(invoiceNumber, ReminderLevel.Second, today.AddDays(-10), ReminderChannel.Letter),
            Context,
            CancellationToken.None);

        Assert.True(second.Succeeded);

        var reminders = await service.ListRemindersAsync(
            "CLI-A",
            invoiceNumber: null,
            from: null,
            to: null,
            level: null,
            CancellationToken.None);

        Assert.Equal(2, reminders.Count);
    }

    [Fact]
    public async Task A_reminder_can_only_be_recorded_against_an_existing_issued_invoice()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        await SeedOrganizationAsync(dbContext, "CLI-A", "Client A");

        var paid = IssuedInvoice("CLI-A", today.AddDays(-40), 1_000m, 1);
        paid.MarkPaid("tests", DateTimeOffset.UtcNow);

        dbContext.Add(paid);
        await dbContext.SaveChangesAsync();

        var unknown = await service.CreateReminderAsync(
            new CreateReminderRequest("FAC-2026-999999", ReminderLevel.First, today, ReminderChannel.Phone),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.NotFound, unknown.ErrorType);

        var settled = await service.CreateReminderAsync(
            new CreateReminderRequest(paid.Number!, ReminderLevel.First, today, ReminderChannel.Phone),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Validation, settled.ErrorType);
    }

    [Fact]
    public async Task Customer_risk_aggregates_the_outstanding_balance_and_the_dunning_history()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        await SeedOrganizationAsync(dbContext, "CLI-A", "Client A", "CLI-B", "Client B");

        var oldest = IssuedInvoice("CLI-A", today.AddDays(-120), 1_000m, 1);
        var middle = IssuedInvoice("CLI-A", today.AddDays(-100), 2_000m, 2);
        var recent = IssuedInvoice("CLI-A", today.AddDays(-10), 500m, 3);

        // Paid, and belonging to another customer: neither must reach the risk figures.
        var settled = IssuedInvoice("CLI-A", today.AddDays(-200), 9_999m, 4);
        settled.MarkPaid("tests", DateTimeOffset.UtcNow);

        var otherCustomer = IssuedInvoice("CLI-B", today.AddDays(-150), 8_888m, 5);

        dbContext.AddRange(oldest, middle, recent, settled, otherCustomer);
        await dbContext.SaveChangesAsync();

        await AssertRecordedAsync(service, oldest.Number!, ReminderLevel.First, today.AddDays(-60));
        await AssertRecordedAsync(service, middle.Number!, ReminderLevel.FormalNotice, today.AddDays(-20));
        await AssertRecordedAsync(service, oldest.Number!, ReminderLevel.Second, today.AddDays(-5));

        var result = await service.GetCustomerRiskAsync("cli-a", CancellationToken.None);

        Assert.True(result.Succeeded);

        var risk = result.Value!;

        Assert.Equal("CLI-A", risk.CustomerCode);
        Assert.Equal("Client A", risk.CustomerName);
        Assert.Equal(3_500m, risk.OutstandingTotal);
        Assert.Equal(3, risk.OutstandingInvoiceCount);
        Assert.Equal(3_000m, risk.Buckets.Over90);
        Assert.Equal(500m, risk.Buckets.Days1To30);
        Assert.Equal(3_500m, risk.Buckets.Total);

        Assert.Equal(oldest.Number, risk.OldestOutstandingInvoiceNumber);
        Assert.Equal(today.AddDays(-120), risk.OldestOutstandingInvoiceDate);
        Assert.Equal(120, risk.OldestOutstandingInvoiceAgeInDays);
        Assert.Equal(1_000m, risk.OldestOutstandingInvoiceAmount);

        Assert.Equal(3, risk.ReminderCount);

        // The most recent action was a second reminder, but the relationship has already been
        // escalated to a formal notice on another invoice: both facts are reported.
        Assert.Equal(ReminderLevel.Second, risk.LastReminderLevel);
        Assert.Equal(today.AddDays(-5), risk.LastReminderSentAt);
        Assert.Equal(ReminderLevel.FormalNotice, risk.HighestReminderLevel);

        var unknownCustomer = await service.GetCustomerRiskAsync("NOBODY", CancellationToken.None);

        Assert.Equal(ApplicationErrorType.NotFound, unknownCustomer.ErrorType);
    }

    private const string HotelUnitCode = "RECHTL";

    private static async Task AssertRecordedAsync(
        IReceivablesService service,
        string invoiceNumber,
        ReminderLevel level,
        DateOnly sentAt)
    {
        var result = await service.CreateReminderAsync(
            new CreateReminderRequest(invoiceNumber, level, sentAt, ReminderChannel.Letter),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task<RaqmiDbContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<RaqmiDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new RaqmiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        return dbContext;
    }

    private static ReceivablesService CreateService(RaqmiDbContext dbContext)
    {
        return new ReceivablesService(dbContext, new AuditLogWriter(dbContext));
    }

    /// <summary>
    /// Creates the hotel unit and the customers the invoices reference (both are real foreign
    /// keys), and flushes them so the invoices can be added afterwards.
    /// </summary>
    private static async Task SeedOrganizationAsync(
        RaqmiDbContext dbContext,
        params string[] customerCodesAndNames)
    {
        dbContext.Add(new HotelUnit(HotelUnitCode, "Hotel Recouvrement", HotelUnitType.Hotel));

        for (var index = 0; index < customerCodesAndNames.Length; index += 2)
        {
            dbContext.Add(new Customer(
                customerCodesAndNames[index],
                customerCodesAndNames[index + 1],
                CustomerType.Company));
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Builds an invoice already carried through the full draft-to-issue cycle, with a single
    /// VAT-exempt line so the VAT-inclusive total is exactly the amount asked for (the aged
    /// balance sums TotalInclVat).
    /// </summary>
    private static Invoice IssuedInvoice(string customerCode, DateOnly invoiceDate, decimal amount, int sequence)
    {
        var invoice = new Invoice(customerCode, HotelUnitCode, invoiceDate);

        invoice.ReplaceLines(new[] { new InvoiceLine("Hebergement", 1m, amount, 0m) });

        invoice.CaptureIssuerSnapshot(
            "Hotel El Manar Spa",
            "098765432112345",
            "16/00-1234567B99",
            "16012345678",
            "543211234509876",
            "Boulevard des Martyrs");

        invoice.Issue(2026, sequence, "tests", DateTimeOffset.UtcNow);

        return invoice;
    }
}
