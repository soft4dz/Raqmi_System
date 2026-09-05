using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Le pack HOTELIER du catalogue d'indicateurs : ce qui n'a de sens que la ou l'on vend des
/// nuitees et des couverts. Hebergement (occupation, ADR, RevPAR, sejours), restauration et
/// boissons (cout matiere, ticket moyen, pertes), etages et distribution, plus les ratios du
/// socle qui se rapportent a la chambre (masse salariale par chambre, energie par chambre
/// occupee). Un commerce ou un cabinet n'active jamais ces paquets et ne voit aucune de ces
/// fiches - pas meme "non disponible".
///
/// La partie SOCLE et la liste ordonnee <see cref="All"/> sont dans <c>KpiCatalog.Core.cs</c>.
/// Les groupes de la seconde moitie de ce fichier sont epissees dans les sections du socle a
/// leur place historique : scinder le catalogue n'a deplace aucune fiche a l'ecran.
/// </summary>
public static partial class KpiCatalog
{
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

    // ======================================================================================
    //                                     HEBERGEMENT
    // ======================================================================================
    private static KpiDefinition[] AccommodationSection() =>
    [
        new KpiDefinition(
            KpiCodes.OccupancyRate,
            "Taux d'occupation",
            "Occupation",
            KpiCategory.Accommodation,
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            KpiScopeLevel.GroupOnly)
    ];

    // ======================================================================================
    //                                RESTAURATION ET BOISSONS
    // ======================================================================================
    private static KpiDefinition[] FoodBeverageSection() =>
    [
        new KpiDefinition(
            KpiCodes.FoodCostAmount,
            "Cout matiere denrees",
            "Cout denrees",
            KpiCategory.FoodBeverage,
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
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
            WasteSource)
    ];

    // ======================================================================================
    //                  GROUPES EPISSES DANS LES SECTIONS DU SOCLE (ordre historique)
    // ======================================================================================
    /// <summary>Les lignes de recettes du modele hotelier, dans la section Finance.</summary>
    private static KpiDefinition[] HospitalityRevenueLines() =>
    [
        new KpiDefinition(
            KpiCodes.RevenueAccommodation,
            "Chiffre d'affaires hebergement",
            "CA hebergement",
            KpiCategory.Finance,
            ModulePack.Hospitality,
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
            ModulePack.FoodBeverage,
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
            ModulePack.FoodBeverage,
            "Part boissons du chiffre d'affaires : le denominateur du beverage cost.",
            "Colonne boissons des recettes journalieres validees",
            KpiUnit.Currency,
            KpiPolarity.HigherIsBetter,
            KpiAggregation.Sum,
            KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Daily,
            KpiSourceModule.DailyRevenue,
            "Recettes journalieres validees, colonne boissons.",
            [PermissionCatalog.RevenueRead])
    ];

    /// <summary>La masse salariale rapportee a la chambre, dans la section Ressources humaines.</summary>
    private static KpiDefinition[] HospitalityPayrollPerRoom() =>
    [
        new KpiDefinition(
            KpiCodes.PayrollCostPerAvailableRoom,
            "Cout salarial par chambre disponible",
            "Cout / dispo",
            KpiCategory.HumanResources,
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
            "Charge de personnel ramenee a l'activite reelle. Compare a l'ADR, il dit si "
            + "chaque chambre vendue paie le personnel qu'elle mobilise.",
            "Masse salariale chargee / Nuitees occupees",
            KpiUnit.Currency,
            KpiPolarity.LowerIsBetter,
            KpiAggregation.RatioOfSums,
            KpiRefreshTrigger.OnDemand | KpiRefreshTrigger.Monthly,
            KpiSourceModule.HumanResources,
            "Masse salariale chargee de la periode divisee par les nuitees occupees.",
            [PermissionCatalog.HrRead, PermissionCatalog.LodgingRead])
    ];

    /// <summary>Productivite des etages et de la salle, dans la section Ressources humaines.</summary>
    private static KpiDefinition[] HospitalityProductivity() =>
    [
        new KpiDefinition(
            KpiCodes.RoomsCleanedPerAttendant,
            "Chambres nettoyees par femme de chambre",
            "Chambres / agent",
            KpiCategory.HumanResources,
            ModulePack.Hospitality,
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
            ModulePack.FoodBeverage,
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
            + "salle par service.")
    ];

    /// <summary>La fidelite lue dans les sejours, dans la section Experience client.</summary>
    private static KpiDefinition[] HospitalityRepeatGuests() =>
    [
        new KpiDefinition(
            KpiCodes.RepeatGuestRate,
            "Taux de clients recurrents",
            "Clients fideles",
            KpiCategory.GuestExperience,
            ModulePack.Hospitality,
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
            [PermissionCatalog.LodgingRead])
    ];

    /// <summary>Les canaux de reservation, dans la section Experience client.</summary>
    private static KpiDefinition[] HospitalityDistribution() =>
    [
        new KpiDefinition(
            KpiCodes.DirectBookingRatio,
            "Part des reservations directes",
            "Direct",
            KpiCategory.GuestExperience,
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            + "reservation, y compris celles qui n'aboutissent pas, avec leur motif de perte.")
    ];

    /// <summary>Les consommations ramenees a la chambre et a la nuitee, dans la section Achats et stocks.</summary>
    private static KpiDefinition[] HospitalityConsumption() =>
    [
        new KpiDefinition(
            KpiCodes.HousekeepingCostPerRoom,
            "Cout housekeeping par chambre",
            "Cout etage",
            KpiCategory.SupplyChain,
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
            ModulePack.Hospitality,
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
