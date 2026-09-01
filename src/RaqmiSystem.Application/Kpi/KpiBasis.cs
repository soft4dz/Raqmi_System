namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Ce que compte reellement cette reponse, dit par le serveur lui-meme. Meme dispositif
/// d'honnetete que la base de la balance agee ou celle du tableau de bord groupe : un ecran ne
/// doit jamais avoir a redire de son cote ce qu'un chiffre recouvre, et un utilisateur surpris
/// par une valeur doit trouver l'explication a cote d'elle, pas dans une documentation.
/// </summary>
public sealed record KpiBasis(
    string Revenue,
    string Occupancy,
    string Receivables,
    string Payroll,
    string CostOfSales,
    string OperatingResult,
    string Budget,
    string Consolidation,
    string DataQuality);
