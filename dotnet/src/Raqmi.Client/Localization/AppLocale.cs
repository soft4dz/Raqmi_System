namespace Raqmi.Client.Localization;

public enum AppLocale
{
    Fr,
    En,
    Ar,
}

public static class AppLocaleExtensions
{
    public static string ToCode(this AppLocale locale) => locale switch
    {
        AppLocale.En => "en",
        AppLocale.Ar => "ar",
        _ => "fr",
    };

    public static AppLocale FromCode(string? code) => code?.ToLowerInvariant() switch
    {
        "en" => AppLocale.En,
        "ar" => AppLocale.Ar,
        _ => AppLocale.Fr,
    };

    public static bool IsRtl(this AppLocale locale) => locale == AppLocale.Ar;

    public static string Label(this AppLocale locale) => locale switch
    {
        AppLocale.En => "EN",
        AppLocale.Ar => "AR",
        _ => "FR",
    };
}
