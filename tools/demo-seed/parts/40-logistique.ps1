# =============================================================================================
#  Logistique : magasins, articles, mouvements valorises au PMP, inventaire physique,
#  fournisseurs, bons de commande, receptions, fiches techniques et HACCP.
#  Modules 11, 11.5, 12.
# =============================================================================================

$tokenControle = Get-Token 'Controle'
$tokenAlger = Get-Token 'ChefAlger'

# ------------------------------------------------------------------------- Magasins (11)

Write-Step 'Magasins et articles (module 11)'

$warehouses = @(
    @{ Code = 'ECO-ALG'; Label = 'Economat Alger Centre'; Unit = 'ALG-CEN' },
    @{ Code = 'CAV-ALG'; Label = 'Cave et boissons Alger'; Unit = 'ALG-CEN' },
    @{ Code = 'ECO-ORN'; Label = 'Economat Corniche Oran'; Unit = 'ORN-COR' },
    @{ Code = 'ECO-TIP'; Label = 'Economat Residence Tipaza'; Unit = 'TIP-AZU' },
    @{ Code = 'ECO-BEJ'; Label = 'Economat Marina Bejaia'; Unit = 'BEJ-CAP' }
)

foreach ($warehouse in $warehouses) {
    $null = Invoke-Api -Method POST -Path '/inventory/warehouses' -Token (Get-UnitToken $warehouse.Unit) -Body @{
        code          = $warehouse.Code
        label         = $warehouse.Label
        hotelUnitCode = $warehouse.Unit
    } -Quiet
}

# Prix d'achat indicatifs en dinars : ils servent a la fois aux entrees en stock (valorisation
# au PMP) et aux fiches techniques de la cuisine, qui lisent le cout matiere depuis le stock.
$stockItems = @(
    @{ Code = 'ALI-FAR'; Designation = 'Farine de ble T55'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 80; Cost = 95 },
    @{ Code = 'ALI-SEM'; Designation = 'Semoule moyenne'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 120; Cost = 110 },
    @{ Code = 'ALI-RIZ'; Designation = 'Riz long grain'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 60; Cost = 190 },
    @{ Code = 'ALI-HUI'; Designation = 'Huile de tournesol 5 L'; Uom = 'bidon'; Category = 'Alimentaire'; Minimum = 24; Cost = 950 },
    @{ Code = 'ALI-SUC'; Designation = 'Sucre cristallise'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 50; Cost = 130 },
    @{ Code = 'ALI-SEL'; Designation = 'Sel fin de table'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 20; Cost = 45 },
    @{ Code = 'ALI-TOM'; Designation = 'Concentre de tomate 800 g'; Uom = 'boite'; Category = 'Alimentaire'; Minimum = 40; Cost = 320 },
    @{ Code = 'ALI-POM'; Designation = 'Pommes de terre'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 150; Cost = 85 },
    @{ Code = 'ALI-OIG'; Designation = 'Oignons'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 100; Cost = 70 },
    @{ Code = 'ALI-CAR'; Designation = 'Carottes'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 60; Cost = 90 },
    @{ Code = 'ALI-POU'; Designation = 'Poulet entier frais'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 40; Cost = 480 },
    @{ Code = 'ALI-AGN'; Designation = 'Epaule d''agneau'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 25; Cost = 1850 },
    @{ Code = 'ALI-BOE'; Designation = 'Viande de boeuf a braiser'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 25; Cost = 1650 },
    @{ Code = 'ALI-POI'; Designation = 'Filet de merlan'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 20; Cost = 1400 },
    @{ Code = 'ALI-OEU'; Designation = 'Oeufs calibre moyen'; Uom = 'plaque'; Category = 'Alimentaire'; Minimum = 30; Cost = 620 },
    @{ Code = 'ALI-LAI'; Designation = 'Lait UHT 1 L'; Uom = 'brique'; Category = 'Alimentaire'; Minimum = 120; Cost = 125 },
    @{ Code = 'ALI-BEU'; Designation = 'Beurre de laiterie 500 g'; Uom = 'plaquette'; Category = 'Alimentaire'; Minimum = 30; Cost = 780 },
    @{ Code = 'ALI-FRO'; Designation = 'Fromage a pate pressee'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 15; Cost = 1550 },
    @{ Code = 'ALI-DAT'; Designation = 'Dattes Deglet Nour'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 20; Cost = 900 },
    @{ Code = 'ALI-EPI'; Designation = 'Melange d''epices ras el hanout'; Uom = 'kg'; Category = 'Alimentaire'; Minimum = 5; Cost = 2200 },
    @{ Code = 'BOI-EAU'; Designation = 'Eau minerale 1,5 L'; Uom = 'bouteille'; Category = 'Boisson'; Minimum = 300; Cost = 55 },
    @{ Code = 'BOI-SOD'; Designation = 'Soda 33 cl'; Uom = 'canette'; Category = 'Boisson'; Minimum = 240; Cost = 90 },
    @{ Code = 'BOI-JUS'; Designation = 'Jus d''orange 1 L'; Uom = 'brique'; Category = 'Boisson'; Minimum = 100; Cost = 210 },
    @{ Code = 'BOI-CAF'; Designation = 'Cafe en grains'; Uom = 'kg'; Category = 'Boisson'; Minimum = 30; Cost = 1750 },
    @{ Code = 'BOI-THE'; Designation = 'The vert en vrac'; Uom = 'kg'; Category = 'Boisson'; Minimum = 12; Cost = 1350 },
    @{ Code = 'ENT-DET'; Designation = 'Detergent sols 5 L'; Uom = 'bidon'; Category = 'Entretien'; Minimum = 30; Cost = 690 },
    @{ Code = 'ENT-JAV'; Designation = 'Eau de javel 2 L'; Uom = 'bidon'; Category = 'Entretien'; Minimum = 40; Cost = 180 },
    @{ Code = 'ENT-PAP'; Designation = 'Papier hygienique (lot de 12)'; Uom = 'lot'; Category = 'Entretien'; Minimum = 60; Cost = 540 },
    @{ Code = 'ENT-SAV'; Designation = 'Savon liquide invite 30 ml'; Uom = 'unite'; Category = 'Entretien'; Minimum = 400; Cost = 35 },
    @{ Code = 'ENT-SHA'; Designation = 'Shampoing invite 30 ml'; Uom = 'unite'; Category = 'Entretien'; Minimum = 400; Cost = 38 },
    @{ Code = 'ENT-SAC'; Designation = 'Sacs poubelle 100 L'; Uom = 'rouleau'; Category = 'Entretien'; Minimum = 50; Cost = 260 },
    @{ Code = 'EQU-DRA'; Designation = 'Drap plat 240x300'; Uom = 'unite'; Category = 'Equipement'; Minimum = 80; Cost = 2400 },
    @{ Code = 'EQU-SER'; Designation = 'Serviette de bain 70x140'; Uom = 'unite'; Category = 'Equipement'; Minimum = 120; Cost = 1250 },
    @{ Code = 'EQU-OREI'; Designation = 'Oreiller microfibre'; Uom = 'unite'; Category = 'Equipement'; Minimum = 40; Cost = 1600 },
    @{ Code = 'AUT-PAP'; Designation = 'Papeterie et fournitures bureau'; Uom = 'lot'; Category = 'Autre'; Minimum = 15; Cost = 1800 }
)

foreach ($item in $stockItems) {
    $null = Invoke-Api -Method POST -Path '/inventory/items' -Token $tokenAlger -Body @{
        code            = $item.Code
        designation     = $item.Designation
        unitOfMeasure   = $item.Uom
        category        = $item.Category
        minimumQuantity = $item.Minimum
    } -Quiet
}

Write-Detail ($warehouses.Count.ToString() + ' magasins, ' + $stockItems.Count + ' articles')

# ----------------------------------------------------------- Mouvements de stock (11)

Write-Step 'Mouvements de stock et valorisation PMP (module 11)'

$movementCount = 0

foreach ($warehouse in $warehouses) {
    $unitToken = Get-UnitToken $warehouse.Unit

    # La cave ne recoit que des boissons, l'economat tout le reste : un magasin qui contient
    # de tout ne ressemble a aucun economat d'hotel.
    if ($warehouse.Code -eq 'CAV-ALG') { $catalogue = $stockItems | Where-Object { $_.Category -eq 'Boisson' } }
    else { $catalogue = $stockItems | Where-Object { $_.Category -ne 'Boisson' } }

    foreach ($item in $catalogue) {
        # Deux entrees a des couts differents : c'est ce qui fait bouger le PMP et rend la
        # colonne "cout unitaire moyen" interessante a montrer.
        foreach ($entryOffset in @(52, 21)) {
            $entryDate = $script:Today.AddDays(-$entryOffset - (Get-RandomInt 0 4))
            $drift = 1.0 + ((($script:Random.NextDouble() * 2.0) - 1.0) * 0.12)

            $entry = Invoke-Api -Method POST -Path '/inventory/movements' -Token $unitToken -Body @{
                warehouseCode = $warehouse.Code
                itemCode      = $item.Code
                movementDate  = (Get-Date-Text $entryDate)
                kind          = 'PurchaseEntry'
                quantity      = ($item.Minimum * (Get-RandomInt 2 4))
                unitCost      = [math]::Round($item.Cost * $drift, 2)
                reference     = ('BE-' + $entryDate.ToString('yyyyMM') + '-' + (Get-RandomInt 100 999))
            } -Quiet

            if ($null -ne $entry) { $movementCount++ }
        }

        # Sorties de consommation : quelques articles descendent volontairement sous leur
        # seuil, pour que l'ecran des alertes de stock bas ne soit pas vide.
        $consumptionRounds = Get-RandomInt 2 5
        for ($round = 0; $round -lt $consumptionRounds; $round++) {
            $consumptionDate = $script:Today.AddDays(-(Get-RandomInt 1 18))

            $consumption = Invoke-Api -Method POST -Path '/inventory/movements' -Token $unitToken -Body @{
                warehouseCode = $warehouse.Code
                itemCode      = $item.Code
                movementDate  = (Get-Date-Text $consumptionDate)
                kind          = 'Consumption'
                quantity      = ($item.Minimum * (Get-RandomInt 1 2) / 2)
                reference     = ('BS-' + $consumptionDate.ToString('yyyyMM') + '-' + (Get-RandomInt 100 999))
                notes         = 'Sortie vers la production'
            } -Quiet

            if ($null -ne $consumption) { $movementCount++ }
        }
    }
}

Write-Detail ($movementCount.ToString() + ' mouvements : entrees valorisees et sorties de consommation')

# ------------------------------------------------------------------ Inventaire physique (11)

Write-Step 'Inventaire physique (module 11)'

$count = Invoke-Api -Method POST -Path '/inventory/counts' -Token $tokenAlger -Body @{
    warehouseCode = 'ECO-ALG'
    countDate     = (Get-Date-Text $script:Today.AddDays(-3))
} -Quiet

if ($null -ne $count) {
    $stock = Invoke-Api -Method GET -Path '/inventory/warehouses/ECO-ALG/stock' -Token $tokenAlger -Quiet

    $stockLines = @()
    if ($null -ne $stock) {
        if ($stock -is [array]) { $stockLines = $stock }
        elseif ($null -ne $stock.items) { $stockLines = $stock.items }
    }

    $lines = @()
    foreach ($line in ($stockLines | Select-Object -First 16)) {
        # Un ecart de comptage sur une ligne sur trois : un inventaire ou tout tombe juste
        # n'apprend rien au lecteur du guide.
        $counted = $line.quantity
        if (($script:Random.Next(0, 3)) -eq 0) { $counted = [math]::Max(0, $line.quantity - (Get-RandomInt 1 6)) }

        $lines += @{ itemCode = $line.itemCode; countedQuantity = $counted }
    }

    if ($lines.Count -gt 0) {
        $null = Invoke-Api -Method PUT -Path ('/inventory/counts/' + $count.id + '/lines') -Token $tokenAlger -Body @{ lines = $lines } -Quiet
        $null = Invoke-Api -Method POST -Path ('/inventory/counts/' + $count.id + '/validate') -Token $tokenControle -Quiet
        Write-Detail ($lines.Count.ToString() + ' lignes comptees puis validees par le controle')
    }
}

# ------------------------------------------------------------------------ Fournisseurs (12)

Write-Step 'Fournisseurs, commandes et receptions (module 12)'

$suppliers = @(
    @{ Code = 'FR-0001'; Name = 'SARL Fraicheur Distribution'; Type = 'Company'; City = 'Alger' },
    @{ Code = 'FR-0002'; Name = 'EURL Grands Moulins du Centre'; Type = 'Company'; City = 'Blida' },
    @{ Code = 'FR-0003'; Name = 'SPA Laiterie Soummam Ouest'; Type = 'Company'; City = 'Bejaia' },
    @{ Code = 'FR-0004'; Name = 'SARL Boissons Mediterranee'; Type = 'Company'; City = 'Oran' },
    @{ Code = 'FR-0005'; Name = 'EURL Hygiene Pro Services'; Type = 'Company'; City = 'Alger' },
    @{ Code = 'FR-0006'; Name = 'SARL Literie du Sud'; Type = 'Company'; City = 'Setif' },
    @{ Code = 'FR-0007'; Name = 'Boucherie Centrale El Harrach'; Type = 'Company'; City = 'Alger' },
    @{ Code = 'FR-0008'; Name = 'SARL Maree du Littoral'; Type = 'Company'; City = 'Oran' },
    @{ Code = 'FR-0009'; Name = 'Cooperative Maraichere de Tipaza'; Type = 'Company'; City = 'Tipaza' },
    @{ Code = 'FR-0010'; Name = 'Rachid Ammari'; Type = 'Individual'; City = 'Alger' },
    @{ Code = 'FR-0011'; Name = 'EURL Papeterie du Centre'; Type = 'Company'; City = 'Alger' },
    @{ Code = 'FR-0012'; Name = 'SARL Cafes et Thes d''Orient'; Type = 'Company'; City = 'Constantine' }
)

$supplierIndex = 0
foreach ($supplier in $suppliers) {
    $supplierIndex++

    $body = @{
        code         = $supplier.Code
        name         = $supplier.Name
        supplierType = $supplier.Type
        city         = $supplier.City
        address      = ('Zone d''activite, lot ' + $supplierIndex)
        phone        = ('+213 ' + (Get-RandomInt 21 41) + ' ' + (Get-RandomInt 10 99) + ' ' + (Get-RandomInt 10 99) + ' ' + (Get-RandomInt 10 99))
        email        = ('commercial' + $supplierIndex.ToString('00') + '@fournisseur-demo.dz')
    }

    if ($supplier.Type -ne 'Individual') {
        $body['nif'] = ('0002' + (Get-RandomInt 10 44) + (Get-RandomInt 100000000 999999999))
        $body['rc'] = ((Get-RandomInt 10 44).ToString() + '/00-' + (Get-RandomInt 1000000 9999999) + 'B' + (Get-RandomInt 10 25))
    }

    $null = Invoke-Api -Method POST -Path '/purchasing/suppliers' -Token $tokenAlger -Body $body -Quiet
}

Write-Detail ($suppliers.Count.ToString() + ' fournisseurs references')

$orderPlans = @(
    @{ Supplier = 'FR-0002'; Warehouse = 'ECO-ALG'; Items = @('ALI-FAR', 'ALI-SEM', 'ALI-SUC') },
    @{ Supplier = 'FR-0001'; Warehouse = 'ECO-ALG'; Items = @('ALI-POM', 'ALI-OIG', 'ALI-CAR', 'ALI-TOM') },
    @{ Supplier = 'FR-0007'; Warehouse = 'ECO-ALG'; Items = @('ALI-POU', 'ALI-AGN', 'ALI-BOE') },
    @{ Supplier = 'FR-0003'; Warehouse = 'ECO-ALG'; Items = @('ALI-LAI', 'ALI-BEU', 'ALI-FRO') },
    @{ Supplier = 'FR-0004'; Warehouse = 'CAV-ALG'; Items = @('BOI-EAU', 'BOI-SOD', 'BOI-JUS') },
    @{ Supplier = 'FR-0012'; Warehouse = 'CAV-ALG'; Items = @('BOI-CAF', 'BOI-THE') },
    @{ Supplier = 'FR-0005'; Warehouse = 'ECO-ALG'; Items = @('ENT-DET', 'ENT-JAV', 'ENT-PAP', 'ENT-SAC') },
    @{ Supplier = 'FR-0005'; Warehouse = 'ECO-ORN'; Items = @('ENT-SAV', 'ENT-SHA') },
    @{ Supplier = 'FR-0008'; Warehouse = 'ECO-ORN'; Items = @('ALI-POI') },
    @{ Supplier = 'FR-0009'; Warehouse = 'ECO-TIP'; Items = @('ALI-POM', 'ALI-CAR', 'ALI-OIG') },
    @{ Supplier = 'FR-0006'; Warehouse = 'ECO-ALG'; Items = @('EQU-DRA', 'EQU-SER', 'EQU-OREI') },
    @{ Supplier = 'FR-0011'; Warehouse = 'ECO-ALG'; Items = @('AUT-PAP') },
    @{ Supplier = 'FR-0001'; Warehouse = 'ECO-BEJ'; Items = @('ALI-POM', 'ALI-OIG') },
    @{ Supplier = 'FR-0004'; Warehouse = 'ECO-ORN'; Items = @('BOI-EAU', 'BOI-SOD') }
)

$orderIndex = 0
$approvedOrders = 0
$receivedOrders = 0

foreach ($plan in $orderPlans) {
    $orderIndex++
    $warehouse = $warehouses | Where-Object { $_.Code -eq $plan.Warehouse } | Select-Object -First 1
    $unitToken = Get-UnitToken $warehouse.Unit
    $orderDate = $script:Today.AddDays(-(Get-RandomInt 3 34))

    $lines = @()
    foreach ($itemCode in $plan.Items) {
        $item = $stockItems | Where-Object { $_.Code -eq $itemCode } | Select-Object -First 1
        $lines += @{
            itemCode    = $item.Code
            designation = $item.Designation
            quantity    = ($item.Minimum * (Get-RandomInt 1 3))
            unitPrice   = [math]::Round($item.Cost * (1.0 + ($script:Random.NextDouble() * 0.1)), 2)
        }
    }

    $order = Invoke-Api -Method POST -Path '/purchasing/orders' -Token $unitToken -Body @{
        supplierCode  = $plan.Supplier
        warehouseCode = $plan.Warehouse
        orderDate     = (Get-Date-Text $orderDate)
        lines         = $lines
    } -Quiet

    if ($null -eq $order) { continue }

    # Les trois derniers restent en brouillon : ils n'ont pas encore de numero, ce que
    # l'ecran doit pouvoir montrer a cote de commandes numerotees.
    if ($orderIndex -gt ($orderPlans.Count - 3)) { continue }

    $approved = Invoke-Api -Method POST -Path ('/purchasing/orders/' + $order.id + '/approve') -Token $tokenControle -Quiet
    if ($null -eq $approved) { continue }

    $approvedOrders++

    if ($orderIndex % 4 -eq 0) { continue }

    # Reception partielle une fois sur trois : le statut "partiellement recu" existe dans le
    # produit, le guide doit pouvoir le montrer.
    $isPartial = ($orderIndex % 3 -eq 0)
    $receiptLines = @()

    foreach ($line in $approved.lines) {
        $quantity = $line.quantity
        if ($isPartial) { $quantity = [math]::Round($line.quantity * 0.6, 2) }

        $receiptLines += @{
            lineId   = $line.id
            quantity = $quantity
        }
    }

    $received = Invoke-Api -Method POST -Path ('/purchasing/orders/' + $order.id + '/receive') -Token $unitToken -Body @{
        lines = $receiptLines
    } -Quiet

    if ($null -ne $received) { $receivedOrders++ }
}

Write-Detail ($orderPlans.Count.ToString() + ' bons de commande, ' + $approvedOrders + ' approuves, ' + $receivedOrders + ' receptionnes')

# ------------------------------------------------------------ Cuisine et HACCP (11.5)

Write-Step 'Fiches techniques et releves HACCP (module 11.5)'

$recipes = @(
    @{ Code = 'FT-001'; Name = 'Chorba frik'; Category = 'Entree'; Portions = 20; Allergens = 'Gluten, celeri'; Ingredients = @(@{ Item = 'ALI-AGN'; Quantity = 1.2 }, @{ Item = 'ALI-TOM'; Quantity = 2 }, @{ Item = 'ALI-OIG'; Quantity = 1.5 }, @{ Item = 'ALI-EPI'; Quantity = 0.05 }) },
    @{ Code = 'FT-002'; Name = 'Couscous royal'; Category = 'Plat'; Portions = 25; Allergens = 'Gluten'; Ingredients = @(@{ Item = 'ALI-SEM'; Quantity = 4 }, @{ Item = 'ALI-AGN'; Quantity = 3 }, @{ Item = 'ALI-POU'; Quantity = 2.5 }, @{ Item = 'ALI-CAR'; Quantity = 3 }, @{ Item = 'ALI-OIG'; Quantity = 2 }) },
    @{ Code = 'FT-003'; Name = 'Tajine de poulet aux olives'; Category = 'Plat'; Portions = 20; Allergens = ''; Ingredients = @(@{ Item = 'ALI-POU'; Quantity = 4 }, @{ Item = 'ALI-OIG'; Quantity = 1.5 }, @{ Item = 'ALI-HUI'; Quantity = 0.3 }, @{ Item = 'ALI-EPI'; Quantity = 0.08 }) },
    @{ Code = 'FT-004'; Name = 'Filet de merlan grille'; Category = 'Plat'; Portions = 15; Allergens = 'Poisson'; Ingredients = @(@{ Item = 'ALI-POI'; Quantity = 3 }, @{ Item = 'ALI-HUI'; Quantity = 0.2 }, @{ Item = 'ALI-SEL'; Quantity = 0.05 }) },
    @{ Code = 'FT-005'; Name = 'Gratin de pommes de terre'; Category = 'Plat'; Portions = 20; Allergens = 'Lait'; Ingredients = @(@{ Item = 'ALI-POM'; Quantity = 5 }, @{ Item = 'ALI-LAI'; Quantity = 2 }, @{ Item = 'ALI-FRO'; Quantity = 1 }, @{ Item = 'ALI-BEU'; Quantity = 0.4 }) },
    @{ Code = 'FT-006'; Name = 'Riz pilaf aux legumes'; Category = 'Plat'; Portions = 25; Allergens = ''; Ingredients = @(@{ Item = 'ALI-RIZ'; Quantity = 3 }, @{ Item = 'ALI-CAR'; Quantity = 1.5 }, @{ Item = 'ALI-OIG'; Quantity = 1 }) },
    @{ Code = 'FT-007'; Name = 'Boeuf braise aux carottes'; Category = 'Plat'; Portions = 18; Allergens = ''; Ingredients = @(@{ Item = 'ALI-BOE'; Quantity = 3.5 }, @{ Item = 'ALI-CAR'; Quantity = 2.5 }, @{ Item = 'ALI-OIG'; Quantity = 1.2 }) },
    @{ Code = 'FT-008'; Name = 'Salade mechouia'; Category = 'Entree'; Portions = 20; Allergens = ''; Ingredients = @(@{ Item = 'ALI-TOM'; Quantity = 3 }, @{ Item = 'ALI-OIG'; Quantity = 1 }, @{ Item = 'ALI-HUI'; Quantity = 0.25 }) },
    @{ Code = 'FT-009'; Name = 'Makrout aux dattes'; Category = 'Dessert'; Portions = 40; Allergens = 'Gluten'; Ingredients = @(@{ Item = 'ALI-SEM'; Quantity = 2.5 }, @{ Item = 'ALI-DAT'; Quantity = 2 }, @{ Item = 'ALI-BEU'; Quantity = 0.8 }, @{ Item = 'ALI-SUC'; Quantity = 1 }) },
    @{ Code = 'FT-010'; Name = 'Creme patissiere'; Category = 'SousPreparation'; Portions = 30; Allergens = 'Lait, oeuf, gluten'; Ingredients = @(@{ Item = 'ALI-LAI'; Quantity = 3 }, @{ Item = 'ALI-OEU'; Quantity = 1 }, @{ Item = 'ALI-SUC'; Quantity = 0.8 }, @{ Item = 'ALI-FAR'; Quantity = 0.4 }) },
    @{ Code = 'FT-011'; Name = 'Pain de mie maison'; Category = 'SousPreparation'; Portions = 24; Allergens = 'Gluten, lait'; Ingredients = @(@{ Item = 'ALI-FAR'; Quantity = 3 }, @{ Item = 'ALI-LAI'; Quantity = 1 }, @{ Item = 'ALI-BEU'; Quantity = 0.3 }, @{ Item = 'ALI-SEL'; Quantity = 0.06 }) },
    @{ Code = 'FT-012'; Name = 'The a la menthe du salon'; Category = 'Boisson'; Portions = 50; Allergens = ''; Ingredients = @(@{ Item = 'BOI-THE'; Quantity = 0.4 }, @{ Item = 'ALI-SUC'; Quantity = 2 }) }
)

foreach ($recipe in $recipes) {
    $ingredients = @()
    foreach ($ingredient in $recipe.Ingredients) {
        $ingredients += @{
            itemCode = $ingredient.Item
            quantity = $ingredient.Quantity
        }
    }

    $null = Invoke-Api -Method POST -Path '/kitchen/recipes' -Token $tokenAlger -Body @{
        code          = $recipe.Code
        name          = $recipe.Name
        category      = $recipe.Category
        yieldPortions = $recipe.Portions
        allergens     = $recipe.Allergens
        instructions  = 'Preparation selon la fiche technique validee par le chef executif.'
        ingredients   = $ingredients
    } -Quiet
}

Write-Detail ($recipes.Count.ToString() + ' fiches techniques, cout matiere lu depuis le stock')

$checkpoints = @(
    @{ Code = 'CP-FRIG1'; Label = 'Chambre froide positive - cuisine'; Min = 0; Max = 4 },
    @{ Code = 'CP-FRIG2'; Label = 'Chambre froide negative - cuisine'; Min = -22; Max = -18 },
    @{ Code = 'CP-VITR'; Label = 'Vitrine refrigeree buffet'; Min = 2; Max = 6 },
    @{ Code = 'CP-BAIN'; Label = 'Bain-marie service chaud'; Min = 63; Max = 90 },
    @{ Code = 'CP-LAIT'; Label = 'Armoire produits laitiers'; Min = 0; Max = 4 },
    @{ Code = 'CP-CAVE'; Label = 'Cave a boissons'; Min = 8; Max = 14 }
)

foreach ($checkpoint in $checkpoints) {
    $null = Invoke-Api -Method POST -Path '/kitchen/checkpoints' -Token $tokenAlger -Body @{
        code    = $checkpoint.Code
        label   = $checkpoint.Label
        minTemp = $checkpoint.Min
        maxTemp = $checkpoint.Max
    } -Quiet
}

$readingCount = 0
$nonCompliant = 0

foreach ($checkpoint in $checkpoints) {
    for ($dayOffset = 13; $dayOffset -ge 0; $dayOffset--) {
        foreach ($hour in @(7, 15)) {
            $measuredAt = $script:Today.AddDays(-$dayOffset).AddHours($hour)
            $middle = ($checkpoint.Min + $checkpoint.Max) / 2.0
            $halfRange = ($checkpoint.Max - $checkpoint.Min) / 2.0

            # Une mesure sur vingt sort de la plage : c'est ce qui alimente l'ecran des
            # releves non conformes et l'action corrective qui l'accompagne.
            $isDeviation = (($script:Random.Next(0, 20)) -eq 0)

            if ($isDeviation) {
                $value = [math]::Round($checkpoint.Max + (Get-RandomInt 2 6), 1)
                $corrective = 'Groupe froid relance, produits transferes en chambre de secours'
            }
            else {
                $value = [math]::Round($middle + ((($script:Random.NextDouble() * 2.0) - 1.0) * $halfRange * 0.7), 1)
                $corrective = $null
            }

            $body = @{
                checkpointCode = $checkpoint.Code
                valueCelsius   = $value
                measuredAt     = $measuredAt.ToString('yyyy-MM-ddTHH:mm:sszzz')
            }

            if ($null -ne $corrective) { $body['correctiveAction'] = $corrective }

            $reading = Invoke-Api -Method POST -Path '/kitchen/readings' -Token $tokenAlger -Body $body -Quiet

            if ($null -ne $reading) {
                $readingCount++
                if ($isDeviation) { $nonCompliant++ }
            }
        }
    }
}

Write-Detail ($readingCount.ToString() + ' releves de temperature sur 14 jours, ' + $nonCompliant + ' non conformes')
