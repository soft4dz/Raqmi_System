# Chaine documentaire (A5, version minimale : la facture)

## Objectif

Donner a Raqmi System sa premiere piece imprimable : **la facture de vente**, en francais, au
format A4, **imprimable et reimprimable a l'identique par son numero**. C'est la version minimale
retenue par `docs/reorganisation/10-plan-comprime.md` (point A5) : un gabarit, une langue, une
archive. Les quatre autres gabarits (note de sejour, recu, bon de commande, bulletin de paie), le
bilingue et l'archivage documentaire complet sont differes.

Le principe fondamental tient en une phrase : **un document legal emis ne se re-rend jamais**. La
premiere demande rend et archive ; toute demande suivante renvoie l'archive, octet pour octet. Le
gabarit peut evoluer, le parametrage de l'emetteur peut changer, la fiche client peut etre
corrigee : la facture remise au client reste celle qui a ete remise.

~~~
Facture emise (module Facturation, IBillingService)
        v
Modele du document (InvoiceDocumentModelBuilder - fonction pure, AUCUN montant recalcule)
        v
Rendu PDF (QuestPDF, IDocumentRenderer<InvoiceDocumentModel>)
        v
Archive (documents.rendered_documents, unique par type + reference)
        v
API application/pdf + X-Document-Sha256
        v
Desktop : DownloadInvoicePdfAsync -> DocumentPreviewWindow (ouvrir / imprimer / enregistrer sous)
~~~

## Bibliotheque et licence

| Paquet | Version | Ou |
|---|---|---|
| `QuestPDF` | `2024.10.3` (epinglee dans `Directory.Packages.props`) | `RaqmiSystem.Infrastructure` uniquement |

QuestPDF n'a aucune dependance NuGet : les natifs Skia sont embarques dans le paquet
(`win-x64`, `win-x86`, `linux-x64`, `linux-musl-x64`, `linux-arm64`, `osx-*`), ce qui couvre le
serveur on-premise Windows comme l'image Docker Linux. Ni Application ni Desktop ne le referencent :
le rendu est un detail d'infrastructure derriere le port `IDocumentRenderer<TModel>`.

**Condition de licence (a verifier a chaque changement de situation de l'editeur ou du client).**
QuestPDF est utilise sous **licence Community**, declaree au demarrage par
`QuestPDF.Settings.License = LicenseType.Community` (`QuestPdfInvoiceRenderer.ConfigureEngine()`,
appele par `AddRaqmiDocuments()`). Cette licence est gratuite pour les organisations dont le
**chiffre d'affaires annuel est inferieur a 1 000 000 USD** (ainsi que pour les projets open source
et l'usage non commercial). Le seuil s'apprecie pour l'organisation qui exploite le logiciel. Au-dela,
une licence Professional ou Enterprise est due : c'est un point a inscrire au contrat de licence
Raqmi (W8.2) et a revoir avant tout deploiement chez un groupe hotelier depassant ce seuil.
Reference : `https://www.questpdf.com/license/` (texte de la licence dans le paquet NuGet).

## Ce que le module possede en propre

Une table, aucune donnee d'exploitation :

| Table | Role |
|---|---|
| `documents.rendered_documents` | Les pieces rendues : type, reference metier, version du gabarit, SHA-256, taille, date et auteur du rendu, contenu (`bytea`) |

Contraintes tenues par la base, pas par convention :

- `ux_rendered_documents_type_reference` : **unique (type, reference)** - la regle d'immuabilite ;
- `ck_rendered_documents_type` : `type IN ('Invoice')` ;
- `ck_rendered_documents_size_bytes` : `size_bytes > 0` ;
- `ck_rendered_documents_sha256` : `length(sha256) = 64`.

L'entite `RenderedDocument` n'a ni `UpdatedAt` ni `UpdatedBy` : rien ne se met a jour. Aucune
route ne supprime ni ne remplace un document.

**Migration attendue a l'integration** (ce chantier ne cree pas de migration, la configuration EF est
ramassee par `ApplyConfigurationsFromAssembly`, et les tests construisent le schema par
`EnsureCreated`) :

~~~
dotnet ef migrations add Documents --project src/RaqmiSystem.Infrastructure --startup-project src/RaqmiSystem.Api
~~~

Elle cree le schema `documents` et la table ci-dessus. Le jeton EF du plan de reorganisation
s'applique (une migration a la fois, serialisee a l'integration).

## Architecture

| Couche | Contenu |
|---|---|
| Domain | `RaqmiSystem.Domain.Documents` : `DocumentType`, `RenderedDocument` (fabrique `Create`, SHA-256, normalisation de la reference, garde « c'est un PDF ») |
| Application | `RaqmiSystem.Application.Documents` : `InvoiceDocumentModel` (+ `DocumentPartyModel`, `InvoiceDocumentLineModel`, `InvoiceVatSummaryModel`), `InvoiceDocumentModelBuilder`, `InvoiceDocumentDefaults`, ports `IDocumentRenderer<TModel>`, `IDocumentArchive`, `IDocumentService`, reponses `DocumentMetadataResponse`, `DocumentContentResponse`, constante `DocumentHeaders.Sha256` |
| Infrastructure | `RaqmiSystem.Infrastructure.Documents` : `QuestPdfInvoiceRenderer` (gabarit), `EfDocumentArchive`, `DocumentService`, `RenderedDocumentConfiguration` ; `DocumentsDependencyInjection.AddRaqmiDocuments()` (espace `RaqmiSystem.Infrastructure`, pour tenir en une ligne dans Program.cs) |
| API | `RaqmiSystem.Api.Endpoints.DocumentsEndpoints` |
| Desktop | `RaqmiApiClient.Documents` (`DownloadInvoicePdfAsync`, `GetInvoiceDocumentMetadataAsync`), `Views/DocumentPreviewWindow` |

### Le document lit la facture par son module, jamais par sa table

`DocumentService` consomme `IBillingService.GetInvoiceAsync` et `GetCustomerAsync`, et
`IApplicationSettingsService.GetAsync`. Il ne touche aucune entite `Invoice` : la facture est la
propriete du module Facturation, et le document est une **projection** de ce que ce module sert.

### Aucun montant n'est calcule dans le document

Charte du depot : les chiffres viennent du serveur. Ici, plus precisement, **du module Facturation** :

- les lignes (quantite, PU HT, taux, montant HT) sont recopiees de `InvoiceLineResponse` ;
- les totaux HT / TVA / TTC sont recopies de `InvoiceResponse`, **meme s'ils etaient incoherents
  avec les lignes** (test `Build_copies_the_invoice_totals_verbatim_even_when_they_disagree_with_the_lines`) ;
- le recapitulatif par taux additionne des montants de lignes deja calcules (`LineTotalExclVat`,
  `VatAmount`), jamais `base x taux` ;
- le gabarit ne fait que de la mise en forme (separateur decimal virgule, milliers par espace,
  dates `jj/mm/aaaa`).

Le test HTTP `Rendered_document_carries_the_invoice_figures_and_nothing_else` enveloppe le vrai
gabarit d'un capteur dans le vrai pipeline et verifie que le modele imprime est, chiffre pour
chiffre, la facture servie par l'API.

### Provenance de chaque bloc du gabarit

| Bloc | Source | Pourquoi |
|---|---|---|
| Emetteur : raison sociale, adresse, NIF, RC, AI, NIS | `InvoiceResponse.Issuer*` (instantane fige a l'emission) | Un changement de parametrage apres coup ne reecrit pas une facture emise |
| Emetteur : ville, telephone, courriel | `ApplicationSettings` courant | Coordonnees de contact, hors instantane, sans valeur d'identification fiscale |
| Client : raison sociale | `InvoiceResponse.CustomerName` (instantane fige a l'emission) | Meme regle |
| Client : adresse, NIF, RC, AI, NIS | Fiche client courante (`GetCustomerAsync`) | `InvoiceResponse` n'expose pas encore les colonnes `customer_*_snapshot` de la facture (voir « A cabler a l'integration ») ; l'archive fige de toute facon la piece des le premier rendu |
| Client : identifiants fiscaux | Masques pour un particulier (`CustomerType.Individual`) | Un particulier n'a ni NIF ni RC a imprimer |
| Numero, date de facture, date d'emission, unite | `InvoiceResponse` | |
| Lignes, recapitulatif TVA, totaux | `InvoiceResponse` (voir ci-dessus) | |
| Mode de reglement | `InvoiceDocumentDefaults.PaymentTermsMention` (constante) | La facture ne porte aucun mode de reglement ; le reglement est un fait distinct (A7) |
| Mentions de bas de page | `InvoiceDocumentDefaults.FooterMention` (constante) | `ApplicationSettings` n'a pas de texte libre ; **a completer avec l'expert-comptable** |
| Devise | `ApplicationSettings.CurrencyLabel` | |

Le gabarit **n'imprime pas le statut** de la facture (emise, reglee, annulee) : un statut qui change
apres le premier rendu ferait mentir une piece qu'on ne re-rend jamais. Une facture emise puis
annulee garde son document ; la piece corrective (l'avoir, B3) aura le sien.

### Concurrence : premier ecrit gagne

Deux demandes simultanees d'un document jamais rendu rendent toutes les deux ; la seconde ecriture
viole l'index unique, `EfDocumentArchive.StoreAsync` detache son entite, relit celle du gagnant et
**sert celle-la**. Les deux demandeurs recoivent la meme piece. Seul le rendu effectivement archive
ecrit une entree d'audit (`documents.invoice.rendered`, entite `RenderedDocument`).

## API

| Route | Permission | Reponse |
|---|---|---|
| `GET /api/v1/documents/invoices/{invoiceId:guid}` | `billing.invoice.read` (couvert par la cle historique `invoices.read`) | `application/pdf`, `Content-Disposition: attachment; filename=FAC-AAAA-NNNNNN.pdf`, `X-Document-Sha256`, `ETag` = empreinte, `Last-Modified` = date de rendu |
| `GET /api/v1/documents/invoices/{invoiceId:guid}/metadata` | idem | `DocumentMetadataResponse` (id, type, reference, version du gabarit, SHA-256, taille, type MIME, nom de fichier, date et auteur du rendu) |

Les deux routes sont idempotentes et partagent le meme chemin (« obtenir ou rendre ») : les
metadonnees decrivent toujours la piece qui sera servie.

Codes d'erreur : **404** si la facture n'existe pas **ou si elle est encore un brouillon** (un
brouillon n'a pas de numero, donc pas de document legal ; une facture annulee alors qu'elle etait
brouillon non plus) ; **403** sans la permission ; **401** sans jeton.

## Client Desktop

- `RaqmiApiClient.DownloadInvoicePdfAsync(apiBaseUrl, invoiceId)` renvoie un `DownloadedDocument`
  (nom de fichier issu du `Content-Disposition`, octets, empreinte annoncee par le serveur,
  empreinte calculee localement, `IntegrityVerified`).
- `DocumentPreviewWindow` est un apercu **minimal** : nom, type, taille, SHA-256, verdict
  d'integrite, et trois gestes - **Enregistrer sous**, **Ouvrir**, **Imprimer**. Aucun moteur de
  rendu embarque : le PDF est ecrit dans `%TEMP%\RaqmiSystem\Documents\` et confie au shell Windows
  (verbes `open` / `print` du lecteur PDF par defaut). Si aucun lecteur n'expose le verbe `print`,
  la fenetre l'explique et ouvre le document pour impression depuis le lecteur.
- Une piece dont l'empreinte locale differe de celle annoncee par le serveur **ne s'ouvre pas et ne
  s'imprime pas** depuis la fenetre (Enregistrer sous reste possible pour le diagnostic).

## A cabler a l'integration

1. `src/RaqmiSystem.Api/Program.cs` - deux lignes, deja posees par ce chantier :
   `builder.Services.AddRaqmiDocuments();` apres `AddRaqmiInfrastructure(...)`, et
   `api.MapDocumentsEndpoints();` a la fin des mappings. D'autres lots ajoutent une ligne au meme
   endroit : conflit trivial a resoudre en gardant toutes les lignes.
2. `InvoicesView` - bouton « Imprimer » (hors perimetre de ce chantier) :

   ~~~csharp
   var document = await active.ApiClient.DownloadInvoicePdfAsync(active.ApiBaseUrl, selected.Id);
   new DocumentPreviewWindow(document) { Owner = Window.GetWindow(this) }.ShowDialog();
   ~~~

   A n'activer que pour une facture qui a un numero (`selected.Number is not null`) : le serveur
   repond 404 pour un brouillon.
3. Migration `Documents` (commande ci-dessus).
4. Module Facturation (autre agent) : exposer `CustomerNifSnapshot`, `CustomerRcSnapshot`,
   `CustomerAiSnapshot`, `CustomerNisSnapshot`, `CustomerAddressSnapshot` dans `InvoiceResponse`,
   puis faire lire ces champs par `InvoiceDocumentModelBuilder.BuildCustomer` a la place de la fiche
   courante. Le gabarit ne change pas.
5. Relecture du gabarit par l'expert-comptable (plan comprime : 2 a 4 semaines a partir de la
   fusion) : les deux constantes d'`InvoiceDocumentDefaults` et, le cas echeant, un champ de
   parametrage pour les porter.

## Tests

| Fichier | Ce qu'il fixe |
|---|---|
| `DocumentsEndpointTests` | PDF valide (`%PDF-`), `Content-Disposition` avec le numero, `X-Document-Sha256` = SHA-256 du corps, second appel octet pour octet identique, une seule ligne archivee, une seule entree d'audit ; 404 brouillon ; 403 sans `invoices.read` ; 401 anonyme ; 404 inconnue ; **modele imprime = facture servie** (capteur autour du vrai gabarit, jeu a trois taux 0 / 9 / 19 %) |
| `DocumentsArchiveTests` | Premier ecrit gagne sur SQLite (deux contextes), normalisation de la reference, gardes de `RenderedDocument.Create` (vide, non-PDF, reference manquante, version < 1), copie defensive du contenu |
| `DocumentsInvoiceRendererTests` | Une page pour une facture a trois taux, pagination d'une facture de 90 lignes, emetteur minimal + particulier sans mentions optionnelles, version du gabarit |
| `DocumentsInvoiceModelTests` | Totaux recopies meme incoherents, recapitulatif par taux depuis les montants de lignes, ordre des lignes, emetteur depuis l'instantane et non le parametrage, identifiants d'un particulier masques, fiche client absente, brouillon refuse, mentions par defaut |

Les tests ne dependent pas de la culture de la machine : le gabarit formate avec un
`NumberFormatInfo` francais explicite (virgule decimale, espace pour les milliers), pas avec
`fr-FR`, dont le separateur de milliers ICU est une espace fine insecable que la police pourrait ne
pas couvrir.
