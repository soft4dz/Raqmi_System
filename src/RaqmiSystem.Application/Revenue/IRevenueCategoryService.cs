using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Paramétrage des catégories de recettes. La liste sert aussi bien à l'administration (toutes les
/// catégories, actives ou non) qu'aux écrans de saisie, qui demandent celles qui s'appliquent à
/// une unité ou à un secteur et reçoivent alors le jeu dédié, ou à défaut le jeu générique.
/// </summary>
public interface IRevenueCategoryService
{
    /// <summary>
    /// Sans filtre : toutes les catégories actives, dans l'ordre d'affichage. Avec
    /// <paramref name="hotelUnitCode"/> ou <paramref name="sector"/> : celles qui s'appliquent à
    /// ce secteur (l'unité donne le sien). <paramref name="includeInactive"/> ajoute les
    /// catégories désactivées - pour l'administration, jamais pour une saisie.
    /// </summary>
    Task<ApplicationResult<IReadOnlyCollection<RevenueCategoryResponse>>> ListAsync(
        BusinessSector? sector,
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RevenueCategoryResponse>> GetAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RevenueCategoryResponse>> CreateAsync(
        CreateRevenueCategoryRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RevenueCategoryResponse>> UpdateAsync(
        string code,
        UpdateRevenueCategoryRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Supprime une catégorie qu'aucune recette ni aucun budget ne référence ; sinon la
    /// suppression est refusée (conflit) et la désactivation est la voie à suivre.
    /// </summary>
    Task<ApplicationResult<bool>> DeleteAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken);
}
