# Multitenancy B2B + B2C: accesos, empresas, páginas públicas e interacción

> Complementa a [`backend.md`](backend.md). Un cambio que toca el modelo de accesos o el aislamiento requiere un ADR; un cambio técnico, como la CSP o la caché, no lo necesita y se actualiza junto con el código. Rehecho el 2026-09-27 con la definición del usuario (ADR 0030).

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

Dentro de una organización, los usuarios se diferencian **solo por rol**: el **Dueño** (`TenantAdmin`, toda la organización), el **Administrador** de cada empresa (`CompanyAdmin`) y los roles que cree la organización. El Dueño sale solo del rol de sistema `TenantAdmin` (`RoleAssignment`), nunca de un dato de la membresía. La organización puede tener varias **empresas** (razones sociales o sedes), y son **planas**: no hay empresa matriz ni herencia entre empresas (ADR 0034). Cada rol tiene un alcance, que en pantalla es la columna "Vale en": toda la organización (`Organization`, se asigna sin empresa y vale en todas), cada empresa (`AnyCompany`, la empresa se elige al asignarlo y vale solo en esa) o solo en una empresa (`SpecificCompany`, el rol pertenece a esa empresa y solo se asigna ahí). Al crear un rol, la pantalla ofrece toda la organización o una empresa; «Cada empresa» queda para el rol de sistema Administrador de empresa y para la API (ADR 0034, enmienda del 2026-10-03). El detalle y el catálogo de permisos están en [backend.md §14](backend.md#14-autorización).

## 3. Una cuenta, dos accesos

- **Identidad global:** una persona tiene **una sola cuenta**, con uno o varios métodos de ingreso (correos, teléfonos y Google, todos verificados y cada uno único en todo el sistema; §3.1). No hay dos registros ni contraseñas: se ingresa con un código, un enlace o Google.
- **Dos accesos que no se mezclan:**
  - **Acceso B2C:** su **espacio personal** (tenant `Kind=Personal`). Se crea solo al registrarse como persona o la primera vez que entra como persona.
  - **Acceso B2B:** sus **organizaciones** (tenants `Kind=Business`), por membresía. Solo existe si registró una empresa o lo invitaron a una.
- **Al ingresar se elige el acceso, con dos puertas:** "Ingresá" (`/login`, como persona) e "Ingresá como empresa" (`/login/empresa`). No se vuelve "al último lado": se entra al de la puerta elegida. Las páginas públicas llevan a la puerta de personas; la portada ofrece las dos. Por la puerta de empresas, con una sola organización entra directo; con varias, a la última que usó dentro del lado empresa. Si hay varias membresías activas y nunca usó una organización en el lado empresa, se elige la membresía activa con `JoinedAtUtc` más antiguo; un empate se resuelve por nombre de organización. Sin ninguna, ve el estado sin empresa del tablero: el control "Registrá tu empresa" se oculta hasta que nazca el alta empresarial en la Etapa 6. Si su única membresía está deshabilitada, ve "Tu acceso a <organización> está deshabilitado" (`Tenancy.Member.Inactive`), con "Ingresá como persona". Con otra membresía activa, entra a esa.
- **Cambiar de acceso** (de paciente a médico) no pide ingresar de nuevo: el menú de la cuenta muestra «Perfiles», con Personal («Tu perfil personal») y cada organización de la que es miembro, con su rol. El activo lleva ✓ y las suspendidas aparecen con «Suspendida» y no se pueden elegir. La lista es la misma en los dos lados. Elegir otro perfil cambia de lado o de organización. Por dentro, son tokens nuevos del otro acceso.
- **Lo que ve cada acceso no se mezcla:** el espacio personal **no muestra organizaciones** (la lista «Perfiles» del menú de la cuenta es de la cuenta, no del lado), y la empresa no ve lo personal.
- **Ejemplo:** alguien trabaja en la Empresa A y además es cliente de la Empresa B. Tiene **una** cuenta. Como **empresa** ve lo de la Empresa A según su rol. Como **persona** ve lo que le pidió a la Empresa B.

### 3.1 Métodos de ingreso: la cuenta no depende de un solo correo

**El problema:** a una persona la invitan con su correo de la empresa (`kevin@empresa-a.test`). Con ese correo ingresa como empresa **y también como persona**. Si después la desvinculan, pasan dos cosas:
- **pierde su cuenta**, incluido su espacio personal;
- **peor aún, la empresa sigue siendo dueña de ese buzón**: cualquiera con acceso a él podría pedir un código y entrar a lo personal de Kevin.

**La solución:**
- **Varios métodos de ingreso por cuenta:** correos, teléfonos (WhatsApp) y Google, todos **verificados** y todos **únicos en todo el sistema** (`identity.LoginMethods`: tipo, valor, `VerifiedAtUtc`, `IsPrimary`, `ManagedByTenantId?`). Se ingresa con **cualquiera** de ellos, y uno es el principal (el que recibe los avisos).
- **Correo de recupero:** es un correo personal más, verificado. Sirve para ingresar y para recuperar la cuenta.
- **Correos administrados por una empresa:** una organización puede verificar su **dominio** (`empresa-a.com`, con un registro DNS TXT). Los correos de ese dominio quedan marcados `ManagedByTenantId` = esa organización.
  - **Mientras la persona es miembro**, un correo administrado sirve para ingresar a los **dos** accesos.
  - **Cuando la membresía termina**, el correo administrado **deja de servir para ingresar**, y se le avisa a la persona por sus otros métodos. Así la empresa nunca entra a lo personal de alguien que ya no trabaja ahí.
  - **Si ese era su único método**, la cuenta **necesita recuperación**. No es un valor de `UserStatus` ni una columna guardada: es un estado **derivado** que calcula `IPlatformAccountReader` (vista de plataforma). Una cuenta necesita recuperación cuando su `Status` es `Active` y no le queda ningún método de ingreso que sirva. Un método sirve con la misma regla que el ingreso (`LoginMethodAvailabilityPolicy`): está verificado, su tipo tiene canal configurado y, si es administrado (`ManagedByTenantId`), la membresía de la persona y esa organización siguen activas. `Suspended`, `PendingDeletion` y `Deleted` tienen prioridad y se muestran como tales. El cálculo va en la consulta, porque lo usan la ficha (`/plataforma/cuentas/:id`) y el filtro "Necesitan recuperación" del listado de cuentas. Con ese estado, la persona puede entrar con "Recuperar mi cuenta" (`platform.AccountRecoveryRequests`), verificando otro correo o teléfono con la ayuda de un operador de la plataforma. Esto es la excepción: lo normal es que ya haya agregado uno propio (ver lo que sigue).
- **Aviso para que no se llegue a eso:** si **todos** los métodos de ingreso de una cuenta son administrados por una organización (o es un correo de la invitación y no hay otro), la cuenta muestra un aviso fijo: "Agregá un correo personal o tu WhatsApp para no perder tu cuenta si dejás la empresa". Aparece al aceptar la invitación, en "Mi cuenta" y al entrar al espacio personal. El aviso se va solo cuando hay un método propio verificado.
- **Sin dominio verificado** (la regla por defecto): se aplica igual el aviso cuando el correo con el que llegó por invitación **no** es el mismo con el que ya tenía cuenta. Además, al terminar una membresía, la persona recibe el aviso "Revisá tus métodos de ingreso" en sus otros métodos.
- **Reglas:**
  - la persona puede sumar, verificar, elegir el principal y quitar métodos, siempre que le **quede al menos uno propio o activo**;
  - Mi cuenta usa la sesión iniciada para agregar/vincular, hacer principal, quitar y dar de baja, **sin** código de confirmación ni ReauthTicket (ADR 0045); solo el método nuevo prueba su propia posesión;
  - todo cambio queda en `SecurityEvents` y se avisa en **todos** los métodos, por `IAccountNoticeChannel` (sin el módulo de WhatsApp, los teléfonos no reciben aviso).

### 3.2 Baja de una cuenta (ADR 0035)

La baja, la gracia, sus participantes y los límites legales están en [datos personales](../rules/datos-personales.md). La ejecución y la copia compartida retenida se describen en [Personal](../features/personal.md). Un módulo aporta su participante al límite y alcance del llamador.

### 3.3 Invitaciones a una organización

Una invitación y su `Member(Invited)` son datos privados del tenant Business, en `tenant`, con filtro EF y RLS forzado. `Member.UserId` puede ser null durante la reserva; el índice global de accesos omite esas filas y las agrega al vincular una identidad. Un miembro invitado nunca queda Active sin identidad y una reserva retirada no se reactiva ni vincula después.

`InvitationIssuer` emite dentro de la UoW de su llamador y encola mediante `IInvitationChannel`; no abre su propia transacción, no crea una cuenta y no expone una ruta. El token opaco va en el fragmento del enlace de `/invitacion`; la base conserva solo su hash. La vista previa y la aceptación reciben el token en JSON (`POST /api/invitations/preview` y `POST /api/invitations/accept`), nunca en la URL, query, `Location` ni logs. Preview no consume. Accept comprueba la organización, destino, sesión/cuenta y estados bajo locks y consume Invitation y activa Member en la misma transacción.

Una identidad nueva se crea ya vinculada a la membresía Business, con su método verificado y consentimientos vigentes, pero sin espacio Personal ni acceso Consumer. Una cuenta existente debe demostrar su identidad por el ingreso habitual; el enlace no ingresa por ella ni permite cambiar de cuenta vinculada. El espacio Personal nace solo al entrar luego por Consumer. Si la respuesta de una aceptación nueva se pierde, únicamente el navegador que inició el flujo puede recuperar la sesión durante cinco minutos mediante su nonce protegido HttpOnly; el replay no crea otra identidad ni otra membresía. Emisión administrativa, reenvío y revocación HTTP desde Usuarios quedan para E5/E6; WhatsApp, para E8.

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
- **Resolución por host, solo para lo público (E7):** `PublicSiteResolutionMiddleware` lee el subdominio y fija `IPublicSiteContext.BusinessTenantId`. Sirve **únicamente** para leer datos **públicos** de esa empresa. **Nunca** da acceso a datos privados: para eso sigue mandando el claim `tenant_id` del token.
- **Un solo front** (el mismo SPA) atiende todos los subdominios: con un subdominio de empresa muestra la página pública; con el dominio principal, el sitio de la plataforma y los accesos.
- **Ingreso desde un subdominio:**
  - hay un solo servidor OIDC, con el **issuer fijo** en el dominio principal (`plataforma.com`);
  - `SubdomainApplicationManager<TApplication>` conserva las URI fijas y amplía sólo el cliente `web`, sin guardar comodines. `SubdomainRedirectUriValidator` acepta `/auth/callback` y `/silent-renew.html`, y `/` para la salida, en una sola etiqueta `<slug>.<host del issuer>`, con su mismo esquema y puerto, sin usuario, query ni fragmento. `IPublicSiteReader` exige una organización Business activa, con página publicada sin bloqueo y slug no reservado; la salida sin `client_id` usa la misma regla al buscar el cliente por su URI;
  - `authorize` y `logout` son navegaciones al dominio principal, donde vive la cookie de `/connect`; el canje del código, la renovación y `userinfo` van a `/connect/*` del **propio origen del subdominio**. En el front, `oidc-client-ts` usa `authority` = issuer, con un `metadataSeed` que apunta `token_endpoint`, `userinfo_endpoint` y `revocation_endpoint` al origen propio;
  - `prompt=none&access=consumer` recupera sólo un espacio personal existente: `ConnectController` usa `GetActiveUserAsync` y devuelve `login_required` si falta. El espacio nace al elegir Personal de forma interactiva. Business conserva `SelectAccessAsync`, incluso en silencio, para recordar la organización elegida;
  - la CSP permite enmarcar sólo `/connect/authorize` desde `<esquema>://*.<host del issuer>[:puerto]`. Un documento HTML del subdominio puede enmarcar el origen fijo del issuer; las demás respuestas conservan la política anterior. No se abre `connect-src` ni CORS.
- **Infraestructura:** el SPA y la Api se sirven desde el **mismo origen en cada host** (el dominio principal y cada `<slug>.plataforma.com`), así que **no hay CORS**, tampoco en los subdominios, y `BackendPrefixes` se atiende igual en todos los hosts. En producción, desde wwwroot, con DNS comodín `*.plataforma.com` y certificado comodín. En desarrollo, con el proxy de Vite por host, sobre `*.localtest.me` (resuelve a 127.0.0.1).
- **Reservados:** `www`, `app`, `api`, `auth`, `admin`, `empresas`, `plataforma`… no pueden ser el slug de una empresa (`ReservedTenantSlugs`).
- **Después, opcional:** dominio propio de una empresa (`www.empresa-a.com.ar`), que es la misma resolución por host, con una tabla de dominios verificados.

## 6. Decisiones

| Decisión | Valor |
|---|---|
| Aislamiento | Una base compartida, `TenantId`, filtros de EF con nombre y **RLS forzado** en `tenant`, `public_site` y `engagement` |
| Identidad | Global: una persona, una cuenta (correo y teléfono únicos) |
| Accesos | `access` = `consumer` \| `business` \| `platform` en el token. B2C nunca crea empresas; el alta B2B es "Registrá tu empresa" |
| Tenant activo | Solo del claim `tenant_id` (el espacio personal en B2C; la organización elegida en B2B). El host solo resuelve lo **público** |
| Empresas y roles | Empresas planas dentro de la organización, sin matriz ni herencia; roles con alcance `Organization`, `AnyCompany` o `SpecificCompany` (en pantalla, toda la organización o una empresa); `companyId` en la URL, nunca en el token (ADR 0012 y 0034) |
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

El [contexto de petición del backend](backend.md#8-contexto-de-la-petición) distingue ICurrentUser, ITenantContext, IRequestInfo e IPublicSiteContext. El host resuelve solo publicación; el claim identifica el alcance privado.

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

El seed inicial de `PlatformSettings` deja `ConsumerSignup = Open`, `BusinessSignup = Open` y `MaxOwnedOrganizations = 1`. Los cambios posteriores se hacen desde la plataforma y no se pisan al reiniciar.

**Registro de una persona (B2C):**
1. "Creá tu cuenta" → código (o Google).
2. Se crea la identidad y su espacio personal, con aceptación de términos.
3. Tokens con `access=consumer`.

**Registro de una empresa (B2B), "Registrá tu empresa":**
1. Quien la registra ingresa o crea su identidad (código por correo o WhatsApp, o Google, con la aceptación de términos) y después carga los datos de la organización: nombre, slug obligatorio con comprobación de disponibilidad y `ReservedTenantSlugs`, su primera empresa y, si quiere, el CUIT de esa empresa. Si ya tiene cuenta, entra con ella; si no, se crea la identidad. El slug se puede cambiar después en «Página pública» (`/org/pagina`, «Dirección de la página»).
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

- **Operador:** `access=platform`; el segundo factor llega en la E9. Su cuenta es exclusiva de Plataforma: no tiene perfiles de persona ni entra a una organización. Se lo invita por correo y sus permisos se eligen uno por uno, sin roles; el último operador activo con `platform.operators.manage` no se puede quitar, suspender ni dar de baja ([ADR 0042](../decisions/0042-operadores-de-plataforma.md)).
  - **Organizaciones:** aprueba o rechaza las que esperan aprobación (rechazar la deja cerrada), y las suspende, reactiva y cierra.
  - Prende y apaga módulos por organización (plan, manual o prueba con vencimiento).
  - Ve el dominio de correo verificado de cada organización (§3.1), y puede verificarlo o quitarlo con motivo. Normalmente lo registra y lo verifica la propia organización, con `settings.manage`.
  - Modera páginas públicas: puede despublicarlas con motivo, lo que bloquea volver a publicarlas hasta que la plataforma lo permita («Permitir publicar», también con motivo). Las dos acciones quedan en la auditoría (§7).
  - **Cuentas:** suspende y reactiva identidades, cierra sus sesiones e inicia la baja (§3.2, punto 4).
  - Atiende los pedidos de «Recuperar mi cuenta» (`/plataforma/recuperaciones`): los aprueba o los rechaza.
  - Publica versiones nuevas de los documentos legales, por idioma; nunca edita una ya publicada.
  - Administra a los otros operadores y la configuración de la plataforma (`PlatformSettings`).
  - Nunca ve datos privados ni compartidos: la auditoría registra cada acción con su motivo.

## 12. Caché, locks, unicidad

[Multitenancy](../rules/multitenancy.md) define claves por alcance y locks compartidos; [guardado](../rules/guardado.md), su orden transaccional. [Identidad](../features/identidad.md) conserva LoginMethods como fuente única de correos y teléfonos; la unicidad global y los índices están en [persistencia](../rules/persistencia-ef.md).

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
| `SubdomainTests` | retorno dinámico de web sólo al subdominio publicado; PKCE, issuer, token/userinfo/refresh/revocación del propio origen, salida y recuperación sin crear Personal |
| `PublicSiteTests`, `DirectoryTests` | contrato mínimo, 404 público uniforme, filtros de publicación/estado, logo revalidado y paginado/búsqueda reales |
| `PersonalIsolationTests`, `EngagementTests`, `PartyPolicyTests` | persona/persona y accesos separados, dos partes con copia mínima, tercero404, transición403, versiones, replay y gates |
| `Wrong_access_is_forbidden`, `Access_switch_requires_membership` | accesos y membresías |
| `AccountDeletionTests` | la baja requiere sesión y motivo, sin código adicional; el único Dueño no puede darse de baja; revoca las sesiones; durante la gracia el ingreso ofrece cancelar; la eliminación anonimiza, libera el correo, quita las membresías y anonimiza la copia en los datos compartidos; la auditoría queda |
| RLS, rol de runtime, inventario de políticas, columnas inmutables, caché con prefijo | igual que antes, ahora para las tres clases |
