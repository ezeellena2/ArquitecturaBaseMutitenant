<#
.SYNOPSIS
  Dice qué secretos están cargados y cuáles faltan, SIN mostrar ningún valor.
#>
param([string]$Destino = (Join-Path $PSScriptRoot '..\..\src\ArquitecturaBaseMultitenant.Api'))

$requeridas = [ordered]@{
    'Email:Smtp:Password'               = 'Gmail (contraseña de aplicación). Sin ella la Api no arranca con Email:Delivery=Smtp'
    'Authentication:Google:ClientSecret' = 'Ingreso y registro con Google'
    'Authentication:LoginCode:HashKey'    = 'Firma HMAC de los códigos de ingreso y registro'
    'WhatsApp:AccessToken'              = 'Enviar por WhatsApp (obligatoria si hay WhatsApp:PhoneNumberId)'
    'WhatsApp:AppSecret'                = 'Webhook de WhatsApp (va junto con VerifyToken)'
    'WhatsApp:VerifyToken'              = 'Webhook de WhatsApp (va junto con AppSecret)'
}

$cargadas = (dotnet user-secrets list --project $Destino) -replace ' = .*', ''
foreach ($clave in $requeridas.Keys) {
    if ($cargadas -contains $clave) { Write-Host "[ok]    $clave" -ForegroundColor Green }
    else { Write-Host "[falta] $clave  -> $($requeridas[$clave])" -ForegroundColor Yellow }
}
