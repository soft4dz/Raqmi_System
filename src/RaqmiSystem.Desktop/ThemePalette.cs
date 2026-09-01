namespace RaqmiSystem.Desktop;

/// <summary>
/// Apparence choisie pour l'application.
///
/// Nomme <c>ApparenceMode</c> et non <c>ThemeMode</c> : .NET 10 a introduit
/// <c>System.Windows.Window.ThemeMode</c>, et le nom court aurait ete masque par la
/// propriete heritee dans tout code-behind de fenetre.
/// </summary>
public enum ApparenceMode
{
    /// <summary>Suit le reglage d'apparence de Windows.</summary>
    Systeme,

    Clair,

    Sombre
}

/// <summary>
/// Densite d'affichage des grilles.
///
/// Compact ne reduit PAS la taille du texte - il retire de l'air. Sur les ecrans de ce
/// produit, le facteur limitant est le nombre de lignes visibles sans defiler, pas la
/// finesse des caracteres ; rabougrir la police ferait perdre en lisibilite ce qu'on
/// gagnerait en lignes, et personne ne saisit plus vite sur du texte qu'il dechiffre.
/// </summary>
public enum ApparenceDensite
{
    /// <summary>Lignes de 40 px : la densite historique du produit.</summary>
    Confortable,

    /// <summary>Lignes de 32 px : un quart de lignes en plus a hauteur d'ecran egale.</summary>
    Compact
}

/// <summary>
/// Les deux palettes de l'application, clef par clef.
///
/// Le theme sombre n'est pas une inversion de la palette claire : une inversion
/// mecanique donne des accents criards, des ombres absurdes et des textes trop
/// contrastes, fatigants a la longue. Chaque valeur est posee pour son role :
///
///   - les surfaces s'ECLAIRCISSENT avec l'elevation (fond &lt; carte &lt; carte survolee),
///     la ou en clair elles s'assombrissent : c'est la lumiere qui change de sens ;
///   - les accents s'ECLAIRCISSENT au lieu de s'assombrir, et le texte qu'ils portent
///     devient sombre - d'ou <c>AccentActionForegroundBrush</c>, sans quoi le bouton
///     principal serait blanc sur turquoise clair ;
///   - les badges gardent leur teinte semantique (vert = accompli, ambre = en attente,
///     rouge = refuse) mais renversent le couple : fond sombre teinte, texte clair ;
///   - le texte principal n'est pas blanc pur (#FFFFFF sur fond sombre eblouit et fait
///     baver les caracteres) mais un blanc bleute a 13,7:1.
///
/// L'ecran de connexion et l'en-tete de fenetre bougent a peine : ils sont deja sombres
/// dans les deux themes, c'est la scene de marque.
///
/// Les 45 paires texte/fond des deux palettes ont ete verifiees a WCAG AA - 4,5:1 pour
/// du texte, 3:1 pour un trait ou une bordure de champ.
/// </summary>
internal static class ThemePalette
{
    /// <summary>
    /// Palette sombre. Toute clef absente d'ici garde sa valeur claire, ce qui se verrait
    /// immediatement a l'ecran : <see cref="ThemeManager"/> verifie donc au demarrage que
    /// cette table couvre exactement les brushes du dictionnaire de ressources.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Sombre = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // ---- Structure et marque : l'en-tete reste la scene sombre de la marque ----
        ["StructureBrush"] = "#050F1C",
        ["StructureElevatedBrush"] = "#0C1B2B",
        ["HeaderForegroundBrush"] = "#EAF2F9",
        ["HeaderMutedBrush"] = "#8FB0CA",

        // ---- Surfaces : l'elevation eclaircit ----
        ["AppBackgroundBrush"] = "#0E1826",
        ["SurfaceBrush"] = "#152233",
        ["SurfaceSubtleBrush"] = "#1B2B3E",
        ["SurfaceHoverBrush"] = "#22344A",

        // ---- Bordures ----
        ["PanelBorderBrush"] = "#26374D",
        ["FieldBorderBrush"] = "#62748D",
        ["BorderStrongBrush"] = "#4B6E93",

        // ---- Textes ----
        ["TextPrimaryBrush"] = "#E6EEF7",
        ["TextSecondaryBrush"] = "#AFC3D6",
        ["TextMutedBrush"] = "#98AFC6",
        ["TextLabelBrush"] = "#C2D2E1",
        ["TextPlaceholderBrush"] = "#8DA4BC",
        ["MutedBrush"] = "#AFC3D6",

        // ---- Accents : eclaircis, et le texte qu'ils portent s'assombrit ----
        ["AccentBrush"] = "#22C4CE",
        ["AccentHoverBrush"] = "#3ED5DE",
        ["AccentPressedBrush"] = "#16A8B1",
        ["AccentActionBrush"] = "#2FCBD6",
        ["AccentActionHoverBrush"] = "#4CDAE3",
        ["AccentActionPressedBrush"] = "#21B3BD",
        ["AccentActionForegroundBrush"] = "#062227",
        ["AccentSoftBrush"] = "#0E3B40",
        ["AccentSelectionBrush"] = "#3D22C4CE",

        ["PrimaryBrush"] = "#5B9BE8",
        ["PrimaryHoverBrush"] = "#77AEEE",
        ["PrimaryPressedBrush"] = "#4585D2",
        ["SecondaryBrush"] = "#6FAEF0",
        ["SecondaryHoverBrush"] = "#8CC0F5",
        ["SecondaryPressedBrush"] = "#5697DB",

        // ---- Danger ----
        ["DangerBrush"] = "#F4776A",
        ["DangerHoverBrush"] = "#F89286",
        ["DangerPressedBrush"] = "#DC5C4F",
        ["DangerSoftBrush"] = "#3A1A17",
        ["DangerBorderBrush"] = "#5E2D28",

        // ---- Focus clavier ----
        ["FocusRingBrush"] = "#7FB6F5",
        ["FocusRingOnFilledBrush"] = "#08222A",
    ["FocusRingOnDarkBrush"] = "#EAF2F9",

        // ---- Etats desactives ----
        ["DisabledBackgroundBrush"] = "#1C2938",
        ["DisabledForegroundBrush"] = "#8195AB",
        ["DisabledBorderBrush"] = "#26374D",

        // ---- Navigation laterale ----
        ["ModuleInactiveBackgroundBrush"] = "#1B2B3E",
        ["ModuleActiveBackgroundBrush"] = "#2622C4CE",

        // ---- Grilles ----
        ["RowHoverBrush"] = "#1B2B3E",
        ["RowAltBrush"] = "#182637",
        ["GridHeaderForegroundBrush"] = "#9CB3C9",
        ["ScrollThumbBrush"] = "#53759B",
        ["ScrollThumbHoverBrush"] = "#708FB2",

        // ---- Badges de statut : teinte semantique conservee, couple renverse ----
        ["StatusDraftBackgroundBrush"] = "#26313E",
        ["StatusDraftForegroundBrush"] = "#C4D0DC",
        ["StatusSubmittedBackgroundBrush"] = "#3B2E11",
        ["StatusSubmittedForegroundBrush"] = "#F5C761",
        ["StatusValidatedBackgroundBrush"] = "#11321F",
        ["StatusValidatedForegroundBrush"] = "#68D397",
        ["StatusRejectedBackgroundBrush"] = "#3A1A19",
        ["StatusRejectedForegroundBrush"] = "#F58C82",

        // ---- Avancement des modules (accueil) ----
        ["ModuleStatusAvailableBackgroundBrush"] = "#11321F",
        ["ModuleStatusAvailableForegroundBrush"] = "#68D397",
        ["ModuleStatusApiBackgroundBrush"] = "#0E3439",
        ["ModuleStatusApiForegroundBrush"] = "#4FD3DC",
        ["ModuleStatusPartialBackgroundBrush"] = "#3A2D12",
        ["ModuleStatusPartialForegroundBrush"] = "#EFC15C",
        ["ModuleStatusPlannedBackgroundBrush"] = "#212F3F",
        ["ModuleStatusPlannedForegroundBrush"] = "#A8BCD0",
        ["ModuleProgressAvailableBrush"] = "#3FBF74",
        ["ModuleProgressApiBrush"] = "#22C4CE",
        ["ModuleProgressPartialBrush"] = "#E5B23A",
        ["ModuleProgressPlannedBrush"] = "#506D91",

        // ---- Maturite des domaines : meme renversement que les badges, fond sombre
        //      teinte et texte clair (7,0 a 8,0:1) ; accents eclaircis pour tenir 3:1
        //      sur SurfaceBrush sombre ----
        ["MaturityPlannedBackgroundBrush"] = "#212F3F",
        ["MaturityPlannedForegroundBrush"] = "#A8BCD0",
        ["MaturityPlannedAccentBrush"] = "#506D91",
        ["MaturityTechnicalPreviewBackgroundBrush"] = "#3A2D12",
        ["MaturityTechnicalPreviewForegroundBrush"] = "#EFC15C",
        ["MaturityTechnicalPreviewAccentBrush"] = "#E5B23A",
        ["MaturityFunctionalBackgroundBrush"] = "#0E3439",
        ["MaturityFunctionalForegroundBrush"] = "#4FD3DC",
        ["MaturityFunctionalAccentBrush"] = "#22C4CE",
        ["MaturityProductionReadyBackgroundBrush"] = "#11321F",
        ["MaturityProductionReadyForegroundBrush"] = "#68D397",
        ["MaturityProductionReadyAccentBrush"] = "#3FBF74",
    };
}
