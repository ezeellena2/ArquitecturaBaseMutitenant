# Multitenancy B2B + B2C: accesos, empresas, páginas públicas e interacción

> Complementa a [`backend.md`](backend.md). Cambiar una regla de este documento requiere un ADR. Rehecho el 2026-09-27 con la definición del usuario (ADR 0030).

## 1. Qué es, en simple

Una **plantilla estándar para cualquier tipo de negocio**. No es un producto: no sabe de médicos, de gimnasios ni de ventas. Lo que resuelve es **quién ingresa y cómo**, y la mecánica para que dos mundos se encuentren, como en Mercado Libre:

- **Organizaciones (B2B):** se registran como empresa, tienen sus usuarios con roles, y opcionalmente una **página pública por organización** en su subdominio (`<slug>.plataforma.com`).
- **Personas (B2C):** se registran solas como personas y pueden **interactuar con una empresa** a través de lo que esa empresa publica: pedir, contratar, reservar… lo que defina cada producto.

**El núcleo de la plantilla son los accesos:** quién puede ingresar, por dónde, qué ve cada tipo de usuario y cómo pasa de un acceso al otro (§2 y §3). Las páginas públicas y los datos compartidos (§4 y §5) son la **mecánica genérica** que cada producto usa para sus módulos.

> **Ejemplo (solo para entender; la plantilla no trae nada de esto):** una empresa de salud tiene médicos como usuarios B2B. Un kinesiólogo de esa empresa también ingresa **como persona** y le pide un turno a un traumatólogo desde la página de la organización. El turno lo ven el paciente y el profesional; nadie más. Otro producto usaría lo mismo para clases de un gimnasio, visitas de una inmobiliaria o servicios de un estudio contable.

## 2. Quiénes entran

| Tipo | Cómo entra | Qué hace | Qué **no** puede |
|---|---|---|---|
| **Visitante** | sin sesión | ve el sitio de la plataforma y las páginas públicas de las empresas | contratar nada |
| **Persona (acceso B2C)** | "Ingresá" o "Creá tu cuenta" (correo, WhatsApp o Google) | busca empresas, interactúa con ellas desde su página (pedir, contratar, reservar: lo que defina cada producto), ve lo que le pidió a cada una y su cuenta (la plantilla trae solo el inicio, vacío, y Mi cuenta) | **crear una empresa**, ver la administración de una empresa |
| **Usuario de empresa (acceso B2B)** | "Ingresá como empresa" | según su rol: administrar la empresa, armar su página y atender lo que piden las personas (lo que defina cada producto) | ver datos de otras empresas, o de personas fuera de lo que ellas le compartieron |
| **Dueño de la empresa** | "Registrá tu empresa" (alta B2B aparte) | crea la organización y queda como **Dueño** (`TenantAdmin`, todos los permisos) | — |
| **Operador de la plataforma** | acceso aparte, con segundo factor | backoffice: aprobar, rechazar, suspender y cerrar organizaciones, módulos por plan, cuentas, recuperaciones, documentos legales, auditoría | ver los datos de negocio de una empresa o de una persona |

Dentro de una organización, los usuarios se diferencian **solo por rol**: el **Dueño** (`TenantAdmin`, toda la organización), el **Administrador** de cada empresa (`CompanyAdmin`) y los roles que cree la organización. El Dueño sale solo del rol de sistema `TenantAdmin` (`RoleAssignment`), nunca de un dato de la membresía. La organización puede tener varias **empresas** (razones sociales o sedes), y son **planas**: no hay empresa matriz ni herencia entre empresas (ADR 0034). Cada rol tiene un alcance, que en pantalla es la columna "Vale en": toda la organización (`Organization`, se asigna sin empresa y vale en todas), cada empresa (`AnyCompany`, la empresa se elige al asignarlo y vale solo en esa) o solo en una empresa (`SpecificCompany`, el rol pertenece a esa empresa y solo se asigna ahí). El detalle y el catálogo de permisos están en [backend.md §14](backend.md#14-autorización).

## 3. Una cuenta, dos accesos

- **Identidad global:** una persona tiene **una sola cuenta**, con uno o varios métodos de ingreso (correos, teléfonos y Google, todos verificados y cada uno único en todo el sistema; §3.1). No hay dos registros ni contraseñas: se ingresa con un código, un enlace o Google.
- **Dos accesos que no se mezclan:**
  - **Acceso B2C:** su **espacio personal** (tenant `Kind=Personal`). Se crea solo al registrarse como persona o la primera vez que entra como persona.
  - **Acceso B2B:** sus **organizaciones** (tenants `Kind=Business`), por membresía. Solo existe si registró una empresa o lo invitaron a una.
- **Al ingresar se elige el acceso, con dos puertas:** "Ingresá" (`/login`, como persona) e "Ingresá como empresa" (`/login/empresa`). No se vuelve "al último lado": se entra al de la puerta elegida. Las páginas públicas llevan a la puerta de personas; la portada ofrece las dos. Por la puerta de empresas, con una sola organización entra directo; con varias, a la última que usó dentro del lado empresa; sin ninguna, ve "Registrá tu empresa"; si su única membresía está deshabilitada, ve "Tu acceso a <organización> está deshabilitado" (`Tenancy.Member.Inactive`), con "Ingresá como persona". Con otra membresía activa, entra a esa.
- **Cambiar de acceso** (de paciente a médico) no pide ingresar de nuevo: el menú de la cuenta muestra «Perfiles», con Personal («Tu perfil personal») y cada organización de la que es miembro, con su rol. El activo lleva ✓ y las suspendidas aparecen con «Suspendida» y no se pueden elegir. La lista es la misma en los dos lados. Elegir otro perfil cambia de lado o de organización. Por dentro, son tokens nuevos del otro acceso.
- **Lo que ve cada acceso no se mezcla:** el espacio personal **no muestra organizaciones** (la lista «Perfiles» del menú de la cuenta es de la cuenta, no del lado), y la empresa no ve lo personal.
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
  - **Si ese era su único método**, la cuenta **necesita recuperación**. No es un valor de `UserStatus` ni una columna guardada: es un estado **derivado** que calcula `IPlatformReader` (vista de plataforma). Una cuenta necesita recuperación cuando su `Status` es `Active` y no le queda ningún método de ingreso que sirva; un método sirve cuando está verificado y, si es administrado (`ManagedByTenantId`), la membresía de la persona en esa organización no terminó. `Suspended`, `PendingDeletion` y `Deleted` tienen prioridad y se muestran como tales. El cálculo va en la consulta, porque lo usan la ficha (`/plataforma/cuentas/:id`) y el filtro "Necesitan recuperación" del listado de cuentas. Con ese estado, la persona puede entrar con "Recuperar mi cuenta" (`platform.AccountRecoveryRequests`), verificando otro correo o teléfono con la ayuda de un operador de la plataforma. Esto es la excepción: lo normal es que ya haya agregado uno propio (ver lo que sigue).
- **Aviso para que no se llegue a eso:** si **todos** los métodos de ingreso de una cuenta son administrados por una organización (o es un correo de la invitación y no hay otro), la cuenta muestra un aviso fijo: "Agregá un correo personal o tu WhatsApp para no perder tu cuenta si dejás la empresa". Aparece al aceptar la invitación, en "Mi cuenta" y al entrar al espacio personal. El aviso se va solo cuando hay un método propio verificado.
- **Sin dominio verificado** (la regla por defecto): se aplica igual el aviso cuando el correo con el que llegó por invitación **no** es el mismo con el que ya tenía cuenta. Además, al terminar una membresía, la persona recibe el aviso "Revisá tus métodos de ingreso" en sus otros métodos.
- **Reglas:**
  - la persona puede sumar, verificar, elegir el principal y quitar métodos, siempre que le **quede al menos uno propio o activo**;
  - quitar o cambiar un método pide un código en **otro** método ya verificado;
  - todo cambio queda en `SecurityEvents` y se avisa en **todos** los métodos, por `IAccountNoticeChannel` (sin el módulo de WhatsApp, los teléfonos no reciben aviso).

### 3.2 Baja de una cuenta (ADR 0035)

La persona elimina **toda su cuenta**: la identidad, sus métodos de ingreso, su espacio personal y sus membresías. Es definitiva, pero tiene **30 días para arrepentirse** (`PlatformSettings.AccountDeletionGraceDays`). Ley 25.326: el derecho a la supresión.

**Estados de la identidad** (`ApplicationUser.Status`):

```
Active ──(pide la baja o la inicia la plataforma)──► PendingDeletion ──(pasan los días de gracia)──► Deleted
  ▲                              │
  └────(ingresa y la cancela)────┘

Active ──(la plataforma suspende)──► Suspended ──(reactiva)──► Active
Suspended ──(la plataforma inicia la baja)──► Suspended + fecha ──(pasan los días de gracia)──► Deleted
Suspended + fecha ──(reactiva, con la baja pedida)──► PendingDeletion
```

"Necesita recuperación" (§3.1) y "Baja iniciada por la plataforma" son estados que solo se muestran en pantalla, derivados del estado real; no son valores de `ApplicationUser.Status`.

Campos: `DeletionRequestedAtUtc`, `DeletionScheduledForUtc`, `DeletionReason`, `DeletionRequestedByOperatorId?` (null si la pidió la persona; si no, el operador que la inició) y `DeletedAtUtc`. El estado y la baja se guardan por separado (`Status` y `DeletionScheduledForUtc`): una cuenta suspendida con la baja iniciada sigue `Suspended`, con la fecha.

**1. Pedir la baja** (`POST /api/me/deletion`, `[Idempotent]`, desde "Mi cuenta" en cualquiera de los dos accesos):
- **Reautenticación:** un código enviado a un método verificado, validado hace menos de 5 minutos (`ReauthTicket`, el mismo que piden quitar o cambiar un método). Sin eso da `Legal.AccountDeletion.ReauthRequired`.
- **Motivo** obligatorio (texto libre, `TextLimits`).
- **`AccountDeletionPolicy` bloquea cuando:**
  - es el **único Dueño** de una organización que no está cerrada (cualquier estado menos `Closed`: `PendingApproval`, `Provisioning`, `Active` o `Suspended`). Da `Legal.AccountDeletion.LastAdmin`, con la lista de organizaciones. La salida es sumar otro Dueño, o pedirle a la plataforma que cierre la organización;
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
  - la identidad vuelve a `Active`, se limpian `DeletionRequestedAtUtc`, `DeletionScheduledForUtc`, `DeletionReason` y `DeletionRequestedByOperatorId`, y cada participante corre `OnCancelledAsync`;
  - se avisa en todos los métodos y se registra el `SecurityEvent`;
  - el ingreso sigue por la puerta que había elegido.

  Solo la persona puede cancelar; la plataforma no.
- **Sus métodos siguen reservados:** nadie puede registrarse con ese correo o ese teléfono. Una invitación a esos métodos no se puede aceptar sin ingresar, y ingresar lleva a cancelar la baja.
- **En cada organización**, el usuario se ve con el estado **"Baja pedida"**, que sale del estado de la identidad y no de la membresía. No se le pueden cambiar los roles; sí se lo puede quitar.
- **No cuenta como Dueño.** La protección del último Dueño exige otro Dueño activo (con el rol `TenantAdmin` y la identidad `Active`), así que una organización nunca queda sin Dueño cuando se completa la baja.
- **La plataforma** ve la cuenta como "Baja pedida", con la fecha.

**3. Eliminación** (`AccountDeletionWorker`):
- Corre cada hora y toma con `SKIP LOCKED` toda cuenta con `DeletionScheduledForUtc` vencido, esté `PendingDeletion` o `Suspended`.
- Procesa **una cuenta por pasos**: una transacción por cada tenant que toca (el espacio personal y cada organización), entrando con `ITenantScope` antes de abrirla, y una última para la identidad. La cuenta conserva su estado (`PendingDeletion` o `Suspended`) hasta ese último paso. Cada paso es idempotente, así que si se corta, la próxima corrida la vuelve a tomar y sigue desde donde quedó.

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
- Una cuenta **suspendida** sigue `Suspended`, con la fecha de eliminación. Como no puede ingresar (el ingreso responde cuenta suspendida), **no puede cancelar**. Si la plataforma la reactiva antes de la fecha, pasa a `PendingDeletion` (no a `Active`), y ahí sí puede ingresar y cancelar.
- Lleva motivo y no pide reautenticar a la persona. Se guarda `DeletionRequestedByOperatorId`, y el `SecurityEvent` `AccountDeletionRequested` se registra con el operador como actor (`ActorKind = PlatformOperator`).
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
  - hay un solo servidor OIDC, con el **issuer fijo** en el dominio principal (`plataforma.com`);
  - la página de la empresa pide `authorize` con `redirect_uri = https://<slug>.plataforma.com/auth/callback`, y OpenIddict la acepta si el slug existe y está publicado (`SubdomainRedirectUriValidator`, nunca un comodín abierto);
  - `authorize` y `logout` son navegaciones al dominio principal, donde vive la cookie de `/connect`; el canje del código, la renovación y `userinfo` van a `/connect/*` del **propio origen del subdominio**. En el front, `oidc-client-ts` usa `authority` = issuer, con un `metadataSeed` que apunta `token_endpoint`, `userinfo_endpoint` y `revocation_endpoint` al origen propio;
  - la sesión del servidor OIDC hace que, si ya ingresó en otro subdominio, no tenga que volver a hacerlo (`prompt=none`).
- **Infraestructura:** el SPA y la Api se sirven desde el **mismo origen en cada host** (el dominio principal y cada `<slug>.plataforma.com`), así que **no hay CORS**, tampoco en los subdominios, y `BackendPrefixes` se atiende igual en todos los hosts. En producción, desde wwwroot, con DNS comodín `*.plataforma.com` y certificado comodín. En desarrollo, con el proxy de Vite por host, sobre `*.localtest.me` (resuelve a 127.0.0.1).
- **Reservados:** `www`, `app`, `api`, `auth`, `admin`, `empresas`, `plataforma`… no pueden ser el slug de una empresa (`ReservedSlugs`).
- **Después, opcional:** dominio propio de una empresa (`www.empresa-a.com.ar`), que es la misma resolución por host, con una tabla de dominios verificados.

## 6. Decisiones

| Decisión | Valor |
|---|---|
| Aislamiento | Una base compartida, `TenantId`, filtros de EF con nombre y **RLS forzado** en `tenant`, `public_site` y `engagement` |
| Identidad | Global: una persona, una cuenta (correo y teléfono únicos) |
| Accesos | `access` = `consumer` \| `business` \| `platform` en el token. B2C nunca crea empresas; el alta B2B es "Registrá tu empresa" |
| Tenant activo | Solo del claim `tenant_id` (el espacio personal en B2C; la organización elegida en B2B). El host solo resuelve lo **público** |
| Empresas y roles | Empresas planas dentro de la organización, sin matriz ni herencia; roles con alcance `Organization`, `AnyCompany` o `SpecificCompany`; `companyId` en la URL, nunca en el token (ADR 0012 y 0034) |
| Páginas públicas | Subdominio por organización, slug único, redirect URI validada contra los slugs |
| Interacción B2C ↔ B2B | La base trae la mecánica (públicos, compartidos, `PartyPolicy`); los módulos que la usan los pone cada producto con `[FeatureGate]`. La plantilla **no trae ningún módulo de negocio** |

## 7. Estados

```
Espacio personal: Active ──(se elimina la cuenta, §3.2)──► Closed
Organización:     PendingApproval ─► Provisioning ─► Active ──► Suspended ──► Active | Closed
                  PendingApproval ──(la plataforma rechaza, con motivo)──► Closed
Página pública:   Draft ──(publicar)──► Published ──(despublicar)──► Draft
                  Published ──(la plataforma despublica, con motivo)──► Draft + publicación bloqueada (fecha y motivo)
                  publicación bloqueada ──(la plataforma permite publicar, con motivo)──► Draft sin bloqueo
```

- A una organización que no está activa no se puede entrar. Sus usuarios reciben 403 con el código de su estado: `Tenancy.Tenant.Suspended` si está suspendida, `Tenancy.Tenant.PendingApproval` mientras la plataforma no la aprobó (alta con aprobación) y `Tenancy.Tenant.Closed` si está cerrada. Su página muestra "no disponible". Los datos compartidos quedan visibles para la persona en modo solo lectura.
- Rechazar una organización que espera aprobación la deja en `Closed`, con motivo y `SecurityEvent`, y se le avisa a quien la registró. Su cuenta y su espacio personal siguen funcionando.
- `PublicPageStatus` sigue siendo `Draft | Published`: la moderación es un **bloqueo** en `PublicPage` (`PublishBlockedAtUtc`, `PublishBlockedReason` y `PublishBlockedByUserId`). Si la plataforma despublica la página, con motivo (`POST /api/platform/tenants/{id}/public-site/unpublish`, `platform.tenants.manage`), esta vuelve a `Draft` con el bloqueo, se les avisa a los Dueños y queda en la auditoría de seguridad. Mientras esté bloqueada, la organización ve el motivo y «Despublicada por la plataforma», y no puede publicarla (`PublicSite.PublicPage.PublishBlocked`). Solo la plataforma levanta el bloqueo, con «Permitir publicar» (`POST .../public-site/allow-publish`, también con motivo): la página queda en `Draft` y la organización la publica cuando quiera. Mientras no esté publicada, el sitio público responde 404.
- Suspender una identidad revoca sus sesiones en los dos accesos.
- El espacio personal no se suspende por separado: la plataforma suspende la identidad (§3.2), lo que corta los dos accesos sin cambiar el estado del tenant `Personal`.
- La identidad tiene además `PendingDeletion` y `Deleted` (la baja, §3.2).

## 8. Contexto de la petición

| Pieza | Capa | Rol |
|---|---|---|
| `ICurrentUser` | Application | `UserId`, `Access` (`Consumer` \| `Business` \| `Platform`) |
| `ITenantContext` | Application | `TenantId?` y `TenantKind?` del acceso activo |
| `IPublicSiteContext` | Application | `BusinessTenantId?` resuelto por el subdominio (solo para datos públicos) |
| `ITenantScope` | Application | `Enter(tenantId)` para plataforma, workers y altas |
| `TenantResolutionMiddleware` | Api | lee `access`, `tenant_id` y `tenant_kind` del token; verifica en caché que estén activos; si la organización no está activa, responde el 403 de su estado (§7) |
| `PublicSiteResolutionMiddleware` | Api | subdominio → `BusinessTenantId` de una página publicada |

**Rutas:** cada una declara su acceso. `[Access]` acepta uno o varios accesos:
- `[Access(Consumer)]` para lo de las personas;
- `[Access(Business)]` + permiso para la administración de la empresa;
- `[Access(Platform)]` para el backoffice;
- `[Access(Consumer, Business, Platform)]` para la propia cuenta, que vale con cualquier sesión: `/api/me`, `/api/me/*` y `POST /api/legal/accept`. El operador también lee `GET /api/me`; por eso `POST /api/me/deletion` le responde `Legal.AccountDeletion.PlatformOperator` y no 403;
- `[PublicSite]` + `[AllowAnonymous]` **solo** para lo que responde en el subdominio de una organización publicada (`PublicPageController`);
- solo `[AllowAnonymous]`, sin `[Access]`, para las rutas anónimas del dominio principal: el ingreso y el registro (`/api/auth/*` y `/connect/*`), «Registrá tu empresa» (`/api/auth/business-signup`), cancelar la baja (`/api/auth/deletion/cancel`), la invitación (`/api/invitations/*`), el enlace, los documentos legales (`GET /api/legal/*`), `GET /api/reference-data` y sus cinco rutas por catálogo (`ReferenceDataController`), el directorio (`DirectoryController`, que no lleva `[PublicSite]`), el pedido de «Recuperar mi cuenta» y los webhooks. `AccessDeclarationTests` las acepta únicamente si su controller está en la lista explícita del test. Estas rutas no dan acceso a datos privados de ningún tenant; el directorio lee solo lo publicado.

Con el acceso equivocado responde 403 `Tenancy.Access.Wrong`. Un recurso de otra organización responde 404, nunca 403, para no revelar que existe.

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
1. Quien la registra ingresa o crea su identidad (código por correo o WhatsApp, o Google, con la aceptación de términos) y después carga los datos de la organización: nombre, slug obligatorio con comprobación de disponibilidad y `ReservedSlugs`, su primera empresa y, si quiere, el CUIT de esa empresa. Si ya tiene cuenta, entra con ella; si no, se crea la identidad. El slug se puede cambiar después en «Página pública» (`/org/pagina`, «Dirección de la página»).
2. `BusinessSignupPolicy` aplica `PlatformSettings`: con `BusinessSignup` en `Closed` no se puede registrar («Alta cerrada»), y si la persona ya llegó a su límite de organizaciones propias (`MaxOwnedOrganizations`) tampoco («Llegó al límite»). Si pasa, `Tenant(Business)` queda en `PendingApproval` (modo `RequiresApproval`) o en `Provisioning` (modo `Open`).
3. `TenantProvisioner` crea la configuración, los roles de sistema, la primera empresa, la membresía con el rol `TenantAdmin` (Dueño) y la página pública en `Draft`.
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
  - **Organizaciones:** aprueba o rechaza las que esperan aprobación (rechazar la deja cerrada), y las suspende, reactiva y cierra.
  - Prende y apaga módulos por plan.
  - Ve el dominio de correo verificado de cada organización (§3.1), y puede verificarlo o quitarlo con motivo. Normalmente lo registra y lo verifica la propia organización, con `settings.manage`.
  - Modera páginas públicas: puede despublicarlas con motivo, lo que bloquea volver a publicarlas hasta que la plataforma lo permita («Permitir publicar», también con motivo). Las dos acciones quedan en la auditoría (§7).
  - **Cuentas:** suspende y reactiva identidades, cierra sus sesiones e inicia la baja (§3.2, punto 4).
  - Atiende los pedidos de «Recuperar mi cuenta» (`/plataforma/recuperaciones`): los aprueba o los rechaza.
  - Publica versiones nuevas de los documentos legales, por idioma; nunca edita una ya publicada.
  - Administra a los otros operadores y la configuración de la plataforma (`PlatformSettings`).
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
