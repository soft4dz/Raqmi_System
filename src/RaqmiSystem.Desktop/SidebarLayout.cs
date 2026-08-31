namespace RaqmiSystem.Desktop;

// Plan de la barre laterale : ses sections, leur ordre, et l'ordre des modules a
// l'interieur de chacune.
//
// Ce n'est PAS le catalogue. ModuleCatalog repond a « ou en est le produit ? » et
// range les 49 modules par famille fonctionnelle, planifies compris : c'est ce que
// montre l'accueil. Cette table-ci repond a « ou est-ce que je vais travailler ? »
// et ne classe que les ecrans livres, dans l'ordre d'une journee d'exploitation :
// ce que la direction ouvre en arrivant, puis l'exploitation du jour, puis la
// finance, puis le controle.
//
// Le parametrage ferme la marche, epingle en pied de panneau et separe du reste.
// C'est la convention des ERP etablis - Setup chez NetSuite, Administration chez
// Opera, « Setup & Extensions » chez Business Central - et elle evite qu'un ecran
// ouvert deux fois l'an occupe la premiere ligne de la navigation, comme c'etait le
// cas tant que la barre laterale suivait la numerotation du catalogue.
//
// Deux regles d'edition :
//   - une section porte un domaine du metier, pas un module isole. « Sauvegarde »
//     n'est pas un domaine, c'est du parametrage : elle rejoint l'administration au
//     lieu de s'offrir un en-tete a elle seule. Les ressources humaines, elles, en
//     sont un, et gardent leur section meme avec un seul ecran livre ;
//   - un ecran absent de cette table n'est jamais perdu : ModuleNavigationGroup.Build
//     lui rend sa famille de catalogue - dans la section de meme nom si elle existe,
//     sinon dans une section creee avant celle epinglee. La table reste donc un choix
//     editorial, pas une obligation de maintenance.
public static class SidebarLayout
{
    // Une section de la barre laterale.
    //   IconKey  : cle d'icone partagee avec l'accueil ("ModuleGroupIcon.<cle>").
    //   IsPinned : section presentee en pied de panneau, hors de la liste defilante.
    //   Tabs     : index d'onglets de MainTabs, dans l'ordre d'affichage.
    public sealed record Section(
        string Name,
        string IconKey,
        bool IsPinned,
        IReadOnlyList<int> Tabs);

    public static IReadOnlyList<Section> Sections { get; } =
    [
        // Ce qu'on ouvre en arrivant : la vue d'ensemble avant le detail. Les
        // rapports ferment la section, on les tire d'une question deja posee.
        new Section("Pilotage", "Pilotage", false, [3, 19, 20, 17]),

        // Le sejour, dans l'ordre de la journee : saisie de la recette, cloture,
        // reception, chambres, client. Les tarifs ferment la section : on les
        // consulte souvent, on les modifie rarement.
        new Section("Exploitation", "Exploitation", false, [2, 5, 15, 21, 23, 14]),

        // L'arriere-cuisine au sens large : ce qu'on a en magasin, ce qu'on commande,
        // ce qu'on produit. Separee de l'exploitation parce que ce n'est ni les memes
        // ecrans ni les memes mains.
        new Section("Achats & stocks", "Achats", false, [24, 25, 26]),

        // L'argent, de l'encaissement au grand livre : ce qui entre, ce qui est
        // facture, a qui, ce qui reste du, puis le back-office comptable.
        new Section("Finance", "Finance", false, [6, 8, 7, 13, 11, 12]),

        // Un domaine a part entiere, meme avec un seul ecran livre : la paie et les
        // temps ne se rangent ni dans l'exploitation ni dans la finance.
        new Section("Ressources humaines", "RessourcesHumaines", false, [22]),

        // Les validations avant l'audit : on decide tous les jours, on remonte la
        // piste d'audit ponctuellement.
        new Section("Contrôle", "Controle", false, [16, 4]),

        // Epinglee en bas : referentiel des unites, comptes, reglages, sauvegardes.
        new Section("Administration", "Systeme", true, [1, 10, 9, 18])
    ];
}
