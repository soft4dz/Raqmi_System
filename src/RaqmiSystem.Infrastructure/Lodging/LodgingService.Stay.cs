using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Le deroulement d'un sejour : affectation, arrivee, changement de chambre, changement de type,
/// prolongation, depart, annulation et no-show.
/// </summary>
public sealed partial class LodgingService
{
    /// <summary>
    /// La cle de geste d'une nuitee. C'EST ELLE qui rend le posting idempotent : le night audit,
    /// une relance du night audit et le rattrapage au depart produisent tous la meme cle, et l'index
    /// unique (folio, cle) n'en accepte qu'une.
    /// </summary>
    private static string NightSourceReference(Guid reservationId, DateOnly night)
    {
        return $"night:{reservationId:N}:{night:yyyy-MM-dd}";
    }

    private static string ExtraSourceReference(Guid reservationExtraId, DateOnly night)
    {
        return $"extra:{reservationExtraId:N}:{night:yyyy-MM-dd}";
    }

    // ======================================= Arrivee =======================================

    public async Task<ApplicationResult<ReservationResponse>> CheckInAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Transaction Serializable + claim conditionnel : l'arrivee bascule le statut ET ouvre le
        // folio, de sorte qu'un double clic en course avec lui-meme doit produire exactement un
        // folio. L'index unique (unite, numero de folio) est le dernier filet.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound("Le dossier est introuvable.");
            }

            if (!reservation.Status.IsPreArrival() || reservation.Status == ReservationStatus.Inquiry)
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "Seul un dossier en option, confirme ou garanti peut etre enregistre a l'arrivee.");
            }

            if (!reservation.HasRoom)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    "Aucune chambre n'est affectee a ce dossier : affectez-en une avant l'arrivee.");
            }

            var now = DateTimeOffset.UtcNow;
            var claimed = reservation.Status;

            if (!await TryClaimReservationStatusAsync(reservation.Id, claimed, now, cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);

            // Le jour metier suit UTC quand aucune cloture n'a encore eu lieu, comme toute decision
            // basee sur UtcNow dans ce depot (annee d'emission des factures, dates de cloture).
            var today = DateOnly.FromDateTime(now.UtcDateTime);

            try
            {
                reservation.CheckIn(today, context.UserName, now);
            }
            catch (InvalidOperationException ex)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            // Le folio CLIENT s'ouvre a l'arrivee. Les folios societe ou agence, eux, s'ouvrent a
            // la demande : la plupart des sejours n'en ont pas.
            var folio = await CreateFolioEntityAsync(
                reservation,
                FolioKind.Guest,
                billToCustomerCode: null,
                label: null,
                context,
                now,
                cancellationToken);

            // POSTING DE LA NUIT D'ARRIVEE. Les nuits suivantes sont posees par le night audit,
            // nuit apres nuit - c'est la seule facon d'affecter chaque nuitee a SA journee
            // d'exploitation. La nuit d'arrivee, elle, est deja consommee : la laisser au night
            // audit signifierait qu'un sejour d'une nuit enregistre et solde le meme jour ne
            // facturerait rien du tout. La cle de geste est la MEME que celle du night audit, qui
            // la sautera donc proprement.
            var arrivalRate = reservation.GetNightlyRate(reservation.ArrivalDate);

            if (arrivalRate is { Amount: > 0m })
            {
                folio.AddCharge(new FolioCharge(
                    arrivalRate.Night,
                    $"Nuitee du {arrivalRate.Night:dd/MM/yyyy}",
                    arrivalRate.Amount,
                    ChargeKind.Night,
                    sourceReference: NightSourceReference(reservation.Id, arrivalRate.Night),
                    businessDate: businessDay.Date));
            }

            // ARRIVEE ANTICIPEE. Le supplement ne s'applique que si la politique de l'unite en
            // prevoit un ET si l'heure annoncee est effectivement avant l'heure d'ouverture : on ne
            // facture jamais un supplement sur une heure inconnue.
            var policy = await GetPolicyEntityAsync(reservation.HotelUnitCode, cancellationToken);

            if (!policy.EarlyCheckInIsFree && policy.IsEarlyCheckIn(reservation.EstimatedArrivalTime))
            {
                var charge = policy.ComputeEarlyCheckInCharge(reservation.NightlyRateSnapshot);

                if (charge > 0m)
                {
                    folio.AddCharge(new FolioCharge(
                        reservation.ArrivalDate,
                        $"Arrivee anticipee ({reservation.EstimatedArrivalTime:HH\\:mm})",
                        charge,
                        ChargeKind.Extra,
                        sourceReference: $"eci:{reservation.Id:N}",
                        businessDate: businessDay.Date));

                    AddJournalEntry(
                        reservation,
                        ReservationEventKind.EarlyCheckInApplied,
                        $"Arrivee anticipee facturee {charge:0.00}.",
                        context,
                        now,
                        businessDay.Date);
                }
            }

            reservation.MarkUpdated(context.UserName, now);

            // La chambre est occupee : le housekeeping doit le savoir pour ne pas la reproposer.
            await ApplyRoomConditionAsync(
                reservation.HotelUnitCode,
                reservation.RoomId,
                Domain.Housekeeping.RoomConditionStatus.Dirty,
                context,
                now,
                null,
                null,
                cancellationToken);

            AddJournalEntry(
                reservation,
                ReservationEventKind.StatusChanged,
                $"Arrivee enregistree. Folio {folio.Number} ouvert.",
                context,
                now,
                businessDay.Date,
                claimed.ToString(),
                ReservationStatus.CheckedIn.ToString());

            await WriteAuditAsync(
                "lodging.reservation.checked_in",
                ReservationsEntity,
                reservation.Id,
                context,
                new
                {
                    reservation.Number,
                    reservation.HotelUnitCode,
                    reservation.RoomId,
                    FolioId = folio.Id,
                    FolioNumber = folio.Number,
                    NightCount = reservation.Nights,
                    reservation.NightlyRateSnapshot,
                    FolioTotal = folio.Balance
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(
                Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // Une arrivee concurrente a deja ouvert le folio de ce sejour.
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    // ======================================== Depart ========================================

    /// <summary>
    /// PREPARE le depart : pose les nuitees manquantes et le supplement de depart tardif, puis rend
    /// les folios a jour.
    ///
    /// POURQUOI C'EST UN GESTE SEPARE ET COMMITTE. Le night audit pose chaque nuit a sa journee ;
    /// s'il n'a pas tourne, il manque des nuitees au moment ou le client demande sa note. Si le
    /// rattrapage vivait uniquement dans la transaction du depart, un depart refuse pour solde non
    /// nul annulerait aussi le rattrapage - et la reception verrait un total plus bas que ce que le
    /// client doit reellement. On pose donc d'abord, on encaisse ensuite, on solde enfin.
    ///
    /// Le geste est IDEMPOTENT : les nuitees deja posees portent la meme cle de geste et sont
    /// sautees. L'appeler dix fois ne facture rien de plus.
    /// </summary>
    public async Task<ApplicationResult<IReadOnlyCollection<FolioResponse>>> PrepareCheckOutAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<IReadOnlyCollection<FolioResponse>>.NotFound("Le dossier est introuvable.");
            }

            if (reservation.Status != ReservationStatus.CheckedIn)
            {
                return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Conflict(
                    "Seul un sejour en cours peut etre prepare au depart.");
            }

            var folios = await dbContext.Set<Folio>()
                .Include(folio => folio.Charges)
                .Where(folio => folio.ReservationId == reservation.Id)
                .ToListAsync(cancellationToken);

            if (folios.Count == 0)
            {
                return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Validation(
                    "Ce sejour n'a aucun folio : il ne peut pas etre solde.");
            }

            var now = DateTimeOffset.UtcNow;
            var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);
            var guestFolio = folios.FirstOrDefault(folio => folio.Kind == FolioKind.Guest) ?? folios[0];

            var posted = await PostMissingNightsAsync(reservation, folios, businessDay.Date, guestFolio);

            var policy = await GetPolicyEntityAsync(reservation.HotelUnitCode, cancellationToken);
            var lateFailure = ApplyLateCheckOut(reservation, guestFolio, policy, businessDay.Date, context, now);

            if (lateFailure is not null)
            {
                return MirrorFailure<ReservationResponse, IReadOnlyCollection<FolioResponse>>(
                    lateFailure,
                    "Le depart tardif n'a pas pu etre applique.");
            }

            if (posted > 0)
            {
                foreach (var folio in folios)
                {
                    folio.MarkUpdated(context.UserName, now);
                }

                AddJournalEntry(
                    reservation,
                    ReservationEventKind.FolioCharged,
                    $"{posted} nuitee(s) posee(s) en rattrapage a la preparation du depart.",
                    context,
                    now,
                    businessDay.Date);
            }

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Success(folios.Select(Map).ToArray());
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Conflict(
                ConcurrentReservationMutationRefused);
        }
    }

    /// <summary>
    /// Applique le supplement de depart tardif, si la politique en prevoit un ET si l'heure de
    /// depart annoncee le justifie. Jamais sur une heure inconnue : on ne facture pas un supplement
    /// sur une inconnue. Idempotent par sa cle de geste.
    /// </summary>
    private ApplicationResult<ReservationResponse>? ApplyLateCheckOut(
        Reservation reservation,
        Folio guestFolio,
        LodgingPolicy policy,
        DateOnly businessDate,
        OperationContext context,
        DateTimeOffset now)
    {
        if (policy.LateCheckOutIsFree || !policy.IsLateCheckOut(reservation.EstimatedDepartureTime))
        {
            return null;
        }

        if (policy.LateCheckOutUntilTime is { } limit
            && reservation.EstimatedDepartureTime is { } departureTime
            && departureTime > limit)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"Un depart a {departureTime:HH\\:mm} depasse la limite de depart tardif "
                + $"({limit:HH\\:mm}) : facturez une nuit supplementaire plutot qu'un supplement.");
        }

        var lastNight = reservation.GetNightlyRate(reservation.DepartureDate.AddDays(-1));
        var charge = policy.ComputeLateCheckOutCharge(lastNight?.Amount ?? reservation.NightlyRateSnapshot);
        var source = $"lco:{reservation.Id:N}";

        if (charge <= 0m || guestFolio.HasCharge(source))
        {
            return null;
        }

        guestFolio.AddCharge(new FolioCharge(
            reservation.DepartureDate,
            $"Depart tardif ({reservation.EstimatedDepartureTime:HH\\:mm})",
            charge,
            ChargeKind.Extra,
            sourceReference: source,
            businessDate: businessDate));

        AddJournalEntry(
            reservation,
            ReservationEventKind.LateCheckOutApplied,
            $"Depart tardif facture {charge:0.00}.",
            context,
            now,
            businessDate);

        return null;
    }

    public async Task<ApplicationResult<ReservationResponse>> CheckOutAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // ETAPE 1 - PREPARATION, dans sa propre transaction : les nuitees manquantes et le depart
        // tardif sont poses et COMMITTES. Les garder dans la transaction du depart les annulerait
        // en cas de solde non nul, et la reception verrait une note plus basse que ce qui est du.
        var prepared = await PrepareCheckOutAsync(id, context, cancellationToken);

        if (!prepared.Succeeded)
        {
            return MirrorFailure<IReadOnlyCollection<FolioResponse>, ReservationResponse>(
                prepared,
                "Le depart n'a pas pu etre prepare.");
        }

        // ETAPE 2 - LE DEPART. La regle du solde nul lit les folios : la lecture, le controle et la
        // bascule de statut tiennent dans une seule transaction Serializable, sans quoi une ligne
        // ajoutee entre la lecture du solde et le commit laisserait partir un client non solde.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound("Le dossier est introuvable.");
            }

            if (reservation.Status != ReservationStatus.CheckedIn)
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "Seul un sejour en cours peut etre enregistre au depart.");
            }

            var folios = await dbContext.Set<Folio>()
                .Include(folio => folio.Charges)
                .Where(folio => folio.ReservationId == reservation.Id)
                .ToListAsync(cancellationToken);

            if (folios.Count == 0)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    "Ce sejour n'a aucun folio : il ne peut pas etre solde.");
            }

            var now = DateTimeOffset.UtcNow;

            var unsettled = folios.Where(folio => folio.Balance != 0m).ToArray();

            if (unsettled.Length > 0)
            {
                var detail = string.Join(
                    ", ",
                    unsettled.Select(folio => $"{folio.Number} ({folio.Kind}) : {folio.Balance:0.00}"));

                return ApplicationResult<ReservationResponse>.Validation(
                    $"Depart refuse : {unsettled.Length} folio(s) ne sont pas soldes - {detail}. "
                    + "Enregistrez le reglement en tresorerie, ajoutez la ligne de reglement correspondante, "
                    + "puis recommencez.");
            }

            if (!await TryClaimReservationStatusAsync(
                    reservation.Id,
                    ReservationStatus.CheckedIn,
                    now,
                    cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            try
            {
                reservation.CheckOut(context.UserName, now);
            }
            catch (InvalidOperationException ex)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            foreach (var folio in folios)
            {
                folio.Close(context.UserName, now);
                folio.MarkUpdated(context.UserName, now);
            }

            // La chambre libere : elle part en SALE. C'est l'evenement que la gouvernante attend.
            await MarkRoomDirtyAsync(reservation.HotelUnitCode, reservation.RoomId, context, now, cancellationToken);

            var currentAssignment = await dbContext.Set<StayRoomAssignment>()
                .Where(assignment => assignment.ReservationId == reservation.Id && assignment.ReleasedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            currentAssignment?.Release(now, context.UserName, "Depart.");

            reservation.MarkUpdated(context.UserName, now);

            AddJournalEntry(
                reservation,
                ReservationEventKind.StatusChanged,
                "Depart enregistre.",
                context,
                now,
                DateOnly.FromDateTime(now.UtcDateTime),
                ReservationStatus.CheckedIn.ToString(),
                ReservationStatus.CheckedOut.ToString());

            await WriteAuditAsync(
                "lodging.reservation.checked_out",
                ReservationsEntity,
                reservation.Id,
                context,
                new
                {
                    reservation.Number,
                    reservation.HotelUnitCode,
                    reservation.RoomId,
                    FolioCount = folios.Count,
                    Total = folios.Sum(folio => folio.TotalCharges)
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(
                Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    /// <summary>
    /// Pose les nuitees du sejour qui ne l'ont pas encore ete, sur le folio qui porte deja la nuit
    /// d'arrivee - ou, a defaut, sur le folio indique. Rend le nombre de lignes posees.
    ///
    /// La recherche du folio porteur compte : sur un sejour dont les nuits partent au compte
    /// societe, le rattrapage ne doit pas les basculer sur le folio client.
    /// </summary>
    private static Task<int> PostMissingNightsAsync(
        Reservation reservation,
        IReadOnlyCollection<Folio> folios,
        DateOnly businessDate,
        Folio fallbackFolio)
    {
        var posted = 0;

        foreach (var nightRate in reservation.GetNightlyRates())
        {
            if (nightRate.Amount <= 0m)
            {
                continue;
            }

            var source = NightSourceReference(reservation.Id, nightRate.Night);

            if (folios.Any(folio => folio.HasCharge(source)))
            {
                continue;
            }

            var target = folios.FirstOrDefault(folio =>
                folio.IsOpen && folio.Charges.Any(charge => charge.Kind == ChargeKind.Night))
                ?? fallbackFolio;

            if (!target.IsOpen)
            {
                continue;
            }

            target.AddCharge(new FolioCharge(
                nightRate.Night,
                $"Nuitee du {nightRate.Night:dd/MM/yyyy}",
                nightRate.Amount,
                ChargeKind.Night,
                sourceReference: source,
                businessDate: businessDate));

            posted++;
        }

        return Task.FromResult(posted);
    }

    // ================================ Annulation et no-show ================================

    public async Task<ApplicationResult<ReservationResponse>> CancelReservationAsync(
        Guid id,
        CancelReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutatePreArrivalReservationAsync(
            id,
            context,
            "lodging.reservation.cancelled",
            ReservationEventKind.CancellationPolicyApplied,
            (reservation, now, businessDate) =>
            {
                // LA PENALITE VIENT DE LA POLITIQUE FIGEE DANS LE DOSSIER, jamais de celle en
                // vigueur aujourd'hui : le client a accepte les conditions du jour de sa
                // reservation, et un bareme qui change retroactivement est indefendable.
                var fee = 0m;

                if (reservation.CancellationPolicySnapshotJson is { } snapshot)
                {
                    var daysBefore = reservation.ArrivalDate.DayNumber - businessDate.DayNumber;

                    fee = CancellationPolicy.EvaluateSnapshot(
                        snapshot,
                        daysBefore,
                        reservation.TotalStayAmount,
                        reservation.GetNightlyRates());
                }

                reservation.ApplyCancellationFee(fee);
                reservation.Cancel(request.Reason, context.UserName, now);

                return new MutationOutcome(
                    fee > 0m
                        ? $"Annulation : {request.Reason} Penalite retenue : {fee:0.00}."
                        : $"Annulation sans penalite : {request.Reason}",
                    null,
                    fee.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            },
            cancellationToken);
    }

    public async Task<ApplicationResult<ReservationResponse>> MarkNoShowAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutatePreArrivalReservationAsync(
            id,
            context,
            "lodging.reservation.no_show",
            ReservationEventKind.CancellationPolicyApplied,
            (reservation, now, businessDate) =>
            {
                var fee = 0m;

                if (reservation.CancellationPolicySnapshotJson is { } snapshot)
                {
                    fee = CancellationPolicy.EvaluateNoShowSnapshot(
                        snapshot,
                        reservation.TotalStayAmount,
                        reservation.GetNightlyRates());
                }

                reservation.ApplyCancellationFee(fee);
                reservation.MarkNoShow(DateOnly.FromDateTime(now.UtcDateTime), context.UserName, now);

                return new MutationOutcome(
                    fee > 0m
                        ? $"No-show constate. Penalite retenue : {fee:0.00}."
                        : "No-show constate, sans penalite.",
                    null,
                    fee.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            },
            cancellationToken);
    }

    // ===================================== Affectation =====================================

    public async Task<ApplicationResult<ReservationResponse>> AssignRoomAsync(
        Guid id,
        AssignRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound("Le dossier est introuvable.");
            }

            if (!reservation.Status.IsPreArrival())
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "Un sejour deja commence ne s'affecte plus : utilisez un changement de chambre.");
            }

            var now = DateTimeOffset.UtcNow;
            var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);

            if (!await TryClaimReservationStatusAsync(reservation.Id, reservation.Status, now, cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            var previousRoomNumber = await LoadRoomNumberAsync(reservation.RoomId, cancellationToken);

            if (request.RoomId is not { } targetRoomId)
            {
                if (!reservation.HasRoom)
                {
                    return ApplicationResult<ReservationResponse>.Validation(
                        "Ce dossier n'a deja aucune chambre affectee.");
                }

                reservation.ReleaseRoom();

                await ReleaseCurrentAssignmentAsync(
                    reservation.Id,
                    now,
                    context,
                    request.Reason ?? "Chambre liberee.",
                    cancellationToken);

                AddJournalEntry(
                    reservation,
                    ReservationEventKind.RoomReleased,
                    $"Chambre {previousRoomNumber} liberee : le dossier repasse en attente d'affectation.",
                    context,
                    now,
                    businessDay.Date,
                    previousRoomNumber,
                    null);
            }
            else
            {
                var check = await EnsureRoomIsFreeAsync(
                    reservation,
                    targetRoomId,
                    reservation.ArrivalDate,
                    reservation.DepartureDate,
                    cancellationToken);

                if (check.Failure is not null)
                {
                    return check.Failure;
                }

                var room = check.Room!;

                reservation.AssignRoom(room.Id);

                await ReleaseCurrentAssignmentAsync(
                    reservation.Id,
                    now,
                    context,
                    "Reaffectation.",
                    cancellationToken);

                dbContext.Set<StayRoomAssignment>().Add(new StayRoomAssignment(
                    reservation.Id,
                    room.Id,
                    room.Number,
                    room.RoomTypeCode,
                    now,
                    context.UserName,
                    request.Reason));

                AddJournalEntry(
                    reservation,
                    ReservationEventKind.RoomAssigned,
                    previousRoomNumber is null
                        ? $"Chambre {room.Number} affectee."
                        : $"Chambre {previousRoomNumber} -> {room.Number}.",
                    context,
                    now,
                    businessDay.Date,
                    previousRoomNumber,
                    room.Number);
            }

            reservation.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "lodging.reservation.room_assigned",
                ReservationsEntity,
                reservation.Id,
                context,
                new { reservation.Number, reservation.HotelUnitCode, reservation.RoomId, request.Reason },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(
                Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    // ================================== Changement de chambre ==================================

    public async Task<ApplicationResult<ReservationResponse>> MoveRoomAsync(
        Guid id,
        RoomMoveRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApplicationResult<ReservationResponse>.Validation(
                "Le motif du changement de chambre est obligatoire.");
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound("Le dossier est introuvable.");
            }

            if (reservation.Status.IsClosed())
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "Un sejour termine, annule ou en no-show ne peut plus changer de chambre.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimReservationStatusAsync(reservation.Id, reservation.Status, now, cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);
            var previousRoomId = reservation.RoomId;
            var previousRoomNumber = await LoadRoomNumberAsync(previousRoomId, cancellationToken);

            // LA PERIODE REVALIDEE COMMENCE AUJOURD'HUI, pas a l'arrivee : les nuits deja passees
            // ont eu lieu dans l'ancienne chambre, et exiger que la nouvelle ait ete libre depuis
            // le debut du sejour interdirait tout deplacement en cours de sejour.
            var from = reservation.Status == ReservationStatus.CheckedIn
                ? (businessDay.Date > reservation.ArrivalDate ? businessDay.Date : reservation.ArrivalDate)
                : reservation.ArrivalDate;

            var check = await EnsureRoomIsFreeAsync(
                reservation,
                request.TargetRoomId,
                from,
                reservation.DepartureDate,
                cancellationToken);

            if (check.Failure is not null)
            {
                return check.Failure;
            }

            var room = check.Room!;

            try
            {
                reservation.MoveToRoom(room.Id);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            await ReleaseCurrentAssignmentAsync(reservation.Id, now, context, request.Reason, cancellationToken);

            dbContext.Set<StayRoomAssignment>().Add(new StayRoomAssignment(
                reservation.Id,
                room.Id,
                room.Number,
                room.RoomTypeCode,
                now,
                context.UserName,
                request.Reason));

            // L'ANCIENNE CHAMBRE PART EN SALE. C'est l'evenement que la gouvernante attend : une
            // chambre liberee en cours de journee n'est pas prete, meme si le plan la montre libre.
            if (reservation.Status == ReservationStatus.CheckedIn && previousRoomId is not null)
            {
                await MarkRoomDirtyAsync(reservation.HotelUnitCode, previousRoomId, context, now, cancellationToken);
            }

            reservation.MarkUpdated(context.UserName, now);

            AddJournalEntry(
                reservation,
                ReservationEventKind.RoomMoved,
                $"Changement de chambre {previousRoomNumber} -> {room.Number} : {request.Reason}",
                context,
                now,
                businessDay.Date,
                previousRoomNumber,
                room.Number);

            await WriteAuditAsync(
                "lodging.stay.room_moved",
                ReservationsEntity,
                reservation.Id,
                context,
                new
                {
                    reservation.Number,
                    reservation.HotelUnitCode,
                    PreviousRoom = previousRoomNumber,
                    NewRoom = room.Number,
                    request.Reason
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(Map(reservation, room.Number));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    /// <summary>
    /// Verifie qu'une chambre est reellement prenable pour ce dossier sur cette periode : active,
    /// de l'unite, libre de tout sejour bloquant et de tout blocage.
    /// </summary>
    private async Task<(ApplicationResult<ReservationResponse>? Failure, Room? Room)> EnsureRoomIsFreeAsync(
        Reservation reservation,
        Guid targetRoomId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var room = await dbContext.Set<Room>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == targetRoomId, cancellationToken);

        if (room is null || room.HotelUnitCode != reservation.HotelUnitCode)
        {
            return (
                ApplicationResult<ReservationResponse>.NotFound("La chambre est introuvable dans cette unite."),
                null);
        }

        if (!room.IsActive)
        {
            return (
                ApplicationResult<ReservationResponse>.Validation(
                    $"La chambre {room.Number} est inactive : elle ne fait plus partie du parc."),
                null);
        }

        var overlapping = await dbContext.Set<Reservation>()
            .Where(current => current.RoomId == targetRoomId && current.Id != reservation.Id)
            .Where(BlocksPeriod(from, to))
            .AnyAsync(cancellationToken);

        if (overlapping)
        {
            return (ApplicationResult<ReservationResponse>.Conflict(RoomAlreadyReserved), null);
        }

        var blocked = await dbContext.Set<RoomBlock>()
            .Where(block => block.RoomId == targetRoomId
                && (block.Status == RoomBlockStatus.Planned || block.Status == RoomBlockStatus.Active)
                && block.StartDate < to
                && block.EndDate > from)
            .AnyAsync(cancellationToken);

        if (blocked)
        {
            return (
                ApplicationResult<ReservationResponse>.Conflict(
                    $"La chambre {room.Number} est hors service sur tout ou partie de cette periode."),
                null);
        }

        return (null, room);
    }

    private async Task ReleaseCurrentAssignmentAsync(
        Guid reservationId,
        DateTimeOffset now,
        OperationContext context,
        string reason,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.Set<StayRoomAssignment>()
            .Where(assignment => assignment.ReservationId == reservationId && assignment.ReleasedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var assignment in current)
        {
            assignment.Release(now, context.UserName, reason);
        }
    }
}
