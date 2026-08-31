using System.Reflection;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Identite de ce poste, telle qu'elle est declaree au serveur.
///
/// Ce n'est PAS une authentification et cela ne doit jamais etre lu comme tel : la cle est un Guid
/// tire au hasard, stocke en clair dans le fichier de reglages de l'utilisateur, et le serveur
/// l'accepte telle quelle. Elle repond a "quelle installation parle", jamais a "qui a fait cela".
/// La reponse a cette derniere question reste le jeton d'authentification, et lui seul.
///
/// PORTEE EXACTE : la cle vit dans %APPDATA%, donc dans le PROFIL WINDOWS et non sur la machine.
/// Deux sessions Windows sur un meme PC produisent deux postes distincts, et un profil itinerant
/// suit son utilisateur d'une machine a l'autre. C'est pourquoi le nom de machine est envoye a
/// cote : il rend l'ecart visible a l'ecran au lieu de le laisser tromper le lecteur.
/// </summary>
public static class StationIdentity
{
    private static readonly Lazy<Guid> LazyStationId = new(DesktopSettings.LoadOrCreateStationKey);

    private static readonly Lazy<string> LazyAppVersion = new(ResolveAppVersion);

    /// <summary>Identifiant stable de cette installation.</summary>
    public static Guid StationId => LazyStationId.Value;

    /// <summary>
    /// Nom de la machine. Environment.MachineName ne fait pas d'appel reseau et ne peut pas
    /// echouer ici ; il est neanmoins borne, un nom NetBIOS restant court par nature.
    /// </summary>
    public static string Label => Environment.MachineName;

    /// <summary>
    /// Version du client. C'est la donnee la plus utile que ce poste transmette : deux versions
    /// differentes en service contre la meme API est un danger d'exploitation reel, et c'est
    /// exactement ce que le registre sert a rendre visible.
    /// </summary>
    public static string AppVersion => LazyAppVersion.Value;

    private static string ResolveAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // On prefere la version informationnelle (qui porte le suffixe de build quand il existe)
        // et on retombe sur la version d'assembly. Le suffixe de metadonnees ajoute par le SDK
        // apres un '+' n'apporte rien a l'ecran et est retire.
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            var trimmed = plus > 0 ? informational[..plus] : informational;

            return Shorten(trimmed);
        }

        return Shorten(assembly.GetName().Version?.ToString() ?? "inconnue");
    }

    // La colonne serveur est bornee a 32 caracteres : on tronque ici plutot que de laisser le
    // serveur le faire, pour que l'ecran affiche exactement ce qui est stocke.
    private static string Shorten(string value)
    {
        var trimmed = value.Trim();

        return trimmed.Length <= 32 ? trimmed : trimmed[..32];
    }
}
