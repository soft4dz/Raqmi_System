using System.Globalization;
using System.Text;
using RaqmiSystem.Desktop;

namespace RaqmiSystem.DocShots;

// Un ecran a capturer. La liste n'est pas ecrite ici : elle est DEDUITE de ModuleCatalog,
// seule source de verite de l'avancement. Un module qui passe Planifie -> Disponible entre
// donc dans le guide a la campagne suivante, sans qu'on ait a y penser.
internal sealed record CaptureTarget(
    int TabIndex,
    string FileName,
    string Title,
    string Name,
    string Group,
    string Priority,
    string Status,
    string Description,
    string? StatusNote,
    IReadOnlyList<string> Orders)
{
    private const int HomeTabIndex = 0;

    public static IReadOnlyList<CaptureTarget> BuildAll()
    {
        var targets = new List<CaptureTarget>
        {
            new(
                HomeTabIndex,
                FileNameFor(HomeTabIndex, "mon-espace"),
                "Mon Espace - Mon travail",
                "Mon Espace",
                "Socle",
                "P0",
                "Disponible",
                "Files de travail composees des permissions du profil, et catalogue des 50 modules en seconde section.",
                null,
                Array.Empty<string>())
        };

        // Un onglet peut porter plusieurs modules du catalogue : le journal d'audit (22) et
        // la journalisation (30) sont le meme ecran. On capture l'ecran une fois et on note
        // les deux numeros, plutot que de livrer deux fois la meme image.
        var byTab = ModuleCatalog.Entries
            .Where(entry => entry.TabIndex.HasValue)
            .Where(entry => entry.Status is ModuleStatus.Disponible or ModuleStatus.Partiel)
            .GroupBy(entry => entry.TabIndex!.Value)
            .OrderBy(group => group.Key);

        foreach (var group in byTab)
        {
            var first = group.First();
            var orders = group.Select(entry => entry.Order).ToArray();

            targets.Add(new CaptureTarget(
                group.Key,
                FileNameFor(group.Key, first.Name),
                string.Join(" / ", orders) + " - " + first.Name,
                first.Name,
                first.Group,
                first.Priority,
                ModuleCatalog.StatusLabel(first.Status),
                first.Description,
                first.StatusNote,
                orders));
        }

        return targets;
    }

    // Prefixe par l'index d'onglet : les fichiers se classent dans l'ordre de navigation
    // du client, et le nom reste stable meme si un module est renumerote.
    private static string FileNameFor(int tabIndex, string name) =>
        "tab" + tabIndex.ToString("00", CultureInfo.InvariantCulture) + "-" + Slugify(name) + ".png";

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastWasSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }
}
