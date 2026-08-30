using System.Net.Http;
using RaqmiSystem.Application.Identity;

namespace RaqmiSystem.Desktop.Api;

// Module "Mon compte" (/api/v1/account/...) : ce qu'un utilisateur connecte fait a
// SON PROPRE compte, par opposition a l'administration des comptes d'autrui, qui
// vit dans RaqmiApiClient.Users.cs.
//
// Fichier de classe partielle : SendAsync, ReadResponseAsync et EnsureAuthenticated
// sont definis dans RaqmiApiClient.cs.
public sealed partial class RaqmiApiClient
{
    /// <summary>
    /// Change le mot de passe du compte connecte.
    ///
    /// La requete ne porte AUCUN identifiant de compte, et cette absence est
    /// voulue : le serveur agit sur le compte que le jeton authentifie. Le mot de
    /// passe actuel est exige malgre la session ouverte, pour qu'un poste laisse
    /// sans surveillance ne suffise pas a en verrouiller le proprietaire.
    ///
    /// Effet de bord a connaitre : le serveur revoque toutes les sessions du
    /// compte (<see cref="ChangePasswordResponse.RevokedSessionCount"/>). Le jeton
    /// d'acces detenu ici reste valide jusqu'a son expiration - il n'est pas un
    /// jeton de rafraichissement - donc la session en cours n'est pas interrompue.
    /// </summary>
    public async Task<ChangePasswordResponse> ChangePasswordAsync(
        string apiBaseUrl,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            "/api/v1/account/change-password",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ChangePasswordResponse>(response, cancellationToken);
    }
}
