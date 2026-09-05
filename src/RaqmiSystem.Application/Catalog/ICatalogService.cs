using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Catalog;

/// <summary>
/// Le catalogue des articles vendables : ce que la facturation met sur une ligne. Le module
/// Facturation CONSOMME ce contrat pour construire une ligne depuis un code article (prix et TVA
/// repris de l'article) et, a l'emission, pour savoir quelles lignes sortent du stock.
/// </summary>
public interface ICatalogService
{
    Task<IReadOnlyCollection<ArticleResponse>> ListArticlesAsync(
        string? search,
        string? family,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ArticleResponse>> GetArticleAsync(
        string code,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolution groupee de plusieurs codes en une requete, pour la facturation : une facture
    /// de vingt lignes ne fait pas vingt allers-retours. Les codes inconnus sont simplement
    /// absents du resultat ; c'est a l'appelant de dire lesquels lui manquent.
    /// </summary>
    Task<IReadOnlyCollection<ArticleResponse>> FindArticlesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ArticleResponse>> CreateArticleAsync(
        CreateArticleRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ArticleResponse>> UpdateArticleAsync(
        string code,
        UpdateArticleRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ArticleResponse>> SetArticleActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);
}
