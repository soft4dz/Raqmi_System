using System.Diagnostics;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop;

// Etat d'avancement reel d'un module dans le depot .NET.
//   Disponible : ecran utilisable maintenant dans l'application.
//   ApiPrete   : backend et API livres et testes, ecran client a venir.
//   Partiel    : partiellement couvert (API seule, fonction absorbee par un
//                autre ecran, ou outillage serveur hors application).
//   Planifie   : pas encore developpe.
public enum ModuleStatus
{
    Disponible,
    ApiPrete,
    Partiel,
    Planifie
}

// Une ligne du catalogue des 49 modules de l'ERP.
//   Order         : numero d'ordre fonctionnel ("4.5", "22.8"), non numerique.
//   PermissionKey : cle de PermissionCatalog exigee pour ouvrir le module ;
//                   null = module jamais verrouille par permission.
//   TabIndex      : index de l'onglet de MainTabs quand un ecran existe ;
//                   null = carte non cliquable.
//   StatusNote    : precision affichee en info-bulle (statut Partiel).
public sealed record ModuleCatalogEntry(
    string Order,
    string Group,
    string Name,
    string Description,
    string Priority,
    ModuleStatus Status,
    string? PermissionKey = null,
    int? TabIndex = null,
    string? StatusNote = null);

// Source unique de verite de l'ecran d'accueil : les 49 modules de l'ERP, leur
// groupe fonctionnel, leur priorite et leur avancement dans ce depot.
// L'ordre de la liste est l'ordre d'affichage ; le regroupement par Group est
// realise par la vue (CollectionViewSource), pas ici.
public static class ModuleCatalog
{
    // Totaux attendus - garde de coherence verifiee au chargement du type.
    public const int ExpectedTotal = 49;
    public const int ExpectedAvailable = 30;
    public const int ExpectedApiReady = 0;
    public const int ExpectedPartial = 1;
    public const int ExpectedPlanned = 18;

    // Libelles affiches des groupes fonctionnels (ordre d'apparition).
    public static class Groups
    {
        public const string Socle = "Socle";
        public const string Finance = "Finance";
        public const string Exploitation = "Exploitation";
        public const string Juridique = "Juridique & commercial";
        public const string RessourcesHumaines = "Ressources humaines";
        public const string Controle = "Contrôle";
        public const string Conformite = "Conformité & légal";
        public const string Pilotage = "Pilotage";
        public const string Specifique = "Spécifique";
        public const string Documentaire = "Système documentaire";
        public const string Systeme = "Système";
    }

    // Cle ASCII du groupe -> icone vectorielle "ModuleGroupIcon.<cle>" declaree
    // dans Themes/RaqmiTheme.xaml. Evite de manipuler des libelles accentues
    // dans les cles de ressources.
    private static readonly IReadOnlyDictionary<string, string> IconKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Groups.Socle] = "Socle",
            [Groups.Finance] = "Finance",
            [Groups.Exploitation] = "Exploitation",
            [Groups.Juridique] = "Juridique",
            [Groups.RessourcesHumaines] = "RessourcesHumaines",
            [Groups.Controle] = "Controle",
            [Groups.Conformite] = "Conformite",
            [Groups.Pilotage] = "Pilotage",
            [Groups.Specifique] = "Specifique",
            [Groups.Documentaire] = "Documentaire",
            [Groups.Systeme] = "Systeme"
        };

    // Glyphe propre a chaque application du lanceur. Segoe Fluent Icons est livre
    // avec Windows ; les caracteres restent vectoriels et nets a toute echelle.
    // Le catalogue garde cette correspondance a cote des metadonnees fonctionnelles
    // afin qu'une vue n'ait jamais a deviner une icone depuis un libelle traduit.
    private static readonly IReadOnlyDictionary<string, string> ModuleIconGlyphs =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["1"] = "\uE77B", ["2"] = "\uE713", ["3"] = "\uE80F",
            ["4"] = "\uE9D2", ["4.5"] = "\uE823", ["5"] = "\uE8C7",
            ["5.2"] = "\uE8EF", ["5.4"] = "\uE9D9", ["6"] = "\uE9D2",
            ["8"] = "\uE8A5", ["9"] = "\uE8C7", ["9.2"] = "\uE716",
            ["10"] = "\uE825", ["10.2"] = "\uE7C3",
            ["10.4"] = "\uE77B", ["10.6"] = "\uE716", ["11"] = "\uE7B8",
            ["11.5"] = "\uE8D4", ["11.6"] = "\uE719", ["12"] = "\uE7BF",
            ["12.5"] = "\uE8AB", ["13"] = "\uE90F", ["13.5"] = "\uE7E8",
            ["14.5"] = "\uE8EC", ["18"] = "\uE939", ["20"] = "\uE8A5",
            ["20.2"] = "\uE8D7", ["21"] = "\uE716", ["21.2"] = "\uE77B",
            ["22"] = "\uE83D", ["22.2"] = "\uE8D7", ["22.4"] = "\uE9D5",
            ["22.6"] = "\uE7BA", ["22.8"] = "\uE8BD", ["23"] = "\uE73E",
            ["23.2"] = "\uE72E", ["23.4"] = "\uE8A5", ["23.6"] = "\uE789",
            ["24"] = "\uE9D2", ["24.2"] = "\uE95E", ["24.4"] = "\uE9D2",
            ["25"] = "\uE9D5", ["25.2"] = "\uE7BA", ["25.4"] = "\uE8AB",
            ["26"] = "\uE7E3", ["27"] = "\uE8A5", ["28"] = "\uE777",
            ["29"] = "\uE8B9", ["30"] = "\uE9D5"
        };

    static ModuleCatalog()
    {
        // Garde de coherence : le tableau de verite du depot compte 50 modules,
        // repartis en 31 Disponible / 0 API prete / 0 Partiel / 19 Planifie.
        // Plus aucun module partiel : le 10.6 (Groupes & MICE) a recu son volet
        // groupes - allotements et rooming lists - qui manquait a l'appel.
        // La vague E1 fait passer trois modules d'exploitation de Planifie a
        // Disponible : Stocks (11), Cuisine (11.5) et Achats (12).
        // Le module 29 passe Disponible en supervision seule, et change de nom au
        // passage : il tient un registre des postes, il ne synchronise rien.
        // La bibliotheque KPI (onglet 29) fait passer le 25.4 (Comparatif inter-unites)
        // de Planifie a Disponible : tableau de bord d'indicateurs, comparatif et alertes.
        // Le PMS est le point d'entree unique pour l'hebergement. Le parametrage, la vente,
        // les reservations et les folios ne doivent pas apparaitre comme deux modules concurrents.
        // Toute edition qui casse ces totaux doit etre volontaire et reportee ici.
        EnsureCount("total", Entries.Count, ExpectedTotal);
        EnsureCount("Disponible", CountOf(ModuleStatus.Disponible), ExpectedAvailable);
        EnsureCount("API prete", CountOf(ModuleStatus.ApiPrete), ExpectedApiReady);
        EnsureCount("Partiel", CountOf(ModuleStatus.Partiel), ExpectedPartial);
        EnsureCount("Planifie", CountOf(ModuleStatus.Planifie), ExpectedPlanned);
    }

    public static IReadOnlyList<ModuleCatalogEntry> Entries { get; } = new[]
    {
        // ---------------------------------------------------------------- Socle
        new ModuleCatalogEntry("1", Groups.Socle, "Administration & utilisateurs",
            "Comptes, rôles, permissions et périmètres",
            "P0", ModuleStatus.Disponible, PermissionCatalog.UsersRead, 10),
        new ModuleCatalogEntry("2", Groups.Socle, "Paramétrage global",
            "Identité de l'établissement, réglages du poste et santé du système",
            "P0", ModuleStatus.Disponible, PermissionCatalog.SettingsRead, 9),
        new ModuleCatalogEntry("3", Groups.Socle, "Unités hôtelières",
            "Référentiel des unités et des établissements",
            "P0", ModuleStatus.Disponible, PermissionCatalog.UnitsRead, 1),

        // -------------------------------------------------------------- Finance
        new ModuleCatalogEntry("4", Groups.Finance, "CA journalier (ERP)",
            "Saisie du jour, validation unité et DEC",
            "P0", ModuleStatus.Disponible, PermissionCatalog.RevenueRead, 2),
        new ModuleCatalogEntry("4.5", Groups.Finance, "Clôture journalière & Night Audit",
            "Clôture de la date métier et Night Audit",
            "P1", ModuleStatus.Disponible, PermissionCatalog.ClosingRead, 5),
        new ModuleCatalogEntry("5", Groups.Finance, "Encaissements & trésorerie",
            "Caisses, banques et ordres de paiement",
            "P1", ModuleStatus.Disponible, PermissionCatalog.TreasuryRead, 6),
        new ModuleCatalogEntry("5.2", Groups.Finance, "Comptabilité SCF",
            "Plan de comptes, journaux, écritures en partie double et balance",
            "P1", ModuleStatus.Disponible, PermissionCatalog.AccountingRead, 11),
        new ModuleCatalogEntry("5.4", Groups.Finance, "Fiscalité DGI & SIFEC",
            "TVA, déclarations, liasse et lien SIFEC",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("6", Groups.Finance, "Budget & prévisions",
            "Objectifs, budgets mensuels et écarts",
            "P1", ModuleStatus.Disponible, PermissionCatalog.BudgetRead, 12),
        new ModuleCatalogEntry("8", Groups.Finance, "Facturation",
            "Factures clients, avoirs et registre des ventes",
            "P1", ModuleStatus.Disponible, PermissionCatalog.InvoicesRead, 8),
        new ModuleCatalogEntry("9", Groups.Finance, "Créances & recouvrement",
            "Balance âgée, relances et risque client",
            "P1", ModuleStatus.Disponible, PermissionCatalog.ReceivablesRead, 13),
        new ModuleCatalogEntry("9.2", Groups.Finance, "Clients",
            "Fichier clients et historique commercial",
            "P1", ModuleStatus.Disponible, PermissionCatalog.CustomersRead, 7),

        // --------------------------------------------------------- Exploitation
        new ModuleCatalogEntry("10", Groups.Exploitation, "PMS & Hébergement",
            "Chambres, réservations, séjours, folios, occupation et exploitation hôtelière",
            "P1", ModuleStatus.Disponible, PermissionCatalog.LodgingRead, 15),
        new ModuleCatalogEntry("10.2", Groups.Exploitation, "Housekeeping & chambres",
            "Planning des équipes, inspection, minibar",
            "P2", ModuleStatus.Disponible, PermissionCatalog.HousekeepingRead, 21),
        new ModuleCatalogEntry("10.4", Groups.Exploitation, "CRM & expérience client",
            "Vue client 360°, segmentation, fidélité, campagnes et NPS",
            "P2", ModuleStatus.Disponible, PermissionCatalog.CrmRead, 23),
        // Les six fonctions annoncees au catalogue sont livrees. Le volet groupes touche le coeur
        // du PMS : un allotement est soustrait A LA FOIS de la recherche de disponibilite et du
        // garde de creation de reservation, par un calcul unique partage - les laisser diverger
        // ferait survendre l'hotel en silence.
        new ModuleCatalogEntry("10.6", Groups.Exploitation, "Groupes & MICE",
            "Salles, événements, devis, BEO, allotements et rooming lists",
            "P2", ModuleStatus.Disponible, PermissionCatalog.MiceRead, 28),
        new ModuleCatalogEntry("11", Groups.Exploitation, "Stocks & consommations",
            "Magasins, articles, mouvements valorisés au PMP et inventaires physiques",
            "P2", ModuleStatus.Disponible, PermissionCatalog.InventoryRead, 24),
        // Perimetre livre, dit tel quel : fiches techniques avec cout matiere lu du stock,
        // points de controle HACCP et releves de temperature. Le menu engineering et la
        // tracabilite complete des lots ne sont PAS developpes - la description ne les
        // annonce donc pas.
        new ModuleCatalogEntry("11.5", Groups.Exploitation, "Cuisine, production & qualité",
            "Fiches techniques, coût matière et relevés de température HACCP",
            "P2", ModuleStatus.Disponible, PermissionCatalog.KitchenRead, 26),
        new ModuleCatalogEntry("11.6", Groups.Exploitation, "Points de vente (POS)",
            "Plan de salle, tickets et transfert au folio",
            "P2", ModuleStatus.Partiel, PermissionCatalog.KitchenRead, 30,
            "Comptoir local livré : articles, ticket, quantités et paiement. Persistance serveur et transfert au folio à venir."),
        // Perimetre livre, dit tel quel : fournisseurs, bons de commande numerotes a
        // l'approbation et receptions qui alimentent le stock. Les demandes d'achat, les
        // demandes de prix et les factures fournisseurs sont HORS perimetre - la
        // description ne les annonce donc pas.
        new ModuleCatalogEntry("12", Groups.Exploitation, "Achats & approvisionnements",
            "Fournisseurs, bons de commande et réceptions entrées en stock",
            "P2", ModuleStatus.Disponible, PermissionCatalog.PurchasingRead, 25),
        new ModuleCatalogEntry("12.5", Groups.Exploitation, "Appels d'offres",
            "Lots, ouverture des plis et attribution",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("13", Groups.Exploitation, "Maintenance & interventions",
            "Équipements, ordres de travail et préventif",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("13.5", Groups.Exploitation, "Intégrations matérielles",
            "Serrures, PBX, TPE CIB et imprimantes",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("14.5", Groups.Exploitation, "Tarifs & conventions",
            "Plans tarifaires, périodes de tarif et conventions clients",
            "P1", ModuleStatus.Disponible, PermissionCatalog.TariffsRead, 14),
        new ModuleCatalogEntry("18", Groups.Exploitation, "Qualité & réclamations clients",
            "Réclamations, délais et analyse des causes",
            "P2", ModuleStatus.Planifie),

        // ------------------------------------------------- Juridique & commercial
        new ModuleCatalogEntry("20", Groups.Juridique, "Contrats & conventions",
            "Contrats, allotements et échéances",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("20.2", Groups.Juridique, "Commercial & partenariats",
            "Prospection, partenariats et suivi commercial",
            "P2", ModuleStatus.Planifie),

        // -------------------------------------------------- Ressources humaines
        new ModuleCatalogEntry("21", Groups.RessourcesHumaines, "RH & paie",
            "Collaborateurs, contrats, temps et absences, paie algérienne",
            "P2", ModuleStatus.Disponible, PermissionCatalog.HrRead, 22),
        // Reste planifie en toute rigueur : le module RH consomme des pointages, mais la
        // synchronisation des badgeuses (import ZKTeco, logs bruts, rapprochement des badges)
        // n'est pas developpee. Annoncer "Disponible" ici serait faux sur la carte du module.
        new ModuleCatalogEntry("21.2", Groups.RessourcesHumaines, "Pointeuses & badgeuses",
            "Import des pointages et réconciliation",
            "P2", ModuleStatus.Planifie),

        // ------------------------------------------------------------- Contrôle
        new ModuleCatalogEntry("22", Groups.Controle, "Audit & contrôle interne",
            "Consultation des traces et piste d'audit",
            "P0", ModuleStatus.Disponible, PermissionCatalog.AuditRead, 4),
        new ModuleCatalogEntry("22.2", Groups.Controle, "Workflows & validations",
            "Circuits d'approbation par type de sujet et décisions étape par étape",
            "P1", ModuleStatus.Disponible, PermissionCatalog.ApprovalsRead, 16),
        new ModuleCatalogEntry("22.4", Groups.Controle, "Checklists de contrôle",
            "Modèles, exécution et suivi des écarts",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("22.6", Groups.Controle, "Journal des anomalies",
            "Déclaration, affectation et corrections",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("22.8", Groups.Controle, "Décisions & instructions",
            "Instructions de direction et échéances",
            "P2", ModuleStatus.Planifie),

        // --------------------------------------------------- Conformité & légal
        new ModuleCatalogEntry("23", Groups.Conformite, "Conformité hôtelière",
            "Fiches police, taxe de séjour, tourisme",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("23.2", Groups.Conformite, "Protection des données",
            "Registre des traitements et consentements",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("23.4", Groups.Conformite, "Modules légaux",
            "Immobilisations, CASNOS, inventaire légal",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("23.6", Groups.Conformite, "Veille juridique & réglementaire",
            "Textes, mise en conformité et échéances",
            "P2", ModuleStatus.Planifie),

        // ------------------------------------------------------------- Pilotage
        new ModuleCatalogEntry("24", Groups.Pilotage, "Tableaux de bord directionnels",
            "Indicateurs consolidés par période et unité",
            "P0", ModuleStatus.Disponible, PermissionCatalog.DashboardRead, 3),
        // Les deux ecrans de direction ont desormais leur onglet dedie (19 et 20).
        // Agregation pure des modules existants : aucune table, aucune migration,
        // aucune permission nouvelle - ils lisent sous la cle dashboard.read deja
        // semee, comme le tableau de bord unifie.
        new ModuleCatalogEntry("24.2", Groups.Pilotage, "Dashboard PDG",
            "Vision groupe et alertes de direction",
            "P0", ModuleStatus.Disponible, PermissionCatalog.DashboardRead, 19),
        new ModuleCatalogEntry("24.4", Groups.Pilotage, "Cockpit DEC",
            "Pilotage exploitation et contrôles quotidiens",
            "P0", ModuleStatus.Disponible, PermissionCatalog.DashboardRead, 20),
        new ModuleCatalogEntry("25", Groups.Pilotage, "Rapports automatiques",
            "Catalogue de rapports paramétrables, export CSV et journal des exécutions",
            "P1", ModuleStatus.Disponible, PermissionCatalog.ReportsRead, 17),
        new ModuleCatalogEntry("25.2", Groups.Pilotage, "Alertes & notifications",
            "Règles d'alerte et préférences de diffusion",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("25.4", Groups.Pilotage, "Comparatif inter-unités",
            "Bibliothèque KPI, classement des unités et comparaisons N/N-1",
            "P2", ModuleStatus.Disponible, PermissionCatalog.DashboardRead, 29),

        // ------------------------------------------------------------ Spécifique
        new ModuleCatalogEntry("26", Groups.Specifique, "PortMaster",
            "Bateaux, emplacements, contrats et facturation",
            "P2", ModuleStatus.Planifie),

        // -------------------------------------------------- Système documentaire
        new ModuleCatalogEntry("27", Groups.Documentaire, "Gestion documentaire",
            "GED, versions, signature et archivage légal",
            "P2", ModuleStatus.Planifie),

        // -------------------------------------------------------------- Système
        // La RESTAURATION reste volontairement hors ecran (voir IBackupService) : restaurer
        // la base de production est un acte d'administration serveur, execute selon la
        // procedure documentee. La description dit donc exactement ce que l'ecran fait.
        new ModuleCatalogEntry("28", Groups.Systeme, "Sauvegarde & restauration",
            "État des sauvegardes, déclenchement à la demande et paliers de rétention",
            "P1", ModuleStatus.Disponible, PermissionCatalog.MaintenanceRead, 18),
        // Renomme en connaissance de cause. Le titre d'origine "Synchronisation multi-postes"
        // vient de l'ancien produit Electron, ou chaque poste portait sa propre base SQLite et ou
        // une file de synchronisation avait donc un sens. Ici tous les postes ecrivent dans la
        // meme base PostgreSQL : il n'y a rien a synchroniser, et annoncer une file qui n'existe
        // pas induirait l'exploitant en erreur sur ce que le produit sait faire.
        new ModuleCatalogEntry("29", Groups.Systeme, "Registre des postes & erreurs clients",
            "Postes déclarés, dernier contact et erreurs remontées par les clients",
            "P1", ModuleStatus.Disponible, PermissionCatalog.SyncRead, 27),
        new ModuleCatalogEntry("30", Groups.Systeme, "Journalisation & traçabilité",
            "Journal d'audit, recherche et export",
            "P0", ModuleStatus.Disponible, PermissionCatalog.AuditRead, 4)
    };

    // Libelle francais affiche sur la pastille de statut.
    public static string StatusLabel(ModuleStatus status) => status switch
    {
        ModuleStatus.Disponible => "Disponible",
        ModuleStatus.ApiPrete => "API prête",
        ModuleStatus.Partiel => "Partiel",
        _ => "Planifié"
    };

    public static int CountOf(ModuleStatus status) => Entries.Count(entry => entry.Status == status);

    // Cle d'icone du groupe ; repli sur "Systeme" pour un groupe inconnu, afin
    // qu'une carte ne se retrouve jamais sans icone.
    public static string GroupIconKey(string group) =>
        IconKeys.TryGetValue(group, out var key) ? key : "Systeme";

    public static string ModuleIconGlyph(string order) =>
        ModuleIconGlyphs.TryGetValue(order, out var glyph) ? glyph : "\uE71D";

    // Debug.Assert et non une exception : une derive de ces totaux est une erreur
    // d'edition du catalogue, connue des la compilation. La faire remonter en
    // TypeInitializationException fermerait l'application au demarrage chez le
    // client, sans message exploitable, pour un probleme purement editorial.
    // En Debug elle arrete le developpeur ; en Release l'ecran s'affiche avec
    // les compteurs reels, qui restent justes carte par carte.
    private static void EnsureCount(string label, int actual, int expected)
    {
        Debug.Assert(
            actual == expected,
            $"ModuleCatalog incoherent : {label} = {actual}, attendu {expected}.");
    }
}
