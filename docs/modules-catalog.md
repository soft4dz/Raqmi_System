# Catalogue des modules

Ce catalogue reprend la structure fonctionnelle de l'ancien depot `soft4dz/Hotel_Metrics_Pro_Desktop`, fichier source `src/modules/moduleCatalog.ts`.

- Source : soft4dz/Hotel_Metrics_Pro_Desktop
- Branche source : main
- Commit source : c3a5795864f44363464a41ece95c169f4ca04bcf
- Total modules : 49
- Statuts source : operationnel=43, socle=6

Important : le statut ci-dessous correspond a l'ancien projet Electron/SQLite. Dans le nouveau depot .NET, ces modules servent de reference de reprise, pas de preuve qu'ils sont deja reimplementes.

## Repartition par groupe

| Groupe | Nombre |
|---|---:|
| Socle | 3 |
| Finance | 9 |
| Exploitation | 13 |
| Juridique & commercial | 2 |
| Ressources humaines | 2 |
| Contrôle | 5 |
| Conformité & légal | 4 |
| Pilotage | 6 |
| Spécifique | 1 |
| Système documentaire | 1 |
| Système | 3 |

## Liste complete des modules

| Ordre | Groupe | Module | Statut source | Priorite nouvelle version | Route source | Capacites principales |
|---:|---|---|---|---|---|---|
| 1 | Socle | Administration & utilisateurs | operationnel | P0 - Socle/pilote | /admin/users | Comptes utilisateurs; Rôles et permissions; Périmètres par unité |
| 2 | Socle | Paramétrage global | operationnel | P0 - Socle/pilote | /settings | Préférences générales; Interface et thème; Santé du système |
| 3 | Socle | Unités hôtelières | operationnel | P0 - Socle/pilote | /admin/hotels | Référentiel des unités; Paramètres par établissement; Affectations utilisateurs |
| 4 | Finance | CA journalier (ERP) | operationnel | P0 - Socle/pilote | /recettes/journalieres | Saisie quotidienne; Validation unité et DEC; Consolidation du chiffre d’affaires |
| 4.5 | Finance | Clôture journalière & Night Audit | socle | P1 - Core ERP | /recettes/cloture | Clôture journalière; Contrôles Night Audit; Date métier hôtelière |
| 5 | Finance | Encaissements & trésorerie | operationnel | P1 - Core ERP | /encaissements | Encaissements; Comptes bancaires; Ordres de paiement; Prévisions de trésorerie; Rapprochement bancaire |
| 5.2 | Finance | Comptabilité SCF | operationnel | P1 - Core ERP | /comptabilite | Plan comptable SCF; Journaux et écritures; Balance; Exercices; Lettrage |
| 5.4 | Finance | Fiscalité DGI & SIFEC | socle | P2 - Extension | /fiscalite | TVA ventes et achats; Déclarations fiscales; Retenues à la source; Liasse fiscale; Connecteur SIFEC |
| 6 | Finance | Budget & prévisions | operationnel | P1 - Core ERP | /objectifs | Objectifs; Budgets mensuels; Réalisé et écarts |
| 8 | Finance | Facturation | operationnel | P1 - Core ERP | /facturation | Factures clients; Avoirs; Registre des ventes |
| 9 | Finance | Créances & recouvrement | operationnel | P1 - Core ERP | /creances | Balance âgée; Relances; Suivi du risque client |
| 9.2 | Finance | Clients | operationnel | P1 - Core ERP | /clients | Fichier clients; Coordonnées; Historique commercial |
| 10 | Exploitation | Hébergement & occupation | operationnel | P1 - Core ERP | /hebergement | PMS avancé; Réservations et folios; Channel Manager; Booking Engine; Yield management |
| 10.2 | Exploitation | Housekeeping & chambres | operationnel | P2 - Extension | /housekeeping | Planning des équipes; Inspection des chambres; Minibar; Lingerie; Objets trouvés |
| 10.4 | Exploitation | CRM & expérience client | socle | P2 - Extension | /crm | Vue client 360°; Segmentation; Fidélité; Campagnes; NPS; Portail et pré-check-in |
| 10.6 | Exploitation | Groupes & MICE | socle | P2 - Extension | /mice | Rooming lists; Allotements; Événements et salles; Devis; BEO; Facturation événementielle |
| 11 | Exploitation | Stocks & consommations | operationnel | P2 - Extension | /stocks | Magasins multiples; Lots et péremption; Transferts; Inventaires physiques; Codes-barres; Valorisation |
| 11.5 | Exploitation | Cuisine, production & qualité | socle | P2 - Extension | /cuisine | Fiches techniques; HACCP; Températures; Allergènes; Gaspillage; Menu engineering; Traçabilité alimentaire |
| 11.6 | Exploitation | Points de vente (POS) | operationnel | P2 - Extension | /pos | Plan de salle; Tickets et couverts; Partage de note; Multi-paiement; Transfert au folio; KDS |
| 12 | Exploitation | Achats & approvisionnements | operationnel | P2 - Extension | /achats | Fournisseurs; Demandes d’achat; Consultations et devis; Bons de commande; Réceptions; Factures fournisseurs |
| 12.5 | Exploitation | Appels d'offres | operationnel | P2 - Extension | /appels-offres | Dossiers multi-lots; Documents et cahier des charges; Ouverture des plis; Grille d'évaluation; Attribution |
| 13 | Exploitation | Maintenance & interventions | operationnel | P2 - Extension | /maintenance | Équipements; Ordres de travail; Maintenance préventive; SLA; Pièces détachées; Garanties et contrats |
| 13.5 | Exploitation | Intégrations matérielles | socle | P2 - Extension | /integrations-materielles | Serrures électroniques; PBX et IPTV; TPE CIB/Edahabia/SATIM; Scanners; Imprimantes fiscales |
| 14.5 | Exploitation | Tarifs & conventions | operationnel | P1 - Core ERP | /tarifs | Plans tarifaires; Grilles; Promotions; Conventions clients; Yield management |
| 18 | Exploitation | Qualité & réclamations clients | operationnel | P2 - Extension | /reclamations | Réclamations; Traitement et délais; Analyse des causes |
| 20 | Juridique & commercial | Contrats & conventions | operationnel | P2 - Extension | /contrats | Contrats clients; Conventions; Allotements; Échéances |
| 20.2 | Juridique & commercial | Commercial & partenariats | operationnel | P2 - Extension | /commercial | Prospection; Partenariats; Suivi commercial |
| 21 | Ressources humaines | RH & productivité | operationnel | P2 - Extension | /rh | Collaborateurs; Temps et présence; Pré-paie; Talents; Formation; GPEC |
| 21.2 | Ressources humaines | Pointeuses & badgeuses | operationnel | P2 - Extension | /rh/temps/pointeuse | Pointeuses; Import des pointages; Réconciliation |
| 22 | Contrôle | Audit & contrôle interne | operationnel | P0 - Socle/pilote | /audit/logs | Consultation des traces; Contrôles internes; Piste d’audit |
| 22.2 | Contrôle | Workflows & validations | operationnel | P1 - Core ERP | /workflows | Circuits d’approbation; Procédures de validation; Historique des décisions |
| 22.4 | Contrôle | Checklists de contrôle | operationnel | P2 - Extension | /controle/checklists | Modèles de contrôle; Exécution des checklists; Suivi des écarts |
| 22.6 | Contrôle | Journal des anomalies | operationnel | P2 - Extension | /anomalies | Déclaration des anomalies; Affectation; Suivi des corrections |
| 22.8 | Contrôle | Décisions & instructions | operationnel | P2 - Extension | /decisions | Instructions de direction; Échéances; Suivi d’exécution |
| 23 | Conformité & légal | Conformité hôtelière | operationnel | P2 - Extension | /hotel-legal | Fiches police; Taxe de séjour; Rapports tourisme |
| 23.2 | Conformité & légal | Protection des données | operationnel | P2 - Extension | /conformite/donnees-personnelles | Registre des traitements; Consentements; Demandes de droits; Incidents; Conservation |
| 23.4 | Conformité & légal | Modules légaux | operationnel | P2 - Extension | /conformite/modules-legaux | Immobilisations; CASNOS; Inventaire légal |
| 23.6 | Conformité & légal | Veille juridique & réglementaire | operationnel | P2 - Extension | /veille-reglementaire | Répertoire des textes; Suivi de mise en conformité; Rappels d'échéance; Documents attachés |
| 24 | Pilotage | Tableaux de bord directionnels | operationnel | P0 - Socle/pilote | /dashboard | Dashboard global; Indicateurs consolidés; Filtres par période et unité |
| 24.2 | Pilotage | Dashboard PDG | operationnel | P0 - Socle/pilote | /dashboard/pdg | Vision groupe; Consolidation des unités; Alertes de direction |
| 24.4 | Pilotage | Cockpit DEC | operationnel | P0 - Socle/pilote | /dec/cockpit | Pilotage exploitation; Contrôles quotidiens; Actions et alertes |
| 25 | Pilotage | Rapports automatiques | operationnel | P1 - Core ERP | /rapports | Rapports configurables; Exports PDF et Excel; Historique des exécutions |
| 25.2 | Pilotage | Alertes & notifications | operationnel | P2 - Extension | /settings/notifications | Notifications internes; Règles d’alerte; Préférences utilisateurs |
| 25.4 | Pilotage | Comparatif inter-unités | operationnel | P2 - Extension | /dashboard | Classement des unités; Comparaisons N/N-1; Écarts aux objectifs |
| 26 | Spécifique | PortMaster | operationnel | P2 - Extension | /portmaster | Bateaux et clients; Emplacements; Contrats; Mouvements; Facturation et recouvrement |
| 27 | Système documentaire | Gestion documentaire | operationnel | P2 - Extension | /ged | GED; Versions; OCR et signature; Archivage légal |
| 28 | Système | Sauvegarde & restauration | operationnel | P1 - Core ERP | /settings/backup | Sauvegardes locales; Restauration; Politiques de rétention |
| 29 | Système | Synchronisation multi-postes | operationnel | P1 - Core ERP | /system/sync | File de synchronisation; État des postes; Résolution des erreurs |
| 30 | Système | Journalisation & traçabilité | operationnel | P0 - Socle/pilote | /audit/logs | Journal d’audit; Traçabilité des opérations; Recherche et export |

## Lecture recommandee

- `P0 - Socle/pilote` : a developper en premier pour obtenir une version serveur-client exploitable.
- `P1 - Core ERP` : a implementer apres validation du socle securite, des unites et des recettes.
- `P2 - Extension` : a reprendre progressivement apres le pilote metier.

## Suites metier cible

La navigation historique etait organisee autour de grandes suites : Pilotage, Exploitation, Finance, Operations, Controle interne, Qualite, Commercial/GED, RH, PortMaster, Administration et Systeme. La nouvelle version .NET doit garder cette logique, mais avec des autorisations serveur et une base PostgreSQL centrale.

## Ecarts assumes entre le catalogue source et le depot .NET

Le tableau ci-dessus transcrit fidelement le catalogue de l'ancien produit Electron/SQLite : il est
conserve tel quel comme releve d'origine. Lorsque la reprise s'ecarte volontairement de la source,
l'ecart est consigne ici plutot que d'etre efface du tableau.

### Module 10 - le PMS hotelier, complete

Le catalogue source annonce pour le module 10 : PMS avance, reservations et folios, channel manager,
booking engine, yield management. Le depot livrait jusqu'ici un noyau volontairement etroit -
chambres, reservation sur UNE chambre nommee, folio unique pose a l'arrivee. Ce noyau etait correct
mais il ne suffisait pas a faire tourner une reception.

Cette passe le complete. Le detail vit dans `docs/modules/pms-hebergement.md` ; l'essentiel tient en
quelques points.

CE QUI STRUCTURE TOUT : une seule source de verite pour l'inventaire. Un calcul unique et pur
(`AvailabilityCalculator`) alimente par cinq sources - parc, blocages OOO/OOS, nuitees vendues,
allotements de groupe, surreservation autorisee - et appele par TOUS les chemins qui vendent ou
tiennent une chambre. Deux endroits qui compteraient les chambres finiraient toujours par ne plus
etre d'accord, et l'ecart se paierait en survente silencieuse.

LIVRE :
- inventaire : hors service technique (OOO) et d'exploitation (OOS) dates et motives, politique
  d'unite decidant si le second retire ou non l'inventaire commercial ;
- vente PAR TYPE avec affectation differee de la chambre, walk-in, statuts demande / option /
  confirmee / garantie ;
- regles de vente : stop sell, CTA, CTD, MinLOS, MaxLOS, delais de reservation, combinees par la
  plus restrictive ;
- surreservation datee, par type, tracee sur le dossier qui franchit la capacite physique ;
- gestes de sejour : affectation, changement de chambre avec historique complet, prolongation,
  surclassement et declassement deduits du RANG des types, arrivee anticipee et depart tardif ;
- argent : folios multiples (client / societe / agence / groupe), transfert de ligne par
  contre-passation, extras, forfaits a ventilation equilibree, acomptes, politiques d'annulation
  FIGEES dans le dossier ;
- exploitation : date metier hoteliere, night audit idempotent, previsionnel avec ADR et RevPAR,
  planning graphique, tableaux d'arrivees, de departs et de clients presents, balayage des no-shows ;
- douze permissions fines, les cles historiques valant les cles qu'elles ont remplacees.

NON LIVRE, et il vaut mieux le lire ici que le decouvrir en production :
- **channel manager** : l'interface `IChannelManagerProvider` et son registre existent, aucun
  connecteur OTA n'est ecrit. La frontiere est posee - un fournisseur publie ce que le PMS a calcule
  et rejoue ce que le canal a vendu, il ne calcule jamais d'inventaire ;
- **moteur de reservation directe** : rien n'est livre. La conception l'oblige deja a passer par
  `ILodgingService`, donc un second moteur de disponibilite est exclu par construction ;
- **deversement comptable automatique des ecritures PMS** : les lignes de folio portent leur TVA,
  leur journee d'exploitation et leur nature, elles sont pretes ; le deversement appartient au
  module Comptabilite, qui possede le plan comptable, et le doubler ici creerait une seconde source
  d'ecritures.

### Module 29 - "Synchronisation multi-postes" livre en supervision seule

Dans le produit d'origine, chaque poste portait sa PROPRE base SQLite locale et l'API centrale etait
optionnelle : une file `sync_queue` poussait les changements vers le serveur. Le manuel source
(`docs/legacy/.../manuel-modules/synchronisation-multi-postes.md`) note d'ailleurs que la descente de
donnees n'a jamais ete operationnelle - les donnees creees sur un poste n'apparaissaient pas sur les
autres.

Cette premisse a disparu dans le depot .NET : une seule base PostgreSQL centrale par deploiement
(`docs/architecture.md`), un serveur unique par site (`docs/deployment-onpremise.md`), et un client
lourd sans aucune persistance metier locale. La coherence multi-postes est donc deja garantie par la
base : il n'y a plus rien a synchroniser.

Le module 29 est par consequent livre sous le nom **"Registre des postes & erreurs clients"**, en
supervision strictement en lecture seule :

- registre des postes declares, avec dernier contact, dernier utilisateur et version applicative ;
- detection d'une derive de version entre postes, qui est le vrai risque d'exploitation ;
- journal des erreurs que les postes signalent eux-memes, pour rendre les pannes visibles apres coup.

Ce qui n'est PAS livre, et ne doit pas l'etre en l'etat : aucune file de rejeu, aucun mode hors-ligne,
aucune persistance metier sur le poste. La raison est une question d'integrite comptable et non de
charge de travail : les routes qui creent un encaissement, une facture ou une ecriture ne portent
aucune cle d'idempotence, un rejeu differe y produirait des doublons. Aujourd'hui une coupure reseau
fait perdre une action BRUYAMMENT et l'operateur la refait ; une file transformerait cela en doublon
silencieux. Le mode hors-ligne reste par ailleurs explicitement differe par `docs/architecture.md`
tant que le pilote client-serveur n'est pas stabilise.

### Module 10.6 - complete depuis : le volet groupes est livre

La section ci-dessous decrivait un module livre a moitie. Ce n'est plus le cas : les allotements et
les rooming lists ont ete ajoutes, et le module est passe Disponible.

Ce qui rendait ce volet delicat, et comment il est traite : un allotement retire des chambres de la
vente SANS les nommer. La recherche de disponibilite en soustrait le solde ET le garde de creation
de reservation refuse de l'entamer, par un CALCUL UNIQUE partage
(LodgingService.GetAllotmentHoldsAsync). Les laisser diverger etait le risque principal - une
recherche plus stricte que la creation aurait fait survendre l'hotel en silence. Un test parcourt
toutes les tailles de bloc et verifie que le nombre de chambres proposees egale exactement le
nombre de reservations que la creation accepte.

Deux regles portent le reste :
- une chambre prise SUR le bloc le consomme et ne reduit pas une seconde fois l'inventaire public ;
- liberer un bloc rend le SOLDE, jamais les nuitees deja vendues.

La section historique est conservee ci-dessous : elle documente l'etat au moment ou le module a ete
livre en deux temps, et la raison pour laquelle le second temps a ete separe du premier.

### [Historique] Module 10.6 - livre a moitie dans un premier temps

Le catalogue source annonce six fonctions : rooming lists, allotements, evenements et salles,
devis, BEO, facturation evenementielle. Quatre sont livrees, deux ne le sont pas, et le module porte
le statut **Partiel** plutot que Disponible pour que le tableau d'avancement ne mente pas.

LIVRE - tout ce qui porte sur les SALLES :
- referentiel des espaces de reception (capacite, surface, activation) ;
- evenements avec garde anti-double-reservation sur la fenetre REELLE d'occupation, montage et
  demontage compris ;
- devis chiffre par lignes, aux taux de TVA que la facturation accepte ;
- deroule operationnel BEO, qui reste modifiable apres facturation ;
- facturation evenementielle, produite PAR le module Facturation et non par une seconde
  implementation.

NON LIVRE - tout ce qui porte sur les CHAMBRES :
- allotements (bloc de chambres tenu pour un groupe) ;
- rooming lists (affectation nominative des chambres du bloc).

La raison n'est pas la charge de travail mais l'integrite. Un allotement retire des chambres de la
vente : il devrait etre soustrait A LA FOIS a la recherche de disponibilite et au garde de creation
de reservation. Livrer un allotement que ces deux chemins ignorent ferait survendre l'hotel en
silence - le systeme afficherait des chambres libres qui sont en realite promises a un groupe. Cela
se joue au coeur du PMS (LodgingService.GetAvailabilityAsync et CreateReservationAsync) et merite sa
propre passe, avec ses propres tests de concurrence.

Une salle de reception, elle, n'est PAS une chambre : elle se vend au creneau et non a la nuitee,
n'entre ni dans la disponibilite ni dans le taux d'occupation. C'est cette separation qui a permis
de livrer le volet evenementiel sans toucher au coeur reservation.

Point de securite : la route de facturation d'un evenement exige mice.write ET invoices.write. Sans
cela, mice.write serait devenu un chemin detourne pour creer des factures sans en avoir le droit.
