namespace RaqmiSystem.Application.Crm;

/// <summary>
/// Port du CRM vers l'historique des séjours d'un client. Le CRM ne connaît ni la réservation,
/// ni la chambre, ni le module qui les tient : il demande ce qu'il affiche dans la vue 360 et
/// si un séjour qu'on lui cite existe, rien de plus.
/// </summary>
/// <remarks>
/// C'est ce port qui rend le CRM livrable sans le paquet Hébergement : un commerce ou un cabinet
/// installe le CRM avec <see cref="NoStayHistoryReader"/> et la vue 360 s'ouvre, colonne
/// séjours à zéro. L'implémentation qui lit les réservations vit dans l'infrastructure, à côté
/// du module qui les possède, et n'est branchée que là où ce module est présent.
/// </remarks>
public interface IStayHistoryReader
{
    /// <summary>
    /// Un séjour portant cet identifiant est-il connu ? Faux quand aucun module ne tient de
    /// séjours : une réponse de satisfaction ne peut pas citer un séjour que personne ne peut
    /// attester.
    /// </summary>
    Task<bool> StayExistsAsync(Guid stayId, CancellationToken cancellationToken);

    /// <summary>
    /// Ce que l'historique des séjours dit d'un client, résumé à la date du jour indiquée. Lu à
    /// la demande, jamais copié : le CRM ne garde pas de double des chiffres de la réception.
    /// </summary>
    Task<GuestStayStatistics> ReadAsync(string customerCode, DateOnly today, CancellationToken cancellationToken);
}
