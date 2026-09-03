namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Une file composée pour le profil : la définition, le mode que ses clés donnent, le périmètre
/// résolu et l'onglet que la carte ouvrira (cible, repli, ou cible verrouillée).
/// </summary>
/// <param name="TargetLocked">
/// Vrai quand ni la cible ni le repli ne sont ouvrables : le bouton est désactivé avec cadenas,
/// mais le chiffre reste lisible — lire un compteur n'est pas ouvrir l'écran.
/// </param>
public sealed record HomeSlot(
    HomeWorkQueueDefinition Queue,
    HomeMode Mode,
    HomeScope Scope,
    int TargetTab,
    bool TargetLocked);

/// <summary>Une section de « Mon travail » : ses slots (vides hors des trois bandes) et la raison d'un vide.</summary>
public sealed record HomeSection(
    HomeSectionKind Kind,
    IReadOnlyList<HomeSlot> Slots,
    HomeEmptyReason EmptyReason);

/// <summary>
/// Résultat de la composition : les sections dans l'ordre de rendu, les sources à appeler dans
/// l'ordre de l'énumération (dédoublonnées), et ce que le bandeau peut afficher.
/// </summary>
/// <param name="ShowBusinessDate">La date métier est lisible : <c>lodging.read</c> et unité du poste.</param>
/// <param name="ShowUnitLine">Au moins une file de périmètre Unité est lisible : la ligne « Unité du poste » a un sens.</param>
/// <param name="ShowUnitMissingBanner">Des files unitaires lisibles n'ont pas été composées faute d'unité de poste.</param>
/// <param name="ShowEstablishment">Le nom de l'établissement peut être lu (<c>settings.read</c>).</param>
/// <param name="CanReadUnits">La liste des unités est lisible : le nom de l'unité du poste peut compléter son code.</param>
/// <param name="CanOpenSettings">Le paramétrage (onglet 9) est ouvrable : boutons Changer / Mon profil / Mes préférences.</param>
public sealed record HomeLayout(
    IReadOnlyList<HomeSection> Sections,
    IReadOnlyList<HomeSource> Sources,
    bool ShowBusinessDate,
    bool ShowUnitLine,
    bool ShowUnitMissingBanner,
    int UnitQueuesSkipped,
    bool ShowEstablishment,
    bool CanReadUnits,
    bool CanOpenSettings)
{
    public HomeSection Band(HomeBand band) => Sections.Single(section => section.Kind == KindOf(band));

    /// <summary>Tous les slots composés, bande par bande.</summary>
    public IEnumerable<HomeSlot> Slots => Sections.SelectMany(section => section.Slots);

    /// <summary>Aucune carte ne porte de verbe : le bandeau ajoute « suivi seulement ».</summary>
    public bool WatchOnly => Slots.Any() && Slots.All(slot => slot.Mode != HomeMode.Act);

    public static HomeSectionKind KindOf(HomeBand band) => band switch
    {
        HomeBand.Overdue => HomeSectionKind.Overdue,
        HomeBand.Today => HomeSectionKind.Today,
        HomeBand.Watch => HomeSectionKind.Watch,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Bande inconnue.")
    };
}
