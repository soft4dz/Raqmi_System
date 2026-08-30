using System.Net.Http;
using RaqmiSystem.Application.Identity;

namespace RaqmiSystem.Desktop.Api;

// Module Administration & utilisateurs : appels du groupe /api/v1/security.
// Fichier de classe partielle, comme RaqmiApiClient.Customers.cs, pour que ce
// chantier n'entre pas en conflit avec les autres modules qui alimentent le
// meme client API.
//
// Toutes les routes de ce groupe exigent users.read en lecture et users.write en
// ecriture : c'est le serveur qui l'applique, la vue ne fait que griser ce qui
// sera de toute facon refuse.
public sealed partial class RaqmiApiClient
{
    private const string SecurityUsersPath = "/api/v1/security/users";

    /// <summary>
    /// Catalogue des permissions du systeme. Sert a traduire une cle technique
    /// ("users.write") en libelle lisible dans l'ecran d'administration, plutot
    /// que de laisser l'administrateur decoder des cles a la main.
    /// </summary>
    public async Task<IReadOnlyCollection<PermissionSummary>> GetPermissionCatalogAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/security/permissions", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<PermissionSummary>>(response, cancellationToken);
    }

    /// <summary>Roles actifs proposes par le selecteur de roles (libelle + description).</summary>
    public async Task<IReadOnlyCollection<RoleSummary>> GetRolesAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/security/roles", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<RoleSummary>>(response, cancellationToken);
    }

    /// <summary>
    /// Liste les comptes. <paramref name="search"/> filtre cote serveur sur
    /// l'identifiant, le courriel ou le nom affiche ; les comptes desactives ne
    /// remontent que s'ils sont demandes.
    /// </summary>
    public async Task<IReadOnlyCollection<UserAccountResponse>> GetUsersAsync(
        string apiBaseUrl,
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildUsersQuery(search, includeInactive),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<UserAccountResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Detail d'un compte : tout ce que porte la ligne de liste, plus les
    /// permissions EFFECTIVES (l'union de celles accordees par ses roles).
    /// </summary>
    public async Task<UserAccountDetailResponse> GetUserAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{SecurityUsersPath}/{id}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<UserAccountDetailResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Cree un compte. La reponse porte le mot de passe temporaire genere par le
    /// serveur : il n'est renvoye QU'UNE SEULE FOIS, aucune autre route ne permet
    /// de le relire.
    /// </summary>
    public async Task<CreateUserResponse> CreateUserAsync(
        string apiBaseUrl,
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, SecurityUsersPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CreateUserResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Met a jour le courriel et le nom affiche. L'identifiant de connexion n'est
    /// pas modifiable : il figure dans chaque trace d'audit et dans les jetons
    /// deja emis.
    /// </summary>
    public async Task<UserAccountDetailResponse> UpdateUserAsync(
        string apiBaseUrl,
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{SecurityUsersPath}/{id}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<UserAccountDetailResponse>(response, cancellationToken);
    }

    public async Task<UserAccountDetailResponse> SetUserActiveAsync(
        string apiBaseUrl,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{SecurityUsersPath}/{id}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<UserAccountDetailResponse>(response, cancellationToken);
    }

    /// <summary>
    /// REMPLACE l'ensemble des roles du compte : ce qui ne figure pas dans
    /// <paramref name="roleNames"/> est retire. Une collection vide est
    /// legitime et retire tous les roles.
    /// </summary>
    public async Task<UserAccountDetailResponse> SetUserRolesAsync(
        string apiBaseUrl,
        Guid id,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{SecurityUsersPath}/{id}/roles",
            new SetUserRolesRequest(roleNames),
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<UserAccountDetailResponse>(response, cancellationToken);
    }

    /// <summary>Leve immediatement un verrouillage pour echecs de connexion.</summary>
    public async Task<UserAccountDetailResponse> UnlockUserAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{SecurityUsersPath}/{id}/unlock", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<UserAccountDetailResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Reinitialise le mot de passe et renvoie le nouveau mot de passe
    /// temporaire, la aussi UNE SEULE FOIS.
    /// </summary>
    public async Task<ResetPasswordResponse> ResetUserPasswordAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{SecurityUsersPath}/{id}/reset-password", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ResetPasswordResponse>(response, cancellationToken);
    }

    private static string BuildUsersQuery(string? search, bool includeInactive)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        }

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        return query.Count == 0
            ? SecurityUsersPath
            : SecurityUsersPath + "?" + string.Join("&", query);
    }
}
