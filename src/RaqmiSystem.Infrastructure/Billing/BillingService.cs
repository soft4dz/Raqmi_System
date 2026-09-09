using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Catalog;
using RaqmiSystem.Application.Fiscalite;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Fiscalite;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Billing;

/// <summary>
/// Facturation : clients et factures de vente. Consomme deux contrats publies par d'autres
/// modules et n'en reimplemente aucun : <see cref="ICatalogService"/> pour construire une ligne
/// depuis un code article (prix et TVA repris de l'article) et savoir, a l'emission, quelles
/// lignes sortent du stock ; <see cref="IStockOperationService"/> pour la sortie elle-meme, qui
/// est ecrite dans la MEME transaction que l'emission ; <see cref="ITreasuryService"/> pour
/// l'encaissement reel qu'un reglement cree, dans la meme transaction que le passage a Payee ;
/// <see cref="IVatRegisterService"/> pour la ligne du registre TVA ventes qu'une emission cree.
/// </summary>
public sealed class BillingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IApplicationSettingsService applicationSettingsService,
    ICatalogService catalogService,
    IStockOperationService stockOperations,
    ITreasuryService treasuryService,
    IVatRegisterService vatRegisterService) : IBillingService
{
    /// <summary>
    /// Ce que repond un reglement sans mode de paiement : la facture est payee aux yeux de la
    /// facturation, mais RIEN n'est entre en caisse. Dit explicitement pour qu'un appelant qui a
    /// oublie le mode de paiement ne prenne pas ce statut pour un encaissement.
    /// </summary>
    private const string PaidWithoutReceiptNotice =
        "Facture marquee payee sans encaissement en tresorerie : precisez le mode de paiement " +
        "(et la caisse ou le compte bancaire encaisseur) pour que le reglement cree l'encaissement.";

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
            var (lines, lineFailure) = await BuildLinesAsync(request.Lines, cancellationToken);

            if (lineFailure is not null)
            {
                return ApplicationResult<InvoiceResponse>.Validation(lineFailure);
            }

            invoice.ReplaceLines(lines);
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
            var (lines, lineFailure) = await BuildLinesAsync(request.Lines, cancellationToken);

            if (lineFailure is not null)
            {
                return ApplicationResult<InvoiceResponse>.Validation(lineFailure);
            }

            invoice.ReplaceLines(lines);
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
        IssueInvoiceRequest? request,
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

        // Les lignes qui sortent du stock sont connues AVANT toute mutation : un magasin manquant
        // doit refuser l'emission en laissant la facture intacte, comme les autres controles
        // prealables ci-dessous.
        var (stockLines, stockFailure) = await ResolveStockLinesAsync(invoice, cancellationToken);

        if (stockFailure is not null)
        {
            return ApplicationResult<InvoiceResponse>.Validation(stockFailure);
        }

        var warehouseCode = NormalizeNullableCode(request?.WarehouseCode);

        if (stockLines.Count > 0 && warehouseCode is null)
        {
            return ApplicationResult<InvoiceResponse>.Validation(
                "Cette facture porte des lignes d'articles suivis en stock : indiquez le magasin de sortie pour l'emettre.");
        }

        // The emitter's identity comes from the global settings (module "Parametrage global").
        // GetAsync never fails and never 404s: on an installation where nothing has been
        // configured yet it returns the defaults (IsConfigured = false) carrying the placeholder
        // establishment name. Issuing on top of those defaults would freeze an unidentified
        // emitter into a legal document AND burn a slot in the numbering sequence, with no
        // possible correction afterwards, so issuance is refused until the establishment is
        // identified. The check runs BEFORE any snapshot capture and before the sequence is
        // allocated: a refused issuance must leave the invoice a pristine Draft.
        var settings = await applicationSettingsService.GetAsync(cancellationToken);
        var missingIssuerIdentity = DescribeMissingIssuerIdentity(settings);

        if (missingIssuerIdentity is not null)
        {
            return ApplicationResult<InvoiceResponse>.Validation(missingIssuerIdentity);
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

            // Same legal immutability, applied to the EMITTER: a commercial document must
            // identify who issued it, and a later change to the establishment's own
            // identification must not rewrite invoices already issued under the previous one.
            invoice.CaptureIssuerSnapshot(
                settings.CompanyName,
                settings.CompanyNif,
                settings.CompanyRc,
                settings.CompanyAi,
                settings.CompanyNis,
                settings.CompanyAddress);

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

        if (stockLines.Count > 0)
        {
            // Facture ET sortie de stock dans une seule transaction (voir IssueWithStockOutflowAsync).
            var outflowFailure = await IssueWithStockOutflowAsync(
                invoice,
                warehouseCode!,
                stockLines,
                context,
                cancellationToken);

            if (outflowFailure is not null)
            {
                return outflowFailure;
            }
        }
        else
        {
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
        }

        // Registre TVA ventes : ecrit une fois le numero definitif de la facture connu (les deux
        // branches ci-dessus l'ont deja committe), pour ne jamais figer un numero qui serait
        // ensuite reassigne par la reprise sur collision.
        await vatRegisterService.RegisterSaleAsync(
            new RegisterVatSaleRequest(
                invoice.Id,
                invoice.Number!,
                DateOnly.FromDateTime(now.UtcDateTime),
                VatMovementType.Vente,
                invoice.CustomerNameSnapshot ?? invoice.CustomerCode,
                invoice.CustomerNifSnapshot,
                invoice.TotalExclVat,
                invoice.TotalVat),
            context,
            cancellationToken);

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

    public async Task<ApplicationResult<InvoicePaymentResponse>> PayInvoiceAsync(
        Guid id,
        PayInvoiceRequest? request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request?.Method is null)
        {
            // Chemin historique, conserve tel quel : la facture est marquee payee, aucun
            // encaissement n'est cree, et la reponse le dit.
            var marked = await ChangeInvoiceStatusAsync(
                id,
                context,
                "finance.invoice.paid",
                invoice => invoice.MarkPaid(context.UserName, DateTimeOffset.UtcNow),
                cancellationToken);

            return marked.Succeeded && marked.Value is not null
                ? ApplicationResult<InvoicePaymentResponse>.Success(
                    new InvoicePaymentResponse(marked.Value, Receipt: null, PaidWithoutReceiptNotice))
                : MirrorFailure<InvoiceResponse, InvoicePaymentResponse>(marked);
        }

        // ENCAISSEMENT REEL. Le passage a Payee et la creation de l'encaissement tiennent dans
        // une transaction Serializable : deux reglements concurrents de la meme facture liraient
        // tous deux "Emise" et creeraient deux encaissements. Le claim conditionnel (UPDATE ...
        // WHERE status = 'Issued', motif de LodgingService) fait echouer le second au commit -
        // PostgreSQL le refuse en erreur de serialisation, SQLite en base verrouillee - et les
        // deux echecs sont rendus comme un 409 rejouable sans rien avoir ecrit.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var invoice = await dbContext.Set<Invoice>()
                .Include(current => current.Lines)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (invoice is null)
            {
                return ApplicationResult<InvoicePaymentResponse>.NotFound("Invoice was not found.");
            }

            if (invoice.CashReceiptId is not null)
            {
                return ApplicationResult<InvoicePaymentResponse>.Conflict(
                    "Cette facture est deja rattachee a un encaissement : un reglement rejoue ne le double pas.");
            }

            if (invoice.Status != InvoiceStatus.Issued)
            {
                return ApplicationResult<InvoicePaymentResponse>.Validation("Only issued invoices can be marked as paid.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimInvoiceStatusAsync(invoice.Id, InvoiceStatus.Issued, now, cancellationToken))
            {
                return ApplicationResult<InvoicePaymentResponse>.Conflict(
                    "Cette facture vient d'etre reglee par une operation concurrente : rien n'a ete ecrit.");
            }

            // L'encaissement est cree PAR LE MODULE TRESORERIE : ses regles (piece obligatoire sur
            // cheque et virement, caisse ou banque obligatoire hors especes, unite active) valent
            // ici comme au guichet, et un refus est rendu tel quel. Reference = la piece du
            // paiement quand il y en a une, sinon le numero de la facture ; la facture reglee est
            // toujours nommee dans les notes : c'est le lien lisible de la caisse vers la facture.
            var notes = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Reglement de la facture {invoice.Number}."
                : $"Reglement de la facture {invoice.Number}. {request.Notes.Trim()}";

            var created = await treasuryService.CreateReceiptAsync(
                new CreateCashReceiptRequest(
                    request.ReceiptDate ?? DateOnly.FromDateTime(now.UtcDateTime),
                    invoice.HotelUnitCode,
                    request.Method.Value,
                    invoice.TotalInclVat,
                    string.IsNullOrWhiteSpace(request.Reference) ? invoice.Number : request.Reference,
                    request.BankAccountCode,
                    notes),
                context,
                cancellationToken);

            if (!created.Succeeded || created.Value is null)
            {
                return MirrorFailure<CashReceiptResponse, InvoicePaymentResponse>(created);
            }

            // Confirme dans la foulee : une facture Payee en face d'un encaissement encore en
            // brouillon serait de l'argent que la synthese de tresorerie ne compterait pas.
            var confirmed = await treasuryService.ConfirmReceiptAsync(created.Value.Id, context, cancellationToken);

            if (!confirmed.Succeeded || confirmed.Value is null)
            {
                return MirrorFailure<CashReceiptResponse, InvoicePaymentResponse>(confirmed);
            }

            try
            {
                invoice.MarkPaid(context.UserName, now);
                invoice.AttachCashReceipt(confirmed.Value.Id);
            }
            catch (InvalidOperationException ex)
            {
                return ApplicationResult<InvoicePaymentResponse>.Validation(ex.Message);
            }

            invoice.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "finance.invoice.paid",
                "finance.invoices",
                invoice.Id,
                context,
                new
                {
                    invoice.Number,
                    invoice.CustomerCode,
                    Status = invoice.Status.ToString(),
                    invoice.CashReceiptId,
                    Method = request.Method.Value.ToString(),
                    invoice.TotalInclVat
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<InvoicePaymentResponse>.Success(
                new InvoicePaymentResponse(
                    Map(invoice, await LoadCustomerNameAsync(invoice.CustomerCode, cancellationToken)),
                    confirmed.Value,
                    Notice: null));
        }
        catch (Exception ex) when (ex.IsSerializationFailure())
        {
            return ApplicationResult<InvoicePaymentResponse>.Conflict(
                "Cette facture etait reglee par une operation concurrente : le reglement a ete annule et rien n'a ete ecrit.");
        }
    }

    /// <summary>
    /// Forme atomique de "cette facture est toujours dans le statut attendu" : l'invariant voyage
    /// comme clause WHERE d'un UPDATE conditionnel (le motif de LodgingService et
    /// AccountingService). La seule colonne ecrite, UpdatedAt, est celle que le reglement
    /// estampille de toute facon avec le meme horodatage.
    /// </summary>
    private async Task<bool> TryClaimInvoiceStatusAsync(
        Guid invoiceId,
        InvoiceStatus expectedStatus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<Invoice>()
            .Where(current => current.Id == invoiceId && current.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    /// <summary>
    /// Re-type un resultat en echec venant d'un collaborateur (la tresorerie, le chemin
    /// historique) sans perdre ni sa nature d'erreur ni son message.
    /// </summary>
    private static ApplicationResult<TTarget> MirrorFailure<TSource, TTarget>(ApplicationResult<TSource> source)
    {
        var message = source.Error ?? "L'operation a ete refusee.";

        return source.ErrorType switch
        {
            ApplicationErrorType.NotFound => ApplicationResult<TTarget>.NotFound(message),
            ApplicationErrorType.Conflict => ApplicationResult<TTarget>.Conflict(message),
            _ => ApplicationResult<TTarget>.Validation(message)
        };
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

    /// <summary>
    /// Tells whether the establishment is identified well enough to appear as the EMITTER of an
    /// invoice, and describes what is missing when it is not. Two distinct gaps are covered: the
    /// settings row was never written at all (<c>IsConfigured</c> false, the payload being the
    /// placeholder defaults), and the row exists but omits mandatory legal mentions - the update
    /// request only requires a company name, so a "configured" establishment can still be
    /// fiscally unidentified. Returns null when issuance may proceed.
    /// </summary>
    private static string? DescribeMissingIssuerIdentity(ApplicationSettingsResponse settings)
    {
        if (!settings.IsConfigured)
        {
            return "The establishment's global settings must be filled in before an invoice can be issued.";
        }

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.CompanyNif))
        {
            missing.Add("NIF");
        }

        if (string.IsNullOrWhiteSpace(settings.CompanyRc))
        {
            missing.Add("RC");
        }

        if (string.IsNullOrWhiteSpace(settings.CompanyAi))
        {
            missing.Add("AI");
        }

        if (string.IsNullOrWhiteSpace(settings.CompanyAddress))
        {
            missing.Add("address");
        }

        if (missing.Count == 0)
        {
            return null;
        }

        return "The establishment's legal identification is incomplete and an invoice cannot be issued. " +
            $"Missing from the global settings: {string.Join(", ", missing)}.";
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

    /// <summary>
    /// Construit les lignes d'une facture. Une ligne LIBRE porte tout elle-meme ; une ligne
    /// d'ARTICLE reprend du catalogue ce qu'elle n'a pas surcharge (designation, prix HT, taux
    /// de TVA) et garde le code de l'article. Les valeurs reprises sont FIGEES dans la ligne : la
    /// facture ne suit plus les modifications ulterieures du catalogue. Les articles sont resolus
    /// en une requete pour toute la facture. Rend le message du premier refus, ou null.
    /// </summary>
    private async Task<(List<InvoiceLine> Lines, string? Failure)> BuildLinesAsync(
        IReadOnlyCollection<InvoiceLineRequest> requests,
        CancellationToken cancellationToken)
    {
        var articleCodes = requests
            .Where(line => !string.IsNullOrWhiteSpace(line.ArticleCode))
            .Select(line => line.ArticleCode!)
            .ToArray();

        var articles = articleCodes.Length == 0
            ? new Dictionary<string, ArticleResponse>(StringComparer.Ordinal)
            : (await catalogService.FindArticlesAsync(articleCodes, cancellationToken))
                .ToDictionary(article => article.Code, StringComparer.Ordinal);

        var lines = new List<InvoiceLine>(requests.Count);
        var lineNumber = 1;

        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.ArticleCode))
            {
                if (request.UnitPrice is null || request.VatRate is null)
                {
                    return (lines, $"Ligne {lineNumber} : le prix unitaire et le taux de TVA sont obligatoires sur une ligne sans article.");
                }

                lines.Add(new InvoiceLine(
                    request.Designation ?? string.Empty,
                    request.Quantity,
                    request.UnitPrice.Value,
                    request.VatRate.Value,
                    articleCode: null,
                    request.VatAmount));
            }
            else
            {
                var code = request.ArticleCode.Trim().ToUpperInvariant();

                if (!articles.TryGetValue(code, out var article))
                {
                    return (lines, $"Ligne {lineNumber} : l'article '{code}' est introuvable au catalogue.");
                }

                if (!article.IsActive)
                {
                    return (lines, $"Ligne {lineNumber} : l'article '{code}' est inactif et ne peut plus etre vendu.");
                }

                lines.Add(new InvoiceLine(
                    string.IsNullOrWhiteSpace(request.Designation) ? article.Designation : request.Designation,
                    request.Quantity,
                    request.UnitPrice ?? article.UnitPriceExclVat,
                    request.VatRate ?? article.VatRate,
                    article.Code,
                    request.VatAmount));
            }

            lineNumber++;
        }

        return (lines, null);
    }

    /// <summary>
    /// Les lignes qui sortent du stock, resolues A L'EMISSION et non a la saisie : c'est la
    /// definition de l'article au jour ou la marchandise part qui dit si elle part et d'ou. Un
    /// article inactif entre-temps sort quand meme (la vente a ete conclue) ; un article qui
    /// n'existe plus refuse l'emission, le brouillon doit etre corrige.
    /// </summary>
    private async Task<(List<StockExitLine> Lines, string? Failure)> ResolveStockLinesAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var articleLines = invoice.Lines
            .Where(line => line.ArticleCode is not null)
            .OrderBy(line => line.LineNumber)
            .ToArray();

        var stockLines = new List<StockExitLine>();

        if (articleLines.Length == 0)
        {
            return (stockLines, null);
        }

        var articles = (await catalogService.FindArticlesAsync(
                articleLines.Select(line => line.ArticleCode!).Distinct(StringComparer.Ordinal).ToArray(),
                cancellationToken))
            .ToDictionary(article => article.Code, StringComparer.Ordinal);

        foreach (var line in articleLines)
        {
            if (!articles.TryGetValue(line.ArticleCode!, out var article))
            {
                return (stockLines, $"Ligne {line.LineNumber} : l'article '{line.ArticleCode}' n'existe plus au catalogue ; corrigez le brouillon avant de l'emettre.");
            }

            if (article.TracksStock && article.StockItemCode is not null)
            {
                stockLines.Add(new StockExitLine(article.StockItemCode, line.Quantity));
            }
        }

        return (stockLines, null);
    }

    /// <summary>
    /// Emission AVEC sortie de stock : la facture emise et les mouvements de vente sont ecrits
    /// dans une seule transaction Serializable - la sortie re-derive le stock disponible a
    /// l'interieur (garde jamais-negatif du module Stocks) et un stock insuffisant abandonne tout,
    /// facture comprise, qui reste un brouillon sans numero consomme.
    ///
    /// POURQUOI PAS DE RETENTATIVE SUR COLLISION DE NUMERO, contrairement au chemin sans stock :
    /// PostgreSQL refuse toute instruction apres un echec dans un bloc de transaction, une
    /// retentative dans la meme transaction est donc impossible, et une retentative hors
    /// transaction devrait rejouer la sortie de stock. La collision - rare - est rendue comme un
    /// 409 rejouable : rien n'a ete ecrit, l'appelant recommence. Rend null quand tout est ecrit.
    /// </summary>
    private async Task<ApplicationResult<InvoiceResponse>?> IssueWithStockOutflowAsync(
        Invoice invoice,
        string warehouseCode,
        IReadOnlyList<StockExitLine> stockLines,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var sale = await stockOperations.RegisterSaleAsync(
                new RegisterSaleRequest(warehouseCode, invoice.Number!, stockLines),
                context,
                cancellationToken);

            if (!sale.Succeeded)
            {
                // Rien n'a ete ecrit : la transaction est abandonnee sans commit et la facture
                // reste, en base, le brouillon qu'elle etait. Le refus du module Stocks est rendu
                // tel quel : il nomme l'article, le magasin et le disponible.
                var message = sale.Error ?? "La sortie de stock a ete refusee.";

                return sale.ErrorType switch
                {
                    ApplicationErrorType.NotFound => ApplicationResult<InvoiceResponse>.NotFound(message),
                    ApplicationErrorType.Conflict => ApplicationResult<InvoiceResponse>.Conflict(message),
                    _ => ApplicationResult<InvoiceResponse>.Validation(message)
                };
            }

            // La sortie a vidange la facture emise avec ses mouvements (meme DbContext) ; quand la
            // vente etait deja enregistree sous ce numero, elle n'a rien ecrit et la facture
            // attend encore : cette vidange couvre les deux cas.
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return null;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            var statusInDatabase = await dbContext.Set<Invoice>()
                .AsNoTracking()
                .Where(current => current.Id == invoice.Id)
                .Select(current => current.Status)
                .SingleAsync(cancellationToken);

            return statusInDatabase != InvoiceStatus.Draft
                ? ApplicationResult<InvoiceResponse>.Conflict("The invoice has already been issued by a concurrent operation.")
                : ApplicationResult<InvoiceResponse>.Conflict("Invoice number allocation conflict. Please retry the operation.");
        }
        catch (Exception ex) when (ex.IsSerializationFailure())
        {
            return ApplicationResult<InvoiceResponse>.Conflict(
                "Une autre operation ecrivait le meme stock au meme instant : l'emission a ete annulee " +
                "et rien n'a ete ecrit. Reessayez.");
        }
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
                line.LineTotalInclVat,
                line.ArticleCode))
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
            invoice.IssuerNameSnapshot,
            invoice.IssuerNifSnapshot,
            invoice.IssuerRcSnapshot,
            invoice.IssuerAiSnapshot,
            invoice.IssuerNisSnapshot,
            invoice.IssuerAddressSnapshot,
            invoice.CreatedAt,
            invoice.CreatedBy,
            invoice.UpdatedAt,
            invoice.UpdatedBy,
            invoice.CashReceiptId);
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
