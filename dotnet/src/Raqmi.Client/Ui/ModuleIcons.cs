namespace Raqmi.Client.Ui;

/// <summary>Glyphs Segoe MDL2 Assets pour familles et statistiques.</summary>
public static class ModuleIcons
{
    public static string ForFamily(string family) => family.ToLowerInvariant() switch
    {
        "core" => "\uE80F",
        "finance" => "\uEB42",
        "hr" => "\uE716",
        "operations" => "\uE719",
        "specific" => "\uE734",
        "system" => "\uE713",
        _ => "\uE8A5",
    };

    public static string StatModules => "\uE8F1";
    public static string StatFamilies => "\uE8FD";
    public static string StatPack => "\uE7C3";
    public static string StatMode => "\uE7EF";

    public static string Server => "\uE968";
    public static string Lock => "\uE72E";
    public static string Shield => "\uE72E";
    public static string License => "\uE785";
    public static string Check => "\uE73E";
    public static string Block => "\uE711";
}
