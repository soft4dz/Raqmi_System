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
//   - une section porte au moins deux modules ; une famille a un seul element coute
//     un en-tete, un chevron et un clic pour rien ;
//   - un ecran absent de cette table n'est jamais perdu : ModuleNavigationGroup.Build
//     lui rend sa famille de catalogue, ajoutee avant la section epinglee. La table
//     reste donc un choix editorial, pas une obligation de maintenance.
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

        // La journee, dans son ordre reel : saisie de la recette du jour, cloture,
        // puis la reception et les chambres. Les tarifs ferment la section : on les
        // consulte souvent, on les modifie rarement.
        new Section("Exploitation", "Exploitation", false, [2, 5, 15, 21, 14]),

        // L'argent, de l'encaissement au grand livre : ce qui entre, ce qui est
        // facture, a qui, ce qui reste du, puis le back-office comptable.
        new Section("Finance", "Finance", false, [6, 8, 7, 13, 11, 12]),

        // Les validations avant l'audit : on decide tous les jours, on remonte la
        // piste d'audit ponctuellement.
        new Section("Contrôle", "Controle", false, [16, 4]),

        // Epinglee en bas : referentiel des unites, comptes, reglages, sauvegardes.
        new Section("Administration", "Systeme", true, [1, 10, 9, 18])
    ];
}
