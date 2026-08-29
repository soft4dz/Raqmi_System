using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Settings;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP coverage of the global settings module, including what makes it more than
/// decoration: the identity configured here is what an issued invoice freezes as its EMITTER.
/// </summary>
public sealed class SettingsEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public SettingsEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Writing_the_settings_is_reserved_to_settings_write()
    {
        await CreateUserWithPermissionsAsync(
            "settings.reader",
            "settings.reader@example.com",
            PermissionCatalog.SettingsRead);

        using var readerClient = await _factory.CreateAuthenticatedClientAsync("settings.reader", Password);

        var response = await readerClient.GetAsync("/api/v1/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settings = await response.Content.ReadFromJsonAsync<ApplicationSettingsResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(settings);
        Assert.False(string.IsNullOrWhiteSpace(settings!.CompanyName));

        // settings.write is engaging for the whole installation: reading is not enough.
        var forbidden = await readerClient.PutAsJsonAsync(
            "/api/v1/settings",
            ValidRequest("Hotel El Manar Spa"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Invalid_settings_are_refused_without_touching_the_stored_configuration()
    {
        await CreateUserWithPermissionsAsync(
            "settings.admin",
            "settings.admin@example.com",
            PermissionCatalog.SettingsRead,
            PermissionCatalog.SettingsWrite);

        using var client = await _factory.CreateAuthenticatedClientAsync("settings.admin", Password);

        var accepted = await client.PutAsJsonAsync(
            "/api/v1/settings",
            ValidRequest("Hotel El Djazair", currencyLabel: "DZD"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var badNif = await client.PutAsJsonAsync(
            "/api/v1/settings",
            ValidRequest("Hotel El Djazair") with { CompanyNif = "12345" },
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, badNif.StatusCode);

        var badVatRate = await client.PutAsJsonAsync(
            "/api/v1/settings",
            ValidRequest("Hotel El Djazair") with { DefaultVatRate = 7m },
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, badVatRate.StatusCode);

        var badRetention = await client.PutAsJsonAsync(
            "/api/v1/settings",
            ValidRequest("Hotel El Djazair") with { AuditRetentionDays = 5 },
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, badRetention.StatusCode);

        var reread = await client.GetFromJsonAsync<ApplicationSettingsResponse>(
            "/api/v1/settings",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(reread);
        Assert.True(reread!.IsConfigured);
        Assert.Equal("Hotel El Djazair", reread.CompanyName);
        Assert.Equal(9m, reread.DefaultVatRate);
        Assert.Equal(365, reread.AuditRetentionDays);
    }

    [Fact]
    public async Task Issuing_an_invoice_freezes_the_configured_issuer_identity()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("SETHTL", "Settings Hotel");

        await CreateUserWithPermissionsAsync(
            "settings.billing",
            "settings.billing@example.com",
            PermissionCatalog.SettingsRead,
            PermissionCatalog.SettingsWrite,
            PermissionCatalog.CustomersWrite,
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.InvoicesWrite,
            PermissionCatalog.InvoicesIssue);

        using var client = await _factory.CreateAuthenticatedClientAsync("settings.billing", Password);

        var configured = await client.PutAsJsonAsync(
            "/api/v1/settings",
            new UpdateApplicationSettingsRequest(
                CompanyName: "Hotel El Manar Spa",
                DefaultVatRate: 9m,
                AuditRetentionDays: 365,
                CompanyNif: "098765432112345",
                CompanyRc: "16/00-1234567B99",
                CompanyAi: "16012345678",
                CompanyNis: "543211234509876",
                CompanyAddress: "Boulevard des Martyrs",
                CompanyCity: "Alger"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, configured.StatusCode);

        var customerResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/customers",
            new CreateCustomerRequest("SETCLI", "Client Parametrage", CustomerType.Individual),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/invoices",
            new CreateInvoiceRequest(
                CustomerCode: "SETCLI",
                HotelUnitCode: hotelUnitCode,
                InvoiceDate: new DateOnly(2026, 3, 10),
                Lines: new[] { new InvoiceLineRequest("Hebergement", 1m, 8_000.00m, 9m) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var draft = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draft);

        // A draft identifies no emitter yet: the identity is frozen at issuance, not before.
        Assert.Null(draft!.IssuerName);

        var issueResponse = await client.PostAsync($"/api/v1/billing/invoices/{draft.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, issueResponse.StatusCode);

        var issued = await issueResponse.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(issued);
        Assert.Equal("Hotel El Manar Spa", issued!.IssuerName);
        Assert.Equal("098765432112345", issued.IssuerNif);
        Assert.Equal("16/00-1234567B99", issued.IssuerRc);
        Assert.Equal("16012345678", issued.IssuerAi);
        Assert.Equal("543211234509876", issued.IssuerNis);
        Assert.Equal("Boulevard des Martyrs", issued.IssuerAddress);

        // Renaming the establishment afterwards must not rewrite an invoice already issued.
        var renamed = await client.PutAsJsonAsync(
            "/api/v1/settings",
            ValidRequest("Hotel El Manar Renomme Spa") with
            {
                CompanyNif = "111111111111111",
                CompanyAddress = "Nouvelle adresse"
            },
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var reloaded = await client.GetFromJsonAsync<InvoiceResponse>(
            $"/api/v1/billing/invoices/{draft.Id}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(reloaded);
        Assert.Equal("Hotel El Manar Spa", reloaded!.IssuerName);
        Assert.Equal("098765432112345", reloaded.IssuerNif);
        Assert.Equal("Boulevard des Martyrs", reloaded.IssuerAddress);

        // The change itself is traceable, field by field.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        // SQLite cannot ORDER BY a DateTimeOffset, so the entries are compared in memory.
        var auditDetails = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(entry => entry.Action == "settings.application.updated")
            .Select(entry => entry.DetailsJson)
            .ToArrayAsync();

        Assert.Contains(
            auditDetails,
            details => details is not null
                && details.Contains("CompanyName")
                && details.Contains("Hotel El Manar Renomme Spa"));

        // The singleton stayed a singleton across every update.
        Assert.Equal(1, await dbContext.Settings.CountAsync());
        Assert.Equal(
            ApplicationSettings.SingletonId,
            await dbContext.Settings.AsNoTracking().Select(current => current.Id).SingleAsync());
    }

    private static UpdateApplicationSettingsRequest ValidRequest(
        string companyName,
        string? currencyLabel = null)
    {
        return new UpdateApplicationSettingsRequest(
            CompanyName: companyName,
            DefaultVatRate: 9m,
            AuditRetentionDays: 365,
            CurrencyLabel: currencyLabel);
    }

    /// <summary>
    /// Creates a user attached to a fresh single-purpose role carrying exactly the given
    /// permission keys, mirroring BillingEndpointTests: the per-permission authorization policies
    /// registered in Program.cs are then enforced for real against that user's token.
    /// </summary>
    private async Task CreateUserWithPermissionsAsync(
        string userName,
        string email,
        params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.True(
            permissions.Length == permissionKeys.Length,
            "Permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.settings.{Guid.NewGuid():N}",
            "Settings test role",
            "Role dedicated to global settings endpoint tests.");

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
