param(
  [string]$InstallRoot = "$env:ProgramFiles\Raqmi System Server",
  [string]$DataRoot = "$env:ProgramData\Raqmi System"
)

$ErrorActionPreference = 'Stop'
$pgBin = Join-Path $InstallRoot 'postgresql\bin'
$pgData = Join-Path $DataRoot 'data\postgres'
$serverDir = Join-Path $InstallRoot 'server'
$nodeExe = Join-Path $InstallRoot 'node\node.exe'

$pgService = 'RaqmiPostgreSQL'
$apiService = 'RaqmiSystemServer'

if (Get-Service -Name $pgService -ErrorAction SilentlyContinue) {
  Write-Host "Service $pgService déjà enregistré"
} else {
  $pgCtl = Join-Path $pgBin 'pg_ctl.exe'
  $pgRegister = "sc.exe create `"$pgService`" binPath= `"`"$pgCtl`" runservice -N `"$pgService`" -D `"$pgData`" -w`" start= auto DisplayName= `"Raqmi PostgreSQL`""
  cmd.exe /c $pgRegister
}

$envFile = Join-Path $DataRoot 'server.env'
if (-not (Test-Path $envFile)) {
  @"
PORT=3000
DATABASE_URL=postgresql://raqmi:raqmi_password@127.0.0.1:5432/raqmi_system
RAQMI_DATA_DIR=$DataRoot
LICENSE_FILE_PATH=$DataRoot\license.license
LICENSE_PUBLIC_KEY_PATH=$InstallRoot\server\keys\public.jwk.json
LICENSE_MODE=offline
FILE_STORAGE_DRIVER=local
FILE_STORAGE_LOCAL_PATH=$DataRoot\storage
JWT_SECRET=change-me-after-install
"@ | Set-Content -Encoding UTF8 $envFile
}

$serverStart = Join-Path $InstallRoot 'installer\scripts\start-server.ps1'
if (Get-Service -Name $apiService -ErrorAction SilentlyContinue) {
  Write-Host "Service $apiService déjà enregistré"
} else {
  $binPath = "powershell.exe -ExecutionPolicy Bypass -File `"$serverStart`" -InstallRoot `"$InstallRoot`""
  sc.exe create $apiService binPath= $binPath start= auto DisplayName= "Raqmi System Server"
}

Write-Host "Services Windows enregistrés."
