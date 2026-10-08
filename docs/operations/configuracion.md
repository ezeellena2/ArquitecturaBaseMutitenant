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
| `Seed:PlatformOwner:Email` | correo del operador inicial; obligatorio en Development y Production mientras no exista un operador | Buzón controlado por quien administra la plataforma |
| `Seed:PlatformOwner:FirstName` | nombre opcional del operador inicial; se aplica sólo al crear la cuenta | Quien administra la plataforma |
| `Seed:PlatformOwner:LastName` | apellido opcional del operador inicial; se aplica sólo al crear la cuenta | Quien administra la plataforma |
| `Seed:PlatformOwner:DisplayName` | compatibilidad anterior: primera palabra como nombre y resto como apellido, sólo cuando faltan ambas claves nuevas | Quien administra la plataforma |
| `Seed:PlatformOwner:Phone` | móvil internacional opcional, verificado por el seed exclusivamente para el operador configurado; nunca verifica una cuenta común | Número controlado por quien administra la plataforma |
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

El importador de ArquitecturaBase no conoce al operador ni al certificado Data Protection de esta plataforma: cargá `Seed:PlatformOwner:Email`, los nombres opcionales `Seed:PlatformOwner:FirstName` y `Seed:PlatformOwner:LastName` y, si ya lo tenés, `Seed:PlatformOwner:Phone` por separado en los user-secrets del proyecto Api. En Production usá el gestor de secretos (`Seed__PlatformOwner__Email`, `Seed__PlatformOwner__FirstName`, `Seed__PlatformOwner__LastName`, `Seed__PlatformOwner__Phone`). La clave anterior `Seed:PlatformOwner:DisplayName` sigue siendo compatible sólo si no se configuró ninguna de las dos partes nuevas; una clave nueva vacía cuenta como configurada. Cargá también `DataProtection:Certificate:Base64` por separado; `DataProtection:Certificate:Password` es opcional. El seed de Development y Production crea exactamente los mismos datos: referencias, documentos legales v1, ajustes de plataforma, cliente web y una identidad global de operador sin espacio Personal ni contraseña. El correo es su método principal verificado; ingresa exclusivamente por Plataforma, probando el código del método elegido y disponible. Los nombres se aplican sólo al crear la cuenta; el seed no reescribe una cuenta existente en cada arranque. El Phone opcional crea o verifica un método secundario exclusivamente en la cuenta del operador configurado y conserva Email como principal. El número se normaliza como móvil internacional; un número ya verificado por otra cuenta detiene el seed. No puede usarse para verificar un Phone de una cuenta común. El método puede sembrarse con WhatsApp apagado: solo sirve para ingresar cuando existe un canal disponible. Si ya existe el método de correo verificado y propio, el seed marca esa cuenta como operadora. Si ya hay un operador, los siguientes arranques conservan sus nombres y métodos existentes; solo aseguran el Phone opcional de ese operador cuando se configura. En Development y Production, si aún no hay operador, faltar `Seed:PlatformOwner:Email` detiene el arranque. Ninguno de los dos ambientes siembra personas u organizaciones de ejemplo.

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
| `WhatsApp:Templates:Invitation` y las demás `WhatsApp:Templates:<Nombre>` | **no se copian** de ArquitecturaBase: Invitation es invitacion_organizacion. El módulo configura **16 nombres es/en** (Authentication + 15 Utility), incluidas recuperación/baja por Plataforma y organización. Configurar una clave no acredita aprobación en Meta. Dos «en uso» quedan derogadas, y no hay OTP de confirmación de cambios en Mi cuenta. Claves/paquete según [plantillas](whatsapp-plantillas.md) y ADR 0049. |

`WhatsApp:AllowedCountries` reemplaza la lista predeterminada; no se suma a ella. Configurar solo `US` no conserva `AR`. La política se aplica al registro y a los métodos nuevos o pendientes, también al reenviar y verificar; un Phone ya verificado conserva ingreso y avisos aunque su país deje de admitirse. El país real se obtiene del E.164, no del campo `country` recibido.

## 4. Lo que tenés que hacer vos, una sola vez, fuera del código

**Google Cloud Console** › el mismo cliente OAuth › URIs de redireccionamiento autorizados. Agregar las del multitenant (las de ArquitecturaBase se quedan):
- `https://localhost:5174/signin-google` (a través del front)
- `https://localhost:7280/signin-google` (la Api directa)
- en producción, `https://<dominio>/signin-google`

Desde la Etapa 7, el dominio principal de desarrollo es `https://localtest.me:5174`. Sumá **`https://localtest.me:5174/signin-google`** a las URI de redirección autorizadas del cliente de Google en su consola. Esa configuración la hace el usuario; hasta que esté registrada, falla el ingreso con Google en este host, pero el código por correo sigue funcionando.

El cliente OIDC público `web` usa `https://localtest.me:5174/auth/callback` para volver al front después de `/connect/authorize` y `https://localtest.me:5174/silent-renew.html` para renovar en silencio. Esas URI se registran en `Authentication:Clients:Web:RedirectUris`; son distintas de `/signin-google`, que pertenece al callback de Google. AppHost entrega el mismo issuer fijo a la API y al front (`VITE_AUTH_ISSUER`). El front conserva el origen de cada pestaña para callback, canje, userinfo y revocación; `authorize` y `logout` navegan al dominio principal. Los subdominios publicados y activos se validan individualmente, sin registrar un comodín OIDC.

La SPA y la API comparten el origen de cada host: `https://<slug>.localtest.me:5174` conserva el `Host` hasta la API y usa `/api` y `/connect`, sin CORS. El certificado de desarrollo de .NET cubre `localhost`, **no `localtest.me` ni sus subdominios**: el navegador puede mostrar una advertencia. Para uso local sin esa advertencia, configurá un certificado de desarrollo que cubra `localtest.me` y `*.localtest.me` y sea confiable en esta máquina. Esto no cambia certificados ni configuración de producción.

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
| SMTP con `Email:Smtp:Security` distinto de `StartTls` o `SslOnConnect` | la Api no arranca y nombra `Email:Smtp:Security` |
| Development o Production sin operador y sin `Seed:PlatformOwner:Email` | la Api no arranca y nombra la clave que falta |
| Fuera de Development y Testing sin `DataProtection:Certificate:Base64`, o con PFX inválido o sin clave privada | la Api no arranca y nombra la clave del certificado |
| `Authentication:Google:ClientId` sin `ClientSecret` | la Api no arranca. Sin `ClientId`, el botón de Google no aparece (`GET /api/auth/methods`) |
| `WhatsApp:PhoneNumberId` sin `AccessToken` | la Api no arranca |
| WhatsApp encendido con `Authentication:LoginCode:LifetimeMinutes` distinto de `10` | la Api no arranca: debe coincidir con los diez minutos de la plantilla Authentication aprobada. Con el canal apagado no se aplica esta restricción |
| `AppSecret` sin `VerifyToken`, o al revés | la Api no arranca. Sin ninguno, el webhook queda apagado, con un Warning, y el envío funciona igual |
| Sin `WhatsApp:PhoneNumberId` | WhatsApp apagado: el login y el registro muestran solo correo y Google |

Lo verifican `StartupConfigurationTests`, `EmailDeliveryTests`, `ProductionSeedTests`, `WhatsAppStartupTests` y `WhatsAppTemplateTests`.

El transporte WhatsApp hace un único POST por intento del outbox durable: su cliente excluye los handlers de resiliencia global de ServiceDefaults y los loggers HTTP. Los reintentos corresponden al outbox. Una respuesta aceptada marca Sent; delivered/read se comprueban con eventos del webhook. La cuota técnica de códigos se reserva por número de plataforma y día UTC dentro de la transacción del emisor; borrar una cuenta no devuelve ese cupo.

## 6. Producción

Las mismas claves, como variables de entorno con doble guion bajo (`Email__Smtp__Password`, `DataProtection__Certificate__Base64`, `WhatsApp__AccessToken`…), en el gestor de secretos del proveedor. Nunca en `appsettings.Production.json`. `DataProtection:Certificate:Base64` es un PFX RSA distinto de los certificados OIDC: el proceso necesita su clave privada para leer el anillo persistido en `platform.DataProtectionKeys`; conservá el mismo certificado en todas las réplicas y en los reinicios. Perderlo impide leer cookies y payloads cifrados con esas claves. `ProtectKeysWithCertificate` cifra las claves **nuevas**; una instalación que ya haya escrito claves sin cifrar necesita tratar ese anillo antes de exponer sus backups. La lista completa de lo obligatorio fuera de Development estará en `runbook.md` (E10).

## Preparación del asistente (E8b, todavía planificada)

El [plan E8b T03](../plans/2026-10-08-etapa-8b-asistente.md#t03--configuración-modelos-y-claves) define clave inicial por user-secrets/entorno, luego edición write-only cifrada en la base. Estas opciones y scripts de carga todavía se crean en E8b; no presentar una clave/nombre de configuración como instalado hoy ni imprimir secretos. FakeChatClient solo Testing/E2E aislado, rechazado fuera de esos entornos. Prueba manual con clave real al cerrar; correo de soporte de Config-Plat y SMTP/outbox existentes para alertas de costo. No plantillas Meta para el asistente ni cambio de URL del túnel/webhook E8.
