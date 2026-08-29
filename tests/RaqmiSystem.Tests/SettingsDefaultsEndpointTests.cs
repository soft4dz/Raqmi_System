using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Settings;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// The unconfigured installation. Kept in its own test class so it gets its own dedicated
/// in-memory database (one RaqmiApiFactory per class): the singleton row must genuinely never
/// have been written for these assertions to mean anything.
/// </summary>
public sealed class SettingsDefaultsEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public SettingsDefaultsEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unconfigured_settings_are_served_as_defaults_to_every_reader_role()
    {
        // The seeded read-only role must carry settings.read: the establishment identity and the
        // exploitation defaults are what every screen displays and pre-fills from.
        await _factory.CreateUserAsync(
            "settings.default.reader",
            "settings.default.reader@example.com",
            "Settings Default Reader",
            Password,
            RoleCatalog.Reader);

        using var client = await _factory.CreateAuthenticatedClientAsync("settings.default.reader", Password);

        var response = await client.GetAsync("/api/v1/settings");

        // Never a 404, even though nothing has ever been written.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settings = await response.Content.ReadFromJsonAsync<ApplicationSettingsResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(settings);
        Assert.False(settings!.IsConfigured);
        Assert.Equal(ApplicationSettings.UnconfiguredCompanyName, settings.CompanyName);
        Assert.Equal("DZD", settings.CurrencyLabel);
        Assert.Equal(19m, settings.DefaultVatRate);
        Assert.Equal(365, settings.AuditRetentionDays);

        // A read materializes nothing: GET stays side-effect free.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        Assert.Equal(0, await dbContext.Settings.CountAsync());
    }
}
