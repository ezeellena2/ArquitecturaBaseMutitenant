<#
.SYNOPSIS
  Copia a la Api del multitenant los secretos que ya están cargados en los user-secrets de ArquitecturaBase.
  Los valores nunca se muestran: se leen y se escriben en el mismo paso.

.EXAMPLE
  ./scripts/secretos/importar-desde-arquitecturabase.ps1
#>
param(
    [string]$Origen  = (Join-Path $PSScriptRoot '..\..\..\ArquitecturaBase\src\ArquitecturaBase.Api'),
    [string]$Destino = (Join-Path $PSScriptRoot '..\..\src\ArquitecturaBaseMultitenant.Api')
)

$ErrorActionPreference = 'Stop'

# Las mismas claves que ArquitecturaBase (docs/operations/configuracion.md).
$claves = @(
    'Authentication:Google:ClientSecret',
    'Email:Smtp:Password',
    'WhatsApp:AccessToken',
    'WhatsApp:AppSecret',
    'WhatsApp:VerifyToken'
)

if (-not (Test-Path $Origen))  { throw "No encuentro el proyecto de origen: $Origen" }
if (-not (Test-Path $Destino)) { throw "No encuentro la Api del multitenant: $Destino (nace en la Etapa 0)" }

$lineas = dotnet user-secrets list --project $Origen
$copiadas = 0
foreach ($clave in $claves) {
    $linea = $lineas | Where-Object { $_.StartsWith("$clave = ") } | Select-Object -First 1
    if (-not $linea) { Write-Warning "No está en el origen: $clave"; continue }
    $valor = $linea.Substring($clave.Length + 3)
    dotnet user-secrets set $clave $valor --project $Destino | Out-Null
    Remove-Variable valor
    Write-Host "Copiada: $clave"
    $copiadas++
}
Remove-Variable lineas
Write-Host "`n$copiadas de $($claves.Count) secretos copiados. Verificá con: ./scripts/secretos/verificar.ps1"
