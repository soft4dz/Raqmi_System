# =============================================================================================
#  Pilotage et systeme : circuits de validation, rapports, registre des postes et sauvegarde.
#  Modules 22.2, 25, 28, 29.
# =============================================================================================

$tokenControle = Get-Token 'Controle'
$tokenDirection = Get-Token 'Direction'

# ------------------------------------------------------------ Circuits de validation (22.2)

Write-Step 'Circuits de validation (module 22.2)'

# Un seul type de sujet est cable a ce jour (les ordres de paiement) : le circuit le dit
# franchement plutot que d'annoncer une portee que le produit n'a pas encore.
$circuit = Invoke-Api -Method POST -Path '/approvals/circuits' -Token $tokenControle -Body @{
    code        = 'CIR-PAIEMENT'
    label       = 'Validation des ordres de paiement'
    subjectType = 'PaymentOrder'
    steps       = @(
        @{ label = 'Controle de gestion'; requiredRole = 'exploitation.control' },
        @{ label = 'Direction generale'; requiredRole = 'direction' }
    )
} -Quiet

if ($null -ne $circuit) { Write-Detail 'CIR-PAIEMENT : controle de gestion puis direction generale' }

$instanceCount = 0
$approvedInstances = 0
$orderIndex = 0

foreach ($order in $script:PaymentOrders) {
    $orderIndex++
    if ($orderIndex -gt 8) { break }

    $instance = Invoke-Api -Method POST -Path '/approvals/instances' -Token $tokenControle -Body @{
        subjectType      = 'PaymentOrder'
        subjectReference = $order.reference
    } -Quiet

    if ($null -eq $instance) { continue }

    $instanceCount++

    # Les trois derniers restent en cours : la file "en attente de ma decision" doit avoir
    # quelque chose a montrer, sinon l'ecran du guide est vide.
    if ($orderIndex -gt 5) { continue }

    $firstStep = Invoke-Api -Method POST -Path ('/approvals/instances/' + $instance.id + '/approve') -Token $tokenControle -Body @{
        comment = 'Piece justificative verifiee, imputation conforme'
    } -Quiet

    if ($null -eq $firstStep) { continue }

    if ($orderIndex -eq 4) {
        $null = Invoke-Api -Method POST -Path ('/approvals/instances/' + $instance.id + '/reject') -Token $tokenDirection -Body @{
            comment = 'Montant a renegocier avec le fournisseur avant engagement'
        } -Quiet
        continue
    }

    $secondStep = Invoke-Api -Method POST -Path ('/approvals/instances/' + $instance.id + '/approve') -Token $tokenDirection -Body @{
        comment = 'Bon pour paiement'
    } -Quiet

    if ($null -ne $secondStep) { $approvedInstances++ }
}

Write-Detail ($instanceCount.ToString() + ' demandes ouvertes, ' + $approvedInstances + ' approuvees, 1 rejetee, 3 en cours')

# ------------------------------------------------------------------ Rapports (module 25)

Write-Step 'Rapports automatiques (module 25)'

$from = Get-Date-Text $script:Today.AddDays(-30)
$to = Get-Date-Text $script:Today
$asOf = Get-Date-Text $script:Today

$reportRuns = @(
    @{ Code = 'recettes-par-unite'; Parameters = @{ from = $from; to = $to } },
    @{ Code = 'recettes-par-unite'; Parameters = @{ from = (Get-Date-Text $script:Today.AddDays(-7)); to = $to; unitCode = 'ALG-CEN' } },
    @{ Code = 'encaissements-par-mode'; Parameters = @{ from = $from; to = $to } },
    @{ Code = 'encaissements-par-mode'; Parameters = @{ from = $from; to = $to; unitCode = 'ORN-COR' } },
    @{ Code = 'balance-agee'; Parameters = @{ asOfDate = $asOf } },
    @{ Code = 'tva-facturee'; Parameters = @{ from = (Get-Date-Text $script:Today.AddDays(-60)); to = $to } },
    @{ Code = 'occupation-par-unite'; Parameters = @{ from = $from; to = $to } },
    @{ Code = 'occupation-par-unite'; Parameters = @{ from = $from; to = $to; unitCode = 'TIP-AZU' } }
)

$reportCount = 0
foreach ($run in $reportRuns) {
    $result = Invoke-Api -Method POST -Path '/reporting/run' -Token $tokenControle -Body @{
        code       = $run.Code
        parameters = $run.Parameters
    } -Quiet

    if ($null -ne $result) { $reportCount++ }
}

Write-Detail ($reportCount.ToString() + ' executions de rapports tracees au journal')

# --------------------------------------------------- Registre des postes clients (29)

Write-Step 'Registre des postes et erreurs clients (module 29)'

# Les postes s'annoncent eux-memes par un battement de coeur : le module ne synchronise rien,
# il tient un registre de ce qui s'est manifeste. Le jeu de demonstration simule donc ce que
# feraient de vrais postes en exploitation.
$stations = @(
    @{ Id = '9f1c4a52-6d1b-4d0e-9a4f-6f3b2c8e1a01'; Label = 'RECEPTION-ALG-01'; Unit = 'ALG-CEN'; Version = '1.0.0' },
    @{ Id = '9f1c4a52-6d1b-4d0e-9a4f-6f3b2c8e1a02'; Label = 'RECEPTION-ALG-02'; Unit = 'ALG-CEN'; Version = '1.0.0' },
    @{ Id = '9f1c4a52-6d1b-4d0e-9a4f-6f3b2c8e1a03'; Label = 'CAISSE-ALG-01'; Unit = 'ALG-CEN'; Version = '1.0.0' },
    @{ Id = '9f1c4a52-6d1b-4d0e-9a4f-6f3b2c8e1a04'; Label = 'RECEPTION-ORN-01'; Unit = 'ORN-COR'; Version = '1.0.0' },
    @{ Id = '9f1c4a52-6d1b-4d0e-9a4f-6f3b2c8e1a05'; Label = 'DIRECTION-ORN'; Unit = 'ORN-COR'; Version = '0.9.4' },
    @{ Id = '9f1c4a52-6d1b-4d0e-9a4f-6f3b2c8e1a06'; Label = 'ACCUEIL-TIP-01'; Unit = 'TIP-AZU'; Version = '1.0.0' },
    @{ Id = '9f1c4a52-6d1b-4d0e-9a4f-6f3b2c8e1a07'; Label = 'CAPITAINERIE-BEJ'; Unit = 'BEJ-CAP'; Version = '0.9.4' }
)

foreach ($station in $stations) {
    $null = Invoke-Api -Method POST -Path '/sync/stations/heartbeat' -Body @{
        stationId     = $station.Id
        label         = $station.Label
        appVersion    = $station.Version
        hotelUnitCode = $station.Unit
    } -Quiet
}

Write-Detail ($stations.Count.ToString() + ' postes declares, dont deux sur une version anterieure')

# Erreurs remontees par les postes : ce sont celles que le client n'a pas pu envoyer sur le
# moment et qu'il rejoue ensuite. Elles doivent ressembler a de vraies pannes de terrain.
$failureTemplates = @(
    @{ Method = 'POST'; Path = '/api/v1/revenue/daily'; Status = $null; Kind = 'NetworkFailure'; Message = 'Le serveur est injoignable depuis ce poste.' },
    @{ Method = 'POST'; Path = '/api/v1/treasury/receipts'; Status = 503; Kind = 'ServerUnavailable'; Message = 'Service temporairement indisponible.' },
    @{ Method = 'GET'; Path = '/api/v1/lodging/front-desk'; Status = $null; Kind = 'Timeout'; Message = 'Delai d''attente depasse au bout de 90 secondes.' },
    @{ Method = 'POST'; Path = '/api/v1/lodging/reservations'; Status = 409; Kind = 'Conflict'; Message = 'La chambre est deja occupee sur la periode demandee.' },
    @{ Method = 'PUT'; Path = '/api/v1/billing/invoices'; Status = 403; Kind = 'Forbidden'; Message = 'Permission invoices.issue requise.' },
    @{ Method = 'POST'; Path = '/api/v1/housekeeping/tasks/generate'; Status = 500; Kind = 'ServerError'; Message = 'Erreur interne lors de la generation des taches.' }
)

$failureCount = 0
foreach ($station in ($stations | Select-Object -First 4)) {
    $items = @()
    $itemCount = Get-RandomInt 1 3

    for ($index = 0; $index -lt $itemCount; $index++) {
        $template = Get-RandomPick $failureTemplates
        $claimedAt = $script:Today.AddDays(-(Get-RandomInt 0 9)).AddHours((Get-RandomInt 7 20))

        $item = @{
            eventId     = [guid]::NewGuid().ToString()
            method      = $template.Method
            path        = $template.Path
            kind        = $template.Kind
            message     = $template.Message
            claimedAtUtc = $claimedAt.ToString('yyyy-MM-ddTHH:mm:sszzz')
        }

        if ($null -ne $template.Status) { $item['statusCode'] = $template.Status }

        $items += $item
    }

    $reported = Invoke-Api -Method POST -Path '/sync/stations/failures' -Body @{
        stationId = $station.Id
        items     = $items
    } -Quiet

    if ($null -ne $reported) { $failureCount += $items.Count }
}

Write-Detail ($failureCount.ToString() + ' erreurs clients remontees au registre')

# --------------------------------------------------------------- Sauvegarde (module 28)

Write-Step 'Sauvegarde (module 28)'

# Ne reussit que si BACKUP_DIR et RAQMI_PG_BIN sont configures sur l'API. En leur absence le
# module reste consultable et l'ecran explique ce qui manque : c'est un etat legitime du
# produit, pas un echec du jeu de demonstration.
$backup = Invoke-Api -Method POST -Path '/maintenance/backups/trigger' -Quiet

if ($null -ne $backup) { Write-Detail 'Sauvegarde a la demande declenchee et enregistree' }
else { Write-Detail 'Sauvegarde non declenchee (BACKUP_DIR / RAQMI_PG_BIN non configures sur l''API)' }
