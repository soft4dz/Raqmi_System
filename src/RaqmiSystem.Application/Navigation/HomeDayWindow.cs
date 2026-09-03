namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Les bornes d'une journée de poste, en heure LOCALE du poste : ce que les sources de
/// l'accueil qui prennent un instant (et non une date) doivent envoyer au serveur.
/// </summary>
/// <remarks>
/// Pourquoi une fonction plutôt qu'une expression posée dans la vue : la forme naïve
/// <c>new DateTimeOffset(DateTime.Today, TimeSpan.Zero)</c> lève <c>ArgumentException</c>
/// dès que le poste n'est pas à UTC — « L'offset UTC du paramètre dateTime local ne correspond
/// pas à l'argument offset ». L'Algérie est à UTC+1 : c'est le cas nominal, pas un cas limite.
/// Et cette exception-là n'est pas une erreur d'appel : elle ne passe donc pas par le
/// try/catch du contrat de vue, elle remonte au Dispatcher et ferme l'application.
///
/// La journée est donc bornée avec l'écart horaire du poste, comme le fait déjà l'écran
/// Cuisine (<c>KitchenView.ToInstantStart</c>) : minuit ici, minuit demain ici.
/// </remarks>
public static class HomeDayWindow
{
    /// <summary>Minuit du jour donné, avec l'écart horaire du fuseau du poste.</summary>
    public static DateTimeOffset Start(DateTime day, TimeZoneInfo? zone = null)
    {
        // Kind = Unspecified : le constructeur n'accepte un offset librement choisi que là.
        var local = DateTime.SpecifyKind(day.Date, DateTimeKind.Unspecified);

        return new DateTimeOffset(local, (zone ?? TimeZoneInfo.Local).GetUtcOffset(local));
    }

    /// <summary>Minuit du lendemain : borne haute exclusive de la journée du poste.</summary>
    public static DateTimeOffset End(DateTime day, TimeZoneInfo? zone = null) =>
        Start(day.Date.AddDays(1), zone);
}
