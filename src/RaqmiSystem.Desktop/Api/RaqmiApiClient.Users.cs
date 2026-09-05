using System.Buffers.Text;
using System.Net.Http;
using System.Text.Json;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;

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

    // ------------------------- Perimetre utilisateur <-> unite (lot 2.2) -------------------------

    /// <summary>
    /// Perimetre d'un compte : ses unites affectees, ou IsGlobal quand il n'en a aucune
    /// (aucune affectation = toutes les unites, comme aujourd'hui pour tous les comptes).
    /// </summary>
    public async Task<UserUnitScopeResponse> GetUserUnitsAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{SecurityUsersPath}/{id}/units", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<UserUnitScopeResponse>(response, cancellationToken);
    }

    /// <summary>
    /// REMPLACE le perimetre du compte : ce qui ne figure pas dans
    /// <paramref name="hotelUnitCodes"/> est retire. Une collection vide est legitime et rend
    /// le compte GLOBAL. Un code inconnu est refuse en bloc par le serveur (400).
    /// </summary>
    public async Task<UserUnitScopeResponse> SetUserUnitsAsync(
        string apiBaseUrl,
        Guid id,
        IReadOnlyCollection<string> hotelUnitCodes,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Put,
            $"{SecurityUsersPath}/{id}/units",
            new SetUserUnitsRequest(hotelUnitCodes),
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<UserUnitScopeResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Le perimetre de la SESSION, tel que le jeton d'acces le porte (scope=global, ou un
    /// claim unit par code). Lecture d'AFFICHAGE seulement : le bandeau de session et le
    /// filtre de navigation s'en servent pour dire a l'utilisateur ce qu'il voit, jamais pour
    /// decider de quoi que ce soit - la signature n'est pas verifiee ici, c'est le serveur
    /// qui fait autorite sur chaque route. Null hors session ou si le jeton est illisible.
    ///
    /// Le jeton est decode ici, dans la classe partielle, parce que c'est le seul endroit du
    /// client qui detient le jeton : la fenetre principale ne conserve de la reponse de
    /// connexion que les permissions.
    /// </summary>
    public UnitScopeResponse? TryGetSessionUnitScope()
    {
        if (string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return null;
        }

        var parts = session.AccessToken.Split('.');

        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            var root = payload.RootElement;

            var scope = root.TryGetProperty(SecurityClaimTypes.Scope, out var scopeElement)
                && scopeElement.ValueKind == JsonValueKind.String
                ? scopeElement.GetString()
                : null;

            if (scope == SecurityClaimTypes.GlobalScope)
            {
                return new UnitScopeResponse(true, []);
            }

            var units = new List<string>();

            if (root.TryGetProperty(SecurityClaimTypes.Unit, out var unitElement))
            {
                // Un seul claim est ecrit comme une chaine, plusieurs comme un tableau.
                if (unitElement.ValueKind == JsonValueKind.String)
                {
                    units.Add(unitElement.GetString()!);
                }
                else if (unitElement.ValueKind == JsonValueKind.Array)
                {
                    units.AddRange(unitElement.EnumerateArray()
                        .Where(element => element.ValueKind == JsonValueKind.String)
                        .Select(element => element.GetString()!));
                }
            }

            if (units.Count > 0 || scope == SecurityClaimTypes.NoUnitScope)
            {
                return new UnitScopeResponse(false, units.Order(StringComparer.Ordinal).ToArray());
            }

            // Aucun claim de perimetre : jeton emis avant le lot 2.2, lu global comme le serveur.
            return new UnitScopeResponse(true, []);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
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
