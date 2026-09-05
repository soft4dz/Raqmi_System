using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Catalog;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture HTTP du catalogue d'articles : le cycle creer / lire / modifier / desactiver, le
/// lien vers un article de stock existant, et les cles de la facturation reutilisees telles
/// quelles (invoices.read en lecture, invoices.write en ecriture - aucune cle nouvelle).
/// </summary>
public sealed class CatalogEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public CatalogEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Un_article_se_cree_se_lit_se_modifie_et_se_desactive_avec_les_cles_de_la_facturation()
    {
        await CreateUserAsync("catalog.writer", "catalog.writer@example.com", PermissionCatalog.InvoicesRead, PermissionCatalog.InvoicesWrite);
        await CreateUserAsync("catalog.reader", "catalog.reader@example.com", PermissionCatalog.InvoicesRead);

        using var writer = await _factory.CreateAuthenticatedClientAsync("catalog.writer", Password);
        using var reader = await _factory.CreateAuthenticatedClientAsync("catalog.reader", Password);

        var created = await writer.PostAsJsonAsync(
            "/api/v1/catalog/articles",
            new CreateArticleRequest("nuit-dbl", "Nuitee chambre double", "nuit", 9m, 12_500.00m, "Hebergement"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var article = await created.Content.ReadFromJsonAsync<ArticleResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(article);
        Assert.Equal("NUIT-DBL", article!.Code);
        Assert.Equal(9m, article.VatRate);
        Assert.False(article.TracksStock);

        // Un second article du meme code est un conflit, pas un ecrasement.
        var duplicate = await writer.PostAsJsonAsync(
            "/api/v1/catalog/articles",
            new CreateArticleRequest("NUIT-DBL", "Autre", "nuit", 9m, 1m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // Le lecteur consulte...
        var read = await reader.GetAsync("/api/v1/catalog/articles/nuit-dbl");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var listed = await reader.GetFromJsonAsync<IReadOnlyCollection<ArticleResponse>>(
            "/api/v1/catalog/articles?family=hebergement",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(listed);
        Assert.Contains(listed!, item => item.Code == "NUIT-DBL");

        // ...mais n'ecrit pas : invoices.read ne vaut pas invoices.write.
        var forbidden = await reader.PutAsJsonAsync(
            "/api/v1/catalog/articles/NUIT-DBL",
            new UpdateArticleRequest("Nuitee", "nuit", 9m, 13_000m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var updated = await writer.PutAsJsonAsync(
            "/api/v1/catalog/articles/NUIT-DBL",
            new UpdateArticleRequest("Nuitee chambre double vue mer", "nuit", 9m, 13_000m, "Hebergement"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var deactivated = await writer.PostAsync("/api/v1/catalog/articles/NUIT-DBL/deactivate", content: null);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        var activeOnly = await reader.GetFromJsonAsync<IReadOnlyCollection<ArticleResponse>>(
            "/api/v1/catalog/articles",
            RaqmiApiFactory.JsonOptions);

        Assert.DoesNotContain(activeOnly!, item => item.Code == "NUIT-DBL");

        var withInactive = await reader.GetFromJsonAsync<IReadOnlyCollection<ArticleResponse>>(
            "/api/v1/catalog/articles?includeInactive=true",
            RaqmiApiFactory.JsonOptions);

        Assert.Contains(withInactive!, item => item.Code == "NUIT-DBL" && !item.IsActive && item.UnitPriceExclVat == 13_000m);
    }

    [Fact]
    public async Task Un_article_suivi_en_stock_doit_referencer_un_article_de_stock_existant()
    {
        await CreateUserAsync("catalog.stock", "catalog.stock@example.com", PermissionCatalog.InvoicesRead, PermissionCatalog.InvoicesWrite);
        await CreateStockItemAsync("BOI-COCA", "Coca-Cola 33cl");

        using var client = await _factory.CreateAuthenticatedClientAsync("catalog.stock", Password);

        // Suivi sans article de stock : refuse par le domaine.
        var missingLink = await client.PostAsJsonAsync(
            "/api/v1/catalog/articles",
            new CreateArticleRequest("COCA", "Coca-Cola 33cl", "piece", 19m, 250m, "Boissons", TracksStock: true),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, missingLink.StatusCode);

        // Suivi vers un article de stock inconnu : refuse par le service, qui interroge le module Stocks.
        var unknownStock = await client.PostAsJsonAsync(
            "/api/v1/catalog/articles",
            new CreateArticleRequest("COCA", "Coca-Cola 33cl", "piece", 19m, 250m, "Boissons", TracksStock: true, StockItemCode: "PAS-UN-ARTICLE"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, unknownStock.StatusCode);
        Assert.Contains("PAS-UN-ARTICLE", await unknownStock.Content.ReadAsStringAsync());

        var created = await client.PostAsJsonAsync(
            "/api/v1/catalog/articles",
            new CreateArticleRequest("COCA", "Coca-Cola 33cl", "piece", 19m, 250m, "Boissons", TracksStock: true, StockItemCode: "boi-coca"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var article = await created.Content.ReadFromJsonAsync<ArticleResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(article);
        Assert.True(article!.TracksStock);
        Assert.Equal("BOI-COCA", article.StockItemCode);
    }

    private async Task CreateStockItemAsync(string code, string designation)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        dbContext.Set<StockItem>().Add(new StockItem(code, designation, "piece", StockItemCategory.Boisson));
        await dbContext.SaveChangesAsync();
    }

    private async Task CreateUserAsync(string userName, string email, params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.Equal(permissionKeys.Length, permissions.Length);

        var role = new Role($"test.catalog.{Guid.NewGuid():N}", "Catalog test role", "Role dedie aux tests du catalogue.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, userName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
