namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Un montant par catégorie tel que renvoyé au client. Le libellé accompagne le code pour que les
/// écrans n'aient aucune table de correspondance à maintenir : ils affichent ce que le serveur
/// leur donne, dans l'ordre où il le donne.
/// </summary>
public sealed record DailyRevenueLineResponse(string CategoryCode, string CategoryLabel, decimal Amount);
