using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Receivables;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Receivables;

/// <summary>
/// Receivables and collection. This service produces no financial data of its own: the aged
/// balance and the customer risk view are recomputed from finance.invoices on every call, and
/// the only row this module ever writes is a <see cref="Reminder"/> - the trace of a dunning
/// action a human already performed.
///
/// The module deliberately does not send anything. There is no mail, SMS or postal
/// infrastructure in this repository, and recording a reminder must never be mistaken for
/// having contacted the customer.
/// </summary>
public sealed class ReceivablesService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IReceivablesService
{
    /// <summary>
    /// Shipped inside every payload: the reader must never have to guess which invoices were
    /// counted. See AgingBalanceResponse for why this travels with the figures.
    /// </summary>
    private const string ScopeDescription =
        "Only invoices with status Issued are counted. Draft invoices are not receivables, " +
        "Paid invoices are settled and Cancelled invoices carry no claim. The outstanding " +
        "amount of an invoice is its full VAT-inclusive total: the system records no partial " +
        "settlement of an invoice.";

    /// <summary>
    /// The honest statement of what the buckets measure. See AgingCalculator for why no due
    /// date is involved.
    /// </summary>
    private const string AgingBasisDescription =
        "Ages are counted from the invoice date, not from a due date: the invoice carries no " +
        "due date and the system holds no payment terms, so no payment delay is assumed. " +
        "'Not due' therefore means an invoice dated on the reporting date or later.";

    public async Task<AgingBalanceResponse> GetAgingBalanceAsync(
        DateOnly asOfDate,
        string? customerCode,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerCode = NormalizeNullableCode(customerCode);

        // An aged balance "as of" a date cannot count invoices that did not exist yet on that
        // date, hence the InvoiceDate <= asOfDate filter (the risk view, which is always about
        // today, does not apply it - see GetCustomerRiskAsync).
        var query = dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice => invoice.Status == InvoiceStatus.Issued && invoice.InvoiceDate <= asOfDate);

        if (normalizedCustomerCode is not null)
        {
            query = query.Where(invoice => invoice.CustomerCode == normalizedCustomerCode);
        }

        var outstanding = await query
            .Select(invoice => new OutstandingInvoice(
                invoice.CustomerCode,
                invoice.Number,
                invoice.InvoiceDate,
                invoice.TotalInclVat))
            .ToArrayAsync(cancellationToken);

        var customerNames = await LoadCustomerNamesAsync(
            outstanding.Select(invoice => invoice.CustomerCode).Distinct().ToArray(),
            cancellationToken);

        var customers = outstanding
            .GroupBy(invoice => invoice.CustomerCode)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var oldestInvoiceDate = group.Min(invoice => invoice.InvoiceDate);

                return new CustomerAgingResponse(
                    group.Key,
                    customerNames.GetValueOrDefault(group.Key),
                    group.Count(),
                    oldestInvoiceDate,
                    AgingCalculator.AgeInDays(oldestInvoiceDate, asOfDate),
                    BuildBuckets(group, asOfDate));
            })
            .ToArray();

        return new AgingBalanceResponse(
            asOfDate,
            ScopeDescription,
            AgingBasisDescription,
            customers,
            BuildBuckets(outstanding, asOfDate));
    }

    public async Task<IReadOnlyCollection<ReminderResponse>> ListRemindersAsync(
        string? customerCode,
        string? invoiceNumber,
        DateOnly? from,
        DateOnly? to,
        ReminderLevel? level,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Reminder>().AsNoTracking();

        var normalizedCustomerCode = NormalizeNullableCode(customerCode);

        if (normalizedCustomerCode is not null)
        {
            query = query.Where(reminder => reminder.CustomerCode == normalizedCustomerCode);
        }

        var normalizedInvoiceNumber = NormalizeNullableCode(invoiceNumber);

        if (normalizedInvoiceNumber is not null)
        {
            query = query.Where(reminder => reminder.InvoiceNumber == normalizedInvoiceNumber);
        }

        if (from.HasValue)
        {
            query = query.Where(reminder => reminder.SentAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(reminder => reminder.SentAt <= to.Value);
        }

        if (level.HasValue)
        {
            query = query.Where(reminder => reminder.Level == level.Value);
        }

        var reminders = await query
            .OrderByDescending(reminder => reminder.SentAt)
            .ThenBy(reminder => reminder.InvoiceNumber)
            .ThenBy(reminder => reminder.Level)
            .ToArrayAsync(cancellationToken);

        var customerNames = await LoadCustomerNamesAsync(
            reminders.Select(reminder => reminder.CustomerCode).Distinct().ToArray(),
            cancellationToken);

        return reminders
            .Select(reminder => Map(reminder, customerNames.GetValueOrDefault(reminder.CustomerCode)))
            .ToArray();
    }

    public async Task<ApplicationResult<ReminderResponse>> GetReminderAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Set<Reminder>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (reminder is null)
        {
            return ApplicationResult<ReminderResponse>.NotFound("Reminder was not found.");
        }

        return ApplicationResult<ReminderResponse>.Success(
            Map(reminder, await LoadCustomerNameAsync(reminder.CustomerCode, cancellationToken)));
    }

    public async Task<ApplicationResult<ReminderResponse>> CreateReminderAsync(
        CreateReminderRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
        {
            return ApplicationResult<ReminderResponse>.Validation("Invoice number is required.");
        }

        string normalizedInvoiceNumber;

        try
        {
            normalizedInvoiceNumber = Reminder.NormalizeInvoiceNumber(request.InvoiceNumber);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<ReminderResponse>.Validation(ex.Message);
        }

        if (!Enum.IsDefined(request.Level))
        {
            return ApplicationResult<ReminderResponse>.Validation(
                "Reminder level must be First, Second or FormalNotice.");
        }

        if (!Enum.IsDefined(request.Channel))
        {
            return ApplicationResult<ReminderResponse>.Validation(
                "Reminder channel must be Phone, Email, Letter or InPerson.");
        }

        // The referential invariant that the database cannot carry (finance.invoices.number is
        // nullable for drafts, so it cannot be a foreign-key target - see ReminderConfiguration).
        var invoice = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Number == normalizedInvoiceNumber, cancellationToken);

        if (invoice is null)
        {
            return ApplicationResult<ReminderResponse>.NotFound("Invoice was not found.");
        }

        if (invoice.Status != InvoiceStatus.Issued)
        {
            return ApplicationResult<ReminderResponse>.Validation(
                "A reminder can only be recorded for an issued invoice: a draft is not a receivable, " +
                "and a paid or cancelled invoice is no longer owed.");
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        // A reminder is a declaration about the past: the system sends nothing, so a date in
        // the future would describe an action nobody has performed.
        if (request.SentAt > today)
        {
            return ApplicationResult<ReminderResponse>.Validation(
                "A reminder cannot be dated in the future: it records an action already carried out.");
        }

        if (request.SentAt < invoice.InvoiceDate)
        {
            return ApplicationResult<ReminderResponse>.Validation(
                "A reminder cannot be dated before the invoice it chases.");
        }

        var alreadyRecorded = await dbContext.Set<Reminder>()
            .AnyAsync(
                current => current.InvoiceNumber == normalizedInvoiceNumber && current.Level == request.Level,
                cancellationToken);

        if (alreadyRecorded)
        {
            return ApplicationResult<ReminderResponse>.Conflict(
                "This reminder level has already been recorded for this invoice.");
        }

        Reminder reminder;

        try
        {
            reminder = new Reminder(
                invoice.CustomerCode,
                normalizedInvoiceNumber,
                request.Level,
                request.SentAt,
                request.Channel,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<ReminderResponse>.Validation(ex.Message);
        }

        reminder.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Reminder>().Add(reminder);

        try
        {
            await WriteAuditAsync(
                "finance.receivable.reminder_recorded",
                "finance.reminders",
                reminder.Id,
                context,
                new
                {
                    reminder.CustomerCode,
                    reminder.InvoiceNumber,
                    Level = reminder.Level.ToString(),
                    Channel = reminder.Channel.ToString(),
                    reminder.SentAt
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The check above and this insert are not atomic: a concurrent operator recording
            // the same level for the same invoice loses the race against
            // ux_reminders_invoice_number_level.
            return ApplicationResult<ReminderResponse>.Conflict(
                "This reminder level has already been recorded for this invoice.");
        }

        return ApplicationResult<ReminderResponse>.Success(
            Map(reminder, await LoadCustomerNameAsync(reminder.CustomerCode, cancellationToken)));
    }

    public async Task<ApplicationResult<CustomerRiskResponse>> GetCustomerRiskAsync(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerCode = NormalizeCodeOrEmpty(customerCode);

        var customer = await dbContext.Set<Customer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCustomerCode, cancellationToken);

        if (customer is null)
        {
            return ApplicationResult<CustomerRiskResponse>.NotFound("Customer was not found.");
        }

        var asOfDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        // Unlike the aged balance, the risk view counts every issued invoice whatever its date:
        // an invoice dated in the future is still money owed, and lands in the "not due" bucket.
        var outstanding = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice =>
                invoice.CustomerCode == normalizedCustomerCode &&
                invoice.Status == InvoiceStatus.Issued)
            .Select(invoice => new OutstandingInvoice(
                invoice.CustomerCode,
                invoice.Number,
                invoice.InvoiceDate,
                invoice.TotalInclVat))
            .ToArrayAsync(cancellationToken);

        var reminders = await dbContext.Set<Reminder>()
            .AsNoTracking()
            .Where(reminder => reminder.CustomerCode == normalizedCustomerCode)
            .Select(reminder => new ReminderTrace(reminder.Level, reminder.SentAt, reminder.CreatedAt))
            .ToArrayAsync(cancellationToken);

        var oldest = outstanding
            .OrderBy(invoice => invoice.InvoiceDate)
            .ThenBy(invoice => invoice.Number, StringComparer.Ordinal)
            .FirstOrDefault();

        var lastReminder = reminders
            .OrderByDescending(reminder => reminder.SentAt)
            .ThenByDescending(reminder => reminder.CreatedAt)
            .FirstOrDefault();

        return ApplicationResult<CustomerRiskResponse>.Success(new CustomerRiskResponse(
            customer.Code,
            customer.Name,
            customer.IsActive,
            asOfDate,
            ScopeDescription,
            AgingBasisDescription,
            outstanding.Sum(invoice => invoice.Amount),
            outstanding.Length,
            BuildBuckets(outstanding, asOfDate),
            oldest?.Number,
            oldest?.InvoiceDate,
            oldest is null ? (int?)null : AgingCalculator.AgeInDays(oldest.InvoiceDate, asOfDate),
            oldest?.Amount,
            reminders.Length,
            lastReminder?.Level,
            lastReminder?.SentAt,
            reminders.Length == 0 ? (ReminderLevel?)null : reminders.Max(reminder => reminder.Level)));
    }

    private static AgingBucketsResponse BuildBuckets(
        IEnumerable<OutstandingInvoice> invoices,
        DateOnly asOfDate)
    {
        decimal notDue = 0m;
        decimal days1To30 = 0m;
        decimal days31To60 = 0m;
        decimal days61To90 = 0m;
        decimal over90 = 0m;

        foreach (var invoice in invoices)
        {
            switch (AgingCalculator.Classify(invoice.InvoiceDate, asOfDate))
            {
                case AgingBucket.NotDue:
                    notDue += invoice.Amount;
                    break;
                case AgingBucket.Days1To30:
                    days1To30 += invoice.Amount;
                    break;
                case AgingBucket.Days31To60:
                    days31To60 += invoice.Amount;
                    break;
                case AgingBucket.Days61To90:
                    days61To90 += invoice.Amount;
                    break;
                default:
                    over90 += invoice.Amount;
                    break;
            }
        }

        return new AgingBucketsResponse(
            notDue,
            days1To30,
            days31To60,
            days61To90,
            over90,
            notDue + days1To30 + days31To60 + days61To90 + over90);
    }

    private static ReminderResponse Map(Reminder reminder, string? customerName)
    {
        return new ReminderResponse(
            reminder.Id,
            reminder.CustomerCode,
            customerName,
            reminder.InvoiceNumber,
            reminder.Level,
            reminder.SentAt,
            reminder.Channel,
            reminder.Notes,
            reminder.CreatedAt,
            reminder.CreatedBy,
            reminder.UpdatedAt,
            reminder.UpdatedBy);
    }

    private async Task<string?> LoadCustomerNameAsync(string customerCode, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customer.Code == customerCode)
            .Select(customer => customer.Name)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadCustomerNamesAsync(
        string[] customerCodes,
        CancellationToken cancellationToken)
    {
        if (customerCodes.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        return await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customerCodes.Contains(customer.Code))
            .ToDictionaryAsync(customer => customer.Code, customer => customer.Name, cancellationToken);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Explicit flush after the audit write, mirroring BillingService: AuditLogWriter.WriteAsync
    /// already calls SaveChangesAsync internally, so this is usually a no-op - it exists so
    /// persistence never silently depends on the audit writer's internals.
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

    /// <summary>
    /// Flat projection of the only invoice columns the receivables views need. Keeps the whole
    /// Invoice aggregate (and its lines) out of memory for a report that can span every open
    /// invoice of the establishment.
    /// </summary>
    private sealed record OutstandingInvoice(
        string CustomerCode,
        string? Number,
        DateOnly InvoiceDate,
        decimal Amount);

    private sealed record ReminderTrace(
        ReminderLevel Level,
        DateOnly SentAt,
        DateTimeOffset CreatedAt);
}
