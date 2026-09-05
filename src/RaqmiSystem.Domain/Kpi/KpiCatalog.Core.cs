using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;

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
///
/// PAQUETS. Chaque definition porte le <see cref="ModulePack"/> qui la justifie. Le SOCLE
/// (ce fichier : finance, ressources humaines, achats et stocks, maintenance, experience
/// client) parle a toute entreprise ; le PACK HOTELIER (<c>KpiCatalog.Hospitality.cs</c> :
/// hebergement, restauration, etages, distribution) n'a de sens que la ou l'on vend des
/// nuitees et des couverts. <see cref="All"/> reste la liste complete, dans l'ordre historique
/// des ecrans ; <see cref="ForPacks"/> ne garde que ce qu'une installation a active.
/// </summary>
public static partial class KpiCatalog
{
    private const string GmaoSource =
        "Module GMAO absent : il faudrait un referentiel d'equipements (mise en service, valeur) "
        + "et des ordres de travail dates (declaration, prise en charge, remise en service, "
        + "nature preventive ou corrective, temps passe, pieces consommees). Le module "
        + "\"Maintenance\" existant de Raqmi System couvre les sauvegardes de la base, pas "
        + "l'entretien des equipements.";

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
    /// Les indicateurs des paquets actifs d'une installation, dans l'ordre de <see cref="All"/>.
    /// Un indicateur dont le paquet n'est pas actif n'est pas "non disponible" : il n'existe pas
    /// pour cette entreprise, et aucun ecran ne doit le lister.
    /// </summary>
    public static IReadOnlyList<KpiDefinition> ForPacks(IReadOnlySet<ModulePack> activePacks)
    {
        ArgumentNullException.ThrowIfNull(activePacks);

        return All.Where(definition => activePacks.Contains(definition.Pack)).ToArray();
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
        // L'ordre historique des ecrans, section par section : la direction lit d'abord ce
        // qu'elle a produit, puis ce que cela a rapporte, puis ce que cela coute. Les sections
        // du pack hotelier et les groupes epissees dans celles du socle sont definis dans
        // KpiCatalog.Hospitality.cs ; leur place dans la liste n'a pas bouge.
        return
        [
            .. AccommodationSection(),
            .. FinanceSection(),
            .. FoodBeverageSection(),
            .. HumanResourcesSection(),
            .. MaintenanceSection(),
            .. GuestExperienceSection(),
            .. SupplyChainSection()
        ];
    }

    // ======================================================================================
    //                                       FINANCE
    // ======================================================================================
    // Les lignes de recettes du modele hotelier (hebergement, restauration, boissons) sont
    // epissees a leur place historique, entre le total et la ligne "autres".
    private static KpiDefinition[] FinanceSection() =>
    [
        new KpiDefinition(
            KpiCodes.RevenueTotal,
            "Chiffre d'affaires",
            "CA",
            KpiCategory.Finance,
            ModulePack.Finance,
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

        .. HospitalityRevenueLines(),

        new KpiDefinition(
            KpiCodes.RevenueOther,
            "Chiffre d'affaires autres prestations",
            "CA autres",
            KpiCategory.Finance,
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
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
            ModulePack.Finance,
            "Poids relatif des creances les plus anciennes dans l'encours total : la mesure "
            + "de degradation du poste client.",
            "Creances de plus de 90 jours / Creances totales x 100",
            KpiUnit.Percentage,
            KpiPolarity.LowerIsBetter,
            KpiAggregation.RatioOfSums,
            KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
            KpiSourceModule.Receivables,
            "Meme base d'anciennete que la balance agee du module Creances.",
            [PermissionCatalog.ReceivablesRead])
    ];

    // ======================================================================================
    //                                 RESSOURCES HUMAINES
    // ======================================================================================
    private static KpiDefinition[] HumanResourcesSection() =>
    [
        new KpiDefinition(
            KpiCodes.PayrollCost,
            "Masse salariale chargee",
            "Masse salariale",
            KpiCategory.HumanResources,
            ModulePack.HumanResources,
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
            ModulePack.HumanResources,
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
            ModulePack.HumanResources,
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

        .. HospitalityPayrollPerRoom(),

        new KpiDefinition(
            KpiCodes.AbsenteeismRate,
            "Taux d'absenteisme",
            "Absenteisme",
            KpiCategory.HumanResources,
            ModulePack.HumanResources,
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
            ModulePack.HumanResources,
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
            ModulePack.HumanResources,
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
            ModulePack.HumanResources,
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
            ModulePack.HumanResources,
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
            ModulePack.HumanResources,
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

        .. HospitalityProductivity(),

        new KpiDefinition(
            KpiCodes.InterventionsPerTechnician,
            "Interventions par technicien",
            "Interventions / tech.",
            KpiCategory.HumanResources,
            ModulePack.Core,
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
            GmaoSource)
    ];

    // ======================================================================================
    //                                     MAINTENANCE
    // ======================================================================================
    private static KpiDefinition[] MaintenanceSection() =>
    [
        new KpiDefinition(
            KpiCodes.Mttr,
            "Temps moyen de reparation (MTTR)",
            "MTTR",
            KpiCategory.Maintenance,
            ModulePack.Core,
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
            ModulePack.Core,
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
            ModulePack.Core,
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
            ModulePack.Core,
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
            ModulePack.Core,
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
            GmaoSource)
    ];

    // ======================================================================================
    //                                 EXPERIENCE CLIENT
    // ======================================================================================
    private static KpiDefinition[] GuestExperienceSection() =>
    [
        new KpiDefinition(
            KpiCodes.GuestSatisfactionScore,
            "Score de satisfaction client",
            "Satisfaction",
            KpiCategory.GuestExperience,
            ModulePack.Crm,
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
            ModulePack.Crm,
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

        .. HospitalityRepeatGuests(),

        new KpiDefinition(
            KpiCodes.ComplaintRate,
            "Taux de reclamation",
            "Reclamations",
            KpiCategory.GuestExperience,
            ModulePack.Crm,
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

        .. HospitalityDistribution()
    ];

    // ======================================================================================
    //                                   ACHATS ET STOCKS
    // ======================================================================================
    private static KpiDefinition[] SupplyChainSection() =>
    [
        new KpiDefinition(
            KpiCodes.InventoryTurnover,
            "Rotation des stocks",
            "Rotation",
            KpiCategory.SupplyChain,
            ModulePack.Inventory,
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
            ModulePack.Inventory,
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
            ModulePack.Purchasing,
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
            ModulePack.Purchasing,
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

        .. HospitalityConsumption()
    ];
}
