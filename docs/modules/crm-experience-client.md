# Module CRM & expérience client

## Objectif

Le module CRM (10.4 du catalogue) couvre la relation client de l'établissement : la vue client
360°, la segmentation du fichier clients, le programme de fidélité, les campagnes commerciales, la
satisfaction mesurée en NPS et le journal des contacts.

Le périmètre fonctionnel est repris de `Hotel_Metrics_Pro_Desktop` (voir
[migration-from-hotel-metrics-pro-desktop.md](../migration-from-hotel-metrics-pro-desktop.md)) et
retranscrit selon la règle de transformation du dépôt : entités domaine, contrats applicatifs,
migration PostgreSQL, endpoints sécurisés, permissions serveur, audit et écran WPF.

## Le principe qui tient tout le module

**Le CRM ne possède que quatre faits sur un client** : comment il est qualifié (segment,
préférences, consentement), ce qu'il a gagné dans le programme de fidélité, ce qu'il a dit de
l'établissement, et qui lui a parlé.

Tout le reste de ce que la vue 360 affiche — identité, séjours, factures — est **lu au moment de la
requête** dans le module qui le possède, et n'est jamais recopié ici. C'est ce qui garantit que le
CRM ne peut pas finir par contredire le comptoir sur le nombre de nuits dormies par un client.

Corollaire : **tout ce qui peut être déduit l'est**. Le solde de points est la somme du grand
livre, le palier de fidélité est le palier actif le plus haut que ce solde atteint, et les familles
NPS viennent de `SatisfactionEntry.Classify` — jamais d'une colonne. Un solde stocké ou un palier
stocké seraient une seconde vérité à tenir en phase avec les mouvements qui la justifient.

## Données gérées

| Table (schéma `crm`) | Rôle |
|---|---|
| `customer_segments` | Familles commerciales du fichier clients : l'unité de ciblage des campagnes |
| `guest_profiles` | Moitié CRM d'un client (1 pour 1 avec `finance.customers`) : segment, langue, préférences, VIP, consentement marketing daté |
| `loyalty_tiers` | Paliers du programme et seuil de points qui les ouvre |
| `loyalty_transactions` | Grand livre des points, en ajout seul |
| `campaigns` | Campagnes commerciales et leur cycle de vie |
| `satisfaction_entries` | Réponses à la question NPS (0 à 10), brutes |
| `guest_interactions` | Journal des contacts avec les clients |

## Le grand livre de fidélité

Le grand livre est **en ajout seul** : un mouvement n'est jamais modifié ni supprimé, une erreur se
corrige par une correction qui le dit. Le solde d'un client est la somme de ses mouvements, et
c'est la seule définition qu'en donne le module.

`points` est **signé**, et son signe est imposé par le type de mouvement :

| Type | Signe | Sens |
|---|---|---|
| `Earn` | strictement positif | Points gagnés par le client |
| `Redeem` | strictement négatif | Points dépensés contre un avantage |
| `Expiry` | strictement négatif | Points périmés |
| `Adjustment` | non nul, dans les deux sens | Correction manuelle d'un responsable |

C'est ce qui permet au solde d'être une simple somme, et ce qui empêche une utilisation de points
de créditer silencieusement le compte. La règle vit dans l'entité
(`LoyaltyTransaction.RequireSignMatchingKind`) **et** dans une contrainte de vérification
`ck_loyalty_transactions_sign` : la base refuse le couple que le domaine ne pourrait pas produire.

Côté API, le sens vient de la **route appelée**, jamais du corps de la requête : le corps porte une
quantité de points, jamais un signe. C'est pourquoi il y a quatre routes plutôt qu'une seule
prenant le type en paramètre.

### Le garde de solde

Utiliser des points est le seul endroit du module où une lecture décide d'une écriture. Le solde
est donc relu **à l'intérieur** de la transaction `Serializable` qui écrit le mouvement : lu en
dehors, deux utilisations concurrentes voient toutes les deux assez de points et valident toutes
les deux, laissant un client à découvert. L'échec de sérialisation est rendu en 409 rejouable,
comme partout ailleurs dans l'ERP.

## Le consentement marketing

Le consentement est stocké **avec le moment où il a changé**, pas comme un simple drapeau : c'est
la date qui fait preuve au sens de la loi 18-07. Trois états, et non deux :

- jamais recueilli (`marketing_consent = false`, `marketing_consent_updated_at IS NULL`) ;
- refus enregistré (`false` + date) ;
- accord enregistré (`true` + date).

Enregistrer deux fois la même réponse ne réécrit pas la date : ce qui est conservé est la date à
laquelle le consentement a été **obtenu**, pas celle où un écran a été ré-enregistré. La contrainte
`ck_guest_profiles_consent_stamp` interdit un accord sans date.

Recueillir le consentement **crée la fiche** si le client n'en avait pas : l'accord recueilli à
l'arrivée est souvent la première chose que l'on sait d'un client, et le refuser faute de fiche
ferait perdre le seul élément de la relation qui doit être prouvable.

## Campagnes : ce que le canal impose

`Campaign.RequiresMarketingConsent` est la source unique de vérité de la règle : les canaux qui
**poussent** un message vers le client (e-mail, SMS) exigent un consentement enregistré ; un appel
du service commercial et une offre servie au comptoir s'adressent à un client déjà en relation avec
l'établissement, et ne sont pas conditionnés à un accord qu'on ne lui a jamais demandé.

L'audience d'une campagne rend donc trois nombres, pas un seul : les clients atteints, ceux exclus
faute de consentement, et ceux que le canal ne peut pas joindre (pas d'adresse pour un e-mail, pas
de numéro pour un SMS). Sans ces deux exclusions affichées, une audience courte se lit comme une
erreur de ciblage.

La vue 360 applique **la même règle** : une campagne e-mail en cours n'apparaît pas sur la fiche
d'un client à qui elle ne peut pas légalement être envoyée.

### Cycle de vie

`Draft → Scheduled → Running → Completed`, avec `Cancelled` accessible depuis tout état non
terminal, motif obligatoire. Une campagne n'est modifiable **qu'en brouillon** : à partir du moment
où elle est planifiée, ce qu'elle dit et qui elle adresse est ce que les clients ont reçu. Une
campagne qui doit changer après cela est annulée puis rouverte, ce qui laisse les deux faits dans
l'historique.

## NPS

Les réponses sont stockées **brutes**. La famille d'une réponse et le score d'une population sont
déduits à la lecture, si bien qu'un changement de méthode n'a jamais à être rétro-appliqué aux
réponses déjà collectées, et que deux écrans ne peuvent pas être en désaccord sur ce que vaut le
même jeu de réponses.

Les seuils sont ceux de la méthode, pas une convention locale : 0-6 détracteurs, 7-8 passifs, 9-10
promoteurs. Le NPS est le pourcentage de promoteurs moins le pourcentage de détracteurs ; les
passifs comptent dans le total et dans rien d'autre.

`ComputeNps` rend **null** quand personne n'a répondu, et non zéro : « aucune réponse » et « une
population exactement partagée entre promoteurs et détracteurs » sont deux situations très
différentes, et l'écran doit pouvoir afficher un tiret pour la première.

## Permissions

| Clé | Portée |
|---|---|
| `crm.read` | Vue 360, segments, fidélité, campagnes, satisfaction, journal des contacts |
| `crm.write` | Qualifier un client, consentement, segments, paliers, campagnes, enquêtes, contacts |
| `crm.loyalty` | Les quatre mouvements du grand livre de points |

`crm.loyalty` est séparée de `crm.write` parce que déplacer des points déplace quelque chose que le
client peut dépenser — le même raisonnement que `invoices.issue` ou `treasury.approve`.

| Rôle | Clés reçues |
|---|---|
| `direction` | `crm.read` |
| `exploitation.control`, `unit.manager` | `crm.read`, `crm.write`, `crm.loyalty` |
| `cashier` (comptoir) | `crm.read`, `crm.write` |
| `reader` | `crm.read` |

Le comptoir est l'endroit où la relation est réellement consignée : l'accord recueilli à l'arrivée,
une préférence de chambre, la carte de satisfaction rendue, l'appel pris ce matin. Il ne reçoit pas
`crm.loyalty` : créditer ou débiter des points reste avec les rôles qui répondent du programme.

## API

| Méthode | Route | Permission |
|---|---|---|
| GET | `/api/v1/crm/segments` | `crm.read` |
| POST | `/api/v1/crm/segments` | `crm.write` |
| PUT, POST `/{code}/activate\|deactivate` | `/api/v1/crm/segments/{code}` | `crm.write` |
| GET | `/api/v1/crm/guests` | `crm.read` |
| GET | `/api/v1/crm/guests/{customerCode}` | `crm.read` |
| GET | `/api/v1/crm/guests/{customerCode}/360` | `crm.read` |
| PUT | `/api/v1/crm/guests/{customerCode}` | `crm.write` |
| POST | `/api/v1/crm/guests/{customerCode}/marketing-consent` | `crm.write` |
| GET | `/api/v1/crm/loyalty/tiers` | `crm.read` |
| POST, PUT, activate/deactivate | `/api/v1/crm/loyalty/tiers` | `crm.write` |
| GET | `/api/v1/crm/loyalty/accounts/{customerCode}` | `crm.read` |
| POST | `/api/v1/crm/loyalty/accounts/{customerCode}/earn\|redeem\|expire\|adjust` | `crm.loyalty` |
| GET | `/api/v1/crm/campaigns`, `/{code}`, `/{code}/audience` | `crm.read` |
| POST, PUT | `/api/v1/crm/campaigns` | `crm.write` |
| POST | `/api/v1/crm/campaigns/{code}/schedule\|launch\|complete\|cancel` | `crm.write` |
| GET | `/api/v1/crm/satisfaction`, `/satisfaction/nps` | `crm.read` |
| POST | `/api/v1/crm/satisfaction` | `crm.write` |
| GET | `/api/v1/crm/interactions` | `crm.read` |
| POST | `/api/v1/crm/interactions` | `crm.write` |

La vue 360 prend la date du poste (`?today=`) : « en cours » doit vouloir dire aujourd'hui pour
l'utilisateur qui regarde l'écran, pas pour le serveur.

## Règles

- Une fiche CRM ne peut qualifier qu'un client **existant** du fichier clients : le CRM qualifie
  des clients, il n'en invente pas.
- Un segment porté par une fiche ou ciblé par une campagne doit exister **et être actif** : pointer
  du travail neuf vers un segment retiré est la façon dont une audience devient silencieusement
  vide. Les fiches et campagnes qui le portaient déjà le conservent.
- Un segment et un palier sont **désactivés, jamais supprimés**.
- Deux paliers actifs ne peuvent pas ouvrir au même solde : « le palier d'un solde » serait
  ambigu. L'unicité porte sur les paliers **actifs** seulement, si bien qu'un palier retiré peut
  garder le seuil que son successeur utilise désormais.
- Une utilisation de points qui passerait le solde sous zéro est refusée : on ne dépense pas des
  points que le client n'a jamais gagnés.
- Un contact consigné n'est plus modifiable : le journal dit ce qui s'est passé.

## Écran

Onglet `CRM & expérience client` du client WPF, en six sections :

1. **Vue 360** — recherche dans les clients qualifiés, puis tout ce que l'ERP sait d'un client :
   fiche CRM éditable, consentement daté, compteurs (séjours, nuitées, hébergement, facturé,
   restant dû, points, dernière note), derniers contacts, campagnes en cours le concernant et
   réponses de satisfaction.
2. **Segments** — référentiel de ciblage, avec le nombre de clients portant chaque segment.
3. **Fidélité** — paliers du programme d'un côté, compte d'un client et ses mouvements de l'autre.
4. **Campagnes** — cycle de vie complet et audience réelle, exclusions comprises.
5. **Satisfaction** — NPS de la période, unité par unité, et le détail des réponses.
6. **Contacts** — journal des échanges et consignation d'un contact.

Les boutons d'écriture sont grisés selon la clé qui les concerne (`crm.write` ou `crm.loyalty`)
plutôt que de laisser l'utilisateur découvrir un 403 après avoir saisi tout un formulaire ; le
refus fait évidemment autorité côté serveur.

## Hors périmètre de cette vague

- **Portail client et pré-check-in** (annoncés au catalogue pour 10.4) : ils supposent une surface
  web ouverte au client, que ce dépôt n'a pas — il sert une API interne et un client WPF. Le socle
  posé ici (fiche 360, préférences, consentement) est ce sur quoi un portail s'appuiera.
- **Envoi effectif des campagnes** : le module décide QUI une campagne atteint et enregistre son
  cycle de vie ; le routage vers une passerelle e-mail ou SMS relève des intégrations (module 13.5).
- **Réclamations** : elles sont le module 18 (Qualité & réclamations clients), distinct au
  catalogue.
- **Attribution automatique de points** à la clôture d'un séjour : les mouvements sont saisis, non
  déclenchés. Le grand livre et sa règle de signe sont en place pour qu'une règle d'acquisition
  automatique s'y branche sans être réécrite.
