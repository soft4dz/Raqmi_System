using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Les extras attaches a un sejour et les acomptes qui le garantissent.
/// </summary>
public sealed partial class LodgingService
{
    // ======================================== Extras ========================================

    public async Task<ApplicationResult<IReadOnlyCollection<ReservationExtraResponse>>> ListReservationExtrasAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<IReadOnlyCollection<ReservationExtraResponse>>.NotFound(
                "Le dossier est introuvable.");
        }

        var extras = await dbContext.Set<ReservationExtra>()
            .AsNoTracking()
            .Where(extra => extra.ReservationId == reservationId)
            .OrderBy(extra => extra.ExtraCode)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<ReservationExtraResponse>>.Success(
            extras.Select(extra => Map(extra, reservation)).ToArray());
    }

    public async Task<ApplicationResult<ReservationExtraResponse>> AddReservationExtraAsync(
        Guid reservationId,
        AddReservationExtraRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Set<Reservation>()
            .SingleOrDefaultAsync(current => current.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<ReservationExtraResponse>.NotFound("Le dossier est introuvable.");
        }

        if (reservation.Status.IsClosed())
        {
            return ApplicationResult<ReservationExtraResponse>.Conflict(
                "Un sejour termine, annule ou en no-show n'accepte plus d'extra.");
        }

        var extraCode = NormalizeCodeOrEmpty(request.ExtraCode);

        var item = await dbContext.Set<ExtraItem>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                current => current.HotelUnitCode == reservation.HotelUnitCode && current.Code == extraCode,
                cancellationToken);

        if (item is null)
        {
            return ApplicationResult<ReservationExtraResponse>.NotFound(
                $"L'extra '{extraCode}' est introuvable dans cette unite.");
        }

        if (!item.IsActive)
        {
            return ApplicationResult<ReservationExtraResponse>.Validation(
                $"L'extra '{extraCode}' est inactif : il n'est plus vendable.");
        }

        ReservationExtra extra;

        try
        {
            // LE PRIX EST FIGE ICI, meme discipline que le tarif de la nuit : une hausse ulterieure
            // du tarif du petit-dejeuner ne doit pas reecrire ce qui a ete promis a la vente.
            extra = new ReservationExtra(
                reservation.Id,
                item.Code,
                item.Label,
                item.PricingBasis,
                item.UnitPrice,
                item.VatRate,
                item.ChargeKind,
                request.Quantity,
                request.FromDate,
                request.ToDate,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<ReservationExtraResponse>.Validation(ex.Message);
        }

        var now = DateTimeOffset.UtcNow;
        var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);

        dbContext.Set<ReservationExtra>().Add(extra);

        AddJournalEntry(
            reservation,
            ReservationEventKind.Note,
            $"Extra ajoute : {item.Label} (x{request.Quantity:0.##}).",
            context,
            now,
            businessDay.Date,
            null,
            item.Code);

        reservation.MarkUpdated(context.UserName, now);

        await WriteAuditAsync(
            "lodging.reservation.extra_added",
            ReservationsEntity,
            reservation.Id,
            context,
            new { reservation.Number, item.Code, item.Label, request.Quantity, item.UnitPrice },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ReservationExtraResponse>.Success(Map(extra, reservation));
    }

    public async Task<ApplicationResult<bool>> RemoveReservationExtraAsync(
        Guid reservationId,
        Guid extraId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Set<Reservation>()
            .SingleOrDefaultAsync(current => current.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<bool>.NotFound("Le dossier est introuvable.");
        }

        var extra = await dbContext.Set<ReservationExtra>()
            .SingleOrDefaultAsync(
                current => current.Id == extraId && current.ReservationId == reservationId,
                cancellationToken);

        if (extra is null)
        {
            return ApplicationResult<bool>.NotFound("L'extra est introuvable sur ce dossier.");
        }

        if (extra.IsIncludedInRate)
        {
            return ApplicationResult<bool>.Validation(
                "Cet extra est une composante d'un forfait : retirez le forfait plutot que sa composante, "
                + "sinon le prix global ne correspondrait plus a ce qui est livre.");
        }

        var now = DateTimeOffset.UtcNow;
        var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);

        dbContext.Set<ReservationExtra>().Remove(extra);

        AddJournalEntry(
            reservation,
            ReservationEventKind.Note,
            $"Extra retire : {extra.LabelSnapshot}.",
            context,
            now,
            businessDay.Date,
            extra.ExtraCode,
            null);

        reservation.MarkUpdated(context.UserName, now);

        await WriteAuditAsync(
            "lodging.reservation.extra_removed",
            ReservationsEntity,
            reservation.Id,
            context,
            new { reservation.Number, extra.ExtraCode, extra.LabelSnapshot },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<bool>.Success(true);
    }

    private static ReservationExtraResponse Map(ReservationExtra extra, Reservation reservation)
    {
        // Estimation, pas facturation : elle sert a annoncer le total au client avant que les
        // lignes n'existent. Le montant reellement facture reste celui pose sur le folio.
        var nights = Math.Max(1, reservation.Nights);
        var guests = Math.Max(1, reservation.GuestCount);

        var multiplier = extra.PricingBasis switch
        {
            ExtraPricingBasis.PerStay => 1m,
            ExtraPricingBasis.PerNight => nights,
            ExtraPricingBasis.PerPerson => guests,
            ExtraPricingBasis.PerPersonPerNight => (decimal)nights * guests,
            ExtraPricingBasis.PerQuantity => extra.Quantity,
            _ => 1m
        };

        var estimated = extra.IsIncludedInRate
            ? 0m
            : Math.Round(extra.UnitPriceSnapshot * multiplier, 2, MidpointRounding.AwayFromZero);

        return new ReservationExtraResponse(
            extra.Id,
            extra.ReservationId,
            extra.ExtraCode,
            extra.LabelSnapshot,
            extra.PricingBasis,
            extra.UnitPriceSnapshot,
            extra.VatRateSnapshot,
            extra.ChargeKind,
            extra.Quantity,
            extra.FromDate,
            extra.ToDate,
            extra.IsIncludedInRate,
            extra.PackageCode,
            extra.Notes,
            estimated);
    }

    // ======================================= Acomptes =======================================

    public async Task<ApplicationResult<IReadOnlyCollection<DepositResponse>>> ListDepositsAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .AnyAsync(current => current.Id == reservationId, cancellationToken);

        if (!exists)
        {
            return ApplicationResult<IReadOnlyCollection<DepositResponse>>.NotFound("Le dossier est introuvable.");
        }

        var deposits = await dbContext.Set<Deposit>()
            .AsNoTracking()
            .Where(deposit => deposit.ReservationId == reservationId)
            .OrderBy(deposit => deposit.DueDate)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<DepositResponse>>.Success(deposits.Select(Map).ToArray());
    }

    public async Task<ApplicationResult<DepositResponse>> CreateDepositAsync(
        Guid reservationId,
        CreateDepositRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Set<Reservation>()
            .SingleOrDefaultAsync(current => current.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<DepositResponse>.NotFound("Le dossier est introuvable.");
        }

        if (reservation.Status.IsClosed())
        {
            return ApplicationResult<DepositResponse>.Conflict(
                "Un sejour termine, annule ou en no-show n'accepte plus d'acompte.");
        }

        Deposit deposit;

        try
        {
            deposit = new Deposit(reservation.Id, request.Amount, request.DueDate, request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<DepositResponse>.Validation(ex.Message);
        }

        var now = DateTimeOffset.UtcNow;
        var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);

        deposit.MarkCreated(context.UserName, now);
        dbContext.Set<Deposit>().Add(deposit);

        AddJournalEntry(
            reservation,
            ReservationEventKind.DepositRecorded,
            $"Acompte de {deposit.Amount:0.00} demande pour le {deposit.DueDate:dd/MM/yyyy}.",
            context,
            now,
            businessDay.Date);

        await WriteAuditAsync(
            "lodging.deposit.created",
            DepositsEntity,
            deposit.Id,
            context,
            new { reservation.Number, deposit.Amount, deposit.DueDate },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<DepositResponse>.Success(Map(deposit));
    }

    public async Task<ApplicationResult<DepositResponse>> PayDepositAsync(
        Guid depositId,
        PayDepositRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutateDepositAsync(
            depositId,
            context,
            "lodging.deposit.paid",
            (deposit, reservation) =>
            {
                deposit.MarkPaid(request.PaidDate, request.PaymentMethod, request.Reference);

                return $"Acompte de {deposit.Amount:0.00} verse le {request.PaidDate:dd/MM/yyyy} "
                    + $"({request.PaymentMethod}).";
            },
            cancellationToken);
    }

    public async Task<ApplicationResult<DepositResponse>> RefundDepositAsync(
        Guid depositId,
        CloseDepositRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutateDepositAsync(
            depositId,
            context,
            "lodging.deposit.refunded",
            (deposit, reservation) =>
            {
                deposit.Refund(request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow), request.Reason);

                return $"Acompte de {deposit.Amount:0.00} rembourse : {request.Reason}";
            },
            cancellationToken);
    }

    public async Task<ApplicationResult<DepositResponse>> ForfeitDepositAsync(
        Guid depositId,
        CloseDepositRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutateDepositAsync(
            depositId,
            context,
            "lodging.deposit.forfeited",
            (deposit, reservation) =>
            {
                deposit.Forfeit(request.Reason);

                return $"Acompte de {deposit.Amount:0.00} conserve a titre de penalite : {request.Reason}";
            },
            cancellationToken);
    }

    /// <summary>
    /// Impute un acompte verse au folio CLIENT du sejour : le folio recoit une ligne de reglement
    /// negative, exactement comme un encaissement au comptoir.
    ///
    /// TANT QU'IL N'EST PAS IMPUTE, l'acompte n'apparait PAS sur le folio. C'est delibere : de
    /// l'argent recu avant toute prestation ne doit pas afficher un solde negatif sur une chambre
    /// pas encore occupee.
    /// </summary>
    public async Task<ApplicationResult<DepositResponse>> ApplyDepositAsync(
        Guid depositId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var deposit = await dbContext.Set<Deposit>()
            .SingleOrDefaultAsync(current => current.Id == depositId, cancellationToken);

        if (deposit is null)
        {
            return ApplicationResult<DepositResponse>.NotFound("L'acompte est introuvable.");
        }

        var reservation = await dbContext.Set<Reservation>()
            .SingleOrDefaultAsync(current => current.Id == deposit.ReservationId, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<DepositResponse>.NotFound("Le dossier de cet acompte est introuvable.");
        }

        var folios = await dbContext.Set<Folio>()
            .Include(folio => folio.Charges)
            .Where(folio => folio.ReservationId == reservation.Id)
            .ToListAsync(cancellationToken);

        var folio = folios.FirstOrDefault(current => current.Kind == FolioKind.Guest && current.IsOpen)
            ?? folios.FirstOrDefault(current => current.IsOpen);

        if (folio is null)
        {
            return ApplicationResult<DepositResponse>.Validation(
                "Aucun folio ouvert sur ce sejour : l'acompte s'impute a partir de l'arrivee.");
        }

        var now = DateTimeOffset.UtcNow;
        var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);

        try
        {
            deposit.ApplyTo(folio.Id, context.UserName, now);

            folio.AddCharge(new FolioCharge(
                businessDay.Date,
                $"Acompte impute ({deposit.PaymentMethod})",
                -deposit.Amount,
                ChargeKind.Settlement,
                deposit.Reference,
                sourceReference: $"deposit:{deposit.Id:N}",
                businessDate: businessDay.Date));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<DepositResponse>.Validation(ex.Message);
        }

        deposit.MarkUpdated(context.UserName, now);
        folio.MarkUpdated(context.UserName, now);

        AddJournalEntry(
            reservation,
            ReservationEventKind.DepositRecorded,
            $"Acompte de {deposit.Amount:0.00} impute au folio {folio.Number}.",
            context,
            now,
            businessDay.Date);

        await WriteAuditAsync(
            "lodging.deposit.applied",
            DepositsEntity,
            deposit.Id,
            context,
            new
            {
                ReservationNumber = reservation.Number,
                FolioNumber = folio.Number,
                deposit.Amount,
                NewBalance = folio.Balance
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<DepositResponse>.Success(Map(deposit));
    }

    private async Task<ApplicationResult<DepositResponse>> MutateDepositAsync(
        Guid depositId,
        OperationContext context,
        string auditAction,
        Func<Deposit, Reservation, string> mutate,
        CancellationToken cancellationToken)
    {
        var deposit = await dbContext.Set<Deposit>()
            .SingleOrDefaultAsync(current => current.Id == depositId, cancellationToken);

        if (deposit is null)
        {
            return ApplicationResult<DepositResponse>.NotFound("L'acompte est introuvable.");
        }

        var reservation = await dbContext.Set<Reservation>()
            .SingleOrDefaultAsync(current => current.Id == deposit.ReservationId, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<DepositResponse>.NotFound("Le dossier de cet acompte est introuvable.");
        }

        var now = DateTimeOffset.UtcNow;
        var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);

        string summary;

        try
        {
            summary = mutate(deposit, reservation);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<DepositResponse>.Validation(ex.Message);
        }

        deposit.MarkUpdated(context.UserName, now);

        AddJournalEntry(
            reservation,
            ReservationEventKind.DepositRecorded,
            summary,
            context,
            now,
            businessDay.Date,
            null,
            deposit.Status.ToString());

        await WriteAuditAsync(
            auditAction,
            DepositsEntity,
            deposit.Id,
            context,
            new { reservation.Number, deposit.Amount, Status = deposit.Status.ToString(), Summary = summary },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<DepositResponse>.Success(Map(deposit));
    }

    private static DepositResponse Map(Deposit deposit)
    {
        return new DepositResponse(
            deposit.Id,
            deposit.ReservationId,
            deposit.Amount,
            deposit.DueDate,
            deposit.Status,
            deposit.PaidDate,
            deposit.PaymentMethod,
            deposit.Reference,
            deposit.AppliedToFolioId,
            deposit.AppliedAt,
            deposit.AppliedBy,
            deposit.RefundedDate,
            deposit.ClosingReason,
            deposit.Notes,
            deposit.CreatedAt,
            deposit.CreatedBy,
            deposit.UpdatedAt,
            deposit.UpdatedBy);
    }
}
