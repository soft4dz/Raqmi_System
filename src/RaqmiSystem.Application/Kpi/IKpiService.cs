using RaqmiSystem.Application.Common;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// La lecture de la bibliotheque KPI. Aucune de ces routes n'ecrit quoi que ce soit et le
/// module ne possede aucune donnee metier : il lit les transactions des autres modules et
/// n'en est jamais la source.
/// </summary>
public interface IKpiService
{
    /// <summary>
    /// Le catalogue complet, indicateurs en attente de source compris. Chaque fiche dit si le
    /// profil connecte peut esperer une valeur : connaitre la bibliotheque n'est pas connaitre
    /// les chiffres.
    /// </summary>
    Task<ApplicationResult<KpiCatalogResponse>> GetCatalogAsync(
        KpiAccessContext access,
        CancellationToken cancellationToken);

    /// <summary>
    /// Le tableau de bord : indicateurs de tete, bibliotheque rangee par famille, unites du
    /// groupe et alertes en cours, sur la periode demandee et compares a la periode equivalente
    /// un an plus tot.
    /// </summary>
    Task<ApplicationResult<KpiDashboardResponse>> GetDashboardAsync(
        KpiQuery query,
        KpiAccessContext access,
        CancellationToken cancellationToken);

    /// <summary>Un indicateur precis, avec ses references et son verdict.</summary>
    Task<ApplicationResult<KpiMeasureResponse>> GetMeasureAsync(
        string code,
        KpiQuery query,
        KpiAccessContext access,
        CancellationToken cancellationToken);

    /// <summary>
    /// L'historique conserve d'un indicateur. Rend les instantanes deja poses ; ne recalcule
    /// jamais le passe.
    /// </summary>
    Task<ApplicationResult<KpiHistoryResponse>> GetHistoryAsync(
        string code,
        string? hotelUnitCode,
        DateOnly from,
        DateOnly to,
        KpiAccessContext access,
        CancellationToken cancellationToken);

    /// <summary>Le comparatif inter-unites et ses classements indicateur par indicateur.</summary>
    Task<ApplicationResult<KpiComparisonResponse>> GetComparisonAsync(
        KpiQuery query,
        KpiAccessContext access,
        CancellationToken cancellationToken);

    /// <summary>
    /// Les alertes en cours : les indicateurs dont la valeur est hors des bornes configurees,
    /// groupe et unites confondus.
    /// </summary>
    Task<ApplicationResult<IReadOnlyCollection<KpiAlertResponse>>> GetAlertsAsync(
        KpiQuery query,
        KpiAccessContext access,
        CancellationToken cancellationToken);
}
