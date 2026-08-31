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
