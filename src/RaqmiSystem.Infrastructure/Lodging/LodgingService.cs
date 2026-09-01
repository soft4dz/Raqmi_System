using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using System.Linq.Expressions;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Le PMS hotelier : parc de chambres, inventaire, disponibilite, reservations, sejours, folios,
/// previsionnel et night audit.
///
/// UNE SOURCE DE VERITE, UN SEUL CALCUL. Toutes les operations qui vendent ou tiennent une chambre
/// passent par <see cref="BuildRoomTypeAvailabilityAsync"/>, qui alimente
/// <see cref="AvailabilityCalculator"/> avec les memes sources : parc physique, blocages OOO/OOS,
/// reservations bloquantes, allotements de groupe, autorisations de surreservation. La recherche,
/// la creation, le walk-in, l'affectation, le changement de chambre, la prolongation, le
/// changement de type, le previsionnel et - le jour ou ils existeront - le moteur de reservation
/// directe et le channel manager lisent tous ce meme calcul. Deux chemins qui compteraient
/// differemment finiraient par ne plus etre d'accord, et l'ecart se paierait en survente
/// silencieuse.
///
/// CONCURRENCE. L'invariant central - deux reservations de la meme chambre ne se chevauchent
/// jamais - ne s'exprime pas comme une contrainte de ligne : il est tenu avec le meme motif de
/// garde atomique que <c>AccountingService</c> / <c>UserAdministrationService</c>, une transaction
/// Serializable, le controle rejoue A L'INTERIEUR de cette transaction, et les echecs de
/// serialisation remontes en 409 rejouables plutot qu'en 500. Les transitions de statut qui lisent
/// un etat qu'une requete concurrente pourrait invalider (arrivee ouvrant le folio, depart
/// affirmant un solde nul, ligne de folio en course avec un depart) utilisent la variante par
/// claim conditionnel du meme motif.
///
/// La classe est decoupee en fichiers partiels par domaine de responsabilite : Rooms, Inventory,
/// Availability, Reservations, Stay, Folios, Catalog, Operations.
/// </summary>
public sealed partial class LodgingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ITariffResolutionService tariffResolutionService)
    : ILodgingService, ILodgingInventoryService, ILodgingCatalogService, ILodgingOperationsService
{
    private const string RoomTypesEntity = "lodging.room_types";

    private const string RoomsEntity = "lodging.rooms";

    private const string ReservationsEntity = "lodging.reservations";

    private const string FoliosEntity = "lodging.folios";

    private const string RoomBlocksEntity = "lodging.room_blocks";

    private const string RestrictionsEntity = "lodging.rate_restrictions";

    private const string OverbookingEntity = "lodging.overbooking_allowances";

    private const string PoliciesEntity = "lodging.lodging_policies";

    private const string ExtrasEntity = "lodging.extra_items";

    private const string PackagesEntity = "lodging.packages";

    private const string DepositsEntity = "lodging.deposits";

    private const string CancellationPoliciesEntity = "lodging.cancellation_policies";

    private const string YieldRulesEntity = "lodging.yield_rules";

    private const string NightAuditEntity = "lodging.night_audit_runs";

    /// <summary>
    /// Reponse rendue quand le claim atomique constate que le dossier n'est plus dans le statut ou
    /// la requete l'avait lu, ou quand la base a refuse de serialiser des transactions
    /// concurrentes. Rien n'a ete modifie dans les deux cas.
    /// </summary>
    private const string ConcurrentReservationMutationRefused =
        "Ce dossier vient d'etre modifie par une operation concurrente : le changement a ete annule "
        + "et rien n'a ete modifie. Rechargez le dossier et reessayez.";

    private const string RoomAlreadyReserved =
        "La chambre est deja reservee sur cette periode par un autre dossier.";

    /// <summary>
    /// L'occupation est calculee jour par jour en memoire ; une fenetre sans borne transformerait
    /// une requete en travail arbitraire.
    /// </summary>
    private const int MaxOccupancyWindowDays = 366;

    /// <summary>
    /// Une recherche de disponibilite resout un tarif par type et par nuit ; une fenetre plus
    /// longue qu'une saison n'est plus une recherche de reservation, c'est un traitement par lot.
    /// </summary>
    private const int MaxAvailabilityWindowNights = 92;

    /// <summary>Le previsionnel va jusqu'a un an : au-dela il ne previent plus rien d'utile.</summary>
    private const int MaxForecastDays = 365;

    /// <summary>
    /// LA regle de chevauchement de l'invariant anti-double-reservation, sous forme d'expression
    /// traduisible en SQL, en UN SEUL endroit : un dossier tient sa chambre sur [arrivee, depart)
    /// quand son statut tient l'inventaire (<see cref="ReservationStatuses.Blocks"/>) et que sa
    /// propre periode demi-ouverte la recouvre (<see cref="Reservation.PeriodsOverlap"/>).
    ///
    /// Une simple DEMANDE (Inquiry) n'y figure pas : elle ne tient rien, sans quoi un formulaire
    /// web ferait fermer l'hotel. La garde de creation, la recherche de disponibilite et le calcul
    /// d'inventaire filtrent par cette meme expression - ils ne peuvent pas diverger et se
    /// contredire sur ce qui est libre.
    /// </summary>
    private static Expression<Func<Reservation, bool>> BlocksPeriod(
        DateOnly arrivalDate,
        DateOnly departureDate)
    {
        return reservation => reservation.Status != ReservationStatus.Inquiry
            && reservation.Status != ReservationStatus.Cancelled
            && reservation.Status != ReservationStatus.NoShow
            && reservation.ArrivalDate < departureDate
            && reservation.DepartureDate > arrivalDate;
    }

    /// <summary>La meme regle, appliquee en memoire a un dossier deja charge.</summary>
    private static bool BlocksPeriod(Reservation reservation, DateOnly arrivalDate, DateOnly departureDate)
    {
        return reservation.IsBlocking
            && Reservation.PeriodsOverlap(
                reservation.ArrivalDate,
                reservation.DepartureDate,
                arrivalDate,
                departureDate);
    }

    // ==================================== Helpers partages ====================================

    /// <summary>
    /// Forme atomique de "ce dossier est toujours dans le statut attendu" : l'invariant voyage
    /// comme clause WHERE d'un UPDATE conditionnel sur la ligne du dossier (le motif
    /// claim-en-une-instruction de <c>AccountingService.TryClaimDraftEntryAsync</c>). La seule
    /// colonne ecrite, UpdatedAt, est celle que la mutation de l'appelant estampille de toute
    /// facon avec le meme horodatage : le claim n'ajoute aucun etat, il a seulement besoin d'etre
    /// une ecriture.
    /// </summary>
    private async Task<bool> TryClaimReservationStatusAsync(
        Guid reservationId,
        ReservationStatus expectedStatus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<Reservation>()
            .Where(current => current.Id == reservationId && current.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    /// <summary>
    /// Variante du claim pour les gestes de sejour qui n'ont pas UN statut attendu mais une
    /// FAMILLE (tous les statuts d'avant-arrivee, par exemple) : la clause WHERE porte alors sur
    /// l'ensemble, et le geste ne s'applique qu'a une ligne que la base vient de reconfirmer.
    /// </summary>
    private async Task<bool> TryClaimReservationStatusesAsync(
        Guid reservationId,
        IReadOnlyCollection<ReservationStatus> expectedStatuses,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var statuses = expectedStatuses.ToArray();

        var claimedRows = await dbContext.Set<Reservation>()
            .Where(current => current.Id == reservationId && statuses.Contains(current.Status))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    /// <summary>
    /// Charge une unite hoteliere pour une operation ecrivante et refuse proprement celles qui
    /// manquent ou sont inactives. Rend le code normalise pour que les appelants cessent de le
    /// renormaliser.
    /// </summary>
    private async Task<(ApplicationResult<T>? Failure, string UnitCode)> RequireActiveHotelUnitAsync<T>(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return (ApplicationResult<T>.Validation("Le code de l'unite hoteliere est requis."), string.Empty);
        }

        var unit = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (unit is null)
        {
            return (ApplicationResult<T>.NotFound("L'unite hoteliere est introuvable."), normalizedUnitCode);
        }

        if (!unit.IsActive)
        {
            return (
                ApplicationResult<T>.Validation("Cette operation n'est pas autorisee sur une unite inactive."),
                normalizedUnitCode);
        }

        return (null, normalizedUnitCode);
    }

    /// <summary>Verifie l'existence d'une unite pour une operation en LECTURE.</summary>
    private async Task<ApplicationResult<T>?> RequireHotelUnitAsync<T>(
        string normalizedUnitCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return ApplicationResult<T>.Validation("Le code de l'unite hoteliere est requis.");
        }

        var exists = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .AnyAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        return exists ? null : ApplicationResult<T>.NotFound("L'unite hoteliere est introuvable.");
    }

    private async Task<ApplicationResult<T>?> RequireActiveRoomTypeAsync<T>(
        string hotelUnitCode,
        string roomTypeCode,
        CancellationToken cancellationToken)
    {
        var roomType = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                current => current.HotelUnitCode == hotelUnitCode && current.Code == roomTypeCode,
                cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<T>.NotFound(
                $"Le type de chambre '{roomTypeCode}' est introuvable dans l'unite '{hotelUnitCode}'.");
        }

        if (!roomType.IsActive)
        {
            return ApplicationResult<T>.Validation(
                $"Le type de chambre '{roomTypeCode}' est inactif et ne peut pas etre utilise.");
        }

        return null;
    }

    /// <summary>
    /// Re-type un <see cref="ApplicationResult{T}"/> en echec venant d'un collaborateur (ici le
    /// resolveur tarifaire) sans perdre ni sa nature d'erreur ni son message.
    /// </summary>
    private static ApplicationResult<TTarget> MirrorFailure<TSource, TTarget>(
        ApplicationResult<TSource> source,
        string fallbackMessage)
    {
        var message = source.Error ?? fallbackMessage;

        return source.ErrorType switch
        {
            ApplicationErrorType.NotFound => ApplicationResult<TTarget>.NotFound(message),
            ApplicationErrorType.Conflict => ApplicationResult<TTarget>.Conflict(message),
            _ => ApplicationResult<TTarget>.Validation(message)
        };
    }

    private async Task<string?> LoadRoomNumberAsync(Guid? roomId, CancellationToken cancellationToken)
    {
        if (roomId is not { } id)
        {
            return null;
        }

        return await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => room.Id == id)
            .Select(room => room.Number)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadRoomNumbersAsync(
        Guid[] roomIds,
        CancellationToken cancellationToken)
    {
        if (roomIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => roomIds.Contains(room.Id))
            .ToDictionaryAsync(room => room.Id, room => room.Number, cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadCustomerNamesAsync(
        IReadOnlyCollection<string> customerCodes,
        CancellationToken cancellationToken)
    {
        if (customerCodes.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var codes = customerCodes.Distinct().ToArray();

        return await dbContext.Set<Domain.Billing.Customer>()
            .AsNoTracking()
            .Where(customer => codes.Contains(customer.Code))
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
    /// Vidange explicite apres l'ecriture d'audit. AuditLogWriter.WriteAsync appelle deja
    /// SaveChangesAsync en interne (persistant les changements en attente en meme temps que la
    /// ligne d'audit), de sorte que cet appel est habituellement sans effet - il existe pour que la
    /// persistance ne depende jamais silencieusement des details du writer d'audit.
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
