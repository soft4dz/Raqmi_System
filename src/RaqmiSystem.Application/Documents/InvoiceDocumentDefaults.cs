namespace RaqmiSystem.Application.Documents;

/// <summary>
/// Mentions de bas de page du gabarit de facture tant qu'aucun champ de parametrage ne les porte.
///
/// <c>ApplicationSettings</c> n'a pas de texte libre pour les mentions legales ni pour le mode de
/// reglement, et la facture elle-meme ne porte aucun mode de reglement. Plutot que d'inventer un
/// champ ou d'imprimer une piece muette, on imprime des mentions CONSTANTES, visibles, a relire
/// avec l'expert-comptable (plan comprime : « gabarit relu 2-4 semaines a partir de la fusion
/// d'A5 »). Le jour ou Settings porte ces textes, seul <see cref="InvoiceDocumentModelBuilder"/>
/// change : le gabarit lit deja le modele, pas cette classe.
/// </summary>
public static class InvoiceDocumentDefaults
{
    public const string PaymentTermsMention =
        "Mode de règlement : selon les conditions convenues avec le client "
        + "(virement, chèque ou espèces ; droit de timbre en sus pour tout règlement en espèces).";

    public const string FooterMention =
        "Mentions légales à compléter avec l'expert-comptable (conditions de règlement, pénalités "
        + "de retard, clauses particulières).";
}
