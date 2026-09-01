# Bibliotheque KPI (moteur d'indicateurs)

## Objectif

Doter Raqmi System d'une bibliotheque d'indicateurs hoteliers **standardisee, centralisee et
reutilisable** : une seule definition par indicateur, une seule formule, une seule regle de
consolidation, lues par tous les tableaux de bord de l'ERP.

Le principe fondamental du module tient en une phrase : **les KPI ne sont jamais une deuxieme
base metier**. Ils sont calcules a partir des transactions officielles des modules existants et
ne possedent aucune donnee d'exploitation en propre.

~~~
Transactions metier (recettes, reservations, factures, stocks, paie, ecritures)
        v
Faits rapatries (KpiFactSet)
        v
Moteur KPI (calculateurs purs)
        v
Historique KPI (instantanes)
        v
API
        v
Tableaux de bord / Comparatif / Alertes
~~~

La source de verite reste toujours la donnee metier d'origine.

## Ce que le module possede en propre

Trois tables, aucune donnee d'exploitation :

| Table | Role |
|---|---|
| `kpi.kpi_thresholds` | Bornes de pilotage et objectifs, par indicateur et par perimetre |
| `kpi.kpi_account_mappings` | Rattachement des comptes du plan comptable aux groupes de gestion |
| `kpi.kpi_snapshots` | Valeurs historisees, provisoires ou cloturees |

## Architecture

| Couche | Contenu |
|---|---|
| Domain | `RaqmiSystem.Domain.Kpi` : catalogue, vocabulaire, arithmetique commune, trois entites |
| Application | `RaqmiSystem.Application.Kpi` : faits, calculateurs purs, moteur, assembleur de reponses |
| Infrastructure | `RaqmiSystem.Infrastructure.Kpi` : configurations EF, chargeur de faits, services |
| API | `RaqmiSystem.Api.Endpoints.KpiEndpoints` |
| Desktop | `RaqmiApiClient.Kpi`, `Views/KpiView` (onglet 29) |

**Aucune regle de calcul ne vit dans l'interface.** Les calculateurs sont purs et sans acces
base : toutes les formules sont testables sans base de donnees ni HTTP, et une optimisation SQL
ne peut pas devenir par accident la definition d'un indicateur.

### Separation service / calculateur

Le decoupage reprend celui deja etabli par le tableau de bord groupe : un **service EF** va
chercher les faits (et peut donc filtrer cote base pour ne pas rapatrier dix ans de brouillons),
un **calculateur pur** les combine. Toutes les regles de comptage vivent dans le calculateur, qui
les reapplique sur ce qu'il recoit.

### Decouplage du PMS

Deux faits sont volontairement **neutres** vis-a-vis du module hebergement :

- `KpiStayFact` porte trois booleens - le sejour tient-il la chambre, a-t-il ete annule, le
  client s'est-il presente - et jamais un statut de reservation ;
- `KpiRoomOutageFact` porte une fenetre de nuits et la nature de l'indisponibilite, et jamais un
  etat housekeeping ni un blocage de chambre.

Le vocabulaire des statuts evolue avec le PMS ; les trois questions que se pose un indicateur ne
changent jamais. Seul le chargeur de faits est a mettre a jour quand le PMS s'enrichit.

## Catalogue

`KpiCatalog` est la source unique de verite. Chaque fiche porte : code stable, nom, nom court,
categorie, description, formule en clair, unite, sens de lecture, regle de consolidation, maille,
declencheurs de recalcul, module source, detail de ce qui est compte, permissions exigees,
disponibilite et version de formule.

Un code (`OCCUPANCY_RATE`, `ADR`, `HR_PAYROLL_TO_REVENUE`...) est l'**identite** d'un indicateur :
il voyage dans les URL, les instantanes et les seuils configures. Il ne change jamais.

### Les regles transversales

**1. Un taux ne se moyenne jamais.** L'ADR d'un groupe n'est pas la moyenne des ADR de ses
unites : c'est la somme des revenus chambres divisee par la somme des nuitees vendues. Moyenner
donnerait le meme poids a un hotel de 20 chambres et a un hotel de 300. La regle est portee par
`KpiAggregation` et **verifiee par un test** qui compare, indicateur par indicateur, le calcul
direct du groupe a la somme des numerateurs sur la somme des denominateurs.

**2. Une division par zero rend `null`, jamais `0`.** Un RevPAR sans chambre disponible, un food
cost sans CA restauration, une variation contre un N-1 vide : tous renvoient une valeur absente,
que l'ecran affiche par un tiret. Renvoyer 0 dirait "l'hotel n'a rien produit" la ou la verite
est "la question ne se pose pas".

**3. Un indicateur sans valeur dit pourquoi.** Quatre statuts de qualite - `Valid`, `Partial`,
`MissingData`, `NotApplicable` - et une liste de raisons lisibles, actionnables.

**4. Les statuts font foi.** Seules les recettes *Validees* sont du chiffre d'affaires, seuls les
encaissements *Confirmes* sont de l'argent entre, seules les factures *Emises* sont dues, seuls
les bulletins *Valides* comptent, seules les absences *Approuvees* comptent, seules les ecritures
*Comptabilisees* entrent dans le resultat. Chaque regle est celle du module proprietaire, reprise
et jamais redecidee.

### Hebergement

| Code | Indicateur | Formule |
|---|---|---|
| `OCCUPANCY_RATE` | Taux d'occupation | Nuitees occupees / Nuitees disponibles x 100 |
| `PHYSICAL_ROOMS` | Chambres physiques | Chambres du referentiel |
| `ROOMS_AVAILABLE` | Nuitees disponibles | (Chambres actives - indisponibles) x jours |
| `ROOMS_OUT_OF_ORDER` | Nuitees indisponibles | Chambres retirees de la vente, nuit par nuit |
| `ROOMS_OCCUPIED` | Nuitees occupees | Chambres distinctes couvertes par un sejour bloquant |
| `COMPLIMENTARY_ROOMS` | Nuitees gratuites | Nuitees au tarif fige nul |
| `ROOMS_SOLD` | Nuitees vendues | Occupees - gratuites |
| `ADR` | Prix moyen | Revenus hebergement / Nuitees vendues |
| `REVPAR` | Revenu par chambre disponible | Revenus hebergement / Nuitees disponibles |
| `TREVPAR` | Revenu total par chambre disponible | CA total / Nuitees disponibles |
| `GOPPAR` | Resultat brut par chambre disponible | GOP / Nuitees disponibles |
| `ALOS` | Duree moyenne de sejour | Nuitees / Nombre de sejours |
| `CANCELLATION_RATE` | Taux d'annulation | Annulees / Reservations x 100 |
| `NOSHOW_RATE` | Taux de no-show | No-show / Arrivees attendues x 100 |
| `NOSHOW_LOST_REVENUE` | Revenu perdu sur no-show | Tarif fige x nuits des no-show |
| `GUEST_NIGHTS` | Nuitees clients | Personnes x nuits |
| `REVENUE_PER_GUEST` | Revenu par client | CA total / Nuitees clients |
| `BOOKING_LEAD_TIME` | Delai de reservation | (Arrivee - prise) / Reservations |
| `CPOR` | Cout par chambre occupee | Charges d'exploitation / Nuitees occupees |

#### Les deux occupations - le point a connaitre

Le produit publie **un** taux d'occupation, qui compte les nuitees gratuites : la chambre est
bien occupee, elle n'est simplement pas vendue. L'**ADR**, lui, divise par les nuitees **vendues**,
gratuites exclues - sans quoi une operation commerciale ferait chuter le prix moyen sans qu'aucun
tarif ait bouge.

Consequence assumee : l'identite `RevPAR = ADR x occupation` se verifie contre le **taux
d'occupation vendue** (nuitees vendues / disponibles), pas contre le taux publie. Les deux
coincident exactement des que l'unite n'a offert aucune nuitee ; l'ecart, quand il existe, mesure
exactement le poids des gratuites. Le test `RevPar_equals_adr_times_sold_occupancy` epingle cette
identite.

Une **nuitee gratuite** est deduite du prix (tarif fige nul) : Raqmi System ne porte pas encore de
motif de gratuite (invitation, house use, contrepartie commerciale).

#### La capacite

Les nuitees disponibles ne sont pas "chambres x jours" : ce sont les chambres **actives**, moins
celles retirees de la vente cette nuit-la. Une chambre en travaux n'est pas une chambre vide, et
la compter comme disponible ferait porter a l'exploitation la responsabilite d'un probleme
technique. Le comptage est fait nuit par nuit, jamais par soustraction, parce que deux blocages
peuvent se chevaucher sur la meme chambre.

### Finance

| Code | Indicateur | Formule |
|---|---|---|
| `REVENUE_TOTAL` | Chiffre d'affaires | Hebergement + Restauration + Boissons + Autres |
| `REVENUE_ACCOMMODATION` / `_FOOD` / `_BEVERAGE` / `_OTHER` | Ventilation | Colonnes des recettes validees |
| `REVENUE_BUDGET_VARIANCE` | Ecart budgetaire | Realise - Objectif |
| `REVENUE_BUDGET_ACHIEVEMENT` | Taux de realisation | Realise / Objectif x 100 |
| `GOP` | Resultat brut d'exploitation | Produits - Charges departementales - Charges non reparties |
| `EBITDA` | Excedent brut d'exploitation | GOP - Charges fixes de propriete |
| `GROSS_MARGIN_RATE` | Marge brute | (Produits - Charges departementales) / Produits x 100 |
| `OPERATING_MARGIN_RATE` | Marge operationnelle | GOP / Produits x 100 |
| `CASH_IN` | Encaissements | Encaissements confirmes |
| `CASH_OUT` | Decaissements | Ordres de paiement regles |
| `OPERATING_CASH_FLOW` | Flux de tresorerie | Encaissements - Decaissements |
| `COMMITTED_OUTFLOW_7D` / `_30D` / `_90D` | Engagements a echeance | Ordres approuves non regles a echeance |
| `DSO` | Delai de reglement client | Deux methodes, voir ci-dessous |
| `RECEIVABLES_TOTAL` | Creances | Factures emises non reglees |
| `RECEIVABLES_OVER_90D` | Creances > 90 jours | Tranche d'anciennete du module Creances |
| `RECEIVABLES_OVERDUE_RATE` | Part des creances > 90 jours | > 90 j / Total x 100 |

#### Le compte de resultat ne s'invente pas

Le GOP et l'EBE sont construits sur les ecritures **comptabilisees**, agregees selon le mapping de
comptes configure par l'etablissement. La structure suit la grammaire USALI :

~~~
Produits - Charges departementales           = marge brute departementale
marge departementale - Charges non reparties = GOP
GOP - Charges fixes de propriete             = EBE / EBITDA
~~~

Le module **ne seme aucun mapping** et n'invente aucun numero de compte, pour la meme raison que
`AccountClassCatalog` ne livre pas de plan comptable : reproduire de memoire une nomenclature
reglementaire presenterait des codes inventes comme une reference legale. Tant qu'aucune regle
n'est saisie, GOP, EBE, marges, GOPPAR et CPOR repondent `MissingData` et disent quoi configurer.

Le rattachement suit la regle du **prefixe le plus long** : declarer `6` en charges non reparties
puis `603` en charges departementales est une facon legitime d'ecrire une exception.

Les produits sont pris en solde crediteur, les charges en solde debiteur, de sorte qu'un avoir ou
une extourne diminue le poste qu'il corrige au lieu de s'y ajouter.

#### DSO : deux methodes, le choix est explicite

- **`Simple`** : encours / CA facture de la periode x jours de la periode. Universellement
  comprise, mais elle suppose une activite reguliere - sur un hotel saisonnier, un encours d'ete
  rapporte a un CA d'hiver donne un delai aberrant.
- **`CountBack`** : on remonte les factures de la plus recente a la plus ancienne jusqu'a absorber
  l'encours. Insensible a la saisonnalite ; ne se consolide pas par sommation, la valeur groupe
  est donc recalculee sur l'ensemble des factures du groupe.

La reponse indique toujours la methode utilisee.

### Restauration et boissons

| Code | Indicateur | Formule |
|---|---|---|
| `FB_FOOD_COST_AMOUNT` | Cout matiere denrees | Quantite consommee x cout unitaire |
| `FB_FOOD_COST_RATE` | Food cost | Cout denrees / CA restauration x 100 |
| `FB_BEVERAGE_COST_AMOUNT` / `_RATE` | Cout et ratio boissons | Idem sur la famille Boisson |
| `FB_COST_OF_SALES_RATE` | Cout matiere global | (Denrees + Boissons) / (CA F + CA B) x 100 |
| `INVENTORY_TURNOVER` | Rotation des stocks | Consommations / Stock moyen valorise |
| `STOCK_OUT_RATE` | Taux de rupture | Articles a zero / Articles actifs x 100 |

**Le cout matiere est une sortie de stock, pas un achat.** C'est la distinction qui separe un vrai
food cost d'un ratio d'achats : ce qui est achete et pose sur une etagere n'a rien coute au
resultat du mois, seul ce qui en sort l'a fait.

**Une sortie non valorisee n'est pas une sortie gratuite.** Un mouvement sans cout unitaire
degrade la qualite de l'indicateur en `Partial` et le dit : un food cost qui ignorerait ces
sorties serait faussement rassurant.

### Ressources humaines

| Code | Indicateur | Formule |
|---|---|---|
| `HR_PAYROLL_COST` | Masse salariale chargee | Somme des couts employeur des bulletins valides |
| `HR_PAYROLL_TO_REVENUE` | Masse salariale / CA | Masse salariale / CA x 100 |
| `HR_COST_PER_EMPLOYEE` | Cout par collaborateur | Masse salariale / Bulletins |
| `HR_COST_PER_AVAILABLE_ROOM` | Cout par chambre disponible | Masse salariale / Nuitees disponibles |
| `HR_COST_PER_OCCUPIED_ROOM` | Cout par chambre occupee | Masse salariale / Nuitees occupees |
| `HR_ABSENTEEISM_RATE` | Absenteisme | Jours d'absence / Jours de presence contractuelle x 100 |
| `HR_TURNOVER_RATE` | Rotation du personnel | Departs / Effectif moyen x 100 |
| `HR_HEADCOUNT_AVERAGE` | Effectif moyen | (Effectif debut + Effectif fin) / 2 |
| `HR_OVERTIME_RATE` | Part des heures supplementaires | Heures sup. / Heures travaillees x 100 |
| `HR_REVENUE_PER_EMPLOYEE` | CA par collaborateur | CA / Effectif moyen |
| `HR_REVENUE_PER_WORKED_HOUR` | CA par heure travaillee | CA / Heures pointees validees |
| `HR_ROOMS_PER_ATTENDANT` | Chambres par agent d'etage | Chambres nettoyees / Journees d'agent |

Deux limites annoncees par les indicateurs eux-memes :

- **Absenteisme en jours calendaires.** Raqmi System ne porte ni calendrier de travail ni planning
  d'equipes ; convertir en heures supposerait un rythme que personne n'a declare.
- **Turnover sans ventilation par motif.** Le motif de rupture est un texte libre porte par le
  contrat, non un motif code exploitable statistiquement.

La productivite d'etage compte des **journees d'agent**, pas des agents : un denominateur qui
compterait les agents sans les jours ferait passer une equipe de trois personnes sur trente jours
pour trois personnes tout court.

### Experience client, achats et stocks

`GUEST_SATISFACTION_SCORE`, `NPS`, `REPEAT_GUEST_RATE`, `INVENTORY_TURNOVER`, `STOCK_OUT_RATE`.

Le classement NPS n'est pas refait dans ce module : les bornes de la methode (0-6 detracteur, 7-8
passif, 9-10 promoteur) appartiennent au module CRM, qui les porte deja.

## Indicateurs declares mais non calculables

Le catalogue declare la bibliotheque **complete** attendue d'un ERP hotelier, y compris les
indicateurs dont la donnee source n'existe pas encore dans le produit. Ils portent
`Availability = AwaitingSource`, repondent `NotApplicable`, et **nomment precisement ce qui leur
manque**. Les declarer fige leur formule et leur unite une fois pour toutes ; les calculer a
partir d'a-peu-pres serait la seule chose reellement inacceptable.

| Famille | Indicateurs | Ce qui manque |
|---|---|---|
| Maintenance | `MTTR`, `MTBF`, `PREVENTIVE_COMPLETION_RATE`, `MAINTENANCE_COST_PER_EQUIPMENT`, `MAINTENANCE_COST_TO_ASSET_VALUE`, `HR_INTERVENTIONS_PER_TECHNICIAN` | Module GMAO : referentiel d'equipements et ordres de travail dates. Le module "Maintenance" existant couvre les sauvegardes de la base, pas l'entretien des equipements. |
| Point de vente | `FB_AVERAGE_CHECK`, `FB_REVPASH`, `FB_COST_PER_COVER`, `FB_THEORETICAL_FOOD_COST_RATE`, `FB_FOOD_COST_VARIANCE`, `HR_COVERS_PER_WAITER` | Tickets de caisse, couverts, articles vendus, referentiel de points de vente avec sieges et plages d'ouverture. |
| Pertes | `FB_WASTE_COST`, `FB_WASTE_RATE` | Mouvement de stock de nature "perte" avec son motif. |
| Distribution | `DIRECT_BOOKING_RATIO`, `CHANNEL_COST`, `CONVERSION_RATE` | Canal d'origine sur la reservation, commissions, suivi des demandes non abouties. |
| Fluides | `ENERGY_COST_PER_OCCUPIED_ROOM`, `WATER_PER_GUEST_NIGHT` | Compteurs par unite et index periodiques. |
| Autres | `CASH_BALANCE` | Soldes et releves bancaires : le compte bancaire est un referentiel d'identification, sans solde. |
| | `PURCHASE_PRICE_VARIANCE` | Prix standard par article. |
| | `SUPPLIER_ON_TIME_DELIVERY_RATE` | Date de livraison attendue sur le bon de commande. |
| | `COMPLAINT_RATE` | Registre des reclamations typees. |
| | `HOUSEKEEPING_COST_PER_ROOM` | Typage hotelier des departements RH et rattachement analytique des consommations. |

## Maille : ce qui n'existe qu'au niveau groupe

La comptabilite de Raqmi System **n'est pas analytique** - une ecriture porte un compte, un
journal et une date, jamais une unite hoteliere - et un ordre de paiement ne porte pas davantage
d'unite. Tout ce qui en derive existe donc au niveau du **groupe** et nulle part ailleurs :

`GOP`, `EBITDA`, `GROSS_MARGIN_RATE`, `OPERATING_MARGIN_RATE`, `GOPPAR`, `CPOR`, `CASH_OUT`,
`OPERATING_CASH_FLOW`, `COMMITTED_OUTFLOW_*`, `CASH_BALANCE`.

Repartir ces montants au prorata d'une cle quelconque produirait un resultat par unite d'apparence
convaincante et sans aucun fondement comptable. Le moteur prefere dire "cet indicateur n'existe
qu'au niveau groupe" plutot que d'inventer une comptabilite analytique que l'etablissement n'a pas
mise en place.

**Consequence directe sur le comparatif inter-unites** : la colonne EBE reste vide unite par
unite, renseignee seulement sur la ligne du groupe.

## Seuils, objectifs et alertes

### Deux bornes, trois etats

Le vocabulaire de gestion parle de trois seuils - favorable, vigilance, critique - mais trois
bornes decouperaient **quatre** bandes pour trois etats. La vigilance n'est donc pas une borne :
c'est la bande **entre** les deux autres.

- Indicateur ou la hausse est bonne : favorable si `valeur >= borne favorable`, critique si
  `valeur <= borne critique`, vigilance entre les deux.
- Indicateur ou la baisse est bonne : les deux comparaisons s'inversent.
- Les bornes sont **inclusives** : un seuil qu'on peut atteindre sans consequence n'est pas un
  seuil.
- Sans seuil configure, le verdict est `Unknown` et **jamais** "favorable" : l'absence de seuil
  n'est pas un satisfecit.

La coherence des bornes avec le sens de lecture est verifiee par le **domaine**, ce qui protege
l'API et le poste client de la meme facon.

### Portee

Une regle sans unite est la regle du groupe ; une regle portant une unite ne vaut que pour elle et
**prend le pas entierement** - pas champ par champ. Melanger le seuil favorable de l'unite et le
seuil critique du groupe donnerait un couple que personne n'a valide ensemble.

### L'objectif n'est pas un seuil

`TargetValue` est la valeur visee, affichee a cote du realise. Un seuil declenche une alerte ; un
objectif non atteint n'en declenche pas.

### Les alertes ne sont pas des tickets

Une alerte est une **evaluation en direct** : elle existe tant que la valeur est hors des bornes,
et disparait quand la situation se redresse. Il n'y a ni accuse de reception ni statut "traitee" -
ce qui demanderait une table d'incidents, un cycle de vie et une responsabilite nominative,
c'est-a-dire un autre module. `OwnerRole` dit **qui repond** de l'indicateur ; le suivi de l'action
appartient au module Validations ou au journal des decisions.

## Historisation

Un recalcul ne rend pas toujours le meme resultat : une facture emise en retard, une recette
validee apres coup, un bulletin corrige changent le passe. Sans instantane, la courbe
pluriannuelle du RevPAR se reecrirait a chaque ouverture d'ecran.

| Statut | Comportement |
|---|---|
| `Provisional` | Rafraichi sans facon par le recalcul suivant |
| `Closed` | **Jamais** reecrit. Une divergence est signalee, jamais corrigee en silence |

L'entite elle-meme refuse le recalcul d'un instantane cloture, pas seulement le service : aucun
chemin d'ecriture ne peut contourner la garantie.

Chaque instantane conserve **numerateur et denominateur** a cote de la valeur (ce qui permet de
reconsolider un groupe sans recharger les transactions, et rend le chiffre verifiable a la main)
et la **version de formule** (sans laquelle une courbe melangerait deux methodes sans que personne
puisse s'en apercevoir).

### Unicite

Les deux index uniques portent une **cle de perimetre non nulle** (`scope_key`) et non le code
d'unite nullable : PostgreSQL comme SQLite considerent deux `NULL` comme distincts dans un index
unique, si bien qu'un index sur le code laisserait passer autant de lignes GROUPE concurrentes
qu'on veut. Le marqueur du groupe est `(groupe)`, en minuscules - `HotelUnit.NormalizeCode` mettant
tout code en majuscules, la collision est impossible par construction.

## Securite

### Le principe

Un indicateur n'est rendu que si l'utilisateur detient **toutes** les permissions des modules dont
il lit les donnees. Un ratio ne doit jamais servir de chemin detourne vers une donnee interdite :
la masse salariale rapportee au chiffre d'affaires reste une donnee de paie, et un profil sans
`hr.read` ne la voit pas, meme deguisee en pourcentage. L'ADR exige `revenue.read` **et**
`lodging.read` : avoir l'un sans l'autre ne suffit pas.

Le filtre est applique **cote serveur**, avant que la moindre valeur ne parte. Passer par l'API
plutot que par l'ecran ne change rien. Le tableau de bord indique combien d'indicateurs il ne
montre pas (`HiddenByPermission`) : un ecran qui perd des lignes sans le dire fait douter de tous
les autres chiffres.

### Permissions

- **Lecture** : `dashboard.read` pour entrer dans le module, puis les cles des modules sources de
  chaque indicateur. **Aucune cle nouvelle n'est creee pour la lecture.**
- **Ecriture** : `kpi.admin`, une cle nouvelle, parce qu'elle recouvre trois actes que personne ne
  pouvait poser jusqu'ici - fixer les bornes d'alerte, rattacher les comptes aux groupes de
  gestion, et cloturer un instantane (irreversible par construction).

### Limite a connaitre

Le filtre porte sur les **modules**, pas sur les **unites**. Raqmi System ne rattache aujourd'hui
aucun utilisateur a un etablissement - ni l'entite `User`, ni les jetons emis ne portent de
perimetre d'unite - de sorte qu'un directeur d'unite qui detient `lodging.read` lit deja
l'occupation de toutes les unites par les endpoints existants.

Restreindre un profil a son etablissement demande un **perimetre utilisateur dans le socle de
securite**, qui vaudrait alors pour les vingt-neuf modules. L'ajouter dans le seul module KPI
donnerait l'illusion d'un cloisonnement que le reste du produit n'applique pas.

## Performance

- Fenetre d'analyse plafonnee a **366 jours** (meme plafond que l'occupation du module hebergement
  et que le tableau de bord groupe).
- Filtres de statut poses cote base : optimisation, jamais definition - le calculateur reapplique
  chaque regle sur ce qu'il recoit.
- Stock d'ouverture rapatrie **deja agrege**, pour ne pas recharger tout l'historique du registre.
- Instantanes pour l'historique, ce qui evite de recalculer plusieurs annees a chaque ouverture
  d'ecran.
- Index : `(kpi_code, scope_key, period_start, period_end)` unique, `(period_start, period_end)`,
  `status`.

## Tests

157 tests couvrent le moteur :

| Fichier | Couverture |
|---|---|
| `KpiMathTests` | Division par zero, arrondis, variations, tendances, verdicts de seuil dans les deux sens de lecture |
| `KpiCatalogTests` | Unicite des codes, completude des fiches, permissions connues, coherence maille/source |
| `LodgingKpiCalculatorTests` | Occupation, chambres hors service, gratuites, ADR, identite RevPAR, ALOS, annulations, no-show, delai de reservation, hotel sans activite |
| `FinanceKpiCalculatorTests` | Statuts, budget, balance agee, DSO (deux methodes), tresorerie, GOP/EBE avec et sans mapping |
| `FoodBeverageKpiCalculatorTests` | Cout matiere, sortie non valorisee, rotation, ruptures |
| `WorkforceKpiCalculatorTests` | Masse salariale, ratios par chambre, absenteisme, turnover, productivite d'etage |
| `KpiEngineTests` | Aucun indicateur sans reponse, aucune mesure en double, **toutes les regles d'agregation du catalogue verifiees sur donnees reelles**, consolidation multi-unites |
| `KpiThresholdTests` / `KpiSnapshotTests` | Invariants du domaine |
| `KpiDashboardBuilderTests` | Filtrage par permissions, alertes, classements, derivation du budget, divergence d'instantane |
| `KpiPersistenceTests` | Aller-retour EF, index uniques, cas du perimetre groupe |

## Etat des livrables

| Livrable | Etat |
|---|---|
| Modele metier, catalogue, formules documentees | Livre |
| Architecture technique, entites Domain, services Application | Livre |
| Persistance PostgreSQL (configurations EF, DbSets) | Livre |
| Permissions (`kpi.admin` semee sur direction et exploitation.control) | Livre |
| Chargeur de faits (`KpiFactLoader`) + services `KpiService` / `KpiAdministrationService` | Livre |
| Endpoints API (`/api/v1/kpis`, 14 routes) | Livre |
| Client API WPF (`RaqmiApiClient.Kpi`) | Livre |
| Ecran `KpiView` (onglet 29 : bibliotheque, comparatif, alertes, parametrage) | Livre |
| Tests unitaires + integration | Livre (182 tests verts : 170 unitaires, 12 HTTP) |
| Documentation | Livre |
| Migration EF `WaveKpi` | A generer APRES la migration du chantier PMS en cours : generee avant elle, elle absorberait les tables du modele hebergement encore sans migration |

### Endpoints livres

| Methode | Route | Permission |
|---|---|---|
| GET | `/api/v1/kpis` | `dashboard.read` |
| GET | `/api/v1/kpis/{code}` | `dashboard.read` + cles du module source |
| GET | `/api/v1/kpis/{code}/history` | idem |
| GET | `/api/v1/kpis/dashboard` | `dashboard.read` |
| GET | `/api/v1/kpis/compare` | `dashboard.read` |
| GET | `/api/v1/kpis/alerts` | `dashboard.read` |
| GET/PUT | `/api/v1/kpis/thresholds` | `kpi.admin` |
| GET/PUT | `/api/v1/kpis/account-mappings` | `kpi.admin` |
| POST | `/api/v1/kpis/snapshots` | `kpi.admin` |
| POST | `/api/v1/kpis/snapshots/close` | `kpi.admin` |

Parametres communs : `from`, `to`, `unitId`, `departmentId`, `dsoMethod`, `compareToPreviousYear`,
`compareToBudget`.
