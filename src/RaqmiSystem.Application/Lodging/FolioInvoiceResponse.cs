using RaqmiSystem.Application.Billing;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Resultat de la facturation d'un folio : le folio identifie, le total de ses prestations tel
/// que le comptoir le lit (TTC), et la facture - en brouillon - que le module Facturation vient
/// de creer pour lui.
/// </summary>
public sealed record FolioInvoiceResponse(
    Guid ReservationId,
    Guid FolioId,
    string FolioNumber,
    FolioKind FolioKind,
    decimal FolioTotalCharges,
    InvoiceResponse Invoice);
