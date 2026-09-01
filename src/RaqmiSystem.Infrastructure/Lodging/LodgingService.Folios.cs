using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Les comptes du sejour : folios, lignes, transferts, extras attaches et acomptes.
/// </summary>
public sealed partial class LodgingService
{
    public async Task<ApplicationResult<FolioResponse>> GetFolioAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var reservationExists = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .AnyAsync(current => current.Id == reservationId, cancellationToken);

        if (!reservationExists)
        {
            return ApplicationResult<FolioResponse>.NotFound("Le dossier est introuvable.");
        }

        var folios = await dbContext.Set<Folio>()
            .AsNoTracking()
            .Include(current => current.Charges)
            .Where(current => current.ReservationId == reservationId)
            .ToArrayAsync(cancellationToken);

        // Le folio CLIENT est celui que le comptoir attend quand il ne precise rien.
        var folio = folios.FirstOrDefault(current => current.Kind == FolioKind.Guest) ?? folios.FirstOrDefault();

        if (folio is null)
        {
            return ApplicationResult<FolioResponse>.NotFound(
                "Ce dossier n'a pas encore de folio : il s'ouvre a l'arrivee.");
        }

        return ApplicationResult<FolioResponse>.Success(Map(folio));
    }

    public async Task<ApplicationResult<IReadOnlyCollection<FolioResponse>>> ListFoliosAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var reservationExists = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .AnyAsync(current => current.Id == reservationId, cancellationToken);

        if (!reservationExists)
        {
            return ApplicationResult<IReadOnlyCollection<FolioResponse>>.NotFound("Le dossier est introuvable.");
        }

        var folios = await dbContext.Set<Folio>()
            .AsNoTracking()
            .Include(current => current.Charges)
            .Where(current => current.ReservationId == reservationId)
            .OrderBy(current => current.Number)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Success(folios.Select(Map).ToArray());
    }

    public async Task<ApplicationResult<FolioResponse>> CreateFolioAsync(
        Guid reservationId,
        CreateFolioRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Set<Reservation>()
            .SingleOrDefaultAsync(current => current.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<FolioResponse>.NotFound("Le dossier est introuvable.");
        }

        if (reservation.Status.IsClosed())
        {
            return ApplicationResult<FolioResponse>.Conflict(
                "Un sejour termine, annule ou en no-show n'accepte plus de nouveau folio.");
        }

        if (request.BillToCustomerCode is not null)
        {
            var payerCode = NormalizeCodeOrEmpty(request.BillToCustomerCode);

            var payer = await dbContext.Set<Customer>()
                .AsNoTracking()
                .SingleOrDefaultAsync(current => current.Code == payerCode, cancellationToken);

            if (payer is null)
            {
                return ApplicationResult<FolioResponse>.NotFound("Le client a facturer est introuvable.");
            }

            if (!payer.IsActive)
            {
                return ApplicationResult<FolioResponse>.Validation(
                    "Un folio ne peut pas etre adresse a un client inactif.");
            }
        }

        var now = DateTimeOffset.UtcNow;

        Folio folio;

        try
        {
            folio = await CreateFolioEntityAsync(
                reservation,
                request.Kind,
                request.BillToCustomerCode,
                request.Label,
                context,
                now,
                cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<FolioResponse>.Validation(ex.Message);
        }

        await WriteAuditAsync(
            "lodging.folio.created",
            FoliosEntity,
            folio.Id,
            context,
            new { ReservationId = reservation.Id, folio.Number, Kind = folio.Kind.ToString(), folio.BillToCustomerCode },
            cancellationToken);

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<FolioResponse>.Conflict(
                "Un folio portant ce numero existe deja : rechargez le dossier et reessayez.");
        }

        return ApplicationResult<FolioResponse>.Success(Map(folio));
    }

    /// <summary>
    /// Cree l'entite folio et lui attribue son numero. Le numero derive du numero de dossier, ce
    /// qui rend la relation lisible au comptoir : R26000042-1, R26000042-2.
    /// </summary>
    private async Task<Folio> CreateFolioEntityAsync(
        Reservation reservation,
        FolioKind kind,
        string? billToCustomerCode,
        string? label,
        OperationContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Set<Folio>()
            .CountAsync(folio => folio.ReservationId == reservation.Id, cancellationToken);

        var baseNumber = string.IsNullOrWhiteSpace(reservation.Number)
            ? reservation.Id.ToString("N")[..10].ToUpperInvariant()
            : reservation.Number;

        var folio = new Folio(
            reservation.Id,
            reservation.HotelUnitCode,
            $"{baseNumber}-{existing + 1}",
            kind,
            billToCustomerCode,
            label);

        folio.MarkCreated(context.UserName, now);
        dbContext.Set<Folio>().Add(folio);

        return folio;
    }

    public async Task<ApplicationResult<FolioResponse>> AddFolioChargeAsync(
        Guid reservationId,
        AddFolioChargeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Une ligne de folio en course avec un depart doit PERDRE contre le sejour deja cloture
        // plutot que d'atterrir sur un folio dont le solde nul vient d'etre affirme - d'ou la meme
        // transaction Serializable et le meme claim conditionnel que le depart lui-meme.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == reservationId, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<FolioResponse>.NotFound("Le dossier est introuvable.");
            }

            if (reservation.Status != ReservationStatus.CheckedIn)
            {
                return ApplicationResult<FolioResponse>.Conflict(
                    "Les lignes de folio ne peuvent etre ajoutees que pendant le sejour.");
            }

            var folios = await dbContext.Set<Folio>()
                .Include(current => current.Charges)
                .Where(current => current.ReservationId == reservation.Id)
                .ToListAsync(cancellationToken);

            var folio = request.FolioId is { } folioId
                ? folios.FirstOrDefault(current => current.Id == folioId)
                : folios.FirstOrDefault(current => current.Kind == FolioKind.Guest) ?? folios.FirstOrDefault();

            if (folio is null)
            {
                return request.FolioId is null
                    ? ApplicationResult<FolioResponse>.Validation(
                        "Ce dossier n'a aucun folio : rien ne peut y etre impute.")
                    : ApplicationResult<FolioResponse>.NotFound(
                        "Le folio vise n'appartient pas a ce dossier.");
            }

            if (!folio.IsOpen)
            {
                return ApplicationResult<FolioResponse>.Conflict(
                    $"Le folio {folio.Number} est ferme : passez par un avoir du module Facturation.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimReservationStatusAsync(
                    reservation.Id,
                    ReservationStatus.CheckedIn,
                    now,
                    cancellationToken))
            {
                return ApplicationResult<FolioResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);

            FolioCharge charge;

            try
            {
                charge = new FolioCharge(
                    request.ChargeDate,
                    request.Label,
                    request.Amount,
                    request.Kind,
                    request.Reference,
                    request.Quantity,
                    request.VatRate,
                    request.ExtraCode,
                    sourceReference: null,
                    businessDate: businessDay.Date);

                folio.AddCharge(charge);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                return ApplicationResult<FolioResponse>.Validation(ex.Message);
            }

            folio.MarkUpdated(context.UserName, now);

            AddJournalEntry(
                reservation,
                ReservationEventKind.FolioCharged,
                $"Folio {folio.Number} : {charge.Label} {charge.Amount:0.00}.",
                context,
                now,
                businessDay.Date,
                null,
                charge.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

            await WriteAuditAsync(
                "lodging.folio.charge_added",
                FoliosEntity,
                folio.Id,
                context,
                new
                {
                    ReservationId = reservation.Id,
                    folio.Number,
                    charge.ChargeDate,
                    charge.Label,
                    charge.Amount,
                    Kind = charge.Kind.ToString(),
                    charge.Reference,
                    NewBalance = folio.Balance
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<FolioResponse>.Success(Map(folio));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<FolioResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    public async Task<ApplicationResult<IReadOnlyCollection<FolioResponse>>> TransferFolioChargeAsync(
        Guid reservationId,
        TransferFolioChargeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Validation(
                "Le motif du transfert est obligatoire.");
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == reservationId, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<IReadOnlyCollection<FolioResponse>>.NotFound("Le dossier est introuvable.");
            }

            var folios = await dbContext.Set<Folio>()
                .Include(current => current.Charges)
                .Where(current => current.ReservationId == reservation.Id)
                .ToListAsync(cancellationToken);

            var source = folios.FirstOrDefault(folio =>
                folio.Charges.Any(charge => charge.Id == request.ChargeId));

            if (source is null)
            {
                return ApplicationResult<IReadOnlyCollection<FolioResponse>>.NotFound(
                    "La ligne est introuvable sur les folios de ce dossier.");
            }

            var target = folios.FirstOrDefault(folio => folio.Id == request.TargetFolioId);

            if (target is null)
            {
                return ApplicationResult<IReadOnlyCollection<FolioResponse>>.NotFound(
                    "Le folio destinataire n'appartient pas a ce dossier.");
            }

            if (target.Id == source.Id)
            {
                return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Validation(
                    "La ligne est deja sur ce folio.");
            }

            if (!source.IsOpen || !target.IsOpen)
            {
                return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Conflict(
                    "Les deux folios doivent etre ouverts pour transferer une ligne.");
            }

            var charge = source.Charges.Single(current => current.Id == request.ChargeId);

            if (charge.Kind == ChargeKind.Settlement)
            {
                return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Validation(
                    "Un reglement ne se transfere pas : il reflete une piece de tresorerie deja imputee. "
                    + "Enregistrez un remboursement et un nouveau reglement.");
            }

            var now = DateTimeOffset.UtcNow;
            var businessDay = await ResolveBusinessDateAsync(reservation.HotelUnitCode, cancellationToken);

            // LE TRANSFERT NE SUPPRIME RIEN. La ligne d'origine est contre-passee et une nouvelle
            // ligne est posee sur le folio cible : effacer la ligne serait plus simple a lire mais
            // ferait disparaitre la trace de ce qui a ete facture puis deplace, ce qu'un controle
            // cherche precisement a retrouver.
            source.AddCharge(new FolioCharge(
                charge.ChargeDate,
                $"Transfert vers {target.Number} : {charge.Label}",
                -charge.Amount,
                ChargeKind.Adjustment,
                charge.Reference,
                charge.Quantity,
                charge.VatRate,
                charge.ExtraCode,
                sourceReference: $"xfer-out:{charge.Id:N}",
                businessDate: businessDay.Date));

            target.AddCharge(new FolioCharge(
                charge.ChargeDate,
                $"Transfert depuis {source.Number} : {charge.Label}",
                charge.Amount,
                charge.Kind,
                charge.Reference,
                charge.Quantity,
                charge.VatRate,
                charge.ExtraCode,
                sourceReference: $"xfer-in:{charge.Id:N}",
                businessDate: businessDay.Date));

            source.MarkUpdated(context.UserName, now);
            target.MarkUpdated(context.UserName, now);

            AddJournalEntry(
                reservation,
                ReservationEventKind.FolioCharged,
                $"Ligne {charge.Label} ({charge.Amount:0.00}) transferee de {source.Number} vers "
                + $"{target.Number} : {request.Reason}",
                context,
                now,
                businessDay.Date,
                source.Number,
                target.Number);

            await WriteAuditAsync(
                "lodging.folio.charge_transferred",
                FoliosEntity,
                target.Id,
                context,
                new
                {
                    ReservationId = reservation.Id,
                    SourceFolio = source.Number,
                    TargetFolio = target.Number,
                    charge.Label,
                    charge.Amount,
                    request.Reason
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Success(
                folios.Select(Map).ToArray());
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<IReadOnlyCollection<FolioResponse>>.Conflict(
                ConcurrentReservationMutationRefused);
        }
    }

    private static FolioResponse Map(Folio folio)
    {
        var charges = folio.Charges
            .OrderBy(charge => charge.LineNumber)
            .Select(charge => new FolioChargeResponse(
                charge.Id,
                charge.LineNumber,
                charge.ChargeDate,
                charge.Label,
                charge.Amount,
                charge.Kind,
                charge.Reference,
                charge.Quantity,
                charge.VatRate,
                charge.AmountExclVat,
                charge.VatAmount,
                charge.ExtraCode,
                charge.SourceReference,
                charge.BusinessDate))
            .ToArray();

        return new FolioResponse(
            folio.Id,
            folio.ReservationId,
            folio.Balance,
            charges,
            folio.CreatedAt,
            folio.CreatedBy,
            folio.UpdatedAt,
            folio.UpdatedBy,
            folio.Number,
            folio.Kind,
            folio.Status,
            folio.BillToCustomerCode,
            folio.Label,
            folio.TotalCharges,
            folio.TotalSettlements,
            folio.ClosedAt,
            folio.ClosedBy,
            folio.InvoiceId);
    }
}
