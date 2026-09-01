using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Le parametrage de la bibliotheque : bornes d'alerte, rattachement des comptes aux groupes de
/// gestion, et pose ou cloture des instantanes historises.
///
/// Ce sont les trois seuls actes d'ECRITURE du module, et ils sont derriere la cle
/// <c>kpi.admin</c>. La cloture d'un instantane est irreversible par construction : elle fige
/// un chiffre correspondant a une cloture officielle, et aucun recalcul ne le reecrira ensuite.
/// </summary>
public interface IKpiAdministrationService
{
    Task<ApplicationResult<IReadOnlyCollection<KpiThresholdResponse>>> GetThresholdsAsync(
        string? kpiCode,
        string? hotelUnitCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cree ou remplace la regle de seuils d'un couple (indicateur, unite). Une seule regle par
    /// couple : reparametrer, c'est remplacer, jamais empiler deux regles concurrentes dont
    /// personne ne saurait laquelle s'applique.
    /// </summary>
    Task<ApplicationResult<KpiThresholdResponse>> SaveThresholdAsync(
        SaveKpiThresholdRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<KpiThresholdResponse>> SetThresholdActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyCollection<KpiAccountMappingResponse>>> GetAccountMappingsAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Cree ou remplace le rattachement d'un prefixe de compte a un groupe de gestion. Sans au
    /// moins un rattachement, le GOP, l'EBE et les marges repondent "donnee manquante" : le
    /// moteur ne devine pas un plan comptable.
    /// </summary>
    Task<ApplicationResult<KpiAccountMappingResponse>> SaveAccountMappingAsync(
        SaveKpiAccountMappingRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<KpiAccountMappingResponse>> SetAccountMappingActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calcule la periode et enregistre un instantane par indicateur et par perimetre. Les
    /// instantanes provisoires deja poses sont rafraichis ; les instantanes CLOTURES ne sont
    /// jamais touches, et une divergence entre le recalcul et une valeur figee est remontee dans
    /// la reponse plutot que corrigee en silence.
    /// </summary>
    Task<ApplicationResult<KpiSnapshotBatchResponse>> CaptureSnapshotsAsync(
        CaptureKpiSnapshotsRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fige les instantanes d'une periode. Acte irreversible : c'est ce qui rend un chiffre
    /// communique retrouvable a l'identique des mois plus tard.
    /// </summary>
    Task<ApplicationResult<KpiSnapshotBatchResponse>> CloseSnapshotsAsync(
        CloseKpiSnapshotsRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
