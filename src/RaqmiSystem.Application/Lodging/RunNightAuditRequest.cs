namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Lance un night audit. <paramref name="DryRun"/> vrai ne fait que les CONTROLES et n'ecrit rien :
/// c'est la repetition qu'un veilleur passe avant de lancer le vrai, et elle doit rester possible
/// autant de fois qu'il le veut.
///
/// <paramref name="AutoNoShow"/> passe automatiquement en no-show les arrivees non presentees de la
/// journee auditee. Faux par defaut : constater un no-show engage une penalite, et beaucoup
/// d'hotels veulent que ce soit un geste humain.
/// </summary>
public sealed record RunNightAuditRequest(
    string HotelUnitCode,
    DateOnly? BusinessDate = null,
    bool DryRun = false,
    bool AutoNoShow = false,
    bool ForcePostWithFindings = false);
