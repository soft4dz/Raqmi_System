using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une alerte KPI : un indicateur dont la valeur a franchi une borne configuree.
///
/// CE N'EST PAS UN TICKET. L'alerte est une EVALUATION EN DIRECT : elle existe tant que la
/// valeur est en dehors des bornes, et disparait d'elle-meme quand la situation se redresse. Il
/// n'y a donc ni accuse de reception, ni statut "traitee" - ce qui demanderait une table
/// d'incidents, un cycle de vie et une responsabilite nominative, c'est-a-dire un autre module.
/// <see cref="OwnerRole"/> vient du seuil configure et dit QUI repond de l'indicateur ; le
/// suivi de l'action, lui, appartient au module Validations ou au journal des decisions.
///
/// Dire cela plutot que d'exposer un statut toujours egal a "ouverte" evite de laisser croire a
/// un suivi qui n'existe pas.
/// </summary>
public sealed record KpiAlertResponse(
    string KpiCode,
    string KpiName,
    KpiCategory Category,
    string? HotelUnitCode,
    string? HotelUnitName,
    decimal? Value,
    KpiUnit Unit,
    decimal? BreachedThreshold,
    KpiAlertSeverity Severity,
    string? OwnerRole,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    DateTimeOffset EvaluatedAt,
    string Message);
