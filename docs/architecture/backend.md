# Arquitectura del backend

Panorama de la plantilla B2B + B2C. Una persona tiene una cuenta y elige Persona o Empresa; Plataforma tiene operadores exclusivos. La plantilla aporta accesos y aislamiento, y cada producto agrega sus módulos. Las reglas canónicas están en [AGENTS.md](../../AGENTS.md) y [fichas](../rules/README.md); el mapa, en [arbol.md](arbol.md). La conducta vigente está en [fichas funcionales](../features/roles.md).

## 1. Stack

| Tema | Elección |
|---|---|
| Runtime | .NET 10 (`global.json` SDK 10.0.400, `rollForward: latestFeature`, runner Microsoft.Testing.Platform) |
| Web | ASP.NET Core MVC (controllers). Sin Minimal APIs de negocio |
| Datos | PostgreSQL 18 + EF Core 10 (Npgsql). Una base, esquemas `platform`, `identity`, `tenant`, `public_site` y `engagement` |
| Identidad | ASP.NET Core Identity (solo usuarios, sin roles de Identity) + OpenIddict 7 (servidor y validación en la misma Api) |
| Validación | FluentValidation 12 |
| Caché | `HybridCache`: en memoria hasta la Etapa 9; desde ahí, Redis como caché compartida entre instancias |
| Orquestación local | Aspire 13.5 (AppHost + ServiceDefaults) |
| Observabilidad | `Microsoft.Extensions.Logging` + OpenTelemetry (sin Serilog) |
| Tests | xUnit v3, Testcontainers (`postgres:18.x`), NetArchTest + Mono.Cecil, `FakeTimeProvider`, dobles a mano (sin Moq ni FluentAssertions) |
| Build | `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, BannedApiAnalyzers, Central Package Management |

Las versiones son las mismas de `ArquitecturaBase/Directory.Packages.props`. `NodaTime` no se usa: alcanza con `TimeZoneInfo` e IDs IANA.

## 2. Solución y proyectos

```
ArquitecturaBaseMultitenant.slnx
├─ src/
│  ├─ ArquitecturaBaseMultitenant.Domain            (sin paquetes, solo BCL)
│  ├─ ArquitecturaBaseMultitenant.Application       → Domain
│  ├─ ArquitecturaBaseMultitenant.Infrastructure    → Application
│  ├─ ArquitecturaBaseMultitenant.Api               → Application, Infrastructure (solo Program.cs), ServiceDefaults
│  ├─ ArquitecturaBaseMultitenant.ServiceDefaults   (IsAspireSharedProject)
│  └─ ArquitecturaBaseMultitenant.AppHost           → Api (recurso Aspire)
└─ tests/
   ├─ ArquitecturaBaseMultitenant.Domain.UnitTests
   ├─ ArquitecturaBaseMultitenant.Application.UnitTests
   ├─ ArquitecturaBaseMultitenant.Api.IntegrationTests   (Api + persistencia + RLS, con Testcontainers)
   └─ ArquitecturaBaseMultitenant.ArchitectureTests
```

**`tools/` solo tiene el soporte del E2E real** (`RealE2ESetup`, que prepara su base aislada), nunca algo para operar ([ADR 0017](../decisions/README.md), enmienda del 2026-10-03):
- El primer operador de plataforma sale del seed (`Seed:PlatformOwner`), como el `InitialAdmin` de la base.
- El rol de base de datos en runtime lo crea el bootstrap de desarrollo, o el DBA en producción.
- Las migraciones fuera de Development se aplican con un migration bundle.

Si más adelante hace falta una CLI, la decisión va con su ADR.

## 3. Capas y dependencias

Lo verifica `ArchitectureTests`.

| Proyecto | Puede referenciar | Contiene |
|---|---|---|
| Domain | nada | entidades, value objects, `<Entidad>Errors`, `Result`, catálogos de permisos, enums; `CurrencyCode` valida sintaxis y `Money.Round(minorUnits)` recibe un dato |
| Application | Domain, FluentValidation, algunos `Microsoft.Extensions.*` (lista blanca en `ApplicationPackagesTests`) | interfaces y servicios de casos de uso, modelos `*Request/*Response/*Row`, validadores, puertos de persistencia e integración, resources |
| Infrastructure | Application, Domain | EF Core, `ApplicationDbContext`, interceptores, repositorios, readers, Identity, OpenIddict, correo, WhatsApp, outbox, workers, caché, RLS |
| Api | Application, Infrastructure (solo desde `Program.cs`), ServiceDefaults | controllers, contratos HTTP, autorización, adaptadores de la petición, manejo de errores, hosting del SPA |

**El recorrido de un request de negocio es fijo:**

```
Api/Controllers
  → Application/Interfaces/Services        (I<X>Service)
    → Application/Services/<Área>          (<X>Service + helpers)
      → Application/Interfaces/Persistence (I<X>Repository, I<X>Reader, IUnitOfWork)
      → Application/Interfaces/Integrations/<Tema>
        → Infrastructure
```

Prohibido:
- `ICommand`, `IQuery`, handlers, MediatR, `Application/Features`, repositorio genérico, Scrutor, AutoMapper, Minimal APIs de negocio;
- que `IQueryable` o `Expression<>` salgan de Infrastructure;
- que Application use `HttpContext`, EF o `ApplicationDbContext`.

Los límites y sufijos de helpers están en [capas y flujo](../rules/capas-y-flujo.md). Cada capa registra sus implementaciones; Api compone.

## 4. Carpetas y archivos que marcan el patrón

El mapa único está en [arbol.md](arbol.md). Roles es el [área de referencia](../features/roles.md).

### 4.1 Domain

[Capas y flujo](../rules/capas-y-flujo.md) y [persistencia](../rules/persistencia-ef.md).

### 4.2 Application

[Capas y flujo](../rules/capas-y-flujo.md), [guardado](../rules/guardado.md) y [validación](../rules/validacion.md).

### 4.3 Infrastructure

[Persistencia](../rules/persistencia-ef.md), [módulos](../rules/modulos.md) y [configuración](../operations/configuracion.md).

### 4.4 Api

[API HTTP](../rules/api-http.md) y [permisos](../rules/permisos.md).

### 4.5 AppHost y ServiceDefaults

AppHost orquesta el entorno; ServiceDefaults reúne observabilidad y health.

### 4.6 Tests

[Tests](../rules/tests.md) y [arnés](arnes.md).

## 5. Patrón MVC: controller → servicio → repositorio

### Controller

[API HTTP](../rules/api-http.md): contrato → modelo → servicio → respuesta.

### Servicio

[Capas y flujo](../rules/capas-y-flujo.md) y [guardado](../rules/guardado.md).

### Repositorio, reader y UnitOfWork

[Persistencia](../rules/persistencia-ef.md), [guardado](../rules/guardado.md) y [concurrencia](../rules/concurrencia.md).

## 6. Result pattern y manejo de errores

[Result y errores](../rules/result-y-errores.md) define códigos, tipos y ProblemDetails; [validación](../rules/validacion.md) define errores por campo. Los mappers traducen el resultado al contrato HTTP común.

## 7. Inyección de dependencias

| Capa | Punto de entrada | Qué registra |
|---|---|---|
| Application | `AddApplication()` | Un `<Área>Registration.cs` por área en `Services/<Área>/`, con sus options (`BindConfiguration + ValidateDataAnnotations + ValidateOnStart`) y cada servicio y helper con `AddScoped`, uno por uno y una sola vez; validadores (`AddValidatorsFromAssembly(includeInternalTypes: true)`), `IRequestValidator` y `ReferenceDataService` (los puertos viven aquí, los implementa Infrastructure) |
| Infrastructure | `AddInfrastructure(cfg, env)` | `JsonReferenceDataCatalog` para los cinco puertos y `TryAddSingleton(TimeProvider.System)`, sin base. `ReferenceDataReader`/HybridCache y `ReferenceDataSeeder` reemplazan el adaptador JSON; se agregan `TenantContext` (scoped, un registro para `ITenantContext` y otro para `ITenantScope`), interceptores (en orden: TenantStamp → SoftDelete → Auditable → AuditTrail; más TenantConnection), `AddDbContext`, `IUnitOfWork`, repositorios, readers y los subregistros `AddIdentityServices`, `AddOpenIddictServer`, `AddMessaging`, `AddCaching`. Los módulos se registran aparte, con su `AddWhatsAppModule()` en cada capa, llamado desde `Program.cs` |
| Api | `AddPresentation()` | ProblemDetails, `GlobalExceptionHandler`, autorización (policy provider y handlers), localización, rate limiting, OpenAPI, `AddControllers` con un solo `ConfigureJson` |

- Sin Scrutor y sin registrar por reflexión.
- Lifetimes:
  - **scoped**: servicios, repositorios, readers, `TenantContext` y `CurrentUser`;
  - **singleton**: `TimeProvider`, clientes HTTP tipados, `CacheKeys` y parsers sin estado.
- Un worker (`BackgroundService`) crea su propio scope por unidad de trabajo y fija el tenant con `ITenantScope`.

## 8. Contexto de la petición

| Interfaz (Application) | Implementación | Fuente |
|---|---|---|
| `ICurrentUser` | `Api/RequestContext/CurrentUser` | claims `sub` y `access` (`consumer`, `business` o `platform`) |
| `IPublicSiteContext` | `Api/Tenancy/PublicSiteContext` | `BusinessTenantId?` y `Slug?` de la organización publicada y activa del subdominio; **solo** para leer lo público |
| `ITenantContext` | `Infrastructure/Persistence/TenantContext` | tenant del acceso activo (`TenantId` y `TenantKind`: el espacio personal en B2C, la organización en B2B). Lo carga `TenantResolutionMiddleware` desde los claims, o `ITenantScope.Enter(tenantId)` en plataforma, workers y altas |
| `IRequestInfo` | `Api/RequestContext/RequestInfo` | IP (después de ForwardedHeaders) y user agent |

- `ITenantContext.TenantId` **nunca** sale de un header, del body ni de la query.
- `PublicSiteResolutionMiddleware` consulta `IPublicSiteReader` una vez por pedido y solo para rutas `[PublicSite]`. El host debe ser exactamente `<slug>.<host del issuer>`, sin distinguir mayúsculas y con el mismo puerto; `TenantSlug.Create` valida la etiqueta y el reader descarta los nombres reservados. Sin sitio, `[PublicSite]` devuelve 404 `PublicSite.PublicPage.NotFound` con `traceId`. El contexto público nunca modifica `ITenantContext` y todavía no usa caché (Etapa 9).
- Las rutas de plataforma reciben el `tenantId` en la URL como **objetivo**; el servicio de plataforma, después de autorizar, entra con `ITenantScope.Enter(tenantId)`.

## 9. Persistencia

Un ApplicationDbContext compone el núcleo y los contribuidores instalados. Los esquemas y barreras están en [multitenancy](multitenancy.md#9-barreras); entidades, índices y convenciones en [persistencia EF](../rules/persistencia-ef.md). La guía de [migración](../guides/migracion.md) contiene el comando y la revisión del SQL. DesignTime construye el mismo modelo sin arrancar la Api ni conectar una base. Development aplica migraciones con la conexión administrativa; producción usa un migration bundle antes del tráfico.

### Paginado, orden y búsqueda

[Paginado y búsqueda](../rules/paginado-y-busqueda.md) define páginas, cursores, orden estable e índices. Los readers proyectan; los repositorios conservan entidades tracked para escribir.

## 10. Auditoría

[Auditoría](../rules/auditoria.md) distingue marcas de fila, AuditEntries privados y SecurityEvents globales. Los cambios y su rastro se confirman juntos; los lectores traducen sin reconstruir datos personales.

## 11. Fechas, UTC y zonas horarias

[Fechas y zonas](../rules/fechas-y-zonas.md) define TimeProvider, UTC, fechas civiles y conversión temporal.

## 12. Idioma, traducciones y resources

[Textos y traducciones](../rules/textos-y-traducciones.md) define cultura, resources es/en y voseo. [Datos de referencia](../rules/datos-de-referencia.md) aporta opciones, relaciones y patrones. Accept-Language gobierna cada petición; los workers reciben cultura explícita.

## 13. Autenticación: Identity + OpenIddict

- OpenIddict 7 corre como **servidor en la misma Api**:
  - Authorization Code + PKCE (obligatorio) + refresh tokens. Sin client credentials.
  - Endpoints `connect/authorize|token|logout|userinfo|revoke`.
  - Scopes `openid profile email offline_access api`.
- **Un solo cliente público, `web`**, para el SPA. El área la decide el claim `access`: `consumer` → espacio personal y páginas públicas; `business` → la organización; `platform` → el backoffice. `SubdomainApplicationManager<TApplication>`, registrado como genérico abierto de OpenIddict, conserva las URI fijas y delega a `SubdomainRedirectUriValidator` únicamente los retornos dinámicos de `web`: `/auth/callback`, `/silent-renew.html` y `/` para la salida. Exige una etiqueta válida sobre el host, esquema y puerto del issuer, sin userinfo, query ni fragmento, con publicación activa y no bloqueada; también amplía la búsqueda de cliente para salir sin `client_id`. No guarda comodines ni desactiva validaciones del protocolo.
- **El issuer es fijo** (el dominio principal). Desde un subdominio, `authorize` y `logout` son navegaciones al dominio principal, donde vive la cookie de `/connect`; el canje del código, la renovación y `userinfo` van a `/connect/*` del propio origen del subdominio (multitenancy.md §5).
- **Recuperación silenciosa en un subdominio:** `ConnectController` usa `GetActiveUserAsync` para `consumer` con `prompt=none`; una cuenta sin espacio personal recibe `login_required` sin crearlo. El ingreso interactivo conserva el alta, y Business mantiene `SelectAccessAsync` para recordar la organización al cambiar de perfil en silencio. La CSP amplía sólo `frame-ancestors` de `/connect/authorize` y `frame-src` del HTML de un subdominio con el esquema y puerto del issuer; las demás directivas y respuestas no cambian. `SubdomainTests` recorre PKCE, issuer y claims, token/userinfo/refresh/revocación del propio origen, salida con y sin cliente/hint y límites de publicación; `SecurityHeadersTests` fija la apertura limitada.
- Duraciones: código 5 min, access token 15 min, refresh 30 días con rotación. `EnableTokenEntryValidation` permite la revocación inmediata.
- **Claims** (`OpenIdPrincipalFactory`): `sub`, `name`, `email`, `access` (`consumer` | `business` | `platform`) y el tenant del acceso activo: `tenant_id` y `tenant_kind` (`personal` | `business`). `access`, `tenant_id` y `tenant_kind` viajan tanto en el access token como en el id token firmado: el SPA lee este último para elegir la ruta del acceso activo. **Ni los permisos ni los roles viajan en el token**: se consultan en vivo, con caché.
- Cookies de Identity solo para `/account` y `/connect` (HttpOnly, Secure, SameSite Lax). La Api usa bearer.
- La cookie de cada ingreso lleva un identificador aleatorio de sesión de navegador. Cada autorización OpenIddict conserva ese identificador; al salir se revocan todas las autorizaciones y refresh tokens de esa sesión, incluidos los accesos anteriores, sin cerrar las sesiones de otros dispositivos. La renovación conserva el identificador de autorización.
- La autorización conserva también `access` y, si corresponde, `tenant_id`, para que `TokenRevoker` pueda revocar un acceso o una organización. `RevokeTenantAsync` aún recorre todas las autorizaciones; antes de conectarlo a la suspensión en la E9 se debe reemplazar ese recorrido por una búsqueda indexada.
- Si la renovación del token falla, el SPA inicia una autorización con `prompt=login`: `ConnectController` presenta el formulario aunque la cookie de Identity siga viva y quita ese prompt del `returnUrl` interno. Tras verificar el código, el authorize reanudado usa la cookie nueva sin volver al formulario.
- En cada request autenticado, `TenantResolutionMiddleware` verifica (con caché de 60 s, invalidado al cambiar de estado) que la identidad, la membresía y el tenant del acceso activo estén activos. Suspender una organización revoca los tokens emitidos para ella; suspender una identidad, todos los suyos, en los dos accesos.

Los recorridos de ingreso, Google, métodos, invitaciones, enlaces y recuperación están en [Identidad](../features/identidad.md); el modelo de [una cuenta con dos accesos](multitenancy.md#3-una-cuenta-dos-accesos) explica la elección del perfil. Los [operadores](../features/plataforma.md) usan cuentas exclusivas de Plataforma. La cookie pública permite iniciar OIDC; no sustituye bearer para las rutas privadas. Un cambio de acceso emite tokens nuevos, y el cliente limpia sus consultas antes de montar el contexto nuevo.

## 14. Autorización

[Permisos](../rules/permisos.md) define los cuatro catálogos, la autorización por acceso y empresa y sus policies. [Roles](../features/roles.md) explica los alcances de organización y empresa. [Plataforma](../features/plataforma.md) define los permisos individuales de operadores. Los tokens identifican el acceso; los permisos se consultan en vivo.

## 15. WhatsApp: un módulo quitable

[Módulos](../rules/modulos.md) define el límite quitable; [WhatsApp](../features/whatsapp.md) describe disponibilidad, webhook, bot, mensajes y cuotas. [Configuración](../operations/configuracion.md) y [plantillas](../operations/whatsapp-plantillas.md) explican la operación. El [Asistente](../features/asistente.md) usa la misma composición neutral.

## 16. Pipeline HTTP (`Program.cs`)

```
UseForwardedHeaders → UseSecurityHeaders → UseRequestLocalization → UseExceptionHandler → UseStatusCodePages
→ UseRateLimiter → UseAuthentication → TenantResolutionMiddleware → PublicSiteResolutionMiddleware
→ LegalAcceptanceMiddleware → FeatureGateMiddleware → UseAuthorization
→ (Development: OpenAPI y Swagger UI | resto: UseHsts) → UseHttpsRedirection
→ UseStaticFiles (si hay wwwroot) → MapDefaultEndpoints → MapControllers → UseSpaFallback
```

Todo middleware que pueda cortar con un error va después de `UseStatusCodePages`.
Antes del pipeline, `Program.cs` prepara la base: en Development, el bootstrap (roles, base ICU y migraciones); fuera de él, la validación del rol de runtime; y el seed, salvo en Testing. `FeatureGateMiddleware`  oculta con 404 un módulo apagado después de validar el acceso y antes de autorizar ([modulos-habilitados](../rules/modulos-habilitados.md)).

`PublicSiteResolutionMiddleware` resuelve el host solo si el endpoint declara `[PublicSite]`. `PublicSiteReader` une la página pública con la organización, sin ignorar filtros ni cambiar el contexto privado: exige página publicada, sin bloqueo de moderación, organización Business activa y slug no reservado. Las rutas privadas no consultan ese reader.

## 17. Logging, OpenAPI, health, rate limiting, caché

- **Logging:** `[LoggerMessage]` obligatorio (`CA1848` como warning, que rompe el build). Nunca se registran códigos, tokens, enlaces, emails completos ni teléfonos. Cada log de negocio lleva un scope con `TenantId`.
- **OpenAPI:** `/openapi/v1.json` y Swagger UI solo en Development. El build exporta `docs/contracts/openapi.json` con `Microsoft.Extensions.ApiDescription.Server`, como en ArquitecturaBase; el front lo usa para generar sus tipos. El CI falla si el archivo commiteado no coincide con el generado.
- **Health:** `/health` (readiness: base de datos, rol de runtime y WhatsApp) y `/alive`.
- **Rate limiting:**
  - por IP: `login-code`, `login-verify`, `invitation-accept` y `whatsapp-webhook`;
  - por organización: `tenant-api`, una ventana deslizante sobre `/api`.
  - Un 429 sale como ProblemDetails con `retryAfter` en segundos; `shared/api/httpClient` del front lo convierte en `ApiError.retryAfterSeconds` para la cuenta regresiva del botón.
- **Caché:** HybridCache con claves de `CacheKeys`, siempre con prefijo: `t:{tenantId}:` (privado), `s:{businessTenantId}:` (página pública; se invalida al publicar), `u:{userId}:` (identidad y accesos) o `p:` (plataforma). Un test verifica que ninguna clave se arme sin prefijo.

## 18. Representación y formato de datos (unificado)

### Contrato en la API (JSON)

[Números y moneda](../rules/numeros-y-moneda.md), [fechas](../rules/fechas-y-zonas.md), [teléfonos](../rules/telefonos.md), [identificación fiscal](../rules/identificacion-fiscal.md) y [API HTTP](../rules/api-http.md) definen la representación.

### Formato del lado del backend

DisplayFormatter recibe cultura y zona explícitas. El [catálogo común de formatos](../../../ArquitecturaBaseMutitenantFront/docs/rules/formatos.md) describe la presentación; los dos repos consumen [los mismos casos](../contracts/format-cases.json). La precisión técnica IA se limita a [ADR 0051](../decisions/0051-precision-tecnica-de-costos-ia.md).

## 19. Front y hosting

- El SPA vive en `../ArquitecturaBaseMutitenantFront`. El SPA y la Api se sirven desde el **mismo origen en cada host**, el dominio principal y cada `<slug>.plataforma.com`: en producción desde wwwroot, con DNS y certificado comodín; en desarrollo, con el proxy de Vite por host (`*.localtest.me`). **No hay CORS**, tampoco en los subdominios.
- `BackendPrefixes` es una lista a mano, y se atiende igual en todos los hosts: `/api`, `/account`, `/connect`, `/signin-google`, `/.well-known`, `/webhooks`, `/health` y `/alive`. También reserva `/swagger` y `/openapi` para que fuera de Development devuelvan 404 en vez del SPA; sus endpoints y el proxy de Vite solo existen en Development. Un prefijo nuevo se suma ahí, al `SpaHostingTests` y al `server.proxy` de `vite.config.ts`.
- En desarrollo, el issuer es `https://localtest.me:5174/`: AppHost pasa el mismo valor a Api, Vite y RealE2ESetup. Los callbacks fijos son del dominio principal; los dinámicos conservan origen y puerto del subdominio publicado. Google registra `https://localtest.me:5174/signin-google`. No se siembran sitios de ejemplo en Development.
- La arquitectura del front está en `../ArquitecturaBaseMutitenantFront/docs/architecture/frontend.md`.

## 20. Reglas de datos que se aplican solas

Las convenciones de JSON, EF y MVC aplican las [fichas](../rules/README.md). El inventario transversal está en [estándares](estandares.md). Una feature usa sus piezas existentes y copia Roles; no duplica validación, normalización, formatos ni guardado.
