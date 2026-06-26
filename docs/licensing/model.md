# Modèle de gestion des licences

## Objectif

Le système de licence doit permettre de vendre Raqmi System sous forme de packs ou de licences personnalisées, en activant uniquement les modules contractuels.

## Entités principales

```text
Client / Tenant
→ Licence
→ Modules autorisés
→ Limites commerciales
→ Activations
→ Historique
```

## Champs d'une licence

Une licence contient au minimum :

- identifiant unique ;
- client / tenant ;
- type de licence : trial, standard, pro, enterprise, custom ;
- date de début ;
- date d'expiration ;
- statut : active, suspendue, expirée, révoquée ;
- nombre maximum d'utilisateurs ;
- nombre maximum de sites / unités ;
- modules autorisés ;
- mode : online, offline, hybride ;
- empreinte serveur optionnelle ;
- signature cryptographique ;
- période de tolérance hors ligne.

## Packs commerciaux proposés

### Starter

Pour une petite structure.

Modules typiques :

- Administration ;
- Utilisateurs ;
- Unités ;
- Recettes ;
- Facturation ;
- Rapports simples.

### Professional

Pour hôtel ou entreprise moyenne.

Modules typiques :

- Starter ;
- Trésorerie ;
- Créances ;
- RH ;
- Stocks ;
- Achats ;
- GED ;
- Dashboards.

### Enterprise

Pour groupes multi-sites.

Modules typiques :

- Professional ;
- Maintenance ;
- Qualité ;
- Checklists ;
- Synchronisation ;
- Stockage cloud ;
- rapports avancés.

### Custom

Licence sur mesure.

Exemples :

- PortMaster uniquement ;
- Hôtels + PortMaster ;
- RH + paie ;
- Finance uniquement ;
- installation cloud dédiée.

## Fonctionnement online

```text
Raqmi System Server
→ contacte Raqmi License Manager
→ vérifie état licence
→ récupère modules autorisés
→ met à jour cache local signé
```

La vérification peut être quotidienne, hebdomadaire ou selon un intervalle défini dans la licence.

## Fonctionnement offline

```text
Raqmi License Manager
→ génère fichier licence signé
→ client importe la licence
→ serveur client vérifie signature
→ modules activés localement
```

Le client ne peut pas modifier le fichier sans invalider la signature.

## Règles de blocage

Si la licence est expirée :

- accès lecture possible selon politique ;
- création de nouvelles opérations bloquée ;
- exports éventuellement bloqués ;
- notification administrateur affichée.

Si un module n'est pas inclus :

- route non accessible ;
- menu masqué ;
- API serveur refuse l'action ;
- tentative journalisée.

## Règle importante

Le contrôle de licence doit être appliqué côté serveur, pas seulement dans l'interface. Cacher un bouton dans le client n'est pas une sécurité, c'est une décoration avec des illusions.
