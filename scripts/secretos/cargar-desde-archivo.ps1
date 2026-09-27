<#
.SYNOPSIS
  Carga todos los secretos de un archivo JSON (guardado FUERA del repo) en los user-secrets de la Api.
  El archivo sigue la forma de docs/operations/secretos.plantilla.json.

.EXAMPLE
  ./scripts/secretos/cargar-desde-archivo.ps1 -Archivo "$env:USERPROFILE\secrets\ArquitecturaBaseMultitenant\secretos.json"
#>
param(
    [Parameter(Mandatory)][string]$Archivo,
    [string]$Destino = (Join-Path $PSScriptRoot '..\..\src\ArquitecturaBaseMultitenant.Api')
)

$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$ruta = (Resolve-Path $Archivo).Path
if ($ruta.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "El archivo de secretos está dentro del repo. Movelo a una carpeta tuya (por ejemplo $env:USERPROFILE\secrets\) y volvé a correr."
}

# Valida que sea JSON antes de mandarlo.
$null = Get-Content $ruta -Raw | ConvertFrom-Json

# dotnet user-secrets acepta un JSON por la entrada estándar y guarda cada clave anidada como "A:B:C".
Get-Content $ruta -Raw | dotnet user-secrets set --project $Destino | Out-Null
Write-Host "Secretos cargados. Verificá con: ./scripts/secretos/verificar.ps1"
