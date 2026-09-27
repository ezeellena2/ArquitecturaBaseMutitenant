# Multitenancy B2B + B2C: accesos, empresas, páginas públicas e interacción

> Complementa a [`backend.md`](backend.md). Cambiar una regla de este documento requiere un ADR. Rehecho el 2026-09-27 con la definición del usuario (ADR 0030).

## 1. Qué es, en simple

Una **plantilla estándar para cualquier tipo de negocio**. No es un producto: no sabe de médicos, de gimnasios ni de ventas. Lo que resuelve es **quién ingresa y cómo**, y la mecánica para que dos mundos se encuentren, como en Mercado Libre:

- **Empresas (B2B):** se registran como empresa, tienen sus usuarios con roles, y opcionalmente una **página pública** en su subdominio (`<empresa>.plataforma.com`).
- **Personas (B2C):** se registran solas como personas y pueden **interactuar con una empresa** a través de lo que esa empresa publica: pedir, contratar, reservar… lo que defina cada producto.

**El núcleo de la plantilla son los accesos:** quién puede ingresar, por dónde, qué ve cada tipo de usuario y cómo pasa de un acceso al otro (§2 y §3). Las páginas públicas y los datos compartidos (§4 y §5) son la **mecánica genérica** que cada producto usa para sus módulos.

> **Ejemplo (solo para entender; la plantilla no trae nada de esto):** una empresa de salud tiene médicos como usuarios B2B. Un kinesiólogo de esa empresa también ingresa **como persona** y le pide un turno a un traumatólogo desde la página de la empresa. El turno lo ven el paciente y el profesional; nadie más. Otro producto usaría lo mismo para clases de un gimnasio, visitas de una inmobiliaria o servicios de un estudio contable.

## 2. Quiénes entran

| Tipo | Cómo entra | Qué hace | Qué **no** puede |
|---|---|---|---|
| **Visitante** | sin sesión | ve el sitio de la plataforma y las páginas públicas de las empresas | contratar nada |
| **Persona (acceso B2C)** | "Ingresá" o "Creá tu cuenta" (correo, WhatsApp o Google) | busca empresas, contrata o pide servicios, ve "Mis turnos" o "Mis contrataciones", su cuenta | **crear una empresa**, ver la administración de una empresa |
| **Usuario de empresa (acceso B2B)** | "Ingresá como empresa" | según su rol: administrar la empresa, armar su página y atender lo que piden las personas (lo que defina cada producto) | ver datos de otras empresas, o de personas fuera de lo que ellas le compartieron |
| **Dueño de la empresa** | "Registrá tu empresa" (alta B2B aparte) | crea la organización y queda como **Dueño** (`TenantAdmin`, todos los permisos) | — |
| **Operador de la plataforma** | acceso aparte, con segundo factor | backoffice: aprobar y suspender empresas, módulos por plan, cuentas, auditoría | ver los datos de negocio de una empresa o de una persona |

Dentro de una empresa, los usuarios se diferencian **solo por rol**: el Dueño, el Administrador de una empresa del grupo y los roles que cree cada organización. La organización puede tener varias **empresas** (razones sociales o sedes).

## 3. Una cuenta, dos accesos

- **Identidad global:** una persona tiene **una sola cuenta** (un correo, un teléfono, un Google). No hay dos contraseñas ni dos registros.
- **Dos accesos que no se mezclan:**
  - **Acceso B2C:** su **espacio personal** (tenant `Kind=Personal`). Se crea solo al registrarse como persona o la primera vez que entra como persona.
  - **Acceso B2B:** sus **organizaciones** (tenants `Kind=Business`), por membresía. Solo existe si registró una empresa o lo invitaron a una.
- **Al ingresar se elige el acceso, con dos puertas:** "Ingresá" (`/login`, como persona) e "Ingresá como empresa" (`/login/empresa`). No se vuelve "al último lado": se entra al de la puerta elegida. Las páginas públicas llevan a la puerta de personas; la portada ofrece las dos. Por la puerta de empresas, con una sola organización entra directo; con varias, a la última que usó dentro del lado empresa; sin ninguna, ve "Registrá tu empresa".
- **Cambiar de acceso** (de paciente a médico) no pide ingresar de nuevo: el menú de la cuenta tiene "Ir a mi empresa" o "Ir a Personal". Por dentro, son tokens nuevos del otro acceso.
- **Lo que ve cada acceso no se mezcla:** el espacio personal **no muestra organizaciones**, y la empresa no ve lo personal.
- **Ejemplo:** alguien trabaja en la Empresa A y además es cliente de la Empresa B. Tiene **una** cuenta. Como **empresa** ve lo de la Empresa A según su rol. Como **persona** ve lo que le pidió a la Empresa B.

### 3.1 Métodos de ingreso: la cuenta no depende de un solo correo

**El problema:** a una persona la invitan con su correo de la empresa (`kevin@empresa-a.com`). Con ese correo ingresa como empresa **y también como persona**. Si después la desvinculan, pasan dos cosas:
- **pierde su cuenta**, incluido su espacio personal;
- **peor aún, la empresa sigue siendo dueña de ese buzón**: cualquiera con acceso a él podría pedir un código y entrar a lo personal de Kevin.

**La solución:**
- **Varios métodos de ingreso por cuenta:** correos, teléfonos (WhatsApp) y Google, todos **verificados** y todos **únicos en todo el sistema** (`identity.LoginMethods`: tipo, valor, `VerifiedAtUtc`, `IsPrimary`, `ManagedByTenantId?`). Se ingresa con **cualquiera** de ellos, y uno es el principal (el que recibe los avisos).
- **Correo de recupero:** es un correo personal más, verificado. Sirve para ingresar y para recuperar la cuenta.
- **Correos administrados por una empresa:** una organización puede verificar su **dominio** (`empresa-a.com`, con un registro DNS TXT). Los correos de ese dominio quedan marcados `ManagedByTenantId` = esa organización.
  - **Mientras la persona es miembro**, un correo administrado sirve para ingresar a los **dos** accesos.
  - **Cuando la membresía termina**, el correo administrado **deja de servir para ingresar**, y se le avisa a la persona por sus otros métodos. Así la empresa nunca entra a lo personal de alguien que ya no trabaja ahí.
  - **Si ese era su único método**, la cuenta queda en "necesita recuperación": puede entrar con "Recuperar mi cuenta", verificando otro correo o teléfono con la ayuda de un operador de la plataforma. Esto es la excepción: lo normal es que ya haya agregado uno propio (ver lo que sigue).
- **Aviso para que no se llegue a eso:** si **todos** los métodos de ingreso de una cuenta son administrados por una organización (o es un correo de la invitación y no hay otro), la cuenta muestra un aviso fijo: "Agregá un correo personal o tu WhatsApp para no perder tu cuenta si dejás la empresa". Aparece al aceptar la invitación, en "Mi cuenta" y al entrar al espacio personal. El aviso se va solo cuando hay un método propio verificado.
- **Sin dominio verificado** (la regla por defecto): se aplica igual el aviso cuando el correo con el que llegó por invitación **no** es el mismo con el que ya tenía cuenta. Además, al terminar una membresía, la persona recibe el aviso "Revisá tus métodos de ingreso" en sus otros métodos.
- **Reglas:**
  - la persona puede sumar, verificar, elegir el principal y quitar métodos, siempre que le **quede al menos uno propio o activo**;
  - quitar o cambiar un método pide un código en **otro** método ya verificado;
  - todo cambio queda en `SecurityEvents` y se avisa en **todos** los métodos.

### 3.2 Baja de una cuenta (ADR 0035)

La persona elimina **toda su cuenta**: la identidad, sus métodos de ingreso, su espacio personal y sus membresías. Es definitiva, pero tiene **30 días para arrepentirse** (`PlatformSettings.AccountDeletionGraceDays`). Ley 25.326: el derecho a la supresión.

**Estados de la identidad** (`ApplicationUser.Status`):

```
Active ──(pide la baja)──► PendingDeletion ──(pasan los días de gracia)──► Deleted
  ▲                              │
  └────(ingresa y la cancela)────┘

Active ──(la plataforma suspende)──► Suspended ──(reactiva)──► Active
```

Campos: `DeletionRequestedAtUtc`, `DeletionScheduledForUtc`, `DeletionReason` y `DeletedAtUtc`.

**1. Pedir la baja** (`POST /api/me/deletion`, `[Idempotent]`, desde "Mi cuenta" en cualquiera de los dos accesos):
- **Reautenticación:** un código enviado a un método verificado, validado hace menos de 5 minutos (`ReauthTicket`, el mismo que piden quitar o cambiar un método). Sin eso da `Legal.AccountDeletion.ReauthRequired`.
- **Motivo** obligatorio (texto libre, `TextLimits`).
- **`AccountDeletionPolicy` bloquea cuando:**
  - es el **único Dueño** de una organización que no está cerrada (activa, en espera de aprobación o suspendida). Da `Legal.AccountDeletion.LastAdmin`, con la lista de organizaciones. La salida es sumar otro Dueño, o pedirle a la plataforma que cierre la organización;
  - es **operador de la plataforma**: `Legal.AccountDeletion.PlatformOperator`. A un operador lo da de baja otro operador;
  - la baja **ya está pedida**: `Legal.AccountDeletion.AlreadyPending`;
  - un **módulo** tiene algo pendiente, según su `IAccountDeletionParticipant.CheckAsync`: `Legal.AccountDeletion.Blocked`, con los motivos traducidos que devuelve cada módulo. La plantilla no trae módulos, así que no bloquea nada por esto.
- **En la misma transacción:**
  - la identidad pasa a `PendingDeletion`, con la fecha programada;
  - se **revocan todas sus sesiones y tokens**, en los dos accesos (autorizaciones y tokens de OpenIddict);
  - cada participante corre `OnRequestedAsync`, por ejemplo un módulo que cancela lo que la persona tenía pendiente;
  - se encola el aviso "Pediste la baja de tu cuenta" en **todos** sus métodos, con la fecha y "si no fuiste vos, ingresá para cancelarla";
  - se registra el `SecurityEvent` `AccountDeletionRequested`.
- El front cierra la sesión y muestra "Tu cuenta se elimina el dd/mm/aaaa". El diálogo de baja sugiere antes "Exportar mis datos".

**2. Durante la gracia:**
- **Nadie puede entrar:** la persona tampoco, salvo para cancelar.
  - Al ingresar (código o Google, por cualquiera de las dos puertas), el servidor **no emite tokens**. Responde `Identity.Account.PendingDeletion`, con la fecha y un `cancelTicket` que vale 5 minutos.
  - El front muestra "Tu cuenta tiene la baja pedida · Se elimina el dd/mm/aaaa", con "Cancelar la baja y entrar" y "Salir".
- **Cancelar** (`POST /api/auth/deletion/cancel`):
  - la identidad vuelve a `Active` y cada participante corre `OnCancelledAsync`;
  - se avisa en todos los métodos y se registra el `SecurityEvent`;
  - el ingreso sigue por la puerta que había elegido.

  Solo la persona puede cancelar; la plataforma no.
- **Sus métodos siguen reservados:** nadie puede registrarse con ese correo o ese teléfono. Una invitación a esos métodos no se puede aceptar sin ingresar, y ingresar lleva a cancelar la baja.
- **En cada organización**, el usuario se ve con el estado **"Baja pedida"**, que sale del estado de la identidad y no de la membresía. No se le pueden cambiar los roles; sí se lo puede quitar.
- **No cuenta como Dueño.** La protección del último Dueño exige otro Dueño activo, así que una organización nunca queda sin Dueño cuando se completa la baja.
- **La plataforma** ve la cuenta como "Baja pedida", con la fecha.

**3. Eliminación** (`AccountDeletionWorker`):
- Corre cada hora y toma las cuentas vencidas con `SKIP LOCKED`.
- Procesa **una cuenta por transacción**, entra a cada tenant con `ITenantScope` y es idempotente: si se corta, la próxima corrida sigue desde donde quedó.

| Dato | Qué pasa |
|---|---|
| Identidad | la fila queda, porque la usan la auditoría y las claves foráneas, pero **anonimizada**: nombre "Cuenta eliminada", sin correo, teléfono ni preferencias. `Status = Deleted` y `DeletedAtUtc` |
| Métodos de ingreso y Google | se **borran**: el correo y el teléfono quedan libres para una cuenta nueva |
| Sesiones y tokens | ya estaban revocados; se purgan |
| Espacio personal | el tenant `Personal` pasa a `Closed`. Sus datos privados se **borran**: cada módulo B2C con su participante, y después el barrido por `TenantId` |
| Membresías | pasan a `Removed`, con motivo `AccountDeleted`, y se quitan sus roles de organización y de empresa. La auditoría de cada organización registra "Cuenta eliminada dejó la organización" |
| Invitaciones pendientes a sus métodos | se revocan |
| Invitaciones que mandó ella | siguen valiendo, porque son de la organización; "Invitado por" muestra "Cuenta eliminada" |
| Datos compartidos con empresas (`engagement`) | la empresa **conserva su registro**, pero la copia de los datos personales (nombre, teléfono y lo que haya pedido el módulo) se reemplaza por "Cuenta eliminada". Si el módulo necesita guardarlos por ley, lo declara con `IRetainedOnConsumerDeletion`, con el motivo y el plazo documentados, y se purgan al vencer |
| Auditoría y `SecurityEvents` | se **conservan** con el `ActorId`, y el nombre se muestra como "Cuenta eliminada". Nunca tuvieron correos ni teléfonos completos |
| Aceptaciones legales | se conservan el documento, la versión y la fecha, como prueba; se borran la IP y el user agent |
| Exportaciones de datos | se borran los archivos |
| Pedidos de "Recuperar mi cuenta" | se cierran |
| Outbox | se cancelan los mensajes pendientes para la persona |
| Caché | se invalidan `u:{userId}:` y `t:{personalTenantId}:` |

El aviso final, "Tu cuenta fue eliminada", va al método principal. Se encola **antes** de borrar los métodos, con la dirección cifrada en el payload. Se registra el `SecurityEvent` `AccountDeleted`.

**4. Desde la plataforma:**
- Un operador puede **iniciar la baja** de una cuenta activa o suspendida, por ejemplo ante un pedido legal que llega por fuera de la plataforma.
- Lleva motivo y no pide reautenticar a la persona.
- Lo bloquean las mismas reglas: si la persona es el **único Dueño** de una organización no cerrada, primero esa organización tiene que sumar otro Dueño, o la plataforma tiene que cerrarla.
- La cuenta muestra "La plataforma inició la baja el dd/mm/aaaa", en lugar de "Pidió la baja".
- Sigue el mismo camino: 30 días de gracia, avisos y eliminación.
- No puede saltearse la gracia ni cancelar por la persona.

**Participantes** (`IAccountDeletionParticipant`: `CheckAsync`, `OnRequestedAsync`, `OnCancelledAsync` y `ExecuteAsync`):
- los del núcleo: membresías, espacio personal, datos compartidos, aceptaciones legales, exportaciones, pedidos de recuperación y outbox;
- cada módulo de un producto registra el suyo si guarda datos de la persona. `AccountDeletionParticipantsTests` verifica que toda entidad con datos de una identidad tenga un participante.

## 4. Tres clases de datos

Todo dato nuevo se clasifica **antes** de escribir su entidad:

| Clase | Qué es | Esquema | Interfaz | Quién lo ve (RLS) |
|---|---|---|---|---|
| **Privado** | lo interno de una empresa o de una persona | `tenant` | `ITenantOwned` (`TenantId`) | solo su tenant |
| **Público** | lo que una empresa publica: su página y lo que cada producto le permita publicar | `public_site` | `IPublishedByBusiness` (`BusinessTenantId`, `IsPublished`) | todos, sin sesión, si está publicado; la empresa, siempre |
| **Compartido** | lo que une a una persona con una empresa (según el producto: una reserva, un pedido, una solicitud, sus mensajes) | `engagement` | `IConsumerBusinessShared` (`ConsumerTenantId`, `BusinessTenantId`) | solo esa persona y esa empresa |

- **Un dato compartido guarda una copia** de lo que la otra parte necesita ver tal como era en ese momento (por ejemplo, el nombre y el precio de lo que se pidió). Nunca se lee el dato privado de la otra parte.
- **Quién hace qué:** cada módulo define qué transiciones puede hacer cada parte (por ejemplo, la persona pide o cancela y la empresa confirma o rechaza). La base trae `PartyPolicy` para verificar que el acceso activo sea la parte correcta.
- **Datos de la persona para la empresa:** la empresa ve **solo** lo que la persona le compartió al contratar (nombre, teléfono, lo que pida el módulo), copiado en el dato compartido. Nunca su cuenta ni su espacio personal.

## 5. Páginas públicas por subdominio

- **Una página pública por organización** (no por empresa): cada organización tiene un `Slug` único (`empresa-a`), y su página vive en `https://empresa-a.plataforma.com`. El sitio de la plataforma (`plataforma.com`) tiene el buscador o directorio de empresas publicadas.
- **Resolución por host, solo para lo público:** `PublicSiteResolutionMiddleware` lee el subdominio y fija `IPublicSiteContext.BusinessTenantId`. Sirve **únicamente** para leer datos **públicos** de esa empresa. **Nunca** da acceso a datos privados: para eso sigue mandando el claim `tenant_id` del token.
- **Un solo front** (el mismo SPA) atiende todos los subdominios: con un subdominio de empresa muestra la página pública; con el dominio principal, el sitio de la plataforma y los accesos.
- **Ingreso desde un subdominio:**
  - hay un solo servidor OIDC (`plataforma.com`);
  - la página de la empresa pide `authorize` con `redirect_uri = https://<slug>.plataforma.com/auth/callback`, y OpenIddict la acepta si el slug existe y está publicado (`SubdomainRedirectUriValidator`, nunca un comodín abierto);
  - la sesión del servidor OIDC hace que, si ya ingresó en otro subdominio, no tenga que volver a hacerlo (`prompt=none`).
- **Infraestructura:** DNS comodín `*.plataforma.com` y certificado comodín. En desarrollo: `*.localtest.me` (resuelve a 127.0.0.1) o `*.plataforma.localhost`.
- **Reservados:** `www`, `app`, `api`, `auth`, `admin`, `empresas`, `plataforma`… no pueden ser el slug de una empresa (`ReservedSlugs`).
- **Después, opcional:** dominio propio de una empresa (`www.empresa-a.com.ar`), que es la misma resolución por host, con una tabla de dominios verificados.

## 6. Decisiones

| Decisión | Valor |
|---|---|
| Aislamiento | Una base compartida, `TenantId`, filtros de EF con nombre y **RLS forzado** en `tenant`, `public_site` y `engagement` |
| Identidad | Global: una persona, una cuenta (correo y teléfono únicos) |
| Accesos | `access` = `consumer` \| `business` \| `platform` en el token. B2C nunca crea empresas; el alta B2B es "Registrá tu empresa" |
| Tenant activo | Solo del claim `tenant_id` (el espacio personal en B2C; la organización elegida en B2B). El host solo resuelve lo **público** |
| Páginas públicas | Subdominio por organización, slug único, redirect URI validada contra los slugs |
| Interacción B2C ↔ B2B | La base trae la mecánica (públicos, compartidos, `PartyPolicy`); los módulos que la usan los pone cada producto con `[FeatureGate]`. La plantilla **no trae ningún módulo de negocio** |

## 7. Estados

```
Espacio personal: Active ──(la plataforma suspende)──► Suspended ──► Active | Closed
Organización:     PendingApproval ─► Provisioning ─► Active ──► Suspended ──► Active | Closed
Página pública:   Draft ──(publicar)──► Published ──(despublicar)──► Draft
```

- Una organización suspendida: su página muestra "no disponible" y sus usuarios reciben 403 `Tenancy.Tenant.Suspended`. Los datos compartidos quedan visibles para la persona en modo solo lectura.
- Suspender una identidad revoca sus sesiones en los dos accesos.
- La identidad tiene además `PendingDeletion` y `Deleted` (la baja, §3.2).

## 8. Contexto de la petición

| Pieza | Capa | Rol |
|---|---|---|
| `ICurrentUser` | Application | `UserId`, `Access` (`Consumer` \| `Business` \| `Platform`) |
| `ITenantContext` | Application | `TenantId?` y `TenantKind?` del acceso activo |
| `IPublicSiteContext` | Application | `BusinessTenantId?` resuelto por el subdominio (solo para datos públicos) |
| `ITenantScope` | Application | `Enter(tenantId)` para plataforma, workers y altas |
| `TenantResolutionMiddleware` | Api | lee `access`, `tenant_id` y `tenant_kind` del token; verifica en caché que estén activos |
| `PublicSiteResolutionMiddleware` | Api | subdominio → `BusinessTenantId` de una página publicada |

**Rutas:** cada una declara su acceso:
- `[Access(Consumer)]` para lo de las personas;
- `[Access(Business)]` + permiso para la administración de la empresa;
- `[Access(Platform)]` para el backoffice;
- `[PublicSite]` + `[AllowAnonymous]` para la página pública.

Con el acceso equivocado responde 403 `Tenancy.Access.Wrong`.

## 9. Barreras

- **EF:** filtros con nombre por clase:
  - `"Tenant"` (privado);
  - `"Public"`: `IsPublished || BusinessTenantId == ctx.TenantId`;
  - `"Parties"`: `ConsumerTenantId == ctx.TenantId || BusinessTenantId == ctx.TenantId`;
  - `"SoftDelete"`.

  `TenantStampInterceptor` sella la columna de la clase y rechaza cambios. `TenantIsolationModelValidator` exige que toda entidad esté clasificada.
- **RLS:** `EnableTenantRls`, `EnablePublicRls` y `EnablePartiesRls`, con `FORCE`, y el trigger `prevent_tenant_change` en las tres clases.
- **Roles de BD:** `mt_owner` y `mt_app` (sin privilegios), validados al arrancar.
- `app.tenant_id` llega a la sesión de Postgres como antes: al abrir la conexión y otra vez dentro de la transacción. Una página pública sin sesión lee con `app.tenant_id = ''`: la política pública deja ver solo lo publicado.

## 10. Flujos

**Registro de una persona (B2C):**
1. "Creá tu cuenta" → código (o Google).
2. Se crea la identidad y su espacio personal, con aceptación de términos.
3. Tokens con `access=consumer`.

**Registro de una empresa (B2B), "Registrá tu empresa":**
1. Datos de la empresa (nombre, slug, CUIT) y de quien la registra. Si esa persona ya tiene cuenta, ingresa con ella; si no, se crea la identidad.
2. `Tenant(Business)` queda en `PendingApproval` o en `Provisioning`, según `BusinessSignup`.
3. `TenantProvisioner` crea la configuración, los roles de sistema, la primera empresa, la membresía de Dueño y la página pública en `Draft`.
4. Tokens con `access=business`.

**Invitar a alguien a la empresa:** si ya tiene cuenta, suma el acceso B2B a esa organización; si no, se crea la identidad al aceptar. **No crea un espacio personal:** ese nace la primera vez que entra como persona.

**Una persona interactúa con una empresa** (mecánica de la base; qué se pide y con qué estados lo define cada módulo):
1. Una persona en `empresa-a.plataforma.com` ve lo que la empresa publicó.
2. Si no ingresó, "Ingresá para continuar" (acceso B2C) y vuelve a la misma página.
3. El módulo crea el dato compartido (`ConsumerTenantId` = su espacio personal, `BusinessTenantId` = la organización), con copia de lo necesario, en una transacción, con `[Idempotent]`.
4. La empresa lo ve en su bandeja; la persona, en su espacio personal. Cada cambio de estado lo hace la parte autorizada (`PartyPolicy`) y queda auditado de los dos lados.
5. Avisos por correo o WhatsApp a las dos partes (outbox).

**Cambio de acceso o de organización:** `/connect/authorize?prompt=none&access=business&tenant=<id>` (o `access=consumer`). El servidor valida la membresía y emite tokens nuevos.

## 11. Plataforma

- **Operador:** `access=platform`, con segundo factor.
  - Aprueba y suspende organizaciones.
  - Prende y apaga módulos por plan.
  - Modera páginas públicas: puede despublicarlas con motivo.
  - Suspende identidades.
  - Nunca ve datos privados ni compartidos: la auditoría registra cada acción con su motivo.

## 12. Caché, locks, unicidad

- **Prefijos de caché:**
  - `t:{tenantId}:` (privado);
  - `s:{businessTenantId}:` (página pública; se invalida al publicar);
  - `u:{userId}:` (identidad y accesos);
  - `p:` (plataforma).
- **Únicos globales:** cada método de ingreso (`LoginMethods`: tipo + valor), el dominio verificado (`TenantDomains`), el `Slug` de la organización y el `PhoneNumberId` de WhatsApp.
- **Una sola fuente para correos y teléfonos:** `LoginMethods`. `AspNetUsers.Email` y `PhoneNumber` son solo una copia del método principal (ASP.NET Identity los usa), sin índice único; los mantiene el servicio de métodos de ingreso.
- **Locks de recursos compartidos:** sobre la fila (`FOR NO KEY UPDATE`), por ejemplo para no dar dos veces el mismo horario.

## 13. Tests obligatorios de aislamiento

`TenantFixture` arma este escenario, con nombres neutros:
- **Empresa A** (B2B), con Ana y Kevin como usuarios;
- **Empresa B** (B2B), con Beto;
- **Kevin como persona** (B2C) y **Carla** (B2C).

Los tests usan las entidades de `TestFeatures` (`Widget` privado, `Poster` público, `Deal` compartido), porque la plantilla no trae módulos de negocio.

| Test | Qué garantiza |
|---|---|
| `Consumer_cannot_create_organization` | con acceso B2C, crear una empresa da 403 |
| `Access_views_do_not_mix` | Kevin como persona no ve lo privado de la Empresa A, y como empresa no ve su espacio personal |
| `Shared_rows_visible_only_to_parties` | un `Deal` entre Kevin (persona) y la Empresa A solo lo ven esas dos partes; no Carla ni la Empresa B |
| `Business_sees_only_what_was_shared` | la Empresa A ve lo copiado en el `Deal`, no la cuenta de Kevin |
| `Public_rows_visible_only_when_published` | un `Poster` en borrador no aparece en la página pública, ni para visitantes ni para otra empresa |
| `Subdomain_resolves_public_data_only` | `empresa-a.plataforma.com` con un token de la Empresa B no ve datos privados de la Empresa A |
| `Redirect_uri_only_for_published_slugs` | `authorize` con un subdominio inexistente es rechazado |
| `Wrong_access_is_forbidden`, `Access_switch_requires_membership` | accesos y membresías |
| `AccountDeletionTests` | la baja pide reautenticación; el único Dueño no puede darse de baja; revoca las sesiones; durante la gracia el ingreso ofrece cancelar; la eliminación anonimiza, libera el correo, quita las membresías y anonimiza la copia en los datos compartidos; la auditoría queda |
| RLS, rol de runtime, inventario de políticas, columnas inmutables, caché con prefijo | igual que antes, ahora para las tres clases |
