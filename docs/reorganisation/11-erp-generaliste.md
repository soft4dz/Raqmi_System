# 11 — Passer d'un ERP hôtelier à un ERP « tout type d'entreprise »

Avis d'architecte. Branche mesurée : `reorg/phase-1` (`da196a7`), lecture seule, aucun build ni test lancé ;
toutes les mesures ont été recomptées par moi-même sur cette révision.

**Révision 2 — après contradiction.** Quatre chiffres de la version 1 étaient faux, tous dans le sens qui
arrangeait ma conclusion : A chiffré à son meilleur cas et C à son pire, dans le même tableau ; manifeste et
licence donnés « à créer » alors qu'ils existent, écrits et testés, dans l'historique de ce dépôt ; 60 jours de
conformité « propres à la généralisation » dont quatre postes déjà dus à l'hôtel ; et **zéro jour-propriétaire**,
alors que c'est le goulet n°1 du plan 10. Corrigés, ils **ne renversent pas la recommandation, ils en déplacent
le fondement** : ce n'est plus « l'hôtel est plus sûr », c'est « aucun secteur n'a de client, donc on garde le
socle neutre au prix le plus bas et on livre le premier qui signe ».

**Unités** (plan 10 §2, jamais mélangées) : **j-a outillé** (1 j-a = 1,2 h de cycle) ; **j non comprimé**
(conformité, facteur 1, 3,5 j/agent/sem.) ; **j-h** (propriétaire, 3,5 j-h/sem.). « j-d » et « semaines-agent »
de la version 1 sont des unités abrogées par le plan 10 §7 : tout est reconverti.

---

## 1. Réponse directe

**Si je n'avais qu'une phrase : votre socle est déjà générique aux deux tiers, ce n'est donc pas la
généralisation qui coûte cher — c'est que la chaîne de vente et le déversement comptable n'existent pour
personne, pas même pour l'hôtel, et qu'aucun client n'est identifié nulle part ; finissez la chaîne, gardez le
socle neutre au prix le plus bas, livrez le premier qui signe.**

| Question | Réponse |
|---|---|
| Est-ce possible ? | Oui. 62 à 66 % du Domain, 68 % des routes, **77 à 80 % des tests** et **72 à 81 % des écrans** ne portent aucune règle hôtelière. |
| Le code est-il « tissé » d'hôtellerie ? | Non, au niveau où ça coûte. **Une seule** clé étrangère part d'un schéma générique vers l'hôtelier (`crm.satisfaction_entries.reservation_id → lodging.reservations`). Le reste est du vocabulaire. |
| Alors qu'est-ce qui manque ? | Le geste central de toute entreprise : vendre un article, le sortir du stock, le facturer, l'imprimer, le comptabiliser. `InvoiceLine` n'a pas d'`ItemCode`, `StockMovementKind` n'a pas de nature « vente », aucun module ne déverse d'écriture, zéro bibliothèque PDF sur 21 paquets. |
| Ces manques sont-ils hôteliers ? | **Non.** Ils manquent déjà à l'hôtel et sont déjà au plan (A4, A5, B3, B4, B9, B10, B11). Généraliser ne détourne pas du plan : c'est le même dernier mètre. |
| Quel est le vrai point de bascule ? | **Aucun client n'est identifié, ni hôtelier ni autre** (plan 10 l. 179). Rester hôtelier est donc un pari commercial, exactement comme généraliser. La différence n'est pas « pari contre certitude », c'est « un pari contre plusieurs ». |
| Effet calendrier si l'on généralise MAINTENANT | **56 à 61 semaines** avant le premier client — recette et mise en service comprises (§8). |
| Effet si l'on reste hôtelier | **45 à 46 semaines à l'état par défaut** (branche B du plan 10, 44 sem., + 1 à 2 de renommage) ; 34-35 seulement si un hôtel mono-établissement **et vide** est identifié avant la fin de la semaine 8 ; jusqu'à 53 s'il n'est pas vide. |
| Ce que je tranche | Socle rendu neutre tout de suite (renommage, manifeste, licence — tous portables dans le temps mort des agents). Cible produit : **le premier client signé**, sans préjuger du secteur. Deuxième **paquet sectoriel** ouvert seulement contre un client signé — règle appliquée aussi à l'hôtel. |
| Ce que ça coûte au propriétaire | **+24 j-h** au-delà des 96 j-h du plan 10, soit un plancher qui passe de 27 à **≈ 34 semaines** (§5). C'est le seul poste que ni les agents ni un budget ne compriment. |

---

## 2. Ce que vous avez déjà

LOC = lignes physiques (`git show | wc -l`, commentaires compris) ; total Domain mesuré **21 617 lignes**.

### 2.1 Socle transverse — utilisable pour n'importe quelle entreprise

| Contexte | LOC | État réel | Tel quel ? |
|---|---:|---|---|
| Accounting (SCF) | 951 | Partie double imposée en C# et en SQL, 7 classes SCF, aucun plan de comptes livré (choix assumé). Zéro `hotelUnitCode`. | Oui — mais personne ne l'alimente |
| Identity / RBAC | 1 222 | PBKDF2 310 000 itérations, rotation de jetons, 175 permissions dont 47 clés hôtelières (catalogue, pas moteur), 7 rôles génériques. | Oui |
| HumanResources | 2 051 | Paie algérienne (CNAS, AT, chômage, formation, IRG, SNMG), CDI/CDD/saisonnier/apprentissage. 7 `hotelUnitCode`, tous sur `Employee`. | Oui, sous réserve B8 (formule IRG fausse) |
| Inventory | 762 | PMP, multi-dépôts, mouvements immuables, inventaires physiques. **Ne connaît pas la chambre.** `LotNumber` et `ExpiryDate` optionnels existent déjà sur `StockMovement`. | Oui pour acheter/consommer ; **non pour vendre** |
| Purchasing | 650 | Fournisseur, commande, approbation, réception partielle déversée en stock. Zéro `hotelUnitCode`. | Oui — sauf TVA (voir §3) |
| Billing | 640 | `Customer{Company, Individual, PublicEntity}` + NIF/RC/AI/NIS ; **12 snapshots d'identité fiscale figés à l'émission** ; TVA {0, 9, 19}. | Oui pour la forme, non pour la chaîne |
| Treasury / Closing | 462 / 148 | Banques, caisses, encaissements, ordres de paiement sous approbation ; verrouillage d'une journée d'exploitation avec réouverture motivée. Rien d'hôtelier hors un commentaire. | Oui |
| Approvals | 556 | Circuits, instances, décisions, port `IApprovalGate`. Mais `ApprovalSubjectType` n'a **qu'un membre** : `PaymentOrder`. | Oui, à câbler |
| Crm | 927 | Segments, fidélité, campagnes avec consentement, NPS. `GuestProfile` se clé sur `Customer` : « Guest » est une étiquette. | Oui après découplage (§5) |
| Kpi | 3 168 | Moteur (`KpiMath`, snapshots, seuils, portées) totalement générique ; seul le catalogue est teinté (32 codes hôteliers sur 86). | Moteur oui, catalogue à scinder |
| Budgeting / Revenue | 350 / 190 | Workflows génériques, **modèle hôtelier** (4 colonnes figées Accommodation/Food/Beverage/Other). | Non sans refonte |
| Receivables | 209 | Balance âgée, relances 3 niveaux, risque client. | Oui, mais inexploitable en B2B (pas d'échéance) |
| Settings / Organization / Audit / Common / Sync / Reporting | 259 / 93 / 57 / 36 / 307 / 235 | Paramétrage (singleton `GLOBAL`), référentiel d'unités, piste d'audit, base d'entité, postes de travail, 5 rapports. | Oui, avec les limites du §3 |
| Kitchen | 570 | **Aucun couplage hôtelier** (0 `Room`/`Folio`/`Reservation`/`hotelUnitCode` dans les 4 couches ; les 2 occurrences sont des commentaires) : nomenclature mono-niveau + coût de revient via `IStockCostProvider`, verticale complète du Domain au XAML. *Correction de la version 1 :* j'écrivais « le seul module vendable tel quel à un client non hôtelier ». Faux, et contraire à mon §4 (restauration = 70-110 j-a) : Kitchen ne facture rien et ne sort rien du stock. C'est le seul module **entièrement découplé**, un actif porté — pas une offre. | Oui |
| Tariffs | 517 | Écrit en nuits : `RoomTypeCode`, `NightlyAmount`, `BoardType` importé de `Lodging`. Générique de concept, hôtelier de code. | **Non sans refonte** |
| **Total transverse après refonte** | **14 360** | somme des lignes ci-dessus | **66,4 %** |
| **Total transverse net** | **13 303** | idem **moins** Budgeting (350), Revenue (190) et Tariffs (517), que ce même tableau déclare « non sans refonte » | **61,5 %** |

**61,5 % est le chiffre opposable, pas 66,4 %** : on ne compte pas comme réutilisable ce que le §5 rechiffre à 2,5-4 semaines de reprise. Seul endroit où la version 1 était trop généreuse.

### 2.2 Irréductiblement hôtelier

| Contexte | LOC | Routes | Verdict |
|---|---:|---:|---|
| Lodging (PMS) | 5 518 | 96 | Aucune réutilisation hors hébergement. À isoler derrière des ports. |
| Mice | 1 049 | 23 | Hôtelier. La moitié « salles + devis » serait réutilisable, mais elle est soudée à l'allotement de chambres. |
| Housekeeping | 690 | 19 | Hôtelier, 3 FK en dur vers `lodging`. |
| **Total** | **7 257** | **138** | **33,6 % du Domain, 32 % des routes** |

### 2.3 Les cinq assiettes de réutilisation — et pourquoi les deux dernières comptent le plus

| Assiette | Transverse | Part | Méthode |
|---|---|---:|---|
| Domain (lignes physiques) | 13 303 / 21 617 net, 14 360 brut | **61,5 à 66,4 %** | §2.1 |
| Routes API | 293 / 431 | 68 % | 33 fichiers d'endpoints |
| Tables | 63 / 101 | 62 % | 19 schémas |
| **Tests `[Fact]`/`[Theory]`** | **728 à 760 / 947** | **77 à 80 %** | 947 attributs sur 129 fichiers ; 219 tests dans 25 fichiers hôteliers au sens large (Lodging, Mice, Housekeeping, Tariffs, RoomAllotment, BedConfiguration, DailyRevenue, KPI F&B/Lodging, HotelUnit, UnitDashboard), 187 au sens strict |
| **Écrans WPF** | **23 à 26 / 32** | **72 à 81 %** | 32 `.xaml` ; hôteliers : Lodging, Mice, Housekeeping, Tariffs, Pms, PmsPrompt, et selon le découpage DecCockpit, GroupDashboard, Closing |

Les deux dernières manquaient à la version 1 et déplacent le débat : **l'actif transféré à un autre secteur n'est pas « 13 303 lignes de domaine », ce sont 728 à 760 tests écrits et 23 à 26 écrans en service** — la partie la plus chère à reconstituer.

---

## 3. Ce qui manque à TOUT ERP généraliste

**Règle d'unité (plan 10 §2) : tout point A\*/C\*/D\* est du j-a outillé (× 1,2 h) ; tout point B\* est du
j non comprimé (facteur 1).** Valeurs reprises du plan 09 quand il les chiffre, estimées sinon.

### 3.1 Manque déjà en hôtellerie — donc déjà au plan, rien à ajouter pour généraliser

| Bloc absent | Preuve mesurée | Charge | Réf. |
|---|---|---:|---|
| Aucun module ne déverse d'écriture comptable | `IAccountingService` référencé par ses seuls endpoints + `DependencyInjection` | 30-45 j **non comprimés** | B10 |
| Le folio ne devient pas une facture | `Folio.AttachInvoice()` : **aucun appelant** ; seul MICE facture (`MiceService.cs:704`) | 6,5 j-a outillé | A4 |
| Aucun document ne s'imprime | 0 bibliothèque PDF/Excel sur 21 paquets ; `ReportCatalog` l'écrit lui-même | 27 j-a outillé | A5 |
| Avoir absent (`Invoice.Cancel()` accepte une facture émise) et droit de timbre | 0 fichier `CreditNote` ; 0 occurrence « timbre » dans `src` | 16 + 13 j **non comprimés** | B3, B4 |
| TVA déductible : `PurchaseOrderLine` n'a **aucun champ TVA** → G50 incalculable | 11 propriétés lues, aucune `VatRate` | mois, **non comprimé** | B9 |
| États financiers SCF (bilan, compte de résultat) et immobilisations | seuls balance, grand livre et balance auxiliaire existent ; `FixedAsset` → 0 fichier ; aucun à-nouveau | 28-40 + 20-28 j **non comprimés** | B11, B13 |
| Périmètre utilisateur ↔ établissement | `SecurityClaimTypes` n'a qu'une constante `"permission"` | 39 j-a outillé | A6 |
| Identité fiscale par établissement | `ApplicationSettings` = singleton `"GLOBAL"` ; `HotelUnit` ne porte ni NIF ni RC | 11 j **non comprimés** | B1 |
| Reprise de données ; session de caisse / encaissement comptoir | 0 occurrence `import|bulk|upload` sur 33 fichiers d'endpoints ; `CashSession` → 0 fichier | 20-35 + 13 j-a outillé | C10, A7 |

### 3.2 Ne manque QUE pour généraliser — le vrai surcoût de la demande

| Bloc | Preuve mesurée | Charge |
|---|---|---:|
| **Catalogue d'articles vendables** — `InvoiceLine` = `Designation` libre, aucun `ItemCode` ; `StockItem` n'a ni prix de vente, ni TVA, ni code-barres | lu ligne à ligne | 20-30 j-a |
| **Sortie de stock sur vente** — `StockMovementKind` = 5 natures, aucune vente ; `IStockOperationService` injecté par `PurchasingService` seul, jamais par Billing | vérifié | 15-25 j-a |
| **Échéance et conditions de règlement** — `AgingCalculator` documente lui-même l'absence : « the Invoice aggregate carries no due date » | lu | **5-10 j non comprimés** + 1 migration |
| Devis, commande client, bon de livraison génériques | `Quotation`/`SalesOrder`/`DeliveryNote` → 0 fichier | 35-50 j-a |
| Facture fournisseur, avoir fournisseur, rapprochement 3 voies | `SupplierInvoice` → 0 fichier | 30-45 j-a |
| Comptabilité analytique / centres de coûts ; devises et taux de change | `CostCenter` → 0 fichier ; `CurrencyLabel` n'est qu'un libellé sur le singleton, `ExchangeRate` → 0 | 15-25 + 15-25 j-a |
| Référentiels centraux (pays, wilayas, unités de mesure, formes juridiques) | `UnitOfMeasure` = chaîne libre. *Les 13 formes juridiques algériennes existent déjà, rédigées, à `a3cc00b^:apps/client/src/lib/legalForms.ts`* | 10-15 j-a |
| GED / pièces jointes ; workflow multi-sujets | aucune persistance de fichier dans le dépôt ; `ApprovalSubjectType` = 1 membre — le fichier écrit lui-même que brancher un sujet coûte « un membre ici plus un appel de porte » | 25-35 + 10-20 j-a |

**Socle généraliste commun ≈ 180 à 280 j-a outillé, plus 5 à 10 j non comprimés** (l'échéance et les conditions de règlement, seul poste réellement propre à la généralisation).

> **Correction de la version 1.** J'annonçais « ≈ 60 j de conformité non comprimés en propre (échéance, avoir,
> timbre, TVA déductible, déversement) ». Quatre de ces cinq postes — B3, B4, B9, B10 — figurent au §3.1 : ils
> sont **déjà dus à l'hôtel**, et les compter deux fois gonflait le surcoût d'un facteur 6 à 12. **Il est de 5
> à 10 jours, pas de 60** — ce qui affaiblit la prudence que je recommandais. En sens inverse, la conformité
> algérienne est en partie **sectorielle** (négoce, situation de travaux BTP et note d'honoraires n'ont ni les mêmes mentions ni le même fait générateur de TVA), et cette part n'est vérifiable nulle part depuis le dépôt.

---

## 4. Ce qui manque PAR SECTEUR

Classement du plus proche au plus lointain. « Charge outillée » = **en plus** du tronc commun du §3.2.
**La dernière colonne est la seule qui commande le calendrier** : elle n'accélère jamais et n'est pas lisible depuis le dépôt. Appliquer le facteur 1,2 h/j-a à la colonne de gauche fait se tromper d'un facteur 10 sur la partie qui compte ; un « inconnu » explicite protège mieux la décision qu'un total d'apparence mesurée.

| Rang | Famille | Ce qui existe déjà | À écrire | Charge outillée (j-a) | **Dont non comprimé (conformité)** |
|---:|---|---|---|---:|---|
| 1 | **Services, professions libérales** | Facture autonome à lignes libres, Customer, Receivables, Treasury, SCF, paie | Mission/affaire, feuille de temps, retenue à la source | **25-40** | **Inconnu — expert-comptable.** Mentions de la note d'honoraires, retenue à la source (assiette, taux, déclaration) |
| 2 | **Restauration** (hors hôtel) | **Kitchen complet** (nomenclature + coût via PMP), Inventory, HACCP | POS, plan de salle, KDS | 70-110 | **Inconnu.** Régime du ticket de caisse, TVA restauration, HACCP réglementaire |
| 3 | **Enseignement privé** | Customer `Individual`, Receivables (relances 3 niveaux), HR complet | Élève, inscription, échéancier, notes | 50-80 | **Inconnu.** Régime TVA de la scolarité, agrément, données de mineurs (18-07) |
| 4 | **Négoce, distribution B2B** | Inventory (PMP, multi-dépôts), Purchasing → stock, Supplier NIF/RC | Toute la moitié aval : tarifs, remises, BL, marge | 60-90 | **Inconnu, et gros.** Mentions de la facture de négoce, **G50 (B9) exigé exactement comme à l'hôtel** |
| 5 | **Commerce de détail à caisse** | Treasury (encaissements, modes), Inventory par dépôt, RBAC | POS complet, périphériques | 90-130, dont A7 partagé avec l'hôtel | **Inconnu, et gros.** Obligations du ticket, X/Z, inaltérabilité de la caisse |
| 6 | **Immobilier, location** | `Folio`/`FolioCharge`/`Deposit` — mais enfermés dans Lodging | Bail, lot, indexation, mandat | 80-120 | **Inconnu.** Enregistrement du bail, IRG foncier, séquestre du dépôt de garantie |
| 7 | **Agroalimentaire** | HACCP (Kitchen), `LotNumber` + `ExpiryDate` **optionnels** sur `StockMovement` | Lot comme agrégat, FEFO, solde par lot, rappel | 150-220 | **Inconnu, et gros.** Traçabilité amont/aval opposable, procédure de rappel, DLC |
| 8 | **Industrie, transformation** | Nomenclature mono-niveau (Kitchen) + `IStockCostProvider` | Multi-niveaux, gammes, OF, ordonnancement | 180-280 | **Inconnu.** Valorisation SCF des en-cours et des produits finis |
| 9 | **Transport, logistique** | HR (chauffeurs), Inventory (pièces), Purchasing (carburant) | Flotte, tournées, GMAO (`Application.Maintenance` = sauvegarde !) | 150-230 | **Inconnu.** Lettre de voiture, autorisations de transport, temps de conduite |
| 10 | **Import-export** | Purchasing, PMP | Multi-devises transverse (111 colonnes monétaires), douane | 130-200 | **Inconnu, et gros.** Domiciliation bancaire, D10/D41, incoterms, change |
| 11 | **BTP** | Rien de spécifique | Un ERP dans l'ERP, adossé à l'analytique absente | 200-300 | **Inconnu, et gros.** Situation de travaux, retenue de garantie, TVA sur encaissement, DGD |
| 12 | **Santé, pharmacie** | Rien de spécifique | Tout, **plus** la loi 18-07 opérationnelle (B14 non implémenté) | 180-280 | **Inconnu, et bloquant.** Données de santé sous 18-07, tiers payant, traçabilité du lot obligatoire |
| 13 | **Agriculture** | `ContractType.Seasonal`, paie, Budgeting | Tout, adossé à l'analytique absente | 150-230 | **Inconnu.** Régime fiscal agricole, CASNOS des saisonniers |

**Atteignables en premier : 1 (services) puis 2 (restauration).** `Customer{Company, Individual,
PublicEntity}`, le NIF à 15 chiffres normalisé en source unique, les 12 instantanés d'identité figés à
l'émission et les taux {0, 9, 19} ne portent **aucune notion de chambre ni de nuit** : une société de services est servie par le code existant plus une entité mission, sans objet de stock, de caisse ni de production. La restauration est déjà servie en back-office par Kitchen ; il ne lui manque que la caisse, qui manque aussi à l'hôtel (A7). **Mais aucun des deux n'est livrable sans sa colonne de droite, et elle est vide de mesures.**

---

## 5. Le travail de généralisation du socle

| Lot | Mesure exacte | Risque de casse | Coût agent | **j-h** |
|---|---|---|---:|---:|
| **Renommer `HotelUnit` → `Establishment`** et ouvrir `HotelUnitType{Hotel, Residence, BeachClub, Marina, Other}` en référentiel | *Recompté :* **1 979 lignes dans 381 fichiers** de `src`+`tests` hors migrations (2 310 occurrences), dont **46 fichiers et 375 occurrences dans le seul WPF** ; mais **1 736 des 2 310 (75 %) sont le seul identifiant `HotelUnitCode`** et la queue tient en dix identifiants, tous vus par le compilateur | Faible en C# ; **réel** sur le contrat HTTP (431 routes) et sur les 31 écrans WPF, non couverts par un test — `RaqmiSystem.Tests.csproj` ne référence **pas** `RaqmiSystem.Desktop`, et c'est le seul `.csproj` sous `tests/` | **3-5 j-a + provision de reprise WPF de 3 j-a** | 2 |
| Descendre `HotelUnit.NormalizeCode()` dans `Common` ; ouvrir `StockItemCategory` (enum figé de 5 valeurs hôtelières) en table de familles ; détacher Kitchen du pack hôtelier | appelé depuis 33 fichiers Domain + 6 Infrastructure ; enum → table ; Kitchen déjà sans couplage | Nul à faible, 1 migration | 1 h + 2 j-a + 0,5 j-a | 1,5 |
| Découpler Crm ↔ Lodging | 1 FK (`SatisfactionEntryConfiguration.cs:81`) + 1 requête directe `Set<Reservation>()` dans `CrmService` | Faible, 1 migration | 5 j-a | 0,5 |
| Scinder `KpiCatalog` (socle / pack hôtelier) | 86 définitions, 32 codes hôteliers, 15 rattachées à Lodging/Housekeeping, 24 déjà en `None` | Nul (aucune migration : les définitions sont du code) | 4-6 j-a | 1 |
| Refondre Revenue + Budgeting | 4 colonnes figées + `BudgetCategory` qui les reflète (le commentaire l'assume) | Moyen : 1 migration avec reprise, 3 écrans, 30-40 tests | 5-7 j-a | 2 |
| Tariffs | `RoomTypeCode`, `NightlyAmount`, `BoardType` importé de `Lodging`, jusque dans la signature du port | Moyen : 3 tables, 18 routes, 4 fichiers de test | 7 j-a — **ou 3-4** en laissant Tariffs à l'hôtel et en écrivant une grille de prix neuve (517 lignes seulement) | 1 |

**Total : ≈ 27 à 36 j-a outillé (6 à 8 semaines d'un agent), 0 j non comprimé, 8 j-h.** C'est peu — et c'est
précisément pourquoi ce n'est pas là qu'est le sujet.

**Paramétrage sectoriel — et la page blanche qui n'en est pas une.** Dans `reorg/phase-1:src/`, `BusinessType`,
`Sector`, `EnabledModules` et `FeatureFlag` donnent bien **0 fichier**. Mais le modèle complet a été conçu,
écrit, signé et testé dans **ce dépôt**, en TypeScript/Prisma, puis effacé le 27 août. Vérifié à `a3cc00b^` :
`packages/shared/src/modules.ts` (23 modules typés, `family: core|finance|hr|operations|specific|system`,
drapeau `commercial`, + son test) ; `packages/licensing/src/` (crypto, packs, policy, fichier, empreinte
serveur, deux fichiers de tests) ; `require-module.ts` (`requireModule` → `evaluateLicense`, lecture seule,
grâce hors ligne) ; `apps/license-manager/` (Electron + `license-sign.cjs`) ; `Site{type}`,
`ModuleDefinition{family, commercial}`, `License{kind, mode, maxUsers, maxSites, maxStorageGb,
offlineGraceDays, signedPayload, signature}`, `LicenseModule{enabled}` ; et `RAQMI_LICENSE_PACKS` (Starter /
Professional / Enterprise). **Autre pile, donc non transposable tel quel — mais la conception est faite et
validée par vous** : le §6 ne chiffre plus une création, il chiffre un **portage**.

**Reste ce qui n'est pas du code : sept arbitrages, tous à votre charge.** Manifeste de paquets (3 j-h, en
reprenant `license-packs.ts`) ; type d'établissement, familles d'articles et de recettes (4) ; **séries de
documents et mentions obligatoires par secteur, avec l'expert-comptable (5)** ; catalogue KPI actif et
permissions seedées par paquet (4). **16 j-h + les 8 ci-dessus = 24 j-h à ajouter aux ≈ 96 j-h du plan 10 :
à 3,5 j-h/semaine, le plancher passe de 27 à ≈ 34 semaines.** La version 1 employait 50 fois « j-d » et
**zéro fois « j-h »** : généraliser est d'abord un travail de décision, pas de code.

---

## 6. L'architecture

**Patron retenu : (a) noyau générique + paquets métier activables, en monolithe modulaire à manifeste.** Une
seule base, une seule chaîne de migrations, un seul binaire ; le paquet est une notion d'exécution, de
navigation et de licence — **jamais** une notion de schéma. Pourquoi pas les autres : 6 projets, un seul
`Domain`, un seul `Application`, un seul `Infrastructure`, un `ApplyConfigurationsFromAssembly` unique —
aucune frontière d'assembly ne peut porter un « produit distinct » ni un plugin.

| Ce qui bloque aujourd'hui | Mesure | Levée |
|---|---|---:|
| Trois catalogues à compteurs en dur ; `TabIndex` positionnel dans un `MainWindow.xaml` de 31 onglets, relu **par position** par le garde CI | `ModuleCatalog` : 50/31/19 gardés par constructeur statique ; `FunctionalArchitectureCatalog` : 22/50, **jette** si le compte diffère ; retirer un écran renumérote 50 entrées et casse `screens.json` | 10-14 + 15-20 j-a, le second conditionné à un projet de tests Desktop (D6) |
| **Le noyau lit les paquets** | `KpiFactLoader` charge `RoomBlock`/`HousekeepingTask` ; `CrmService` importe `Domain.Lodging`. **Désactiver l'hôtel casse aujourd'hui la bibliothèque KPI, le cockpit DEC et le tableau PDG.** Tant que ce point n'est pas levé, « paquet désactivé » veut dire « menu masqué » | 20-30 j-a (ports déjà nommés au doc 05 ; `KpiAvailability.AwaitingSource` existe déjà) |
| 30 groupes d'endpoints mappés sans condition | 138 des 431 routes sont hôtelières | 3-5 j-a — le point le plus facile |
| Aucune licence **dans `src`** | `LicenseFeature` déclaré sur chaque nœud et **nul partout** ; `LicenseAllows` = `_ => true` (`NavigationTreeBuilder.cs:38`, appliqué l. 216 avec `ScopeAllows`). **La prise existe et est déjà câblée ; elle n'est branchée sur rien.** Conception disponible à `a3cc00b^` (§5) | **portage : 8-12 j-a** (au lieu de 10-15 en création) |

**Étapes et re-chiffrage.** V0 renommage (3-5 j-a + 3 de reprise WPF) → V1 manifeste + licence + routes
conditionnelles : **15-25 j-a** en portage de la conception `a3cc00b^`, au lieu des 25-35 annoncés en version 1
sur une hypothèse de page blanche fausse → V2 identifiant d'écran stable (15-20 j-a) → V3 inversion des
dépendances + coupure de la FK CRM (20-30 j-a) → V4 paquet Ventes (§3.2). **Où les loger sans allonger le
calendrier :** le plan 10 §5 écrit que « les agents finiront leurs séries outillées en quelques heures et
attendront » pendant les semaines 5-8 et 12-17, dimensionnées par de la conformité. **V0 à V3 sont du j-a
outillé pur : ils tiennent dans ce temps mort.** Leur coût réel n'est pas du calendrier, c'est du
jour-propriétaire — la seule raison sérieuse de ne pas tout avancer d'un coup.

**Point de non-retour — quatre seuils, et un seul est technique :**

| Seuil | Ce qui se ferme |
|---|---|
| **Fin du lot de sauvetage** (sem. 2-3 du plan 10 §5) | *Seuil ajouté.* Renommer 381 fichiers pendant le rebase des 4 lots dormants (113 fichiers, 55 suivis modifiés) est le scénario de conflit maximal, sur le seul programme dont on connaisse le taux de perte : **4 lots sur 4**. Le renommage attend la fusion verte. |
| **Semaine 5** (lancement d'A6a) | A6 et B1 se construisent sur `HotelUnit`. Après : la reprise est payée deux fois (facteur 2 à 3, avis 08). **C'est un seuil d'intégration, pas de technique.** |
| **Semaine 11** (A5) | Le gabarit de facture part en relecture d'expert-comptable : ses champs sont figés par une validation externe. |
| **Semaine 27** (mise en service) | *Correction de la version 1.* Ce seuil ne ferme **que le renommage physique des 33 colonnes**, qui est **facultatif et invisible du client** : les 99 fichiers `*Configuration.cs` portent **1 320 appels `HasColumnName`**, chaque colonne est mappée explicitement (`.HasColumnName("hotel_unit_code")`) et `OnModelCreating` n'installe **aucune convention snake_case**. Propriété C# et nom de colonne sont découplés par construction. **Le renommage du vocabulaire C#/HTTP/WPF reste donc possible après la mise en service** — il ne produit aucune migration. Ce qui se ferme vraiment ici : 22 migrations en production, `REVOKE UPDATE/DELETE` (B12), conservation légale, aucun script de mise à jour (A3). |

---

## 7. Ma recommandation de séquence

**Ce que je ferais :**

| Quand | Quoi | j-h *(sur les 24 du §5)* | Pourquoi |
|---|---|---:|---|
| **Cette semaine — coût agent nul** | Inspecter les 11 worktrees `worktree-agent-*` (le lot A6 y est-il déjà ?) ; relire `a3cc00b^:packages/shared/src/modules.ts`, `license-packs.ts` et le `schema.prisma` **avant** de rédiger le manifeste ; masquer ou dater les **19 modules « Planifié »** ; **ouvrir la prospection sur tous les secteurs, pas seulement l'hôtellerie** | 2 | Une demi-journée lève l'inconnue de la fenêtre V0. Le manifeste ne se rédige pas deux fois. Les 19 portes vides (38 % du catalogue) sont le seul point où le dépôt vous contredit en démonstration. |
| **Semaines 3-4, après fusion verte du lot de sauvetage — pas avant** | Renommer `HotelUnit` → `Establishment`, ouvrir le type d'établissement, descendre `NormalizeCode` dans `Common`, en **lot exclusif** | 2 | *Correction de la version 1, qui l'ordonnait « cette semaine ».* 381 fichiers renommés pendant le rebase de 113 est le scénario de conflit maximal sur un programme à 4 lots perdus sur 4. Le geste reste techniquement réversible (§6), mais son coût d'intégration, lui, monte avec chaque écran livré. |
| **Semaines 1 → 45** | Exécuter la branche du plan 10 jusqu'au bout sur **le premier client signé**, hôtelier ou non | — | *Correction de la version 1 :* j'écrivais « aucune de ces 33 semaines n'est perdue ». Faux comme absolu. **Vrai :** 10 des 43 points de l'avis 08 sont du socle transverse et se rejouent partout (A1, A4, A5, A6, A12, B1, B3, B4, B6, B7). **Faux :** **11 semaines sur les 33 de la branche A** (R = recette 4 sem., M = mise en service 7 sem.) sont dépensées sur un site nommé, et **B2a** (identité de l'hébergé) et **B5** (taxe de séjour) sont hôteliers et perdus ailleurs. |
| Dans le temps mort des agents (sem. 5-8, 12-17) | V0→V3 : catalogues sans compteurs, manifeste + **portage de la licence `a3cc00b^`**, identifiant d'écran stable, inversion `Kpi`/`Crm` → `Lodging` | 6 | Du j-a outillé pur, hors chemin critique (§6). Le fait **mesuré** qu'il n'y ait aucune licence branchée est ce qui rend aujourd'hui impossible de montrer l'ERP sans l'hôtel. |
| Inséré entre B3 et la recette | Business Event + Outbox + Posting Engine, branchés sur **deux producteurs internes** (facture Billing, encaissement Treasury) | 2 | L'épine dorsale se prouve par un deuxième consommateur, pas par un deuxième secteur. Spécifiée au doc 03 §5.4, **absente de `src`** |
| **Contre un client signé, et seulement là** | Le paquet de son secteur — Services d'abord (échéance + mission + temps), puis Ventes & Négoce, puis POS | — | La règle vaut **dans les deux sens** : pas de paquet Négoce sans négociant signé, **et pas de mise en service hôtelière sans hôtel signé**. |

**Ce que je ne ferais pas :** ouvrir plusieurs chantiers sectoriels ; découper le `DbContext` ou la chaîne de
migrations (40-60 j-a, deux historiques, matrice de test doublée, sans script de mise à jour) ; simuler
l'activation d'un paquet en retirant des permissions (le RBAC dit « qui a le droit », pas « ce qui est vendu ») ;
promettre BTP, industrie, santé ou transport avant qu'un lot de conformité ait été livré et mesuré.

**La symétrie que la version 1 n'appliquait pas.** J'exigeais « un client signé » pour le deuxième secteur et
tenais le pilote hôtelier pour acquis. Or **aucun hôtel n'est identifié** (plan 10 l. 179) et la file pilote
coûte 7 à 14 semaines : les deux côtés sont des paris non couverts. La recommandation n'est donc pas
« l'hôtel est plus sûr », c'est : **un pari sur un secteur coûte moins qu'un pari sur plusieurs, et le socle
doit rester capable d'accueillir celui qui signe.**

**Ma recommandation serait fausse si :** (1) un client **non hôtelier** signe le premier — alors le paquet
Services devient le chemin, et rien de ce qui précède n'est perdu ; (2) le facteur 1,2 h/j-a s'étendait à la
conformité — il ne l'a jamais fait, et le seul lot comptable produit à cette cadence a introduit B7 ; (3) le
propriétaire donne 5 j-h/sem. au lieu de 3,5 — le plancher tombe de 34 à 24 sem. ; (4) l'objectif réel est de
vendre une marque d'ERP — alors les 19 modules vides sont déjà le produit.

---

## 8. Ce que ça coûte

**Correction majeure de la version 1.** Elle chiffrait A à son **meilleur** cas (34-35 sem., hypothèse « hôtel
mono-établissement et vide ») et C à son **pire**, dans le même tableau, et rangeait le pari perdant de A en
note de risque — alors que le plan 10 l. 218 écrit que **« la branche B est l'issue par défaut tant qu'aucun
hôtel n'est identifié »** et l. 179 qu'aucun ne l'est. Les trois scénarios sont rechiffrés **à l'état par
défaut**, et tous incluent les 11 semaines de recette et de mise en service chez un premier client.

| Scénario | Durée à l'état par défaut | Si un hôtel mono-site **et vide** est identifié avant la fin de S8 | Ce qu'on gagne |
|---|---|---|---|
| **A — Livrer d'abord, secteur du 1ᵉʳ signataire** (+ V0-V3 dans le temps mort) | **45-46 sem.** = branche B du plan 10 (44) + 1-2 de renommage. **Jusqu'à 53** si le site n'est pas vide (C10, +4 à 8) | **34-35 sem.** | Un produit en exploitation, un socle transverse fini et payé, **728-760 tests et 23-26 écrans transférables**, l'option de généraliser intacte |
| **B — Ouvrir un 2ᵉ paquet sectoriel APRÈS** | 45-46 **+ 5-7** (V1→V3, s'ils n'ont pas tenu dans le temps mort) **+ 4-21** (tronc de vente §3.2, selon le périmètre retenu) **+ 12-17 non comprimées** (B9 G50, B11 états SCF, B13 immobilisations — **exigés d'un négociant exactement comme d'un hôtelier**) ≈ **66-91 sem.** au 2ᵉ secteur | ≈ 55-80 sem. | Deux marchés, une extraction adossée à du code qui tourne, et **une trésorerie entre les deux** |
| **C — Généraliser MAINTENANT** | **56-61 sem.** avant tout client = 33 de mécanique hors client (44 − 11 de recette et mise en service) + **12-17 de conformité généraliste** + 11 de recette et mise en service chez le 1ᵉʳ client, quel qu'il soit | 56-61 (l'hypothèse hôtelière n'y change rien) | Un discours commercial plus large ; le 2ᵉ secteur ensuite pour +6 à 10 sem. seulement |

*La ligne B de la version 1 (« +12-18 sem. ≈ 46-53 ») était irréconciliable avec le reste : 185-285 j de travail
en 12-18 semaines, quand la branche A du plan 10 met 19 semaines à livrer 137 j-a + 62 j non comprimés — et
aucune semaine de conformité, là où C en portait 12 à 17 pour les mêmes points. C'est cette asymétrie qui
portait toute la recommandation. Levée : B est plus cher que C au 2ᵉ secteur, et c'est normal — B livre un
client 10 à 16 semaines plus tôt et le finance.*

**Écart décisif, corrigé : à l'état par défaut, C coûte 10 à 16 semaines de plus que A avant le premier
client** (3 à 8 seulement si le site pilote n'est pas vide) — et non les « 22 à 27 » annoncés, qui ne
redeviennent vrais que le jour où un hôtel mono-établissement et vide est identifié : **condition datée, W8.1,
porte bloquante de B2, à trancher avant la fin de la semaine 8.** Ces 10 à 16 semaines sont de la conformité,
la seule chose que vos agents ne compriment pas. Mais le chiffre qui décide est ailleurs : **le plancher
propriétaire passe de 27 à ≈ 34 semaines** dès qu'on ajoute les 24 j-h du §5, et **aucun des trois scénarios
ne descend sous ce plancher.** Ajouter un agent ne déplace ni la conformité, ni le propriétaire.

---

## Réserves

- Aucun build, aucun test, aucune migration exécutés. Je compte **947** `[Fact]`/`[Theory]` (957 avec `[PostgresFact]`), contre 948 au dossier : comptage statique, pas un résultat vert. **Aucun écran n'est `ProductionReady`** — `tools/readiness/screens.json` le déclare lui-même (« null aujourd'hui pour tous les écrans »), et 30 `"smoke": null` y figurent.
- **Corrections de mes propres mesures, version 1 → version 2 :** le renommage porte sur **1 979 lignes dans 381 fichiers** (2 310 occurrences), et non « 1 939 dans 285 » ; **375 occurrences dans 46 fichiers WPF**, sans projet de tests Desktop. Les 99 `*Configuration.cs` portent **1 320** `HasColumnName` (un contradicteur avançait 17 669 : c'est faux, mais son argument tient — le mapping est explicite, sans convention automatique). Le total transverse du §2.1 comptait Tariffs, Revenue et Budgeting, déclarés « non sans refonte » par ce même tableau : d'où les deux totaux.
- **Ce que la version 1 ignorait et qui est vérifié :** le manifeste de paquets, la licence signée, l'activation par module et les 13 formes juridiques algériennes ont été écrits et testés dans ce dépôt, à `a3cc00b^`, puis effacés le 27 août. Le §5 et le §6 en tiennent compte. Ce code est en TypeScript/Prisma : **la conception est réutilisable, pas le code**.
- Mes LOC sont des lignes physiques (21 617 contre ≈ 19 000 au dossier) : les **ratios** valent, pas les absolus. Je compte **431 routes** et **22 migrations** (contre 419 et 20) ; écart non élucidé, sans effet sur l'analyse. **Correction au dossier :** `StockMovement` porte déjà `LotNumber` et `ExpiryDate` optionnels — mais ni agrégat Lot, ni solde par lot, ni FEFO, ni rappel.
- Toutes les charges sont des estimations calibrées sur le plan 09, **pas des mesures** : aucun lot de généralisation n'a jamais été produit ici, et le facteur 1,2 h/j-a ne s'applique qu'aux lots outillés — **jamais** à la conformité. Aucune obligation légale algérienne n'est vérifiable depuis le dépôt (SCF, TVA, TAP, IRG, G50, timbre, 18-07, retenue à la source, traçabilité, retenue de garantie) : à confirmer par un expert-comptable avant d'être opposée à un client, en particulier les mentions **sectorielles** de la pièce de vente.
- Les documents 08 à 11 ne sont versionnés sur aucune branche : à commiter. Et les 11 worktrees `worktree-agent-*` n'ont pas été inspectés — si le lot A6 « périmètre unité » y existe déjà, le renommage doit précéder son sauvetage (d'où le premier point du §7).
