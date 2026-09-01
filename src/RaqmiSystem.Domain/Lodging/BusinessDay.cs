namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// La date metier hoteliere d'une unite : le jour d'exploitation en cours, qui n'est pas la date
/// systeme.
///
/// LE PROBLEME QU'ELLE RESOUT. Il est reellement le 15 aout a 02h00, mais la cloture du 14 n'a pas
/// encore ete passee : la nuit du 14 se termine, la caisse du 14 est encore ouverte, et une
/// consommation saisie a cet instant appartient au 14. Utiliser la date systeme la classerait au
/// 15, ou elle n'aurait rien a faire, et la cloture du 14 serait fausse pour toujours. Toute la
/// comptabilite d'exploitation d'un hotel repose sur cette distinction.
///
/// LA REGLE, EN UNE PHRASE : la date metier est le LENDEMAIN de la derniere journee cloturee. Pas
/// une heure de bascule, pas une heure locale devinee - la cloture est un acte humain explicite,
/// et c'est lui qui fait avancer le jour. Une unite qui n'a encore rien cloture tombe sur la date
/// calendaire, seul point de depart possible.
///
/// LE RETARD EST VISIBLE, PAS CORRIGE. Quand une unite n'a pas cloture depuis trois jours, la date
/// metier reste trois jours en arriere et <see cref="IsLate"/> le dit. C'est voulu : avancer
/// automatiquement effacerait le probleme sans le resoudre, et ferait basculer des recettes dans
/// des journees que personne n'a controlees.
/// </summary>
public readonly record struct BusinessDay
{
    private BusinessDay(DateOnly date, DateOnly? lastClosedDate, DateOnly calendarDate, bool hasClosing)
    {
        Date = date;
        LastClosedDate = lastClosedDate;
        CalendarDate = calendarDate;
        HasClosing = hasClosing;
    }

    /// <summary>Le jour d'exploitation en cours.</summary>
    public DateOnly Date { get; }

    /// <summary>Derniere journee cloturee, quand il y en a une.</summary>
    public DateOnly? LastClosedDate { get; }

    /// <summary>Date calendaire de reference utilisee pour le calcul.</summary>
    public DateOnly CalendarDate { get; }

    /// <summary>Faux quand l'unite n'a jamais rien cloture : la date metier suit alors le calendrier.</summary>
    public bool HasClosing { get; }

    /// <summary>
    /// Vrai quand la date metier est en retard sur le calendrier : au moins une journee attend sa
    /// cloture. Le night audit et les tableaux de bord s'en servent pour alerter.
    /// </summary>
    public bool IsLate => Date < CalendarDate;

    /// <summary>Nombre de journees en attente de cloture. Zero quand tout est a jour.</summary>
    public int PendingDays => Math.Max(0, CalendarDate.DayNumber - Date.DayNumber);

    /// <summary>
    /// Calcule la date metier a partir de la derniere journee CLOTUREE de l'unite et de la date
    /// calendaire. Une journee reouverte ne compte pas comme cloturee : l'appelant ne transmet
    /// que les clotures effectives.
    /// </summary>
    public static BusinessDay Resolve(DateOnly? lastClosedDate, DateOnly calendarDate)
    {
        if (lastClosedDate is not { } lastClosed)
        {
            return new BusinessDay(calendarDate, null, calendarDate, hasClosing: false);
        }

        // Le lendemain de la derniere cloture, meme s'il devance le calendrier : quand le night
        // audit du 14 vient d'etre passe a 23h50, l'hotel travaille deja sur le 15.
        return new BusinessDay(lastClosed.AddDays(1), lastClosed, calendarDate, hasClosing: true);
    }

    public override string ToString()
    {
        return Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }
}
