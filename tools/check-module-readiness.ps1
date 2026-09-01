param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$catalogPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Desktop/ModuleCatalog.cs"
$xamlPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Desktop/MainWindow.xaml"
$mainWindowPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Desktop/MainWindow.xaml.cs"
$permissionPath = Join-Path $RepositoryRoot "src/RaqmiSystem.Domain/Identity/PermissionCatalog.cs"

foreach ($path in @($catalogPath, $xamlPath, $mainWindowPath, $permissionPath)) {
    if (-not (Test-Path $path)) {
        throw "Module readiness: fichier requis introuvable: $path"
    }
}

$catalog = Get-Content $catalogPath -Raw -Encoding UTF8
$xaml = Get-Content $xamlPath -Raw -Encoding UTF8
$mainWindow = (Get-ChildItem (Join-Path $RepositoryRoot "src/RaqmiSystem.Desktop") -Filter "MainWindow*.cs" -File |
    ForEach-Object { Get-Content $_.FullName -Raw -Encoding UTF8 }) -join "`n"
$permissions = Get-Content $permissionPath -Raw -Encoding UTF8

function Get-ConstInt([string]$name) {
    $match = [regex]::Match($catalog, "public\s+const\s+int\s+$name\s*=\s*(\d+)\s*;")
    if (-not $match.Success) { throw "Module readiness: constante $name introuvable dans ModuleCatalog." }
    return [int]$match.Groups[1].Value
}

$expectedTotal = Get-ConstInt "ExpectedTotal"
$expectedAvailable = Get-ConstInt "ExpectedAvailable"

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
        TabIndex = $tabIndex
        TabName = $tabName
        Navigation = if ($shared) { "OK (ecran partage)" } else { "OK" }
        RBAC = "OK"
        Desktop = "OK"
    }
}

Write-Host "Module readiness gate: $($rows.Count)/$expectedAvailable modules Disponibles cables Navigation + RBAC + Desktop."
$rows | Sort-Object TabIndex, Order | Format-Table -AutoSize
