namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// La fiche d'identite d'un indicateur : ce qu'il mesure, comment il se calcule, dans quelle
/// unite, a partir de quel module, avec quelle regle de consolidation et sous quelle
/// permission. Elle est immuable et vit dans <see cref="KpiCatalog"/>.
///
/// Une definition ne contient AUCUNE valeur : ni le chiffre du jour, ni le budget, ni les
/// seuils. Les seuils sont des donnees de l'etablissement (<see cref="KpiThreshold"/>), les
/// valeurs sont calculees a la demande. C'est ce qui permet de comparer deux installations sur
/// la meme grille tout en laissant chacune fixer ses propres bornes.
/// </summary>
/// <param name="Code">
/// Identifiant stable et technique de l'indicateur (voir <see cref="KpiCodes"/>). Il apparait
/// dans les URL, dans les instantanes historises et dans les seuils configures : il ne change
/// JAMAIS, meme si le libelle evolue.
/// </param>
/// <param name="Name">Libelle complet, affiche en titre de fiche.</param>
/// <param name="ShortName">Libelle court, pour les tuiles et les en-tetes de colonne.</param>
/// <param name="Category">Famille metier, pour le regroupement des ecrans.</param>
/// <param name="Description">Ce que l'indicateur dit reellement, en une phrase de gestion.</param>
/// <param name="Formula">
/// La formule telle qu'un controleur de gestion l'ecrirait, en clair. Elle est affichee a
/// l'utilisateur : c'est ce qui rend un chiffre discutable plutot qu'a croire sur parole.
/// </param>
/// <param name="Unit">Unite de mesure, qui commande le formatage cote client.</param>
/// <param name="Polarity">Sens de lecture : une hausse est-elle bonne, mauvaise ou neutre ?</param>
/// <param name="Aggregation">Regle de consolidation d'un groupe multi-unites.</param>
/// <param name="ScopeLevel">
/// Maille a laquelle l'indicateur a un sens dans ce produit. Presque tous sont mesurables par
/// unite ; ceux qui derivent de la comptabilite ou des ordres de paiement ne le sont pas, ces
/// donnees ne portant pas d'unite hoteliere - voir <see cref="KpiScopeLevel"/>.
/// </param>
/// <param name="RefreshTriggers">Evenements qui justifient de poser un instantane.</param>
/// <param name="SourceModule">Module de Raqmi System qui possede la donnee d'origine.</param>
/// <param name="SourceDetail">
/// Ce qui est effectivement compte, statuts inclus. Ce champ est la reponse a la question
/// "d'ou sort ce chiffre ?" et il doit rester assez precis pour qu'on puisse aller le verifier
/// dans l'ecran du module source.
/// </param>
/// <param name="RequiredPermissions">
/// Cles de <c>PermissionCatalog</c> exigees EN PLUS de la permission d'entree du module de
/// pilotage, et exigees TOUTES a la fois. Ce sont celles des modules sources : un ratio ne doit
/// jamais reveler une donnee que l'utilisateur n'a pas le droit de lire directement. L'ADR en
/// exige deux (recettes et hebergement) parce qu'il croise deux modules ; le lecteur qui n'a
/// que l'un des deux ne voit pas l'indicateur du tout, plutot que de le voir a moitie.
/// </param>
/// <param name="Availability">Calculable aujourd'hui, ou en attente de sa source.</param>
/// <param name="MissingSource">
/// Pour un indicateur en attente : ce qui manque exactement dans le produit, nomme de facon a
/// etre actionnable ("module GMAO : equipements et ordres de travail"). Null sinon.
/// </param>
/// <param name="FormulaVersion">
/// Version de la formule, incrementee des que le calcul change de sens. Elle est copiee sur
/// chaque instantane historise : sans elle, une courbe pluriannuelle melangerait des valeurs
/// obtenues par deux formules differentes sans que personne ne puisse s'en apercevoir.
/// </param>
public sealed record KpiDefinition(
    string Code,
    string Name,
    string ShortName,
    KpiCategory Category,
    string Description,
    string Formula,
    KpiUnit Unit,
    KpiPolarity Polarity,
    KpiAggregation Aggregation,
    KpiRefreshTrigger RefreshTriggers,
    KpiSourceModule SourceModule,
    string SourceDetail,
    IReadOnlyCollection<string> RequiredPermissions,
    KpiScopeLevel ScopeLevel = KpiScopeLevel.UnitAndGroup,
    KpiAvailability Availability = KpiAvailability.Implemented,
    string? MissingSource = null,
    int FormulaVersion = 1)
{
    /// <summary>
    /// Le moteur sait-il produire une valeur pour cet indicateur ? Raccourci de lecture,
    /// utilise par le calculateur pour court-circuiter proprement les indicateurs declares
    /// mais non alimentes.
    /// </summary>
    public bool IsComputable => Availability == KpiAvailability.Implemented;
}
