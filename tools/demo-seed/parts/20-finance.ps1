# =============================================================================================
#  Finance : clients, recettes journalieres, cloture, tresorerie, facturation, creances,
#  comptabilite SCF et budget.
#  Modules 4, 4.5, 5, 5.2, 6, 8, 9, 9.2.
# =============================================================================================

$tokenControle = Get-Token 'Controle'
$tokenDirection = Get-Token 'Direction'
$tokenCaisse = Get-Token 'Caisse'
$tokenAlger = Get-Token 'ChefAlger'
$tokenOran = Get-Token 'ChefOran'

# Chaque unite a son chef : les ecrans montrent alors qui a saisi quoi, et non un compte
# technique unique sur toutes les lignes.
function Get-UnitToken {
    param([string]$UnitCode)

    if ($UnitCode -eq 'ORN-COR' -or $UnitCode -eq 'BEJ-CAP') { return $tokenOran }
    return $tokenAlger
}

# ---------------------------------------------------------------------------------- Clients

Write-Step 'Fichier clients (module 9.2)'

$script:Customers = @(
    [pscustomobject]@{ Code = 'CL-0001'; Name = 'Sonatrach Direction Regionale'; Type = 'Company'; City = 'Alger'; Nif = '000216000112233' },
    [pscustomobject]@{ Code = 'CL-0002'; Name = 'Air Algerie - Equipages'; Type = 'Company'; City = 'Alger'; Nif = '000216000445566' },
    [pscustomobject]@{ Code = 'CL-0003'; Name = 'Agence Sahara Voyages'; Type = 'Company'; City = 'Alger'; Nif = '000216000778899' },
    [pscustomobject]@{ Code = 'CL-0004'; Name = 'Cevital Services'; Type = 'Company'; City = 'Bejaia'; Nif = '000206001122334' },
    [pscustomobject]@{ Code = 'CL-0005'; Name = 'Wilaya d''Oran - Protocole'; Type = 'PublicEntity'; City = 'Oran'; Nif = '000231002233445' },
    [pscustomobject]@{ Code = 'CL-0006'; Name = 'Universite d''Alger 2'; Type = 'PublicEntity'; City = 'Alger'; Nif = '000216003344556' },
    [pscustomobject]@{ Code = 'CL-0007'; Name = 'Groupe Medical Ibn Sina'; Type = 'Company'; City = 'Alger'; Nif = '000216004455667' },
    [pscustomobject]@{ Code = 'CL-0008'; Name = 'Transmed Logistique'; Type = 'Company'; City = 'Oran'; Nif = '000231005566778' },
    [pscustomobject]@{ Code = 'CL-0009'; Name = 'Association des Architectes'; Type = 'Company'; City = 'Tipaza'; Nif = '000242006677889' },
    [pscustomobject]@{ Code = 'CL-0010'; Name = 'Amel Bouzid'; Type = 'Individual'; City = 'Alger'; Nif = $null },
    [pscustomobject]@{ Code = 'CL-0011'; Name = 'Farid Chaoui'; Type = 'Individual'; City = 'Constantine'; Nif = $null },
    [pscustomobject]@{ Code = 'CL-0012'; Name = 'Leila Ait Kaci'; Type = 'Individual'; City = 'Oran'; Nif = $null },
    [pscustomobject]@{ Code = 'CL-0013'; Name = 'Mourad Slimani'; Type = 'Individual'; City = 'Bejaia'; Nif = $null },
    [pscustomobject]@{ Code = 'CL-0014'; Name = 'Naftal Region Centre'; Type = 'Company'; City = 'Alger'; Nif = '000216007788990' },
    [pscustomobject]@{ Code = 'CL-0015'; Name = 'Agence Mediterranee Tours'; Type = 'Company'; City = 'Oran'; Nif = '000231008899001' },
    [pscustomobject]@{ Code = 'CL-0016'; Name = 'Banque Nationale - Formation'; Type = 'Company'; City = 'Alger'; Nif = '000216009900112' },
    [pscustomobject]@{ Code = 'CL-0017'; Name = 'Sonia Meziane'; Type = 'Individual'; City = 'Alger'; Nif = $null },
    [pscustomobject]@{ Code = 'CL-0018'; Name = 'Office National du Tourisme'; Type = 'PublicEntity'; City = 'Alger'; Nif = '000216001011121' }
)

$index = 0
foreach ($customer in $script:Customers) {
    $index++

    $body = @{
        code         = $customer.Code
        name         = $customer.Name
        customerType = $customer.Type
        city         = $customer.City
        address      = ($index.ToString() + ", rue des Freres Bouadou")
        phone        = ('+213 ' + (Get-RandomInt 21 41) + ' ' + (Get-RandomInt 10 99) + ' ' + (Get-RandomInt 10 99) + ' ' + (Get-RandomInt 10 99))
        email        = ('contact' + $index.ToString('00') + '@client-demo.dz')
    }

    if ($null -ne $customer.Nif) {
        $body['nif'] = $customer.Nif
        $body['rc'] = ((Get-RandomInt 10 44).ToString() + '/00-' + (Get-RandomInt 1000000 9999999) + 'B' + (Get-RandomInt 10 25))
        $body['ai'] = ((Get-RandomInt 10 44).ToString() + (Get-RandomInt 100000000 999999999))
        $body['nis'] = ('000' + (Get-RandomInt 200 250) + (Get-RandomInt 100000000 999999999))
    }

    $null = Invoke-Api -Method POST -Path '/billing/customers' -Token $tokenControle -Body $body
}

Write-Detail ($script:Customers.Count.ToString() + ' clients : societes, entites publiques et particuliers')

# ---------------------------------------------------------------- Recettes journalieres (4)

Write-Step 'Recettes journalieres et validation (module 4)'

# Profil de recettes par unite : une base quotidienne et une amplitude. Le jeudi et le vendredi
# (week-end algerien) recoivent une majoration, sans quoi les courbes du tableau de bord
# seraient plates et le guide montrerait un etablissement qui n'existe pas.
$revenueProfiles = @{
    'ALG-CEN' = @{ Accommodation = 520000; Food = 210000; Beverage = 78000; Other = 34000; Spread = 0.22 }
    'ORN-COR' = @{ Accommodation = 315000; Food = 142000; Beverage = 54000; Other = 21000; Spread = 0.25 }
    'TIP-AZU' = @{ Accommodation = 168000; Food = 62000; Beverage = 24000; Other = 11000; Spread = 0.30 }
    'BEJ-CAP' = @{ Accommodation = 96000; Food = 41000; Beverage = 17000; Other = 7000; Spread = 0.28 }
}

function Get-RevenueAmount {
    param([decimal]$Base, [double]$Spread, [bool]$IsWeekend)

    $variation = 1.0 + ((($script:Random.NextDouble() * 2.0) - 1.0) * $Spread)
    if ($IsWeekend) { $variation = $variation * 1.28 }

    return [math]::Round(($Base * $variation) / 100.0) * 100
}

$revenueDays = 60
$script:RevenueCreated = 0
$script:RevenueValidated = 0

foreach ($unit in $script:Units) {
    $profile = $revenueProfiles[$unit.Code]
    $unitToken = Get-UnitToken $unit.Code

    for ($offset = $revenueDays; $offset -ge 1; $offset--) {
        $date = $script:Today.AddDays(-$offset)
        $isWeekend = ($date.DayOfWeek -eq [System.DayOfWeek]::Thursday -or $date.DayOfWeek -eq [System.DayOfWeek]::Friday)

        $created = Invoke-Api -Method POST -Path '/revenue/daily' -Token $unitToken -Body @{
            businessDate  = (Get-Date-Text $date)
            hotelUnitCode = $unit.Code
            accommodation = (Get-RevenueAmount $profile.Accommodation $profile.Spread $isWeekend)
            food          = (Get-RevenueAmount $profile.Food $profile.Spread $isWeekend)
            beverage      = (Get-RevenueAmount $profile.Beverage $profile.Spread $isWeekend)
            other         = (Get-RevenueAmount $profile.Other $profile.Spread $isWeekend)
        } -Quiet

        if ($null -eq $created) { continue }

        $script:RevenueCreated++

        # Les deux derniers jours restent en cours de circuit : le guide peut alors montrer
        # les trois etats (brouillon, soumis, valide) sur un seul ecran.
        if ($offset -le 1) { continue }

        $submitted = Invoke-Api -Method POST -Path ('/revenue/daily/' + $created.id + '/submit') -Token $unitToken -Quiet
        if ($null -eq $submitted) { continue }

        if ($offset -le 2) { continue }

        $validated = Invoke-Api -Method POST -Path ('/revenue/daily/' + $created.id + '/validate') -Token $tokenControle -Quiet
        if ($null -ne $validated) { $script:RevenueValidated++ }
    }

    Write-Detail ($unit.Code + ' - ' + $revenueDays + ' journees saisies')
}

Write-Detail ($script:RevenueCreated.ToString() + ' recettes creees, ' + $script:RevenueValidated + ' validees')

# ------------------------------------------------------------- Cloture journaliere (4.5)

Write-Step 'Cloture journaliere et Night Audit (module 4.5)'

$closed = 0
foreach ($unit in $script:Units) {
    for ($offset = $revenueDays; $offset -ge 10; $offset--) {
        $date = $script:Today.AddDays(-$offset)

        $result = Invoke-Api -Method POST -Path '/closing/daily/close' -Token $tokenControle -Body @{
            businessDate  = (Get-Date-Text $date)
            hotelUnitCode = $unit.Code
            notes         = 'Cloture automatique de fin de journee'
        } -Quiet

        if ($null -ne $result) { $closed++ }
    }
}

Write-Detail ($closed.ToString() + ' journees cloturees (les 9 derniers jours restent ouverts)')

# --------------------------------------------------------------------- Tresorerie (5)

Write-Step 'Encaissements et tresorerie (module 5)'

$bankAccounts = @(
    @{ Code = 'BNA-001'; Label = 'BNA Compte principal groupe'; Bank = 'Banque Nationale d''Algerie'; Number = '001 00123 4567890123 45' },
    @{ Code = 'CPA-002'; Label = 'CPA Exploitation Alger'; Bank = 'Credit Populaire d''Algerie'; Number = '004 00456 7890123456 78' },
    @{ Code = 'BEA-003'; Label = 'BEA Exploitation Oran'; Bank = 'Banque Exterieure d''Algerie'; Number = '002 00789 0123456789 01' },
    @{ Code = 'BDL-004'; Label = 'BDL Devises'; Bank = 'Banque de Developpement Local'; Number = '005 00234 5678901234 56' }
)

foreach ($account in $bankAccounts) {
    $null = Invoke-Api -Method POST -Path '/treasury/bank-accounts' -Token $tokenCaisse -Body @{
        code          = $account.Code
        label         = $account.Label
        bankName      = $account.Bank
        accountNumber = $account.Number
    }
}

Write-Detail ($bankAccounts.Count.ToString() + ' comptes bancaires')

$methods = @('Cash', 'Card', 'Cheque', 'BankTransfer')
$receiptCount = 0

foreach ($unit in $script:Units) {
    for ($offset = 45; $offset -ge 0; $offset -= 2) {
        $date = $script:Today.AddDays(-$offset)
        $method = Get-RandomPick $methods

        $body = @{
            receiptDate   = (Get-Date-Text $date)
            hotelUnitCode = $unit.Code
            method        = $method
            amount        = (Get-RandomAmount 18000 480000 1000)
            reference     = ('ENC-' + $date.ToString('yyyyMM') + '-' + (Get-RandomInt 100 999))
        }

        if ($method -ne 'Cash') { $body['bankAccountCode'] = (Get-RandomPick @('BNA-001', 'CPA-002', 'BEA-003')) }

        $receipt = Invoke-Api -Method POST -Path '/treasury/receipts' -Token $tokenCaisse -Body $body -Quiet
        if ($null -eq $receipt) { continue }

        $receiptCount++

        # Les encaissements des trois derniers jours restent en brouillon : l'ecran montre
        # ainsi la difference entre un encaissement saisi et un encaissement confirme.
        if ($offset -ge 4) {
            $null = Invoke-Api -Method POST -Path ('/treasury/receipts/' + $receipt.id + '/confirm') -Token $tokenCaisse -Quiet
        }
    }
}

Write-Detail ($receiptCount.ToString() + ' encaissements sur 45 jours, tous modes de paiement')

$paymentOrders = @(
    @{ Beneficiary = 'SARL Fraicheur Distribution'; Amount = 486000; Reference = 'FA-2026-0412' },
    @{ Beneficiary = 'Sonelgaz - Facture electricite'; Amount = 1240000; Reference = 'SLG-08-2026' },
    @{ Beneficiary = 'SEAAL - Facture eau'; Amount = 318000; Reference = 'SEA-08-2026' },
    @{ Beneficiary = 'EURL Blanchisserie El Nour'; Amount = 275000; Reference = 'BL-2026-0088' },
    @{ Beneficiary = 'SPA Maintenance Ascenseurs'; Amount = 194000; Reference = 'MA-2026-0031' },
    @{ Beneficiary = 'Algerie Telecom'; Amount = 96000; Reference = 'AT-08-2026' },
    @{ Beneficiary = 'SARL Literie du Sud'; Amount = 1680000; Reference = 'LS-2026-0007' },
    @{ Beneficiary = 'Cabinet Comptable Ammari'; Amount = 240000; Reference = 'CC-T3-2026' },
    @{ Beneficiary = 'SARL Froid Industriel Oran'; Amount = 612000; Reference = 'FI-2026-0154' },
    @{ Beneficiary = 'Assurance CAAT - Flotte'; Amount = 890000; Reference = 'CAAT-2026-11' },
    @{ Beneficiary = 'EURL Securite Vigile Plus'; Amount = 720000; Reference = 'SV-08-2026' },
    @{ Beneficiary = 'SARL Espaces Verts Tipaza'; Amount = 132000; Reference = 'EV-2026-0022' }
)

$script:PaymentOrders = @()
$orderIndex = 0

foreach ($order in $paymentOrders) {
    $orderIndex++
    $orderDate = $script:Today.AddDays(-(Get-RandomInt 4 40))

    $created = Invoke-Api -Method POST -Path '/treasury/payment-orders' -Token $tokenCaisse -Body @{
        orderDate       = (Get-Date-Text $orderDate)
        beneficiary     = $order.Beneficiary
        amount          = $order.Amount
        dueDate         = (Get-Date-Text $orderDate.AddDays(30))
        bankAccountCode = (Get-RandomPick @('BNA-001', 'CPA-002', 'BEA-003'))
        reference       = $order.Reference
    } -Quiet

    if ($null -eq $created) { continue }

    $script:PaymentOrders += $created

    # Les quatre derniers restent en attente d'approbation : ils alimentent l'ecran des
    # validations (module 22.2) au lieu de laisser une file vide.
    if ($orderIndex -gt ($paymentOrders.Count - 4)) { continue }

    $approved = Invoke-Api -Method POST -Path ('/treasury/payment-orders/' + $created.id + '/approve') -Token $tokenControle -Quiet
    if ($null -eq $approved) { continue }

    if ($orderIndex % 3 -ne 0) {
        $null = Invoke-Api -Method POST -Path ('/treasury/payment-orders/' + $created.id + '/pay') -Token $tokenCaisse -Quiet
    }
}

Write-Detail ($script:PaymentOrders.Count.ToString() + ' ordres de paiement, approuves puis payes pour la plupart')

# ------------------------------------------------------------------------ Facturation (8)

Write-Step 'Facturation clients (module 8)'

$invoiceTemplates = @(
    @{ Designation = 'Hebergement chambre double - demi-pension'; Unit = 12500; Vat = 19 },
    @{ Designation = 'Hebergement chambre simple - petit dejeuner'; Unit = 8900; Vat = 19 },
    @{ Designation = 'Location salle de conference - journee'; Unit = 65000; Vat = 19 },
    @{ Designation = 'Pause-cafe seminaire (par personne)'; Unit = 1200; Vat = 19 },
    @{ Designation = 'Dejeuner groupe - menu 3 services'; Unit = 3800; Vat = 19 },
    @{ Designation = 'Transfert aeroport'; Unit = 4500; Vat = 19 },
    @{ Designation = 'Blanchisserie - forfait sejour'; Unit = 2200; Vat = 19 },
    @{ Designation = 'Denrees alimentaires de base'; Unit = 1800; Vat = 9 }
)

$script:Invoices = @()
$invoiceAges = @(112, 98, 91, 84, 77, 70, 64, 58, 52, 47, 41, 36, 32, 28, 25, 22, 19, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2, 1, 0, 45, 60, 75, 88, 103)

foreach ($age in $invoiceAges) {
    $customer = Get-RandomPick $script:Customers
    $unit = Get-RandomPick $script:Units
    $invoiceDate = $script:Today.AddDays(-$age)

    $lines = @()
    $lineCount = Get-RandomInt 1 4

    for ($line = 1; $line -le $lineCount; $line++) {
        $template = Get-RandomPick $invoiceTemplates
        $lines += @{
            designation = $template.Designation
            quantity    = (Get-RandomInt 1 24)
            unitPrice   = $template.Unit
            vatRate     = $template.Vat
        }
    }

    $invoice = Invoke-Api -Method POST -Path '/billing/invoices' -Token (Get-UnitToken $unit.Code) -Body @{
        customerCode  = $customer.Code
        hotelUnitCode = $unit.Code
        invoiceDate   = (Get-Date-Text $invoiceDate)
        lines         = $lines
    } -Quiet

    if ($null -eq $invoice) { continue }

    # Une facture toute recente reste en brouillon : le guide montre alors une facture encore
    # modifiable a cote de factures emises et payees.
    if ($age -le 1) {
        $script:Invoices += [pscustomobject]@{ Id = $invoice.id; Age = $age; Status = 'Draft'; Number = $null }
        continue
    }

    $issued = Invoke-Api -Method POST -Path ('/billing/invoices/' + $invoice.id + '/issue') -Token $tokenControle -Quiet
    if ($null -eq $issued) { continue }

    # Le recouvrement doit rester visible : les factures recentes sont reglees, les anciennes
    # ne le sont pas toutes - c'est ce qui donne une balance agee lisible.
    $isPaid = ($age -lt 30 -and ($script:Random.Next(0, 10) -lt 7)) -or ($age -ge 30 -and ($script:Random.Next(0, 10) -lt 4))

    if ($isPaid) {
        $null = Invoke-Api -Method POST -Path ('/billing/invoices/' + $invoice.id + '/pay') -Token (Get-UnitToken $unit.Code) -Quiet
        $script:Invoices += [pscustomobject]@{ Id = $invoice.id; Age = $age; Status = 'Paid'; Number = $issued.number }
    }
    else {
        $script:Invoices += [pscustomobject]@{ Id = $invoice.id; Age = $age; Status = 'Issued'; Number = $issued.number }
    }
}

$issuedCount = ($script:Invoices | Where-Object { $_.Status -eq 'Issued' }).Count
Write-Detail ($script:Invoices.Count.ToString() + ' factures, dont ' + $issuedCount + ' emises non reglees')

# ------------------------------------------------------------------------- Creances (9)

Write-Step 'Creances et relances (module 9)'

$reminderCount = 0
$openInvoices = $script:Invoices | Where-Object { $_.Status -eq 'Issued' -and $_.Age -ge 30 -and $null -ne $_.Number }

foreach ($invoice in $openInvoices) {
    $level = 'First'
    if ($invoice.Age -ge 90) { $level = 'FormalNotice' }
    elseif ($invoice.Age -ge 60) { $level = 'Second' }

    $result = Invoke-Api -Method POST -Path '/receivables/reminders' -Token $tokenControle -Body @{
        invoiceNumber = $invoice.Number
        level         = $level
        sentAt        = (Get-Date-Text $script:Today.AddDays(-(Get-RandomInt 1 12)))
        channel       = (Get-RandomPick @('Phone', 'Email', 'Letter', 'InPerson'))
        notes         = 'Relance suivie par le controle de gestion'
    } -Quiet

    if ($null -ne $result) { $reminderCount++ }
}

Write-Detail ($reminderCount.ToString() + ' relances : 1er niveau, 2e niveau et mises en demeure')

# ------------------------------------------------------------------ Comptabilite SCF (5.2)

Write-Step 'Comptabilite SCF (module 5.2)'

# Extrait du plan comptable algerien (SCF), limite aux comptes que les ecritures ci-dessous
# mouvementent : un plan complet ne rendrait pas la capture plus parlante.
$chartAccounts = @(
    @{ Code = '101000'; Label = 'Capital emis'; Kind = 'Equity' },
    @{ Code = '106000'; Label = 'Reserves'; Kind = 'Equity' },
    @{ Code = '213000'; Label = 'Constructions'; Kind = 'Asset' },
    @{ Code = '218000'; Label = 'Autres immobilisations corporelles'; Kind = 'Asset' },
    @{ Code = '311000'; Label = 'Matieres premieres'; Kind = 'Asset' },
    @{ Code = '380000'; Label = 'Achats stockes'; Kind = 'Asset' },
    @{ Code = '401000'; Label = 'Fournisseurs de stocks et services'; Kind = 'Liability' },
    @{ Code = '411000'; Label = 'Clients'; Kind = 'Asset' },
    @{ Code = '421000'; Label = 'Personnel - remunerations dues'; Kind = 'Liability' },
    @{ Code = '431000'; Label = 'Securite sociale'; Kind = 'Liability' },
    @{ Code = '442000'; Label = 'Etat - impots et taxes'; Kind = 'Liability' },
    @{ Code = '445100'; Label = 'TVA collectee'; Kind = 'Liability' },
    @{ Code = '445600'; Label = 'TVA deductible'; Kind = 'Asset' },
    @{ Code = '512000'; Label = 'Banques comptes courants'; Kind = 'Asset' },
    @{ Code = '530000'; Label = 'Caisse'; Kind = 'Asset' },
    @{ Code = '600000'; Label = 'Achats de marchandises'; Kind = 'Expense' },
    @{ Code = '601000'; Label = 'Matieres premieres consommees'; Kind = 'Expense' },
    @{ Code = '607000'; Label = 'Achats non stockes'; Kind = 'Expense' },
    @{ Code = '613000'; Label = 'Locations'; Kind = 'Expense' },
    @{ Code = '615000'; Label = 'Entretien et reparations'; Kind = 'Expense' },
    @{ Code = '618000'; Label = 'Documentation et divers'; Kind = 'Expense' },
    @{ Code = '621000'; Label = 'Personnel exterieur'; Kind = 'Expense' },
    @{ Code = '631000'; Label = 'Remunerations du personnel'; Kind = 'Expense' },
    @{ Code = '635000'; Label = 'Cotisations sociales'; Kind = 'Expense' },
    @{ Code = '700000'; Label = 'Ventes de marchandises'; Kind = 'Revenue' },
    @{ Code = '706100'; Label = 'Prestations hebergement'; Kind = 'Revenue' },
    @{ Code = '706200'; Label = 'Prestations restauration'; Kind = 'Revenue' },
    @{ Code = '706300'; Label = 'Prestations boissons'; Kind = 'Revenue' },
    @{ Code = '708000'; Label = 'Produits des activites annexes'; Kind = 'Revenue' }
)

foreach ($account in $chartAccounts) {
    $null = Invoke-Api -Method POST -Path '/accounting/accounts' -Token $tokenControle -Body @{
        code  = $account.Code
        label = $account.Label
        kind  = $account.Kind
    } -Quiet
}

$journals = @(
    @{ Code = 'VE'; Label = 'Journal des ventes' },
    @{ Code = 'AC'; Label = 'Journal des achats' },
    @{ Code = 'BQ'; Label = 'Journal de banque' },
    @{ Code = 'CA'; Label = 'Journal de caisse' },
    @{ Code = 'OD'; Label = 'Operations diverses' },
    @{ Code = 'PA'; Label = 'Journal de paie' }
)

foreach ($journal in $journals) {
    $null = Invoke-Api -Method POST -Path '/accounting/journals' -Token $tokenControle -Body @{
        code  = $journal.Code
        label = $journal.Label
    } -Quiet
}

Write-Detail ($chartAccounts.Count.ToString() + ' comptes SCF, ' + $journals.Count + ' journaux')

# Ecritures equilibrees, generees a partir de gabarits : le total debit egale toujours le
# total credit, sinon l'API refuse la comptabilisation - et c'est tres bien ainsi.
$entryTemplates = @(
    @{ Journal = 'VE'; Label = 'Facturation hebergement du mois'; Debit = '411000'; Credits = @(@{ Account = '706100'; Share = 0.84 }, @{ Account = '445100'; Share = 0.16 }) },
    @{ Journal = 'VE'; Label = 'Facturation restauration du mois'; Debit = '411000'; Credits = @(@{ Account = '706200'; Share = 0.84 }, @{ Account = '445100'; Share = 0.16 }) },
    @{ Journal = 'AC'; Label = 'Achat denrees alimentaires'; Debit = '601000'; Credits = @(@{ Account = '401000'; Share = 1.0 }) },
    @{ Journal = 'AC'; Label = 'Achat produits d''entretien'; Debit = '607000'; Credits = @(@{ Account = '401000'; Share = 1.0 }) },
    @{ Journal = 'BQ'; Label = 'Reglement fournisseur par virement'; Debit = '401000'; Credits = @(@{ Account = '512000'; Share = 1.0 }) },
    @{ Journal = 'BQ'; Label = 'Encaissement client par virement'; Debit = '512000'; Credits = @(@{ Account = '411000'; Share = 1.0 }) },
    @{ Journal = 'CA'; Label = 'Versement especes en banque'; Debit = '512000'; Credits = @(@{ Account = '530000'; Share = 1.0 }) },
    @{ Journal = 'PA'; Label = 'Salaires du mois'; Debit = '631000'; Credits = @(@{ Account = '421000'; Share = 0.74 }, @{ Account = '431000'; Share = 0.26 }) },
    @{ Journal = 'OD'; Label = 'Dotation aux amortissements'; Debit = '618000'; Credits = @(@{ Account = '218000'; Share = 1.0 }) }
)

$entryCount = 0
$postedCount = 0

for ($round = 0; $round -lt 4; $round++) {
    foreach ($template in $entryTemplates) {
        $entryDate = $script:Today.AddDays(-((($round + 1) * 12) + (Get-RandomInt 0 6)))
        $amount = Get-RandomAmount 120000 2400000 1000

        $lines = @(@{
            accountCode = $template.Debit
            label       = $template.Label
            debit       = $amount
            credit      = 0
        })

        $allocated = 0
        $creditCount = $template.Credits.Count
        $creditIndex = 0

        foreach ($credit in $template.Credits) {
            $creditIndex++

            # La derniere ligne solde l'ecriture au centime : repartir en pourcentages laisse
            # sinon un ecart d'arrondi que l'API refuserait, a juste titre.
            if ($creditIndex -eq $creditCount) { $share = $amount - $allocated }
            else {
                $share = [math]::Round($amount * $credit.Share, 2)
                $allocated += $share
            }

            $lines += @{
                accountCode = $credit.Account
                label       = $template.Label
                debit       = 0
                credit      = $share
            }
        }

        $entry = Invoke-Api -Method POST -Path '/accounting/entries' -Token $tokenControle -Body @{
            entryDate   = (Get-Date-Text $entryDate)
            journalCode = $template.Journal
            label       = $template.Label
            reference   = ('PC-' + $entryDate.ToString('yyyyMM') + '-' + (Get-RandomInt 100 999))
            lines       = $lines
        } -Quiet

        if ($null -eq $entry) { continue }

        $entryCount++

        # Le dernier tour reste en brouillon : la balance montre ce qui est comptabilise, et
        # l'ecran des ecritures montre ce qui attend encore de l'etre.
        if ($round -eq 3) { continue }

        $posted = Invoke-Api -Method POST -Path ('/accounting/entries/' + $entry.id + '/post') -Token $tokenControle -Quiet
        if ($null -ne $posted) { $postedCount++ }
    }
}

Write-Detail ($entryCount.ToString() + ' ecritures en partie double, ' + $postedCount + ' comptabilisees')

# ------------------------------------------------------------------ Budget & previsions (6)

Write-Step 'Budget et previsions (module 6)'

$year = $script:Today.Year
$budgetCategories = @('Accommodation', 'Food', 'Beverage', 'Other')

foreach ($unit in $script:Units) {
    $profile = $revenueProfiles[$unit.Code]

    $lines = @()
    for ($month = 1; $month -le 12; $month++) {
        # Saisonnalite : la haute saison algerienne va de juin a septembre, le creux est en
        # janvier-fevrier. Un budget plat rendrait l'ecran des ecarts illisible.
        $seasonality = 1.0
        if ($month -ge 6 -and $month -le 9) { $seasonality = 1.35 }
        elseif ($month -le 2) { $seasonality = 0.72 }
        elseif ($month -eq 12) { $seasonality = 1.12 }

        foreach ($category in $budgetCategories) {
            $base = $profile.Accommodation
            if ($category -eq 'Food') { $base = $profile.Food }
            elseif ($category -eq 'Beverage') { $base = $profile.Beverage }
            elseif ($category -eq 'Other') { $base = $profile.Other }

            $lines += @{
                month        = $month
                category     = $category
                amountTarget = [math]::Round(($base * 30 * $seasonality) / 1000.0) * 1000
            }
        }
    }

    $plan = Invoke-Api -Method POST -Path '/budget/plans' -Token (Get-UnitToken $unit.Code) -Body @{
        year          = $year
        hotelUnitCode = $unit.Code
        label         = ('Budget ' + $year + ' - ' + $unit.Name)
        lines         = $lines
    } -Quiet

    if ($null -eq $plan) { continue }

    # La marina reste en brouillon : le guide montre ainsi un budget en cours de preparation
    # a cote de budgets approuves.
    if ($unit.Code -eq 'BEJ-CAP') {
        Write-Detail ($unit.Code + ' - budget ' + $year + ' en brouillon')
        continue
    }

    $approved = Invoke-Api -Method POST -Path ('/budget/plans/' + $plan.id + '/approve') -Token $tokenDirection -Quiet
    if ($null -ne $approved) { Write-Detail ($unit.Code + ' - budget ' + $year + ' approuve par la direction') }
}
