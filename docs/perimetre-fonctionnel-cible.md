# Perimetre fonctionnel cible

Ce perimetre consolide les grandes familles fonctionnelles identifiees dans l'ancien depot `Hotel_Metrics_Pro_Desktop` et les adapte au nouveau socle .NET.

Source fonctionnelle : commit c3a5795864f44363464a41ece95c169f4ca04bcf.

## Familles principales

1. Pilotage et tableaux de bord.
2. Hebergement et PMS.
3. Housekeeping.
4. Tarifs et conventions.
5. Recettes et cloture journaliere.
6. Clients, facturation et contrats.
7. Tresorerie et encaissements.
8. Comptabilite et finance.
9. Fiscalite et modules legaux.
10. Points de vente, stocks, cuisine et achats.
11. Parking, plage et controle d'acces.
12. PortMaster.
13. Ressources humaines.
14. GED et conformite ANPDP.
15. Commercial et relation client.
16. Maintenance et controle interne.
17. Administration et securite.
18. Systeme, synchronisation et continuite.

## Flux transverses majeurs

- Reservation -> sejour ou acces -> consommations -> facture -> encaissement -> comptabilite.
- Achat -> reception -> stock -> consommation ou production -> inventaire.
- Recette declaree -> validation -> rapprochement -> cloture journaliere.
- Employe -> onboarding -> affectation -> pointage -> paie -> teledeclaration.
- Document -> OCR -> version -> signature -> archivage legal -> conservation.
- Creance -> relance -> paiement -> lettrage -> recouvrement.

## Decision technique

Le nouveau depot doit garder ce perimetre comme cible fonctionnelle, mais la construction doit rester progressive : socle securite, unites, recettes, dashboard, puis finance et exploitation.
