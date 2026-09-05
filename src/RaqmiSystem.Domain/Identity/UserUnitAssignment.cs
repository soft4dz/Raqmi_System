using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Identity;

/// <summary>
/// Affectation d'un utilisateur a une unite hoteliere : la brique du PERIMETRE utilisateur
/// (lot 2.2 de la reorganisation fonctionnelle, decision 4 du dossier).
///
/// Semantique, volontairement asymetrique pour rester compatible avec l'existant :
/// <list type="bullet">
///   <item>AUCUNE affectation = perimetre GLOBAL. Les roles systeme et tous les comptes crees
///         avant ce lot n'ont aucune ligne ici, et ils voient tout - exactement comme avant.
///         Le perimetre ne se restreint jamais par defaut, il se restreint par un acte
///         d'administration explicite et audite ;</item>
///   <item>AU MOINS UNE affectation = perimetre RESTREINT a ces unites. Parmi elles, seules
///         celles en validite (<see cref="IsEffectiveAt"/>) comptent au moment ou le jeton est
///         emis ; un compte dont toutes les affectations sont expirees n'a acces a AUCUNE unite.
///         Une restriction qui expire ne redevient jamais un acces global par accident : c'est
///         l'existence d'une ligne, pas sa validite, qui fait basculer le compte en restreint.</item>
/// </list>
///
/// L'unite est referencee par son code (<see cref="HotelUnit.Code"/>), comme partout ailleurs
/// dans le systeme (recettes, reservations, stocks) : c'est la valeur que les routes recoivent
/// en parametre, et donc celle que le jeton doit porter pour etre compare sans aller en base.
///
/// Un utilisateur porteur de <c>units.write</c> ou <c>admin.unit.*</c> reste soumis a son
/// perimetre pour les donnees d'exploitation ; seule l'administration des unites elle-meme
/// (routes <c>/organization/*</c>) n'est pas filtree.
/// </summary>
public sealed class UserUnitAssignment
{
    private UserUnitAssignment()
    {
    }

    public UserUnitAssignment(
        Guid userId,
        string hotelUnitCode,
        string assignedBy,
        DateTimeOffset assignedAt,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null)
    {
        if (validFrom is not null && validTo is not null && validTo <= validFrom)
        {
            throw new ArgumentException("The validity end must be after its start.", nameof(validTo));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        AssignedBy = RequireValue(assignedBy, nameof(assignedBy));
        AssignedAt = assignedAt;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Code de l'unite (<see cref="HotelUnit.Code"/>), normalise en majuscules.</summary>
    public string HotelUnitCode { get; private set; } = string.Empty;

    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>Qui a pose l'affectation (nom de connexion de l'administrateur, ou "system").</summary>
    public string AssignedBy { get; private set; } = string.Empty;

    /// <summary>Debut de validite ; nul = des l'affectation.</summary>
    public DateTimeOffset? ValidFrom { get; private set; }

    /// <summary>Fin de validite (exclue) ; nul = sans fin.</summary>
    public DateTimeOffset? ValidTo { get; private set; }

    /// <summary>
    /// L'affectation compte-t-elle a cet instant ? C'est la question que pose l'emission d'un
    /// jeton : une affectation future ou expiree n'ouvre rien, mais elle continue d'exister et
    /// donc de tenir le compte en perimetre restreint.
    /// </summary>
    public bool IsEffectiveAt(DateTimeOffset utcNow)
    {
        return (ValidFrom is null || ValidFrom.Value <= utcNow)
            && (ValidTo is null || ValidTo.Value > utcNow);
    }

    private static string RequireValue(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        return value.Trim();
    }
}
