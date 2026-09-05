using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Facturation d'un folio (A4) : le patron de <c>MiceService.InvoiceEventAsync</c>, applique au
/// compte du sejour. La facture est creee PAR LE MODULE FACTURATION (IBillingService) puis
/// rattachee au folio par <see cref="Folio.AttachInvoice"/>.
///
/// POURQUOI UNE CLASSE A PART ET NON UN PARTIEL DE LodgingService. LodgingService recoit ses
/// collaborateurs par constructeur primaire, et la facturation du folio en a un de plus -
/// IBillingService - que le coeur du PMS n'a aucune raison de connaitre. Une classe dediee porte
/// cette dependance sans elargir celle du PMS.
///
/// DU TTC AU HT. Une ligne de folio est un montant TTC (c'est le solde que le client paie) qui
/// porte son taux de TVA ; une ligne de facture est un prix HT et une TVA. Le HT et la TVA sont
/// ceux que le folio a EXTRAITS du TTC, et la TVA est FIGEE sur la ligne de facture (InvoiceLine
/// l'admet a un centime pres du calcul HT x taux) : recalculee depuis le HT arrondi, elle
/// s'ecarterait parfois d'un centime et la facture dirait 3 999,99 pour un diner paye 4 000.
/// </summary>
public sealed class FolioInvoicingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IBillingService billingService) : IFolioInvoicingService
{
    private const string FoliosEntity = "lodging.folios";

    /// <summary>
    /// Cle de geste que TransferFolioChargeAsync pose sur la contre-passation d'une ligne
    /// transferee ("xfer-out:{id de la ligne d'origine}"). C'est elle qui permet de neutraliser la
    /// paire - la ligne d'origine ET sa contre-passation - sur le folio source : la prestation est
    /// facturee sur le folio qui l'a recue, pas deux fois.
    /// </summary>
    private const string TransferOutPrefix = "xfer-out:";

    public async Task<ApplicationResult<FolioInvoiceResponse>> InvoiceFolioAsync(
        Guid reservationId,
        Guid folioId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Le controle "pas encore facture" et le rattachement de la facture tiennent dans une
        // transaction Serializable, avec un claim conditionnel sur invoice_id IS NULL : deux
        // demandes concurrentes liraient toutes deux un folio non facture et creeraient deux
        // factures - deux numeros legaux pour un seul sejour. Le perdant repart en 409 sans rien
        // avoir ecrit (motif de LodgingService).
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .AsNoTracking()
                .SingleOrDefaultAsync(current => current.Id == reservationId, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<FolioInvoiceResponse>.NotFound("Le dossier est introuvable.");
            }

            var folio = await dbContext.Set<Folio>()
                .Include(current => current.Charges)
                .SingleOrDefaultAsync(
                    current => current.Id == folioId && current.ReservationId == reservationId,
                    cancellationToken);

            if (folio is null)
            {
                return ApplicationResult<FolioInvoiceResponse>.NotFound("Le folio est introuvable sur ce dossier.");
            }

            if (folio.InvoiceId is { } existingInvoiceId)
            {
                var number = await dbContext.Set<Domain.Billing.Invoice>()
                    .AsNoTracking()
                    .Where(invoice => invoice.Id == existingInvoiceId)
                    .Select(invoice => invoice.Number)
                    .SingleOrDefaultAsync(cancellationToken);

                return ApplicationResult<FolioInvoiceResponse>.Conflict(
                    $"Ce folio a deja ete facture ({number ?? "facture en brouillon"}).");
            }

            var (lines, failure) = BuildLines(folio);

            if (failure is not null)
            {
                return ApplicationResult<FolioInvoiceResponse>.Validation(failure);
            }

            if (lines.Count == 0)
            {
                return ApplicationResult<FolioInvoiceResponse>.Validation(
                    "Ce folio ne porte aucune prestation a facturer.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimUninvoicedFolioAsync(folio.Id, now, cancellationToken))
            {
                return ApplicationResult<FolioInvoiceResponse>.Conflict(
                    "Ce folio vient d'etre facture par une operation concurrente : rien n'a ete ecrit.");
            }

            // Le payeur est celui du folio (societe, agence) ; a defaut, le client du sejour.
            var invoice = await billingService.CreateInvoiceAsync(
                new CreateInvoiceRequest(
                    folio.BillToCustomerCode ?? reservation.CustomerCode,
                    folio.HotelUnitCode,
                    DateOnly.FromDateTime(now.UtcDateTime),
                    lines),
                context,
                cancellationToken);

            if (!invoice.Succeeded || invoice.Value is null)
            {
                // Le refus du module Facturation est rendu TEL QUEL : client inactif, unite
                // inconnue, taux refuse - il connait ses regles mieux que ce module.
                var message = invoice.Error ?? "La facture n'a pas pu etre creee.";

                return invoice.ErrorType == ApplicationErrorType.NotFound
                    ? ApplicationResult<FolioInvoiceResponse>.NotFound(message)
                    : ApplicationResult<FolioInvoiceResponse>.Validation(message);
            }

            folio.AttachInvoice(invoice.Value.Id);
            folio.MarkUpdated(context.UserName, now);

            await auditLogWriter.WriteAsync(
                new AuditLogEntry(
                    context.UserId,
                    context.UserName,
                    "lodging.folio.invoiced",
                    FoliosEntity,
                    folio.Id.ToString(),
                    context.IpAddress,
                    JsonSerializer.Serialize(new
                    {
                        ReservationId = reservation.Id,
                        folio.Number,
                        Kind = folio.Kind.ToString(),
                        InvoiceId = invoice.Value.Id,
                        LineCount = lines.Count,
                        invoice.Value.TotalInclVat
                    })),
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<FolioInvoiceResponse>.Success(new FolioInvoiceResponse(
                reservation.Id,
                folio.Id,
                folio.Number,
                folio.Kind,
                folio.TotalCharges,
                invoice.Value));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<FolioInvoiceResponse>.Conflict(
                "Ce folio etait facture par une operation concurrente : la facturation a ete annulee et rien n'a ete ecrit.");
        }
    }

    /// <summary>
    /// Forme atomique de "ce folio n'est pas encore facture" : la clause WHERE d'un UPDATE
    /// conditionnel, evaluee par la base a l'instant du claim. La seule colonne ecrite est celle
    /// que le rattachement estampille de toute facon.
    /// </summary>
    private async Task<bool> TryClaimUninvoicedFolioAsync(Guid folioId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<Folio>()
            .Where(current => current.Id == folioId && current.InvoiceId == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    /// <summary>
    /// Les lignes de facture d'un folio : ses prestations, dans l'ordre du compte. Les reglements
    /// n'en font pas partie (ils soldent, ils ne vendent pas) ; une ligne transferee et sa
    /// contre-passation se neutralisent ; un ajustement NEGATIF qui n'est pas une contre-passation
    /// (geste commercial) est refuse, parce qu'une ligne de facture ne porte pas de montant
    /// negatif : la remise sur facture est un chantier a part (avoir, B3), et facturer sans elle
    /// ferait payer au client plus que son solde.
    /// </summary>
    private static (List<InvoiceLineRequest> Lines, string? Failure) BuildLines(Folio folio)
    {
        var charges = folio.Charges.OrderBy(charge => charge.LineNumber).ToArray();
        var neutralized = new HashSet<Guid>();

        foreach (var charge in charges)
        {
            if (charge.Kind != ChargeKind.Adjustment
                || charge.SourceReference is null
                || !charge.SourceReference.StartsWith(TransferOutPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            neutralized.Add(charge.Id);

            if (Guid.TryParseExact(charge.SourceReference[TransferOutPrefix.Length..], "N", out var originalId))
            {
                neutralized.Add(originalId);
            }
        }

        var lines = new List<InvoiceLineRequest>();

        foreach (var charge in charges)
        {
            if (charge.Kind == ChargeKind.Settlement || neutralized.Contains(charge.Id))
            {
                continue;
            }

            if (charge.Amount < 0m)
            {
                return (lines,
                    $"La ligne {charge.LineNumber} ({charge.Label}) est un ajustement negatif : la facture ne sait pas " +
                    "encore porter une remise. Regularisez le folio (ou passez par un avoir) avant de le facturer.");
            }

            lines.Add(ToInvoiceLine(charge));
        }

        return (lines, null);
    }

    /// <summary>
    /// Une ligne de folio (TTC, quantite, taux) devient une ligne de facture (prix unitaire HT,
    /// quantite, taux, TVA figee). Le HT et la TVA sont ceux que le folio calcule
    /// (<see cref="FolioCharge.AmountExclVat"/>, <see cref="FolioCharge.VatAmount"/>) ; la quantite
    /// est conservee quand le prix unitaire HT tombe juste sur deux decimales, sinon la ligne passe
    /// en quantite 1 avec la quantite rappelee dans le libelle - les montants priment sur la
    /// presentation.
    /// </summary>
    private static InvoiceLineRequest ToInvoiceLine(FolioCharge charge)
    {
        var vatRate = charge.VatRate ?? 0m;
        var totalExclVat = charge.AmountExclVat;

        var unitPrice = charge.Quantity == 1m
            ? totalExclVat
            : Math.Round(totalExclVat / charge.Quantity, 2, MidpointRounding.AwayFromZero);

        if (charge.Quantity != 1m && Math.Round(unitPrice * charge.Quantity, 2, MidpointRounding.AwayFromZero) != totalExclVat)
        {
            return new InvoiceLineRequest(
                $"{charge.Label} (x{charge.Quantity:0.###})",
                1m,
                totalExclVat,
                vatRate,
                VatAmount: charge.VatAmount);
        }

        return new InvoiceLineRequest(charge.Label, charge.Quantity, unitPrice, vatRate, VatAmount: charge.VatAmount);
    }
}
