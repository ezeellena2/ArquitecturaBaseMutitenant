# Configuración: Google, Gmail y WhatsApp

> **Objetivo:** pegar las credenciales y que funcione. El multitenant conserva las claves de configuración compartidas con ArquitecturaBase y suma un certificado propio para cifrar Data Protection en Production. Regla de siempre: **lo secreto va en user-secrets** (o en variables de entorno en producción) y **nunca en el repo**; el resto va en `appsettings`.

## 1. Qué se usa

| Servicio | Para qué | Etapa |
|---|---|---|
| **Google OAuth** | ingresar y **registrarse** con Google (B2C), y vincular Google a una cuenta | E3 |
| **Gmail por SMTP** | todos los correos: códigos, enlaces, invitaciones. En desarrollo también se envían de verdad | E3 |
| **WhatsApp Cloud API (Meta)** | códigos de ingreso, registro e invitaciones por WhatsApp, los avisos de la cuenta ([whatsapp-plantillas.md](whatsapp-plantillas.md)) y el bot de ingreso por webhook | E8 |

## 2. Los secretos (user-secrets de `src/ArquitecturaBaseMultitenant.Api`)

| Clave | Qué es | De dónde sale |
|---|---|---|
| `Authentication:Google:ClientSecret` | secreto del cliente OAuth | Google Cloud Console › Credenciales (el mismo cliente de ArquitecturaBase) |
| `Authentication:LoginCode:HashKey` | clave HMAC de los códigos de ingreso y registro | `appsettings.Development.json` de ArquitecturaBase; el importador la traslada a user-secrets sin mostrarla |
| `Email:Smtp:Password` | contraseña de **aplicación** de Gmail (no la de la cuenta) | https://myaccount.google.com/apppasswords |
| `DataProtection:Certificate:Base64` | PFX RSA con clave privada, codificado en base64, que cifra en reposo las claves de Data Protection fuera de Development y Testing | Certificado propio de la instalación; guardalo separado de la base y sus backups |
| `DataProtection:Certificate:Password` | contraseña del PFX, si tiene una | La definida al exportar ese PFX |
| `Seed:PlatformOwner:Email` | correo del operador inicial; obligatorio en Production mientras no exista un operador | Buzón controlado por quien administra la plataforma |
| `Seed:PlatformOwner:DisplayName` | nombre visible opcional del operador inicial | Quien administra la plataforma |
| `Seed:Development:AnaEmail` | buzón para ingresar como Ana en la Empresa A del seed local | Tu propio buzón para el recorrido manual; sólo Development |
| `WhatsApp:AccessToken` | token del usuario del sistema | Meta Business › Usuarios del sistema (`whatsapp_business_messaging`, `whatsapp_business_management`) |
| `WhatsApp:AppSecret` | con lo que Meta firma cada webhook | Meta for Developers › la app › Configuración › Básica |
| `WhatsApp:VerifyToken` | la palabra de verificación del webhook | la misma que cargaste en Meta |

### Tres formas de cargarlos

**A. Copiarlos de ArquitecturaBase (esta máquina, cero pegado).** Cinco están en los user-secrets de ArquitecturaBase y la clave HMAC en su configuración de desarrollo. El script copia los seis a user-secrets del multitenant sin mostrarlos:
```powershell
./scripts/secretos/importar-desde-arquitecturabase.ps1
```

**B. Desde tu archivo de credenciales.** Copiá [`secretos.plantilla.json`](secretos.plantilla.json) a una carpeta **fuera del repo** (por ejemplo `%USERPROFILE%\secrets\ArquitecturaBaseMultitenant\secretos.json`), pegá los valores y corré:
```powershell
./scripts/secretos/cargar-desde-archivo.ps1 -Archivo "$env:USERPROFILE\secrets\ArquitecturaBaseMultitenant\secretos.json"
```
El script se niega si el archivo está dentro del repo.

**C. De a uno, sin que se vea el valor** (como en ArquitecturaBase):
```powershell
$s = Read-Host "Email:Smtp:Password" -AsSecureString
dotnet user-secrets set "Email:Smtp:Password" (New-Object System.Net.NetworkCredential('', $s)).Password --project src/ArquitecturaBaseMultitenant.Api | Out-Null
Remove-Variable s
```

**Verificar** qué está cargado, sin mostrar valores:
```powershell
./scripts/secretos/verificar.ps1
```
Para comprobar lo obligatorio en Production (incluido el certificado de Data Protection):
```powershell
./scripts/secretos/verificar.ps1 -Ambiente Production
```

El importador de ArquitecturaBase no conoce al operador ni al certificado Data Protection de esta plataforma: cargá `Seed:PlatformOwner:Email` y `DataProtection:Certificate:Base64` por separado en user-secrets o en el gestor de secretos de Production (`Seed__PlatformOwner__Email`, `DataProtection__Certificate__Base64`). `Seed:PlatformOwner:DisplayName` y `DataProtection:Certificate:Password` son opcionales. El seed crea una identidad global sin espacio Personal ni contraseña, con el correo como método principal verificado; cada ingreso exige el código enviado a ese buzón. Si ya existe ese método de correo, marca su cuenta como operadora. Si ya hay un operador, los siguientes arranques no exigen la clave ni alteran esa cuenta. En Development, sin la clave se omite el operador inicial; en Production, si aún no hay operador, falta de clave detiene el arranque.

Para el recorrido de Ana, cargá `Seed:Development:AnaEmail` en user-secrets **antes del primer arranque de Development**. Si falta, el ejemplo usa `ana@example.test`, útil sólo con `Email:Delivery=PickupDirectory`. El seed no cambia métodos de ingreso en reinicios: para cambiar el buzón de Ana antes de la gestión de métodos de la 3b, recreá la base local de desarrollo. No cambies el correo directamente en la tabla.

## 3. Lo que no es secreto (`appsettings.Development.json` de la Api, en el repo)

Mismos valores que `../ArquitecturaBase/src/ArquitecturaBase.Api/appsettings.Development.json` y `appsettings.json`, salvo las plantillas de WhatsApp nuevas del multitenant (ver la tabla). Se copian en la Etapa 3 (Google y Gmail) y en la Etapa 8 (WhatsApp).

| Clave | Valor |
|---|---|
| `Authentication:Google:ClientId` | el mismo `ClientId` de ArquitecturaBase |
| `Authentication:Issuer` | `https://localhost:5174/` (el origen del front del multitenant) |
| `Email:Delivery` | `Smtp`; `PickupDirectory` escribe `.eml` sin enviar solo en Development o Testing |
| `Email:Smtp:Host` / `Port` | `smtp.gmail.com` / `587` (STARTTLS) |
| `Email:Smtp:UserName` / `FromAddress` | tu cuenta de Gmail, la misma de ArquitecturaBase |
| `Email:Smtp:FromName` | el nombre del producto |
| `WhatsApp:PhoneNumberId` | el mismo de ArquitecturaBase. **Es el interruptor**: sin él, WhatsApp queda apagado y la app arranca igual |
| `WhatsApp:BusinessAccountId`, `DisplayPhoneNumber`, `SendArgentineMobilesWithoutNine` | los mismos de ArquitecturaBase (el último, solo con el número de prueba) |
| `WhatsApp:GraphApiVersion`, `Templates:LoginCode`, `AllowedCountries`, `DailyAuthCodeLimit`, `MessageRetentionDays` | los mismos de `appsettings.json` de ArquitecturaBase (`v25.0`, `codigo_ingreso`, `["AR"]`, `100`, `90`) |
| `WhatsApp:Templates:Invitation` y las demás `WhatsApp:Templates:<Nombre>` | **no se copian** de ArquitecturaBase: `Invitation` es `invitacion_organizacion`, que reemplaza a `invitacion_acceso` (no nombra la organización) y se crea en Meta en la Etapa 8, igual que los avisos de la cuenta. Cada clave se agrega al crear su plantilla: [whatsapp-plantillas.md](whatsapp-plantillas.md) |

## 4. Lo que tenés que hacer vos, una sola vez, fuera del código

**Google Cloud Console** › el mismo cliente OAuth › URIs de redireccionamiento autorizados. Agregar las del multitenant (las de ArquitecturaBase se quedan):
- `https://localhost:5174/signin-google` (a través del front)
- `https://localhost:7280/signin-google` (la Api directa)
- en producción, `https://<dominio>/signin-google`

El cliente OIDC público `web` usa `https://localhost:5174/auth/callback` para volver al front después de `/connect/authorize`. Esa URI se registra en `Authentication:Clients:Web:RedirectUris`; es distinta de `/signin-google`, que pertenece al callback de Google. El front la fija en `src/auth/authConfig.ts` y la ruta está en `src/app/routes.tsx`.

**Meta (webhook).** Una app de Meta tiene **una sola** URL de webhook. Hay dos caminos:
- **Mientras se desarrolla:** cuando pruebes el multitenant, cambiá la URL de devolución de llamada a `https://<túnel del multitenant>/webhooks/whatsapp`, con la misma palabra de verificación. Al volver a ArquitecturaBase, la volvés a cambiar. Como la URL del túnel de cada AppHost es fija, siempre son las mismas dos direcciones.
- **Para producción:** una app y un número propios del multitenant, con sus plantillas cargadas otra vez en esa cuenta. Cuáles son, cuándo se crean y cómo: [whatsapp-plantillas.md](whatsapp-plantillas.md).

El túnel se prende igual que en ArquitecturaBase:
```powershell
dotnet user-secrets set "DevTunnel:Enabled" "true" --project src/ArquitecturaBaseMultitenant.AppHost
```

**Gmail:** la misma contraseña de aplicación sirve para los dos proyectos. El límite de unos 500 correos por día es **compartido**.

## 5. Qué pasa si falta algo (fallo claro, nunca a medias)

Las opciones se validan **al arrancar**, como en ArquitecturaBase:

| Situación | Resultado |
|---|---|
| `Email:Delivery=Smtp` sin `Email:Smtp:Password` | la Api no arranca y nombra la clave que falta |
| Fuera de Development y Testing con `Email:Delivery=PickupDirectory` | la Api no arranca y nombra `Email:Delivery` |
| Production sin operador y sin `Seed:PlatformOwner:Email` | la Api no arranca y nombra la clave que falta |
| Fuera de Development y Testing sin `DataProtection:Certificate:Base64`, o con PFX inválido o sin clave privada | la Api no arranca y nombra la clave del certificado |
| `Authentication:Google:ClientId` sin `ClientSecret` | la Api no arranca. Sin `ClientId`, el botón de Google no aparece (`GET /api/auth/methods`) |
| `WhatsApp:PhoneNumberId` sin `AccessToken` | la Api no arranca |
| `AppSecret` sin `VerifyToken`, o al revés | la Api no arranca. Sin ninguno, el webhook queda apagado, con un Warning, y el envío funciona igual |
| Sin `WhatsApp:PhoneNumberId` | WhatsApp apagado: el login y el registro muestran solo correo y Google |

Lo verifican `StartupConfigurationTests`, `EmailDeliveryTests` y `ProductionSeedTests` (WhatsApp se verifica en E8).

## 6. Producción

Las mismas claves, como variables de entorno con doble guion bajo (`Email__Smtp__Password`, `DataProtection__Certificate__Base64`, `WhatsApp__AccessToken`…), en el gestor de secretos del proveedor. Nunca en `appsettings.Production.json`. `DataProtection:Certificate:Base64` es un PFX RSA distinto de los certificados OIDC: el proceso necesita su clave privada para leer el anillo persistido en `platform.DataProtectionKeys`; conservá el mismo certificado en todas las réplicas y en los reinicios. Perderlo impide leer cookies y payloads cifrados con esas claves. `ProtectKeysWithCertificate` cifra las claves **nuevas**; una instalación que ya haya escrito claves sin cifrar necesita tratar ese anillo antes de exponer sus backups. La lista completa de lo obligatorio fuera de Development está en `runbook.md` (Etapa 10).
