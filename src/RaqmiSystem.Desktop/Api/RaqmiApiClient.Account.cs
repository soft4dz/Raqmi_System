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
    /// compte (<see cref="ChangePasswordResponse.RevokedSessionCount"/>), y compris
    /// le jeton de rafraichissement que ce client detient. La session en cours
    /// n'est pas interrompue sur-le-champ - le jeton d'acces reste valide jusqu'a
    /// son expiration - mais son renouvellement sera refuse : au plus tard a cette
    /// echeance, l'operateur verra « Votre session a expiré » et se reconnectera
    /// avec le nouveau mot de passe. C'est voulu : un mot de passe change doit
    /// finir par fermer les sessions ouvertes ailleurs, et celle-ci n'a pas de
    /// raison d'etre traitee autrement.
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
