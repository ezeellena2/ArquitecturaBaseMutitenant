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

**Los datos de la instalación** no son secretos, pero son de cada instalación: van en los mismos user-secrets (en Production, en variables de entorno) y nunca en un json versionado. `StartupConfigurationTests` falla si vuelven a `appsettings.json` o `appsettings.Development.json`.

| Clave | Qué es | De dónde sale |
|---|---|---|
| `Authentication:Google:ClientId` | id del cliente OAuth; sin él, el botón de Google no aparece | Google Cloud Console › Credenciales (el mismo cliente de ArquitecturaBase) |
| `Email:Smtp:UserName` / `FromAddress` | la cuenta de Gmail que manda los correos y su remitente | tu cuenta de Gmail, la misma de ArquitecturaBase |
| `WhatsApp:PhoneNumberId` | el número de Plataforma. Desde la E11 **solo sirve para el arranque**: si no hay número de Plataforma, se crea uno con este valor y `WhatsApp:AccessToken`; mientras nadie lo reemplace desde Plataforma › WhatsApp › Número, sigue a la configuración. Sin número conectado, WhatsApp queda apagado y la app arranca igual | Meta for Developers › la app › WhatsApp › Configuración de la API |
| `WhatsApp:AppId`, `WhatsApp:EmbeddedSignupConfigId` | la app de Meta del alta embebida y la configuración de Facebook Login for Business (E11, §7). Sin las dos, las pantallas de números no ofrecen conectar. `AppSecret` canjea el código del alta en el servidor | Meta for Developers › la app › Configuración › Básica, y Facebook Login for Business › Configuraciones |

### Tres formas de cargarlos

**A. Copiarlos de ArquitecturaBase (esta máquina, cero pegado).** Cinco están en los user-secrets de ArquitecturaBase y la clave HMAC en su configuración de desarrollo. El script copia los seis a user-secrets del multitenant sin mostrarlos; los datos de la instalación no los trae, y se cargan con B o C:
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
| `Authentication:Issuer` | `https://localhost:5174/` (el origen del front del multitenant) |
| `Email:Delivery` | `Smtp`; `PickupDirectory` escribe `.eml` sin enviar solo en Development o Testing |
| `Email:Smtp:Host` / `Port` | `smtp.gmail.com` / `587` (STARTTLS) |
| `Email:Smtp:FromName` | el remitente de arranque: el respaldo de «Nombre y correos» de Plataforma › Configuración mientras no tenga uno guardado (decisión 273); el canal lo lee antes de cada envío |
| `Email:AppName` | el nombre de arranque: el respaldo de «Nombre y correos» para los correos y las respuestas del bot (273), y el emisor de la app autenticadora de los operadores (ver el inventario) |
| `Email:LogoUrl` | opcional: el logo de arranque de los correos, que vale mientras «Nombre y correos» no tenga uno guardado; si Plataforma lo quita, los correos salen sin logo |
| `WhatsApp:BusinessAccountId`, `DisplayPhoneNumber`, `SendArgentineMobilesWithoutNine` | los mismos de ArquitecturaBase (el último, solo con el número de prueba) |
| `WhatsApp:GraphApiVersion`, `Templates:LoginCode`, `AllowedCountries`, `DailyAuthCodeLimit` | `v26.0` desde la E11 (Embedded Signup v4); los demás, los de `appsettings.json` de ArquitecturaBase (`codigo_ingreso`, `["AR"]`, `100`) |
| `WhatsApp:Templates:Invitation` y las demás `WhatsApp:Templates:<Nombre>` | **no se copian** de ArquitecturaBase: Invitation es invitacion_organizacion. El módulo configura **16 nombres es/en** (Authentication + 15 Utility), incluidas recuperación/baja por Plataforma y organización. Configurar una clave no acredita aprobación en Meta. Dos «en uso» quedan derogadas, y no hay OTP de confirmación de cambios en Mi cuenta. Claves/paquete según [plantillas](whatsapp-plantillas.md) y ADR 0049. |

Los países que reciben códigos por WhatsApp son los de «Países que reciben códigos» en la configuración de WhatsApp de Plataforma (`/api/platform/whatsapp/settings`, decisión 270 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md)), leídos de la base en cada pedido. `WhatsApp:AllowedCountries` queda solo de respaldo mientras Plataforma no guarde los suyos, hasta el despliegue siguiente ([0060](../decisions/0060-dos-versiones-conviven.md)); el primer guardado parte de esa lista. La lista reemplaza la predeterminada; no se suma a ella: guardar solo `US` no conserva `AR`. La política se aplica al registro y a los métodos nuevos o pendientes, también al reenviar y verificar; un Phone ya verificado conserva ingreso y avisos aunque su país deje de admitirse. El país real se obtiene del E.164, no del campo `country` recibido.

El bot de ingreso ya no emite enlaces (base de la 12, decisión 179): «Entrar a mi cuenta» abre el ingreso, donde el código se pide desde la web, y no quedan opciones `Authentication:LoginLink`. Un reply libre pendiente vence al cerrar la ventana de 24 horas, comprobada también antes de enviar a Cloud API.

Las opciones de seguridad (E9) están en `appsettings.json` con los valores de siempre y se validan al arrancar:
- `RateLimiting:<Perfil>:PermitLimit` y `WindowSeconds` son las cuotas de arranque de los doce perfiles ([cuotas](../rules/cuotas.md)). El seed las guarda solo al crear la configuración de Plataforma; después se editan desde Plataforma.
- `Authentication:Lifetimes`: código de autorización 5 minutos, access token 15 minutos, refresh token y cookie 30 días.
- `Idempotency`: vigencia de 24 horas y limpieza cada 60 minutos.
- `AccountDeletion`: lote de 100 y un ciclo cada 60 minutos.

La IP de las cuotas se guarda como HMAC con `Authentication:LoginCode:HashKey` y un dominio propio: compartí esa clave entre réplicas.

Desde la E11 el webhook depende solo de `WhatsApp:AppSecret` y `WhatsApp:VerifyToken` (lo usan también los números de las organizaciones). El token de cada número conectado se guarda cifrado en `identity.WhatsAppChannels`; el de la configuración se copia ahí al arrancar.

La copia global de cada mensaje que entra (`identity.WhatsAppMessages`) vence a los **90 días fijos desde `ReceivedAtUtc`**: es técnica y además se purga al pasar a la organización. El contenido de los chats vence con el plazo de su dueño (decisión 272): el de la configuración de WhatsApp de cada organización, hasta el máximo que fija Plataforma, y el de la de Plataforma para su número; sin nada guardado, 90 días. El worker corre al arrancar y una vez por día, cada organización en su alcance con `TenantJobRunner` y la plataforma aparte, en lotes que toman solo filas libres (`SKIP LOCKED`), incluso sin `PhoneNumberId` ni secretos de Meta. Apagar el canal no desactiva esa limpieza. La exportación OpenAPI no inicia mantenimiento ni necesita base; retirar las fuentes del módulo sigue la receta de [quitar WhatsApp](../guides/quitar-whatsapp.md).

**Con el depurador de Visual Studio,** el proveedor de log Debug manda cada línea a la ventana Salida y frena todo el proceso: allá bajó de unos 690 a unos 11 pedidos por segundo. Por eso `Logging:Debug:LogLevel:Default` está en Warning en el `appsettings.Development.json` de la Api desde la base de la 12, y `BuildConfigurationTests` lo controla; la consola y la telemetría no cambian (traspaso 9).

**Redis en desarrollo tiene 5 segundos para conectar** (`Cache:ConnectTimeoutMilliseconds` en el `appsettings.Development.json` de la Api; en los demás ambientes queda el segundo de la [ADR 0052](../decisions/0052-cache-compartida-redis.md)). Aspire le pasa a la Api `localhost:<puerto>,ssl=true`, y Docker publica Redis solo en IPv4: en Windows el cliente prueba primero `::1` y pierde unos dos segundos antes de ir a `127.0.0.1`. Con un segundo no llegaba nunca, y la Api quedaba con la caché en Degraded, leyendo todo de la base (2026-11-18).

### Reglas de negocio en pantalla (270)

Un tope, un plazo, un cupo, una espera o un valor por defecto de una regla de negocio no va en `appsettings` ni fijo en el código: va en la configuración de su dueño, con su valor por defecto, su mínimo, su máximo, revisión y 409, y se lee de la base en cada uso (decisión 270 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md)). Cada organización ajusta lo suyo dentro del máximo que fija la plataforma, y lo que no es de ninguna organización lo fija la plataforma. Quedan acá solo los secretos, lo que fija un proveedor, lo técnico y lo de seguridad, costo o abuso (los cupos de códigos, de invitaciones y de pedidos). Sacar una clave de este archivo sigue la regla de las dos versiones ([0060](../decisions/0060-dos-versiones-conviven.md)): la versión nueva lee la base antes de que se borre la clave, y la clave se borra con la reconstrucción que ya la lee.

**Los topes de los módulos** ([0069](../decisions/0069-modulos-y-suscripciones.md)): un tope con máximo de la plataforma se declara en el aporte de su módulo y se lee por `ModuleLimitResolver`; la organización elige dentro de ese tope, y en cada uso vale el menor ([modulos-habilitados](../rules/modulos-habilitados.md), «Topes»).

**Lo que ya pasó a la configuración de su dueño** (B18 de la [segunda parte de la base](../plans/formularios/base-de-la-12-segunda-parte.md)):

| Valor | Dónde vive | Respaldo hasta el despliegue siguiente |
|---|---|---|
| Los plazos del ingreso: auditoría (12 meses), códigos (30 días) e invitaciones cerradas (30 días) (272) | Plataforma › Configuración, «Privacidad y retención» | ninguno: sin fila, los de defecto |
| El plazo de los chats de WhatsApp (90 días) y el máximo de las organizaciones (272) | la configuración de WhatsApp de Plataforma y la de cada organización | ninguno: sin fila, los de defecto |
| Los países que reciben códigos (270) | la configuración de WhatsApp de Plataforma | `WhatsApp:AllowedCountries` |
| El nombre de la app, el remitente y el logo de los correos y del bot (273) | Plataforma › Configuración, «Nombre y correos» | `Email:AppName`, `Email:Smtp:FromName` y `Email:LogoUrl` |
| Las conversaciones del Inicio | el `retentionDays` del asistente de cada organización | ninguno |
| Las cuotas de los doce perfiles | Plataforma › Configuración (E9) | `RateLimiting:<Perfil>`, solo para el seed |

**Lo que se queda en `appsettings` o en el código, con su motivo:**

| Valor | Clave o lugar | Motivo |
|---|---|---|
| Vigencia, largo, intentos, espera para reenviar y bloqueo de los códigos de ingreso | `Authentication:LoginCode` | seguridad: frenan la adivinación y el abuso, y el mismo valor vale para todas las organizaciones |
| Vigencia de una invitación (7 días) | `Authentication:Invitations:LifetimeDays` | seguridad: es lo que dura un enlace que da acceso a una organización |
| Vida del código de autorización, del token, del refresh y de la cookie | `Authentication:Lifetimes` | seguridad de la sesión |
| El emisor de la app autenticadora de los operadores | `Email:AppName` | queda guardado en cada autenticador al vincularlo: cambiarlo en pantalla dejaría el mismo factor con dos nombres |
| Topes diarios de correo y su reserva para invitaciones | `Email:Quotas` | costo y abuso ([0063](../decisions/0063-topes-de-correo-de-la-plataforma.md)) |
| Cupo diario de códigos por WhatsApp, su parte protegida y los de IP y cuenta | `WhatsApp:DailyAuthCodeLimit` y vecinas | costo y abuso: Meta cobra cada código |
| Los nombres de las plantillas de Meta | `WhatsApp:Templates` | los fija el proveedor al aprobarlas ([plantillas](whatsapp-plantillas.md)) |
| La versión de Graph, las esperas y los reintentos de la entrada y la salida | `WhatsApp:GraphApiVersion`, `InboundLeaseSeconds`, `InboundMaxAttempts`, `Messaging:Outbox` | técnico: cómo trabaja la cola, no qué decide el negocio |
| La copia global de la entrada de WhatsApp (90 días) | fijo en `WhatsAppMessageRetentionService` | técnico: se purga al pasar a la organización y no la ve nadie |
| Lote y ciclo de la baja de cuentas, vigencia y limpieza de la idempotencia | `AccountDeletion`, `Idempotency` | técnico |
| Los tiempos, la concurrencia y la cola del asistente, y el tamaño de los archivos | `Assistant` | técnico y de costo; el cupo de cada organización va en su configuración ([0064](../decisions/0064-reglas-de-toda-la-ia.md)) |

## 4. Lo que tenés que hacer vos, una sola vez, fuera del código

**Google Cloud Console** › el mismo cliente OAuth › URIs de redireccionamiento autorizados. Agregar las del multitenant (las de ArquitecturaBase se quedan):
- `https://localhost:5174/signin-google` (a través del front)
- `https://localhost:7280/signin-google` (la Api directa)
- en producción, `https://<dominio>/signin-google`

Desde la Etapa 7, el dominio principal de desarrollo es `https://localtest.me:5174`. Sumá **`https://localtest.me:5174/signin-google`** a las URI de redirección autorizadas del cliente de Google en su consola. Esa configuración la hace el usuario; hasta que esté registrada, falla el ingreso con Google en este host, pero el código por correo sigue funcionando.

El cliente OIDC público `web` usa `https://localtest.me:5174/auth/callback` para volver al front después de `/connect/authorize` y `https://localtest.me:5174/silent-renew.html` para renovar en silencio. Esas URI se registran en `Authentication:Clients:Web:RedirectUris`; son distintas de `/signin-google`, que pertenece al callback de Google. AppHost entrega el mismo issuer fijo a la API y al front (`VITE_AUTH_ISSUER`). El front conserva el origen de cada pestaña para callback, canje, userinfo y revocación; `authorize` y `logout` navegan al dominio principal. Los subdominios publicados y activos se validan individualmente, sin registrar un comodín OIDC.

La SPA y la API comparten el origen de cada host: `https://<slug>.localtest.me:5174` conserva el `Host` hasta la API y usa `/api` y `/connect`, sin CORS. El certificado de desarrollo de .NET cubre `localhost`, **no `localtest.me` ni sus subdominios**: el navegador puede mostrar una advertencia. Para uso local sin esa advertencia, configurá un certificado de desarrollo que cubra `localtest.me` y `*.localtest.me` y sea confiable en esta máquina. Esto no cambia certificados ni configuración de producción.

**Meta (webhook).** Una app de Meta tiene **una sola** URL de webhook. Hay dos caminos:
- **Mientras se desarrolla:** cada proyecto tiene su app de Meta, y cada app tiene fija la URL de su túnel. El multitenant usa «Plantilla Multitenant» (§7), con `https://<túnel del multitenant>/webhooks/whatsapp`; la dirección no va en el repo, como en ArquitecturaBase, porque es la de la Api en internet. No se cambia nada al pasar de un proyecto al otro. Las dos apps comparten el número de prueba, así que **se levanta un proyecto por vez**: con los dos arriba, a los dos les llega cada mensaje y contestan los dos bots. Con un AppHost apagado, su túnel responde 200 vacío, y Meta no reintenta. Si no llega un webhook, se mira primero la URL en la app de Meta, antes que el túnel o la Api: en ArquitecturaBase se perdieron casi dos semanas de mensajes porque apuntaba a otro sistema (O27, traspaso 16).
- **Para producción:** una app y un número propios del multitenant, con sus plantillas cargadas otra vez en esa cuenta. Cuáles son, cuándo se crean y cómo: [whatsapp-plantillas.md](whatsapp-plantillas.md).

El túnel se prende igual que en ArquitecturaBase. En esta máquina ya está prendido, con un id fijo (`DevTunnel:TunnelId`), y `aspire run` levanta todo: Postgres, Redis, la Api, el front y el túnel. Hacen falta Docker Desktop abierto y la sesión de `devtunnel user login`, que vence cada tantos días: si el túnel no arranca, `devtunnel user show` lo dice.
```powershell
dotnet user-secrets set "DevTunnel:Enabled" "true" --project src/ArquitecturaBaseMultitenant.AppHost
```

**El MCP de Meta.** `.mcp.json` (Claude) y `.codex/config.toml` (Codex) registran el servidor oficial [WhatsApp Business Tools](https://developers.facebook.com/documentation/mcp/whatsapp-business-tools-mcp), igual que en ArquitecturaBase. El repo guarda solo su dirección pública. Para usarlo, abrí en Claude `/mcp` › `whatsapp_business_tools` › **Authenticate** (en Codex, *Settings › MCP servers*) y entrá con una cuenta de Meta que administre el negocio y la app. La autorización queda en tu sesión, y ninguna clave pasa por el agente. Antes de cambiar números, plantillas o webhooks, el agente lista los negocios y te pide el visto bueno. Si Meta todavía no lo habilitó para tu cuenta, dice *Not yet available for your account*.

**Gmail:** la misma contraseña de aplicación sirve para los dos proyectos. El límite de unos 500 correos por día es **compartido**: los topes diarios de los dos proyectos se reparten ese cupo ([topes de correo](#topes-diarios-de-correo-adr-0063-y-256)).

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

La entrada usa `WhatsApp:ProcessInboundInBackground` (true), `InboundPollSeconds` (30), `InboundLeaseSeconds` (120), `InboundMaxAttempts` (8) y `RetryDelaySeconds` (6). El worker reclama filas persistidas y usa scopes nuevos; apagarlo deja la cola para otro worker. Sin consumer no consume intentos. El webhook valida HMAC y tamaño real antes de parsear y confirma recepción después del commit; la señal en memoria no guarda mensajes. `WhatsAppWebhookTests` y `WhatsAppInboundProcessorTests` prueban ese recorrido con Docker.

### Cuota diaria de códigos (E8 M1)

`WhatsApp:DailyAuthCodeLimit=100` es el único total diario. `WhatsApp:ProtectedAuthCodeFraction=0.2` protege capacidad para ingreso a Phone previamente verificado/utilizable; se redondea hacia arriba y los nuevos pueden usar total menos protección (80 por defecto). No limita los ingresos a 20: pueden usar todo el total si no hubo nuevos. `WhatsApp:DailyNewAuthCodeIpLimit=20` limita nuevos por IP/día UTC. Total y tope IP admiten 1–10000; fracción, 0–1. Total 1 con la fracción predeterminada sólo admite ingresos verificados. Los topes de ventana/destino/cooldown existentes se conservan. Todos los agotamientos diarios usan `WhatsApp.LoginCode.DailyLimit` con Retry-After hasta medianoche UTC.

La reserva se confirma únicamente con un enqueue real en la UoW; rollback o ausencia de envío no gastan, replay gasta una vez y fallar luego en Meta no devuelve cupo. El ledger IP usa la clave existente `Authentication:LoginCode:HashKey` con dominio HMAC separado: compartila entre réplicas y conservála estable durante el día UTC. Una rotación debe hacerse al cambiar de día para conservar los buckets diarios; no se genera una clave local por réplica. El mantenimiento instalado retira identificadores IP de días terminados, también con el canal apagado; conserva los contadores globales. La migración trata el gasto anterior desconocido como nuevo y lo descuenta de cada límite IP en ese día; no reconstruye IP históricas.

### Topes diarios de correo (ADR 0063 y 256)

Lo que sale por correo con un código o una invitación cuenta en 24 horas móviles, por horas (las 24 cerradas y la actual), en `identity.MessageQuotaBuckets`: contadores de la base, compartidos entre copias, con la clave en un HMAC ([ADR 0063](../decisions/0063-topes-de-correo-de-la-plataforma.md)). Las claves se validan al arrancar; los valores son los de la decisión 256 y, los demás, los de la 0063:

| Clave | Arranque | Qué limita |
|---|---|---|
| `Email:Quotas:DailyLimit` | 80 | todo lo que sale con un código o una invitación, por debajo del cupo de la cuenta. Al 80 %, al frenar los códigos y al llegar queda un aviso en el log |
| `Email:Quotas:DailyInvitationReserve` | 20 | la parte del tope que queda solo para invitaciones: los códigos frenan en 60. Menor que `DailyLimit` |
| `Email:Quotas:DailyCodeLimitPerAddress` | 50 | códigos pedidos desde una dirección (una IPv6, por su /64), hayan salido o no |
| `Email:Quotas:DailyCodeLimitPerAccount` | 10 | códigos que pide una cuenta desde Mi cuenta |
| `Email:Quotas:DailyInvitationLimitPerInviter` | 100 | invitaciones que manda una persona, sume las organizaciones que sume; también un operador |
| `Email:Quotas:DailyInvitationLimitPerOrganization` | 100 | invitaciones que manda una organización |
| `WhatsApp:DailyAccountCodeLimit` | 10 | códigos por WhatsApp que pide una cuenta desde Mi cuenta (`WhatsApp.LoginCode.AccountDailyLimit`) |

Al llegar, la ruta responde 429 `Messaging.Email.DailyLimit` con `retryAfter` y `Retry-After`, sin mandar ni guardar nada. Las invitaciones por WhatsApp no cuentan: tienen los límites de Meta. La invitación del Dueño al crear una organización desde Plataforma cuenta como cualquier otra. El E2E y el recorrido corren con topes altos (`TestMessageQuotas`), y `IdentityRetentionWorker` borra los contadores que ya salieron de su ventana.

**Con la cuenta compartida con ArquitecturaBase,** la suma de los topes globales de los dos proyectos queda por debajo del cupo, con lugar para los avisos: el multitenant usa 80 (256). Se separan las cuentas antes de producción ([runbook](runbook.md)).

### La cola de salida (ADR 0066, N5, N8 y O3)

`Messaging:Outbox:*` es técnico: `DeliveryTimeoutSeconds` (15) es la espera de un envío, `CompletionTimeoutSeconds` (10) la de anotar su resultado, `MaxAttempts` (5) el tope de intentos y `RetryBaseDelaySeconds` (30) la primera espera, que se duplica. Cada canal puede tener su espera y su tope en `Messaging:Outbox:Channels:<canal>:DeliveryTimeoutSeconds` y `MaxAttempts` (`email` o `whatsapp`, sin distinguir mayúsculas: `Channels__Email__MaxAttempts` también vale). La Api no arranca si la entrega más lenta y la confirmación no caben en el apagado del host (30 s por defecto): así apagarla no deja a medias un envío que ya salió.

Lo que no salió vuelve a pendiente con su espera; lo que no se sabe si salió (un corte o la espera vencida después de que el servidor aceptó al destinatario, una reserva que venció) queda «Sin confirmar» y no se repite; un código por correo que vence en la cola se descarta. El chequeo `email` queda en Degraded con un 535 hasta el próximo envío que sale, y el medidor `ArquitecturaBaseMultitenant.Messaging` cuenta enviados y descartados por canal. Lo prueban `SmtpClassificationTests`, `OutboxReservationTests` y `OutboxOptionsValidatorTests`. WhatsApp clasifica igual (ADR 0066, B7): el módulo deja su canal en `MaxAttempts` 3 (el envío y dos reintentos, a los 30 s y a los 60 s) si `Messaging:Outbox:Channels:whatsapp:MaxAttempts` no dice otra cosa, también cuando se configura solo la espera del canal; lo prueban `WhatsAppCloudClientTests` y `WhatsAppB7AdversarialTests`.

## 6. Producción

Las mismas claves, como variables de entorno con doble guion bajo (`Email__Smtp__Password`, `DataProtection__Certificate__Base64`, `WhatsApp__AccessToken`…), en el gestor de secretos del proveedor. Nunca en `appsettings.Production.json`. `DataProtection:Certificate:Base64` es un PFX RSA distinto de los certificados OIDC: el proceso necesita su clave privada para leer el anillo persistido en `platform.DataProtectionKeys`; conservá el mismo certificado en todas las réplicas y en los reinicios. Perderlo impide leer cookies y payloads cifrados con esas claves. `ProtectKeysWithCertificate` cifra las claves **nuevas**; una instalación que ya haya escrito claves sin cifrar necesita tratar ese anillo antes de exponer sus backups. La lista completa de lo obligatorio fuera de Development estará en `runbook.md` (E10).

## 7. Meta: la app y el alta embebida

La app de desarrollo es «Plantilla Multitenant» (su id va en `WhatsApp:AppId`, §2), **publicada**, en live mode con acceso estándar: sin publicar solo recibe los webhooks de prueba del panel, y con acceso estándar conectan números solo quienes tienen rol en la app. La producción sigue el [manual de operación](runbook.md).

- **Alta embebida:** caso de uso WhatsApp y Facebook Login for Business, con una configuración creada desde la plantilla de Embedded Signup. Su `config_id` va en `WhatsApp:EmbeddedSignupConfigId` (§2), y su acceso vence a los 60 días.
- **OAuth:** los seis interruptores, «Allowed domains» con `localtest.me` y «Valid OAuth redirect URIs» con `https://localtest.me:5174/`, porque la ventana la abre el front.
- **Webhook:** `https://<túnel>/webhooks/whatsapp`, con `messages`, `account_update`, `message_template_status_update`, `template_category_update`, `phone_number_name_update`, `history`, `smb_app_state_sync` y `smb_message_echoes`.
- **Para probar plantillas y difusiones:** un número real libre y un medio de pago en esa cuenta.

Datos de la documentación oficial de Meta, verificados el 2026-10-13:
- **Embedded Signup v4** (v2 y v3 se retiran el 15/10/2026): `extras` vacío y productos elegidos en la configuración de Facebook Login for Business. El evento `WA_EMBEDDED_SIGNUP` devuelve `FINISH` (con `phone_number_id` y `waba_id`), `CANCEL` (con `current_step`) o `ERROR` (con `error_id`); cerrar en la última pantalla no es cancelar. El `code` vive 30 segundos: se canjea en el servidor, y después van `POST /<WABA_ID>/subscribed_apps` y `POST /<PHONE_ID>/register` con un PIN de 6 dígitos.
- **Coexistencia:** `featureType: whatsapp_business_app_onboarding`, app WhatsApp Business 2.24.17 o más nueva, sin `/register`, 20 mensajes por segundo.
- **Precios desde el 01/10/2026,** por mensaje, según país y categoría. Argentina, en USD: marketing 0,0618; utility, authentication y service 0,0260. Cada número tiene 1.000 mensajes de servicio entregados gratis por mes, que no se acumulan y se reinician a las 00:00 de la zona de la cuenta. Sin medio de pago, Meta entrega los de servicio solo dentro de esos 1.000, y las plantillas siempre lo exigen (error 131042).
- **Medio de pago:** `GET /<WABA_ID>?fields=primary_funding_id,timezone_id`; sin `primary_funding_id` no hay. `health_status` no lo informa.
- **Alta de organizaciones:** 10 nuevas cada 7 días, y 200 con empresa verificada y App Review. Access Verification ya no se exige. Cada organización le paga a Meta con su tarjeta.
- **Graph API:** vigente la v26.0; la v25.0 vence el 29/07/2028.

**El agente de IA de Meta, apagado** (traspaso 9): WhatsApp Manager ofrece un agente que responde solo. No se prende en el número de una organización: con él como primer respondedor, lo que escribe el cliente llega por el campo `standby` del webhook, que la app no lee, ni en la atención ni en el asistente, y un mensaje de la app le saca la conversación al agente sin liberarla. Que convivan sería otro desarrollo.

## Asistente

La [configuración del asistente](../features/asistente.md) usa las claves y opciones siguientes. El asistente no usa plantillas de Meta ni cambia la URL del túnel o del webhook de WhatsApp.

### Claves y catálogo técnico

`Assistant:Platform:ApiKey` se carga en user-secrets o en `Assistant__Platform__ApiKey`; nunca en appsettings, repositorio ni logs. Es opcional: sin clave la aplicación inicia y la configuración se puede completar. Una clave guardada desde Plataforma queda cifrada con Data Protection y prevalece sobre el secreto de arranque. `keep` conserva la clave; `replace` valida una petición mínima antes de abrir la transacción y guarda con versión; `clear` persiste la eliminación e impide recuperar el secreto de arranque al reiniciar. Los GET muestran únicamente existencia y últimos cuatro caracteres. La clave propia de cada organización usa un propósito de cifrado independiente y exige un Dueño activo con método Email verificado; un correo de contacto de Google o un método Phone no alcanza.

Para desarrollo, desde la raíz del Back, reemplazá el marcador por tu clave de prueba y ejecutá localmente:

```powershell
dotnet user-secrets set "Assistant:Platform:ApiKey" "<tu-clave-de-Anthropic>" --project src/ArquitecturaBaseMultitenant.Api
```

Reiniciá la API después de cargarla. En producción, configurá el secreto `Assistant__Platform__ApiKey` en el entorno del proceso de la API, mediante el mecanismo de secretos del host. No agregues su valor a archivos versionados. También se puede cargar desde **Configuración del asistente de Plataforma**, con permiso `platform.assistant.configure`: «Reemplazar» valida la clave y la guarda cifrada. Esa elección persistida manda sobre user-secrets y la variable de entorno; «Quitar» también manda e impide que el secreto de arranque la reponga. Cambiar el secreto de arranque no reemplaza una elección persistida: usá Configuración de Plataforma para cambiarla.

La clave puede quedar ausente hasta que la cargues: no es una opción requerida en el arranque. La aplicación y las demás funciones siguen funcionando; el asistente muestra **Sin configurar** del tablero, con **Revisar configuración** sólo para quien tiene permiso. No se llama a Anthropic al iniciar ni se activa un proveedor falso automáticamente. Las suites y el E2E determinístico usan `FakeChatClient` sólo en su entorno aislado explícito y comprueban también el caso sin clave. Por pedido del usuario del 2026-10-10, la [prueba manual con Anthropic real](../features/asistente.md) queda pendiente del usuario hasta que obtenga y cargue la clave; los resultados con Fake no acreditan ese tramo.

Los precios iniciales y presupuestos usan USD. La fuente `scripts/datos-de-referencia/habilitados.json` habilita ARS y USD; ARS sigue siendo el default. Los modelos iniciales y sus precios normales por millón se incorporan una sola vez; el seed del asistente no pisa ediciones posteriores ni rechaza un recorrido sin cambios por una moneda histórica deshabilitada. Los cambios de modelo o política se leen nuevamente antes de admitir cada turno, sin fallback hacia otro modelo o financiamiento de Plataforma.

Según [ADR 0051](../decisions/0051-precision-tecnica-de-costos-ia.md), las tarifas por millón conservan hasta seis decimales: USD 0,125 sigue siendo 0,125. Los presupuestos ingresados se redondean a las unidades menores del catálogo con AwayFromZero (USD 0,125 → 0,13) antes de guardar. Se rechazan tarifas con más de seis decimales e importes fuera de la capacidad de almacenamiento; el presupuesto se valida nuevamente tras redondearlo, antes de validar una clave o abrir UOW.

El catálogo técnico se administra desde las rutas de Modelos de Plataforma y se guarda en la base junto con sus precios. Un ID nuevo con capacidades, límites, esfuerzos y tarifas completos queda disponible inmediatamente para OwnKey y, cuando se permite en la configuración global, PlatformKey. No requiere archivo externo, reinicio ni clave para leer o administrar el catálogo. `Assistant:ProviderReferenceFile` dejó de ser una opción: el JSON publicado sólo aporta los presets del bootstrap inicial y nunca sobrescribe una edición. Verificá las capacidades y tarifas con fuentes oficiales antes de guardarlas.

`technical` exige los cinco booleanos `supportsText`, `supportsTools`, `supportsImages`, `supportsPdf`, `supportsAdaptiveThinking`; esfuerzos únicos no vacíos (`low`, `medium`, `high`); límites positivos con salida no mayor que entrada; y las tres tarifas de caché USD positivas con hasta seis decimales. `longContext` es null o tiene un umbral positivo menor que la entrada y sus cinco tarifas completas. El servidor calcula `technicalRevision` con los datos y precios efectivos. Las filas heredadas desconocidas conservan `technical=null` y no son elegibles hasta completarlas; el upgrade completa sólo los cuatro IDs conocidos sin cambiar precios base, IDs ni configuración.

Para chat se exige modelo activo Anthropic con texto, herramientas, pensamiento adaptativo, esfuerzo elegido y capacidad mínima 51200/2048. Se pueden registrar modelos de menor capacidad coherente, pero no aparecen en los selectores de chat. Imágenes y PDF se verifican al admitir adjuntos. Los máximos del módulo siguen siendo 51200 de entrada, 2048 de salida y 64 para título, con cinco llamadas por turno incluido el título. Una edición no modifica precios ni revisiones de snapshots anteriores.

El fake y sus métricas viven exclusivamente en el proyecto de pruebas de integración, en `TestFeatures/AssistantBrowser`; `ConfigureTestServices` sustituye la fábrica en las pruebas y el producto nunca resuelve ese doble. `Assistant:UseFakeChatClient` sólo se admite en `Testing` o en `E2E` con `Assistant:IsolatedE2E=true`; fuera de esos hosts el arranque lo rechaza. El proveedor se crea al necesitar una llamada, con esfuerzo explícito, sin reintentos automáticos y sin registrar cuerpo ni clave. La validación de una clave usa el modelo predeterminado si está activo y admite esfuerzo low; de lo contrario elige el compatible con menor suma de tarifas de entrada y salida de la base actual. Los intervalos del worker, la renovación de lease, el plazo persistido y los tokens de finalización usan `TimeProvider` y las duraciones de `AssistantOptions`, validadas al arrancar. La respuesta mínima tiene límite de salida 64; errores de autorización quedan sanitizados como `Assistant.Settings.KeyRejected` y fallos transitorios como `Assistant.Provider.Unavailable`.

Las subidas del Inicio ([ADR 0064](../decisions/0064-reglas-de-toda-la-ia.md) §2 y §4) miran el cupo antes de leer el cuerpo y toman un lugar de la persona entre todas las copias, en Redis (sin Redis, de la copia), y uno de la copia, con `Assistant:Uploads:*`, validadas al arrancar: `ConcurrencyLimit` (4 a la vez), `QueueLimit` (8 en espera), `QueuePerOrganization` (2 lugares de la espera por organización; el lado personal y Plataforma cuentan juntos, como una) y `BodyTimeout` (`00:02:00`, el plazo del cuerpo). Son valores de piloto de cada copia, no cuotas.

Los archivos que un operador sube al chat de WhatsApp (E18) y la carga y la corrección de una respuesta de Formularios desde la web (E20 y E21) siguen la misma regla, cada uno con sus claves, validadas al arrancar: `WhatsApp:ChatUploads:*` y `Forms:Uploads:*`, con `ConcurrencyLimit`, `QueueLimit`, `QueuePerOrganization` (en WhatsApp, Plataforma cuenta como una) y `BodyTimeout`. Son valores de cada copia, no cuotas.
