using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le garde de readiness appliqué aux files de l'accueil : chaque route du registre existe dans
/// l'API en GET, et sa politique d'autorisation est satisfaite par la clé de lecture que le
/// composeur exige. Une file dont la clé ne suffirait pas afficherait « Indisponible » sur un 403
/// prévisible ; une route renommée casserait ce test avant de casser l'accueil.
/// </summary>
public sealed class HomeRegistryRoutesTests : IClassFixture<RaqmiApiFactory>
{
    private readonly RaqmiApiFactory _factory;

    public HomeRegistryRoutesTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Every_registry_route_is_a_get_endpoint_whose_policy_accepts_the_declared_read_key()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToArray();

        Assert.NotEmpty(endpoints);

        foreach (var route in HomeWorkQueueCatalog.Routes)
        {
            var matches = endpoints
                .Where(endpoint => string.Equals(Normalize(endpoint.RoutePattern.RawText), Normalize(route.Route), StringComparison.OrdinalIgnoreCase))
                .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET", StringComparer.OrdinalIgnoreCase) == true)
                .ToArray();

            var endpoint = Assert.Single(matches);

            var policies = endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.True(policies.Length > 0, $"{route.Route} n'exige aucune politique : une file de l'accueil ne lit jamais une route ouverte.");

            var accepted = PermissionRegistry.AcceptedClaims(route.ReadKey);

            foreach (var policy in policies)
            {
                Assert.True(
                    accepted.Contains(policy!, StringComparer.Ordinal),
                    $"{route.Route} exige la politique '{policy}', que la clé de lecture '{route.ReadKey}' ne satisfait pas ({string.Join(", ", accepted)}).");
            }
        }
    }

    private static string Normalize(string? pattern)
    {
        var trimmed = (pattern ?? string.Empty).Trim().TrimEnd('/');
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
}
