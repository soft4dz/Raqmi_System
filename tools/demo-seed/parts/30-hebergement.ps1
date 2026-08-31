# =============================================================================================
#  Hebergement : types de chambre, chambres, tarifs, conventions, reservations, folios,
#  housekeeping et minibar.
#  Modules 10, 10.2, 14.5.
# =============================================================================================

$tokenControle = Get-Token 'Controle'
$tokenCaisse = Get-Token 'Caisse'

# ------------------------------------------------------------------- Types de chambre (10)

Write-Step 'Types de chambre et chambres (module 10)'

# Chaque unite a la nomenclature de son metier : un hotel vend des chambres, une residence
# vend des appartements, une marina vend des bungalows. Reprendre partout SGL/DBL aurait
# donne un referentiel qui ne ressemble a aucun de ces trois etablissements.
$roomTypeCatalog = @{
    'ALG-CEN' = @(
        @{ Code = 'SGL'; Label = 'Chambre simple'; Capacity = 1; Description = 'Lit simple, bureau, vue ville' },
        @{ Code = 'DBL'; Label = 'Chambre double'; Capacity = 2; Description = 'Lit double, salon, vue baie' },
        @{ Code = 'TWN'; Label = 'Chambre twin'; Capacity = 2; Description = 'Deux lits simples' },
        @{ Code = 'STE'; Label = 'Suite executive'; Capacity = 3; Description = 'Salon separe, bureau, service majordome' }
    )
    'ORN-COR' = @(
        @{ Code = 'SGL'; Label = 'Chambre simple'; Capacity = 1; Description = 'Lit simple, vue jardin' },
        @{ Code = 'DBL'; Label = 'Chambre double'; Capacity = 2; Description = 'Lit double, balcon vue mer' },
        @{ Code = 'TWN'; Label = 'Chambre twin'; Capacity = 2; Description = 'Deux lits simples, balcon' },
        @{ Code = 'STE'; Label = 'Suite panoramique'; Capacity = 4; Description = 'Terrasse privative sur la corniche' }
    )
    'TIP-AZU' = @(
        @{ Code = 'STU'; Label = 'Studio'; Capacity = 2; Description = 'Kitchenette equipee, terrasse' },
        @{ Code = 'F2'; Label = 'Appartement F2'; Capacity = 4; Description = 'Une chambre, sejour, cuisine' },
        @{ Code = 'F3'; Label = 'Appartement F3'; Capacity = 6; Description = 'Deux chambres, sejour, cuisine' }
    )
    'BEJ-CAP' = @(
        @{ Code = 'BUN'; Label = 'Bungalow bord de mer'; Capacity = 4; Description = 'Acces direct ponton' },
        @{ Code = 'CAB'; Label = 'Cabine equipage'; Capacity = 2; Description = 'Hebergement equipage, sanitaires partages' }
    )
}

$script:Rooms = @{}
$roomTotal = 0

foreach ($unit in $script:Units) {
    $unitToken = Get-UnitToken $unit.Code
    $types = $roomTypeCatalog[$unit.Code]

    foreach ($type in $types) {
        $null = Invoke-Api -Method POST -Path '/lodging/room-types' -Token $unitToken -Body @{
            hotelUnitCode = $unit.Code
            code          = $type.Code
            label         = $type.Label
            capacity      = $type.Capacity
            description   = $type.Description
        } -Quiet
    }

    $script:Rooms[$unit.Code] = @()

    for ($number = 1; $number -le $unit.Rooms; $number++) {
        $floor = [math]::Ceiling($number / 10)
        $type = $types[($number - 1) % $types.Count]

        $room = Invoke-Api -Method POST -Path '/lodging/rooms' -Token $unitToken -Body @{
            hotelUnitCode = $unit.Code
            number        = ($floor.ToString() + (($number - 1) % 10 + 1).ToString('00'))
            roomTypeCode  = $type.Code
            floor         = ('Niveau ' + $floor)
        } -Quiet

        if ($null -eq $room) { continue }

        $script:Rooms[$unit.Code] += [pscustomobject]@{
            Id       = $room.id
            Number   = $room.number
            TypeCode = $type.Code
        }

        $roomTotal++
    }

    Write-Detail ($unit.Code + ' - ' + $types.Count + ' types, ' + $script:Rooms[$unit.Code].Count + ' chambres')
}

# --------------------------------------------------------------- Tarifs et conventions (14.5)

Write-Step 'Plans tarifaires et conventions (module 14.5)'

# Prix de reference par type, en dinars et par nuit. La haute saison majore de 35 %.
$nightlyRates = @{
    'SGL' = 8900; 'DBL' = 12500; 'TWN' = 12500; 'STE' = 24000
    'STU' = 9500; 'F2' = 14000; 'F3' = 19000
    'BUN' = 16000; 'CAB' = 6500
}

$lowSeasonFrom = [datetime]::new($script:Today.Year, 1, 1)
$lowSeasonTo = [datetime]::new($script:Today.Year, 5, 31)
$highSeasonFrom = [datetime]::new($script:Today.Year, 6, 1)
$highSeasonTo = [datetime]::new($script:Today.Year, 9, 30)
$shoulderFrom = [datetime]::new($script:Today.Year, 10, 1)
$shoulderTo = [datetime]::new($script:Today.Year, 12, 31)

$ratePlanCount = 0

foreach ($unit in $script:Units) {
    $types = $roomTypeCatalog[$unit.Code]

    $plans = @(
        @{ Code = ('PUB-' + $unit.Code); Label = 'Tarif public affiche'; IsDefault = $true; Factor = 1.0 },
        @{ Code = ('COR-' + $unit.Code); Label = 'Tarif entreprises conventionnees'; IsDefault = $false; Factor = 0.85 }
    )

    foreach ($plan in $plans) {
        $created = Invoke-Api -Method POST -Path '/tariffs/plans' -Token $tokenControle -Body @{
            code          = $plan.Code
            label         = $plan.Label
            hotelUnitCode = $unit.Code
            isDefault     = $plan.IsDefault
        } -Quiet

        if ($null -eq $created) { continue }

        $ratePlanCount++

        foreach ($type in $types) {
            $base = $nightlyRates[$type.Code] * $plan.Factor

            $seasons = @(
                @{ From = $lowSeasonFrom; To = $lowSeasonTo; Factor = 1.0 },
                @{ From = $highSeasonFrom; To = $highSeasonTo; Factor = 1.35 },
                @{ From = $shoulderFrom; To = $shoulderTo; Factor = 1.1 }
            )

            foreach ($season in $seasons) {
                $null = Invoke-Api -Method POST -Path ('/tariffs/plans/' + $plan.Code + '/periods') -Token $tokenControle -Body @{
                    roomTypeCode  = $type.Code
                    fromDate      = (Get-Date-Text $season.From)
                    toDate        = (Get-Date-Text $season.To)
                    nightlyAmount = ([math]::Round(($base * $season.Factor) / 100.0) * 100)
                } -Quiet
            }
        }
    }
}

Write-Detail ($ratePlanCount.ToString() + ' plans tarifaires, trois saisons par type de chambre')

$conventionCustomers = @('CL-0001', 'CL-0002', 'CL-0004', 'CL-0007', 'CL-0014', 'CL-0016')
$conventionCount = 0

foreach ($customerCode in $conventionCustomers) {
    $unit = Get-RandomPick $script:Units

    $convention = Invoke-Api -Method POST -Path '/tariffs/conventions' -Token $tokenControle -Body @{
        customerCode    = $customerCode
        ratePlanCode    = ('COR-' + $unit.Code)
        discountPercent = (Get-RandomPick @(5, 8, 10, 12, 15))
        fromDate        = (Get-Date-Text ([datetime]::new($script:Today.Year, 1, 1)))
        toDate          = (Get-Date-Text ([datetime]::new($script:Today.Year, 12, 31)))
    } -Quiet

    if ($null -ne $convention) { $conventionCount++ }
}

Write-Detail ($conventionCount.ToString() + ' conventions clients avec remise negociee')

# ----------------------------------------------------------------------- Reservations (10)

Write-Step 'Reservations, arrivees et departs (module 10)'

$script:CheckedInReservations = @()
$reservationStats = @{ Booked = 0; CheckedIn = 0; CheckedOut = 0; Cancelled = 0; NoShow = 0 }

foreach ($unit in $script:Units) {
    $unitToken = Get-UnitToken $unit.Code
    $rooms = $script:Rooms[$unit.Code]
    $roomIndex = 0

    foreach ($room in $rooms) {
        $roomIndex++
        $customer = Get-RandomPick $script:Customers
        $scenario = $roomIndex % 5

        # Un scenario par chambre, et jamais deux sejours qui se chevauchent sur la meme
        # chambre : l'API refuserait la seconde reservation, et elle aurait raison.
        if ($scenario -eq 0) {
            $arrival = $script:Today.AddDays(-(Get-RandomInt 18 26))
            $departure = $arrival.AddDays((Get-RandomInt 2 4))
            $action = 'CheckedOut'
        }
        elseif ($scenario -eq 1) {
            $arrival = $script:Today.AddDays(-(Get-RandomInt 1 3))
            $departure = $script:Today.AddDays((Get-RandomInt 1 4))
            $action = 'CheckedIn'
        }
        elseif ($scenario -eq 2) {
            $arrival = $script:Today.AddDays((Get-RandomInt 2 18))
            $departure = $arrival.AddDays((Get-RandomInt 1 5))
            $action = 'Booked'
        }
        elseif ($scenario -eq 3) {
            $arrival = $script:Today.AddDays(-(Get-RandomInt 30 40))
            $departure = $arrival.AddDays((Get-RandomInt 2 6))
            $action = 'CheckedOut'
        }
        else {
            $arrival = $script:Today.AddDays(-(Get-RandomInt 5 12))
            $departure = $arrival.AddDays((Get-RandomInt 1 3))
            if ($roomIndex % 10 -eq 4) { $action = 'NoShow' } else { $action = 'Cancelled' }
        }

        $reservation = Invoke-Api -Method POST -Path '/lodging/reservations' -Token $unitToken -Body @{
            hotelUnitCode = $unit.Code
            roomId        = $room.Id
            customerCode  = $customer.Code
            arrivalDate   = (Get-Date-Text $arrival)
            departureDate = (Get-Date-Text $departure)
            guestCount    = (Get-RandomInt 1 3)
        } -Quiet

        if ($null -eq $reservation) { continue }

        if ($action -eq 'Booked') {
            $reservationStats.Booked++
            continue
        }

        if ($action -eq 'Cancelled') {
            $cancelled = Invoke-Api -Method POST -Path ('/lodging/reservations/' + $reservation.id + '/cancel') -Token $unitToken -Body @{
                reason = 'Annulation a la demande du client'
            } -Quiet
            if ($null -ne $cancelled) { $reservationStats.Cancelled++ }
            continue
        }

        if ($action -eq 'NoShow') {
            $noShow = Invoke-Api -Method POST -Path ('/lodging/reservations/' + $reservation.id + '/no-show') -Token $unitToken -Quiet
            if ($null -ne $noShow) { $reservationStats.NoShow++ }
            continue
        }

        $checkedIn = Invoke-Api -Method POST -Path ('/lodging/reservations/' + $reservation.id + '/check-in') -Token $tokenCaisse -Quiet
        if ($null -eq $checkedIn) { continue }

        # Extras portes au folio : sans eux le folio n'affiche que les nuitees et l'ecran
        # perd tout l'interet du module.
        $extras = @(
            @{ Label = 'Petit dejeuner buffet'; Amount = 1400; Kind = 'Extra' },
            @{ Label = 'Minibar - consommations'; Amount = 950; Kind = 'Extra' },
            @{ Label = 'Blanchisserie'; Amount = 1800; Kind = 'Extra' },
            @{ Label = 'Diner restaurant'; Amount = 3600; Kind = 'Extra' },
            @{ Label = 'Transfert aeroport'; Amount = 4500; Kind = 'Extra' }
        )

        $extraCount = Get-RandomInt 1 3
        for ($extra = 0; $extra -lt $extraCount; $extra++) {
            $charge = Get-RandomPick $extras
            $null = Invoke-Api -Method POST -Path ('/lodging/reservations/' + $reservation.id + '/folio/charges') -Token $tokenCaisse -Body @{
                chargeDate = (Get-Date-Text $arrival.AddDays($extra))
                label      = $charge.Label
                amount     = $charge.Amount
                kind       = $charge.Kind
            } -Quiet
        }

        if ($action -eq 'CheckedIn') {
            $reservationStats.CheckedIn++
            $script:CheckedInReservations += [pscustomobject]@{
                Id       = $reservation.id
                UnitCode = $unit.Code
                RoomId   = $room.Id
            }
            continue
        }

        $checkedOut = Invoke-Api -Method POST -Path ('/lodging/reservations/' + $reservation.id + '/check-out') -Token $tokenCaisse -Quiet
        if ($null -ne $checkedOut) { $reservationStats.CheckedOut++ }
    }
}

Write-Detail (
    $reservationStats.CheckedIn.ToString() + ' en cours, ' +
    $reservationStats.Booked + ' a venir, ' +
    $reservationStats.CheckedOut + ' terminees, ' +
    $reservationStats.Cancelled + ' annulees, ' +
    $reservationStats.NoShow + ' no-show')

# ------------------------------------------------------------------- Housekeeping (10.2)

Write-Step 'Housekeeping, inspection et minibar (module 10.2)'

$housekeepers = @('Fatima Belhadj', 'Souad Kaci', 'Hakim Toumi', 'Yamina Cherif', 'Djamel Aissaoui', 'Nabila Rahmani')

foreach ($unit in $script:Units) {
    $unitToken = Get-UnitToken $unit.Code

    # La generation part de l'occupation reelle : departs, recouches et chambres vacantes
    # sortent des reservations creees plus haut, pas d'une liste ecrite a la main.
    foreach ($dayOffset in @(-1, 0)) {
        $null = Invoke-Api -Method POST -Path '/housekeeping/tasks/generate' -Token $unitToken -Body @{
            hotelUnitCode = $unit.Code
            serviceDate   = (Get-Date-Text $script:Today.AddDays($dayOffset))
        } -Quiet
    }
}

$tasks = Invoke-Api -Method GET -Path ('/housekeeping/tasks?serviceDate=' + (Get-Date-Text $script:Today)) -Token $tokenControle -Quiet

$taskList = @()
if ($null -ne $tasks) {
    if ($tasks -is [array]) { $taskList = $tasks }
    elseif ($null -ne $tasks.items) { $taskList = $tasks.items }
}

$taskIndex = 0
$advanced = 0

foreach ($task in $taskList) {
    $taskIndex++
    $unitToken = Get-UnitToken $task.hotelUnitCode

    # Une file de menage credible n'est pas uniformement traitee : on laisse un tiers en
    # attente, on en assigne, on en termine, on en fait inspecter et on en rejette une.
    if ($taskIndex % 4 -eq 0) { continue }

    $assigned = Invoke-Api -Method POST -Path ('/housekeeping/tasks/' + $task.id + '/assign') -Token $unitToken -Body @{
        assignedTo = (Get-RandomPick $housekeepers)
    } -Quiet

    if ($null -eq $assigned) { continue }
    $advanced++

    if ($taskIndex % 4 -eq 1) { continue }

    $null = Invoke-Api -Method POST -Path ('/housekeeping/tasks/' + $task.id + '/start') -Token $unitToken -Quiet
    if ($taskIndex % 8 -eq 3) { continue }

    $completed = Invoke-Api -Method POST -Path ('/housekeeping/tasks/' + $task.id + '/complete') -Token $unitToken -Body @{
        notes = 'Chambre remise en etat, lingerie changee'
    } -Quiet

    if ($null -eq $completed) { continue }

    $accepted = ($taskIndex % 11 -ne 0)
    $notes = 'Controle conforme'
    if (-not $accepted) { $notes = 'Salle de bain a reprendre : joints et miroir' }

    $null = Invoke-Api -Method POST -Path ('/housekeeping/tasks/' + $task.id + '/inspect') -Token $unitToken -Body @{
        accepted = $accepted
        notes    = $notes
    } -Quiet
}

Write-Detail ($taskList.Count.ToString() + ' taches du jour, ' + $advanced + ' prises en charge')

$minibarItems = @(
    @{ Code = 'MB-EAU'; Label = 'Eau minerale 50 cl'; Price = 150 },
    @{ Code = 'MB-SOD'; Label = 'Soda 33 cl'; Price = 250 },
    @{ Code = 'MB-JUS'; Label = 'Jus de fruits 25 cl'; Price = 220 },
    @{ Code = 'MB-CHO'; Label = 'Barre chocolatee'; Price = 180 },
    @{ Code = 'MB-CHI'; Label = 'Chips 45 g'; Price = 160 },
    @{ Code = 'MB-CAF'; Label = 'Dosette de cafe'; Price = 120 },
    @{ Code = 'MB-AMD'; Label = 'Amandes grillees'; Price = 320 },
    @{ Code = 'MB-THE'; Label = 'The a la menthe'; Price = 120 }
)

foreach ($unit in $script:Units) {
    $unitToken = Get-UnitToken $unit.Code

    foreach ($item in $minibarItems) {
        $null = Invoke-Api -Method POST -Path '/housekeeping/minibar/items' -Token $unitToken -Body @{
            hotelUnitCode = $unit.Code
            code          = $item.Code
            label         = $item.Label
            unitPrice     = $item.Price
        } -Quiet
    }
}

$consumptionCount = 0
foreach ($reservation in $script:CheckedInReservations) {
    if (($script:Random.Next(0, 10)) -lt 4) { continue }

    $item = Get-RandomPick $minibarItems

    $recorded = Invoke-Api -Method POST -Path '/housekeeping/minibar/consumptions' -Token $tokenCaisse -Body @{
        reservationId = $reservation.Id
        itemCode      = $item.Code
        quantity      = (Get-RandomInt 1 3)
        consumedOn    = (Get-Date-Text $script:Today.AddDays(-(Get-RandomInt 0 1)))
    } -Quiet

    if ($null -ne $recorded) { $consumptionCount++ }
}

Write-Detail ($minibarItems.Count.ToString() + ' articles de minibar, ' + $consumptionCount + ' consommations relevees')
