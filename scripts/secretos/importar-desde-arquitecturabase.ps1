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
# La Base conserva HashKey en appsettings.Development.json; aquí sólo se escribe en user-secrets.
$claves = @(
    'Authentication:Google:ClientSecret',
    'Authentication:LoginCode:HashKey',
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
    if ($linea) {
        $valor = $linea.Substring($clave.Length + 3)
    }
    elseif ($clave -eq 'Authentication:LoginCode:HashKey') {
        $configuracionBase = Get-Content -LiteralPath (Join-Path $Origen 'appsettings.Development.json') -Raw | ConvertFrom-Json
        $valor = $configuracionBase.Authentication.LoginCode.HashKey
        Remove-Variable configuracionBase
    }
    if ([string]::IsNullOrWhiteSpace($valor)) {
        Write-Warning "No está en el origen: $clave"
        Remove-Variable valor -ErrorAction SilentlyContinue
        continue
    }
    dotnet user-secrets set $clave $valor --project $Destino | Out-Null
    Remove-Variable valor
    Write-Host "Copiada: $clave"
    $copiadas++
}
Remove-Variable lineas
Write-Host "`n$copiadas de $($claves.Count) secretos copiados. Verificá con: ./scripts/secretos/verificar.ps1"
