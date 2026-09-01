namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// A qui s'adresse un folio. Un meme sejour en porte souvent plusieurs : la chambre part sur le
/// compte de la societe, les extras restent a la charge du client. Sans cette separation, la
/// reception devrait ventiler a la main au moment du depart, devant le client et sous pression.
/// </summary>
public enum FolioKind
{
    /// <summary>Folio client : ce que le voyageur paie lui-meme.</summary>
    Guest = 0,

    /// <summary>Folio societe : la part prise en charge par l'entreprise.</summary>
    Company = 1,

    /// <summary>Folio agence : la part reglee par l'agence de voyage sur voucher.</summary>
    Agency = 2,

    /// <summary>Folio maitre d'un groupe : la part portee par l'organisateur.</summary>
    Group = 3
}
