<#
.SYNOPSIS
  Dice qué secretos están cargados y cuáles faltan, SIN mostrar ningún valor.
#>
param(
    [string]$Destino = (Join-Path $PSScriptRoot '..\..\src\ArquitecturaBaseMultitenant.Api'),
    [string]$Ambiente = 'Development'
)

$requeridas = [ordered]@{
    'Email:Smtp:Password'               = 'Gmail (contraseña de aplicación). Sin ella la Api no arranca con Email:Delivery=Smtp'
    'Authentication:Google:ClientSecret' = 'Ingreso y registro con Google'
    'Authentication:LoginCode:HashKey'    = 'Firma HMAC de los códigos de ingreso y registro'
    'Seed:PlatformOwner:Email'          = 'Operador inicial; obligatoria en Production si aún no existe uno'
    'WhatsApp:AccessToken'              = 'Enviar por WhatsApp (obligatoria si hay WhatsApp:PhoneNumberId)'
    'WhatsApp:AppSecret'                = 'Webhook de WhatsApp (va junto con VerifyToken)'
    'WhatsApp:VerifyToken'              = 'Webhook de WhatsApp (va junto con AppSecret)'
}
if ($Ambiente -notin @('Development', 'Testing')) {
    $requeridas['DataProtection:Certificate:Base64'] =
        'PFX que cifra las claves de Data Protection fuera de Development y Testing'
}

$cargadas = (dotnet user-secrets list --project $Destino) -replace ' = .*', ''
foreach ($clave in $requeridas.Keys) {
    if ($cargadas -contains $clave) { Write-Host "[ok]    $clave" -ForegroundColor Green }
    else { Write-Host "[falta] $clave  -> $($requeridas[$clave])" -ForegroundColor Yellow }
}
