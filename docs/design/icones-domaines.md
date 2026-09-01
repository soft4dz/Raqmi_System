# Icônes des 22 domaines fonctionnels

> **Statut** : livrable DESIGN de la vague 1 (réorganisation fonctionnelle). Version du 01/09/2026.
>
> Source de vérité des géométries : `src/RaqmiSystem.Desktop/Themes/RaqmiTheme.xaml`, ressources
> `ModuleGroupIcon.<Clé>`. Ce document décrit ce que le thème contient ; il ne le remplace pas.

## 1. Règles du langage graphique

Les règles sont celles de la charte (`docs/charte-ui-desktop.md`, § 1.4), observées sur les douze
icônes de famille déjà en place et reprises telles quelles pour les quinze nouvelles :

| Règle | Valeur | Pourquoi |
|---|---|---|
| Boîte | 16 × 16, tracé utile entre 1,4 et 14,6 | la barre latérale et la carte module rendent l'icône à 16 px, `Stretch="None"` : rien n'est remis à l'échelle |
| Trait | `StrokeThickness` 1.5 (1.4 dans l'en-tête de domaine de la barre latérale), extrémités et jointures arrondies | même poids optique que les icônes de la sidebar et des boutons |
| Remplissage | aucun — trait seul | l'icône prend la couleur du texte ou de l'accent par son `Stroke`, dans les deux thèmes |
| Détail | un seul motif, trois à six segments, jamais de texte ni de lettre | à 16 px un second motif devient du bruit ; « P » de parking et « € » de facture sont exclus |
| Cercle | `M cx,(cy−r) A r,r 0 1 0 cx+0.01,(cy−r)` | convention des icônes de famille : le départ est le sommet du cercle, le centre est en dessous |
| Clé | ASCII, sans accent (`Hebergement`, `Evenementiel`, `Qualite`) | convention du dépôt pour les clés de ressources (charte, § 1.5) |
| Résolution | `ModuleGroupIconConverter` → `TryFindResource("ModuleGroupIcon.<Clé>")` | un domaine dont la clé manque n'a pas d'icône : le convertisseur rend `null`, jamais une exception |

Chaque pictogramme a été contrôlé à 16, 32 et 72 px, en thème clair (trait `AccentPressedBrush`
sur `SurfaceBrush`) et sombre (trait `AccentBrush` sombre sur `SurfaceBrush` sombre), avant d'être
retenu.

## 2. Inventaire : clé → domaine → pictogramme

Ordre = identifiant stable du domaine (`FunctionalArchitectureCatalog`, `01`…`22`).

| Id | Clé `ModuleGroupIcon.` | Domaine | Pictogramme | Origine |
|---:|---|---|---|---|
| 01 | `MonEspace` | Mon Espace | Avatar : buste dans un cercle. Un seul personnage — le portail est personnel — là où RH en montre deux | **nouvelle** |
| 02 | `Administration` | Administration & Socle ERP | Organigramme : une boîte qui en commande deux. Organisation, comptes et référentiels en descendent | **nouvelle** |
| 03 | `Finance` | Finance & Comptabilité | Billet de banque : rectangle, pièce centrale, deux repères latéraux | réutilisée (famille Finance) |
| 04 | `Commercial` | Commercial, Clients & CRM | Fiche contact : carte avec portrait à gauche et deux lignes à droite | **nouvelle** |
| 05 | `Facturation` | Facturation & Ventes | Ticket : feuille à bord inférieur dentelé, deux lignes dont une courte (le montant) | **nouvelle** |
| 06 | `Hebergement` | PMS / Hébergement | Lit : tête de lit, matelas à coin arrondi, séparation d'oreiller, sommier | **nouvelle** |
| 07 | `Revenue` | Revenue Management & Distribution | Courbe montante terminée par une flèche (tendance, pas mesure) | **nouvelle** |
| 08 | `Housekeeping` | Housekeeping | Balai : manche en diagonale, tête trapézoïdale, axe central | **nouvelle** |
| 09 | `Evenementiel` | Groupes, MICE & Événementiel | Calendrier : deux attaches, ligne d'en-tête, un événement posé dans la grille | **nouvelle** |
| 10 | `Restauration` | F&B / Restauration | Cloche de service : dôme, bouton, plat, plateau | **nouvelle** |
| 11 | `Stocks` | Stocks & Économat | Trois cartons empilés (deux en bas, un au-dessus) | **nouvelle** |
| 12 | `Achats` | Achats & Fournisseurs | Carton isométrique de magasin (un carton : on l'achète ; trois : on les range) | réutilisée (section Achats & stocks) |
| 13 | `RessourcesHumaines` | Ressources Humaines & Paie | Deux personnages côte à côte | réutilisée (famille RH) |
| 14 | `Maintenance` | Maintenance & Patrimoine | Clé à molette en diagonale, mâchoire en haut à droite, manche arrondi | **nouvelle** |
| 15 | `Qualite` | Qualité, Audit & Contrôle interne | Porte-bloc coché : la checklist d'audit (le bouclier reste à la famille Contrôle) | **nouvelle** |
| 16 | `Juridique` | Juridique & Conformité | Balance à deux plateaux | réutilisée (famille Juridique & commercial) |
| 17 | `Documentaire` | GED / Gestion documentaire | Dossier à onglet | réutilisée (famille Système documentaire) |
| 18 | `Marina` | PortMaster / Marina | Voilier : coque, mât, grand-voile (l'ancre reste à `Specifique`) | **nouvelle** |
| 19 | `Parking` | Parking & Contrôle d'accès | Voiture de face : caisse, ligne de ceinture, deux roues | **nouvelle** |
| 20 | `Pilotage` | Pilotage, KPI & BI | Cadran à aiguille | réutilisée (famille Pilotage) |
| 21 | `Integrations` | Intégrations & Matériels | Fiche électrique : corps, deux broches, cordon (l'engrenage reste au serveur) | **nouvelle** |
| 22 | `Systeme` | Administration Système | Engrenage : cercle et huit dents | réutilisée (famille Système) |

Sept réutilisées, quinze nouvelles, vingt-deux clés — une par domaine, aucune partagée.

## 3. Clés conservées hors des 22 domaines

Les cinq clés de famille suivantes ne portent plus de domaine cible mais **restent déclarées** : le
catalogue historique (`ModuleCatalog.IconKeys`), `SidebarLayout` (obsolète, conservé le temps du lot
de compatibilité) et les cartes de l'accueil les citent encore. Ne pas les supprimer ni les renommer.

| Clé | Pictogramme | Famille historique | Domaine cible qui l'a remplacée |
|---|---|---|---|
| `Socle` | Bâtiments | Socle | 02 `Administration` |
| `Exploitation` | Clé (serrure) | Exploitation | 06 à 10 et 14, chacun avec son icône |
| `Controle` | Bouclier coché | Contrôle | 15 `Qualite` |
| `Conformite` | Document scellé | Conformité & légal | 16 `Juridique` |
| `Specifique` | Ancre | Spécifique | 18 `Marina`, 19 `Parking` |

## 4. Paires à ne pas confondre

Le contrôle à 16 px a porté en priorité sur les paires de domaines voisins par le sens ; chacune se
distingue par sa silhouette, pas seulement par un détail :

- **Finance / Facturation** : rectangle horizontal à pièce centrale ↔ feuille verticale dentelée.
- **Achats / Stocks** : un cube isométrique ↔ trois rectangles empilés.
- **Pilotage / Revenue** : arc de cadran ↔ ligne brisée montante.
- **RH / Mon Espace** : deux bustes ouverts ↔ un buste enfermé dans un cercle.
- **Juridique / Qualité / Contrôle** : balance ↔ porte-bloc coché ↔ bouclier coché.
- **Systeme / Integrations / Maintenance** : engrenage ↔ fiche à broches ↔ clé à molette.
- **Specifique / Marina** : ancre ↔ voilier.

## 5. Rendu et couleur

L'icône ne choisit jamais sa couleur : elle prend celle de son contexte.

| Surface | Style | Trait |
|---|---|---|
| En-tête de domaine, barre latérale | `ModuleNavGroupHeaderTemplate` | `TextMutedBrush`, épaisseur 1.4 |
| Carte module disponible | `ModuleCardIcon` + `DataTrigger` Disponible | `AccentPressedBrush` sur pastille `AccentSoftBrush` |
| Carte module non ouvrable ou verrouillée | `ModuleCardIcon` | `TextSecondaryBrush` / `TextMutedBrush` sur `SurfaceHoverBrush` / `DisabledBackgroundBrush` |
| Fil d'Ariane (spécification `navigation-shell.md`) | à créer par NAV, même `Path` 16 × 16 | `TextSecondaryBrush` |

Les deux thèmes sont couverts sans rien déclarer : les brushes cités existent dans `ThemePalette.Sombre`.

## 6. Ajouter ou remplacer une icône

1. Dessiner dans la boîte 16 × 16, trait 1.5, en respectant la table du § 1 ; vérifier le rendu à
   16 px dans les deux thèmes (la maquette `docs/design/maquette-shell.html` sert de banc d'essai :
   ses SVG reprennent les mêmes chemins).
2. Déclarer `<PathGeometry x:Key="ModuleGroupIcon.<Clé>" Figures="…" />` dans `RaqmiTheme.xaml`,
   avec un commentaire qui dit le motif et pourquoi il se distingue de ses voisins.
3. Référencer la clé dans `FunctionalArchitectureCatalog` (`IconKey`) ; le convertisseur fait le reste.
4. Mettre à jour le tableau du § 2 de ce document.
