# =============================================================================================
#  Socle : identite de l'etablissement, unites hotelieres, comptes utilisateurs.
#  Alimente les modules 1 (Administration & utilisateurs), 2 (Parametrage global)
#  et 3 (Unites hotelieres).
# =============================================================================================

Write-Step 'Parametrage global (module 2)'

$null = Invoke-Api -Method PUT -Path '/settings' -Body @{
    companyName        = 'Groupe Hotelier El Bahdja'
    defaultVatRate     = 19
    auditRetentionDays = 365
    companyNif         = '000216001234567'
    companyRc          = '16/00-1234567B16'
    companyAi          = '16123456789'
    companyNis         = '000216987654321'
    companyAddress     = "12, boulevard Mohamed V"
    companyCity        = 'Alger Centre'
    companyPhone       = '+213 21 63 40 00'
    companyEmail       = 'contact@elbahdja-demo.dz'
    currencyLabel      = 'DA'
}

Write-Detail 'Identite du groupe, TVA 19 %, retention du journal 365 jours'

# ------------------------------------------------------------------------- Unites hotelieres

Write-Step 'Unites hotelieres (module 3)'

# Quatre unites de nature differente : le guide peut ainsi montrer une consolidation groupe
# qui ne soit pas la simple repetition du meme etablissement.
$script:Units = @(
    [pscustomobject]@{ Code = 'ALG-CEN'; Name = 'Hotel Riadh Alger Centre'; Type = 'Hotel'; Order = 1; Rooms = 42 },
    [pscustomobject]@{ Code = 'ORN-COR'; Name = 'Hotel Corniche Oran'; Type = 'Hotel'; Order = 2; Rooms = 28 },
    [pscustomobject]@{ Code = 'TIP-AZU'; Name = 'Residence Azur Tipaza'; Type = 'Residence'; Order = 3; Rooms = 18 },
    [pscustomobject]@{ Code = 'BEJ-CAP'; Name = 'Marina Cap Carbone Bejaia'; Type = 'Marina'; Order = 4; Rooms = 12 }
)

foreach ($unit in $script:Units) {
    $created = Invoke-Api -Method POST -Path '/organization/hotel-units' -Body @{
        code         = $unit.Code
        name         = $unit.Name
        unitType     = $unit.Type
        displayOrder = $unit.Order
    }

    if ($null -ne $created) { Write-Detail ($unit.Code + ' - ' + $unit.Name) }
}

# --------------------------------------------------------------------------------- Comptes

Write-Step 'Comptes et roles (module 1)'

# Un compte par profil du catalogue de roles. Ils ne servent pas de decor : les etapes
# suivantes agissent SOUS ces comptes, pour que les colonnes "saisi par", "valide par" et
# "approuve par" du guide portent des noms differents et coherents avec les permissions.
$script:People = @{}

$definitions = @(
    @{ Key = 'Direction'; UserName = 'd.hamdani'; Email = 'd.hamdani@elbahdja-demo.dz'; Display = 'Dalila Hamdani'; Roles = @('direction') },
    @{ Key = 'Controle'; UserName = 'k.benali'; Email = 'k.benali@elbahdja-demo.dz'; Display = 'Karim Benali'; Roles = @('exploitation.control') },
    @{ Key = 'ChefAlger'; UserName = 's.merzouk'; Email = 's.merzouk@elbahdja-demo.dz'; Display = 'Samir Merzouk'; Roles = @('unit.manager') },
    @{ Key = 'ChefOran'; UserName = 'r.belkacem'; Email = 'r.belkacem@elbahdja-demo.dz'; Display = 'Rachida Belkacem'; Roles = @('unit.manager') },
    @{ Key = 'Caisse'; UserName = 'y.ferhat'; Email = 'y.ferhat@elbahdja-demo.dz'; Display = 'Yacine Ferhat'; Roles = @('cashier') },
    @{ Key = 'Rh'; UserName = 'n.saidi'; Email = 'n.saidi@elbahdja-demo.dz'; Display = 'Nadia Saidi'; Roles = @('hr.manager') },
    @{ Key = 'Lecture'; UserName = 'l.ouali'; Email = 'l.ouali@elbahdja-demo.dz'; Display = 'Lamine Ouali'; Roles = @('reader') }
)

foreach ($definition in $definitions) {
    $person = New-DemoUser `
        -UserName $definition.UserName `
        -Email $definition.Email `
        -DisplayName $definition.Display `
        -Roles $definition.Roles

    if ($null -ne $person) { $script:People[$definition.Key] = $person }
}

# Raccourcis : les etapes suivantes n'ont pas a savoir si un profil a ete cree ou non.
function Get-Token {
    param([string]$Key)

    if ($script:People.ContainsKey($Key)) { return $script:People[$Key].Token }
    return $script:Token
}
