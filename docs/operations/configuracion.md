# Configuración: Google, Gmail y WhatsApp

> **Objetivo:** pegar las credenciales y que funcione. El multitenant usa **las mismas claves de configuración que ArquitecturaBase**, así que las credenciales que ya usás allá sirven tal cual. Regla de siempre: **lo secreto va en user-secrets** (o en variables de entorno en producción) y **nunca en el repo**; el resto va en `appsettings`.

## 1. Qué se usa

| Servicio | Para qué | Etapa |
|---|---|---|
| **Google OAuth** | ingresar y **registrarse** con Google (B2C), y vincular Google a una cuenta | E3 |
| **Gmail por SMTP** | todos los correos: códigos, enlaces, invitaciones. En desarrollo también se envían de verdad | E3 |
| **WhatsApp Cloud API (Meta)** | códigos de ingreso, registro e invitaciones por WhatsApp, y el bot de ingreso por webhook | E8 |

## 2. Los secretos (user-secrets de `src/ArquitecturaBaseMultitenant.Api`)

| Clave | Qué es | De dónde sale |
|---|---|---|
| `Authentication:Google:ClientSecret` | secreto del cliente OAuth | Google Cloud Console › Credenciales (el mismo cliente de ArquitecturaBase) |
| `Email:Smtp:Password` | contraseña de **aplicación** de Gmail (no la de la cuenta) | https://myaccount.google.com/apppasswords |
| `WhatsApp:AccessToken` | token del usuario del sistema | Meta Business › Usuarios del sistema (`whatsapp_business_messaging`, `whatsapp_business_management`) |
| `WhatsApp:AppSecret` | con lo que Meta firma cada webhook | Meta for Developers › la app › Configuración › Básica |
| `WhatsApp:VerifyToken` | la palabra de verificación del webhook | la misma que cargaste en Meta |

### Tres formas de cargarlos

**A. Copiarlos de ArquitecturaBase (esta máquina, cero pegado).** Los cinco ya están en los user-secrets de ArquitecturaBase. El script los copia sin mostrarlos:
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

## 3. Lo que no es secreto (`appsettings.Development.json` de la Api, en el repo)

Mismos valores que `../ArquitecturaBase/src/ArquitecturaBase.Api/appsettings.Development.json` y `appsettings.json`. Se copian en la Etapa 3 (Google y Gmail) y en la Etapa 8 (WhatsApp).

| Clave | Valor |
|---|---|
| `Authentication:Google:ClientId` | el mismo `ClientId` de ArquitecturaBase |
| `Authentication:Issuer` | `https://localhost:5174/` (el origen del front del multitenant) |
| `Email:Delivery` | `Smtp` (o `PickupDirectory` para escribir `.eml` sin enviar) |
| `Email:Smtp:Host` / `Port` | `smtp.gmail.com` / `587` (STARTTLS) |
| `Email:Smtp:UserName` / `FromAddress` | tu cuenta de Gmail, la misma de ArquitecturaBase |
| `Email:Smtp:FromName` | el nombre del producto |
| `WhatsApp:PhoneNumberId` | el mismo de ArquitecturaBase. **Es el interruptor**: sin él, WhatsApp queda apagado y la app arranca igual |
| `WhatsApp:BusinessAccountId`, `DisplayPhoneNumber`, `SendArgentineMobilesWithoutNine` | los mismos de ArquitecturaBase (el último, solo con el número de prueba) |
| `WhatsApp:GraphApiVersion`, `Templates:LoginCode`, `Templates:Invitation`, `AllowedCountries`, `DailyAuthCodeLimit`, `MessageRetentionDays` | los mismos de `appsettings.json` de ArquitecturaBase (`v25.0`, `codigo_ingreso`, `invitacion_acceso`, `["AR"]`, `100`, `90`) |

## 4. Lo que tenés que hacer vos, una sola vez, fuera del código

**Google Cloud Console** › el mismo cliente OAuth › URIs de redireccionamiento autorizados. Agregar las del multitenant (las de ArquitecturaBase se quedan):
- `https://localhost:5174/signin-google` (a través del front)
- `https://localhost:7280/signin-google` (la Api directa)
- en producción, `https://<dominio>/signin-google`

**Meta (webhook).** Una app de Meta tiene **una sola** URL de webhook. Hay dos caminos:
- **Mientras se desarrolla:** cuando pruebes el multitenant, cambiá la URL de devolución de llamada a `https://<túnel del multitenant>/webhooks/whatsapp`, con la misma palabra de verificación. Al volver a ArquitecturaBase, la volvés a cambiar. Como la URL del túnel de cada AppHost es fija, siempre son las mismas dos direcciones.
- **Para producción:** una app y un número propios del multitenant, con sus plantillas (`codigo_ingreso`, `invitacion_acceso`, en es y en) cargadas otra vez en esa cuenta.

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
| `Authentication:Google:ClientId` sin `ClientSecret` | la Api no arranca. Sin `ClientId`, el botón de Google no aparece (`GET /api/auth/methods`) |
| `WhatsApp:PhoneNumberId` sin `AccessToken` | la Api no arranca |
| `AppSecret` sin `VerifyToken`, o al revés | la Api no arranca. Sin ninguno, el webhook queda apagado, con un Warning, y el envío funciona igual |
| Sin `WhatsApp:PhoneNumberId` | WhatsApp apagado: el login y el registro muestran solo correo y Google |

Lo verifican `SmtpOptionsValidatorTests`, `GoogleOptionsTests`, `WhatsAppOptionsValidatorTests` y `LoginMethodsTests`.

## 6. Producción

Las mismas claves, como variables de entorno con doble guion bajo (`Email__Smtp__Password`, `WhatsApp__AccessToken`…), en el gestor de secretos del proveedor. Nunca en `appsettings.Production.json`. La lista completa de lo obligatorio fuera de Development está en `runbook.md` (Etapa 10).
