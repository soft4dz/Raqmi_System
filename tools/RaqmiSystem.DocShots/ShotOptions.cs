using System.Globalization;

namespace RaqmiSystem.DocShots;

// Options de la campagne. Aucune valeur par defaut pour les identifiants : un outil de
// documentation n'a pas a embarquer un compte, meme de demonstration.
internal sealed class ShotOptions
{
    public const string Usage = """
        Utilisation :
          RaqmiSystem.DocShots --user <identifiant> --password <mot de passe> --out <dossier>
                               [--api http://localhost:5180] [--width 1680] [--height 1050]
                               [--scale 1.5] [--delay 2500]
        """;

    public required string UserName { get; init; }

    public required string Password { get; init; }

    public required string OutputDirectory { get; init; }

    public string ApiBaseUrl { get; init; } = "http://localhost:5180";

    public double Width { get; init; } = 1680;

    public double Height { get; init; } = 1050;

    // 1,5x : le guide reste net a l'ecran comme a l'impression sans quadrupler le poids
    // des PNG, et le rendu vectoriel de WPF ne perd rien a l'agrandissement.
    public double Scale { get; init; } = 1.5;

    public int DelayMilliseconds { get; init; } = 2500;

    public static ShotOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index += 2)
        {
            var key = args[index];

            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException("Argument invalide : " + key);
            }

            values[key[2..]] = args[index + 1];
        }

        return new ShotOptions
        {
            UserName = Required(values, "user"),
            Password = Required(values, "password"),
            OutputDirectory = Required(values, "out"),
            ApiBaseUrl = Optional(values, "api", "http://localhost:5180"),
            Width = Number(values, "width", 1680),
            Height = Number(values, "height", 1050),
            Scale = Number(values, "scale", 1.5),
            DelayMilliseconds = (int)Number(values, "delay", 2500)
        };
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Argument obligatoire manquant : --" + key);

    private static string Optional(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static double Number(IReadOnlyDictionary<string, string> values, string key, double fallback) =>
        values.TryGetValue(key, out var value)
        && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
}
