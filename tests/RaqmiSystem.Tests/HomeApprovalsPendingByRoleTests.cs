using System.Net;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le contrat que la carte « Validations en attente de ma décision » consomme :
/// <c>GET /approvals/instances/pending</c> répond 200 aux quatre rôles décideurs et 403 aux
/// autres. C'est pour cela que le composeur exige la clé de décision, jamais approvals.read.
/// </summary>
public sealed class HomeApprovalsPendingByRoleTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public HomeApprovalsPendingByRoleTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(RoleCatalog.SystemAdministrator, HttpStatusCode.OK)]
    [InlineData(RoleCatalog.Direction, HttpStatusCode.OK)]
    [InlineData(RoleCatalog.ExploitationControl, HttpStatusCode.OK)]
    [InlineData(RoleCatalog.UnitManager, HttpStatusCode.OK)]
    [InlineData(RoleCatalog.Cashier, HttpStatusCode.Forbidden)]
    [InlineData(RoleCatalog.HrManager, HttpStatusCode.Forbidden)]
    [InlineData(RoleCatalog.Reader, HttpStatusCode.Forbidden)]
    public async Task The_pending_queue_answers_by_seeded_role(string roleName, HttpStatusCode expected)
    {
        var userName = $"home.{roleName.Replace('.', '-')}.{Guid.NewGuid():N}"[..40];
        await _factory.CreateUserAsync(userName, $"{userName}@example.com", roleName, Password, roleName);

        using var client = await _factory.CreateAuthenticatedClientAsync(userName, Password);

        var response = await client.GetAsync("/api/v1/approvals/instances/pending");

        Assert.Equal(expected, response.StatusCode);

        // La règle est celle du catalogue des rôles : décideur si et seulement si 200.
        Assert.Equal(RoleCatalog.ApprovalDeciderRoles.Contains(roleName), response.StatusCode == HttpStatusCode.OK);
    }
}
