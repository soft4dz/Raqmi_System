using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// LA bibliotheque d'indicateurs de Raqmi System : une seule liste, dans le domaine, qui fixe
/// pour chaque KPI son code, sa formule, son unite, sa polarite, sa regle de consolidation, son
/// module source et les permissions qu'il exige. Tout le reste du produit - moteur de calcul,
/// API, tableaux de bord, alertes, historisation - lit cette liste et n'en redefinit jamais un
/// morceau localement.
///
/// CE QUE CE CATALOGUE N'EST PAS : il ne contient ni valeur, ni seuil, ni objectif. Les seuils
/// sont des donnees de l'etablissement (<see cref="KpiThreshold"/>), les objectifs viennent du
/// module Budget, et les valeurs sont calculees a la demande sur les transactions. C'est ce qui
/// permet a deux installations de partager exactement la meme grille de lecture tout en
/// pilotant sur des bornes differentes.
///
/// LES INDICATEURS EN ATTENTE DE SOURCE. Le catalogue declare la bibliotheque complete attendue
/// d'un ERP hotelier, y compris des indicateurs que ce produit ne sait PAS encore calculer
/// faute de module (MTTR sans GMAO, ticket moyen sans point de vente, gaspillage sans releve de
/// pertes). Ils portent <see cref="KpiAvailability.AwaitingSource"/> et le nom exact de ce qui
/// leur manque. Les declarer fige leur formule et leur unite une fois pour toutes et permet aux
/// ecrans de les presenter comme "non disponible" plutot que de laisser croire a un trou dans
/// la reflexion ; les calculer a partir d'a-peu-pres serait la seule chose reellement
/// inacceptable - un chiffre faux sous un nom juste est pire que pas de chiffre du tout.
///
/// PERMISSIONS. Aucun indicateur n'invente sa propre cle : chacun exige celles des modules dont
/// il lit les donnees. Un ratio ne doit jamais servir de chemin detourne vers une donnee que
/// l'utilisateur n'a pas le droit de consulter dans l'ecran d'origine - la masse salariale
/// rapportee au CA reste une donnee de paie.
/// </summary>
public static class KpiCatalog
{
    private const string GmaoSource =
        "Module GMAO absent : il faudrait un referentiel d'equipements (mise en service, valeur) "
        + "et des ordres de travail dates (declaration, prise en charge, remise en service, "
        + "nature preventive ou corrective, temps passe, pieces consommees). Le module "
        + "\"Maintenance\" existant de Raqmi System couvre les sauvegardes de la base, pas "
        + "l'entretien des equipements.";

    private const string PosSource =
        "Module point de vente absent : il faudrait des tickets de caisse (nombre, montant, "
        + "couverts, service, serveur, point de vente) et le detail des articles vendus. Les "
        + "recettes journalieres portent le CA restauration en masse, jamais le detail des "
        + "ventes.";

    private const string WasteSource =
        "Releve des pertes absent : il faudrait un mouvement de stock de nature \"perte\" avec "
        + "son motif (surproduction, peremption, erreur de production, retour client, casse, "
        + "rupture de conservation). Le registre des mouvements ne distingue aujourd'hui qu'une "
        + "consommation, sans cause.";

    private const string ChannelSource =
        "Canal de reservation absent : il faudrait porter sur la reservation son origine "
        + "(direct, OTA, agence, centrale) et le cout de distribution associe.";

    private const string UtilitySource =
        "Releves de fluides absents : il faudrait un compteur par unite (energie, eau) avec ses "
        + "index periodiques et le cout associe.";

    /// <summary>
    /// Les indicateurs, dans l'ordre d'affichage des ecrans. L'ordre a un sens de gestion : la
    /// direction lit d'abord ce qu'elle a produit (occupation, prix, revenu), puis ce que cela
    /// a rapporte (resultat, marges), puis ce que cela coute (matiere, personnel).
    /// </summary>
    public static IReadOnlyList<KpiDefinition> All { get; } = BuildAll();

    private static readonly IReadOnlyDictionary<string, KpiDefinition> ByCodeIndex =
        All.ToDictionary(definition => definition.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>La definition portant ce code, ou null quand le code est inconnu.</summary>
    public static KpiDefinition? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return ByCodeIndex.GetValueOrDefault(code.Trim());
    }

    /// <summary>
    /// La definition portant ce code. Lance quand le code est inconnu : un appelant interne qui
    /// cite un code absent du catalogue a une faute de frappe, pas un cas metier.
    /// </summary>
    public static KpiDefinition Require(string code)
    {
        return Find(code)
            ?? throw new ArgumentException($"Indicateur inconnu : {code}.", nameof(code));
    }

    public static IReadOnlyList<KpiDefinition> InCategory(KpiCategory category)
    {
        return All.Where(definition => definition.Category == category).ToArray();
    }

    /// <summary>
    /// Les indicateurs de premier niveau du tableau de bord de direction : ou en sommes-nous, en
    /// dix chiffres. La liste est courte par decision - un tableau de bord qui montre tout ne
    /// montre rien - et elle suit l'ordre de lecture d'un comite de direction.
    /// </summary>
    public static IReadOnlyList<string> DirectionHeadlineCodes { get; } =
    [
        KpiCodes.RevenueTotal,
        KpiCodes.OccupancyRate,
        KpiCodes.Adr,
        KpiCodes.RevPar,
        KpiCodes.Ebitda,
        KpiCodes.OperatingMarginRate,
        KpiCodes.OperatingCashFlow,
        KpiCodes.ReceivablesTotal,
        KpiCodes.FoodCostRate,
        KpiCodes.PayrollToRevenueRate
    ];

    /// <summary>
    /// Les colonnes du comparatif inter-unites. Ce sont des indicateurs consolidables et
    /// comparables entre etablissements de tailles differentes : des taux et des ratios par
    /// chambre, jamais des volumes bruts qui classeraient simplement les hotels par nombre de
    /// chambres.
    /// </summary>
    public static IReadOnlyList<string> BenchmarkCodes { get; } =
    [
        KpiCodes.OccupancyRate,
        KpiCodes.Adr,
        KpiCodes.RevPar,
        KpiCodes.RevenueTotal,
        KpiCodes.Ebitda,
        KpiCodes.FoodCostRate,
        KpiCodes.PayrollToRevenueRate
    ];

    private static KpiDefinition[] BuildAll()
    {
        return
        [
            // ==================================================================================
            //                                   HEBERGEMENT
            // ==================================================================================
            new KpiDefinition(
                KpiCodes.OccupancyRate,
                "Taux d'occupation",
                "Occupation",
                KpiCategory.Accommodation,
                "Part de la capacite vendable effectivement occupee sur la periode. Les nuitees "
                + "gratuites et house use comptent dans l'occupation - la chambre est bien "
                + "occupee - mais sont exclues de l'ADR.",
                "Nuitees occupees / Nuitees disponibles x 100",
                KpiUnit.Percentage,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily | KpiRefreshTrigger.OnDailyClosing,
                KpiSourceModule.Lodging,
                "Nuitee occupee : une chambre distincte couverte cette nuit-la par un sejour ni "
                + "annule ni no-show (reserve, en cours et deja parti comptent tous). Nuitees "
                + "disponibles : chambres actives de l'unite, moins les chambres hors service "
                + "declarees par le housekeeping, multipliees par le nombre de jours.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.PhysicalRooms,
                "Chambres physiques",
                "Chambres",
                KpiCategory.Accommodation,
                "Parc de chambres declare, actives et inactives confondues. C'est la capacite "
                + "batie, pas la capacite vendable.",
                "Nombre de chambres du referentiel",
                KpiUnit.Count,
                KpiPolarity.Neutral,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Toutes les chambres du referentiel de l'unite, quel que soit leur statut actif.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.RoomsAvailable,
                "Nuitees disponibles",
                "Disponibles",
                KpiCategory.Accommodation,
                "Capacite reellement vendable de la periode : le denominateur de l'occupation et "
                + "du RevPAR.",
                "(Chambres actives - Chambres hors service) x Jours de la periode",
                KpiUnit.Nights,
                KpiPolarity.Neutral,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Chambres actives du referentiel, diminuees des chambres dont le housekeeping "
                + "declare l'etat hors service, multipliees par le nombre de jours de la periode.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.RoomsOutOfOrder,
                "Nuitees indisponibles",
                "Indisponibles",
                KpiCategory.Accommodation,
                "Capacite perdue sur la periode : chambres retirees de la vente pour panne, "
                + "travaux, nettoyage approfondi ou usage interne. Elles sortent de la capacite "
                + "vendable et font donc monter l'occupation a activite egale - c'est voulu, une "
                + "chambre qu'on ne peut pas vendre n'a pas a penaliser le taux, mais la "
                + "capacite perdue doit rester visible a cote de lui.",
                "Somme, jour par jour, des chambres actives retirees de la vente",
                KpiUnit.Nights,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Indisponibilites datees des chambres actives, comptees nuit par nuit sur la "
                + "convention [debut, fin[. Une chambre indisponible plusieurs fois la meme nuit "
                + "n'est comptee qu'une fois.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.RoomsOccupied,
                "Nuitees occupees",
                "Occupees",
                KpiCategory.Accommodation,
                "Nuitees effectivement occupees, gratuites comprises. Numerateur du taux "
                + "d'occupation.",
                "Somme, jour par jour, des chambres distinctes couvertes par un sejour bloquant",
                KpiUnit.Nights,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily | KpiRefreshTrigger.OnDailyClosing,
                KpiSourceModule.Lodging,
                "Sejours ni annules ni no-show, comptes en chambres distinctes par nuit, sur la "
                + "convention hoteliere [arrivee, depart[ : la nuit du jour de depart n'est pas "
                + "occupee.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.ComplimentaryRooms,
                "Nuitees gratuites",
                "Gratuites",
                KpiCategory.Accommodation,
                "Nuitees occupees a titre gracieux ou en house use. Elles occupent une chambre "
                + "sans produire de recette et doivent donc sortir du denominateur de l'ADR, "
                + "faute de quoi elles ecrasent artificiellement le prix moyen.",
                "Nuitees dont le tarif fige a la reservation est nul",
                KpiUnit.Nights,
                KpiPolarity.Neutral,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Sejours bloquants dont le tarif nuit fige a la creation vaut zero. Raqmi System "
                + "ne porte pas encore de motif de gratuite (invitation, house use, contrepartie "
                + "commerciale) : la gratuite est deduite du prix, jamais d'une intention "
                + "declaree.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.RoomsSold,
                "Nuitees vendues",
                "Vendues",
                KpiCategory.Accommodation,
                "Nuitees occupees hors gratuites : le denominateur de l'ADR.",
                "Nuitees occupees - Nuitees gratuites",
                KpiUnit.Nights,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily | KpiRefreshTrigger.OnDailyClosing,
                KpiSourceModule.Lodging,
                "Sejours bloquants dont le tarif nuit fige est strictement positif, comptes en "
                + "chambres distinctes par nuit.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.Adr,
                "Prix moyen par chambre vendue (ADR)",
                "ADR",
                KpiCategory.Accommodation,
                "Prix moyen realise sur les chambres reellement vendues. Les nuitees gratuites "
                + "sont exclues du denominateur : les inclure ferait baisser l'ADR sans qu'aucun "
                + "prix ait bouge.",
                "Revenus hebergement / Nuitees vendues",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily | KpiRefreshTrigger.OnDailyClosing,
                KpiSourceModule.DailyRevenue,
                "Revenus hebergement : colonne hebergement des recettes journalieres au statut "
                + "Validee. Nuitees vendues : sejours bloquants au tarif non nul, module "
                + "hebergement.",
                [PermissionCatalog.RevenueRead, PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.RevPar,
                "Revenu par chambre disponible (RevPAR)",
                "RevPAR",
                KpiCategory.Accommodation,
                "Le seul indicateur qui juge simultanement le remplissage et le prix. Un RevPAR "
                + "qui monte pendant que l'ADR baisse signale un hotel qui achete son occupation.",
                "Revenus hebergement / Nuitees disponibles "
                + "(controle : ADR x taux d'occupation vendue)",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily | KpiRefreshTrigger.OnDailyClosing,
                KpiSourceModule.DailyRevenue,
                "Revenus hebergement valides divises par les nuitees disponibles. L'identite "
                + "avec ADR x occupation se verifie contre le taux d'occupation VENDUE (nuitees "
                + "vendues / disponibles) ; contre le taux d'occupation publie, qui compte les "
                + "gratuites, les deux methodes ne coincident que si l'unite n'a offert aucune "
                + "nuitee.",
                [PermissionCatalog.RevenueRead, PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.TRevPar,
                "Revenu total par chambre disponible (TRevPAR)",
                "TRevPAR",
                KpiCategory.Accommodation,
                "Le RevPAR etendu a tout ce que l'hotel vend : restauration, boissons et autres "
                + "prestations comprises. Il mesure la capacite de l'etablissement a faire "
                + "consommer ses clients au-dela de la chambre.",
                "Chiffre d'affaires total / Nuitees disponibles",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily | KpiRefreshTrigger.OnDailyClosing,
                KpiSourceModule.DailyRevenue,
                "Somme des quatre colonnes des recettes journalieres validees (hebergement, "
                + "restauration, boissons, autres). Les activites annexes - spa, piscine, "
                + "location de salles, parking, marina - alimentent la colonne \"autres\" tant "
                + "qu'elles n'ont pas de ventilation propre dans les recettes journalieres.",
                [PermissionCatalog.RevenueRead, PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.GopPar,
                "Resultat brut d'exploitation par chambre disponible (GOPPAR)",
                "GOPPAR",
                KpiCategory.Accommodation,
                "Le juge de paix de la performance hoteliere : ce que chaque chambre disponible "
                + "rapporte reellement une fois les charges d'exploitation payees.",
                "Resultat brut d'exploitation (GOP) / Nuitees disponibles",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly | KpiRefreshTrigger.OnMonthlyClosing,
                KpiSourceModule.Accounting,
                "GOP calcule sur les ecritures comptabilisees selon le mapping de comptes "
                + "configure (voir l'indicateur GOP), divise par les nuitees disponibles du "
                + "module hebergement.",
                [PermissionCatalog.AccountingRead, PermissionCatalog.LodgingRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.Alos,
                "Duree moyenne de sejour (ALOS)",
                "ALOS",
                KpiCategory.Accommodation,
                "Nombre moyen de nuits par sejour. Une duree qui s'allonge reduit le cout "
                + "d'acquisition et la charge de menage a chiffre d'affaires egal.",
                "Nuitees totales / Nombre de sejours",
                KpiUnit.Nights,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Sejours bloquants dont l'arrivee tombe dans la periode, pour ne compter chaque "
                + "sejour qu'une fois ; les nuitees retenues sont celles du sejour entier, y "
                + "compris debordant de la periode.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.CancellationRate,
                "Taux d'annulation",
                "Annulations",
                KpiCategory.Accommodation,
                "Part des reservations annulees. Un taux qui derive signale une politique "
                + "tarifaire trop souple ou une pression concurrentielle sur les dates.",
                "Reservations annulees / Reservations totales x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Reservations dont l'arrivee prevue tombe dans la periode, tous statuts "
                + "confondus au denominateur, statut Annulee au numerateur. La distinction "
                + "annulation dans les delais / tardive / avec ou sans penalite n'est pas "
                + "calculee : la reservation ne porte ni delai d'annulation ni penalite "
                + "facturee.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.NoShowRate,
                "Taux de no-show",
                "No-show",
                KpiCategory.Accommodation,
                "Part des arrivees attendues dont le client ne s'est jamais presente. C'est une "
                + "perte seche : la chambre a ete bloquee et n'a pas ete vendue.",
                "Reservations no-show / Reservations attendues x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Reservations attendues : celles dont l'arrivee tombe dans la periode et qui "
                + "n'ont pas ete annulees. Numerateur : celles constatees no-show.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.NoShowLostRevenue,
                "Revenu perdu sur no-show",
                "Perte no-show",
                KpiCategory.Accommodation,
                "Valorisation des nuitees jamais vendues faute de presentation du client, au "
                + "tarif auquel la reservation avait ete prise.",
                "Somme (tarif fige x nuits du sejour) des reservations no-show",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Tarif nuit fige a la creation de la reservation, multiplie par le nombre de "
                + "nuits prevues. C'est un manque a gagner theorique, pas une perte comptable : "
                + "aucune penalite facturee n'est deduite, Raqmi System ne rattache pas encore "
                + "de penalite a une reservation.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.GuestNights,
                "Nuitees clients",
                "Nuitees clients",
                KpiCategory.Accommodation,
                "Nombre de personnes hebergees, nuit par nuit. Base des ratios de consommation "
                + "par client, distincte des nuitees chambres.",
                "Somme (nombre de personnes x nuits) des sejours bloquants",
                KpiUnit.Nights,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Nombre de personnes declare sur le sejour, multiplie par les nuits du sejour "
                + "tombant dans la periode.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.RevenuePerGuest,
                "Revenu par client",
                "Rev./client",
                KpiCategory.Accommodation,
                "Ce que rapporte en moyenne une personne hebergee, chambre et prestations "
                + "confondues.",
                "Chiffre d'affaires total / Nuitees clients",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.DailyRevenue,
                "Recettes journalieres validees, toutes colonnes, divisees par les nuitees "
                + "clients du module hebergement.",
                [PermissionCatalog.RevenueRead, PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.BookingLeadTime,
                "Delai moyen de reservation",
                "Lead time",
                KpiCategory.Accommodation,
                "Nombre de jours entre la prise de reservation et l'arrivee. Il dit de combien "
                + "de visibilite commerciale l'hotel dispose reellement.",
                "Somme (date d'arrivee - date de prise) / Nombre de reservations",
                KpiUnit.Days,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Lodging,
                "Date de creation de la reservation dans le systeme et date d'arrivee prevue, "
                + "sur les reservations non annulees arrivant dans la periode. Une reservation "
                + "saisie apres l'arrivee (walk-in enregistre en retard) compte pour zero jour, "
                + "jamais pour un delai negatif.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.Cpor,
                "Cout par chambre occupee (CPOR)",
                "CPOR",
                KpiCategory.Accommodation,
                "Ce que coute reellement une chambre occupee, charges d'exploitation comprises. "
                + "Confronte a l'ADR, il dit si chaque chambre vendue est rentable.",
                "Charges d'exploitation / Nuitees occupees",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly | KpiRefreshTrigger.OnMonthlyClosing,
                KpiSourceModule.Accounting,
                "Charges d'exploitation : comptes rattaches aux groupes charges "
                + "departementales et charges non reparties du mapping configure, sur les "
                + "ecritures comptabilisees de la periode.",
                [PermissionCatalog.AccountingRead, PermissionCatalog.LodgingRead],
                KpiScopeLevel.GroupOnly),

            // ==================================================================================
            //                                     FINANCE
            // ==================================================================================
            new KpiDefinition(
                KpiCodes.RevenueTotal,
                "Chiffre d'affaires",
                "CA",
                KpiCategory.Finance,
                "Le chiffre d'affaires d'exploitation de la periode, toutes activites "
                + "confondues.",
                "Hebergement + Restauration + Boissons + Autres prestations",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily | KpiRefreshTrigger.OnDailyClosing,
                KpiSourceModule.DailyRevenue,
                "Recettes journalieres au statut Validee uniquement. Un brouillon est une frappe "
                + "non controlee, une recette soumise attend son controle, une recette rejetee a "
                + "ete refusee : aucune des trois n'est du chiffre d'affaires.",
                [PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.RevenueAccommodation,
                "Chiffre d'affaires hebergement",
                "CA hebergement",
                KpiCategory.Finance,
                "Part hebergement du chiffre d'affaires.",
                "Colonne hebergement des recettes journalieres validees",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.DailyRevenue,
                "Recettes journalieres validees, colonne hebergement.",
                [PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.RevenueFood,
                "Chiffre d'affaires restauration",
                "CA denrees",
                KpiCategory.Finance,
                "Part restauration du chiffre d'affaires : le denominateur du food cost.",
                "Colonne restauration des recettes journalieres validees",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.DailyRevenue,
                "Recettes journalieres validees, colonne restauration.",
                [PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.RevenueBeverage,
                "Chiffre d'affaires boissons",
                "CA boissons",
                KpiCategory.Finance,
                "Part boissons du chiffre d'affaires : le denominateur du beverage cost.",
                "Colonne boissons des recettes journalieres validees",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.DailyRevenue,
                "Recettes journalieres validees, colonne boissons.",
                [PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.RevenueOther,
                "Chiffre d'affaires autres prestations",
                "CA autres",
                KpiCategory.Finance,
                "Prestations hors hebergement et restauration : spa, piscine, salles, parking, "
                + "marina et divers.",
                "Colonne autres des recettes journalieres validees",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.DailyRevenue,
                "Recettes journalieres validees, colonne autres. Aucune ventilation par activite "
                + "annexe n'existe a ce niveau : la separer demanderait des colonnes dediees dans "
                + "les recettes journalieres.",
                [PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.RevenueBudgetVariance,
                "Ecart budgetaire sur chiffre d'affaires",
                "Ecart budget",
                KpiCategory.Finance,
                "Difference entre le chiffre d'affaires realise et l'objectif budgete de la "
                + "periode.",
                "Chiffre d'affaires realise - Objectif budgete",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Budgeting,
                "Objectifs mensuels des plans budgetaires figes (Approuve ou Cloture) des mois "
                + "que la periode touche, un mois partiellement couvert comptant en entier : le "
                + "budget est mensuel par construction, le decouper au jour inventerait une "
                + "saisonnalite que personne n'a budgetee.",
                [PermissionCatalog.RevenueRead, PermissionCatalog.BudgetRead]),

            new KpiDefinition(
                KpiCodes.RevenueBudgetAchievement,
                "Taux de realisation du budget",
                "Realisation",
                KpiCategory.Finance,
                "Part de l'objectif budgetaire atteinte. Cent pour cent signifie exactement le "
                + "budget.",
                "Chiffre d'affaires realise / Objectif budgete x 100",
                KpiUnit.Percentage,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Budgeting,
                "Meme base que l'ecart budgetaire. Une unite sans plan fige n'a pas d'objectif : "
                + "l'indicateur est alors indisponible, jamais zero.",
                [PermissionCatalog.RevenueRead, PermissionCatalog.BudgetRead]),

            new KpiDefinition(
                KpiCodes.GrossOperatingProfit,
                "Resultat brut d'exploitation (GOP)",
                "GOP",
                KpiCategory.Finance,
                "Ce que l'exploitation degage avant charges fixes de propriete, dotations, "
                + "resultat financier et impot. C'est le resultat dont le directeur d'unite "
                + "repond reellement.",
                "Produits d'exploitation - Charges departementales - Charges non reparties",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly | KpiRefreshTrigger.OnMonthlyClosing,
                KpiSourceModule.Accounting,
                "Ecritures au statut Comptabilisee de la periode, agregees selon le mapping de "
                + "comptes du module KPI. Tant qu'aucun mapping n'est configure, l'indicateur "
                + "repond \"donnee manquante\" : sans classement explicite des comptes, tout "
                + "resultat affiche serait le resultat comptable complet presente sous le nom de "
                + "GOP.",
                [PermissionCatalog.AccountingRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.Ebitda,
                "Excedent brut d'exploitation (EBE / EBITDA)",
                "EBE",
                KpiCategory.Finance,
                "Le GOP diminue des charges fixes de propriete : loyers, taxes et assurances. "
                + "C'est le resultat avant amortissements, resultat financier et impot.",
                "GOP - Charges fixes de propriete",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly | KpiRefreshTrigger.OnMonthlyClosing,
                KpiSourceModule.Accounting,
                "Meme base que le GOP, diminuee du groupe charges fixes du mapping de comptes. "
                + "Quand aucun compte n'est rattache aux charges fixes, EBE et GOP coincident - "
                + "ce qui se voit alors dans le detail des composantes.",
                [PermissionCatalog.AccountingRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.GrossMarginRate,
                "Taux de marge brute",
                "Marge brute",
                KpiCategory.Finance,
                "Part du chiffre d'affaires qui reste apres les seules charges directes des "
                + "departements.",
                "(Produits - Charges departementales) / Produits x 100",
                KpiUnit.Percentage,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Accounting,
                "Produits et charges departementales du mapping de comptes, sur les ecritures "
                + "comptabilisees.",
                [PermissionCatalog.AccountingRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.OperatingMarginRate,
                "Taux de marge operationnelle",
                "Marge op.",
                KpiCategory.Finance,
                "Part du chiffre d'affaires transformee en resultat brut d'exploitation. La "
                + "mesure de rentabilite la plus comparable entre unites de tailles differentes.",
                "GOP / Produits d'exploitation x 100",
                KpiUnit.Percentage,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Accounting,
                "GOP rapporte aux produits d'exploitation du mapping de comptes.",
                [PermissionCatalog.AccountingRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.CashIn,
                "Encaissements",
                "Encaissements",
                KpiCategory.Finance,
                "Argent effectivement entre sur la periode.",
                "Somme des encaissements confirmes",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily | KpiRefreshTrigger.OnDailyClosing,
                KpiSourceModule.Treasury,
                "Encaissements au statut Confirme uniquement : un brouillon ou un encaissement "
                + "annule n'est pas de l'argent entre.",
                [PermissionCatalog.TreasuryRead]),

            new KpiDefinition(
                KpiCodes.CashOut,
                "Decaissements",
                "Decaissements",
                KpiCategory.Finance,
                "Argent effectivement sorti sur la periode.",
                "Somme des ordres de paiement regles",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Treasury,
                "Ordres de paiement au statut Regle, dates dans la periode. Ils ne portent PAS "
                + "d'unite hoteliere dans Raqmi System : le decaissement n'existe donc qu'au "
                + "niveau groupe et n'est jamais reparti entre les unites.",
                [PermissionCatalog.TreasuryRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.OperatingCashFlow,
                "Flux de tresorerie d'exploitation",
                "Cash-flow",
                KpiCategory.Finance,
                "Solde entre ce qui est entre et ce qui est sorti sur la periode.",
                "Encaissements - Decaissements",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Treasury,
                "Encaissements confirmes moins ordres de paiement regles. Groupe uniquement, les "
                + "decaissements n'etant pas rattaches a une unite.",
                [PermissionCatalog.TreasuryRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.CashBalance,
                "Solde de tresorerie",
                "Tresorerie",
                KpiCategory.Finance,
                "Position de tresorerie a la date de lecture, tous comptes bancaires et caisses "
                + "confondus.",
                "Somme des soldes des comptes de tresorerie",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.None,
                "Non calculable : le compte bancaire de Raqmi System est un referentiel "
                + "d'identification, sans solde ni releve.",
                [PermissionCatalog.TreasuryRead],
                KpiScopeLevel.GroupOnly,
                KpiAvailability.AwaitingSource,
                "Releves bancaires absents : il faudrait un solde initial par compte et "
                + "l'integration des mouvements bancaires, ou le rapprochement des comptes de "
                + "classe 5 du plan comptable."),

            new KpiDefinition(
                KpiCodes.CommittedOutflow7D,
                "Decaissements engages a 7 jours",
                "Sorties 7 j",
                KpiCategory.Finance,
                "Ce qu'il faudra payer dans les sept prochains jours au titre des ordres de "
                + "paiement deja approuves.",
                "Somme des ordres approuves non regles dont l'echeance tombe dans les 7 jours",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Treasury,
                "Ordres de paiement au statut Approuve, non encore regles, dont la date "
                + "d'echeance tombe entre aujourd'hui et J+7. C'est un engagement de sortie, pas "
                + "une prevision complete : les entrees attendues n'y figurent pas, les factures "
                + "clients de Raqmi System ne portant pas de date d'echeance.",
                [PermissionCatalog.TreasuryRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.CommittedOutflow30D,
                "Decaissements engages a 30 jours",
                "Sorties 30 j",
                KpiCategory.Finance,
                "Meme lecture que l'horizon a 7 jours, sur un mois.",
                "Somme des ordres approuves non regles dont l'echeance tombe dans les 30 jours",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Treasury,
                "Ordres de paiement approuves non regles, echeance entre aujourd'hui et J+30.",
                [PermissionCatalog.TreasuryRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.CommittedOutflow90D,
                "Decaissements engages a 90 jours",
                "Sorties 90 j",
                KpiCategory.Finance,
                "Meme lecture que les horizons precedents, sur un trimestre.",
                "Somme des ordres approuves non regles dont l'echeance tombe dans les 90 jours",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Treasury,
                "Ordres de paiement approuves non regles, echeance entre aujourd'hui et J+90.",
                [PermissionCatalog.TreasuryRead],
                KpiScopeLevel.GroupOnly),

            new KpiDefinition(
                KpiCodes.Dso,
                "Delai moyen de reglement client (DSO)",
                "DSO",
                KpiCategory.Finance,
                "Nombre de jours de chiffre d'affaires immobilises en creances clients. Chaque "
                + "jour gagne est de la tresorerie rendue disponible.",
                "Creances clients / Chiffre d'affaires a credit x Jours de la periode",
                KpiUnit.Days,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Receivables,
                "Creances : factures au statut Emise, datees au plus tard a la fin de periode. "
                + "Chiffre d'affaires a credit : factures emises dans la periode. Deux methodes "
                + "sont proposees et le choix est explicite dans la reponse : la formule "
                + "classique ci-dessus, ou l'epuisement des creances (count-back), qui remonte "
                + "les factures de la plus recente a la plus ancienne jusqu'a epuiser l'encours "
                + "et rend un delai plus juste quand l'activite est saisonniere.",
                [PermissionCatalog.ReceivablesRead]),

            new KpiDefinition(
                KpiCodes.ReceivablesTotal,
                "Creances clients",
                "Creances",
                KpiCategory.Finance,
                "Encours client restant du a la fin de la periode.",
                "Somme TTC des factures emises non reglees",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Receivables,
                "Factures au statut Emise datees au plus tard a la fin de periode. Une facture "
                + "reglee ou annulee sort de l'encours. Les creances contentieuses, provisionnees "
                + "ou douteuses ne sont pas distinguees : Raqmi System ne porte pas encore de "
                + "qualification de risque sur une facture.",
                [PermissionCatalog.ReceivablesRead]),

            new KpiDefinition(
                KpiCodes.ReceivablesOver90,
                "Creances de plus de 90 jours",
                "Creances 90 j+",
                KpiCategory.Finance,
                "La part de l'encours dont le recouvrement devient reellement incertain.",
                "Somme TTC des factures emises agees de plus de 90 jours",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Receivables,
                "Tranche d'anciennete calculee par le module Creances, l'age courant depuis la "
                + "date de facture : le systeme ne porte pas de date d'echeance.",
                [PermissionCatalog.ReceivablesRead]),

            new KpiDefinition(
                KpiCodes.ReceivablesOverdueRate,
                "Part des creances de plus de 90 jours",
                "Part 90 j+",
                KpiCategory.Finance,
                "Poids relatif des creances les plus anciennes dans l'encours total : la mesure "
                + "de degradation du poste client.",
                "Creances de plus de 90 jours / Creances totales x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Receivables,
                "Meme base d'anciennete que la balance agee du module Creances.",
                [PermissionCatalog.ReceivablesRead]),

            // ==================================================================================
            //                              RESTAURATION ET BOISSONS
            // ==================================================================================
            new KpiDefinition(
                KpiCodes.FoodCostAmount,
                "Cout matiere denrees",
                "Cout denrees",
                KpiCategory.FoodBeverage,
                "Valeur des denrees reellement sorties du stock sur la periode.",
                "Somme (quantite consommee x cout unitaire) des articles alimentaires",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Inventory,
                "Mouvements de stock de nature Consommation portant sur des articles de "
                + "categorie Alimentaire, valorises au cout unitaire porte par le mouvement. Un "
                + "mouvement sans cout unitaire est signale comme donnee manquante plutot que "
                + "compte pour zero.",
                [PermissionCatalog.InventoryRead]),

            new KpiDefinition(
                KpiCodes.FoodCostRate,
                "Ratio de cout matiere denrees (food cost)",
                "Food cost",
                KpiCategory.FoodBeverage,
                "Part du chiffre d'affaires restauration absorbee par les denrees. L'indicateur "
                + "de gestion le plus surveille d'une exploitation de restauration.",
                "Cout des denrees consommees / Chiffre d'affaires restauration x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly | KpiRefreshTrigger.OnMonthlyClosing,
                KpiSourceModule.Inventory,
                "Consommations valorisees d'articles alimentaires, rapportees a la colonne "
                + "restauration des recettes journalieres validees. Le rattachement a une unite "
                + "passe par le magasin du mouvement.",
                [PermissionCatalog.InventoryRead, PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.BeverageCostAmount,
                "Cout matiere boissons",
                "Cout boissons",
                KpiCategory.FoodBeverage,
                "Valeur des boissons reellement sorties du stock sur la periode.",
                "Somme (quantite consommee x cout unitaire) des articles boissons",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Inventory,
                "Mouvements de nature Consommation sur articles de categorie Boisson, valorises "
                + "au cout unitaire du mouvement. La ventilation alcools / soft / eaux / cafe "
                + "n'est pas calculee : la categorie d'article de Raqmi System s'arrete a "
                + "\"Boisson\".",
                [PermissionCatalog.InventoryRead]),

            new KpiDefinition(
                KpiCodes.BeverageCostRate,
                "Ratio de cout matiere boissons (beverage cost)",
                "Beverage cost",
                KpiCategory.FoodBeverage,
                "Part du chiffre d'affaires boissons absorbee par les achats de boissons.",
                "Cout des boissons consommees / Chiffre d'affaires boissons x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly | KpiRefreshTrigger.OnMonthlyClosing,
                KpiSourceModule.Inventory,
                "Consommations valorisees d'articles boissons, rapportees a la colonne boissons "
                + "des recettes journalieres validees.",
                [PermissionCatalog.InventoryRead, PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.TotalCostOfSalesRate,
                "Ratio de cout matiere global",
                "Cout matiere",
                KpiCategory.FoodBeverage,
                "Cout matiere denrees et boissons rapporte au chiffre d'affaires restauration et "
                + "boissons reunis.",
                "(Cout denrees + Cout boissons) / (CA restauration + CA boissons) x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Inventory,
                "Somme des deux couts matiere, rapportee a la somme des deux colonnes de "
                + "recettes correspondantes.",
                [PermissionCatalog.InventoryRead, PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.TheoreticalFoodCostRate,
                "Food cost theorique",
                "Food cost th.",
                KpiCategory.FoodBeverage,
                "Ce que le cout matiere AURAIT du etre au vu des fiches techniques et des "
                + "quantites vendues. Confronte au reel, il chiffre les pertes, les surdosages "
                + "et les vols.",
                "Somme (quantite vendue x cout matiere de la fiche technique) / CA restauration x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : les fiches techniques et leur cout matiere existent, les "
                + "QUANTITES VENDUES par plat n'existent pas.",
                [PermissionCatalog.KitchenRead, PermissionCatalog.RevenueRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                PosSource),

            new KpiDefinition(
                KpiCodes.FoodCostVariance,
                "Ecart food cost theorique / reel",
                "Ecart food cost",
                KpiCategory.FoodBeverage,
                "Difference entre le cout matiere theorique et le cout matiere reel : la mesure "
                + "directe des pertes de production.",
                "Cout matiere reel - Cout matiere theorique",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable tant que le cout matiere theorique ne l'est pas.",
                [PermissionCatalog.KitchenRead, PermissionCatalog.InventoryRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                PosSource),

            new KpiDefinition(
                KpiCodes.AverageCheck,
                "Ticket moyen",
                "Ticket moyen",
                KpiCategory.FoodBeverage,
                "Depense moyenne par ticket encaisse au point de vente.",
                "Chiffre d'affaires du point de vente / Nombre de tickets",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.None,
                "Non calculable : aucun ticket n'est enregistre dans Raqmi System.",
                [PermissionCatalog.RevenueRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                PosSource),

            new KpiDefinition(
                KpiCodes.RevPash,
                "Revenu par siege et par heure (RevPASH)",
                "RevPASH",
                KpiCategory.FoodBeverage,
                "Rendement d'une salle de restaurant : ce que rapporte chaque siege pour chaque "
                + "heure d'ouverture.",
                "Chiffre d'affaires du point de vente / (Sieges disponibles x Heures d'ouverture)",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.None,
                "Non calculable : ni les points de vente, ni leur nombre de sieges, ni leurs "
                + "plages d'ouverture ne sont declares.",
                [PermissionCatalog.RevenueRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                PosSource + " Il faudrait en outre un referentiel de points de vente portant "
                + "le nombre de sieges et les plages d'ouverture."),

            new KpiDefinition(
                KpiCodes.CostPerCover,
                "Cout matiere par couvert",
                "Cout / couvert",
                KpiCategory.FoodBeverage,
                "Cout des denrees rapporte au nombre de couverts servis.",
                "Cout matiere / Nombre de couverts",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.None,
                "Non calculable : le nombre de couverts n'est enregistre nulle part.",
                [PermissionCatalog.InventoryRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                PosSource),

            new KpiDefinition(
                KpiCodes.WasteCost,
                "Cout du gaspillage",
                "Gaspillage",
                KpiCategory.FoodBeverage,
                "Valeur des marchandises perdues : surproduction, peremption, erreurs de "
                + "production, retours clients, casse, rupture de conservation.",
                "Somme (quantite perdue x cout unitaire)",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : le registre des mouvements ne distingue pas une perte d'une "
                + "consommation normale.",
                [PermissionCatalog.InventoryRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                WasteSource),

            new KpiDefinition(
                KpiCodes.WasteRate,
                "Part du gaspillage dans le cout matiere",
                "Part gaspillage",
                KpiCategory.FoodBeverage,
                "Poids du gaspillage dans le cout matiere total.",
                "Cout du gaspillage / Cout matiere total x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable tant que le cout du gaspillage ne l'est pas.",
                [PermissionCatalog.InventoryRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                WasteSource),

            // ==================================================================================
            //                               RESSOURCES HUMAINES
            // ==================================================================================
            new KpiDefinition(
                KpiCodes.PayrollCost,
                "Masse salariale chargee",
                "Masse salariale",
                KpiCategory.HumanResources,
                "Cout complet du personnel pour l'employeur sur la periode : brut, cotisations "
                + "patronales et taxes sur salaires.",
                "Somme des couts employeur des bulletins de paie",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly | KpiRefreshTrigger.OnMonthlyClosing,
                KpiSourceModule.HumanResources,
                "Cout employeur des bulletins au statut Valide des periodes de paie que la "
                + "periode d'analyse touche. Les bulletins en brouillon sont exclus : une "
                + "pre-paie non validee peut encore etre recalculee de fond en comble. Le "
                + "rattachement a une unite passe par l'affectation du collaborateur.",
                [PermissionCatalog.HrRead]),

            new KpiDefinition(
                KpiCodes.PayrollToRevenueRate,
                "Masse salariale sur chiffre d'affaires",
                "Masse sal. / CA",
                KpiCategory.HumanResources,
                "Part du chiffre d'affaires absorbee par le personnel. Avec le cout matiere, "
                + "c'est la moitie du resultat d'une exploitation hoteliere.",
                "Masse salariale chargee / Chiffre d'affaires x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly | KpiRefreshTrigger.OnMonthlyClosing,
                KpiSourceModule.HumanResources,
                "Couts employeur des bulletins valides, rapportes aux recettes journalieres "
                + "validees de la meme periode.",
                [PermissionCatalog.HrRead, PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.PayrollCostPerEmployee,
                "Cout salarial par collaborateur",
                "Cout / salarie",
                KpiCategory.HumanResources,
                "Cout employeur moyen par personne payee sur la periode.",
                "Masse salariale chargee / Nombre de bulletins",
                KpiUnit.Currency,
                KpiPolarity.Neutral,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.HumanResources,
                "Bulletins valides de la periode. Le denominateur compte les bulletins, pas les "
                + "personnes : un collaborateur paye deux mois compte deux fois sur une periode "
                + "de deux mois, ce qui est exactement ce qu'il faut pour un cout mensuel moyen.",
                [PermissionCatalog.HrRead]),

            new KpiDefinition(
                KpiCodes.PayrollCostPerAvailableRoom,
                "Cout salarial par chambre disponible",
                "Cout / dispo",
                KpiCategory.HumanResources,
                "Charge de personnel ramenee a la capacite : elle dit si l'effectif est "
                + "dimensionne pour l'hotel, independamment du remplissage.",
                "Masse salariale chargee / Nuitees disponibles",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.HumanResources,
                "Masse salariale chargee de la periode divisee par les nuitees disponibles du "
                + "module hebergement.",
                [PermissionCatalog.HrRead, PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.PayrollCostPerOccupiedRoom,
                "Cout salarial par chambre occupee",
                "Cout / occupee",
                KpiCategory.HumanResources,
                "Charge de personnel ramenee a l'activite reelle. Compare a l'ADR, il dit si "
                + "chaque chambre vendue paie le personnel qu'elle mobilise.",
                "Masse salariale chargee / Nuitees occupees",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.HumanResources,
                "Masse salariale chargee de la periode divisee par les nuitees occupees.",
                [PermissionCatalog.HrRead, PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.AbsenteeismRate,
                "Taux d'absenteisme",
                "Absenteisme",
                KpiCategory.HumanResources,
                "Part du temps de presence contractuel perdue en absences.",
                "Jours d'absence approuves / Jours de presence contractuelle x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.HumanResources,
                "Numerateur : jours d'absence au statut Approuve chevauchant la periode, "
                + "toutes natures confondues et ventiles par nature dans le detail. "
                + "Denominateur : jours calendaires ou un contrat actif couvre le collaborateur "
                + "dans la periode. Le calcul est en JOURS CALENDAIRES et non en heures "
                + "travaillees : Raqmi System ne porte pas de calendrier de travail ni de "
                + "planning d'equipes, et convertir en heures supposerait un rythme que "
                + "personne n'a declare.",
                [PermissionCatalog.HrRead]),

            new KpiDefinition(
                KpiCodes.TurnoverRate,
                "Taux de rotation du personnel",
                "Turnover",
                KpiCategory.HumanResources,
                "Part de l'effectif renouvelee sur la periode. En hotellerie, un turnover eleve "
                + "coute d'abord en qualite de service.",
                "Nombre de departs / Effectif moyen x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.HumanResources,
                "Departs : collaborateurs dont la date de fin tombe dans la periode. Effectif "
                + "moyen : moyenne des effectifs presents au debut et a la fin de la periode. "
                + "La ventilation par motif (demission, retraite, licenciement, fin de contrat, "
                + "mutation) n'est PAS produite : le motif de rupture est un texte libre porte "
                + "par le contrat, non un motif code exploitable statistiquement.",
                [PermissionCatalog.HrRead]),

            new KpiDefinition(
                KpiCodes.HeadcountAverage,
                "Effectif moyen",
                "Effectif",
                KpiCategory.HumanResources,
                "Nombre moyen de collaborateurs presents sur la periode.",
                "(Effectif au debut + Effectif a la fin) / 2",
                KpiUnit.Count,
                KpiPolarity.Neutral,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.HumanResources,
                "Collaborateurs embauches au plus tard a la date consideree et non encore "
                + "partis a cette date.",
                [PermissionCatalog.HrRead]),

            new KpiDefinition(
                KpiCodes.OvertimeRate,
                "Part des heures supplementaires",
                "Heures sup.",
                KpiCategory.HumanResources,
                "Poids des heures supplementaires dans le temps travaille paye. Un taux durable "
                + "revele un sous-effectif structurel plutot qu'un pic d'activite.",
                "Heures supplementaires / Heures travaillees x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.HumanResources,
                "Heures supplementaires et heures travaillees portees par les bulletins valides "
                + "de la periode.",
                [PermissionCatalog.HrRead]),

            new KpiDefinition(
                KpiCodes.RevenuePerEmployee,
                "Chiffre d'affaires par collaborateur",
                "CA / salarie",
                KpiCategory.HumanResources,
                "Productivite globale : ce que produit en moyenne chaque collaborateur.",
                "Chiffre d'affaires / Effectif moyen",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.HumanResources,
                "Recettes journalieres validees de la periode rapportees a l'effectif moyen.",
                [PermissionCatalog.HrRead, PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.RevenuePerWorkedHour,
                "Chiffre d'affaires par heure travaillee",
                "CA / heure",
                KpiCategory.HumanResources,
                "Productivite horaire, insensible aux temps partiels et aux effectifs "
                + "saisonniers - ce que l'effectif moyen ne sait pas dire.",
                "Chiffre d'affaires / Heures travaillees",
                KpiUnit.Currency,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.HumanResources,
                "Pointages au statut Valide de la periode : les heures brutes non controlees ne "
                + "sont jamais comptees, exactement comme la pre-paie les ignore.",
                [PermissionCatalog.HrRead, PermissionCatalog.RevenueRead]),

            new KpiDefinition(
                KpiCodes.RoomsCleanedPerAttendant,
                "Chambres nettoyees par femme de chambre",
                "Chambres / agent",
                KpiCategory.HumanResources,
                "Productivite de l'etage : nombre de chambres traitees par agent affecte et par "
                + "jour de service.",
                "Chambres nettoyees / (Agents affectes x Jours de service)",
                KpiUnit.Ratio,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Housekeeping,
                "Taches de nettoyage de la periode ayant atteint l'etat Nettoyee ou Controlee, "
                + "rapportees au nombre de couples (agent affecte, jour de service) distincts. "
                + "Une tache sans agent affecte compte au numerateur et signale une donnee "
                + "partielle.",
                [PermissionCatalog.HousekeepingRead]),

            new KpiDefinition(
                KpiCodes.CoversPerWaiter,
                "Couverts par serveur",
                "Couverts / serveur",
                KpiCategory.HumanResources,
                "Productivite de la salle.",
                "Nombre de couverts / Nombre de serveurs en service",
                KpiUnit.Ratio,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.None,
                "Non calculable : ni les couverts, ni l'affectation des serveurs par service ne "
                + "sont enregistres.",
                [PermissionCatalog.HrRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                PosSource + " Il faudrait en outre un planning d'affectation du personnel de "
                + "salle par service."),

            new KpiDefinition(
                KpiCodes.InterventionsPerTechnician,
                "Interventions par technicien",
                "Interventions / tech.",
                KpiCategory.HumanResources,
                "Productivite de la maintenance.",
                "Nombre d'interventions / Nombre de techniciens",
                KpiUnit.Ratio,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : aucune intervention de maintenance n'est enregistree.",
                [PermissionCatalog.HrRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                GmaoSource),

            // ==================================================================================
            //                                   MAINTENANCE
            // ==================================================================================
            new KpiDefinition(
                KpiCodes.Mttr,
                "Temps moyen de reparation (MTTR)",
                "MTTR",
                KpiCategory.Maintenance,
                "Duree moyenne d'immobilisation d'un equipement en panne, de la declaration a la "
                + "remise en service.",
                "Temps total de reparation / Nombre d'interventions correctives",
                KpiUnit.Hours,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable en l'etat du produit.",
                [PermissionCatalog.MaintenanceRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                GmaoSource),

            new KpiDefinition(
                KpiCodes.Mtbf,
                "Temps moyen entre pannes (MTBF)",
                "MTBF",
                KpiCategory.Maintenance,
                "Duree moyenne de bon fonctionnement d'un equipement entre deux pannes.",
                "Temps de fonctionnement / Nombre de pannes",
                KpiUnit.Hours,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable en l'etat du produit.",
                [PermissionCatalog.MaintenanceRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                GmaoSource),

            new KpiDefinition(
                KpiCodes.PreventiveCompletionRate,
                "Taux de realisation du preventif",
                "Preventif",
                KpiCategory.Maintenance,
                "Part du programme de maintenance preventive reellement executee. C'est le seul "
                + "indicateur qui anticipe les pannes au lieu de les constater.",
                "Interventions preventives realisees / Interventions preventives planifiees x 100",
                KpiUnit.Percentage,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable en l'etat du produit.",
                [PermissionCatalog.MaintenanceRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                GmaoSource),

            new KpiDefinition(
                KpiCodes.MaintenanceCostPerEquipment,
                "Cout de maintenance par equipement",
                "Cout / equipement",
                KpiCategory.Maintenance,
                "Cout complet d'entretien d'un equipement : pieces, main-d'oeuvre, prestataires, "
                + "contrats et consommables.",
                "Cout total de maintenance / Nombre d'equipements",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable en l'etat du produit.",
                [PermissionCatalog.MaintenanceRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                GmaoSource),

            new KpiDefinition(
                KpiCodes.MaintenanceCostToAssetValue,
                "Cout de maintenance sur valeur de l'equipement",
                "Cout / valeur",
                KpiCategory.Maintenance,
                "Signal de renouvellement : au-dela d'un certain rapport, entretenir coute plus "
                + "cher que remplacer.",
                "Cout de maintenance cumule / Valeur de l'equipement x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable en l'etat du produit.",
                [PermissionCatalog.MaintenanceRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                GmaoSource),

            // ==================================================================================
            //                               EXPERIENCE CLIENT
            // ==================================================================================
            new KpiDefinition(
                KpiCodes.GuestSatisfactionScore,
                "Score de satisfaction client",
                "Satisfaction",
                KpiCategory.GuestExperience,
                "Note moyenne donnee par les clients sur la periode, sur une echelle de 0 a 10.",
                "Somme des notes / Nombre de reponses",
                KpiUnit.Score,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Average,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Crm,
                "Enquetes de satisfaction du module CRM dont la date tombe dans la periode.",
                [PermissionCatalog.CrmRead]),

            new KpiDefinition(
                KpiCodes.Nps,
                "Net Promoter Score (NPS)",
                "NPS",
                KpiCategory.GuestExperience,
                "Difference entre la part de promoteurs et la part de detracteurs. Il varie de "
                + "-100 a +100 et se lit en points, jamais en pourcentage.",
                "(% promoteurs - % detracteurs)",
                KpiUnit.Score,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.Average,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Crm,
                "Meme base d'enquetes, classees selon les bornes de la methode NPS "
                + "(0-6 detracteur, 7-8 passif, 9-10 promoteur) portees par le module CRM et "
                + "jamais redefinies ici.",
                [PermissionCatalog.CrmRead]),

            new KpiDefinition(
                KpiCodes.RepeatGuestRate,
                "Taux de clients recurrents",
                "Clients fideles",
                KpiCategory.GuestExperience,
                "Part des sejours pris par un client deja venu. Un client qui revient coute "
                + "beaucoup moins cher a servir qu'un client a conquerir.",
                "Sejours de clients deja venus / Sejours totaux x 100",
                KpiUnit.Percentage,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Lodging,
                "Sejours bloquants arrivant dans la periode dont le client porte au moins un "
                + "sejour anterieur a la periode, dans n'importe quelle unite du groupe.",
                [PermissionCatalog.LodgingRead]),

            new KpiDefinition(
                KpiCodes.ComplaintRate,
                "Taux de reclamation",
                "Reclamations",
                KpiCategory.GuestExperience,
                "Part des sejours ayant donne lieu a une reclamation.",
                "Nombre de reclamations / Nombre de sejours x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : le journal des contacts du CRM enregistre le canal et le sens "
                + "d'un echange, pas sa nature.",
                [PermissionCatalog.CrmRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                "Registre des reclamations absent : il faudrait une reclamation typee, avec sa "
                + "gravite, son motif, son traitement et sa cloture."),

            new KpiDefinition(
                KpiCodes.DirectBookingRatio,
                "Part des reservations directes",
                "Direct",
                KpiCategory.GuestExperience,
                "Part des reservations prises sans intermediaire. Chaque point gagne est une "
                + "commission de distribution economisee.",
                "Reservations directes / Reservations totales x 100",
                KpiUnit.Percentage,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : la reservation ne porte pas son canal d'origine.",
                [PermissionCatalog.LodgingRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                ChannelSource),

            new KpiDefinition(
                KpiCodes.ChannelCost,
                "Cout de distribution",
                "Cout canal",
                KpiCategory.GuestExperience,
                "Commissions versees aux canaux de distribution, rapportees au revenu qu'ils "
                + "apportent.",
                "Commissions versees / Revenu apporte par le canal x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : ni canal ni commission ne sont portes par la reservation.",
                [PermissionCatalog.LodgingRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                ChannelSource),

            new KpiDefinition(
                KpiCodes.ConversionRate,
                "Taux de transformation",
                "Conversion",
                KpiCategory.GuestExperience,
                "Part des demandes qui deviennent des reservations confirmees.",
                "Reservations confirmees / Demandes recues x 100",
                KpiUnit.Percentage,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : Raqmi System n'enregistre que les reservations prises, jamais "
                + "les demandes non abouties.",
                [PermissionCatalog.LodgingRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                "Suivi des demandes absent : il faudrait enregistrer les demandes de "
                + "reservation, y compris celles qui n'aboutissent pas, avec leur motif de perte."),

            // ==================================================================================
            //                               ACHATS ET STOCKS
            // ==================================================================================
            new KpiDefinition(
                KpiCodes.InventoryTurnover,
                "Rotation des stocks",
                "Rotation",
                KpiCategory.SupplyChain,
                "Nombre de fois ou le stock se renouvelle sur la periode. Une rotation faible "
                + "immobilise de la tresorerie et vieillit la marchandise.",
                "Consommations valorisees / Stock moyen valorise",
                KpiUnit.Ratio,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.Inventory,
                "Consommations valorisees de la periode divisees par la moyenne du stock "
                + "valorise au debut et a la fin, reconstitue a partir du registre des "
                + "mouvements.",
                [PermissionCatalog.InventoryRead]),

            new KpiDefinition(
                KpiCodes.StockOutRate,
                "Taux de rupture de stock",
                "Ruptures",
                KpiCategory.SupplyChain,
                "Part des articles actifs dont le stock est tombe a zero ou en dessous a la fin "
                + "de la periode.",
                "Articles en rupture / Articles actifs x 100",
                KpiUnit.Percentage,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
                KpiSourceModule.Inventory,
                "Stock de fin de periode reconstitue par cumul des mouvements signes, par "
                + "article et par magasin. C'est une photo de fin de periode, pas une mesure du "
                + "temps passe en rupture : le registre ne conserve pas d'historique de "
                + "disponibilite.",
                [PermissionCatalog.InventoryRead]),

            new KpiDefinition(
                KpiCodes.PurchasePriceVariance,
                "Ecart de prix d'achat",
                "Ecart prix",
                KpiCategory.SupplyChain,
                "Ecart entre le prix reellement paye et le prix standard de reference.",
                "Somme (prix reel - prix standard) x quantite recue",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.Sum,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : aucun prix standard n'est defini par article.",
                [PermissionCatalog.PurchasingRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                "Prix standard absent : il faudrait un prix de reference par article et par "
                + "periode (tarif fournisseur negocie ou cout standard budgete)."),

            new KpiDefinition(
                KpiCodes.SupplierOnTimeDeliveryRate,
                "Taux de livraison a l'heure",
                "Ponctualite fourn.",
                KpiCategory.SupplyChain,
                "Part des commandes livrees a la date promise.",
                "Receptions dans les delais / Receptions totales x 100",
                KpiUnit.Percentage,
                KpiPolarity.HigherIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : le bon de commande ne porte pas de date de livraison attendue.",
                [PermissionCatalog.PurchasingRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                "Date de livraison attendue absente du bon de commande, et date de reception "
                + "non conservee ligne a ligne."),

            new KpiDefinition(
                KpiCodes.HousekeepingCostPerRoom,
                "Cout housekeeping par chambre",
                "Cout etage",
                KpiCategory.SupplyChain,
                "Cout complet du nettoyage d'une chambre.",
                "Cout du departement etages / Nombre de chambres nettoyees",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : le referentiel des departements RH est une liste de codes "
                + "libres, rien n'y designe le departement des etages, et aucun cout de "
                + "blanchisserie ou de produits d'entretien n'est rattache a une chambre.",
                [PermissionCatalog.HrRead, PermissionCatalog.HousekeepingRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                "Rattachement analytique absent : il faudrait typer les departements RH par "
                + "nature hoteliere (etages, restauration, reception, technique) et rattacher "
                + "les consommations d'entretien a un departement."),

            new KpiDefinition(
                KpiCodes.EnergyCostPerOccupiedRoom,
                "Cout energie par chambre occupee",
                "Energie / occupee",
                KpiCategory.SupplyChain,
                "Consommation energetique ramenee a l'activite reelle.",
                "Cout de l'energie / Nuitees occupees",
                KpiUnit.Currency,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : aucun releve de fluide n'est enregistre.",
                [PermissionCatalog.AccountingRead, PermissionCatalog.LodgingRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                UtilitySource),

            new KpiDefinition(
                KpiCodes.WaterPerGuestNight,
                "Consommation d'eau par nuitee client",
                "Eau / nuitee",
                KpiCategory.SupplyChain,
                "Consommation d'eau ramenee au nombre de personnes hebergees.",
                "Volume d'eau consomme / Nuitees clients",
                KpiUnit.Ratio,
                KpiPolarity.LowerIsBetter,
                KpiAggregation.RatioOfSums,
                KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
                KpiSourceModule.None,
                "Non calculable : aucun releve de fluide n'est enregistre.",
                [PermissionCatalog.LodgingRead],
                KpiScopeLevel.UnitAndGroup,
                KpiAvailability.AwaitingSource,
                UtilitySource)
        ];
    }
}
