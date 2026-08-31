<#
.SYNOPSIS
    Alimente une base Raqmi System avec un jeu de demonstration complet.

.DESCRIPTION
    Ce script sert a produire les captures du guide utilisateur : il remplit les 28 modules
    livres avec les donnees d'un groupe hotelier algerien fictif, afin qu'aucun ecran du guide
    ne soit vide.

    Il passe EXCLUSIVEMENT par l'API HTTP, jamais par SQL. Consequences voulues :
      - les regles metier s'appliquent (une facture emise ne s'edite plus, un stock se valorise
        au PMP, un bon de commande prend son numero a l'approbation) ;
      - le journal d'audit se remplit tout seul, avec de vrais horodatages et de vrais acteurs ;
      - le jeu ne peut pas contenir un etat que l'application refuserait de produire.

    A NE PAS LANCER SUR UNE BASE DE PRODUCTION. Le script cree des utilisateurs, des clients,
    des factures et des ecritures comptables : il est concu pour une base de demonstration
    dediee (voir tools/demo-seed/README.md).

.PARAMETER ApiBaseUrl
    Racine de l'API, sans /api/v1.

.PARAMETER AdminUser
    Compte administrateur existant (celui du seed de securite).

.PARAMETER AdminPassword
    Mot de passe de ce compte.

.PARAMETER DemoPassword
    Mot de passe attribue aux comptes de demonstration crees par le script (12 caracteres mini).

.EXAMPLE
    .\seed-demo.ps1 -AdminUser admin@demo.local -AdminPassword '...'
#>
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://localhost:5180',
    [Parameter(Mandatory = $true)][string]$AdminUser,
    [Parameter(Mandatory = $true)][string]$AdminPassword,
    [string]$DemoPassword = 'Demo-Raqmi-2026!'
)

$ErrorActionPreference = 'Stop'

$script:ApiBaseUrl = $ApiBaseUrl.TrimEnd('/')
$script:DemoPassword = $DemoPassword
$script:Failures = 0
$script:Calls = 0

# Tirages reproductibles : deux executions du script sur deux bases fraiches produisent les
# memes montants, donc les memes captures. Un guide dont les chiffres changent a chaque
# regeneration est un guide qu'on ne peut pas relire.
$script:Random = New-Object System.Random 20260831

# ---------------------------------------------------------------------------- Utilitaires

function Write-Step {
    param([string]$Message)
    Write-Host ''
    Write-Host ('== ' + $Message) -ForegroundColor Cyan
}

function Write-Detail {
    param([string]$Message)
    Write-Host ('   ' + $Message) -ForegroundColor DarkGray
}

function Get-Date-Text {
    param([datetime]$Value)
    return $Value.ToString('yyyy-MM-dd')
}

function Get-RandomInt {
    param([int]$Minimum, [int]$Maximum)
    return $script:Random.Next($Minimum, $Maximum + 1)
}

function Get-RandomPick {
    param([object[]]$Values)
    return $Values[$script:Random.Next(0, $Values.Length)]
}

function Get-RandomAmount {
    param([decimal]$Minimum, [decimal]$Maximum, [int]$Step = 50)
    $steps = [int](($Maximum - $Minimum) / $Step)
    return $Minimum + ($script:Random.Next(0, $steps + 1) * $Step)
}

function Invoke-Api {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateSet('GET', 'POST', 'PUT', 'DELETE')][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        $Body = $null,
        [string]$Token = $null,
        [switch]$Anonymous,
        [switch]$Quiet
    )

    $uri = $script:ApiBaseUrl + '/api/v1' + $Path
    $headers = @{}

    if (-not $Anonymous) {
        if ([string]::IsNullOrEmpty($Token)) { $Token = $script:Token }
        if (-not [string]::IsNullOrEmpty($Token)) { $headers['Authorization'] = 'Bearer ' + $Token }
    }

    $script:Calls++

    try {
        if ($null -ne $Body) {
            # ConvertTo-Json de PowerShell 5.1 s'arrete a 2 niveaux par defaut : les lignes de
            # facture et les ecritures comptables seraient tronquees sans -Depth.
            $json = $Body | ConvertTo-Json -Depth 20 -Compress
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)

            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers `
                -Body $bytes -ContentType 'application/json; charset=utf-8' -TimeoutSec 90
        }

        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -TimeoutSec 90
    }
    catch {
        $script:Failures++

        if (-not $Quiet) {
            $detail = ''
            try {
                $response = $_.Exception.Response
                if ($null -ne $response) {
                    $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
                    $detail = $reader.ReadToEnd()
                    $reader.Close()
                }
            }
            catch {
                $detail = ''
            }

            Write-Warning ($Method + ' ' + $Path + ' -> ' + $_.Exception.Message + ' ' + $detail)
        }

        return $null
    }
}

function Connect-Raqmi {
    param(
        [Parameter(Mandatory = $true)][string]$UserNameOrEmail,
        [Parameter(Mandatory = $true)][string]$Password
    )

    $login = Invoke-Api -Method POST -Path '/auth/login' -Anonymous -Body @{
        userNameOrEmail = $UserNameOrEmail
        password        = $Password
    }

    if ($null -eq $login) {
        throw ("Connexion refusee pour " + $UserNameOrEmail + " sur " + $script:ApiBaseUrl + ".")
    }

    return $login.accessToken
}

# Cree un compte, consomme son mot de passe temporaire et le remplace par le mot de passe de
# demonstration. Le jeton renvoye permet de faire agir CE profil : le guide montre alors de
# vrais noms en "saisi par" / "valide par", et les permissions reellement en jeu.
function New-DemoUser {
    param(
        [Parameter(Mandatory = $true)][string]$UserName,
        [Parameter(Mandatory = $true)][string]$Email,
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [Parameter(Mandatory = $true)][string[]]$Roles
    )

    $created = Invoke-Api -Method POST -Path '/security/users' -Body @{
        userName    = $UserName
        email       = $Email
        displayName = $DisplayName
        roles       = $Roles
    }

    if ($null -eq $created) { return $null }

    $temporaryToken = Connect-Raqmi -UserNameOrEmail $UserName -Password $created.temporaryPassword

    $changed = Invoke-Api -Method POST -Path '/account/change-password' -Token $temporaryToken -Body @{
        currentPassword = $created.temporaryPassword
        newPassword     = $script:DemoPassword
    }

    if ($null -eq $changed) { return $null }

    Write-Detail ($DisplayName + ' (' + ($Roles -join ', ') + ')')

    return [pscustomobject]@{
        Id          = $created.user.id
        UserName    = $UserName
        DisplayName = $DisplayName
        Token       = Connect-Raqmi -UserNameOrEmail $UserName -Password $script:DemoPassword
    }
}

# ------------------------------------------------------------------------------ Campagne

$script:Today = (Get-Date).Date
$script:Token = Connect-Raqmi -UserNameOrEmail $AdminUser -Password $AdminPassword

Write-Host ''
Write-Host ('Raqmi System - jeu de demonstration') -ForegroundColor White
Write-Host ('API      : ' + $script:ApiBaseUrl) -ForegroundColor DarkGray
Write-Host ("Date du jour : " + (Get-Date-Text $script:Today)) -ForegroundColor DarkGray

$parts = @(
    '10-socle.ps1',
    '20-finance.ps1',
    '30-hebergement.ps1',
    '40-logistique.ps1',
    '50-ressources-humaines.ps1',
    '60-relation-client.ps1',
    '70-pilotage.ps1'
)

foreach ($part in $parts) {
    $path = Join-Path (Join-Path $PSScriptRoot 'parts') $part
    if (-not (Test-Path $path)) { throw ('Etape introuvable : ' + $path) }
    . $path
}

Write-Host ''
if ($script:Failures -eq 0) {
    Write-Host ('Termine : ' + $script:Calls + ' appels API, aucune erreur.') -ForegroundColor Green
}
else {
    Write-Host ('Termine : ' + $script:Calls + ' appels API, ' + $script:Failures + ' en erreur (voir les avertissements ci-dessus).') -ForegroundColor Yellow
}
