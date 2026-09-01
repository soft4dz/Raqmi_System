using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une regle active de rattachement d'un prefixe de compte a un groupe de gestion. Le
/// calculateur applique la regle du PREFIXE LE PLUS LONG : declarer "6" en charges non
/// reparties puis "603" en charges departementales est une facon legitime d'ecrire une
/// exception, et c'est l'exception qui doit gagner.
/// </summary>
public sealed record KpiAccountRuleFact(string AccountPrefix, KpiAccountGroup Group);
