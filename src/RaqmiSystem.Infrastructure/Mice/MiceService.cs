using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Mice;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Mice;

/// <summary>
/// Module 10.6 - volet evenementiel : espaces de reception, evenements, devis, BEO et facturation.
///
/// DEUX INVARIANTS PORTENT CE SERVICE.
///
/// 1. UN ESPACE N'ACCUEILLE QU'UN EVENEMENT A LA FOIS, et la comparaison se fait sur la fenetre
///    REELLE d'occupation, montage et demontage compris. Le controle tourne dans une transaction
///    Serializable avec l'insertion, exactement comme la creation de reservation de chambre : hors
///    transaction, deux ventes simultanees lisent toutes deux "la salle est libre" et valident
///    toutes deux. Sous PostgreSQL le perdant recoit une erreur de serialisation, sous le
///    fournisseur SQLite des tests un "database is locked" ; les deux sont rendus en 409.
///
/// 2. LA FACTURE EST PRODUITE PAR LE MODULE FACTURATION, jamais ici. Une facture d'evenement doit
///    etre de la meme nature que toutes les autres - meme numerotation, meme instantane client,
///    meme registre des ventes. Ce service se contente d'appeler IBillingService et de retenir
///    l'identifiant obtenu, ce qui gele ensuite les lignes du devis.
///
/// CE QUE CE SERVICE NE FAIT PAS : ni allotement, ni rooming list. Ces deux fonctions portent sur
/// les CHAMBRES et devraient etre soustraites a la disponibilite ET au garde de creation de
/// reservation, sans quoi l'hotel survendrait en silence. Elles relevent du coeur du PMS.
/// </summary>
public sealed partial class MiceService(
    RaqmiDbContext dbContext,
    IBillingService billingService,
    ILodgingService lodgingService) : IMiceService
{
    private const string SpaceNotFound = "L'espace de reception est introuvable.";

    private const string EventNotFound = "L'evenement est introuvable.";

    private const string SpaceAlreadyBooked =
        "Cet espace est deja occupe sur ce creneau, montage et demontage compris.";

    // ------------------------------- Espaces de reception -------------------------------

    public async Task<IReadOnlyCollection<FunctionSpaceResponse>> ListFunctionSpacesAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FunctionSpaces.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            var normalized = HotelUnit.NormalizeCode(hotelUnitCode);
            query = query.Where(space => space.HotelUnitCode == normalized);
        }

        if (!includeInactive)
        {
            query = query.Where(space => space.IsActive);
        }

        var spaces = await query
            .OrderBy(space => space.HotelUnitCode)
            .ThenBy(space => space.Code)
            .ToListAsync(cancellationToken);

        // Compteur d'evenements A VENIR et non annules : c'est ce qui dit a l'exploitant si une
        // salle peut etre desactivee sans consequence.
        var today = DateOnly.FromDateTime(DateTime.Today);

        var upcoming = await dbContext.EventBookings
            .AsNoTracking()
            .Where(booking => booking.Status != EventBookingStatus.Cancelled && booking.EventDate >= today)
            .GroupBy(booking => new { booking.HotelUnitCode, booking.FunctionSpaceCode })
            .Select(group => new { group.Key.HotelUnitCode, group.Key.FunctionSpaceCode, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var counts = upcoming.ToDictionary(
            item => $"{item.HotelUnitCode}/{item.FunctionSpaceCode}",
            item => item.Count,
            StringComparer.Ordinal);

        return spaces
            .Select(space => Map(
                space,
                counts.TryGetValue($"{space.HotelUnitCode}/{space.Code}", out var count) ? count : 0))
            .ToList();
    }

    public async Task<ApplicationResult<FunctionSpaceResponse>> CreateFunctionSpaceAsync(
        string hotelUnitCode,
        string code,
        SaveFunctionSpaceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unit = await ResolveUnitAsync(hotelUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<FunctionSpaceResponse>.NotFound("L'unite hoteliere est introuvable.");
        }

        FunctionSpace space;

        try
        {
            space = new FunctionSpace(
                unit,
                code,
                request.Label,
                request.MaxAttendance,
                request.AreaSquareMeters,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<FunctionSpaceResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.FunctionSpaces
            .AnyAsync(item => item.HotelUnitCode == space.HotelUnitCode && item.Code == space.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<FunctionSpaceResponse>.Conflict(
                $"L'espace {space.Code} existe deja pour cette unite.");
        }

        var nowUtc = DateTimeOffset.UtcNow;
        space.MarkCreated(context.UserName, nowUtc);

        dbContext.FunctionSpaces.Add(space);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<FunctionSpaceResponse>.Success(Map(space, 0));
    }

    public async Task<ApplicationResult<FunctionSpaceResponse>> UpdateFunctionSpaceAsync(
        string hotelUnitCode,
        string code,
        SaveFunctionSpaceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var space = await FindSpaceAsync(hotelUnitCode, code, cancellationToken);

        if (space is null)
        {
            return ApplicationResult<FunctionSpaceResponse>.NotFound(SpaceNotFound);
        }

        try
        {
            space.UpdateDetails(request.Label, request.MaxAttendance, request.AreaSquareMeters, request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<FunctionSpaceResponse>.Validation(ex.Message);
        }

        space.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<FunctionSpaceResponse>.Success(Map(space, 0));
    }

    public async Task<ApplicationResult<FunctionSpaceResponse>> SetFunctionSpaceActiveAsync(
        string hotelUnitCode,
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var space = await FindSpaceAsync(hotelUnitCode, code, cancellationToken);

        if (space is null)
        {
            return ApplicationResult<FunctionSpaceResponse>.NotFound(SpaceNotFound);
        }

        space.SetActive(isActive);
        space.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<FunctionSpaceResponse>.Success(Map(space, 0));
    }

    // ------------------------------------ Evenements ------------------------------------

    public async Task<IReadOnlyCollection<EventBookingResponse>> ListEventsAsync(
        string? hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        string? functionSpaceCode,
        bool includeCancelled,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EventBookings
            .AsNoTracking()
            .Include(booking => booking.Lines)
            .Include(booking => booking.Schedule)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            var normalized = HotelUnit.NormalizeCode(hotelUnitCode);
            query = query.Where(booking => booking.HotelUnitCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(functionSpaceCode))
        {
            var normalized = FunctionSpace.NormalizeCode(functionSpaceCode);
            query = query.Where(booking => booking.FunctionSpaceCode == normalized);
        }

        if (from is { } fromDate)
        {
            query = query.Where(booking => booking.EventDate >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(booking => booking.EventDate <= toDate);
        }

        if (!includeCancelled)
        {
            query = query.Where(booking => booking.Status != EventBookingStatus.Cancelled);
        }

        var bookings = await query
            .OrderBy(booking => booking.EventDate)
            .ThenBy(booking => booking.OccupiedFrom)
            .ToListAsync(cancellationToken);

        return await MapManyAsync(bookings, cancellationToken);
    }

    public async Task<ApplicationResult<EventBookingResponse>> GetEventAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var booking = await LoadEventAsync(id, tracking: false, cancellationToken);

        if (booking is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(EventNotFound);
        }

        return ApplicationResult<EventBookingResponse>.Success(await MapAsync(booking, cancellationToken));
    }

    public async Task<ApplicationResult<EventBookingResponse>> CreateEventAsync(
        CreateEventBookingRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!TryParseSetupStyle(request.SetupStyle, out var setupStyle))
        {
            return ApplicationResult<EventBookingResponse>.Validation(
                $"Disposition de salle inconnue : {request.SetupStyle}.");
        }

        var unit = await ResolveUnitAsync(request.HotelUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound("L'unite hoteliere est introuvable.");
        }

        var space = await FindSpaceAsync(unit, request.FunctionSpaceCode, cancellationToken);

        if (space is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(SpaceNotFound);
        }

        if (!space.IsActive)
        {
            return ApplicationResult<EventBookingResponse>.Validation(
                "Cet espace est desactive : il ne peut plus recevoir de nouvel evenement.");
        }

        var customerFailure = await ValidateCustomerAsync(request.CustomerCode, cancellationToken);

        if (customerFailure is not null)
        {
            return ApplicationResult<EventBookingResponse>.Validation(customerFailure);
        }

        EventBooking booking;

        try
        {
            booking = new EventBooking(
                unit,
                request.Reference,
                space.Code,
                request.CustomerCode,
                request.Title,
                request.EventDate,
                request.StartTime,
                request.DurationMinutes,
                request.SetupMinutes,
                request.TeardownMinutes,
                setupStyle,
                request.ExpectedAttendance,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<EventBookingResponse>.Validation(ex.Message);
        }

        if (booking.ExpectedAttendance > space.MaxAttendance)
        {
            return ApplicationResult<EventBookingResponse>.Validation(
                $"L'espace {space.Code} accueille au maximum {space.MaxAttendance} personnes "
                + $"({booking.ExpectedAttendance} attendues).");
        }

        var referenceTaken = await dbContext.EventBookings
            .AnyAsync(
                item => item.HotelUnitCode == booking.HotelUnitCode && item.Reference == booking.Reference,
                cancellationToken);

        if (referenceTaken)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(
                $"La reference {booking.Reference} est deja utilisee dans cette unite.");
        }

        // GARDE ANTI-DOUBLE-RESERVATION : le controle de chevauchement doit tourner DANS la
        // transaction Serializable avec l'insertion. Verifie a l'exterieur, deux ventes simultanees
        // lisent toutes deux "libre" et valident toutes deux.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var overlapping = await dbContext.EventBookings
                .Where(Overlaps(booking.HotelUnitCode, booking.FunctionSpaceCode, booking.OccupiedFrom, booking.OccupiedTo))
                .AnyAsync(cancellationToken);

            if (overlapping)
            {
                return ApplicationResult<EventBookingResponse>.Conflict(SpaceAlreadyBooked);
            }

            booking.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
            dbContext.EventBookings.Add(booking);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(SpaceAlreadyBooked);
        }

        return ApplicationResult<EventBookingResponse>.Success(await MapAsync(booking, cancellationToken));
    }

    public async Task<ApplicationResult<EventBookingResponse>> UpdateEventAsync(
        Guid id,
        UpdateEventBookingRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!TryParseSetupStyle(request.SetupStyle, out var setupStyle))
        {
            return ApplicationResult<EventBookingResponse>.Validation(
                $"Disposition de salle inconnue : {request.SetupStyle}.");
        }

        var booking = await LoadEventAsync(id, tracking: true, cancellationToken);

        if (booking is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(EventNotFound);
        }

        var space = await FindSpaceAsync(booking.HotelUnitCode, booking.FunctionSpaceCode, cancellationToken);

        if (space is not null && request.ExpectedAttendance > space.MaxAttendance)
        {
            return ApplicationResult<EventBookingResponse>.Validation(
                $"L'espace {space.Code} accueille au maximum {space.MaxAttendance} personnes "
                + $"({request.ExpectedAttendance} attendues).");
        }

        try
        {
            booking.UpdateDetails(request.Title, setupStyle, request.ExpectedAttendance, request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<EventBookingResponse>.Validation(ex.Message);
        }

        booking.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<EventBookingResponse>.Success(await MapAsync(booking, cancellationToken));
    }

    public async Task<ApplicationResult<EventBookingResponse>> RescheduleEventAsync(
        Guid id,
        RescheduleEventBookingRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var booking = await LoadEventAsync(id, tracking: true, cancellationToken);

        if (booking is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(EventNotFound);
        }

        var space = await FindSpaceAsync(booking.HotelUnitCode, request.FunctionSpaceCode, cancellationToken);

        if (space is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(SpaceNotFound);
        }

        if (!space.IsActive && !string.Equals(space.Code, booking.FunctionSpaceCode, StringComparison.Ordinal))
        {
            return ApplicationResult<EventBookingResponse>.Validation(
                "Cet espace est desactive : il ne peut plus recevoir d'evenement.");
        }

        if (booking.ExpectedAttendance > space.MaxAttendance)
        {
            return ApplicationResult<EventBookingResponse>.Validation(
                $"L'espace {space.Code} accueille au maximum {space.MaxAttendance} personnes "
                + $"({booking.ExpectedAttendance} attendues).");
        }

        var previousSpace = booking.FunctionSpaceCode;

        try
        {
            booking.Reschedule(
                request.EventDate,
                request.StartTime,
                request.DurationMinutes,
                request.SetupMinutes,
                request.TeardownMinutes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<EventBookingResponse>.Validation(ex.Message);
        }

        var targetSpace = space.Code;

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            // On s'exclut soi-meme du controle : un evenement ne peut pas entrer en conflit avec
            // lui-meme lorsqu'on ne fait que l'allonger ou le decaler de quelques minutes.
            var overlapping = await dbContext.EventBookings
                .Where(item => item.Id != booking.Id)
                .Where(Overlaps(booking.HotelUnitCode, targetSpace, booking.OccupiedFrom, booking.OccupiedTo))
                .AnyAsync(cancellationToken);

            if (overlapping)
            {
                return ApplicationResult<EventBookingResponse>.Conflict(SpaceAlreadyBooked);
            }

            if (!string.Equals(previousSpace, targetSpace, StringComparison.Ordinal))
            {
                booking.MoveToSpace(targetSpace);
            }

            booking.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(SpaceAlreadyBooked);
        }

        return ApplicationResult<EventBookingResponse>.Success(await MapAsync(booking, cancellationToken));
    }

    public async Task<ApplicationResult<EventBookingResponse>> ConfirmEventAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var booking = await LoadEventAsync(id, tracking: true, cancellationToken);

        if (booking is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(EventNotFound);
        }

        try
        {
            booking.Confirm(context.UserName, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(ex.Message);
        }

        booking.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<EventBookingResponse>.Success(await MapAsync(booking, cancellationToken));
    }

    public async Task<ApplicationResult<EventBookingResponse>> CancelEventAsync(
        Guid id,
        CancelEventBookingRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var booking = await LoadEventAsync(id, tracking: true, cancellationToken);

        if (booking is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(EventNotFound);
        }

        try
        {
            booking.Cancel(request.Reason, context.UserName, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<EventBookingResponse>.Validation(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(ex.Message);
        }

        booking.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<EventBookingResponse>.Success(await MapAsync(booking, cancellationToken));
    }

    // ------------------------------- Devis et BEO -------------------------------

    public async Task<ApplicationResult<EventBookingResponse>> ReplaceEventLinesAsync(
        Guid id,
        IReadOnlyCollection<EventBookingLineRequest> lines,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var booking = await LoadEventAsync(id, tracking: true, cancellationToken);

        if (booking is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(EventNotFound);
        }

        var materialized = new List<EventBookingLine>(lines.Count);

        try
        {
            foreach (var line in lines)
            {
                materialized.Add(new EventBookingLine(line.Designation, line.Quantity, line.UnitPrice, line.VatRate));
            }

            booking.ReplaceLines(materialized);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<EventBookingResponse>.Validation(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(ex.Message);
        }

        booking.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<EventBookingResponse>.Success(await MapAsync(booking, cancellationToken));
    }

    public async Task<ApplicationResult<EventBookingResponse>> ReplaceEventScheduleAsync(
        Guid id,
        IReadOnlyCollection<EventScheduleItemRequest> schedule,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var booking = await LoadEventAsync(id, tracking: true, cancellationToken);

        if (booking is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(EventNotFound);
        }

        var materialized = new List<EventScheduleItem>(schedule.Count);

        try
        {
            foreach (var item in schedule)
            {
                materialized.Add(new EventScheduleItem(item.StartTime, item.Description, item.Department));
            }

            booking.ReplaceSchedule(materialized);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<EventBookingResponse>.Validation(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(ex.Message);
        }

        booking.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<EventBookingResponse>.Success(await MapAsync(booking, cancellationToken));
    }

    // -------------------------- Facturation evenementielle --------------------------

    public async Task<ApplicationResult<EventBookingResponse>> InvoiceEventAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var booking = await LoadEventAsync(id, tracking: true, cancellationToken);

        if (booking is null)
        {
            return ApplicationResult<EventBookingResponse>.NotFound(EventNotFound);
        }

        if (booking.IsInvoiced)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(
                "Cet evenement a deja ete facture.");
        }

        if (booking.Status != EventBookingStatus.Confirmed)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(
                "Seul un evenement confirme peut etre facture.");
        }

        if (booking.Lines.Count == 0)
        {
            return ApplicationResult<EventBookingResponse>.Validation(
                "Cet evenement ne porte aucune ligne chiffree : il n'y a rien a facturer.");
        }

        // La facture est creee PAR LE MODULE FACTURATION. On ne reimplemente ni la numerotation, ni
        // l'instantane client, ni l'entree au registre des ventes : une facture d'evenement doit
        // etre exactement de la meme nature que les autres.
        var invoiceRequest = new CreateInvoiceRequest(
            booking.CustomerCode,
            booking.HotelUnitCode,
            booking.EventDate,
            booking.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new InvoiceLineRequest(
                    line.Designation,
                    line.Quantity,
                    line.UnitPrice,
                    line.VatRate))
                .ToList());

        var invoice = await billingService.CreateInvoiceAsync(invoiceRequest, context, cancellationToken);

        if (!invoice.Succeeded || invoice.Value is null)
        {
            // On rend le refus du module facturation TEL QUEL : il connait ses propres regles
            // (client inactif, unite inconnue, taux refuse) mieux que ce module ne les devinerait.
            return invoice.ErrorType == ApplicationErrorType.NotFound
                ? ApplicationResult<EventBookingResponse>.NotFound(invoice.Error!)
                : ApplicationResult<EventBookingResponse>.Validation(invoice.Error!);
        }

        try
        {
            booking.AttachInvoice(invoice.Value.Id);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<EventBookingResponse>.Conflict(ex.Message);
        }

        booking.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<EventBookingResponse>.Success(await MapAsync(booking, cancellationToken));
    }

    // ------------------------------------ Interne ------------------------------------

    /// <summary>
    /// Chevauchement de deux fenetres d'occupation, montage et demontage compris. Le test est
    /// strict aux bornes : un evenement demontant a 18:00 et un autre montant a 18:00 ne se
    /// chevauchent PAS, ce qui est bien le comportement voulu pour deux creneaux qui se succedent.
    /// </summary>
    private static Expression<Func<EventBooking, bool>> Overlaps(
        string hotelUnitCode,
        string functionSpaceCode,
        DateTime occupiedFrom,
        DateTime occupiedTo)
    {
        return booking => booking.Status != EventBookingStatus.Cancelled
            && booking.HotelUnitCode == hotelUnitCode
            && booking.FunctionSpaceCode == functionSpaceCode
            && booking.OccupiedFrom < occupiedTo
            && booking.OccupiedTo > occupiedFrom;
    }

    private async Task<string?> ResolveUnitAsync(string hotelUnitCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            return null;
        }

        var normalized = HotelUnit.NormalizeCode(hotelUnitCode);

        var exists = await dbContext.HotelUnits
            .AsNoTracking()
            .AnyAsync(unit => unit.Code == normalized, cancellationToken);

        return exists ? normalized : null;
    }

    private async Task<string?> ValidateCustomerAsync(string customerCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
        {
            return "Le code client est requis.";
        }

        var normalized = customerCode.Trim().ToUpperInvariant();

        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == normalized, cancellationToken);

        if (customer is null)
        {
            return "Le client est introuvable.";
        }

        return customer.IsActive ? null : "Aucun evenement ne peut etre pris pour un client inactif.";
    }

    private async Task<FunctionSpace?> FindSpaceAsync(
        string hotelUnitCode,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hotelUnitCode) || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        string normalizedUnit;
        string normalizedCode;

        try
        {
            normalizedUnit = HotelUnit.NormalizeCode(hotelUnitCode);
            normalizedCode = FunctionSpace.NormalizeCode(code);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return await dbContext.FunctionSpaces
            .FirstOrDefaultAsync(
                space => space.HotelUnitCode == normalizedUnit && space.Code == normalizedCode,
                cancellationToken);
    }

    private async Task<EventBooking?> LoadEventAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = dbContext.EventBookings
            .Include(booking => booking.Lines)
            .Include(booking => booking.Schedule)
            .AsQueryable();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);
    }

    private static bool TryParseSetupStyle(string value, out EventSetupStyle setupStyle)
    {
        return Enum.TryParse(value, ignoreCase: true, out setupStyle)
            && Enum.IsDefined(setupStyle);
    }

    private static FunctionSpaceResponse Map(FunctionSpace space, int upcomingEventCount)
    {
        return new FunctionSpaceResponse(
            space.HotelUnitCode,
            space.Code,
            space.Label,
            space.MaxAttendance,
            space.AreaSquareMeters,
            space.Notes,
            space.IsActive,
            upcomingEventCount);
    }

    private async Task<EventBookingResponse> MapAsync(EventBooking booking, CancellationToken cancellationToken)
    {
        var mapped = await MapManyAsync([booking], cancellationToken);

        return mapped[0];
    }

    /// <summary>
    /// Projection groupee : les libelles d'espace, de client et le numero de facture sont resolus
    /// en trois requetes pour l'ensemble du lot, et non une par evenement.
    /// </summary>
    private async Task<IReadOnlyList<EventBookingResponse>> MapManyAsync(
        IReadOnlyCollection<EventBooking> bookings,
        CancellationToken cancellationToken)
    {
        if (bookings.Count == 0)
        {
            return [];
        }

        var spaceKeys = bookings
            .Select(booking => new { booking.HotelUnitCode, booking.FunctionSpaceCode })
            .Distinct()
            .ToList();

        var unitCodes = spaceKeys.Select(key => key.HotelUnitCode).Distinct().ToList();

        var spaces = await dbContext.FunctionSpaces
            .AsNoTracking()
            .Where(space => unitCodes.Contains(space.HotelUnitCode))
            .Select(space => new { space.HotelUnitCode, space.Code, space.Label, space.MaxAttendance })
            .ToListAsync(cancellationToken);

        var spaceLookup = spaces.ToDictionary(
            space => $"{space.HotelUnitCode}/{space.Code}",
            space => space,
            StringComparer.Ordinal);

        var customerCodes = bookings.Select(booking => booking.CustomerCode).Distinct().ToList();

        var customers = await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customerCodes.Contains(customer.Code))
            .Select(customer => new { customer.Code, customer.Name })
            .ToListAsync(cancellationToken);

        var customerLookup = customers.ToDictionary(
            customer => customer.Code,
            customer => customer.Name,
            StringComparer.Ordinal);

        var invoiceIds = bookings
            .Where(booking => booking.InvoiceId is not null)
            .Select(booking => booking.InvoiceId!.Value)
            .Distinct()
            .ToList();

        var invoiceNumbers = invoiceIds.Count == 0
            ? []
            : await dbContext.Invoices
                .AsNoTracking()
                .Where(invoice => invoiceIds.Contains(invoice.Id))
                .Select(invoice => new { invoice.Id, invoice.Number })
                .ToListAsync(cancellationToken);

        var invoiceLookup = invoiceNumbers.ToDictionary(invoice => invoice.Id, invoice => invoice.Number);

        return bookings.Select(booking =>
        {
            var spaceKey = $"{booking.HotelUnitCode}/{booking.FunctionSpaceCode}";
            spaceLookup.TryGetValue(spaceKey, out var space);

            return new EventBookingResponse(
                booking.Id,
                booking.HotelUnitCode,
                booking.Reference,
                booking.FunctionSpaceCode,
                space?.Label ?? booking.FunctionSpaceCode,
                booking.CustomerCode,
                customerLookup.TryGetValue(booking.CustomerCode, out var name) ? name : booking.CustomerCode,
                booking.Title,
                booking.EventDate,
                booking.StartTime,
                booking.DurationMinutes,
                booking.SetupMinutes,
                booking.TeardownMinutes,
                booking.OccupiedFrom,
                booking.OccupiedTo,
                booking.SetupStyle.ToString(),
                booking.ExpectedAttendance,
                space?.MaxAttendance ?? 0,
                booking.Status.ToString(),
                booking.Notes,
                booking.CancelReason,
                booking.InvoiceId,
                booking.InvoiceId is { } invoiceId && invoiceLookup.TryGetValue(invoiceId, out var number)
                    ? number
                    : null,
                booking.TotalExclVat,
                booking.TotalVat,
                booking.TotalInclVat,
                booking.Lines
                    .OrderBy(line => line.LineNumber)
                    .Select(line => new EventBookingLineResponse(
                        line.Id,
                        line.LineNumber,
                        line.Designation,
                        line.Quantity,
                        line.UnitPrice,
                        line.VatRate,
                        line.LineTotalExclVat,
                        line.VatAmount,
                        line.LineTotalInclVat))
                    .ToList(),
                booking.Schedule
                    .OrderBy(item => item.StartTime)
                    .Select(item => new EventScheduleItemResponse(
                        item.Id,
                        item.StartTime,
                        item.Description,
                        item.Department))
                    .ToList());
        }).ToList();
    }
}
