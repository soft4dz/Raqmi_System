using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Définition de l'arbre Domaine → Module → Sous-module → Écran.
/// </summary>
/// <remarks>
/// Les modules et sous-modules reprennent les tableaux de la cartographie cible
/// (<c>docs/reorganisation/03-cartographie-cible.md</c>) ; les 30 onglets actuels sont placés
/// selon le mapping <c>04-mapping-existant-vers-cible.md</c> § 4.F. Chaque onglet a UN chemin
/// primaire ; les autres chemins vers le même écran sont des alias (un même écran sert
/// plusieurs sous-modules : la comptabilité SCF tient à la fois le plan, les écritures et les
/// exercices). Un sous-module sans écran est un nœud planifié : visible, jamais ouvrable.
///
/// Les libellés d'écran sont ceux des cartes de l'accueil (catalogue historique), pour qu'il
/// n'y ait qu'un vocabulaire dans toute l'application. Les identifiants, eux, sont les
/// identifiants cibles : stables, ASCII, hiérarchiques - jamais dérivés d'un TabIndex.
/// </remarks>
public static partial class FunctionalArchitectureCatalog
{
    // ------------------------------------------------------------------ brouillons
    // Formes intermédiaires de la définition : les identifiants complets, les rangs, l'icône
    // et la maturité des conteneurs sont calculés à la matérialisation, pas écrits à la main.

    private sealed record ModuleDraft(string Key, string Label, string[] LegacyOrders, SubmoduleDraft[] Submodules);

    private sealed record SubmoduleDraft(string Key, string Label, ScreenDraft[] Screens);

    private sealed record ScreenDraft(
        string Key,
        int Tab,
        string? LegacyOrder,
        string? Label,
        string? Permission,
        string? Description,
        bool IsAlias);

    private static ModuleDraft Module(string key, string label, string[] legacyOrders, params SubmoduleDraft[] submodules) =>
        new(key, label, legacyOrders, submodules);

    private static SubmoduleDraft Sub(string key, string label, params ScreenDraft[] screens) =>
        new(key, label, screens);

    // Écran existant, chemin primaire : l'onglet, l'entrée historique dont il porte le nom et
    // la description, et la permission de lecture exigée pour l'ouvrir.
    private static ScreenDraft Screen(string key, int tab, string legacyOrder, string label, string permission, string description) =>
        new(key, tab, legacyOrder, label, permission, description, IsAlias: false);

    // Chemin secondaire vers un onglet déjà placé. Permission et description viennent du
    // chemin primaire ; le libellé et l'entrée historique peuvent être propres à ce chemin
    // (« Audit & contrôle interne » sous 15 est le même écran que « Journalisation » sous 22).
    private static ScreenDraft Alias(string key, int tab, string? label = null, string? legacyOrder = null) =>
        new(key, tab, legacyOrder, label, Permission: null, Description: null, IsAlias: true);

    // ------------------------------------------------------------------ définition

    private static IReadOnlyList<(string DomainId, ModuleDraft[] Modules)> DefineTree() =>
    [
        ("01",
        [
            Module("travail", "Mon travail", ["22.2"],
                Sub("tableau-de-bord", "Tableau de bord personnel"),
                Sub("mes-taches", "Mes tâches"),
                Sub("mes-validations", "Mes validations",
                    Screen("validations", 16, "22.2", "Workflows & validations", PermissionCatalog.ApprovalsRead,
                        "Circuits d'approbation par type de sujet et décisions étape par étape")),
                Sub("mes-demandes", "Mes demandes"),
                Sub("mes-delegations", "Mes délégations"),
                Sub("mon-activite", "Mon activité")),
            Module("communication", "Communication", ["25.2"],
                Sub("notifications", "Notifications"),
                Sub("messagerie", "Messagerie interne"),
                Sub("agenda", "Mon agenda")),
            Module("documents", "Mes documents et favoris", [],
                Sub("mes-documents", "Mes documents"),
                Sub("mes-favoris", "Mes favoris")),
            Module("compte", "Mon compte", [],
                Sub("profil", "Mon profil"),
                Sub("preferences", "Mes préférences"),
                Sub("securite", "Ma sécurité"))
        ]),

        ("02",
        [
            Module("organisation", "Organisation", ["3"],
                Sub("unites", "Unités hôtelières",
                    Screen("unites", 1, "3", "Unités hôtelières", PermissionCatalog.UnitsRead,
                        "Référentiel des unités et des établissements")),
                Sub("structure", "Entreprise, établissements et services")),
            Module("utilisateurs", "Utilisateurs", ["1"],
                Sub("comptes", "Comptes et rôles",
                    Screen("administration", 10, "1", "Administration & utilisateurs", PermissionCatalog.UsersRead,
                        "Comptes, rôles, permissions et périmètres")),
                Sub("perimetres", "Profils, périmètres et délégations")),
            Module("referentiels", "Référentiels", [],
                Sub("partages", "Référentiels partagés (TVA, devises, pays)")),
            Module("parametrage", "Paramétrage", ["2"],
                Sub("global", "Paramètres globaux",
                    Screen("parametrage", 9, "2", "Paramétrage global", PermissionCatalog.SettingsRead,
                        "Identité de l'établissement, réglages du poste et santé du système")),
                Sub("circuits", "Circuits de validation",
                    Alias("validations", 16))),
            Module("securite", "Sécurité", [],
                Sub("sessions", "Sessions et politiques de sécurité"))
        ]),

        ("03",
        [
            Module("comptabilite", "Comptabilité", ["5.2"],
                Sub("generale", "Comptabilité générale",
                    Screen("scf", 11, "5.2", "Comptabilité SCF", PermissionCatalog.AccountingRead,
                        "Plan de comptes, journaux, écritures en partie double et balance")),
                Sub("exercices", "Exercices et périodes", Alias("scf", 11)),
                Sub("auxiliaire", "Comptabilité auxiliaire", Alias("scf", 11)),
                Sub("etats", "États comptables", Alias("scf", 11)),
                Sub("analytique", "Comptabilité analytique")),
            Module("tresorerie", "Trésorerie", ["5"],
                Sub("encaissements", "Encaissements et décaissements",
                    Screen("tresorerie", 6, "5", "Encaissements & trésorerie", PermissionCatalog.TreasuryRead,
                        "Caisses, banques et ordres de paiement")),
                Sub("banques", "Banques et caisses", Alias("tresorerie", 6)),
                Sub("ordres-de-paiement", "Ordres de paiement", Alias("tresorerie", 6)),
                Sub("rapprochement", "Rapprochement bancaire et prévisions")),
            Module("creances", "Créances", ["9"],
                Sub("balance-agee", "Balance âgée et relances",
                    Screen("creances", 13, "9", "Créances & recouvrement", PermissionCatalog.ReceivablesRead,
                        "Balance âgée, relances et risque client")),
                Sub("contentieux", "Contentieux et provisions")),
            Module("budget", "Budget", ["6"],
                Sub("budget-realise", "Budget vs réalisé",
                    Screen("budget", 12, "6", "Budget & prévisions", PermissionCatalog.BudgetRead,
                        "Objectifs, budgets mensuels et écarts")),
                Sub("revisions", "Révisions, engagements et forecast")),
            Module("fiscalite", "Fiscalité", ["5.4"],
                Sub("declarations", "Déclarations DGI et SIFEC")),
            Module("recettes", "Recettes journalières", ["4"],
                Sub("ca-journalier", "CA journalier",
                    Screen("ca-journalier", 2, "4", "CA journalier (ERP)", PermissionCatalog.RevenueRead,
                        "Saisie du jour, validation unité et DEC")),
                Sub("cloture", "Clôture journalière", Alias("cloture", 5)))
        ]),

        ("04",
        [
            Module("clients", "Clients", ["9.2"],
                Sub("fichier", "Fichier clients",
                    Screen("clients", 7, "9.2", "Clients", PermissionCatalog.CustomersRead,
                        "Fichier clients et historique commercial")),
                Sub("conventions", "Conventions et tarifs négociés", Alias("tarifs", 14))),
            Module("crm", "CRM", ["10.4"],
                Sub("fiche-360", "Fiche client 360°",
                    Screen("crm", 23, "10.4", "CRM & expérience client", PermissionCatalog.CrmRead,
                        "Vue client 360°, segmentation, fidélité, campagnes et NPS")),
                Sub("segmentation", "Segmentation, fidélité et VIP", Alias("crm", 23)),
                Sub("satisfaction", "Satisfaction et NPS", Alias("crm", 23)),
                Sub("prospects", "Prospects et opportunités")),
            Module("reclamations", "Réclamations", ["18"],
                Sub("reclamations", "Réclamations clients")),
            Module("commercial", "Commercial et partenariats", ["20.2"],
                Sub("partenariats", "Prospection et partenariats"))
        ]),

        ("05",
        [
            Module("documents-de-vente", "Documents de vente", ["8"],
                Sub("factures", "Factures",
                    Screen("facturation", 8, "8", "Facturation", PermissionCatalog.InvoicesRead,
                        "Factures clients, avoirs et registre des ventes")),
                Sub("devis", "Devis et pro forma"),
                Sub("avoirs", "Avoirs et notes de débit"),
                Sub("consolidee", "Facturation société, agence, groupe et consolidée")),
            Module("reglements", "Règlements", [],
                Sub("paiements", "Paiements et remboursements", Alias("facturation", 8)),
                Sub("remises", "Remises et taxes"),
                Sub("echeanciers", "Échéanciers")),
            Module("comptabilisation", "Alimentation du moteur comptable", [],
                Sub("evenements", "Événements de vente vers la comptabilité"))
        ]),

        ("06",
        [
            Module("inventaire", "Inventaire", ["10"],
                Sub("chambres", "Types, chambres et couchages",
                    Screen("hebergement", 15, "10", "Hébergement & occupation", PermissionCatalog.LodgingRead,
                        "Types de chambre, réservations, folios et taux d'occupation")),
                Sub("equipements", "Bâtiments, étages et équipements")),
            Module("reservations", "Réservations", [],
                Sub("dossiers", "Disponibilité et réservations", Alias("hebergement", 15))),
            Module("folios", "Folios", [],
                Sub("folios", "Folios, extras et acomptes", Alias("hebergement", 15))),
            Module("front-office", "Front Office", ["10.1"],
                Sub("arrivals", "Arrivées et départs",
                    Screen("pms", 30, "10.1", "PMS front office", PermissionCatalog.LodgingRead,
                        "Planning, arrivées, départs, clients présents, prévisionnel, hors service et night audit")),
                Sub("in-house", "Clients présents et séjours", Alias("pms", 30))),
            Module("planning", "Planning", [],
                Sub("tape-chart", "Planning et prévisionnel", Alias("pms", 30))),
            Module("controle", "Contrôle", ["4.5"],
                Sub("cloture", "Clôture journalière",
                    Screen("cloture", 5, "4.5", "Clôture journalière & Night Audit", PermissionCatalog.ClosingRead,
                        "Clôture de la date métier et Night Audit")),
                Sub("night-audit", "Night audit et date métier", Alias("pms", 30)))
        ]),

        ("07",
        [
            Module("tarification", "Tarification", ["14.5"],
                Sub("plans", "Plans tarifaires et périodes",
                    Screen("tarifs", 14, "14.5", "Tarifs & conventions", PermissionCatalog.TariffsRead,
                        "Plans tarifaires, périodes de tarif et conventions clients")),
                Sub("conventions", "Conventions clients", Alias("tarifs", 14)),
                Sub("promotions", "Promotions")),
            Module("restrictions", "Restrictions", [],
                Sub("regles-de-vente", "Stop sell, durées de séjour, CTA et CTD", Alias("pms", 30))),
            Module("revenue-management", "Revenue Management", [],
                Sub("yield", "Règles de yield et surréservation", Alias("pms", 30)),
                Sub("pickup", "Pickup, pace et cibles ADR/RevPAR")),
            Module("distribution", "Distribution", [],
                Sub("channel-manager", "Channel manager"),
                Sub("booking-engine", "Booking engine"))
        ]),

        ("08",
        [
            Module("housekeeping", "Housekeeping", ["10.2"],
                Sub("planning", "Planning et états des chambres",
                    Screen("housekeeping", 21, "10.2", "Housekeeping & chambres", PermissionCatalog.HousekeepingRead,
                        "Planning des équipes, inspection, minibar")),
                Sub("inspections", "Inspections", Alias("housekeeping", 21)),
                Sub("minibar", "Minibar", Alias("housekeeping", 21)),
                Sub("linge", "Linge, objets trouvés et incidents"))
        ]),

        ("09",
        [
            Module("groupes", "Groupes", ["10.6"],
                Sub("allotements", "Allotements et rooming lists",
                    Screen("mice", 28, "10.6", "Groupes & MICE", PermissionCatalog.MiceRead,
                        "Salles, événements, devis, BEO, allotements et rooming lists")),
                Sub("tarifs-groupes", "Tarifs groupes")),
            Module("evenements", "Événements", [],
                Sub("salles", "Salles et événements", Alias("mice", 28)),
                Sub("devis-beo", "Devis et BEO", Alias("mice", 28)),
                Sub("facturation-groupe", "Facturation groupe", Alias("mice", 28)),
                Sub("prestations", "Restauration, matériel et personnel"))
        ]),

        ("10",
        [
            Module("production", "Fiches techniques", ["11.5"],
                Sub("recettes", "Recettes, portions et coût matière",
                    Screen("cuisine", 26, "11.5", "Cuisine, production & qualité", PermissionCatalog.KitchenRead,
                        "Fiches techniques, coût matière et relevés de température HACCP"))),
            Module("hygiene", "Hygiène", [],
                Sub("haccp", "HACCP et températures", Alias("cuisine", 26)),
                Sub("allergenes", "Allergènes et traçabilité")),
            Module("points-de-vente", "Points de vente", ["11.6"],
                Sub("pos", "POS et KDS")),
            Module("controle-fnb", "Contrôle F&B", [],
                Sub("food-cost", "Food cost, gaspillage et menu engineering"))
        ]),

        ("11",
        [
            Module("stocks", "Stocks", ["11"],
                Sub("articles", "Articles, familles et magasins",
                    Screen("stocks", 24, "11", "Stocks & consommations", PermissionCatalog.InventoryRead,
                        "Magasins, articles, mouvements valorisés au PMP et inventaires physiques")),
                Sub("mouvements", "Mouvements et valorisation", Alias("stocks", 24)),
                Sub("inventaires", "Inventaires physiques", Alias("stocks", 24)),
                Sub("lots", "Lots, expiration, emplacements et rotation"))
        ]),

        ("12",
        [
            Module("fournisseurs", "Fournisseurs", ["12"],
                Sub("fiches", "Fiches fournisseurs",
                    Screen("achats", 25, "12", "Achats & approvisionnements", PermissionCatalog.PurchasingRead,
                        "Fournisseurs, bons de commande et réceptions entrées en stock")),
                Sub("evaluation", "Documents et évaluation")),
            Module("commandes", "Commandes", [],
                Sub("bons-de-commande", "Bons de commande et validation", Alias("achats", 25))),
            Module("reception", "Réception", [],
                Sub("receptions", "Réceptions et entrées en stock", Alias("achats", 25)),
                Sub("retours", "Contrôle et retours")),
            Module("besoins", "Besoins et consultation", [],
                Sub("demandes-achat", "Expression de besoin et demandes d'achat"),
                Sub("comparatifs", "Demandes de prix et comparatifs")),
            Module("factures-fournisseurs", "Factures fournisseurs", [],
                Sub("rapprochement", "Factures, avoirs et rapprochement à trois voies"),
                Sub("paiement", "Échéances et propositions de paiement")),
            Module("marches", "Marchés", ["12.5"],
                Sub("appels-offres", "Appels d'offres"))
        ]),

        ("13",
        [
            Module("personnel", "Personnel", ["21"],
                Sub("dossiers", "Dossiers, contrats et affectations",
                    Screen("rh", 22, "21", "RH & paie", PermissionCatalog.HrRead,
                        "Collaborateurs, contrats, temps et absences, paie algérienne")),
                Sub("carriere", "Carrière et documents")),
            Module("temps", "Temps", [],
                Sub("pointages", "Pointages, présences et absences", Alias("rh", 22)),
                Sub("planning", "Planning, retards et heures supplémentaires")),
            Module("conges", "Congés", [],
                Sub("demandes", "Demandes et validation", Alias("rh", 22)),
                Sub("soldes", "Soldes et planning des congés")),
            Module("paie", "Paie", [],
                Sub("bulletins", "Variables, bulletins et clôture", Alias("rh", 22)),
                Sub("acomptes", "Acomptes, prêts, STC et génération comptable")),
            Module("developpement", "Développement RH", [],
                Sub("formation", "Formation, discipline et santé"))
        ]),

        ("14",
        [
            Module("maintenance", "Maintenance", ["13"],
                Sub("interventions", "Équipements et ordres de travail"),
                Sub("preventif", "Maintenance préventive")),
            Module("patrimoine", "Patrimoine", ["23.4"],
                Sub("immobilisations", "Immobilisations et inventaire légal"))
        ]),

        ("15",
        [
            Module("audit", "Audit", ["22"],
                Sub("piste", "Piste d'audit",
                    Alias("audit", 4, "Audit & contrôle interne", "22")),
                Sub("missions", "Programme, missions et rapports")),
            Module("controle-interne", "Contrôle interne", ["22.4", "22.6"],
                Sub("checklists", "Checklists de contrôle"),
                Sub("anomalies", "Journal des anomalies")),
            Module("decisions", "Décisions et instructions", ["22.8"],
                Sub("instructions", "Décisions, instructions et échéances")),
            Module("risques", "Risques et incidents", [],
                Sub("registre", "Registre des risques et incidents"))
        ]),

        ("16",
        [
            Module("contrats", "Contrats et conventions", ["20"],
                Sub("contrats", "Contrats, allotements et échéances")),
            Module("conformite", "Conformité hôtelière", ["23"],
                Sub("police", "Fiches police, taxe de séjour et tourisme")),
            Module("donnees", "Protection des données", ["23.2"],
                Sub("registre", "Registre des traitements et consentements")),
            Module("veille", "Veille juridique et réglementaire", ["23.6"],
                Sub("textes", "Textes, mise en conformité et échéances"))
        ]),

        ("17",
        [
            Module("ged", "Gestion documentaire", ["27"],
                Sub("documents", "Documents, versions et liens métier"),
                Sub("archivage", "Signature et archivage légal"))
        ]),

        ("18",
        [
            Module("marina", "PortMaster", ["26"],
                Sub("emplacements", "Bateaux et emplacements"),
                Sub("contrats", "Contrats et facturation"))
        ]),

        ("19",
        [
            Module("parking", "Parking", [],
                Sub("abonnements", "Abonnements et tickets")),
            Module("acces", "Contrôle d'accès", [],
                Sub("badges", "Badges, barrières et plage-piscine"))
        ]),

        ("20",
        [
            Module("dashboards", "Dashboards", ["24", "24.2", "24.4"],
                Sub("unite", "Unité",
                    Screen("tableau-de-bord", 3, "24", "Tableaux de bord directionnels", PermissionCatalog.DashboardRead,
                        "Indicateurs consolidés par période et unité")),
                Sub("groupe", "Groupe",
                    Screen("dashboard-pdg", 19, "24.2", "Dashboard PDG", PermissionCatalog.DashboardRead,
                        "Vision groupe et alertes de direction")),
                Sub("exploitation", "Exploitation (DEC)",
                    Screen("cockpit-dec", 20, "24.4", "Cockpit DEC", PermissionCatalog.DashboardRead,
                        "Pilotage exploitation et contrôles quotidiens")),
                Sub("metiers", "Dashboards F&B, RH et maintenance")),
            Module("kpi", "KPI Engine", ["25.4"],
                Sub("bibliotheque", "Bibliothèque KPI et comparatif inter-unités",
                    Screen("kpi", 29, "25.4", "Comparatif inter-unités", PermissionCatalog.DashboardRead,
                        "Bibliothèque KPI, classement des unités et comparaisons N/N-1")),
                Sub("analyse", "Analyse N/N-1 et budget/réalisé", Alias("kpi", 29)),
                Sub("kpi-maintenance", "KPI maintenance")),
            Module("bi", "BI", ["25"],
                Sub("rapports", "Rapports",
                    Screen("rapports", 17, "25", "Rapports automatiques", PermissionCatalog.ReportsRead,
                        "Catalogue de rapports paramétrables, export CSV et journal des exécutions")),
                Sub("entrepot", "Data Warehouse et historisation"))
        ]),

        ("21",
        [
            Module("distribution", "Channel Manager Providers", [],
                Sub("providers", "Fournisseurs de distribution")),
            Module("materiels", "Matériels", ["13.5", "21.2"],
                Sub("equipements", "Serrures, TPE, PBX et imprimantes"),
                Sub("badgeuses", "Pointeuses et badgeuses")),
            Module("journal", "Journal des interfaces", [],
                Sub("interfaces", "Journal des interfaces", Alias("postes", 27))),
            Module("api-externes", "API externes et webhooks", [],
                Sub("webhooks", "API externes, webhooks et banques"))
        ]),

        ("22",
        [
            Module("maintenance", "Maintenance", ["28", "30"],
                Sub("sauvegarde", "Sauvegarde",
                    Screen("sauvegarde", 18, "28", "Sauvegarde & restauration", PermissionCatalog.MaintenanceRead,
                        "État des sauvegardes, déclenchement à la demande et paliers de rétention")),
                Sub("journal-audit", "Journal d'audit",
                    Screen("audit", 4, "30", "Journalisation & traçabilité", PermissionCatalog.AuditRead,
                        "Journal d'audit, recherche et export")),
                Sub("migrations", "Migrations, health checks et logs")),
            Module("diagnostic", "Diagnostic", ["29"],
                Sub("postes", "Postes",
                    Screen("postes", 27, "29", "Registre des postes & erreurs clients", PermissionCatalog.SyncRead,
                        "Postes déclarés, dernier contact et erreurs remontées par les clients"))),
            Module("serveur", "Serveur", [],
                Sub("configuration", "PostgreSQL, API, services et configuration")),
            Module("deploiement", "Déploiement", [],
                Sub("versions", "Serveur, client, postes et versions")),
            Module("updates", "Mises à jour et licence", [],
                Sub("updates", "Updates, rollback et licence"))
        ])
    ];

    // Réservation de place, pas un filtrage : aucune affectation utilisateur ↔ unité n'existe.
    // La répartition suit la donnée : ce qui se tient par unité hôtelière est Unit, ce qui est
    // commun au groupe (identité, fichier client unique, pilotage, système) est Global.
    private static NavigationScope ScopeOf(string domainId) => domainId switch
    {
        "01" or "02" or "04" or "16" or "17" or "20" or "21" or "22" => NavigationScope.Global,
        _ => NavigationScope.Unit
    };

    // ------------------------------------------------------------------ matérialisation

    private static IReadOnlyList<DomainNode> BuildTree()
    {
        var definitions = DefineTree();

        if (definitions.Count != Domains.Count)
        {
            throw new InvalidOperationException(
                $"L'arbre définit {definitions.Count} domaines, la liste des domaines en compte {Domains.Count}.");
        }

        var primaries = CollectPrimaries(definitions);
        var tree = new List<DomainNode>(definitions.Count);

        for (var index = 0; index < definitions.Count; index++)
        {
            var (domainId, modules) = definitions[index];
            var definition = Domains[index];

            // Même ordre que la liste des domaines : c'est elle qui fixe les identifiants et
            // l'ordre d'affichage, l'arbre ne fait que la développer.
            if (!string.Equals(definition.Id, domainId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"L'arbre définit le domaine '{domainId}' au rang {index + 1}, la liste des domaines y place '{definition.Id}'.");
            }

            var scope = ScopeOf(domainId);
            var moduleNodes = modules
                .Select((module, position) => Materialize(definition, scope, module, position + 1, primaries))
                .ToList();

            tree.Add(new DomainNode(
                definition.Id,
                definition.Name,
                index + 1,
                definition.IconKey,
                ReadPermissionKey: null,
                definition.Maturity,
                LicenseFeature: null,
                scope,
                moduleNodes));
        }

        return tree;
    }

    // Les chemins primaires sont relevés avant toute matérialisation : un alias peut être
    // écrit plus haut dans le fichier que l'écran qu'il désigne.
    private static IReadOnlyDictionary<int, ScreenDraft> CollectPrimaries(
        IReadOnlyList<(string DomainId, ModuleDraft[] Modules)> definitions)
    {
        var primaries = new Dictionary<int, ScreenDraft>();

        foreach (var screen in definitions
                     .SelectMany(domain => domain.Modules)
                     .SelectMany(module => module.Submodules)
                     .SelectMany(submodule => submodule.Screens)
                     .Where(screen => !screen.IsAlias))
        {
            if (!primaries.TryAdd(screen.Tab, screen))
            {
                throw new InvalidOperationException(
                    $"L'onglet {screen.Tab} a deux chemins primaires ('{primaries[screen.Tab].Key}' et '{screen.Key}').");
            }
        }

        return primaries;
    }

    private static ModuleNode Materialize(
        FunctionalDomainDefinition domain,
        NavigationScope scope,
        ModuleDraft module,
        int order,
        IReadOnlyDictionary<int, ScreenDraft> primaries)
    {
        var moduleId = $"{domain.Id}.{module.Key}";
        var submodules = module.Submodules
            .Select((submodule, position) => Materialize(domain, scope, moduleId, submodule, position + 1, primaries))
            .ToList();

        return new ModuleNode(
            moduleId,
            module.Label,
            order,
            domain.IconKey,
            ReadPermissionKey: null,
            FunctionalMaturityMapper.Highest(submodules.Select(submodule => submodule.Maturity)),
            LicenseFeature: null,
            scope,
            module.LegacyOrders,
            submodules);
    }

    private static SubmoduleNode Materialize(
        FunctionalDomainDefinition domain,
        NavigationScope scope,
        string moduleId,
        SubmoduleDraft submodule,
        int order,
        IReadOnlyDictionary<int, ScreenDraft> primaries)
    {
        var submoduleId = $"{moduleId}.{submodule.Key}";
        var screens = submodule.Screens
            .Select((screen, position) => Materialize(domain, scope, submoduleId, screen, position + 1, primaries))
            .ToList();

        return new SubmoduleNode(
            submoduleId,
            submodule.Label,
            order,
            domain.IconKey,
            ReadPermissionKey: null,
            FunctionalMaturityMapper.Highest(screens.Select(screen => screen.Maturity)),
            LicenseFeature: null,
            scope,
            screens);
    }

    private static ScreenNode Materialize(
        FunctionalDomainDefinition domain,
        NavigationScope scope,
        string submoduleId,
        ScreenDraft screen,
        int order,
        IReadOnlyDictionary<int, ScreenDraft> primaries)
    {
        if (!primaries.TryGetValue(screen.Tab, out var primary))
        {
            throw new InvalidOperationException(
                $"L'alias '{submoduleId}.{screen.Key}' désigne l'onglet {screen.Tab}, qui n'a aucun chemin primaire.");
        }

        // Un onglet n'existe que pour une entrée Disponible du catalogue : son écran est donc
        // Functional, jamais ProductionReady (ce niveau exige des preuves que le catalogue ne
        // porte pas).
        return new ScreenNode(
            $"{submoduleId}.{screen.Key}",
            screen.Label ?? primary.Label!,
            order,
            domain.IconKey,
            primary.Permission!,
            FunctionalMaturity.Functional,
            LicenseFeature: null,
            scope,
            screen.Tab,
            screen.LegacyOrder,
            screen.IsAlias,
            primary.Description);
    }
}
