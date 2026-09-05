using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Du folio a la facture (A4). La facture est creee PAR LE MODULE FACTURATION - ni numerotation,
/// ni instantane client, ni registre des ventes ne sont reimplementes ici, exactement comme pour
/// un evenement MICE - puis rattachee au folio (<c>Folio.AttachInvoice</c>). Contrat distinct
/// d'ILodgingService parce qu'il consomme IBillingService, que le coeur du PMS ne connait pas.
/// </summary>
public interface IFolioInvoicingService
{
    /// <summary>
    /// Construit la facture du folio a partir de ses lignes de prestation non encore facturees -
    /// nuits, extras, taxes, composantes de forfait, ajustements positifs ; les reglements ne sont
    /// pas des lignes de facture, et une ligne transferee vers un autre folio est neutralisee avec
    /// sa contre-passation. Refuse un folio deja facture ou qui n'a rien a facturer. Le depart,
    /// lui, continue d'exiger un solde nul : facturer ne solde pas.
    /// </summary>
    Task<ApplicationResult<FolioInvoiceResponse>> InvoiceFolioAsync(
        Guid reservationId,
        Guid folioId,
        OperationContext context,
        CancellationToken cancellationToken);
}
