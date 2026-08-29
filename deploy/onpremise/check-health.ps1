# deploy/onpremise/check-health.ps1
#
# Local health probe for the on-premise pilot server. Calls the two endpoints
# exposed by the API (Program.cs):
#
#   GET /health           -> 200 when the API process is up
#   GET /health/database  -> 200 when PostgreSQL is reachable, 503 otherwise
#
# Exit code: 0 when both answer 200, 1 otherwise - so it can be wired into a
# scheduled task whose "last run result" becomes the local alert signal, e.g.:
#
#   Register-ScheduledTask -TaskName 'Raqmi System Health' `
#     -Action (New-ScheduledTaskAction -Execute 'powershell.exe' `
#       -Argument '-NoProfile -ExecutionPolicy Bypass -File "C:\RaqmiSystem\scripts\check-health.ps1"') `
#     -Trigger (New-ScheduledTaskTrigger -Once -At (Get-Date) `
#       -RepetitionInterval (New-TimeSpan -Minutes 15)) `
#     -Principal (New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount)
#
# Reporting choice: a plain STATE FILE (<install-root>\logs\health-status.txt,
# overwritten on every run) instead of the Windows event log. Write-EventLog
# needs an event source registered once by an administrator (New-EventLog)
# and adds nothing for a single-server pilot where whoever investigates will
# open the logs directory anyway; a text file next to the API logs is
# self-explanatory and needs no privileges beyond writing to that directory.
#
# The port is read from the same protected environment file as the other
# scripts (RAQMI_API_PORT in <install-root>\config\raqmi.env.ps1), and can be
# overridden with -Port. If the file is unreadable (e.g. probe launched by a
# non-admin), the default port 5180 is used.

[CmdletBinding()]
param(
    [int]$Port = 0,
    [string]$EnvFile = ''
)

$ErrorActionPreference = 'Stop'

$baseDir = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $EnvFile = Join-Path $baseDir 'config\raqmi.env.ps1'
}

if ($Port -eq 0) {
    try {
        if (Test-Path $EnvFile) {
            . $EnvFile
        }
    }
    catch {
        # Config unreadable (ACL) - fall through to the default port.
    }
    if (-not [string]::IsNullOrWhiteSpace($env:RAQMI_API_PORT)) {
        $Port = [int]$env:RAQMI_API_PORT
    }
    else {
        $Port = 5180
    }
}

function Test-Endpoint {
    param([Parameter(Mandatory = $true)][string]$Url)
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 10
        if ($response.StatusCode -eq 200) {
            return 'OK'
        }
        return ('FAIL (HTTP {0})' -f $response.StatusCode)
    }
    catch {
        return ('FAIL ({0})' -f $_.Exception.Message)
    }
}

$apiStatus = Test-Endpoint -Url ("http://localhost:{0}/health" -f $Port)
$dbStatus = Test-Endpoint -Url ("http://localhost:{0}/health/database" -f $Port)

if ($apiStatus -eq 'OK' -and $dbStatus -eq 'OK') {
    $overall = 'HEALTHY'
    $exitCode = 0
}
else {
    $overall = 'UNHEALTHY'
    $exitCode = 1
}

$report = @(
    ('checked_at      : {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
    ('overall         : {0}' -f $overall)
    ('/health         : {0}' -f $apiStatus)
    ('/health/database: {0}' -f $dbStatus)
    ('port            : {0}' -f $Port)
)

foreach ($line in $report) {
    Write-Host ('[check-health] ' + $line)
}

# State file: best effort - a probe must still return its exit code even if
# the logs directory is unwritable.
try {
    $logDir = Join-Path $baseDir 'logs'
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    Set-Content -Path (Join-Path $logDir 'health-status.txt') -Value $report -Encoding utf8
}
catch {
    Write-Host ('[check-health] WARNING: could not write the state file: {0}' -f $_.Exception.Message)
}

exit $exitCode
