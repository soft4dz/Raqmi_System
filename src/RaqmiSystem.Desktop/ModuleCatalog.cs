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
    public const int ExpectedAvailable = 21;
    public const int ExpectedApiReady = 0;
    public const int ExpectedPartial = 0;
    public const int ExpectedPlanned = 28;

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

    static ModuleCatalog()
    {
        // Garde de coherence : le tableau de verite du depot compte 49 modules,
        // repartis en 21 Disponible / 0 API prete / 0 Partiel / 28 Planifie.
        // Plus AUCUN module partiel : les deux derniers (Dashboard PDG 24.2 et
        // Cockpit DEC 24.4) ont recu leur ecran dedie et sont passes Disponible.
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
        new ModuleCatalogEntry("10", Groups.Exploitation, "Hébergement & occupation",
            "Types de chambre, réservations, folios et taux d'occupation",
            "P1", ModuleStatus.Disponible, PermissionCatalog.LodgingRead, 15),
        new ModuleCatalogEntry("10.2", Groups.Exploitation, "Housekeeping & chambres",
            "Planning des équipes, inspection, minibar",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("10.4", Groups.Exploitation, "CRM & expérience client",
            "Vue client 360°, fidélité, campagnes, NPS",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("10.6", Groups.Exploitation, "Groupes & MICE",
            "Rooming lists, allotements, salles et BEO",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("11", Groups.Exploitation, "Stocks & consommations",
            "Magasins, lots et péremption, inventaires",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("11.5", Groups.Exploitation, "Cuisine, production & qualité",
            "Fiches techniques, HACCP et allergènes",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("11.6", Groups.Exploitation, "Points de vente (POS)",
            "Plan de salle, tickets et transfert au folio",
            "P2", ModuleStatus.Planifie),
        new ModuleCatalogEntry("12", Groups.Exploitation, "Achats & approvisionnements",
            "Fournisseurs, commandes et réceptions",
            "P2", ModuleStatus.Planifie),
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
        new ModuleCatalogEntry("21", Groups.RessourcesHumaines, "RH & productivité",
            "Collaborateurs, temps de présence, formation",
            "P2", ModuleStatus.Planifie),
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
            "Classement des unités et comparaisons N/N-1",
            "P2", ModuleStatus.Planifie),

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
        new ModuleCatalogEntry("29", Groups.Systeme, "Synchronisation multi-postes",
            "File de synchronisation et état des postes",
            "P1", ModuleStatus.Planifie),
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
