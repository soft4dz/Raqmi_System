using RaqmiSystem.Application.Treasury;

namespace RaqmiSystem.Application.Billing;

/// <summary>
/// Resultat d'un reglement : la facture telle qu'elle est apres le geste, l'encaissement de
/// tresorerie cree quand un mode de paiement a ete indique, et - quand il n'y en a pas eu - la
/// mention explicite que la facture a ete marquee payee SANS encaissement, pour qu'un appelant
/// qui n'a pas precise le mode de paiement ne croie pas l'argent enregistre en caisse.
/// </summary>
public sealed record InvoicePaymentResponse(
    InvoiceResponse Invoice,
    CashReceiptResponse? Receipt,
    string? Notice);
