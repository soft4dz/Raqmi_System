# Design system Raqmi System

## Direction

Raqmi System adopte une interface institutionnelle, calme et orientée décision. Le produit reprend cinq qualités de référence sans reproduire leurs marques :

- la sobriété et la densité maîtrisée des logiciels SAP ;
- la lisibilité des tableaux de bord Stripe ;
- la simplicité des surfaces et des libellés de Notion ;
- la rapidité d’accès de Plane ;
- la cohérence modulaire d’Odoo.

L’identité propre reste prioritaire : signature bilingue « Raqmi System · رقمي سيستم », symbole Q/ق, Manrope + Noto Kufi Arabic et palette marine–bleu–turquoise.

## Principes d’interface

1. **Comprendre en deux secondes.** Le titre, la donnée principale et l’action utile doivent former la première lecture.
2. **Une couleur a une fonction.** Marine pour la structure, turquoise pour l’action ou le repère, couleurs sémantiques pour les états.
3. **La densité sert le métier.** Les listes et tableaux sont compacts ; l’espace blanc sépare les décisions, pas chaque décoration.
4. **Une navigation prévisible.** Huit suites métier stables regroupent les 49 modules. La recherche globale est disponible avec `Ctrl/Cmd + K`.
5. **Pas d’effets gratuits.** Les surfaces restent plates : pas de dégradé décoratif, de halo, de verre dépoli ou de zoom au survol.

## Fondations visuelles

| Usage | Valeur |
|---|---|
| Fond application | `#F4F7FA` |
| Surface | `#FFFFFF` |
| Structure / navigation | `#071525` |
| Primaire | `#073B78` |
| Secondaire | `#145CAB` |
| Accent | `#0AA3AD` |
| Texte principal | `#071525` |
| Rayon standard | `8px` |
| Police latine | Manrope |
| Police arabe | Noto Kufi Arabic |

Les ombres restent légères et ne remplacent jamais une bordure. Les rayons supérieurs à 10 px sont réservés aux dialogues ou aux usages exceptionnellement isolés.

## Hiérarchie des pages

Une page métier suit cet ordre :

1. contexte et période ;
2. résultat principal ;
3. indicateurs décisionnels ;
4. analyse et alertes ;
5. détails, tableaux et historique.

Le tableau de bord global évite de répéter les mêmes indicateurs entre la synthèse et les cartes secondaires.

## Navigation

- La barre latérale porte l’architecture générale et reste marine, stable et peu colorée.
- La barre supérieure est claire et réservée à la recherche, la synchronisation, les notifications et la langue.
- Le lanceur de modules présente d’abord les suites métier, puis les fonctions de la suite active.
- Les sous-suites RH et PortMaster utilisent les mêmes lignes, filtres et états que le lanceur principal.
- L’accès rapide affiche uniquement les destinations autorisées par le rôle actif.

## États et accessibilité

- Un état actif combine contraste, fond et repère turquoise ; il ne dépend jamais de la couleur seule.
- Les alertes utilisent vert, orange ou rouge uniquement lorsqu’une information métier le justifie.
- Tous les boutons d’icône disposent d’un libellé accessible.
- Le focus clavier doit rester visible et les dialogues doivent pouvoir être fermés avec `Échap`.
- Les valeurs financières et les pourcentages utilisent des chiffres tabulaires pour faciliter la comparaison.
