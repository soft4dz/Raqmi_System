namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Nature de la garantie d'une reservation : ce qui permet a l'hotel de facturer si le client ne
/// vient pas. Sans garantie, un no-show ne se facture pas - la penalite d'annulation n'a rien sur
/// quoi s'appuyer - et c'est la raison pour laquelle cette information ne peut pas rester une note
/// libre.
/// </summary>
public enum GuaranteeKind
{
    /// <summary>Aucune garantie : la reservation tombe d'elle-meme a l'heure limite d'annulation.</summary>
    None = 0,

    /// <summary>Carte bancaire donnee en garantie. La reference porte l'empreinte ou le jeton.</summary>
    CreditCard = 1,

    /// <summary>Acompte verse. Le detail vit dans les depots (<see cref="Deposit"/>).</summary>
    Deposit = 2,

    /// <summary>Prise en charge par l'entreprise : la societe reglera tout ou partie du sejour.</summary>
    CompanyBillingArrangement = 3,

    /// <summary>Voucher d'agence de voyage. La reference porte le numero du voucher.</summary>
    AgencyVoucher = 4,

    /// <summary>Garantie societe donnee par contrat ou convention, sans prise en charge du folio.</summary>
    CorporateGuarantee = 5
}
