using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Billing;

/// <summary>
/// Reglement d'une facture emise. Le corps est optionnel sur la route : sans mode de paiement,
/// la facture est seulement marquee payee, comme avant, et la reponse le signale. Avec un mode
/// de paiement, un encaissement REEL est cree et confirme en tresorerie, pour le montant TTC de
/// la facture, dans la meme transaction que le passage au statut Payee.
/// </summary>
/// <param name="Method">Mode de paiement ; null = pas d'encaissement en tresorerie (comportement historique).</param>
/// <param name="BankAccountCode">Caisse ou compte bancaire encaisseur ; exige par la tresorerie pour carte, cheque et virement.</param>
/// <param name="Reference">Piece du paiement (numero de cheque, de virement...). A defaut, le numero de la facture.</param>
/// <param name="ReceiptDate">Date de l'encaissement ; aujourd'hui a defaut.</param>
/// <param name="Notes">Complement libre, ajoute a la mention de la facture reglee.</param>
public sealed record PayInvoiceRequest(
    PaymentMethod? Method = null,
    string? BankAccountCode = null,
    string? Reference = null,
    DateOnly? ReceiptDate = null,
    string? Notes = null);
