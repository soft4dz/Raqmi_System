param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    # Fichier de preuves par ecran (lot 1.3). Surchargeable pour tester une variante.
    [string]$ScreensPath = "",
    # Date de reference pour la periode de grace documentation (AAAA-MM-JJ). Par defaut : aujourd'hui.
    [string]$AsOf = "",
    # Chemin d'un fichier Markdown ou ecrire le tableau (GITHUB_STEP_SUMMARY est utilise s'il existe).
    [string]$MarkdownSummaryPath = ""
)

$ErrorActionPreference = "Stop"

# Les libelles du catalogue sont accentues : sans UTF-8 la console CI les rend illisibles.
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$catalogPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Desktop/ModuleCatalog.cs"
$xamlPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Desktop/MainWindow.xaml"
$mainWindowPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Desktop/MainWindow.xaml.cs"
$permissionPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Domain/Identity/PermissionCatalog.cs"
$functionalCatalogPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Application/Navigation/FunctionalArchitectureCatalog.cs"
if (-not $ScreensPath) { $ScreensPath = Join-Path $RepositoryRoot "tools/readiness/screens.json" }

foreach ($path in @($catalogPath, $xamlPath, $mainWindowPath, $permissionPath, $functionalCatalogPath, $ScreensPath)) {
    if (-not (Test-Path $path)) {
        throw "Module readiness: fichier requis introuvable: $path"
    }
}

$catalog = Get-Content $catalogPath -Raw -Encoding UTF8
$xaml = Get-Content $xamlPath -Raw -Encoding UTF8
# Tous les partiels MainWindow*.cs sont lus : le cablage RBAC peut vivre dans n'importe lequel
# (MainWindow.ModuleAccessFixes.cs aujourd'hui, MainWindow.Navigation.cs demain).
$mainWindow = (Get-ChildItem (Join-Path $RepositoryRoot "src/RaqmiSystem.Desktop") -Filter "MainWindow*.cs" -File |
    ForEach-Object { Get-Content $_.FullName -Raw -Encoding UTF8 }) -join "`n"
$permissions = Get-Content $permissionPath -Raw -Encoding UTF8
$functionalCatalog = Get-Content $functionalCatalogPath -Raw -Encoding UTF8

function Get-ConstInt([string]$name) {
    $match = [regex]::Match($catalog, "public\s+const\s+int\s+$name\s*=\s*(\d+)\s*;")
    if (-not $match.Success) { throw "Module readiness: constante $name introuvable dans ModuleCatalog." }
    return [int]$match.Groups[1].Value
}

$expectedTotal = Get-ConstInt "ExpectedTotal"
$expectedAvailable = Get-ConstInt "ExpectedAvailable"

# =====================================================================================
# Controles 1 a 8 (historiques) : chaque module Disponible est cable Navigation + RBAC +
# Desktop. Ils echouent immediatement (throw) : un module Disponible mal cable est une
# regression, pas une preuve manquante.
# =====================================================================================

# La grammaire est volontairement stricte : une seule entree du catalogue, avec
# ses six champs obligatoires, sa permission et son onglet. Le motif ne peut pas
# traverser une entree Planifiee pour aller chercher le prochain Disponible.
$availablePattern = @'
new\s+ModuleCatalogEntry\(\s*
"(?<order>[^"]+)"\s*,\s*
Groups\.[A-Za-z0-9_]+\s*,\s*
"(?<name>[^"]+)"\s*,\s*
"[^"]*"\s*,\s*
"[^"]*"\s*,\s*
ModuleStatus\.Disponible\s*,\s*
PermissionCatalog\.(?<permission>[A-Za-z0-9_]+)\s*,\s*
(?<tab>\d+)\s*
(?:,\s*"[^"]*")?\s*
\)
'@
$availableMatches = [regex]::Matches(
    $catalog,
    $availablePattern,
    [Text.RegularExpressions.RegexOptions]::Singleline -bor [Text.RegularExpressions.RegexOptions]::IgnorePatternWhitespace)

if ($availableMatches.Count -ne $expectedAvailable) {
    throw "Module readiness: $($availableMatches.Count) modules Disponibles correctement cables trouves, $expectedAvailable attendus. Un module Disponible a probablement perdu PermissionKey ou TabIndex."
}

$allEntries = [regex]::Matches($catalog, 'new\s+ModuleCatalogEntry\(').Count
if ($allEntries -ne $expectedTotal) {
    throw "Module readiness: catalogue contient $allEntries entrees, $expectedTotal attendues."
}

# Les TabItem sont lus dans leur ordre reel. L'index du catalogue est donc verifie
# contre l'ecran effectivement affiche, et pas seulement contre une valeur numerique.
$tabTags = [regex]::Matches($xaml, '<TabItem\b[^>]*>', [Text.RegularExpressions.RegexOptions]::Singleline)
$seenTabs = @{}
$rows = @()

foreach ($module in $availableMatches) {
    $order = $module.Groups['order'].Value
    $name = $module.Groups['name'].Value
    $permissionConst = $module.Groups['permission'].Value
    $tabIndex = [int]$module.Groups['tab'].Value

    # Plusieurs lignes fonctionnelles peuvent volontairement partager le meme ecran
    # (ex. Audit & controle interne + Journalisation & tracabilite). Ce partage est
    # acceptable UNIQUEMENT si la permission de lecture est identique. Une collision
    # avec deux permissions differentes serait un vrai melange de perimetres RBAC.
    if ($seenTabs.ContainsKey($tabIndex)) {
        $previous = $seenTabs[$tabIndex]
        if ($previous.Permission -ne $permissionConst) {
            throw "Module readiness: onglet $tabIndex partage par '$($previous.Name)' ($($previous.Permission)) et '$name' ($permissionConst). Un ecran partage doit avoir le meme perimetre RBAC."
        }
    }
    else {
        $seenTabs[$tabIndex] = [pscustomobject]@{ Name = $name; Permission = $permissionConst }
    }

    if ($tabIndex -ge $tabTags.Count) {
        throw "Module readiness: '$name' pointe vers l'onglet $tabIndex mais MainWindow n'en contient que $($tabTags.Count)."
    }

    $permissionConstPattern = "public\s+const\s+string\s+$([regex]::Escape($permissionConst))\s*="
    if (-not [regex]::IsMatch($permissions, $permissionConstPattern)) {
        throw "Module readiness: '$name' reference PermissionCatalog.$permissionConst, constante absente."
    }

    $tabTag = $tabTags[$tabIndex].Value
    $tabNameMatch = [regex]::Match($tabTag, 'x:Name\s*=\s*"(?<name>[^"]+)"')
    if (-not $tabNameMatch.Success) {
        throw "Module readiness: l'onglet $tabIndex de '$name' n'a pas de x:Name; le RBAC WPF ne peut pas etre verifie."
    }
    $tabName = $tabNameMatch.Groups['name'].Value

    $accessPattern = "ApplyModuleAccess\(\s*PermissionCatalog\.$([regex]::Escape($permissionConst))\s*,\s*$([regex]::Escape($tabName))\s*\)"
    if (-not [regex]::IsMatch($mainWindow, $accessPattern)) {
        throw "Module readiness: '$name' ($tabName) n'est pas cable a PermissionCatalog.$permissionConst via ApplyModuleAccess."
    }

    $shared = ($availableMatches | Where-Object {
        [int]$_.Groups['tab'].Value -eq $tabIndex
    }).Count -gt 1

    $rows += [pscustomobject]@{
        Order = $order
        Module = $name
        Permission = "PermissionCatalog.$permissionConst"
        PermissionConst = $permissionConst
        TabIndex = $tabIndex
        TabName = $tabName
        Navigation = if ($shared) { "OK (ecran partage)" } else { "OK" }
        RBAC = "OK"
        Desktop = "OK"
    }
}

Write-Host "Module readiness gate: $($rows.Count)/$expectedAvailable modules Disponibles cables Navigation + RBAC + Desktop."

# =====================================================================================
# A partir d'ici, les controles ACCUMULENT leurs echecs : on veut la liste complete
# des preuves manquantes en une seule execution, puis un code de sortie non nul.
# =====================================================================================
$failures = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
function Add-Failure([string]$message) { $script:failures.Add($message) | Out-Null }
function Add-Warning([string]$message) { $script:warnings.Add($message) | Out-Null }

# ---------------------------------------------------------------------------------
# Controle 9 : FunctionalArchitectureCatalog - 22 domaines, ids 01..22 uniques, chaque
# ordre Disponible rattache a exactement un domaine.
# Le parse s'appuie uniquement sur le motif Domain("NN", "Nom", "IconKey",
# FunctionalMaturity.X, "ordres"...). Tout autre noeud (Module, Submodule, Screen...)
# est ignore : le catalogue peut grandir sans casser ce garde.
# ---------------------------------------------------------------------------------
$domainPattern = @'
\bDomain\(\s*
"(?<id>[^"]+)"\s*,\s*
"(?<name>[^"]+)"\s*,\s*
"(?<icon>[^"]*)"\s*,\s*
FunctionalMaturity\.(?<maturity>[A-Za-z]+)
(?<orders>(?:\s*,\s*"[^"]+")*)
\s*[,)]
'@
$domainMatches = [regex]::Matches(
    $functionalCatalog,
    $domainPattern,
    [Text.RegularExpressions.RegexOptions]::Singleline -bor [Text.RegularExpressions.RegexOptions]::IgnorePatternWhitespace)

$expectedDomainCount = 22
$expectedDomainMatch = [regex]::Match($functionalCatalog, 'public\s+const\s+int\s+ExpectedDomainCount\s*=\s*(\d+)\s*;')
if ($expectedDomainMatch.Success -and [int]$expectedDomainMatch.Groups[1].Value -ne $expectedDomainCount) {
    Add-Failure "Catalogue fonctionnel: ExpectedDomainCount vaut $($expectedDomainMatch.Groups[1].Value), la cible validee est $expectedDomainCount domaines."
}

$domains = @()
$domainByOrder = @{}
$orderDomainCount = @{}
foreach ($match in $domainMatches) {
    $id = $match.Groups['id'].Value
    $orders = [regex]::Matches($match.Groups['orders'].Value, '"(?<order>[^"]+)"') | ForEach-Object { $_.Groups['order'].Value }
    $domain = [pscustomobject]@{
        Id = $id
        Name = $match.Groups['name'].Value
        Maturity = $match.Groups['maturity'].Value
        Orders = @($orders)
    }
    $domains += $domain
    foreach ($order in $domain.Orders) {
        if (-not $orderDomainCount.ContainsKey($order)) { $orderDomainCount[$order] = 0 }
        $orderDomainCount[$order]++
        if (-not $domainByOrder.ContainsKey($order)) { $domainByOrder[$order] = $domain }
    }
}

if ($domains.Count -ne $expectedDomainCount) {
    Add-Failure "Catalogue fonctionnel: $($domains.Count) domaines Domain(...) trouves dans FunctionalArchitectureCatalog.cs, $expectedDomainCount attendus."
}

$domainIds = @($domains | ForEach-Object { $_.Id })
$duplicateIds = $domainIds | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name }
if ($duplicateIds) {
    Add-Failure "Catalogue fonctionnel: identifiants de domaine dupliques: $($duplicateIds -join ', ')."
}
$expectedIds = 1..$expectedDomainCount | ForEach-Object { $_.ToString("00") }
$missingIds = $expectedIds | Where-Object { $domainIds -notcontains $_ }
$unknownIds = $domainIds | Where-Object { $expectedIds -notcontains $_ } | Sort-Object -Unique
if ($missingIds) { Add-Failure "Catalogue fonctionnel: identifiants de domaine absents: $($missingIds -join ', ') (attendus 01..$expectedDomainCount)." }
if ($unknownIds) { Add-Failure "Catalogue fonctionnel: identifiants de domaine hors plage 01..$expectedDomainCount : $($unknownIds -join ', ')." }

foreach ($row in $rows) {
    $count = if ($orderDomainCount.ContainsKey($row.Order)) { $orderDomainCount[$row.Order] } else { 0 }
    if ($count -eq 0) {
        Add-Failure "Catalogue fonctionnel: l'ordre Disponible '$($row.Order)' ($($row.Module)) n'est rattache a aucun domaine."
    }
    elseif ($count -gt 1) {
        Add-Failure "Catalogue fonctionnel: l'ordre Disponible '$($row.Order)' ($($row.Module)) est rattache a $count domaines ; un seul domaine primaire est admis."
    }
}

# Un ordre inconnu du catalogue WPF n'est pas une regression de readiness (le test xUnit du
# catalogue le couvre) : signale en avertissement pour rester tolerant aux evolutions du NAV.
$allOrders = [regex]::Matches($catalog, 'new\s+ModuleCatalogEntry\(\s*"(?<order>[^"]+)"') | ForEach-Object { $_.Groups['order'].Value }
foreach ($order in $orderDomainCount.Keys) {
    if ($allOrders -notcontains $order) {
        Add-Warning "Catalogue fonctionnel: l'ordre '$order' rattache a un domaine n'existe pas dans ModuleCatalog."
    }
}

Write-Host "Catalogue fonctionnel: $($domains.Count)/$expectedDomainCount domaines, $($orderDomainCount.Keys.Count) ordres rattaches."

# ---------------------------------------------------------------------------------
# Controle 10 : tools/readiness/screens.json - preuves par ecran et niveau calcule.
# ---------------------------------------------------------------------------------
$levelRank = @{ Planned = 0; TechnicalPreview = 1; Functional = 2; ProductionReady = 3 }
$levelLabel = @{ Planned = "Planned"; TechnicalPreview = "Technical Preview"; Functional = "Functional"; ProductionReady = "Production Ready" }

try {
    $screensDocument = Get-Content $ScreensPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    throw "Module readiness: $ScreensPath n'est pas un JSON valide: $($_.Exception.Message)"
}

$asOfDate = if ($AsOf) {
    [datetime]::ParseExact($AsOf, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
} else {
    (Get-Date).Date
}

$grace = $screensDocument.documentationGrace
$graceUntil = $null
$graceScreens = @()
$graceActive = $false
if ($null -ne $grace) {
    if (-not $grace.until) {
        Add-Failure "screens.json: documentationGrace.until est obligatoire (AAAA-MM-JJ)."
    }
    else {
        try {
            $graceUntil = [datetime]::ParseExact([string]$grace.until, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
            $graceActive = $asOfDate -le $graceUntil
        }
        catch {
            Add-Failure "screens.json: documentationGrace.until '$($grace.until)' n'est pas une date AAAA-MM-JJ."
        }
    }
    if (-not $grace.reason) { Add-Failure "screens.json: documentationGrace.reason est obligatoire." }
    if ($null -ne $grace.screens) { $graceScreens = @($grace.screens) }
}

# Couverture des cles historiques par le registre domaine.ressource.action (lot 2.1).
# Depuis ce lot, une route peut exiger la cle CIBLE (PermissionCatalog.PurchasingOrderRead) a la
# place de la cle historique de l'ecran (PermissionCatalog.PurchasingRead) : la politique de la
# cle cible accepte la cle historique qui la couvre, l'acces de l'ecran est donc intact. La
# preuve RBAC accepte les deux, en lisant la couverture dans PermissionRegistry.cs - chaque
# entree a la forme Target(PermissionCatalog.<cible>, ..., PermissionCatalog.<historique>...) -
# jamais depuis une liste saisie a la main, qui finirait par mentir.
$registryPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Domain/Identity/PermissionRegistry.cs"
$targetsByLegacyConst = @{}
if (Test-Path $registryPath) {
    $registrySource = Get-Content $registryPath -Raw -Encoding UTF8
    # Les commentaires ne portent aucune couverture : retires avant le decoupage par entree.
    $registrySource = [regex]::Replace($registrySource, '(?m)^\s*//.*$', '')
    foreach ($chunk in @($registrySource -split 'Target\(') | Select-Object -Skip 1) {
        # Une entree s'arrete a la fin du tableau : les membres qui suivent ne sont pas des cles.
        $chunkBody = @($chunk -split '\};', 2)[0]
        $refs = @([regex]::Matches($chunkBody, 'PermissionCatalog\.(?<const>[A-Za-z0-9_]+)') |
            ForEach-Object { $_.Groups['const'].Value })
        if ($refs.Count -lt 2) { continue }
        $targetConst = $refs[0]
        foreach ($legacyConst in @($refs | Select-Object -Skip 1 | Select-Object -Unique)) {
            if (-not $targetsByLegacyConst.ContainsKey($legacyConst)) {
                $targetsByLegacyConst[$legacyConst] = New-Object System.Collections.Generic.List[string]
            }
            if (-not $targetsByLegacyConst[$legacyConst].Contains($targetConst)) {
                $targetsByLegacyConst[$legacyConst].Add($targetConst)
            }
        }
    }
}

function Resolve-PathList($value) {
    if ($value -is [string]) { return @($value) }
    if ($value -is [array]) { return @($value | ForEach-Object { [string]$_ }) }
    return $null
}

# Une preuve est : un ou des chemins existants (ok), { status: n/a, reason } (na), null (missing).
# Un chemin declare mais absent est une ERREUR de saisie (echec immediat du garde), pas une
# preuve manquante : le fichier doit dire la verite.
function Resolve-Evidence($value, [string]$screen, [string]$criterion) {
    if ($null -eq $value) {
        return [pscustomobject]@{ Status = "missing"; Detail = "aucune preuve" }
    }
    $paths = Resolve-PathList $value
    if ($null -ne $paths) {
        if ($paths.Count -eq 0) {
            return [pscustomobject]@{ Status = "missing"; Detail = "liste vide" }
        }
        foreach ($relative in $paths) {
            if (-not (Test-Path (Join-Path $RepositoryRoot $relative))) {
                Add-Failure "screens.json: $screen / $criterion : preuve introuvable '$relative'."
                return [pscustomobject]@{ Status = "missing"; Detail = "chemin introuvable" }
            }
        }
        return [pscustomobject]@{ Status = "ok"; Detail = ($paths -join "; "); Paths = $paths }
    }
    if ($value.PSObject.Properties['status']) {
        if ([string]$value.status -eq "n/a") {
            if (-not $value.reason) {
                Add-Failure "screens.json: $screen / $criterion : 'n/a' sans justification (reason)."
                return [pscustomobject]@{ Status = "missing"; Detail = "n/a non justifie" }
            }
            return [pscustomobject]@{ Status = "na"; Detail = [string]$value.reason }
        }
        Add-Failure "screens.json: $screen / $criterion : statut '$($value.status)' inconnu (seul 'n/a' est admis)."
        return [pscustomobject]@{ Status = "missing"; Detail = "statut inconnu" }
    }
    Add-Failure "screens.json: $screen / $criterion : forme de preuve non reconnue."
    return [pscustomobject]@{ Status = "missing"; Detail = "forme inconnue" }
}

# Le smoke test n'est pas un fichier : c'est un compte rendu date, sur une build nommee,
# joue avec au moins deux profils (administrateur + restreint).
function Resolve-Smoke($value, [string]$screen) {
    if ($null -eq $value) {
        return [pscustomobject]@{ Status = "missing"; Detail = "non joue" }
    }
    $profiles = @()
    if ($null -ne $value.PSObject.Properties['profiles'] -and $null -ne $value.profiles) { $profiles = @($value.profiles) }
    if (-not $value.validatedOn -or -not $value.build -or $profiles.Count -lt 2) {
        Add-Failure "screens.json: $screen / smoke : un smoke valide exige validatedOn, build et au moins deux profils."
        return [pscustomobject]@{ Status = "missing"; Detail = "compte rendu incomplet" }
    }
    try {
        [void][datetime]::ParseExact([string]$value.validatedOn, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        Add-Failure "screens.json: $screen / smoke : validatedOn '$($value.validatedOn)' n'est pas une date AAAA-MM-JJ."
        return [pscustomobject]@{ Status = "missing"; Detail = "date invalide" }
    }
    return [pscustomobject]@{ Status = "ok"; Detail = "$($value.validatedOn) / $($value.build) / $($profiles -join ', ')" }
}

function Test-Satisfied($evidence) { return $evidence.Status -eq "ok" -or $evidence.Status -eq "na" }

# Lecture tolerante d'une propriete JSON : absente et null se lisent pareil.
function Get-Field($object, [string]$name) {
    if ($null -ne $object -and $null -ne $object.PSObject.Properties[$name]) { return $object.$name }
    return $null
}

$screensNode = $screensDocument.screens
if ($null -eq $screensNode) {
    throw "Module readiness: screens.json ne contient pas d'objet 'screens'."
}
$declaredScreens = @{}
foreach ($property in $screensNode.PSObject.Properties) { $declaredScreens[$property.Name] = $property.Value }

# Regroupement des lignes du catalogue par onglet : un ecran = un x:Name.
$screensByTab = @{}
foreach ($row in $rows) {
    if (-not $screensByTab.ContainsKey($row.TabName)) { $screensByTab[$row.TabName] = @() }
    $screensByTab[$row.TabName] += $row
}

foreach ($tabName in $declaredScreens.Keys) {
    if (-not $screensByTab.ContainsKey($tabName)) {
        Add-Failure "screens.json: l'ecran '$tabName' n'est servi par aucun module Disponible du catalogue (onglet inconnu ou module retrograde)."
    }
}

$screenRows = @()
$apiContentCache = @{}

foreach ($tabName in ($screensByTab.Keys | Sort-Object { $screensByTab[$_][0].TabIndex })) {
    $modules = $screensByTab[$tabName]
    $catalogOrders = @($modules | ForEach-Object { $_.Order })
    $permissionConst = $modules[0].PermissionConst
    $tabIndex = $modules[0].TabIndex
    $domainIdsForScreen = @($catalogOrders | ForEach-Object {
        if ($domainByOrder.ContainsKey($_)) { $domainByOrder[$_].Id } else { "?" }
    } | Sort-Object -Unique)

    if (-not $declaredScreens.ContainsKey($tabName)) {
        Add-Failure "screens.json: l'onglet Disponible '$tabName' (ordres $($catalogOrders -join ', ')) n'a pas de fiche de preuves."
        $screenRows += [pscustomobject]@{
            Onglet = $tabIndex; Ecran = $tabName; Ordres = ($catalogOrders -join ", "); Domaine = ($domainIdsForScreen -join ", ")
            Permission = $permissionConst; Declare = "-"; Prouve = "-"; Manquant = "fiche absente de screens.json"
        }
        continue
    }

    $screen = $declaredScreens[$tabName]
    $screenOrders = @(Resolve-PathList $screen.orders)
    if ($null -eq $screen.orders -or (Compare-Object ($screenOrders | Sort-Object) ($catalogOrders | Sort-Object))) {
        Add-Failure "screens.json: $tabName : ordres declares [$($screenOrders -join ', ')] differents du catalogue [$($catalogOrders -join ', ')]."
    }
    if ([string]$screen.permission -ne $permissionConst) {
        Add-Failure "screens.json: $tabName : permission declaree '$($screen.permission)' differente du catalogue '$permissionConst'."
    }
    $declared = [string]$screen.declared
    if (-not $levelRank.ContainsKey($declared)) {
        Add-Failure "screens.json: $tabName : niveau declare '$declared' inconnu (Planned, TechnicalPreview, Functional, ProductionReady)."
        $declared = "Planned"
    }

    $evidence = $screen.evidence
    if ($null -eq $evidence) {
        Add-Failure "screens.json: $tabName : objet 'evidence' absent."
        $evidence = [pscustomobject]@{}
    }

    $proof = @{}
    $proof.Domain = Resolve-Evidence (Get-Field $evidence 'domain') $tabName 'domain'
    $proof.Application = Resolve-Evidence (Get-Field $evidence 'application') $tabName 'application'
    $proof.Api = Resolve-Evidence (Get-Field $evidence 'api') $tabName 'api'
    $proof.PostgreSql = Resolve-Evidence (Get-Field $evidence 'postgresql') $tabName 'postgresql'
    $proof.Desktop = Resolve-Evidence (Get-Field $evidence 'desktop') $tabName 'desktop'
    $proof.Tests = Resolve-Evidence (Get-Field $evidence 'tests') $tabName 'tests'
    $proof.Documentation = Resolve-Evidence (Get-Field $evidence 'documentation') $tabName 'documentation'
    $proof.Smoke = Resolve-Smoke (Get-Field $evidence 'smoke') $tabName

    # API : chaque endpoint declare doit etre protege (RequireAuthorization). L'API n'est
    # pas 'n/a' : un ecran sans API ne peut pas etre Functional dans un client leger.
    if ($proof.Api.Status -eq "na") {
        Add-Failure "screens.json: $tabName / api : 'n/a' n'est pas admis, l'API est obligatoire."
        $proof.Api = [pscustomobject]@{ Status = "missing"; Detail = "n/a refuse" }
    }
    # La permission de lecture de l'ecran, ou n'importe quelle cle cible que le registre lui
    # fait couvrir : une route retaguee vers la cle cible reste ouverte au porteur de la cle
    # historique, la preuve ne doit donc pas retomber quand le retag avance.
    $acceptedConsts = @($permissionConst)
    if ($targetsByLegacyConst.ContainsKey($permissionConst)) {
        $acceptedConsts += @($targetsByLegacyConst[$permissionConst])
    }
    $acceptedPattern = ($acceptedConsts | ForEach-Object { [regex]::Escape($_) }) -join '|'
    $apiReferencesPermission = $false
    if ($proof.Api.Status -eq "ok") {
        foreach ($relative in $proof.Api.Paths) {
            $full = Join-Path $RepositoryRoot $relative
            if (-not $apiContentCache.ContainsKey($full)) { $apiContentCache[$full] = Get-Content $full -Raw -Encoding UTF8 }
            $content = $apiContentCache[$full]
            if ($content -notmatch 'RequireAuthorization\(') {
                Add-Failure "screens.json: $tabName / api : '$relative' ne contient aucun RequireAuthorization."
            }
            if ($content -match "PermissionCatalog\.($acceptedPattern)\b") { $apiReferencesPermission = $true }
        }
    }

    # RBAC : derive, jamais saisi. Constante presente (controle 6), cablage WPF (controle 7)
    # et politique API referencant la permission de lecture de l'ecran ou une cle cible couverte.
    $proof.Rbac = if ($apiReferencesPermission) {
        [pscustomobject]@{ Status = "ok"; Detail = "PermissionCatalog.$permissionConst (ou cle cible couverte : $($acceptedConsts -join ', ')) : constante + ApplyModuleAccess + politique API" }
    } else {
        [pscustomobject]@{ Status = "missing"; Detail = "aucun endpoint declare ne reference PermissionCatalog.$permissionConst ni une cle cible qu'elle couvre ($($acceptedConsts -join ', '))" }
    }

    # Desktop : onglet reel avec x:Name (controles 3 a 5) + fichier(s) de vue declare(s).
    if ($proof.Desktop.Status -eq "na") {
        Add-Failure "screens.json: $tabName / desktop : 'n/a' n'est pas admis pour un ecran."
        $proof.Desktop = [pscustomobject]@{ Status = "missing"; Detail = "n/a refuse" }
    }
    if ($proof.Tests.Status -eq "na") {
        Add-Failure "screens.json: $tabName / tests : 'n/a' n'est pas admis."
        $proof.Tests = [pscustomobject]@{ Status = "missing"; Detail = "n/a refuse" }
    }
    if ($proof.Documentation.Status -eq "na") {
        Add-Failure "screens.json: $tabName / documentation : 'n/a' n'est pas admis ; une fiche est due pour tout ecran."
        $proof.Documentation = [pscustomobject]@{ Status = "missing"; Detail = "n/a refuse" }
    }

    $productionReady = Get-Field $screen 'productionReady'
    $postgresqlCi = [pscustomobject]@{ Status = "missing"; Detail = "aucune preuve" }
    $e2e = [pscustomobject]@{ Status = "missing"; Detail = "aucune preuve" }
    if ($null -ne $productionReady) {
        $postgresqlCi = Resolve-Evidence (Get-Field $productionReady 'postgresqlCi') $tabName 'productionReady.postgresqlCi'
        $e2e = Resolve-Evidence (Get-Field $productionReady 'e2e') $tabName 'productionReady.e2e'
        if ($postgresqlCi.Status -eq "na") { Add-Failure "screens.json: $tabName / productionReady.postgresqlCi : 'n/a' n'est pas admis (PostgreSQL reel exige)."; $postgresqlCi.Status = "missing" }
        if ($e2e.Status -eq "na") { Add-Failure "screens.json: $tabName / productionReady.e2e : 'n/a' n'est pas admis."; $e2e.Status = "missing" }
    }

    # Grace documentation : uniquement pour les ecrans listes, uniquement jusqu'a la date.
    $inGraceList = $graceScreens -contains $tabName
    $documentationSatisfied = $proof.Documentation.Status -eq "ok"
    $documentationByGrace = (-not $documentationSatisfied) -and $inGraceList -and $graceActive
    if ($documentationSatisfied -and $inGraceList) {
        Add-Warning "screens.json: $tabName a une fiche de documentation ; le retirer de documentationGrace.screens."
    }
    if ((-not $documentationSatisfied) -and $inGraceList -and (-not $graceActive)) {
        Add-Warning "screens.json: $tabName : periode de grace documentation expiree le $($graceUntil.ToString('yyyy-MM-dd'))."
    }

    # Niveau calcule depuis les preuves. Jamais saisi.
    $technicalPreview = (Test-Satisfied $proof.Domain) -and ($proof.Api.Status -eq "ok") -and ($proof.Tests.Status -eq "ok")
    $functional = $technicalPreview -and
        ($proof.Application.Status -eq "ok") -and
        (Test-Satisfied $proof.PostgreSql) -and
        ($proof.Rbac.Status -eq "ok") -and
        ($proof.Desktop.Status -eq "ok") -and
        ($documentationSatisfied -or $documentationByGrace)
    $production = $functional -and $documentationSatisfied -and
        ($proof.Smoke.Status -eq "ok") -and ($postgresqlCi.Status -eq "ok") -and ($e2e.Status -eq "ok")

    $computed = if ($production) { "ProductionReady" } elseif ($functional) { "Functional" } elseif ($technicalPreview) { "TechnicalPreview" } else { "Planned" }

    if ($levelRank[$declared] -gt $levelRank[$computed]) {
        Add-Failure "$tabName ($($catalogOrders -join ', ')) : niveau declare $($levelLabel[$declared]) superieur au niveau prouve $($levelLabel[$computed])."
    }

    $missing = @()
    foreach ($criterion in @('Domain', 'Application', 'Api', 'PostgreSql', 'Rbac', 'Desktop', 'Tests')) {
        if (-not (Test-Satisfied $proof[$criterion])) { $missing += $criterion }
    }
    if (-not $documentationSatisfied) {
        $missing += if ($documentationByGrace) { "Documentation (grace jusqu'au $($graceUntil.ToString('yyyy-MM-dd')))" } else { "Documentation" }
    }
    if ($proof.Smoke.Status -ne "ok") { $missing += "Smoke" }

    $screenRows += [pscustomobject]@{
        Onglet = $tabIndex
        Ecran = $tabName
        Ordres = ($catalogOrders -join ", ")
        Domaine = ($domainIdsForScreen -join ", ")
        Permission = $permissionConst
        Declare = $levelLabel[$declared]
        Prouve = if ($documentationByGrace -and $computed -eq "Functional") { "Functional (grace doc)" } else { $levelLabel[$computed] }
        Manquant = if ($missing.Count -eq 0) { "-" } else { $missing -join ", " }
        Computed = $computed
        Grace = $documentationByGrace
        Proof = $proof
    }
}

# ---------------------------------------------------------------------------------
# Sortie : tableau enrichi + resume + Markdown pour le resume de job GitHub.
# ---------------------------------------------------------------------------------
Write-Host ""
$screenRows |
    Select-Object Onglet, Ecran, Ordres, Domaine, Permission, Declare, Prouve, Manquant |
    Format-Table -AutoSize -Wrap | Out-String -Width 260 | Write-Host

$counts = @{ Planned = 0; TechnicalPreview = 0; Functional = 0; ProductionReady = 0 }
$graceCount = 0
foreach ($screenRow in $screenRows) {
    if ($screenRow.PSObject.Properties['Computed'] -and $screenRow.Computed) { $counts[$screenRow.Computed]++ }
    if ($screenRow.PSObject.Properties['Grace'] -and $screenRow.Grace) { $graceCount++ }
}
$summaryLine = "Readiness par ecran: $($screenRows.Count) ecrans - Production Ready $($counts.ProductionReady), Functional $($counts.Functional) (dont $graceCount en grace documentation), Technical Preview $($counts.TechnicalPreview), Planned $($counts.Planned)."
Write-Host $summaryLine
if ($graceUntil) {
    $graceState = if ($graceActive) { "active" } else { "EXPIREE" }
    Write-Host "Grace documentation: $graceState (jusqu'au $($graceUntil.ToString('yyyy-MM-dd')), date de reference $($asOfDate.ToString('yyyy-MM-dd')), $($graceScreens.Count) ecrans listes)."
}

$summaryTarget = if ($MarkdownSummaryPath) { $MarkdownSummaryPath } elseif ($env:GITHUB_STEP_SUMMARY) { $env:GITHUB_STEP_SUMMARY } else { $null }
if ($summaryTarget) {
    $markdown = New-Object System.Collections.Generic.List[string]
    $markdown.Add("## Module readiness")
    $markdown.Add("")
    $markdown.Add("Modules Disponibles cables : **$($rows.Count)/$expectedAvailable** - Domaines fonctionnels : **$($domains.Count)/$expectedDomainCount**")
    $markdown.Add("")
    $markdown.Add("| Onglet | Ecran | Ordres | Domaine cible | Permission | Declare | Prouve | Preuves manquantes |")
    $markdown.Add("|---:|---|---|---|---|---|---|---|")
    foreach ($screenRow in $screenRows) {
        $markdown.Add("| $($screenRow.Onglet) | $($screenRow.Ecran) | $($screenRow.Ordres) | $($screenRow.Domaine) | $($screenRow.Permission) | $($screenRow.Declare) | $($screenRow.Prouve) | $($screenRow.Manquant) |")
    }
    $markdown.Add("")
    $markdown.Add($summaryLine)
    if ($failures.Count -gt 0) {
        $markdown.Add("")
        $markdown.Add("### Echecs")
        foreach ($failure in $failures) { $markdown.Add("- $failure") }
    }
    if ($warnings.Count -gt 0) {
        $markdown.Add("")
        $markdown.Add("### Avertissements")
        foreach ($warning in $warnings) { $markdown.Add("- $warning") }
    }
    Add-Content -Path $summaryTarget -Value ($markdown -join "`n") -Encoding UTF8
}

foreach ($warning in $warnings) { Write-Host "AVERTISSEMENT: $warning" -ForegroundColor Yellow }

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "Module readiness: $($failures.Count) echec(s)." -ForegroundColor Red
    foreach ($failure in $failures) { Write-Host "ECHEC: $failure" -ForegroundColor Red }
    exit 1
}

Write-Host "Module readiness: OK (catalogue, domaines, preuves par ecran et niveaux coherents)." -ForegroundColor Green
exit 0
