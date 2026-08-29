using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Billing;

public sealed class BillingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IBillingService
{
    public async Task<IReadOnlyCollection<CustomerResponse>> ListCustomersAsync(
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Customer>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(customer => customer.IsActive);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();

        if (normalizedSearch is not null)
        {
            query = query.Where(customer =>
                customer.Code.Contains(normalizedSearch) ||
                customer.Name.ToUpper().Contains(normalizedSearch));
        }

        var customers = await query
            .OrderBy(customer => customer.Code)
            .ToArrayAsync(cancellationToken);

        return customers.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<CustomerResponse>> GetCustomerAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var customer = await dbContext.Set<Customer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (customer is null)
        {
            return ApplicationResult<CustomerResponse>.NotFound("Customer was not found.");
        }

        return ApplicationResult<CustomerResponse>.Success(Map(customer));
    }

    public async Task<ApplicationResult<CustomerResponse>> CreateCustomerAsync(
        CreateCustomerRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(request.Code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<CustomerResponse>.Validation("Customer code is required.");
        }

        var exists = await dbContext.Set<Customer>()
            .AnyAsync(current => current.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            return ApplicationResult<CustomerResponse>.Conflict("A customer with this code already exists.");
        }

        Customer customer;

        try
        {
            customer = new Customer(
                normalizedCode,
                request.Name,
                request.CustomerType,
                request.Nif,
                request.Rc,
                request.Ai,
                request.Nis,
                request.Address,
                request.City,
                request.Phone,
                request.Email);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CustomerResponse>.Validation(ex.Message);
        }

        customer.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Customer>().Add(customer);

        try
        {
            await WriteAuditAsync(
                "finance.customer.created",
                "finance.customers",
                customer.Id,
                context,
                new { customer.Code, customer.Name, CustomerType = customer.CustomerType.ToString() },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create with
            // the same code loses the race against the unique index on customers.code.
            return ApplicationResult<CustomerResponse>.Conflict("A customer with this code already exists.");
        }

        return ApplicationResult<CustomerResponse>.Success(Map(customer));
    }

    public async Task<ApplicationResult<CustomerResponse>> UpdateCustomerAsync(
        string code,
        UpdateCustomerRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var customer = await dbContext.Set<Customer>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (customer is null)
        {
            return ApplicationResult<CustomerResponse>.NotFound("Customer was not found.");
        }

        try
        {
            customer.UpdateDetails(
                request.Name,
                request.CustomerType,
                request.Nif,
                request.Rc,
                request.Ai,
                request.Nis,
                request.Address,
                request.City,
                request.Phone,
                request.Email);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<CustomerResponse>.Validation(ex.Message);
        }

        customer.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "finance.customer.updated",
            "finance.customers",
            customer.Id,
            context,
            new { customer.Code, customer.Name, CustomerType = customer.CustomerType.ToString() },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CustomerResponse>.Success(Map(customer));
    }

    public async Task<ApplicationResult<CustomerResponse>> SetCustomerActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var customer = await dbContext.Set<Customer>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (customer is null)
        {
            return ApplicationResult<CustomerResponse>.NotFound("Customer was not found.");
        }

        if (isActive)
        {
            customer.Activate();
        }
        else
        {
            customer.Deactivate();
        }

        customer.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "finance.customer.activated" : "finance.customer.deactivated",
            "finance.customers",
            customer.Id,
            context,
            new { customer.Code, customer.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<CustomerResponse>.Success(Map(customer));
    }

    public async Task<IReadOnlyCollection<InvoiceResponse>> ListInvoicesAsync(
        DateOnly? from,
        DateOnly? to,
        string? customerCode,
        string? hotelUnitCode,
        InvoiceStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Invoice>()
            .AsNoTracking()
            .Include(invoice => invoice.Lines)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(invoice => invoice.InvoiceDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(invoice => invoice.InvoiceDate <= to.Value);
        }

        var normalizedCustomerCode = NormalizeNullableCode(customerCode);

        if (normalizedCustomerCode is not null)
        {
            query = query.Where(invoice => invoice.CustomerCode == normalizedCustomerCode);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(invoice => invoice.HotelUnitCode == normalizedUnitCode);
        }

        if (status.HasValue)
        {
            query = query.Where(invoice => invoice.Status == status.Value);
        }

        var invoices = await query
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .ThenBy(invoice => invoice.CustomerCode)
            .ToArrayAsync(cancellationToken);

        var customerNames = await LoadCustomerNamesAsync(
            invoices.Select(invoice => invoice.CustomerCode).Distinct().ToArray(),
            cancellationToken);

        return invoices
            .Select(invoice => Map(invoice, customerNames.GetValueOrDefault(invoice.CustomerCode)))
            .ToArray();
    }

    public async Task<ApplicationResult<InvoiceResponse>> GetInvoiceAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (invoice is null)
        {
            return ApplicationResult<InvoiceResponse>.NotFound("Invoice was not found.");
        }

        return ApplicationResult<InvoiceResponse>.Success(
            Map(invoice, await LoadCustomerNameAsync(invoice.CustomerCode, cancellationToken)));
    }

    public async Task<ApplicationResult<InvoiceResponse>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return ApplicationResult<InvoiceResponse>.Validation("Invoice must contain at least one line.");
        }

        var normalizedCustomerCode = NormalizeCodeOrEmpty(request.CustomerCode);

        if (string.IsNullOrWhiteSpace(normalizedCustomerCode))
        {
            return ApplicationResult<InvoiceResponse>.Validation("Customer code is required.");
        }

        var normalizedUnitCode = NormalizeCodeOrEmpty(request.HotelUnitCode);

        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return ApplicationResult<InvoiceResponse>.Validation("Hotel unit code is required.");
        }

        var customer = await dbContext.Set<Customer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCustomerCode, cancellationToken);

        if (customer is null)
        {
            return ApplicationResult<InvoiceResponse>.NotFound("Customer was not found.");
        }

        if (!customer.IsActive)
        {
            return ApplicationResult<InvoiceResponse>.Validation("Invoices cannot be created for an inactive customer.");
        }

        var unit = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<InvoiceResponse>.NotFound("Hotel unit was not found.");
        }

        if (!unit.IsActive)
        {
            return ApplicationResult<InvoiceResponse>.Validation("Invoices cannot be created for an inactive hotel unit.");
        }

        Invoice invoice;

        try
        {
            invoice = new Invoice(normalizedCustomerCode, normalizedUnitCode, request.InvoiceDate);
            invoice.ReplaceLines(BuildLines(request.Lines));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<InvoiceResponse>.Validation(ex.Message);
        }

        invoice.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Invoice>().Add(invoice);

        await WriteAuditAsync(
            "finance.invoice.created",
            "finance.invoices",
            invoice.Id,
            context,
            new { invoice.CustomerCode, invoice.HotelUnitCode, invoice.InvoiceDate, invoice.TotalInclVat },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<InvoiceResponse>.Success(Map(invoice, customer.Name));
    }

    public async Task<ApplicationResult<InvoiceResponse>> UpdateInvoiceLinesAsync(
        Guid id,
        UpdateInvoiceLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return ApplicationResult<InvoiceResponse>.Validation("Invoice must contain at least one line.");
        }

        var invoice = await dbContext.Set<Invoice>()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (invoice is null)
        {
            return ApplicationResult<InvoiceResponse>.NotFound("Invoice was not found.");
        }

        try
        {
            invoice.ReplaceLines(BuildLines(request.Lines));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<InvoiceResponse>.Validation(ex.Message);
        }

        invoice.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "finance.invoice.lines_updated",
            "finance.invoices",
            invoice.Id,
            context,
            new { invoice.CustomerCode, LineCount = invoice.Lines.Count, invoice.TotalInclVat },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<InvoiceResponse>.Success(
            Map(invoice, await LoadCustomerNameAsync(invoice.CustomerCode, cancellationToken)));
    }

    public async Task<ApplicationResult<InvoiceResponse>> IssueInvoiceAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Set<Invoice>()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (invoice is null)
        {
            return ApplicationResult<InvoiceResponse>.NotFound("Invoice was not found.");
        }

        var customer = await dbContext.Set<Customer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == invoice.CustomerCode, cancellationToken);

        if (customer is null)
        {
            return ApplicationResult<InvoiceResponse>.Validation("The invoice's customer no longer exists.");
        }

        var now = DateTimeOffset.UtcNow;

        // The legal numbering follows the ISSUE date, not the (backdatable) invoice date:
        // FAC-{year}- sequences are per issuance year, so an invoice antedated to a previous
        // year still consumes a number in the year it is actually issued. This matches the
        // norm that the chronological, gapless numbering tracks emission.
        var year = now.Year;

        try
        {
            // Freeze the customer's identification as of the moment of issuance (legal
            // immutability): later customer edits must never rewrite an issued invoice.
            invoice.CaptureCustomerSnapshot(
                customer.Name,
                customer.Nif,
                customer.Rc,
                customer.Ai,
                customer.Nis,
                customer.Address);

            invoice.Issue(
                year,
                await NextIssueSequenceAsync(year, cancellationToken),
                context.UserName,
                now);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<InvoiceResponse>.Validation(ex.Message);
        }

        invoice.MarkUpdated(context.UserName, now);

        // The number is allocated as SELECT max(sequence)+1 protected by the unique index
        // ux_invoices_issued_year_sequence. If a concurrent issue won the race, the save throws
        // a unique-violation DbUpdateException and we retry exactly once with a freshly computed
        // sequence; a second collision surfaces as a 409. Only unique violations are treated as
        // sequence collisions - any other DbUpdateException keeps propagating.
        //
        // Note on optimistic concurrency: Invoice carries no rowversion/xmin token (the
        // Npgsql-specific xmin mapping would break the SQLite test provider - same constraint
        // as documented on CashReceiptConfiguration). A double-click on /issue is instead
        // neutralized by the unique index plus the DB status re-check below: when the unique
        // violation was caused by the same invoice having been issued by a concurrent request,
        // we return a clean 409 instead of silently burning a second legal number.
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            var statusInDatabase = await dbContext.Set<Invoice>()
                .AsNoTracking()
                .Where(current => current.Id == id)
                .Select(current => current.Status)
                .SingleAsync(cancellationToken);

            if (statusInDatabase != InvoiceStatus.Draft)
            {
                return ApplicationResult<InvoiceResponse>.Conflict(
                    "The invoice has already been issued by a concurrent operation.");
            }

            try
            {
                invoice.ReassignIssueNumber(year, await NextIssueSequenceAsync(year, cancellationToken));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException retryEx) when (retryEx.IsUniqueViolation())
            {
                return ApplicationResult<InvoiceResponse>.Conflict(
                    "Invoice number allocation conflict. Please retry the operation.");
            }
        }

        await WriteAuditAsync(
            "finance.invoice.issued",
            "finance.invoices",
            invoice.Id,
            context,
            new { invoice.Number, invoice.CustomerCode, invoice.TotalInclVat },
            cancellationToken);

        return ApplicationResult<InvoiceResponse>.Success(
            Map(invoice, await LoadCustomerNameAsync(invoice.CustomerCode, cancellationToken)));
    }

    public async Task<ApplicationResult<InvoiceResponse>> MarkInvoicePaidAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangeInvoiceStatusAsync(
            id,
            context,
            "finance.invoice.paid",
            invoice => invoice.MarkPaid(context.UserName, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<ApplicationResult<InvoiceResponse>> CancelInvoiceAsync(
        Guid id,
        CancelInvoiceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangeInvoiceStatusAsync(
            id,
            context,
            "finance.invoice.cancelled",
            invoice => invoice.Cancel(request.Reason, context.UserName, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private async Task<ApplicationResult<InvoiceResponse>> ChangeInvoiceStatusAsync(
        Guid id,
        OperationContext context,
        string auditAction,
        Action<Invoice> change,
        CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Set<Invoice>()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (invoice is null)
        {
            return ApplicationResult<InvoiceResponse>.NotFound("Invoice was not found.");
        }

        try
        {
            change(invoice);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<InvoiceResponse>.Validation(ex.Message);
        }

        invoice.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            auditAction,
            "finance.invoices",
            invoice.Id,
            context,
            new { invoice.Number, invoice.CustomerCode, Status = invoice.Status.ToString(), invoice.CancellationReason },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<InvoiceResponse>.Success(
            Map(invoice, await LoadCustomerNameAsync(invoice.CustomerCode, cancellationToken)));
    }

    private async Task<int> NextIssueSequenceAsync(int year, CancellationToken cancellationToken)
    {
        var maxSequence = await dbContext.Set<Invoice>()
            .Where(invoice => invoice.IssuedYear == year)
            .MaxAsync(invoice => (int?)invoice.IssuedSequence, cancellationToken);

        return (maxSequence ?? 0) + 1;
    }

    private static List<InvoiceLine> BuildLines(IReadOnlyCollection<InvoiceLineRequest> requests)
    {
        return requests
            .Select(line => new InvoiceLine(line.Designation, line.Quantity, line.UnitPrice, line.VatRate))
            .ToList();
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

    private static CustomerResponse Map(Customer customer)
    {
        return new CustomerResponse(
            customer.Id,
            customer.Code,
            customer.Name,
            customer.CustomerType,
            customer.Nif,
            customer.Rc,
            customer.Ai,
            customer.Nis,
            customer.Address,
            customer.City,
            customer.Phone,
            customer.Email,
            customer.IsActive,
            customer.CreatedAt,
            customer.CreatedBy,
            customer.UpdatedAt,
            customer.UpdatedBy);
    }

    private static InvoiceResponse Map(Invoice invoice, string? customerName)
    {
        // Legal immutability: once an invoice has left the Draft state (Issued/Paid/Cancelled
        // after issue), the customer name frozen at issue time is rendered instead of the live
        // customer record. Drafts (and invoices cancelled while still drafts, which never
        // captured a snapshot) keep following the current customer.
        var displayedCustomerName = invoice.Status != InvoiceStatus.Draft && invoice.CustomerNameSnapshot is not null
            ? invoice.CustomerNameSnapshot
            : customerName;

        var lines = invoice.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line => new InvoiceLineResponse(
                line.Id,
                line.LineNumber,
                line.Designation,
                line.Quantity,
                line.UnitPrice,
                line.VatRate,
                line.LineTotalExclVat,
                line.VatAmount,
                line.LineTotalInclVat))
            .ToArray();

        return new InvoiceResponse(
            invoice.Id,
            invoice.Number,
            invoice.CustomerCode,
            displayedCustomerName,
            invoice.HotelUnitCode,
            invoice.InvoiceDate,
            invoice.Status,
            invoice.TotalExclVat,
            invoice.TotalVat,
            invoice.TotalInclVat,
            lines,
            invoice.CanEdit,
            invoice.IssuedAt,
            invoice.IssuedBy,
            invoice.PaidAt,
            invoice.PaidBy,
            invoice.CancelledAt,
            invoice.CancelledBy,
            invoice.CancellationReason,
            invoice.CreatedAt,
            invoice.CreatedBy,
            invoice.UpdatedAt,
            invoice.UpdatedBy);
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
}
