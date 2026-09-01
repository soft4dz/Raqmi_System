using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// L'exploitation quotidienne : date metier, night audit, previsionnel, planning graphique,
/// tableaux d'arrivees et de departs, clients presents, balayage des no-shows.
/// </summary>
public interface ILodgingOperationsService
{
    /// <summary>La date metier hoteliere de l'unite, et le retard de cloture eventuel.</summary>
    Task<ApplicationResult<BusinessDateResponse>> GetBusinessDateAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Previsionnel d'occupation jour par jour : parc, OOO, OOS, allotements, vendu, arrivees,
    /// departs, stay-over, restant, taux, revenu chambres, ADR et RevPAR.
    /// </summary>
    Task<ApplicationResult<ForecastResponse>> GetForecastAsync(
        string hotelUnitCode,
        DateOnly from,
        int days,
        CancellationToken cancellationToken);

    /// <summary>Le planning graphique (tape chart) : chambres en lignes, jours en colonnes.</summary>
    Task<ApplicationResult<TapeChartResponse>> GetTapeChartAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ArrivalBoardResponse>> GetArrivalsAsync(
        string hotelUnitCode,
        DateOnly? date,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DepartureBoardResponse>> GetDeparturesAsync(
        string hotelUnitCode,
        DateOnly? date,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyCollection<InHouseGuestResponse>>> GetInHouseAsync(
        string hotelUnitCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Le rapport des non-presentations. En lecture seule quand <paramref name="apply"/> est faux :
    /// la liste des candidats, avec la penalite que chacun declencherait.
    /// </summary>
    Task<ApplicationResult<NoShowSweepResponse>> SweepNoShowsAsync(
        string hotelUnitCode,
        DateOnly? businessDate,
        bool apply,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Passe le night audit. En mode repetition (DryRun), il ne fait que les controles et n'ecrit
    /// rien. En mode reel, il pose les nuitees et les prestations automatiques de la journee - de
    /// facon IDEMPOTENTE : le relancer ne double jamais une ecriture.
    /// </summary>
    Task<ApplicationResult<NightAuditResponse>> RunNightAuditAsync(
        RunNightAuditRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyCollection<NightAuditResponse>>> ListNightAuditRunsAsync(
        string hotelUnitCode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);
}
