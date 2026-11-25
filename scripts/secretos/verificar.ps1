<#
.SYNOPSIS
  Dice qué secretos y datos de la instalación están cargados y cuáles faltan, SIN mostrar ningún valor.
#>
param(
    [string]$Destino = (Join-Path $PSScriptRoot '..\..\src\ArquitecturaBaseMultitenant.Api'),
    [string]$Ambiente = 'Development'
)

$requeridas = [ordered]@{
    'Email:Smtp:Password'               = 'Gmail (contraseña de aplicación). Sin ella la Api no arranca con Email:Delivery=Smtp'
    'Authentication:Google:ClientSecret' = 'Ingreso y registro con Google'
    'Authentication:LoginCode:HashKey'    = 'Firma HMAC de los códigos de ingreso y registro'
    'Seed:PlatformOwner:Email'           = 'Correo del operador inicial; obligatorio en Development y Production'
    'Seed:PlatformOwner:DisplayName'     = 'Nombre visible del operador inicial'
    'WhatsApp:AccessToken'              = 'Enviar por WhatsApp (obligatoria si hay WhatsApp:PhoneNumberId)'
    'WhatsApp:AppSecret'                = 'Webhook de WhatsApp (va junto con VerifyToken)'
    'WhatsApp:VerifyToken'              = 'Webhook de WhatsApp (va junto con AppSecret)'
    'Authentication:Google:ClientId'     = 'Dato de la instalación: sin él el botón de Google no aparece'
    'Email:Smtp:UserName'               = 'Dato de la instalación: la cuenta de Gmail que manda los correos'
    'Email:Smtp:FromAddress'            = 'Dato de la instalación: el remitente, la misma cuenta de Gmail'
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

$opcionales = [ordered]@{
    'WhatsApp:PhoneNumberId'          = 'Dato de la instalación: el número de Plataforma del arranque; sin él WhatsApp queda apagado'
    'WhatsApp:AppId'                  = 'Dato de la instalación: con EmbeddedSignupConfigId, las pantallas de números ofrecen conectar'
    'WhatsApp:EmbeddedSignupConfigId' = 'Dato de la instalación: va junto con WhatsApp:AppId'
}
foreach ($clave in $opcionales.Keys) {
    if ($cargadas -contains $clave) { Write-Host "[ok]       $clave" -ForegroundColor Green }
    else { Write-Host "[opcional] $clave  -> $($opcionales[$clave])" -ForegroundColor DarkGray }
}

$claveTelefono = 'Seed:PlatformOwner:Phone'
$claveAssistant = 'Assistant:Platform:ApiKey'
if ($cargadas -contains $claveAssistant) {
    Write-Host "[ok]       $claveAssistant" -ForegroundColor Green
}
else {
    Write-Host "[opcional] $claveAssistant  -> Clave Anthropic de arranque; sin ella el chat queda pendiente de configuración" -ForegroundColor DarkGray
}
if ($cargadas -contains $claveTelefono) {
    Write-Host "[ok]       $claveTelefono" -ForegroundColor Green
}
else {
    Write-Host "[opcional] $claveTelefono  -> Teléfono E.164 del operador; se usará recién en la E8" -ForegroundColor DarkGray
}
