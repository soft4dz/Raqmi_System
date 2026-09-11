# Audit externe — septembre 2026

> **Nature :** avis externe en lecture seule, produit le 11 septembre 2026 sur la révision
> `b412b8c` (branche `main`, identique à `claude/fervent-mendel-ckhx81` au moment de l'analyse).
> **Aucun fichier du produit n'a été modifié par cet audit** : seuls les cinq documents de ce
> dossier ont été ajoutés.

## Limite de méthode, à connaître avant de lire

L'environnement d'analyse ne disposait **pas du SDK .NET** : aucun `dotnet build`, aucun
`dotnet test`, aucune exécution du client WPF n'a été faite. Tout ce qui suit est de la **mesure
statique** — comptage de routes, de fichiers, de symboles, lecture du code et des documents.

Conséquence pratique : les constats de *structure* (ce qui existe, ce qui manque, ce qui est
compté) sont fiables ; les constats de *comportement* (« cela fonctionne », « cela plante ») ne
sont jamais affirmés.

### Ce que la CI a établi depuis, sur la révision auditée

L'audit ne pouvait pas dire si la base était verte. La CI de la PR qui porte ce dossier l'a dit,
sur le même arbre de code (aucun fichier du produit n'y est modifié) :

| Job | Résultat |
|---|---|
| `Build API and tests` — build Release + les 1 092 tests | ✅ succès |
| `Build WPF desktop` — compilation Release sur `windows-latest` | ✅ succès |
| `PostgreSQL integration gate` — migrations, contraintes et concurrence contre PostgreSQL 16 réel | ✅ succès |
| `Module readiness gate` | ❌ **échec** — voir le constat F-10 |

**La solution compile et la totalité des tests passe.** Le seul rouge est le garde de readiness,
et son motif précède cet audit (constat **F-10**, chantier **A-07**). C'est donc le point de
départ réel pour l'exécution du plan 04.

## Les cinq documents

| Document | Contenu |
|---|---|
| [`01-avis-technique-global.md`](./01-avis-technique-global.md) | Avis complet : architecture, domaine, données, sécurité, tests, exploitation, conformité. Notation par dimension. **Recommandation de langage de programmation.** |
| [`02-fonctionnalites-par-module.md`](./02-fonctionnalites-par-module.md) | Inventaire des fonctionnalités des 50 modules, extrait des 469 routes réelles de l'API — pas de la documentation. |
| [`03-avis-ui-ux-design.md`](./03-avis-ui-ux-design.md) | Avis sur l'identité de marque, le système de design, le processus de conception et l'ergonomie. |
| [`04-plan-de-remediation.md`](./04-plan-de-remediation.md) | **Le document opérationnel.** 18 chantiers numérotés, priorisés, avec critères d'acceptation vérifiables. |
| [`PROMPT-REMEDIATION.md`](./PROMPT-REMEDIATION.md) | Prompt prêt à coller dans une autre session Claude pour exécuter le plan 04. |

## Rapport avec les documents existants

Ce dossier **ne remplace pas** `docs/reorganisation/` (avis 08, plan 10, généralisation 11), qui
reste plus détaillé sur le calendrier, le chiffrage et la stratégie produit. Il s'en distingue
sur trois points :

1. il porte sur une révision **plus récente** (`b412b8c` contre `da196a7`) et enregistre donc des
   progrès que l'avis 08 signalait comme manquants — la facturation du folio est câblée
   (`FolioInvoicingService.cs:135`), une bibliothèque PDF est branchée (QuestPDF), la sauvegarde
   existe (`deploy/backup/`), le module Fiscalité DGI/SIFEC est livré ;
2. il couvre deux dimensions que les documents existants ne traitaient pas : le **choix du
   langage et de la couche cliente**, et l'**ergonomie de saisie** ;
3. il est écrit pour être **exécutable** : le document 04 est une liste de travaux, pas une
   analyse.

## Ce que l'audit ne dit pas

- Il ne se prononce pas sur la stratégie commerciale (secteur cible, généralisation) : les
  documents 10 et 11 le font mieux.
- Il ne remet pas en cause le choix de PostgreSQL, d'EF Core ni de l'architecture en couches.
- Il ne propose **aucune réécriture** du code existant.
