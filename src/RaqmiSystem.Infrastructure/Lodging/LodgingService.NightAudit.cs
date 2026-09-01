using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Text;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Le night audit et le balayage des non-presentations.
///
/// L'IDEMPOTENCE EST L'EXIGENCE CENTRALE DE CE FICHIER. Relancer un night audit ne doit JAMAIS
/// doubler une ecriture, parce qu'un veilleur qui doute relance, et parce qu'une coupure au milieu
/// du traitement laisse une partie du travail fait. Elle est obtenue par la cle de geste unique par
/// folio (<c>FolioCharge.SourceReference</c>) : chaque nuitee, chaque prestation automatique porte
/// une cle deterministe, et l'index unique refuse la seconde insertion. Le compteur
/// "deja pose" du rapport n'est donc pas une anomalie, c'est la preuve que le garde-fou a joue.
/// </summary>
public sealed partial class LodgingService
{
    public async Task<ApplicationResult<NoShowSweepResponse>> SweepNoShowsAsync(
        string hotelUnitCode,
        DateOnly? businessDate,
        bool apply,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<NoShowSweepResponse>(normalizedUnitCode, cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var businessDay = await ResolveBusinessDateAsync(normalizedUnitCode, cancellationToken);
        var day = businessDate ?? businessDay.Date;

        // CANDIDATS : les dossiers d'avant-arrivee dont la date d'arrivee est PASSEE. Tant que le
        // jour d'arrivee court, le client peut encore se presenter - constater un no-show ce
        // jour-la fermerait la porte a quelqu'un qui arrive a 23h.
        var candidates = await dbContext.Set<Reservation>()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && reservation.ArrivalDate < day
                && (reservation.Status == ReservationStatus.Option
                    || reservation.Status == ReservationStatus.Confirmed
                    || reservation.Status == ReservationStatus.Guaranteed))
            .OrderBy(reservation => reservation.ArrivalDate)
            .ThenBy(reservation => reservation.Number)
            .ToListAsync(cancellationToken);

        var roomNumbers = await LoadRoomNumbersAsync(
            candidates.Where(reservation => reservation.RoomId is not null)
                .Select(reservation => reservation.RoomId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var customerNames = await LoadCustomerNamesAsync(
            candidates.Select(reservation => reservation.CustomerCode).ToArray(),
            cancellationToken);

        var rows = new List<NoShowCandidateResponse>(candidates.Count);
        var recorded = 0;
        var totalPenalty = 0m;
        var now = DateTimeOffset.UtcNow;

        foreach (var reservation in candidates)
        {
            var penalty = reservation.CancellationPolicySnapshotJson is { } snapshot
                ? CancellationPolicy.EvaluateNoShowSnapshot(
                    snapshot,
                    reservation.TotalStayAmount,
                    reservation.GetNightlyRates())
                : 0m;

            var wasRecorded = false;

            if (apply)
            {
                try
                {
                    reservation.ApplyCancellationFee(penalty);
                    reservation.MarkNoShow(day, context.UserName, now);
                    reservation.MarkUpdated(context.UserName, now);

                    AddJournalEntry(
                        reservation,
                        ReservationEventKind.CancellationPolicyApplied,
                        penalty > 0m
                            ? $"No-show constate par le balayage du {day:dd/MM/yyyy}. Penalite : {penalty:0.00}."
                            : $"No-show constate par le balayage du {day:dd/MM/yyyy}, sans penalite.",
                        context,
                        now,
                        day);

                    // La chambre tenue pour rien redevient vendable : elle repart en SALE parce que
                    // personne ne sait dans quel etat elle a ete laissee depuis la derniere visite.
                    if (reservation.RoomId is not null)
                    {
                        await MarkRoomDirtyAsync(
                            reservation.HotelUnitCode,
                            reservation.RoomId,
                            context,
                            now,
                            cancellationToken);
                    }

                    recorded++;
                    totalPenalty += penalty;
                    wasRecorded = true;
                }
                catch (InvalidOperationException)
                {
                    // Un dossier passe entre-temps a un autre statut n'est plus un candidat : il
                    // sort du balayage sans le faire echouer.
                    wasRecorded = false;
                }
            }

            rows.Add(new NoShowCandidateResponse(
                reservation.Id,
                reservation.Number,
                reservation.CustomerCode,
                customerNames.GetValueOrDefault(reservation.CustomerCode),
                reservation.RoomId is { } roomId ? roomNumbers.GetValueOrDefault(roomId) : null,
                reservation.RoomTypeCode,
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.Guarantee,
                reservation.TotalStayAmount,
                penalty,
                wasRecorded));
        }

        if (apply && recorded > 0)
        {
            await WriteAuditAsync(
                "lodging.no_show.swept",
                ReservationsEntity,
                Guid.Empty,
                context,
                new { HotelUnitCode = normalizedUnitCode, BusinessDate = day, Recorded = recorded, totalPenalty },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }

        return ApplicationResult<NoShowSweepResponse>.Success(new NoShowSweepResponse(
            normalizedUnitCode,
            day,
            apply,
            rows,
            recorded,
            totalPenalty));
    }

    // ====================================== Night audit ======================================

    public async Task<ApplicationResult<NightAuditResponse>> RunNightAuditAsync(
        RunNightAuditRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<NightAuditResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        var unitCode = unitFailure.UnitCode;
        var businessDay = await ResolveBusinessDateAsync(unitCode, cancellationToken);
        var day = request.BusinessDate ?? businessDay.Date;

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var findings = new List<NightAuditFindingResponse>();

            // ------------------------------- Les controles -------------------------------

            var pendingArrivals = await dbContext.Set<Reservation>()
                .AsNoTracking()
                .Where(reservation => reservation.HotelUnitCode == unitCode
                    && reservation.ArrivalDate <= day
                    && (reservation.Status == ReservationStatus.Option
                        || reservation.Status == ReservationStatus.Confirmed
                        || reservation.Status == ReservationStatus.Guaranteed))
                .Select(reservation => new { reservation.Id, reservation.Number, reservation.ArrivalDate })
                .ToArrayAsync(cancellationToken);

            foreach (var arrival in pendingArrivals)
            {
                findings.Add(new NightAuditFindingResponse(
                    "arrivee.non_traitee",
                    $"Dossier {arrival.Number} : arrivee du {arrival.ArrivalDate:dd/MM/yyyy} non enregistree. "
                    + "Enregistrez l'arrivee ou constatez le no-show.",
                    IsBlocking: true,
                    arrival.Id,
                    null));
            }

            var pendingDepartures = await dbContext.Set<Reservation>()
                .AsNoTracking()
                .Where(reservation => reservation.HotelUnitCode == unitCode
                    && reservation.Status == ReservationStatus.CheckedIn
                    && reservation.DepartureDate <= day)
                .Select(reservation => new { reservation.Id, reservation.Number, reservation.DepartureDate })
                .ToArrayAsync(cancellationToken);

            foreach (var departure in pendingDepartures)
            {
                findings.Add(new NightAuditFindingResponse(
                    "depart.non_cloture",
                    $"Dossier {departure.Number} : depart du {departure.DepartureDate:dd/MM/yyyy} non enregistre. "
                    + "La chambre reste occupee et continue d'etre facturee.",
                    IsBlocking: true,
                    departure.Id,
                    null));
            }

            var openFolios = await dbContext.Set<Folio>()
                .AsNoTracking()
                .Where(folio => folio.HotelUnitCode == unitCode && folio.Status == FolioStatus.Open)
                .Join(
                    dbContext.Set<Reservation>().AsNoTracking()
                        .Where(reservation => reservation.Status == ReservationStatus.CheckedOut),
                    folio => folio.ReservationId,
                    reservation => reservation.Id,
                    (folio, reservation) => new { folio.Number, ReservationNumber = reservation.Number, reservation.Id })
                .ToArrayAsync(cancellationToken);

            foreach (var folio in openFolios)
            {
                findings.Add(new NightAuditFindingResponse(
                    "folio.ouvert",
                    $"Folio {folio.Number} encore ouvert sur le dossier solde {folio.ReservationNumber}.",
                    IsBlocking: false,
                    folio.Id,
                    null));
            }

            // Incoherence d'etat : une chambre occupee cette nuit mais declaree hors service. Les
            // deux ne peuvent pas etre vrais en meme temps, et c'est le genre d'ecart qui fait
            // vendre - ou refuser - une chambre a tort.
            var occupiedRoomIds = await dbContext.Set<Reservation>()
                .AsNoTracking()
                .Where(reservation => reservation.HotelUnitCode == unitCode
                    && reservation.RoomId != null
                    && reservation.Status == ReservationStatus.CheckedIn)
                .Select(reservation => reservation.RoomId!.Value)
                .ToArrayAsync(cancellationToken);

            var mismatched = await dbContext.Set<RoomCondition>()
                .AsNoTracking()
                .Where(condition => condition.HotelUnitCode == unitCode
                    && condition.Status == RoomConditionStatus.OutOfOrder
                    && occupiedRoomIds.Contains(condition.RoomId))
                .Select(condition => condition.RoomId)
                .ToArrayAsync(cancellationToken);

            var mismatchedNumbers = await LoadRoomNumbersAsync(mismatched, cancellationToken);

            foreach (var roomId in mismatched)
            {
                findings.Add(new NightAuditFindingResponse(
                    "chambre.incoherente",
                    $"La chambre {mismatchedNumbers.GetValueOrDefault(roomId)} est occupee mais declaree hors "
                    + "service : corrigez l'un ou l'autre.",
                    IsBlocking: false,
                    null,
                    mismatchedNumbers.GetValueOrDefault(roomId)));
            }

            var run = new NightAuditRun(unitCode, day, context.UserName, DateTimeOffset.UtcNow);
            run.RecordChecks(pendingArrivals.Length, pendingDepartures.Length, openFolios.Length, mismatched.Length);

            // ---------------------------- Repetition : on n'ecrit rien ----------------------------

            if (request.DryRun)
            {
                run.SetReport(BuildReport(day, findings, 0, 0, 0m, 0, 0, dryRun: true));

                return ApplicationResult<NightAuditResponse>.Success(Map(run, findings));
            }

            var blocking = findings.Where(finding => finding.IsBlocking).ToArray();

            if (blocking.Length > 0 && !request.ForcePostWithFindings)
            {
                run.Block(context.UserName, DateTimeOffset.UtcNow);
                run.SetReport(BuildReport(day, findings, 0, 0, 0m, 0, 0, dryRun: false));
                run.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
                dbContext.Set<NightAuditRun>().Add(run);

                await WriteAuditAsync(
                    "lodging.night_audit.blocked",
                    NightAuditEntity,
                    run.Id,
                    context,
                    new { unitCode, BusinessDate = day, Blocking = blocking.Length },
                    cancellationToken);

                await SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return ApplicationResult<NightAuditResponse>.Success(Map(run, findings));
            }

            // ------------------------------ Le passage reel ------------------------------

            var now = DateTimeOffset.UtcNow;
            var noShows = 0;

            if (request.AutoNoShow)
            {
                var sweep = await SweepNoShowsAsync(unitCode, day, apply: true, context, cancellationToken);

                if (sweep.Succeeded && sweep.Value is not null)
                {
                    noShows = sweep.Value.RecordedCount;
                }
            }

            var posting = await PostNightAsync(unitCode, day, context, now, cancellationToken);

            run.RecordPostings(
                posting.RoomNights,
                posting.Extras,
                posting.Amount,
                noShows,
                posting.Skipped);

            run.SetReport(BuildReport(
                day,
                findings,
                posting.RoomNights,
                posting.Extras,
                posting.Amount,
                noShows,
                posting.Skipped,
                dryRun: false));

            run.Complete(context.UserName, now);
            run.MarkCreated(context.UserName, now);
            dbContext.Set<NightAuditRun>().Add(run);

            try
            {
                // L'ecriture d'audit persiste elle-meme (AuditLogWriter appelle SaveChangesAsync) :
                // elle doit etre DANS ce bloc, sinon la violation de l'index d'unicite du passage
                // s'echapperait en erreur technique au lieu du conflit attendu.
                await WriteAuditAsync(
                    "lodging.night_audit.completed",
                    NightAuditEntity,
                    run.Id,
                    context,
                    new
                    {
                        unitCode,
                        BusinessDate = day,
                        run.PostedRoomNights,
                        run.PostedExtras,
                        run.PostedAmount,
                        run.SkippedAlreadyPosted,
                        run.NoShowsRecorded
                    },
                    cancellationToken);

                await SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueViolation())
            {
                // ux_night_audit_runs_unit_business_date_completed : un passage EXECUTE existe deja
                // pour cette journee. Rien n'a ete ecrit, et c'est exactement le comportement
                // voulu - relancer un night audit deja passe ne doit rien produire.
                return ApplicationResult<NightAuditResponse>.Conflict(
                    $"Le night audit de la journee du {day:dd/MM/yyyy} a deja ete passe pour cette unite.");
            }

            return ApplicationResult<NightAuditResponse>.Success(Map(run, findings));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<NightAuditResponse>.Conflict(
                "Un autre night audit tourne sur cette unite : rien n'a ete ecrit. Reessayez.");
        }
    }

    /// <summary>Le resultat chiffre d'un passage de posting.</summary>
    private sealed record NightPosting(int RoomNights, int Extras, decimal Amount, int Skipped);

    /// <summary>
    /// Pose les nuitees et les prestations automatiques de la journee auditee, pour tous les sejours
    /// qui la couvrent.
    ///
    /// TOUT PASSE PAR UNE CLE DE GESTE DETERMINISTE. Une nuitee deja posee - par un passage
    /// precedent, par l'arrivee, par le rattrapage d'un depart - est SAUTEE, jamais reposee. C'est
    /// ce qui rend le night audit rejouable sans risque.
    /// </summary>
    private async Task<NightPosting> PostNightAsync(
        string hotelUnitCode,
        DateOnly businessDate,
        OperationContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reservations = await dbContext.Set<Reservation>()
            .Where(reservation => reservation.HotelUnitCode == hotelUnitCode
                && reservation.ArrivalDate <= businessDate
                && reservation.DepartureDate > businessDate
                && (reservation.Status == ReservationStatus.CheckedIn
                    || reservation.Status == ReservationStatus.CheckedOut))
            .ToListAsync(cancellationToken);

        if (reservations.Count == 0)
        {
            return new NightPosting(0, 0, 0m, 0);
        }

        var ids = reservations.Select(reservation => reservation.Id).ToArray();

        var folios = await dbContext.Set<Folio>()
            .Include(folio => folio.Charges)
            .Where(folio => ids.Contains(folio.ReservationId))
            .ToListAsync(cancellationToken);

        var extrasByReservation = (await dbContext.Set<ReservationExtra>()
            .AsNoTracking()
            .Where(extra => ids.Contains(extra.ReservationId))
            .ToArrayAsync(cancellationToken))
            .GroupBy(extra => extra.ReservationId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var roomNights = 0;
        var extraLines = 0;
        var skipped = 0;
        var amount = 0m;

        foreach (var reservation in reservations)
        {
            var stayFolios = folios.Where(folio => folio.ReservationId == reservation.Id).ToArray();

            var target = stayFolios.FirstOrDefault(folio => folio.IsOpen && folio.Kind == FolioKind.Guest)
                ?? stayFolios.FirstOrDefault(folio => folio.IsOpen);

            if (target is null)
            {
                continue;
            }

            var nightRate = reservation.GetNightlyRate(businessDate);

            if (nightRate is { Amount: > 0m })
            {
                var source = NightSourceReference(reservation.Id, businessDate);

                if (stayFolios.Any(folio => folio.HasCharge(source)))
                {
                    skipped++;
                }
                else
                {
                    target.AddCharge(new FolioCharge(
                        businessDate,
                        $"Nuitee du {businessDate:dd/MM/yyyy}",
                        nightRate.Amount,
                        ChargeKind.Night,
                        sourceReference: source,
                        businessDate: businessDate));

                    target.MarkUpdated(context.UserName, now);
                    roomNights++;
                    amount += nightRate.Amount;
                }
            }

            if (!extrasByReservation.TryGetValue(reservation.Id, out var extras))
            {
                continue;
            }

            foreach (var extra in extras)
            {
                // Seuls les extras a poser AUTOMATIQUEMENT chaque nuit passent ici : une pension,
                // une taxe de sejour. Le reste se saisit a la consommation.
                if (extra.PricingBasis is not (ExtraPricingBasis.PerNight or ExtraPricingBasis.PerPersonPerNight))
                {
                    continue;
                }

                if (!extra.CoversNight(businessDate, reservation.ArrivalDate, reservation.DepartureDate))
                {
                    continue;
                }

                if (extra.IsIncludedInRate)
                {
                    continue;
                }

                var source = ExtraSourceReference(extra.Id, businessDate);

                if (stayFolios.Any(folio => folio.HasCharge(source)))
                {
                    skipped++;
                    continue;
                }

                var quantity = extra.PricingBasis == ExtraPricingBasis.PerPersonPerNight
                    ? reservation.GuestCount * extra.Quantity
                    : extra.Quantity;

                var lineAmount = Math.Round(
                    extra.UnitPriceSnapshot * quantity,
                    2,
                    MidpointRounding.AwayFromZero);

                if (lineAmount <= 0m)
                {
                    continue;
                }

                target.AddCharge(new FolioCharge(
                    businessDate,
                    $"{extra.LabelSnapshot} - {businessDate:dd/MM/yyyy}",
                    lineAmount,
                    extra.ChargeKind,
                    quantity: quantity,
                    vatRate: extra.VatRateSnapshot,
                    extraCode: extra.ExtraCode,
                    sourceReference: source,
                    businessDate: businessDate));

                target.MarkUpdated(context.UserName, now);
                extraLines++;
                amount += lineAmount;
            }
        }

        return new NightPosting(roomNights, extraLines, amount, skipped);
    }

    public async Task<ApplicationResult<IReadOnlyCollection<NightAuditResponse>>> ListNightAuditRunsAsync(
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitFailure = await RequireHotelUnitAsync<IReadOnlyCollection<NightAuditResponse>>(
            normalizedUnitCode,
            cancellationToken);

        if (unitFailure is not null)
        {
            return unitFailure;
        }

        var query = dbContext.Set<NightAuditRun>()
            .AsNoTracking()
            .Where(run => run.HotelUnitCode == normalizedUnitCode);

        if (from is { } start)
        {
            query = query.Where(run => run.BusinessDate >= start);
        }

        if (to is { } end)
        {
            query = query.Where(run => run.BusinessDate <= end);
        }

        var runs = (await query
            .OrderByDescending(run => run.BusinessDate)
            .Take(200)
            .ToArrayAsync(cancellationToken))
            .OrderByDescending(run => run.BusinessDate)
            .ThenByDescending(run => run.StartedAt)
            .ToArray();

        return ApplicationResult<IReadOnlyCollection<NightAuditResponse>>.Success(
            runs.Select(run => Map(run, [])).ToArray());
    }

    private static string BuildReport(
        DateOnly businessDate,
        IReadOnlyCollection<NightAuditFindingResponse> findings,
        int roomNights,
        int extras,
        decimal amount,
        int noShows,
        int skipped,
        bool dryRun)
    {
        var report = new StringBuilder();

        report.AppendLine($"Night audit - journee du {businessDate:dd/MM/yyyy}{(dryRun ? " (repetition)" : string.Empty)}");
        report.AppendLine(new string('-', 60));

        if (findings.Count == 0)
        {
            report.AppendLine("Aucun ecart releve.");
        }
        else
        {
            foreach (var finding in findings)
            {
                report.AppendLine($"[{(finding.IsBlocking ? "BLOQUANT" : "signale")}] {finding.Message}");
            }
        }

        report.AppendLine(new string('-', 60));

        if (dryRun)
        {
            report.AppendLine("Aucune ecriture : mode repetition.");
        }
        else
        {
            report.AppendLine($"Nuitees posees      : {roomNights}");
            report.AppendLine($"Prestations posees  : {extras}");
            report.AppendLine($"Montant pose        : {amount:0.00}");
            report.AppendLine($"No-shows constates  : {noShows}");
            report.AppendLine($"Deja posees (sautes): {skipped}");
        }

        var text = report.ToString();

        return text.Length <= NightAuditRun.ReportMaxLength
            ? text
            : text[..NightAuditRun.ReportMaxLength];
    }

    private static NightAuditResponse Map(
        NightAuditRun run,
        IReadOnlyCollection<NightAuditFindingResponse> findings)
    {
        return new NightAuditResponse(
            run.Id,
            run.HotelUnitCode,
            run.BusinessDate,
            run.Status,
            run.PendingArrivals,
            run.PendingDepartures,
            run.OpenFolios,
            run.RoomStateMismatches,
            run.PostedRoomNights,
            run.PostedExtras,
            run.PostedAmount,
            run.NoShowsRecorded,
            run.SkippedAlreadyPosted,
            run.Report,
            findings,
            run.StartedAt,
            run.StartedBy,
            run.CompletedAt,
            run.CompletedBy);
    }
}
