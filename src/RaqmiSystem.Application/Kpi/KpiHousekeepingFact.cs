using RaqmiSystem.Domain.Housekeeping;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une tache de nettoyage. Le couple (agent affecte, jour de service) est ce qui donne le
/// denominateur de la productivite d'etage : compter les agents sans compter les jours ferait
/// passer une equipe de trois personnes sur trente jours pour trois personnes tout court.
/// </summary>
public sealed record KpiHousekeepingFact(
    string HotelUnitCode,
    DateOnly ServiceDate,
    string? AssignedTo,
    HousekeepingTaskStatus Status);
