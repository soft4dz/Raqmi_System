# Module 10 - PMS hotelier (hebergement & occupation)

Ce document decrit le PMS de Raqmi System : ce qui existait avant cette passe, ce qui manquait, ce
qui a ete construit, et les regles qui tiennent l'ensemble. Il s'adresse autant a l'exploitant
(comprendre ce que le systeme accepte et refuse) qu'au developpeur (savoir ou une regle vit et
pourquoi elle est la).

---

## 1. Etat des lieux avant cette passe

Le module 10 existait deja et fonctionnait, sur un perimetre volontairement etroit.

**Ce qui etait deja en place et n'a pas ete refait :**

| Acquis | Ou il vit |
|---|---|
| Referentiel types de chambres et chambres, avec couchage (lits fixes, lits d'appoint, berceaux) | `Domain/Lodging/RoomType.cs`, `Room.cs`, `RoomTypeBed.cs`, `RoomBed.cs` |
| Reservation avec tarif fige NUIT PAR NUIT a la vente | `Domain/Lodging/Reservation.cs` |
| Garde anti-double-reservation en transaction Serializable | `LodgingService.CreateReservationCoreAsync` |
| Folio ouvert a l'arrivee, depart refuse tant que le solde n'est pas nul | `Domain/Lodging/Folio.cs` |
| Allotements de groupe soustraits de la disponibilite ET du garde de creation | `LodgingService.GetAllotmentHoldsAsync` |
| Plans tarifaires, periodes, conventions clients et resolution nuit par nuit | module Tarifs (14.5) |
| Etat menage des chambres (propre / sale / controlee / hors service) | module Housekeeping (10.2) |
| Cloture journaliere par unite | module Cloture (4.5) |

**Ce qui manquait, et pourquoi cela empechait de parler de PMS :**

1. **Aucune notion d'inventaire retire.** Une chambre en panne restait vendable. L'etat menage
   portait bien un statut « hors service », mais sans dates : il ne repondait qu'a « aujourd'hui »,
   jamais a « la nuit du 14 aout ».
2. **La vente etait liee a UNE chambre.** Impossible de vendre « une double standard » sans nommer
   la 214, ce qui interdit toute optimisation du plan et tout regroupement de groupe.
3. **Aucune regle de vente.** Ni stop sell, ni CTA, ni CTD, ni duree minimale ou maximale, ni delai
   de reservation.
4. **Aucune surreservation.** La vente s'arretait a la derniere chambre, sans possibilite de piloter
   un depassement assume.
5. **Un seul statut avant arrivee.** Pas de demande, pas d'option, pas de garantie.
6. **Aucun geste de sejour.** Ni affectation differee, ni walk-in, ni changement de chambre, ni
   prolongation, ni surclassement, ni declassement, ni arrivee anticipee, ni depart tardif.
7. **Un seul folio par sejour**, sans ventilation client / societe / agence.
8. **Aucun acompte, aucune politique d'annulation, aucun extra, aucun forfait.**
9. **Aucun night audit** : le folio etait pose en une fois a l'arrivee, ce qui rattache toutes les
   nuitees a la meme journee d'exploitation et fausse toute recette journaliere.
10. **Pas de date metier hoteliere**, pas de previsionnel, pas de planning graphique, pas de
    tableaux d'arrivees / departs / presents.
11. **Trois permissions seulement** (`lodging.read`, `.write`, `.checkin`), donc aucun moyen de
    separer « vendre » de « changer un prix » ou « lever une fermeture ».

---

## 2. Le principe qui structure tout : une seule source de verite

Le risque principal d'un PMS n'est pas la complexite, c'est la **divergence**. Deux endroits qui
comptent les chambres finissent toujours par ne plus etre d'accord, et l'ecart se paie en survente
silencieuse - l'hotel a vendu une chambre qu'il ne peut pas livrer, et personne ne l'a vu venir.

Toute la conception decoule de la : **un seul calcul d'inventaire**, en fonction pure, alimente par
des donnees chargees ailleurs.

```
parc physique actif
  - chambres bloquees (OOO toujours, OOS selon la politique de l'unite)
  = capacite vendable
  - nuitees deja vendues
  = disponible physique
  - chambres tenues pour des groupes (allotements)
  = disponible a la vente publique
  + solde de surreservation autorise
  = disponible commercial
```

- Le calcul : `Domain/Lodging/AvailabilityCalculator.cs` + `NightInventory.cs` - arithmetique pure,
  testable a la ligne, sans base de donnees.
- L'alimentation : `LodgingService.BuildRoomTypeAvailabilityAsync` - le SEUL endroit ou les cinq
  sources se rencontrent.
- Les consommateurs : recherche de disponibilite, creation de reservation, walk-in, affectation,
  changement de chambre, prolongation, changement de type, previsionnel, planning. Et demain le
  moteur de reservation directe et le channel manager.

L'ordre des soustractions n'est pas arbitraire : soustraire les allotements avant les blocages ferait
disparaitre deux fois la meme chambre le jour ou un bloc porte sur une chambre en panne.

---

## 3. Modele du domaine

### 3.1 Inventaire

| Entite | Role |
|---|---|
| `RoomBlock` | Retrait d'une chambre sur une PERIODE : hors service technique (OOO) ou d'exploitation (OOS). Periode demi-ouverte `[start, end)`, motif obligatoire, categorie de panne, reference de maintenance, date reelle de retour. |
| `LodgingPolicy` | Regles d'exploitation d'une unite : heures de comptoir, arrivee anticipee, depart tardif, effet du OOS sur l'inventaire, interrupteur general de surreservation. Une unite sans ligne suit des defauts volontairement prudents. |
| `RateRestriction` | Stop sell, CTA, CTD, MinLOS, MaxLOS, delais de reservation. Portee (type / plan / canal) : nulle = TOUS. |
| `OverbookingAllowance` | Autorisation datee de vendre au-dela de la capacite physique, par type. |

**OOO contre OOS.** Le hors service technique retire toujours la chambre de l'inventaire vendable :
elle n'est pas louable. Le hors service d'exploitation (usage interne, nettoyage approfondi, blocage
administratif) ne la retire que si `LodgingPolicy.OutOfServiceReducesInventory` le dit - certains
hotels assument de deplacer l'usage interne si un client se presente. Dans les DEUX cas la chambre
cesse d'etre proposee a l'affectation : on ne met pas un client dans une chambre reservee au
personnel.

### 3.2 Vente et sejour

`Reservation` porte desormais un **type** obligatoire et une **chambre facultative**. C'est le fait
central : un client achete « une double standard », pas la 214. La chambre est affectee quand
l'hotel le decide - a la prise, la veille, ou au comptoir.

Statuts : `Inquiry` (ne tient rien) → `Option` / `Confirmed` / `Guaranteed` (tiennent l'inventaire) →
`CheckedIn` → `CheckedOut` ; `Cancelled` et `NoShow` rendent la chambre. La definition unique de
« occupe » vit dans `ReservationStatuses.Blocks`.

Le dossier porte aussi : numero unique par unite, composition adultes / enfants / bebes, heures
annoncees, segment, canal, source, societe, agence, convention, garantie, politique d'annulation
FIGEE, marques walk-in et surreservation, notes et demandes speciales.

| Entite | Role |
|---|---|
| `StayRoomAssignment` | Historique des chambres reellement occupees. Un sejour deplace deux fois porte trois lignes : 101, 205, 310. |
| `ReservationEvent` | Journal metier du sejour : ce qui a change, quand, par qui, ancienne et nouvelle valeur. |

### 3.3 Argent

| Entite | Role |
|---|---|
| `Folio` | Compte du sejour. PLUSIEURS par sejour : client, societe, agence, groupe. Numero unique par unite, statut ouvert / ferme. |
| `FolioCharge` | Ligne TTC, avec quantite, taux de TVA, journee d'exploitation et **cle de geste** (`SourceReference`). |
| `ExtraItem` / `ReservationExtra` | Referentiel des prestations vendables et ce qui est attache a un sejour, au prix fige a la vente. |
| `Package` / `PackageComponent` | Forfait a prix global avec ventilation interne. La somme des composantes DOIT egaler le prix global. |
| `Deposit` | Acompte : demande → verse → impute / rembourse / conserve. |
| `CancellationPolicy` / `CancellationPolicyRule` | Bareme par paliers + regle de no-show, figeable en JSON dans le dossier. |
| `YieldRule` | Regle de revenue management : declencheur, seuil, ajustement, priorite. |
| `NightAuditRun` | Trace d'un passage de night audit : controles, ecritures, rapport. |

---

## 4. Les regles qui comptent

### 4.1 Prix fige a la vente, jamais reecrit en silence

Le tarif de chaque nuit est resolu par le module Tarifs AU MOMENT DE LA VENTE et fige dans le
dossier. Une evolution de tarif ne reecrit jamais le prix auquel une reservation a ete prise.

Il n'est repose que par un geste EXPLICITE - prolongation, changement de type facture, revision -
et **les nuits deja posees au folio ne sont jamais retarifees** : elles sont facturees, le client
les a vues, et les reecrire ferait apparaitre sur la note un prix different de celui annonce.

### 4.2 Le posting des nuitees est idempotent

Chaque nuitee posee porte une cle de geste deterministe : `night:{dossier}:{date}`. Un index unique
`(folio, source_reference)` refuse la seconde insertion.

Consequence : le night audit peut etre relance sans jamais doubler une ecriture, et le rattrapage au
depart repose les nuits manquantes sans risque.

Le posting se fait en trois endroits qui produisent tous la MEME cle :

1. **A l'arrivee** : la nuit d'arrivee, deja consommee. Sans cela, un sejour d'une nuit enregistre et
   solde le meme jour ne facturerait rien.
2. **Au night audit** : la nuit de la journee auditee, pour chaque sejour en cours.
3. **A la preparation du depart** : tout ce qui manque encore. Le night audit peut ne pas avoir
   tourne - panne, oubli, hotel qui ne le passe pas tous les jours - et un client ne doit pas partir
   sans avoir ete facture.

La preparation du depart est **committee separement** du depart lui-meme : si elle vivait dans la
transaction du depart, un depart refuse pour solde non nul annulerait aussi le rattrapage, et la
reception verrait un total plus bas que ce qui est du.

### 4.3 La politique d'annulation est figee dans le dossier

Un dossier confirme porte une COPIE de la politique du jour de sa confirmation. La modifier ensuite
ne change rien aux dossiers deja pris : le client a accepte les conditions affichees ce jour-la, et
un bareme qui change retroactivement est indefendable, commercialement comme juridiquement.

Le bareme est lu par paliers : le palier retenu est **le plus genereux encore applicable**. Lu dans
l'autre sens, il facturerait la penalite maximale a qui annule six mois a l'avance. La penalite est
plafonnee au prix du sejour.

### 4.4 Les restrictions se combinent par la plus restrictive

Quand plusieurs regles couvrent la meme date, c'est la plus contraignante qui s'applique : le plus
grand MinLOS, le plus petit MaxLOS, toute fermeture. C'est la seule combinaison qui empeche de
contourner un stop sell en ajoutant une regle plus fine.

- **Stop sell** ferme les NUITS.
- **CTA** interdit de COMMENCER un sejour a cette date ; les clients presents poursuivent.
- **CTD** interdit de TERMINER a cette date - et la date de depart n'est pas une nuit du sejour,
  d'ou son controle separe.
- **MinLOS / MaxLOS** se lisent sur la DATE D'ARRIVEE (convention des channel managers).
- Les delais de reservation se mesurent depuis la **date metier**, pas la date systeme.

Une regle visant un type inexistant est refusee a la saisie : elle donnerait l'illusion d'une
fermeture posee alors que la vente reste ouverte.

### 4.5 La surreservation est explicite, datee et tracee

Aucune survente sans `OverbookingAllowance` active, sur le type et la periode, ET
`LodgingPolicy.OverbookingEnabled` vrai - l'interrupteur qui coupe tout d'un geste en periode
tendue sans effacer le parametrage.

Toute vente franchissant la capacite physique marque le dossier (`IsOverbooking`), pour que la
reception puisse lister ces dossiers AVANT le jour J et organiser le relogement.

### 4.6 La date metier hoteliere

`BusinessDay.Resolve` : la date metier est le **lendemain de la derniere journee cloturee**, ou la
date calendaire si rien n'a jamais ete cloture.

Il est reellement le 15 aout a 02h00, la cloture du 14 n'est pas passee : la date metier est le 14.
Une consommation saisie a cet instant appartient au 14, pas au 15.

Le retard n'est pas corrige automatiquement, il est **signale** (`IsLate`, `PendingDays`) : avancer
tout seul ferait basculer des recettes dans des journees que personne n'a controlees.

### 4.7 Le PMS pousse les evenements vers le housekeeping

Le module Housekeeping possede le WORKFLOW ; le PMS possede les EVENEMENTS :

| Evenement PMS | Effet |
|---|---|
| Arrivee | chambre → SALE (occupee) |
| Changement de chambre | ancienne chambre → SALE |
| Depart | chambre → SALE |
| No-show constate | chambre → SALE |
| Mise hors service technique | chambre → hors service (retiree du plan de nettoyage) |
| Remise en service | chambre → SALE, jamais PROPRE - elle sort de travaux, pas de menage |

### 4.8 Concurrence

L'invariant anti-double-reservation ne s'exprime pas comme une contrainte de ligne. Il est tenu avec
le motif deja etabli dans le depot : **transaction Serializable**, controle rejoue A L'INTERIEUR,
echecs de serialisation remontes en 409 rejouables. Les transitions de statut utilisent la variante
par claim conditionnel (`UPDATE ... WHERE status = attendu`).

Sont proteges de la meme facon : creation, walk-in, affectation, changement de chambre, changement de
type, prolongation, arrivee, preparation du depart, depart, ligne de folio, night audit.

---

## 5. API

Toutes les routes sont sous `/api/v1`.

### Disponibilite et dossiers

| Verbe | Route |
|---|---|
| GET | `/lodging/availability` |
| GET / POST | `/lodging/reservations` |
| GET | `/lodging/reservations/{id}` et `/detail` |
| PUT | `/lodging/reservations/{id}` |
| POST | `/lodging/reservations/walk-in` |
| POST | `/lodging/reservations/{id}/status`, `/guarantee`, `/assign-room` |
| POST | `/lodging/reservations/{id}/check-in`, `/prepare-check-out`, `/check-out` |
| POST | `/lodging/reservations/{id}/cancel`, `/no-show` |
| POST | `/lodging/stays/{id}/room-move`, `/extend`, `/change-room-type` |

### Folios, extras, acomptes

| Verbe | Route |
|---|---|
| GET | `/lodging/reservations/{id}/folio`, `/folios` |
| POST | `/lodging/reservations/{id}/folios`, `/folio/charges`, `/folio/transfer` |
| GET / POST / DELETE | `/lodging/reservations/{id}/extras` |
| GET / POST | `/lodging/reservations/{id}/deposits` |
| POST | `/lodging/deposits/{id}/pay`, `/apply`, `/refund`, `/forfeit` |

### Inventaire

| Verbe | Route |
|---|---|
| GET / POST / PUT | `/lodging/room-blocks` |
| POST | `/lodging/room-blocks/{id}/close`, `/cancel` |
| POST | `/lodging/rooms/{id}/out-of-order`, `/out-of-service` |
| GET / PUT | `/lodging/policy` |
| GET / POST / PUT | `/lodging/restrictions`, `/lodging/overbooking` |

### Referentiels commerciaux

`/lodging/extras`, `/lodging/packages`, `/lodging/cancellation-policies`, `/lodging/yield-rules`
(GET / POST / PUT / activate / deactivate).

### Exploitation

| Verbe | Route |
|---|---|
| GET | `/lodging/business-date`, `/forecast`, `/tape-chart` |
| GET | `/lodging/arrivals`, `/departures`, `/in-house` |
| GET / POST | `/lodging/no-shows`, `/no-shows/apply` |
| GET / POST | `/lodging/night-audit`, `/night-audit/run` |

---

## 6. Permissions

| Cle | Ce qu'elle autorise |
|---|---|
| `lodging.read` | toutes les lectures |
| `lodging.reserve` | vendre : creation, walk-in, affectation, prolongation, extras |
| `lodging.checkin` | comptoir pendant le sejour : arrivee, folios, acomptes |
| `lodging.checkout` | enregistrer un depart |
| `lodging.room_move` | deplacer un client de chambre |
| `lodging.change_rate` | surclasser / declasser en facturant l'ecart |
| `lodging.cancel` | annuler un dossier |
| `lodging.noshow` | constater une non-presentation |
| `lodging.override_restriction` | vendre malgre un stop sell / CTA / CTD / duree |
| `lodging.overbooking` | vendre au-dela de la capacite physique |
| `lodging.manage_rooms` | parc et blocages hors service |
| `lodging.manage_rates` | restrictions, surreservation, extras, forfaits, politiques, yield |
| `lodging.night_audit` | passer le night audit |

**Compatibilite.** Les cles historiques `lodging.write` et `lodging.checkin` VALENT les cles fines
qu'elles ont remplacees (voir `Program.cs`) : une installation en service ne voit aucun ecran se
fermer. Trois cles n'ont volontairement pas d'equivalent historique - `change_rate`,
`override_restriction`, `overbooking` - parce qu'elles autorisent des gestes qui n'existaient pas, et
que les faire heriter de `lodging.write` reviendrait a les accorder retroactivement.

**Profils.** La reception (`Caissier`) recoit `reserve`, `checkin`, `checkout`, `room_move`,
`noshow`, `cancel`, `night_audit`. Elle ne recoit PAS `change_rate`, `override_restriction`,
`overbooking`, `manage_rooms` ni `manage_rates` : ces cinq-la engagent au-dela de la nuit en cours.

---

## 7. Ecrans

Deux ecrans, un seul moteur :

- **Hebergement & occupation** (onglet 15) : parametrage du parc, recherche de disponibilite, vente,
  folios.
- **PMS front office** (onglet 30) : planning graphique, arrivees, departs, clients presents,
  previsionnel, hors service, regles de vente, night audit.

Le planning graphique dessine une ligne par chambre et une colonne par jour. Les **sejours sans
chambre affectee sont rendus a part** : ils n'ont aucune ligne et pourtant ils consomment
l'inventaire ; les omettre ferait croire a des chambres libres deja vendues - la facon la plus
courante dont un tape chart fait survendre un hotel.

Il n'y a **pas de glisser-deposer**, et c'est delibere : deplacer un sejour exige un motif et une
revalidation de disponibilite. Un geste qui contourne ces deux controles fait survendre l'hotel sans
que personne ne le voie.

---

## 8. Reprise des donnees

La migration `20260901130154_WavePmsFrontOffice` cree les quinze tables du socle PMS et fait evoluer
`reservations`, `folios` et `rate_plans`. Elle contient une **reprise de donnees** executee avant la
creation des index uniques et des contraintes, sans laquelle elle echouerait sur une base peuplee :

1. statut `Booked` → `Confirmed` ;
2. `guest_count` → `adults` (lecture la plus prudente : un adulte occupe un couchage plein) ;
3. garantie → `None` ;
4. type vendu et type d'origine repris de la chambre affectee ;
5. numero de dossier reconstitue par unite dans l'ordre de creation, au format `R{AA}{sequence}` ;
6. folios rattaches a leur unite, numerotes d'apres le dossier, en nature `Guest`, fermes pour les
   sejours deja soldes.

Chaque instruction ne touche que les lignes encore vides : la migration est rejouable.

---

## 9. Ce qui n'est PAS livre

Trois choses sont volontairement laissees a l'etat de fondation, et il vaut mieux le lire ici que le
decouvrir en production.

**Channel manager (§40).** L'interface `IChannelManagerProvider` et le registre existent
(`Application/Channels`), aucun connecteur n'est livre. La frontiere est posee : un fournisseur
PUBLIE ce que le PMS a calcule et RAPPORTE ce que le canal a vendu, il ne calcule jamais de
disponibilite lui-meme. Le jour ou un connecteur arrive, il se branche la et nulle part ailleurs.

**Moteur de reservation directe (§41).** Rien n'est livre. La contrainte est deja tenue par la
conception : le moteur devra appeler `ILodgingService.SearchAvailabilityAsync` et
`CreateReservationAsync` comme n'importe quel autre appelant. Un second moteur de disponibilite est
exclu.

**Comptabilisation automatique des ecritures PMS (§47).** Les lignes de folio portent desormais leur
taux de TVA, leur journee d'exploitation et leur nature ; elles sont donc pretes a alimenter le
moteur comptable. Le deversement lui-meme n'est pas ecrit : il appartient au module Comptabilite,
qui possede le plan comptable et les journaux, et le doubler ici creerait une seconde source
d'ecritures.

**Yield : application automatique.** Les regles sont modelisees, parametrables et APPLIQUEES a la
resolution tarifaire (au plus une regle par nuit, celle de plus petite priorite, et son code reste
dans le tarif resolu). Ce qui n'existe pas est un moteur de recommandation qui proposerait des
regles a partir de l'historique.

---

## 10. Tests

Les scenarios obligatoires sont couverts par des tests de service sur base SQLite isolee, et par des
tests de calcul pur pour les regles :

| Fichier | Ce qu'il verifie |
|---|---|
| `LodgingInventoryTests` | OOO / OOS, politique d'unite, blocage d'une chambre habitee, remise en service, colonnes du previsionnel |
| `LodgingRestrictionTests` | stop sell, CTA, CTD, MinLOS, MaxLOS, delais, combinaison de la plus restrictive, portee par type, application reelle a la vente et levee |
| `LodgingOverbookingTests` | disponible physique contre commercial, marquage des dossiers, interrupteur d'unite, chevauchement d'autorisations |
| `LodgingStayTests` | vente par type sans chambre, affectation, walk-in, changement de chambre et historique, prolongation, surclassement, declassement, ECI/LCO |
| `LodgingNightAuditTests` | date metier, posting, **relance qui ne double pas**, repetition sans ecriture, constat bloquant, balayage des no-shows, prestations automatiques |
| `LodgingBillingTests` | bareme d'annulation fige, plafond de penalite, no-show, acomptes, folios multiples, transfert de ligne, forfait equilibre |
| `LodgingReservationConcurrencyTests` | deux postes vendant la derniere chambre simultanement |
| `RoomAllotmentTests` | la recherche et la creation disent exactement la meme chose |
