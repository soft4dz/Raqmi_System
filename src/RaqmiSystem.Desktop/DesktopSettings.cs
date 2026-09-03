using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Persists the desktop client's per-user preferences across sessions: the API base URL
/// (so pointing the client at a different server never requires a rebuild) and, when the
/// user opts in, the login credentials. Resolution order for the URL: environment
/// variable, then the per-user settings file, then a hard-coded fallback default.
/// </summary>
public sealed class DesktopSettings
{
    /// <summary>
    /// Name of the environment variable that overrides the stored API URL. Public because the
    /// settings screen must be able to TELL the user when it is set: an override silently wins
    /// over whatever they save there, and a URL that appears saved but is never used is a trap.
    /// </summary>
    public const string ApiBaseUrlEnvironmentVariable = "RAQMI_DESKTOP_API_URL";

    private const string DefaultApiBaseUrl = "http://localhost:5180";

    // Six puces tiennent sur une ligne a la largeur minimale de la fenetre, et au-dela
    // « derniers ecrans » cesse d'etre un raccourci pour devenir une seconde barre laterale.
    private const int MaxRecentTabs = 6;

    // Extra entropy mixed into DPAPI so another local app calling Unprotect with
    // CurrentUser scope alone cannot read the value. Not a secret by itself.
    private static readonly byte[] PasswordEntropy = Encoding.UTF8.GetBytes("RaqmiSystem.Desktop.v1");

    public string? ApiBaseUrl { get; set; }

    public string? RememberedUserName { get; set; }

    /// <summary>
    /// The remembered password, encrypted with Windows DPAPI (CurrentUser scope) and
    /// base64-encoded. Only the same Windows account on the same machine can decrypt
    /// it; the plaintext password is never written to disk.
    /// </summary>
    public string? ProtectedPassword { get; set; }

    /// <summary>
    /// Stable identifier of this workstation, generated once and kept for the life of the
    /// installation. It carries NO secret and identifies an installation, never a person.
    ///
    /// It lives in the per-user settings file, so it is bound to the WINDOWS PROFILE rather than
    /// to the machine: two Windows accounts on one PC are two workstations, and a roaming profile
    /// follows its user from PC to PC. The registry screen shows the machine name next to it so
    /// that this discrepancy stays visible instead of silently misleading the reader.
    /// </summary>
    public Guid? StationKey { get; set; }

    /// <summary>
    /// Apparence choisie sur ce poste : « Systeme », « Clair » ou « Sombre ». Le reglage est
    /// per-poste et non per-compte, parce qu'il depend de l'ecran et de l'eclairage du lieu,
    /// pas de qui se connecte : une reception de nuit garde son ecran sombre quand l'equipe
    /// change. Valeur absente ou inconnue = « Systeme ».
    /// </summary>
    public string? Apparence { get; set; }

    /// <summary>
    /// Densite des grilles sur ce poste : « Confortable » ou « Compact ». Meme portee que
    /// l'apparence - c'est l'ecran et le travail fait devant qui decident, pas le compte.
    /// </summary>
    public string? Densite { get; set; }

    /// <summary>
    /// Code de l'unite hoteliere a laquelle ce POSTE est rattache (« ALG-CEN »).
    ///
    /// Per-poste et non per-compte, comme l'apparence : un comptoir de reception appartient
    /// a un etablissement, quelle que soit la personne qui s'y connecte. C'est un confort de
    /// poste, jamais un perimetre de securite - le serveur reste seul juge de ce que le
    /// jeton donne le droit de lire, et un code faux ne donne pas des chiffres faux, il
    /// donne des cartes « Indisponible » avec le message du serveur.
    ///
    /// Absent = poste de siege : l'accueil ne compose alors aucune file unitaire et le dit.
    /// </summary>
    public string? StationUnitCode { get; set; }

    /// <summary>
    /// Derniers onglets ouverts sur ce poste, du plus recent au plus ancien, six au plus.
    /// « Ce poste » est dans le libelle de l'accueil : sur un comptoir partage, ce sont les
    /// ecrans du poste, pas ceux de la personne. Ce ne sont pas des favoris par compte -
    /// il n'en existe pas cote serveur, et en simuler serait mentir.
    /// </summary>
    public int[]? RecentTabs { get; set; }

    /// <summary>
    /// The URL forced by <see cref="ApiBaseUrlEnvironmentVariable"/>, or null when the variable is
    /// unset. When it is set, <see cref="Save"/> still writes to the settings file but the stored
    /// value is never used - the settings screen surfaces that instead of letting it surprise the
    /// user at the next start.
    /// </summary>
    public static string? ApiBaseUrlEnvironmentOverride
    {
        get
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);

            return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment.Trim();
        }
    }

    public static string Load()
    {
        if (ApiBaseUrlEnvironmentOverride is { } fromEnvironment)
        {
            return fromEnvironment;
        }

        var settings = ReadFile();

        return string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
            ? DefaultApiBaseUrl
            : settings.ApiBaseUrl.Trim();
    }

    /// <summary>Apparence enregistree sur ce poste, « Systeme » a defaut.</summary>
    public static ApparenceMode LoadApparence()
    {
        var stored = ReadFile().Apparence;

        return Enum.TryParse<ApparenceMode>(stored, ignoreCase: true, out var mode)
            ? mode
            : ApparenceMode.Systeme;
    }

    /// <summary>Densite enregistree sur ce poste, « Confortable » a defaut.</summary>
    public static ApparenceDensite LoadDensite()
    {
        return Enum.TryParse<ApparenceDensite>(ReadFile().Densite, ignoreCase: true, out var densite)
            ? densite
            : ApparenceDensite.Confortable;
    }

    public static void SaveDensite(ApparenceDensite densite)
    {
        var settings = ReadFile();
        settings.Densite = densite.ToString();
        WriteFile(settings);
    }

    /// <summary>Unite de ce poste, ou null quand le poste n'est rattache a aucune unite.</summary>
    public static string? LoadStationUnitCode()
    {
        var stored = ReadFile().StationUnitCode;

        return string.IsNullOrWhiteSpace(stored) ? null : stored.Trim().ToUpperInvariant();
    }

    /// <summary>Fixe (ou efface, avec null ou une chaine vide) l'unite de ce poste.</summary>
    public static void SaveStationUnitCode(string? unitCode)
    {
        var settings = ReadFile();
        settings.StationUnitCode = string.IsNullOrWhiteSpace(unitCode) ? null : unitCode.Trim().ToUpperInvariant();
        WriteFile(settings);
    }

    /// <summary>Derniers onglets ouverts sur ce poste, du plus recent au plus ancien.</summary>
    public static IReadOnlyList<int> LoadRecentTabs()
    {
        return ReadFile().RecentTabs is { } tabs
            ? tabs.Distinct().Take(MaxRecentTabs).ToList()
            : [];
    }

    /// <summary>
    /// Enregistre l'ouverture d'un onglet : il passe en tete, les doublons disparaissent, et
    /// la liste ne garde que les six derniers. L'ecriture est silencieuse par construction
    /// (<see cref="WriteFile"/>) : un profil en lecture seule sur le disque ne doit pas faire
    /// echouer une navigation.
    /// </summary>
    public static void SaveRecentTab(int tabIndex)
    {
        var settings = ReadFile();
        var tabs = new List<int> { tabIndex };

        if (settings.RecentTabs is { } existing)
        {
            tabs.AddRange(existing.Where(tab => tab != tabIndex));
        }

        settings.RecentTabs = [.. tabs.Take(MaxRecentTabs)];
        WriteFile(settings);
    }

    public static void SaveApparence(ApparenceMode mode)
    {
        // Load-modify-write, comme partout ici : enregistrer l'apparence ne doit effacer ni
        // l'URL du serveur, ni les identifiants memorises, ni la clef du poste.
        var settings = ReadFile();
        settings.Apparence = mode.ToString();
        WriteFile(settings);
    }

    public static void Save(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return;
        }

        // Load-modify-write so saving the URL never wipes remembered credentials.
        var settings = ReadFile();
        settings.ApiBaseUrl = apiBaseUrl.Trim();
        WriteFile(settings);
    }

    /// <summary>
    /// Returns this workstation's identifier, creating and persisting it on first use.
    /// A failure to persist is not fatal: the caller still gets a usable identifier for the
    /// current session, and supervision is a convenience that must never block a login. The only
    /// consequence is that the workstation would appear as a new row at the next start.
    /// </summary>
    public static Guid LoadOrCreateStationKey()
    {
        var settings = ReadFile();

        if (settings.StationKey is { } existing && existing != Guid.Empty)
        {
            return existing;
        }

        var created = Guid.NewGuid();

        try
        {
            // Load-modify-write, comme partout ailleurs dans ce fichier : ecrire la cle ne doit
            // jamais effacer l'URL ni les identifiants memorises.
            settings.StationKey = created;
            WriteFile(settings);
        }
        catch
        {
            // Voir le resume : on rend quand meme une cle utilisable pour la session en cours.
        }

        return created;
    }

    public static void SaveCredentials(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return;
        }

        try
        {
            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(password),
                PasswordEntropy,
                DataProtectionScope.CurrentUser);

            var settings = ReadFile();
            settings.RememberedUserName = userName.Trim();
            settings.ProtectedPassword = Convert.ToBase64String(protectedBytes);
            WriteFile(settings);
        }
        catch
        {
            // Remembering credentials is a convenience; a DPAPI/disk failure must never
            // interrupt an already-successful login.
        }
    }

    public static void ClearCredentials()
    {
        var settings = ReadFile();

        if (settings.RememberedUserName is null && settings.ProtectedPassword is null)
        {
            return;
        }

        settings.RememberedUserName = null;
        settings.ProtectedPassword = null;
        WriteFile(settings);
    }

    public static bool TryLoadCredentials(out string userName, out string password)
    {
        userName = string.Empty;
        password = string.Empty;

        var settings = ReadFile();

        if (string.IsNullOrWhiteSpace(settings.RememberedUserName) ||
            string.IsNullOrWhiteSpace(settings.ProtectedPassword))
        {
            return false;
        }

        try
        {
            var plainBytes = ProtectedData.Unprotect(
                Convert.FromBase64String(settings.ProtectedPassword),
                PasswordEntropy,
                DataProtectionScope.CurrentUser);

            userName = settings.RememberedUserName.Trim();
            password = Encoding.UTF8.GetString(plainBytes);
            return true;
        }
        catch
        {
            // Undecryptable blob (other Windows account, restored file, corruption):
            // drop it so the login screen simply starts empty instead of erroring.
            ClearCredentials();
            return false;
        }
    }

    private static DesktopSettings ReadFile()
    {
        try
        {
            var path = GetSettingsFilePath();

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<DesktopSettings>(json);

                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // The settings file is a convenience, not a source of truth: any read/parse
            // failure (missing folder, corrupted JSON, denied access, ...) behaves like
            // an absent file.
        }

        return new DesktopSettings();
    }

    private static void WriteFile(DesktopSettings settings)
    {
        try
        {
            var path = GetSettingsFilePath();
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(path, json);
        }
        catch
        {
            // Same rationale as ReadFile: never let a disk/permission failure surface.
        }
    }

    private static string GetSettingsFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RaqmiSystem");

        return Path.Combine(folder, "desktop-settings.json");
    }
}
