# =============================================================================================
#  Ressources humaines : departements, postes, collaborateurs, contrats, temps, absences
#  et paie algerienne.
#  Module 21.
# =============================================================================================

$tokenRh = Get-Token 'Rh'

Write-Step 'Departements et postes (module 21)'

$departments = @(
    @{ Code = 'DIR'; Label = 'Direction generale' },
    @{ Code = 'HEB'; Label = 'Hebergement et reception' },
    @{ Code = 'ETG'; Label = 'Etages et housekeeping' },
    @{ Code = 'RES'; Label = 'Restauration et salle' },
    @{ Code = 'CUI'; Label = 'Cuisine et production' },
    @{ Code = 'TEC'; Label = 'Technique et maintenance' },
    @{ Code = 'ADM'; Label = 'Administration et finances' },
    @{ Code = 'COM'; Label = 'Commercial et evenementiel' }
)

foreach ($department in $departments) {
    $null = Invoke-Api -Method POST -Path '/hr/departments' -Token $tokenRh -Body @{
        code  = $department.Code
        label = $department.Label
    } -Quiet
}

# Salaires bruts mensuels de base, en dinars. Ils servent de plancher au poste et de reference
# aux contrats crees ensuite : sans eux la paie generee n'aurait aucun sens.
$positions = @(
    @{ Code = 'DIR-UNI'; Label = 'Directeur d''unite'; Department = 'DIR'; Minimum = 180000 },
    @{ Code = 'ADJ-DIR'; Label = 'Directeur adjoint'; Department = 'DIR'; Minimum = 140000 },
    @{ Code = 'CHF-REC'; Label = 'Chef de reception'; Department = 'HEB'; Minimum = 92000 },
    @{ Code = 'REC-PTN'; Label = 'Receptionniste'; Department = 'HEB'; Minimum = 58000 },
    @{ Code = 'BAG-AGT'; Label = 'Bagagiste'; Department = 'HEB'; Minimum = 42000 },
    @{ Code = 'GOU-ETG'; Label = 'Gouvernante d''etage'; Department = 'ETG'; Minimum = 72000 },
    @{ Code = 'FDC-ETG'; Label = 'Femme ou valet de chambre'; Department = 'ETG'; Minimum = 42000 },
    @{ Code = 'LIN-AGT'; Label = 'Agent de lingerie'; Department = 'ETG'; Minimum = 44000 },
    @{ Code = 'MAI-RES'; Label = 'Maitre d''hotel'; Department = 'RES'; Minimum = 78000 },
    @{ Code = 'SER-RES'; Label = 'Serveur de restaurant'; Department = 'RES'; Minimum = 46000 },
    @{ Code = 'BAR-RES'; Label = 'Barman'; Department = 'RES'; Minimum = 50000 },
    @{ Code = 'CHF-CUI'; Label = 'Chef de cuisine'; Department = 'CUI'; Minimum = 125000 },
    @{ Code = 'SOU-CUI'; Label = 'Sous-chef de partie'; Department = 'CUI'; Minimum = 78000 },
    @{ Code = 'COM-CUI'; Label = 'Commis de cuisine'; Department = 'CUI'; Minimum = 44000 },
    @{ Code = 'PLO-CUI'; Label = 'Plongeur'; Department = 'CUI'; Minimum = 38000 },
    @{ Code = 'TEC-MAI'; Label = 'Technicien de maintenance'; Department = 'TEC'; Minimum = 68000 },
    @{ Code = 'ELE-TEC'; Label = 'Electricien'; Department = 'TEC'; Minimum = 64000 },
    @{ Code = 'COM-ADM'; Label = 'Comptable'; Department = 'ADM'; Minimum = 88000 },
    @{ Code = 'RH-ADM'; Label = 'Gestionnaire des ressources humaines'; Department = 'ADM'; Minimum = 82000 },
    @{ Code = 'ATT-COM'; Label = 'Attache commercial'; Department = 'COM'; Minimum = 76000 }
)

foreach ($position in $positions) {
    $null = Invoke-Api -Method POST -Path '/hr/positions' -Token $tokenRh -Body @{
        code                = $position.Code
        label               = $position.Label
        departmentCode      = $position.Department
        minimumGrossSalary  = $position.Minimum
    } -Quiet
}

Write-Detail ($departments.Count.ToString() + ' departements, ' + $positions.Count + ' postes')

# ----------------------------------------------------------------------- Collaborateurs

Write-Step 'Collaborateurs et contrats (module 21)'

$firstNames = @('Amine', 'Sofiane', 'Yasmine', 'Karima', 'Bilal', 'Nawel', 'Redouane', 'Assia', 'Toufik', 'Meriem',
    'Djamel', 'Hayet', 'Riad', 'Souhila', 'Abdelkader', 'Nassima', 'Islam', 'Fadila', 'Zineddine', 'Ryma',
    'Mehdi', 'Sabrina', 'Lotfi', 'Chahrazed', 'Walid', 'Imene', 'Fouad', 'Kenza', 'Salim', 'Dounia',
    'Hicham', 'Radia', 'Anis', 'Wassila')

$lastNames = @('Bouzid', 'Hamidi', 'Belkacemi', 'Zerrouki', 'Mokrani', 'Cherifi', 'Larbi', 'Bendjelloul', 'Tounsi', 'Guerrache',
    'Amrani', 'Slimani', 'Benhamou', 'Kessai', 'Ouadah', 'Meddour', 'Berkane', 'Haddadi', 'Nedjari', 'Boukhalfa',
    'Sahraoui', 'Ferradj', 'Bencherif', 'Aouissi', 'Latreche', 'Ghezali', 'Yahiaoui', 'Merabet', 'Chelbi', 'Douadi',
    'Rahal', 'Benmoussa', 'Kaddour', 'Sadouki')

$script:Employees = @()
$employeeIndex = 0

foreach ($position in $positions) {
    # Deux collaborateurs par poste sur les grandes unites, un seul sur les petites : l'effectif
    # obtenu ressemble a un organigramme d'exploitation, pas a une liste uniforme.
    $headcount = 2
    if ($position.Minimum -ge 120000) { $headcount = 1 }

    for ($slot = 0; $slot -lt $headcount; $slot++) {
        $employeeIndex++
        if ($employeeIndex -gt $firstNames.Count) { break }

        $unit = $script:Units[($employeeIndex - 1) % $script:Units.Count]
        $hireDate = $script:Today.AddDays(-(Get-RandomInt 400 2600))

        $employee = Invoke-Api -Method POST -Path '/hr/employees' -Token $tokenRh -Body @{
            employeeNumber          = ('EMP-' + $employeeIndex.ToString('0000'))
            firstName               = $firstNames[$employeeIndex - 1]
            lastName                = $lastNames[$employeeIndex - 1]
            hotelUnitCode           = $unit.Code
            positionCode            = $position.Code
            hireDate                = (Get-Date-Text $hireDate)
            email                   = ($firstNames[$employeeIndex - 1].ToLower() + '.' + $lastNames[$employeeIndex - 1].ToLower() + '@elbahdja-demo.dz')
            phone                   = ('+213 5' + (Get-RandomInt 50 59) + ' ' + (Get-RandomInt 10 99) + ' ' + (Get-RandomInt 10 99) + ' ' + (Get-RandomInt 10 99))
            nationalIdentityNumber  = ((Get-RandomInt 100000000000 999999999999).ToString())
            socialSecurityNumber    = ((Get-RandomInt 1000000000 9999999999).ToString())
            bankAccountNumber       = ('00' + (Get-RandomInt 1 9) + ' 00' + (Get-RandomInt 100 999) + ' ' + (Get-RandomInt 1000000000 9999999999) + ' ' + (Get-RandomInt 10 99))
            badgeId                 = ('BDG-' + $employeeIndex.ToString('0000'))
            dependentChildren       = (Get-RandomInt 0 4)
        } -Quiet

        if ($null -eq $employee) { continue }

        # Salaire au-dessus du plancher du poste, avec une dispersion d'anciennete.
        $gross = [math]::Round(($position.Minimum * (1.0 + ($script:Random.NextDouble() * 0.28))) / 500.0) * 500

        $contractType = 'Permanent'
        $contractEnd = $null

        if ($employeeIndex % 9 -eq 0) {
            $contractType = 'FixedTerm'
            $contractEnd = $script:Today.AddDays((Get-RandomInt 60 240))
        }
        elseif ($employeeIndex % 13 -eq 0) {
            $contractType = 'Seasonal'
            $contractEnd = $script:Today.AddDays((Get-RandomInt 20 90))
        }

        $contractBody = @{
            type        = $contractType
            startDate   = (Get-Date-Text $hireDate)
            grossSalary = $gross
            weeklyHours = 40
        }

        if ($null -ne $contractEnd) { $contractBody['endDate'] = (Get-Date-Text $contractEnd) }

        $null = Invoke-Api -Method POST -Path ('/hr/employees/' + $employee.id + '/contracts') -Token $tokenRh -Body $contractBody -Quiet

        $script:Employees += [pscustomobject]@{
            Id       = $employee.id
            Number   = $employee.employeeNumber
            UnitCode = $unit.Code
            Gross    = $gross
        }
    }
}

Write-Detail ($script:Employees.Count.ToString() + ' collaborateurs, contrats CDI, CDD et saisonniers')

# -------------------------------------------------------------------- Temps et absences

Write-Step 'Temps de travail et absences (module 21)'

$period = $script:Today.AddMonths(-1)
$periodText = $period.ToString('yyyy-MM')
$daysInPeriod = [datetime]::DaysInMonth($period.Year, $period.Month)

$timeEntryCount = 0

foreach ($employee in ($script:Employees | Select-Object -First 18)) {
    for ($day = 1; $day -le 10; $day++) {
        $workDate = [datetime]::new($period.Year, $period.Month, $day)
        if ($workDate.DayOfWeek -eq [System.DayOfWeek]::Friday) { continue }

        # Huit heures de base, avec des heures supplementaires une fois sur quatre : la paie
        # calcule alors une majoration, ce que le bulletin doit pouvoir montrer.
        $hours = 8
        if (($script:Random.Next(0, 4)) -eq 0) { $hours = 8 + (Get-RandomInt 1 3) }

        $entry = Invoke-Api -Method POST -Path '/hr/time-entries' -Token $tokenRh -Body @{
            employeeId  = $employee.Id
            workDate    = (Get-Date-Text $workDate)
            hoursWorked = $hours
            source      = 'Manual'
        } -Quiet

        if ($null -eq $entry) { continue }

        $timeEntryCount++
        $null = Invoke-Api -Method POST -Path ('/hr/time-entries/' + $entry.id + '/validate') -Token $tokenRh -Quiet
    }
}

Write-Detail ($timeEntryCount.ToString() + ' pointages saisis et valides sur ' + $periodText)

$absenceTypes = @('AnnualLeave', 'SickLeave', 'UnpaidLeave', 'Maternity', 'Exceptional')
$absenceCount = 0
$absenceIndex = 0

foreach ($employee in ($script:Employees | Select-Object -First 14)) {
    $absenceIndex++
    $start = $script:Today.AddDays((Get-RandomInt -25 20))
    $type = Get-RandomPick $absenceTypes

    $absence = Invoke-Api -Method POST -Path '/hr/absences' -Token $tokenRh -Body @{
        employeeId = $employee.Id
        type       = $type
        startDate  = (Get-Date-Text $start)
        endDate    = (Get-Date-Text $start.AddDays((Get-RandomInt 1 8)))
        reason     = 'Demande deposee par le collaborateur'
    } -Quiet

    if ($null -eq $absence) { continue }

    $absenceCount++

    # Un tiers reste en attente de decision, une sur sept est refusee : sans cela l'ecran ne
    # montrerait qu'une colonne de "approuve" et le circuit resterait invisible.
    if ($absenceIndex % 3 -eq 0) { continue }

    if ($absenceIndex % 7 -eq 0) {
        $null = Invoke-Api -Method POST -Path ('/hr/absences/' + $absence.id + '/reject') -Token $tokenRh -Body @{
            note = 'Periode de forte activite, report demande'
        } -Quiet
    }
    else {
        $null = Invoke-Api -Method POST -Path ('/hr/absences/' + $absence.id + '/approve') -Token $tokenRh -Body @{
            note = 'Accord du responsable hierarchique'
        } -Quiet
    }
}

Write-Detail ($absenceCount.ToString() + ' absences : conges, maladie, maternite et exceptionnelles')

# ------------------------------------------------------------------------------- Paie

Write-Step 'Parametres et periode de paie (module 21)'

# Bareme statutaire algerien tel que le domaine le porte comme valeur de reference
# (PayrollParameterSet.CreateStatutoryDefault) : taux exprimes en fraction, pas en pourcentage.
$null = Invoke-Api -Method POST -Path '/hr/payroll/parameters' -Token $tokenRh -Body @{
    effectiveFrom              = ([datetime]::new($script:Today.Year, 1, 1)).ToString('yyyy-MM')
    label                      = ('Bareme statutaire ' + $script:Today.Year)
    monthlyReferenceHours      = 173.33
    overtimeMultiplier         = 1.5
    referenceDaysPerMonth      = 30
    employeeSocialRate         = 0.09
    employerSocialRate         = 0.26
    workAccidentRate           = 0.0125
    unemploymentInsuranceRate  = 0.015
    vocationalTrainingRate     = 0.01
    incomeTaxAbatement         = 40000
    incomeTaxAbatementPerChild = 1000
    minimumWage                = 20000
    brackets                   = @(
        @{ upperBound = 30000; rate = 0.23 },
        @{ upperBound = 120000; rate = 0.27 },
        @{ upperBound = $null; rate = 0.33 }
    )
} -Quiet

Write-Detail ('Bareme ' + $script:Today.Year + ' : cotisations, abattements IRG et tranches')

# Primes du mois : une paie sans prime ne montre ni la ligne, ni son effet sur le net.
$bonusPlans = @(
    @{ Code = 'PRIME-REND'; Label = 'Prime de rendement'; Amount = 12000 },
    @{ Code = 'PRIME-NUIT'; Label = 'Prime de travail de nuit'; Amount = 8000 },
    @{ Code = 'PRIME-PANIER'; Label = 'Prime de panier'; Amount = 6000 }
)

$bonusCount = 0
foreach ($employee in ($script:Employees | Select-Object -First 12)) {
    $bonus = Get-RandomPick $bonusPlans

    $result = Invoke-Api -Method POST -Path ('/hr/payroll/periods/' + $periodText + '/bonuses') -Token $tokenRh -Body @{
        employeeId = $employee.Id
        code       = $bonus.Code
        label      = $bonus.Label
        amount     = $bonus.Amount
    } -Quiet

    if ($null -ne $result) { $bonusCount++ }
}

$generated = Invoke-Api -Method POST -Path ('/hr/payroll/periods/' + $periodText + '/generate') -Token $tokenRh -Quiet

if ($null -ne $generated) {
    $payslips = Invoke-Api -Method GET -Path ('/hr/payroll/periods/' + $periodText + '/payslips') -Token $tokenRh -Quiet

    $payslipList = @()
    if ($null -ne $payslips) {
        if ($payslips -is [array]) { $payslipList = $payslips }
        elseif ($null -ne $payslips.items) { $payslipList = $payslips.items }
    }

    $validated = 0
    $payslipIndex = 0

    foreach ($payslip in $payslipList) {
        $payslipIndex++

        # Quelques bulletins restent en brouillon : la periode ne peut alors pas etre cloturee,
        # et le guide peut expliquer pourquoi - c'est exactement le controle attendu.
        if ($payslipIndex % 9 -eq 0) { continue }

        $result = Invoke-Api -Method POST -Path ('/hr/payroll/periods/' + $periodText + '/payslips/' + $payslip.id + '/validate') -Token $tokenRh -Quiet
        if ($null -ne $result) { $validated++ }
    }

    Write-Detail ($payslipList.Count.ToString() + ' bulletins generes pour ' + $periodText + ', ' + $validated + ' valides, ' + $bonusCount + ' primes')
}
else {
    Write-Detail ('Generation de la paie ' + $periodText + ' non aboutie : voir les avertissements')
}
