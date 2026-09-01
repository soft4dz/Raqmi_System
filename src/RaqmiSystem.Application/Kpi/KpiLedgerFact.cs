namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une ligne d'ecriture comptabilisee. Elle ne porte pas d'unite hoteliere - la comptabilite de
/// Raqmi System n'est pas analytique - d'ou la maille exclusivement groupe des indicateurs de
/// resultat.
///
/// Seules les lignes d'ecritures au statut Comptabilisee sont chargees : un brouillon peut
/// encore etre desequilibre et une ecriture abandonnee n'est jamais entree dans les livres.
/// </summary>
public sealed record KpiLedgerFact(string AccountCode, DateOnly EntryDate, decimal Debit, decimal Credit);
