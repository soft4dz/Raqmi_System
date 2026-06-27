param(
  [string]$InstallRoot = "$env:ProgramFiles\Raqmi System Server"
)

$ErrorActionPreference = 'Stop'
$DataRoot = "$env:ProgramData\Raqmi System"
$nodeExe = Join-Path $InstallRoot 'node\node.exe'
$serverEntry = Join-Path $InstallRoot 'server\dist\server.mjs'
$envFile = Join-Path $DataRoot 'server.env'
$databaseRoot = Join-Path $InstallRoot 'packages\database'

if (-not (Test-Path $nodeExe)) {
  $nodeExe = 'node'
}

Get-Content $envFile | ForEach-Object {
  if ($_ -match '^\s*([^#=]+)=(.*)$') {
    Set-Item -Path "env:$($matches[1].Trim())" -Value $matches[2].Trim()
  }
}

Set-Location (Join-Path $InstallRoot 'server')
& $nodeExe $serverEntry
