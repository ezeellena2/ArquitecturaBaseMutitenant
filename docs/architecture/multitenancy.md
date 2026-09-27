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
| **Dueño de la empresa** | "Registrá tu empresa" (alta B2B aparte) | crea la organización y queda como **Administrador general** (todos los permisos) | — |
| **Operador de la plataforma** | acceso aparte, con segundo factor | backoffice: aprobar y suspender empresas, módulos por plan, cuentas, auditoría | ver los datos de negocio de una empresa o de una persona |

Dentro de una empresa, los usuarios se diferencian **solo por rol**: el Administrador general, el Administrador de una empresa del grupo y los roles que cree cada organización. La organización puede tener varias **empresas** (razones sociales o sedes).

## 3. Una cuenta, dos accesos

- **Identidad global:** una persona tiene **una sola cuenta** (un correo, un teléfono, un Google). No hay dos contraseñas ni dos registros.
- **Dos accesos que no se mezclan:**
  - **Acceso B2C:** su **espacio personal** (tenant `Kind=Personal`). Se crea solo al registrarse como persona o la primera vez que entra como persona.
  - **Acceso B2B:** sus **organizaciones** (tenants `Kind=Business`), por membresía. Solo existe si registró una empresa o lo invitaron a una.
- **Al ingresar se elige el acceso:** "como persona" o "como empresa". El sitio de la plataforma y las páginas públicas llevan al acceso B2C; el portal "Empresas" lleva al B2B. Con una sola organización, entra directo; con varias, elige cuál.
- **Cambiar de acceso** (de paciente a médico) no pide ingresar de nuevo: el menú de la cuenta tiene "Ir a mi empresa" o "Ir a mi espacio personal". Por dentro, son tokens nuevos del otro acceso.
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

- **Cada organización tiene un `Slug` único** (`empresa-a`), y su página vive en `https://empresa-a.plataforma.com`. El sitio de la plataforma (`plataforma.com`) tiene el buscador o directorio de empresas publicadas.
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
3. `TenantProvisioner` crea la configuración, los roles de sistema, la primera empresa, la membresía de Administrador general y la página pública en `Draft`.
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
- **Únicos globales:** el correo y el teléfono de la identidad, el `Slug` de la organización y el `PhoneNumberId` de WhatsApp.
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
| RLS, rol de runtime, inventario de políticas, columnas inmutables, caché con prefijo | igual que antes, ahora para las tres clases |
