# deploy/onpremise/start-api.ps1
#
# Entry point of the "Raqmi System API" scheduled task registered by
# install-server.ps1 (SYSTEM account, at boot). Not meant to be launched by
# hand except for troubleshooting.
#
# What it does:
#   1. Dot-sources the protected environment file
#      <install-root>\config\raqmi.env.ps1 (ASPNETCORE_*, RAQMI_POSTGRES__*,
#      RAQMI_JWT__*) - the API reads all its production configuration from
#      those environment variables (Program.cs: AddEnvironmentVariables
#      prefix "RAQMI_").
#   2. Rotates logs: deletes api_*.log files older than 30 days in
#      <install-root>\logs.
#   3. Runs <install-root>\api\RaqmiSystem.Api.exe, redirecting its console
#      output to date-stamped files. The API logs one compact JSON object per
#      line to stdout (Serilog, see Program.cs), so api_<timestamp>.log is a
#      JSON-lines file; stderr goes to api_<timestamp>_err.log.
#   4. Exits with the API's own exit code, so the Task Scheduler's
#      restart-on-failure setting can react to a crash.

$ErrorActionPreference = 'Stop'

$baseDir = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $baseDir 'config\raqmi.env.ps1'
$apiDir = Join-Path $baseDir 'api'
$apiExe = Join-Path $apiDir 'RaqmiSystem.Api.exe'
$logDir = Join-Path $baseDir 'logs'

if (-not (Test-Path $envFile)) {
    Write-Host "[start-api] ERROR: environment file not found: $envFile"
    exit 1
}
if (-not (Test-Path $apiExe)) {
    Write-Host "[start-api] ERROR: API executable not found: $apiExe (run install-server.ps1 first)"
    exit 1
}

. $envFile

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

# Simple rotation: one log file per API start, anything older than 30 days
# is deleted. The filter matches both api_<ts>.log and api_<ts>_err.log.
$cutoff = (Get-Date).AddDays(-30)
foreach ($oldLog in @(Get-ChildItem -Path $logDir -Filter 'api_*.log' -File)) {
    if ($oldLog.LastWriteTime -lt $cutoff) {
        Remove-Item -Path $oldLog.FullName -Force -ErrorAction SilentlyContinue
    }
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$stdoutLog = Join-Path $logDir ('api_{0}.log' -f $stamp)
$stderrLog = Join-Path $logDir ('api_{0}_err.log' -f $stamp)

Write-Host "[start-api] Starting $apiExe (logs: $stdoutLog)"

# Start-Process is used (rather than `&` with redirection) because PowerShell
# 5.1 wraps a native process's stderr lines in ErrorRecords when redirected
# in-shell; Start-Process writes both streams to files verbatim. The child
# inherits the environment variables loaded above.
$process = Start-Process -FilePath $apiExe -WorkingDirectory $apiDir `
    -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog `
    -NoNewWindow -PassThru -Wait

Write-Host ("[start-api] API exited with code {0}" -f $process.ExitCode)
exit $process.ExitCode
