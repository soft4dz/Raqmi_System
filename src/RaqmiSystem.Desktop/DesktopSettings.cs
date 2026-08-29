using System.IO;
using System.Text.Json;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Persists the desktop client's API base URL across sessions, without requiring a rebuild
/// to point the client at a different server. Resolution order: environment variable,
/// then the per-user settings file, then a hard-coded fallback default.
/// </summary>
public sealed class DesktopSettings
{
    private const string EnvironmentVariableName = "RAQMI_DESKTOP_API_URL";
    private const string DefaultApiBaseUrl = "http://localhost:5180";

    public string? ApiBaseUrl { get; set; }

    public static string Load()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);

        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment.Trim();
        }

        try
        {
            var path = GetSettingsFilePath();

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<DesktopSettings>(json);

                if (settings is not null && !string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
                {
                    return settings.ApiBaseUrl.Trim();
                }
            }
        }
        catch
        {
            // The settings file is a convenience, not a source of truth: any read/parse
            // failure (missing folder, corrupted JSON, denied access, ...) falls back to
            // the default below instead of surfacing to the caller.
        }

        return DefaultApiBaseUrl;
    }

    public static void Save(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return;
        }

        try
        {
            var path = GetSettingsFilePath();
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new DesktopSettings { ApiBaseUrl = apiBaseUrl.Trim() };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(path, json);
        }
        catch
        {
            // Remembering the API URL is a convenience for next launch; a disk/permission
            // failure here must never interrupt an already-successful login.
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
