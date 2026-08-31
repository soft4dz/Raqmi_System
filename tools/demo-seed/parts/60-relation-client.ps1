# =============================================================================================
#  Relation client : segmentation, fidelite, interactions, campagnes, satisfaction (NPS)
#  puis salles, evenements et facturation evenementielle.
#  Modules 10.4 et 10.6.
# =============================================================================================

$tokenAlger = Get-Token 'ChefAlger'

# ------------------------------------------------------------------ Segments et fidelite

Write-Step 'Segmentation et programme de fidelite (module 10.4)'

$segments = @(
    @{ Code = 'SEG-AFF'; Label = 'Clientele affaires'; Description = 'Sejours courts en semaine, facturation entreprise' },
    @{ Code = 'SEG-LOI'; Label = 'Clientele loisirs'; Description = 'Sejours week-end et vacances, reservation directe' },
    @{ Code = 'SEG-GRP'; Label = 'Groupes et seminaires'; Description = 'Reservations groupees avec prestations evenementielles' },
    @{ Code = 'SEG-INS'; Label = 'Institutionnels'; Description = 'Administrations et entites publiques conventionnees' },
    @{ Code = 'SEG-AGE'; Label = 'Agences de voyages'; Description = 'Intermediaires avec allotements negocies' }
)

foreach ($segment in $segments) {
    $null = Invoke-Api -Method POST -Path '/crm/segments' -Token $tokenAlger -Body @{
        code        = $segment.Code
        label       = $segment.Label
        description = $segment.Description
    } -Quiet
}

$tiers = @(
    @{ Code = 'FID-BRZ'; Label = 'Bronze'; Threshold = 0; Benefits = 'Cumul des points, offre de bienvenue' },
    @{ Code = 'FID-ARG'; Label = 'Argent'; Threshold = 2000; Benefits = 'Depart tardif, surclassement selon disponibilite' },
    @{ Code = 'FID-OR'; Label = 'Or'; Threshold = 6000; Benefits = 'Surclassement garanti, petit dejeuner offert' },
    @{ Code = 'FID-PLA'; Label = 'Platine'; Threshold = 15000; Benefits = 'Suite selon disponibilite, transfert aeroport inclus' }
)

foreach ($tier in $tiers) {
    $null = Invoke-Api -Method POST -Path '/crm/loyalty/tiers' -Token $tokenAlger -Body @{
        code            = $tier.Code
        label           = $tier.Label
        pointsThreshold = $tier.Threshold
        benefits        = $tier.Benefits
    } -Quiet
}

Write-Detail ($segments.Count.ToString() + ' segments, ' + $tiers.Count + ' niveaux de fidelite')

# ------------------------------------------------------------------------ Fiches client

$languages = @('Francais', 'Arabe', 'Anglais')
$profileCount = 0
$customerIndex = 0

foreach ($customer in $script:Customers) {
    $customerIndex++

    $segmentCode = 'SEG-LOI'
    if ($customer.Type -eq 'PublicEntity') { $segmentCode = 'SEG-INS' }
    elseif ($customer.Name -like '*Agence*' -or $customer.Name -like '*Tours*' -or $customer.Name -like '*Voyages*') { $segmentCode = 'SEG-AGE' }
    elseif ($customer.Type -eq 'Company') { $segmentCode = 'SEG-AFF' }

    $profile = Invoke-Api -Method PUT -Path ('/crm/guests/' + $customer.Code) -Token $tokenAlger -Body @{
        segmentCode       = $segmentCode
        preferredLanguage = (Get-RandomPick $languages)
        preferences       = (Get-RandomPick @('Etage eleve, chambre calme', 'Non-fumeur, lit king size', 'Proche ascenseur', 'Vue mer si disponible', 'Regime sans gluten'))
        notes             = 'Fiche renseignee a la reception'
        isVip             = ($customerIndex % 6 -eq 0)
    } -Quiet

    if ($null -eq $profile) { continue }

    $profileCount++

    $null = Invoke-Api -Method POST -Path ('/crm/guests/' + $customer.Code + '/marketing-consent') -Token $tokenAlger -Body @{
        consent = ($customerIndex % 3 -ne 0)
    } -Quiet

    # Points de fidelite : un cumul, puis une conversion pour une partie des clients. Un
    # compte qui n'a que des gains ne montre pas la moitie du mecanisme.
    $earned = Invoke-Api -Method POST -Path ('/crm/loyalty/accounts/' + $customer.Code + '/earn') -Token $tokenAlger -Body @{
        points     = (Get-RandomInt 400 9000)
        occurredOn = (Get-Date-Text $script:Today.AddDays(-(Get-RandomInt 5 120)))
        reason     = 'Points acquis sur sejours de l''annee'
    } -Quiet

    if ($null -ne $earned -and $customerIndex % 4 -eq 0) {
        $null = Invoke-Api -Method POST -Path ('/crm/loyalty/accounts/' + $customer.Code + '/redeem') -Token $tokenAlger -Body @{
            points     = (Get-RandomInt 200 1500)
            occurredOn = (Get-Date-Text $script:Today.AddDays(-(Get-RandomInt 1 30)))
            reason     = 'Conversion en nuitee offerte'
        } -Quiet
    }
}

Write-Detail ($profileCount.ToString() + ' fiches client 360 avec segment, consentement et points')

# ----------------------------------------------------------------------- Interactions

$interactionSubjects = @(
    'Demande de devis pour un seminaire',
    'Reclamation sur la climatisation de la chambre',
    'Confirmation de reservation groupe',
    'Demande de facture acquittee',
    'Suivi de satisfaction apres sejour',
    'Negociation de tarif entreprise',
    'Demande de transfert aeroport',
    'Signalement objet oublie en chambre',
    'Relance amiable sur facture echue',
    'Invitation a la soiree de fin d''annee'
)

$handlers = @('Samir Merzouk', 'Rachida Belkacem', 'Yacine Ferhat', 'Karim Benali')
$interactionCount = 0

for ($index = 0; $index -lt 36; $index++) {
    $customer = Get-RandomPick $script:Customers
    $unit = Get-RandomPick $script:Units
    $occurredAt = $script:Today.AddDays(-(Get-RandomInt 0 45)).AddHours((Get-RandomInt 8 19))

    $result = Invoke-Api -Method POST -Path '/crm/interactions' -Token $tokenAlger -Body @{
        customerCode  = $customer.Code
        occurredAt    = $occurredAt.ToString('yyyy-MM-ddTHH:mm:sszzz')
        channel       = (Get-RandomPick @('Phone', 'Email', 'Sms', 'InPerson', 'Web'))
        direction     = (Get-RandomPick @('Inbound', 'Outbound'))
        subject       = (Get-RandomPick $interactionSubjects)
        handledBy     = (Get-RandomPick $handlers)
        hotelUnitCode = $unit.Code
        notes         = 'Echange trace au dossier client'
    } -Quiet

    if ($null -ne $result) { $interactionCount++ }
}

Write-Detail ($interactionCount.ToString() + ' interactions clients tracees sur 45 jours')

# -------------------------------------------------------------------------- Campagnes

$campaigns = @(
    @{ Code = 'CMP-2026-01'; Label = 'Offre week-end de printemps'; Channel = 'Email'; Segment = 'SEG-LOI'; Objective = 'Remplir les week-ends de basse saison'; Start = -120; End = -80; Status = 'Completed' },
    @{ Code = 'CMP-2026-02'; Label = 'Seminaires entreprises - rentree'; Channel = 'Email'; Segment = 'SEG-AFF'; Objective = 'Vendre 40 journees d''etude'; Start = -30; End = 25; Status = 'Running' },
    @{ Code = 'CMP-2026-03'; Label = 'Relance agences partenaires'; Channel = 'Phone'; Segment = 'SEG-AGE'; Objective = 'Renouveler les allotements de la saison'; Start = -12; End = 18; Status = 'Running' },
    @{ Code = 'CMP-2026-04'; Label = 'SMS clients fideles - haute saison'; Channel = 'Sms'; Segment = 'SEG-LOI'; Objective = 'Reactiver les porteurs de carte Or'; Start = 10; End = 40; Status = 'Scheduled' },
    @{ Code = 'CMP-2026-05'; Label = 'Marches publics - appel a conventions'; Channel = 'Email'; Segment = 'SEG-INS'; Objective = 'Conventionner trois administrations'; Start = 20; End = 60; Status = 'Draft' },
    @{ Code = 'CMP-2026-06'; Label = 'Accueil personnalise en reception'; Channel = 'OnSite'; Segment = 'SEG-GRP'; Objective = 'Augmenter le NPS des groupes'; Start = -60; End = -20; Status = 'Cancelled' }
)

foreach ($campaign in $campaigns) {
    $created = Invoke-Api -Method POST -Path '/crm/campaigns' -Token $tokenAlger -Body @{
        code              = $campaign.Code
        label             = $campaign.Label
        channel           = $campaign.Channel
        startDate         = (Get-Date-Text $script:Today.AddDays($campaign.Start))
        endDate           = (Get-Date-Text $script:Today.AddDays($campaign.End))
        targetSegmentCode = $campaign.Segment
        objective         = $campaign.Objective
        message           = 'Message commercial adresse au segment cible.'
    } -Quiet

    if ($null -eq $created) { continue }
    if ($campaign.Status -eq 'Draft') { continue }

    $null = Invoke-Api -Method POST -Path ('/crm/campaigns/' + $campaign.Code + '/schedule') -Token $tokenAlger -Quiet

    if ($campaign.Status -eq 'Scheduled') { continue }

    if ($campaign.Status -eq 'Cancelled') {
        $null = Invoke-Api -Method POST -Path ('/crm/campaigns/' + $campaign.Code + '/cancel') -Token $tokenAlger -Body @{
            reason = 'Operation reportee a la saison prochaine'
        } -Quiet
        continue
    }

    $null = Invoke-Api -Method POST -Path ('/crm/campaigns/' + $campaign.Code + '/launch') -Token $tokenAlger -Quiet

    if ($campaign.Status -eq 'Completed') {
        $null = Invoke-Api -Method POST -Path ('/crm/campaigns/' + $campaign.Code + '/complete') -Token $tokenAlger -Quiet
    }
}

Write-Detail ($campaigns.Count.ToString() + ' campagnes couvrant les cinq etats du cycle de vie')

# ----------------------------------------------------------------------- Satisfaction

$comments = @{
    Promoter  = @('Accueil impeccable et chambre tres propre.', 'Personnel disponible, petit dejeuner copieux.', 'Vue magnifique, je reviendrai.', 'Rapport qualite-prix excellent.')
    Passive   = @('Sejour correct, rien a signaler.', 'Bon emplacement mais chambre un peu bruyante.', 'Conforme a mes attentes.')
    Detractor = @('Climatisation en panne pendant deux nuits.', 'Attente trop longue a la reception.', 'Chambre non prete a l''heure annoncee.')
}

$satisfactionCount = 0

for ($index = 0; $index -lt 48; $index++) {
    $customer = Get-RandomPick $script:Customers
    $unit = Get-RandomPick $script:Units

    # Repartition NPS realiste pour un 4 etoiles bien tenu : environ 55 % de promoteurs,
    # 28 % de passifs, 17 % de detracteurs. Un NPS parfait ne serait pas credible.
    $draw = $script:Random.Next(0, 100)
    if ($draw -lt 55) { $score = Get-RandomInt 9 10; $bucket = 'Promoter' }
    elseif ($draw -lt 83) { $score = Get-RandomInt 7 8; $bucket = 'Passive' }
    else { $score = Get-RandomInt 2 6; $bucket = 'Detractor' }

    $result = Invoke-Api -Method POST -Path '/crm/satisfaction' -Token $tokenAlger -Body @{
        customerCode  = $customer.Code
        hotelUnitCode = $unit.Code
        surveyDate    = (Get-Date-Text $script:Today.AddDays(-(Get-RandomInt 0 60)))
        score         = $score
        source        = (Get-RandomPick @('InRoom', 'Email', 'FrontDesk', 'Online', 'Phone'))
        comment       = (Get-RandomPick $comments[$bucket])
    } -Quiet

    if ($null -ne $result) { $satisfactionCount++ }
}

Write-Detail ($satisfactionCount.ToString() + ' enquetes de satisfaction, NPS calcule sur 60 jours')

# ------------------------------------------------------------------ Groupes & MICE (10.6)

Write-Step 'Salles et evenements (module 10.6)'

# mice.write n'est porte que par le role systeme : c'est le compte administrateur qui agit
# ici, et le guide le dira plutot que de laisser croire a un droit qui n'existe pas encore.
$spaces = @(
    @{ Unit = 'ALG-CEN'; Code = 'SAL-ATLAS'; Label = 'Salle Atlas'; Max = 220; Area = 310 },
    @{ Unit = 'ALG-CEN'; Code = 'SAL-HOGGAR'; Label = 'Salle Hoggar'; Max = 90; Area = 140 },
    @{ Unit = 'ALG-CEN'; Code = 'SAL-CONSEIL'; Label = 'Salle de conseil'; Max = 24; Area = 48 },
    @{ Unit = 'ORN-COR'; Code = 'SAL-MURDJ'; Label = 'Salle Murdjadjo'; Max = 160; Area = 240 },
    @{ Unit = 'ORN-COR'; Code = 'SAL-CORNI'; Label = 'Terrasse Corniche'; Max = 120; Area = 200 },
    @{ Unit = 'TIP-AZU'; Code = 'SAL-AZUR'; Label = 'Patio Azur'; Max = 80; Area = 130 },
    @{ Unit = 'BEJ-CAP'; Code = 'SAL-PONTON'; Label = 'Espace Ponton'; Max = 60; Area = 95 }
)

foreach ($space in $spaces) {
    $null = Invoke-Api -Method POST -Path ('/mice/spaces/' + $space.Unit + '/' + $space.Code) -Body @{
        label             = $space.Label
        maxAttendance     = $space.Max
        areaSquareMeters  = $space.Area
        notes             = 'Sonorisation, videoprojection et climatisation'
    } -Quiet
}

Write-Detail ($spaces.Count.ToString() + ' salles et espaces evenementiels')

$events = @(
    @{ Ref = 'EVT-2026-001'; Unit = 'ALG-CEN'; Space = 'SAL-ATLAS'; Customer = 'CL-0001'; Title = 'Convention annuelle des cadres'; Day = -18; Start = '09:00'; Duration = 480; Attendance = 180; Style = 'Theatre'; Action = 'Invoiced' },
    @{ Ref = 'EVT-2026-002'; Unit = 'ALG-CEN'; Space = 'SAL-HOGGAR'; Customer = 'CL-0016'; Title = 'Seminaire de formation bancaire'; Day = -9; Start = '08:30'; Duration = 420; Attendance = 70; Style = 'Classe'; Action = 'Invoiced' },
    @{ Ref = 'EVT-2026-003'; Unit = 'ALG-CEN'; Space = 'SAL-CONSEIL'; Customer = 'CL-0007'; Title = 'Conseil d''administration'; Day = 3; Start = '10:00'; Duration = 240; Attendance = 18; Style = 'U'; Action = 'Confirmed' },
    @{ Ref = 'EVT-2026-004'; Unit = 'ORN-COR'; Space = 'SAL-MURDJ'; Customer = 'CL-0005'; Title = 'Journee d''etude wilaya'; Day = 7; Start = '09:00'; Duration = 360; Attendance = 140; Style = 'Theatre'; Action = 'Confirmed' },
    @{ Ref = 'EVT-2026-005'; Unit = 'ORN-COR'; Space = 'SAL-CORNI'; Customer = 'CL-0015'; Title = 'Diner de gala agences partenaires'; Day = 14; Start = '19:30'; Duration = 300; Attendance = 110; Style = 'Banquet'; Action = 'Confirmed' },
    @{ Ref = 'EVT-2026-006'; Unit = 'TIP-AZU'; Space = 'SAL-AZUR'; Customer = 'CL-0009'; Title = 'Atelier des architectes'; Day = 21; Start = '09:30'; Duration = 300; Attendance = 60; Style = 'Classe'; Action = 'Draft' },
    @{ Ref = 'EVT-2026-007'; Unit = 'BEJ-CAP'; Space = 'SAL-PONTON'; Customer = 'CL-0004'; Title = 'Reception de fin de saison'; Day = 28; Start = '18:00'; Duration = 240; Attendance = 55; Style = 'Cocktail'; Action = 'Draft' },
    @{ Ref = 'EVT-2026-008'; Unit = 'ALG-CEN'; Space = 'SAL-HOGGAR'; Customer = 'CL-0006'; Title = 'Colloque universitaire'; Day = 35; Start = '08:00'; Duration = 540; Attendance = 85; Style = 'Theatre'; Action = 'Confirmed' },
    @{ Ref = 'EVT-2026-009'; Unit = 'ALG-CEN'; Space = 'SAL-ATLAS'; Customer = 'CL-0014'; Title = 'Presentation reseau distributeurs'; Day = -35; Start = '09:00'; Duration = 420; Attendance = 160; Style = 'Theatre'; Action = 'Cancelled' },
    @{ Ref = 'EVT-2026-010'; Unit = 'ORN-COR'; Space = 'SAL-MURDJ'; Customer = 'CL-0008'; Title = 'Assemblee generale transporteurs'; Day = 42; Start = '10:00'; Duration = 300; Attendance = 130; Style = 'Theatre'; Action = 'Confirmed' }
)

$eventLineCatalog = @(
    @{ Designation = 'Location de salle - journee'; Unit = 65000; Vat = 19 },
    @{ Designation = 'Pause-cafe (par personne)'; Unit = 1200; Vat = 19 },
    @{ Designation = 'Dejeuner assis (par personne)'; Unit = 3800; Vat = 19 },
    @{ Designation = 'Cocktail dinatoire (par personne)'; Unit = 4600; Vat = 19 },
    @{ Designation = 'Sonorisation et regie'; Unit = 28000; Vat = 19 },
    @{ Designation = 'Videoprojection HD'; Unit = 18000; Vat = 19 },
    @{ Designation = 'Hotesse d''accueil - journee'; Unit = 12000; Vat = 19 }
)

$eventCount = 0
$invoicedEvents = 0

foreach ($event in $events) {
    $eventDate = $script:Today.AddDays($event.Day)

    $created = Invoke-Api -Method POST -Path '/mice/events' -Body @{
        hotelUnitCode      = $event.Unit
        reference          = $event.Ref
        functionSpaceCode  = $event.Space
        customerCode       = $event.Customer
        title              = $event.Title
        eventDate          = (Get-Date-Text $eventDate)
        startTime          = ($event.Start + ':00')
        durationMinutes    = $event.Duration
        setupMinutes       = 60
        teardownMinutes    = 45
        setupStyle         = $event.Style
        expectedAttendance = $event.Attendance
        notes              = 'Devis etabli par le service commercial'
    } -Quiet

    if ($null -eq $created) { continue }

    $eventCount++

    $lines = @(@{
        designation = 'Location de salle - journee'
        quantity    = 1
        unitPrice   = 65000
        vatRate     = 19
    })

    foreach ($template in ($eventLineCatalog | Select-Object -Skip 1 | Select-Object -First (Get-RandomInt 2 4))) {
        $quantity = 1
        if ($template.Designation -like '*par personne*') { $quantity = $event.Attendance }

        $lines += @{
            designation = $template.Designation
            quantity    = $quantity
            unitPrice   = $template.Unit
            vatRate     = $template.Vat
        }
    }

    $null = Invoke-Api -Method PUT -Path ('/mice/events/' + $created.id + '/lines') -Body $lines -Quiet

    # Le deroule (BEO) est ce que la salle et la cuisine lisent le jour J : un evenement sans
    # deroule laisserait l'onglet le plus utile du module completement vide.
    $schedule = @(
        @{ startTime = ($event.Start + ':00'); description = 'Accueil des participants et emargement'; department = 'Reception' },
        @{ startTime = '10:30:00'; description = 'Pause-cafe en foyer'; department = 'Restauration' },
        @{ startTime = '12:30:00'; description = 'Service du dejeuner'; department = 'Restauration' },
        @{ startTime = '15:30:00'; description = 'Seconde pause et remise en salle'; department = 'Etages' },
        @{ startTime = '17:30:00'; description = 'Demontage et remise en configuration'; department = 'Technique' }
    )

    $null = Invoke-Api -Method PUT -Path ('/mice/events/' + $created.id + '/schedule') -Body $schedule -Quiet

    if ($event.Action -eq 'Draft') { continue }

    if ($event.Action -eq 'Cancelled') {
        $null = Invoke-Api -Method POST -Path ('/mice/events/' + $created.id + '/cancel') -Body @{
            reason = 'Report demande par le client'
        } -Quiet
        continue
    }

    $confirmed = Invoke-Api -Method POST -Path ('/mice/events/' + $created.id + '/confirm') -Quiet
    if ($null -eq $confirmed) { continue }

    if ($event.Action -eq 'Invoiced') {
        $invoice = Invoke-Api -Method POST -Path ('/mice/events/' + $created.id + '/invoice') -Quiet
        if ($null -ne $invoice) { $invoicedEvents++ }
    }
}

Write-Detail ($eventCount.ToString() + ' evenements avec devis et BEO, ' + $invoicedEvents + ' factures')
