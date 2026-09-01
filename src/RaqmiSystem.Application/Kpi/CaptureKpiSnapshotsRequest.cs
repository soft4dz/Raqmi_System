using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Demande de pose d'instantanes sur une periode.
///
/// <paramref name="Codes"/> vide signifie "tout le catalogue" ; le renseigner permet de
/// n'historiser que les indicateurs critiques, ce qui est le cas d'usage courant - historiser
/// soixante indicateurs a chaque cloture journaliere remplirait la table sans que personne ne
/// relise jamais la plupart d'entre eux.
/// </summary>
public sealed record CaptureKpiSnapshotsRequest(
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<string>? Codes = null,
    KpiDsoMethod DsoMethod = KpiDsoMethod.Simple);
