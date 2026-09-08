using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Texte d'une carte ou d'une rangée verrouillée : le motif, puis la permission qui manque,
/// nommée comme le catalogue la nomme (« Lire la comptabilite »), jamais par sa clé technique.
/// Partagé par la barre latérale et les cartes de l'accueil, pour qu'un même écran verrouillé
/// dise la même chose des deux côtés, à la souris comme au lecteur d'écran.
/// </summary>
public static class AccessDeniedMessage
{
    public const string Text = "Accès non autorisé pour votre profil";

    /// <summary>
    /// « Accès non autorisé pour votre profil — permission requise : {libellé} ». Sans clé, ou
    /// pour une clé que le catalogue ne connaît pas, le motif seul : mieux vaut ne rien nommer
    /// que montrer un identifiant technique.
    /// </summary>
    public static string For(string? permissionKey)
    {
        if (permissionKey is null || PermissionCatalog.Find(permissionKey) is not { } permission)
        {
            return Text;
        }

        return $"{Text} — permission requise : {permission.Name}";
    }
}
