using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Catalog;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Catalog;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Catalog;

/// <summary>
/// Catalogue des articles vendables. L'existence d'un article de STOCK reference par un article
/// suivi est verifiee a travers <see cref="IStockCostProvider"/>, le contrat que le module Stocks
/// publie a cet usage (c'est ainsi que les achats controlent un code commande) : ce module ne lit
/// pas les tables du module Stocks.
/// </summary>
public sealed class CatalogService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IStockCostProvider stockCostProvider) : ICatalogService
{
    private const string ArticlesEntity = "catalog.articles";

    public async Task<IReadOnlyCollection<ArticleResponse>> ListArticlesAsync(
        string? search,
        string? family,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Article>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(article => article.IsActive);
        }

        var normalizedFamily = string.IsNullOrWhiteSpace(family) ? null : family.Trim().ToUpperInvariant();

        if (normalizedFamily is not null)
        {
            query = query.Where(article => article.Family != null && article.Family.ToUpper() == normalizedFamily);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToUpperInvariant();

        if (normalizedSearch is not null)
        {
            query = query.Where(article =>
                article.Code.Contains(normalizedSearch) ||
                article.Designation.ToUpper().Contains(normalizedSearch));
        }

        var articles = await query
            .OrderBy(article => article.Code)
            .ToArrayAsync(cancellationToken);

        return articles.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<ArticleResponse>> GetArticleAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var article = await dbContext.Set<Article>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (article is null)
        {
            return ApplicationResult<ArticleResponse>.NotFound("L'article est introuvable.");
        }

        return ApplicationResult<ArticleResponse>.Success(Map(article));
    }

    public async Task<IReadOnlyCollection<ArticleResponse>> FindArticlesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = codes
            .Select(NormalizeCodeOrEmpty)
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return Array.Empty<ArticleResponse>();
        }

        var articles = await dbContext.Set<Article>()
            .AsNoTracking()
            .Where(article => normalizedCodes.Contains(article.Code))
            .ToArrayAsync(cancellationToken);

        return articles.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<ArticleResponse>> CreateArticleAsync(
        CreateArticleRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ApplicationResult<ArticleResponse>.Validation("Le code article est requis.");
        }

        Article article;

        try
        {
            article = new Article(
                request.Code,
                request.Designation,
                request.UnitOfMeasure,
                request.VatRate,
                request.UnitPriceExclVat,
                request.Family,
                request.TracksStock,
                request.StockItemCode);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<ArticleResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<Article>()
            .AnyAsync(current => current.Code == article.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<ArticleResponse>.Conflict("Un article portant ce code existe deja.");
        }

        var stockFailure = await CheckStockItemAsync(article.StockItemCode, cancellationToken);

        if (stockFailure is not null)
        {
            return stockFailure;
        }

        article.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Article>().Add(article);

        try
        {
            await WriteAuditAsync(
                "catalog.article.created",
                article,
                context,
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Le controle d'existence et l'insertion ne sont pas atomiques : une creation
            // concurrente du meme code perd la course contre l'index unique ux_articles_code.
            return ApplicationResult<ArticleResponse>.Conflict("Un article portant ce code existe deja.");
        }

        return ApplicationResult<ArticleResponse>.Success(Map(article));
    }

    public async Task<ApplicationResult<ArticleResponse>> UpdateArticleAsync(
        string code,
        UpdateArticleRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var article = await dbContext.Set<Article>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (article is null)
        {
            return ApplicationResult<ArticleResponse>.NotFound("L'article est introuvable.");
        }

        try
        {
            article.UpdateDetails(
                request.Designation,
                request.UnitOfMeasure,
                request.VatRate,
                request.UnitPriceExclVat,
                request.Family,
                request.TracksStock,
                request.StockItemCode);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<ArticleResponse>.Validation(ex.Message);
        }

        var stockFailure = await CheckStockItemAsync(article.StockItemCode, cancellationToken);

        if (stockFailure is not null)
        {
            return stockFailure;
        }

        article.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync("catalog.article.updated", article, context, cancellationToken);
        await SaveAsync(cancellationToken);

        return ApplicationResult<ArticleResponse>.Success(Map(article));
    }

    public async Task<ApplicationResult<ArticleResponse>> SetArticleActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var article = await dbContext.Set<Article>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (article is null)
        {
            return ApplicationResult<ArticleResponse>.NotFound("L'article est introuvable.");
        }

        if (isActive)
        {
            article.Activate();
        }
        else
        {
            article.Deactivate();
        }

        article.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "catalog.article.activated" : "catalog.article.deactivated",
            article,
            context,
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<ArticleResponse>.Success(Map(article));
    }

    /// <summary>
    /// Un article suivi doit pointer vers un article de stock qui existe. Existence seulement,
    /// pas activite : desactiver un article de stock ne doit pas casser le catalogue qui le
    /// vend encore - c'est a la sortie de stock, a l'emission, que l'etat du stock compte.
    /// </summary>
    private async Task<ApplicationResult<ArticleResponse>?> CheckStockItemAsync(
        string? stockItemCode,
        CancellationToken cancellationToken)
    {
        if (stockItemCode is null)
        {
            return null;
        }

        var cost = await stockCostProvider.GetAverageCostAsync(stockItemCode, cancellationToken);

        if (cost.Succeeded)
        {
            return null;
        }

        return ApplicationResult<ArticleResponse>.Validation(
            $"L'article de stock '{stockItemCode}' est introuvable : un article suivi doit referencer un article de stock existant.");
    }

    private static ArticleResponse Map(Article article)
    {
        return new ArticleResponse(
            article.Id,
            article.Code,
            article.Designation,
            article.Family,
            article.UnitOfMeasure,
            article.VatRate,
            article.UnitPriceExclVat,
            article.TracksStock,
            article.StockItemCode,
            article.IsActive,
            article.CreatedAt,
            article.CreatedBy,
            article.UpdatedAt,
            article.UpdatedBy);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Vidange explicite apres l'ecriture d'audit. AuditLogWriter.WriteAsync appelle deja
    /// SaveChangesAsync ; cet appel existe pour que la persistance ne depende jamais
    /// silencieusement des details du writer d'audit - meme motif que BillingService.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        Article article,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                ArticlesEntity,
                article.Id.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(new
                {
                    article.Code,
                    article.Designation,
                    article.VatRate,
                    article.UnitPriceExclVat,
                    article.TracksStock,
                    article.StockItemCode,
                    article.IsActive
                })),
            cancellationToken);
    }
}
