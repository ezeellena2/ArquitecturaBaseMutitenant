# Plan de desarrollo: ArquitecturaBaseMultitenant (back + front)

> **Para agentes:** este es el plan maestro. La Etapa 0 está al nivel ejecutable. Cada una de las demás se detalla **al arrancarla**, con `superpowers:writing-plans`, en `docs/plans/AAAA-MM-DD-etapa-N-<tema>.md` (TDD, pasos de 2 a 5 minutos), porque depende de cómo quedó la anterior. Casillas `- [ ]` para el seguimiento.

**Objetivo:** construir desde cero la plantilla multitenant **B2B + B2C** con las convenciones de ArquitecturaBase. Incluye:
- una sola cuenta por persona con **dos accesos que no se mezclan**: como persona (B2C) y como empresa (B2B), como en Mercado Libre y para cualquier tipo de negocio;
- **páginas públicas por subdominio** y la **mecánica** para que una persona interactúe con una empresa (datos públicos y compartidos), sin módulos de negocio;
- aislamiento en dos barreras (EF + RLS);
- empresas, plataforma, auditoría, i18n, UTC y WhatsApp;
- **una sola forma de mostrar los datos** en todo el sistema.

El resultado tiene que servir para empezar productos reales.

**Arquitectura:**
- [`backend.md`](../architecture/backend.md), [`multitenancy.md`](../architecture/multitenancy.md) y [`arbol.md`](../architecture/arbol.md);
- en el front: `frontend.md`, `formatos.md` y `arbol.md`;
- las [decisiones](../decisions/README.md).

**Origen:** el intento del 2026-09-26 se descartó el 2026-09-27 y se arranca de cero. De él se toman solo ideas (RLS forzado, validar el rol de runtime al arrancar, provisioning en una transacción, inventario de políticas), nunca código.

## Reglas para todas las etapas

**Puerta de cada etapa.** No se pasa a la siguiente sin cumplir todo esto:
1. `dotnet build ArquitecturaBaseMultitenant.slnx` sin advertencias y `dotnet test` en verde.
2. En el front, `npm run build && npm run lint && npm test` limpios.
3. El inventario de rutas (`ExplicitRouteInventoryTests`) actualizado, con un test por cada ruta nueva.
4. Si la etapa toca datos de negocio: los tests de aislamiento de `Tenancy/` en verde y ampliados a las rutas nuevas.
5. Si la etapa muestra datos nuevos: se usan `shared/ui/format` y `shared/ui/fields`, y cada tipo nuevo tiene sus casos en `format-cases.json`.
6. `docs/contracts/openapi.json` y `src/shared/api/generated` regenerados y commiteados.
7. La documentación de la etapa, actualizada **en el mismo commit** que el código. **Arnés:**
   - toda carpeta nueva del mapa (`arnes.md` §3) tiene su `AGENTS.md` + `CLAUDE.md`;
   - toda regla nueva tiene su ficha en `docs/rules/` y su verificación;
   - los "Copiá de" marcados con esta etapa ya apuntan a archivos reales;
   - `HarnessTests` y `harness.test.ts` están en verde.
8. `aspire stop` si se levantó el AppHost.

**Forma de trabajo:**
- Commits chicos, en español, con conventional commits. **Cada tarea termina en un commit.**
- En el front, toda pantalla se dibuja y el usuario la aprueba antes de programarla.

---

## Mapa de etapas

| Etapa | Tema | Back | Front | Depende de |
|---|---|---|---|---|
| 0 | Esqueleto de las dos soluciones | ✔ | ✔ | — |
| 1 | Núcleo transversal: Result, errores, i18n y cultura, UTC, validación, logging, OpenAPI, **formatos unificados** | ✔ | ✔ | 0 |
| 2 | Persistencia multitenant: DbContext, interceptores, UoW, RLS, roles de BD | ✔ | — | 1 |
| 3 | Identidad global, OpenIddict, **accesos B2C / B2B / plataforma**, **registro de personas**, cambio de acceso, invitaciones, outbox, /api/me | ✔ | ✔ | 2 |
| 4 | Autorización y Roles (área de referencia) | ✔ | ✔ | 3 |
| 5 | Plataforma: organizaciones, aprobaciones, identidades, operadores, auditoría de seguridad | ✔ | ✔ | 4 |
| 6 | Área B2B: **"Registrá tu empresa"**, usuarios, empresas, membresías, configuración, auditoría, **mi página pública** | ✔ | ✔ | 4 |
| 7 | Área B2C y **sitio público**: espacio personal, cuenta, páginas por subdominio, directorio, ingreso desde un subdominio, y la **mecánica de interacción** persona ↔ empresa probada de punta a punta | ✔ | ✔ | 3 |
| 8 | WhatsApp (número de la plataforma: códigos, invitaciones, bot de ingreso) | ✔ | ✔ | 3 |
| 9 | Endurecimiento: TOTP para operadores, rate limit, headers, observabilidad, fuzz de aislamiento | ✔ | ✔ | 5–8 |
| 10 | Despliegue y operación: CI/CD, migration bundle, backup, runbook | ✔ | ✔ | 9 |
| 11 | Opcional: canales de WhatsApp por organización | ✔ | ✔ | 8 |

Las etapas 5, 6, 7 y 8 pueden avanzar en paralelo una vez cerrada la 4. Los módulos de negocio del producto (B2B y B2C) se suman después, cada uno con su plan, copiando Roles, **como módulos con `[FeatureGate]`** (P8).

**Antes de la Etapa 4:** los tableros de teléfono (390 px) de las pantallas del lienzo (P10), para que Roles, la feature de referencia, nazca adaptable.

**Estándares transversales:** P1 a P10 de [`estandares.md`](../architecture/estandares.md), adoptados el 2026-09-27, ya están repartidos en las tareas de cada etapa (marcados con su P#).

---

## Etapa 0: esqueleto (medio día a un día)

**Objetivo:** que las dos soluciones compilen vacías, con toda la maquinaria de calidad encendida, y que el AppHost levante Postgres, la Api y el front.

### Tarea 0.1: repo y archivos de raíz (back)
- [ ] `.gitignore`: la plantilla de VisualStudio más `.local/`, `*.env`, `**/appsettings.*.local.json` y `.artifacts/`.
- [ ] `global.json`, `Directory.Build.props`, `Directory.Packages.props` (copiados de ArquitecturaBase y completados con los paquetes que falten, según backend.md §1), `BannedSymbols.txt`, `.editorconfig` y `aspire.config.json`.
- [ ] Commit `chore: archivos de raíz y análisis estático`.

### Tarea 0.2: proyectos y referencias
- [ ] `ArquitecturaBaseMultitenant.slnx` con los 6 proyectos de `src/` y los 4 de `tests/`, y las referencias de la tabla de capas.
- [ ] `tests/Directory.Build.props` (xunit.v3, OutputType Exe).
- [ ] `dotnet build` sin advertencias. Commit `chore: solución y proyectos`.

### Tarea 0.3: Aspire y health
- [ ] Copiar `ServiceDefaults/Extensions.cs` de la base.
- [ ] `AppHost.cs`: Postgres en el puerto 5434 con volumen persistente, la base `appdb`, la Api y el front con `AddViteApp`.
- [ ] Api mínima (`AddServiceDefaults`, `MapDefaultEndpoints`) y `HealthCheckTests` (`/alive` → 200). Commit `feat: AppHost y health`.

### Tarea 0.4: tests de arquitectura de base
- [ ] `LayerDependencyTests`, `ProjectReferencesTests`, `ApplicationPackagesTests` y `MinimalApiRoutesTests`, copiados y adaptados de la base. Commit `test: reglas de capas`.

### Tarea 0.5: CI
- [ ] `.github/workflows/ci.yml` en los dos repos: en el back, build + test (con Docker); en el front, `npm ci`, build, lint y test. Commit `ci: build y test`.

### Tarea 0.6: front
- [ ] Borrar `node_modules`, `dist` y `.npm-cache` del scaffold viejo.
- [ ] `package.json` con las dependencias y versiones de ArquitecturaBaseFront, más `openapi-typescript` y `libphonenumber-js`. Además: `tsconfig*`, `vite.config.ts` (proxy, puerto 5174), `.oxlintrc.json`, `components.json`, `index.html` y `silent-renew.html`.
- [ ] Copiar de la base `index.css` (tokens), `shared/ui`, `shared/hooks`, `shared/lib` y `test/`. Sumar a `shared/ui` `FilterBar`, `StatusDot`, `Avatar`, `FormError`, `Surface` y `tabs.tsx` (shadcn).
- [ ] `App.tsx` con un router mínimo. build, lint y test limpios. Commit `chore: esqueleto del SPA`.

### Tarea 0.7: el arnés
- [ ] `HarnessTests` en ArchitectureTests y `src/test/harness.test.ts` en el front, con las cuatro verificaciones de `arnes.md` §5.
- [ ] Un `AGENTS.md` + `CLAUDE.md` (`@AGENTS.md`) en cada carpeta que crea la Etapa 0, según el mapa de `arnes.md` §3 (back) y §2 (front).
- [ ] `structure.test.ts` en el front (sin imports entre features ni entre áreas) y la regla `react/jsx-no-literals` en `.oxlintrc.json`.
- [ ] **Accesibilidad (P9):** `vitest-axe` instalado, `extend-expect` en `test/setup.ts` y el primer `toHaveNoViolations` en `App.test.tsx`.
- [ ] Commit `chore: arnés de reglas para agentes`.

**Puerta:** la general, más `aspire run` levantando los 3 recursos y `/alive` en verde.

---

## Etapa 1: núcleo transversal

**Objetivo:** que todo lo transversal exista y esté probado antes del primer caso de uso. **Incluye la presentación unificada de datos**, para que ninguna pantalla nazca formateando a mano.

**Back:**
1. `Domain/Results`, `Entity`, `ValueObject`, y los value objects `Money` y `CurrencyCode`, con sus tests.
2. `Api/ErrorHandling`: ProblemDetailsMapper, ControllerResultExtensions, GlobalExceptionHandler, MvcInvalidModelStateResponseFactory, ApiErrorCodes y EmptyJsonBodyContentTypeFilter.
3. **Cultura y textos:**
   - `SupportedCultures` (`es-AR`, `en-US`) y `LocalizationExtensions`;
   - `Errors.resx` y `Validation.resx` con sus `.en.resx`;
   - los envoltorios de textos, `ResourceParityTests` y `ErrorCodeTests`.
4. **Tiempo:** `TimeProvider`, `UtcDateTimeConverter`, conversores de `DateOnly` y `TimeOnly`, `ITimeZoneService` y `GET /api/time-zones`.
5. **Formatos:**
   - `Application/Common/Formatting/DisplayFormatter.cs`, con los perfiles `es-AR` y `en-US` (backend.md §18);
   - `docs/contracts/format-cases.json` con los casos del catálogo de `formatos.md`;
   - `DisplayFormatterTests`, que los recorre;
   - `MoneyJsonConverter` y la convención EF de `decimal` con precisión explícita (test: ningún `decimal` sin `HasPrecision`).
6. **Validación y paginado:** `IRequestValidator`, `ValidationRules` (con `ValidCulture`, `ValidCurrency` y `ValidTimeZone`) y `FieldErrors`. También `PagedRequest`/`PagedResult`, `CursorRequest`/`CursorResult`, sus validadores y `SortMap` + `ApplySort` (backend.md §9, "Paginado, orden y búsqueda").
7. **Logging:** `OperationLog` con `[LoggerMessage]`.
8. **OpenAPI:** la convención de problemas, la exportación a `docs/contracts/openapi.json` y `OpenApiContractTests`.
9. **Hosting:** ForwardedHeaders, SecurityHeaders, SpaExtensions y el pipeline completo.
10. `TestFeatures/TestController` y los tests de errores, localización, JSON UTC y `Money`.
11. **Datos de entrada (P3, P4):**
    - `TextNormalizer` + `NormalizedStringJsonConverter` global + `[RawText]`;
    - `Domain/Common/TextLimits.cs` y los tipos de texto de `ValidationRules`;
    - los value objects `Email` y `PhoneNumber` con sus casos en `format-cases.json`;
    - tests `TextNormalizerTests`, `NormalizedInputTests`, `TextLimitsTests`, `EmailTests` y `EmailPropertyTests`.
12. **Idempotencia (P6):** `platform.IdempotencyKeys`, `[Idempotent]` + `IdempotencyFilter`, el worker de vencidas, `IdempotencyTests` e `IdempotentActionsTests`.

**Front:**
1. `shared/api`: httpClient, ApiError, queryClient y formErrors.
2. `shared/i18n` (`common`, `errors`, `enums`) y `parity.test.ts`.
3. **`shared/format` completo:**
   - `cultureProfiles`, `formatters`, `parsers`, `useFormat` y `statusTones`;
   - `formatters.test.ts`, que recorre el **mismo** `format-cases.json` del back;
   - `format-usage.test.ts`.
4. **Paginado:** `usePagination` (vuelve a la página 1 y corrige una página fuera de rango), `useCursorList`, `Pagination` con el selector 10/20/50/100 (10 por defecto) y `LoadMore`.
5. **`shared/ui/format`** (DateText, MoneyText, NumberText, PercentText, EnumText, StatusBadge, EmptyValue…) y **`shared/ui/fields`** (DateField, MoneyField, NumberField, PercentField, PhoneField…). `DataTable` ya resuelve el formato por el `type` de cada columna.
6. `scripts/generate-contracts.mjs`, `npm run contracts` y `contracts:check`.
7. **Campos con forma propia:** `EmailField`, `PhoneField` con `shared/phone` (países, banderas SVG y `CountrySelect`) y `TaxIdField` (`stdnum`), más `useIdempotentMutation` (P6).
8. **Diseño adaptable (P10):** `DataTable` con tarjetas por `mobile`, `FilterBar` con el panel inferior, `Dialog` a pantalla completa, y `columns-mobile.test.ts`.

**Puerta:** la general. Además:
- cada `ErrorType` sale con su status y el `detail` traducido en es-AR y en-US;
- el back y el front producen **exactamente el mismo texto** para cada caso de `format-cases.json`.

---

## Etapa 2: persistencia multitenant (la base de todo; riesgo alto)

**Objetivo:** que sea **imposible** leer o escribir datos de otro tenant, aunque un bug se saltee EF.

**Back:**
1. `Domain/Common`: `IAuditable`, `ISoftDeletable`, `ITenantOwned`, `ICompanyOwned`, `IPublishedByBusiness` e `IConsumerBusinessShared`. `Domain/Tenancy`: `Tenant`, `TenantKind`, `TenantStatus`, `Member` y `MemberStatus`.
2. `ITenantContext` (con `TenantKind`), `ITenantScope` y `TenantContext`, con tests de las reglas de `Enter`.
3. `ApplicationDbContext` con los esquemas `platform`, `identity`, `tenant`, `public_site` y `engagement`, y los filtros con nombre `"Tenant"`, `"Public"`, `"Parties"` y `"SoftDelete"`. `PartyPolicy`.
4. Interceptores: TenantConnection, TenantStamp, SoftDelete, Auditable y AuditTrail (con `AuditEntry`).
5. `UnitOfWork` + `CommitPolicy`: set_config local, sin anidar, rollback y el error 23505.
6. RLS:
   - `EnableTenantRls`, `EnablePublicRls` y `EnablePartiesRls`;
   - los triggers `prevent_tenant_change` y `prevent_update_delete`;
   - `TenantIsolationModelValidator`.
7. Roles de BD: la cadena `appdb-admin`, `DatabaseBootstrapExtensions` y `RuntimeRoleValidator`. El AppHost entrega las dos cadenas.
8. `AdvisoryLockKeys` y `CacheKeys`, con prefijo obligatorio. `TenantJobRunner`.
9. **Búsqueda y paginado sobre Postgres:** `unaccent` + `pg_trgm` + `f_unaccent`, `ApplySearch`, `CursorCodec`, `ToCursorResultAsync` y los tests `PaginationTests`, `CursorPaginationTests`, `SearchTests` y `SortIndexTests`.
10. `TestFeatures/Isolation`: `Widget : ITenantOwned`, `Poster : IPublishedByBusiness` y `Deal : IConsumerBusinessShared` (la referencia de las tres clases, porque la plantilla no trae módulos de negocio), más los tests de multitenancy.md §11 que no necesitan HTTP, `TransactionBoundaryTests`, `EntityConfigurationTests` y `TenantScopeUsageTests`.
11. **Concurrencia (P1):** `IVersioned` con la convención a `xmin`, `ConcurrencyConflictException` en `UnitOfWork`, el 409 en el mapper, `ConcurrencyTests` y `VersionedContractTests`. `Widget` es `IVersioned`.
12. **Orden alfabético (P2):** base creada con ICU `es-AR` en `DatabaseBootstrapExtensions` y en el contenedor de los tests; `RuntimeRoleValidator` verifica la collation; `CollationTests`.

**Puerta:** la general, con `Rls_blocks_cross_tenant_even_with_filters_ignored` y `Runtime_role_is_not_privileged` en verde **en el CI**.

---

## Etapa 3: identidad, accesos y OpenIddict

**Back:**
1. `ApplicationUser` **global** (IsPlatformOperator, Status, Culture, TimeZoneId, LastAccess, LastBusinessTenantId), sin índice único de email ni teléfono: esos valores viven en `LoginMethods` (ver 6c-bis), y `Email`/`PhoneNumber` son solo una copia del método principal.
   - **Sin setters públicos:** las reglas de la cuenta (correo o teléfono obligatorio, largo del nombre, restaurar) viven en métodos de la entidad, con tests unitarios.
   - Un nombre demasiado largo es un error de validación, no un recorte silencioso.
   - `ISignInService` es solo técnico, con 12 miembros como máximo; los datos de la cuenta van por `IUserRepository`. Así queda como en las Etapas 2 y 7 del plan de ArquitecturaBase.
2. OpenIddict:
   - code + PKCE + refresh y el cliente `web`;
   - `OpenIdPrincipalFactory` (`access`, `tenant_id`, `tenant_kind`);
   - `ConnectController` con `tenant=` en authorize.
3. `TenantResolutionMiddleware`, `[Access]`, `CurrentUser` y `RequestInfo`. `ConnectService` con `access=` y `tenant=` en authorize (cambio de acceso sin volver a ingresar).
4. Ingreso sin contraseña por correo: códigos, enlaces, `LoginAudit` y rate limits. El canal del código y el de la invitación pasan por `ILoginCodeChannel` e `IInvitationChannel`: el núcleo trae solo `"email"` y WhatsApp se enchufa en la Etapa 8. `IPhoneLinkObserver` avisa los cambios de teléfono.
5. **Registro B2C:** identidad + tenant `Personal` + `Member(Owner)` + `TenantSettings` (cultura, zona y moneda del navegador).
6. Outbox persistente y **Gmail por SMTP**, que en desarrollo también envía de verdad (pickup `.eml` como opción), con plantillas de `Notifications.resx` y `DisplayFormatter`.
6b. **Ingreso y registro con Google** (`Authentication:Google:*`): si la cuenta no existe, crea la identidad y su espacio personal, como el registro por código (solo en el ingreso como persona). Vincular y desvincular Google desde la cuenta.
6c. **Configuración lista para pegar** ([`docs/operations/configuracion.md`](../operations/configuracion.md)): las mismas claves que ArquitecturaBase; `appsettings.Development.json` con los valores no secretos; los scripts de `scripts/secretos/` probados (importar de ArquitecturaBase, cargar desde un archivo, verificar); validación al arrancar con el nombre de la clave que falta.
6c-bis. **Métodos de ingreso (ADR 0033):**
   - `identity.LoginMethods` (correo, teléfono o Google, verificado, principal, `ManagedByTenantId?`);
   - ingresar con cualquier método verificado;
   - sumar, verificar, elegir el principal y quitar, con código en otro método y aviso en todos;
   - el aviso "Agregá un correo personal…" cuando la cuenta depende solo de métodos de una empresa;
   - al terminar una membresía, desactivar sus correos administrados y avisar;
   - tests `LoginMethodsTests` y `ManagedEmailTests` (un exmiembro no puede ingresar con el correo de la empresa).

   El **dominio verificado** de la organización (registro TXT) y "Recuperar mi cuenta" asistida por la plataforma van en la Etapa 5.
6d. **Términos y privacidad (P7):** `LegalDocuments` y `LegalDocumentContents` (versión 1 de términos y privacidad, con su texto en es y en, sembrada), `LegalAcceptances`, `acceptedTerms` en el registro (correo, WhatsApp y Google), `LegalAcceptanceMiddleware` y `LegalAcceptanceTests`. En el front, la casilla del registro y la pantalla bloqueante de aceptación.
6e. **Baja de la cuenta (ADR 0035, multitenancy.md §3.2):** estados `PendingDeletion` y `Deleted` en la identidad, `ReauthTicket`, `AccountDeletionPolicy`, `POST /api/me/deletion` y `POST /api/auth/deletion/cancel`, el ingreso durante la gracia, `AccountDeletionWorker`, `IAccountDeletionParticipant` con los participantes del núcleo (espacio personal, aceptaciones, outbox), y los avisos. Las membresías suman su participante en la Etapa 6, y la plataforma su "Dar de baja" en la Etapa 5. Tests `AccountDeletionTests`.
7. Invitaciones a una organización.
8. `GET /api/me` (cuenta, acceso activo, espacio personal, organizaciones, permisos y las preferencias efectivas de cultura, zona y moneda) y `PUT /api/me`.
9. Seed idempotente en **todos** los ambientes, dentro de un límite y con el advisory lock `seed:` para que dos réplicas no choquen. En desarrollo, además: operador; Empresa A con Ana y Kevin; Kevin y Carla como personas. Test: arrancar en `Production` contra una base migrada y vacía deja el cliente `web`, los ajustes de plataforma y el operador inicial.
10. Tests:
    - el recorrido real de ingreso;
    - el registro;
    - cambio de acceso u organización válido e inválido;
    - acceso equivocado → 403, y una persona no puede crear una empresa;
    - suspensiones.

**Front:**
1. `auth/` y `tenancy/`: elegir acceso al ingresar, "Ir a mi empresa" / "Ir a mi espacio personal" y elegir organización, con `queryClient.clear()` al cambiar.
2. `areas/public/auth`: ingreso, código, enlace, registro, callback e invitación.
3. Los tres layouts vacíos y `areas/personal/account`.
4. `useFormat` conectado a las preferencias de `/api/me`.

**Puerta:** la general, más un recorrido manual:
0. `./scripts/secretos/importar-desde-arquitecturabase.ps1` y `verificar.ps1` con todo `[ok]`;
1. registrarse como persona con un código que llega **de verdad** por Gmail, y otra vez con Google → queda en su espacio personal;
2. pasar a "Demo" con Ana;
3. F5 → sigue en "Demo";
4. volver a Personal;
5. cambiar la cultura a en-US → fechas y números cambian en todas partes;
6. logout.

---

## Etapa 4: autorización y Roles (área de referencia)

**Back:**
1. `Permissions` (organización y empresa), `PersonalPermissions`, `PlatformPermissions`, `Role`, `RoleScope`, `RoleAssignment`, `SystemRoles` y `Permissions.resx`.
2. `PermissionService` (efectivos por organización y empresa, con caché e invalidación) y `PlatformPermissionService`.
3. Los atributos `HasPermission`, `HasCompanyPermission` y `HasPlatformPermission`, su policy provider y handlers, y `PermissionAuthorizationTests`.
4. **RoleService completo como referencia:** listado paginado con filtros y conteos, get by id, create (201), update, delete, catálogo agrupado y protección de los roles de sistema.
5. Tests unitarios, de integración y de aislamiento.

**Front:** `areas/business/roles` (RolesPage con "Vale en" y RoleEditorPage), usando `DataTable` con columnas tipadas: es la feature de referencia.

**Documentación de la receta** (como las Etapas 4 y 5 de ArquitecturaBase):
- `docs/guides/agregar-un-area.md`: los pasos en orden, con la ruta de cada archivo y un enlace al equivalente de Roles, más una lista de verificación. Cubre B2B, B2C y los datos públicos o compartidos: cambian el `[Access]`, los permisos y la clase del dato.
- `docs/guides/permiso-nuevo.md`, `migracion.md` y `prefijo-de-backend.md`.
- `docs/features/roles.md`, y un `AGENTS.md` de una línea (más su `CLAUDE.md` con `@AGENTS.md`) en cada carpeta de código del área, que apunta a su documento.

**Puerta:** la general, más dos pruebas:
- **Probar la receta:** un subagente sin contexto sigue `agregar-un-area.md` y agrega un área de prueba (por ejemplo `Tags`) sin preguntar nada que la guía no responda. Se corrige la guía y se descarta el área.
- **Puerta de documentación a ciegas:** un agente que lee solo `AGENTS.md` contesta bien diez preguntas del tipo "¿dónde va X?". Las preguntas se escriben antes.

---

## Etapa 5: plataforma

**Back:**
1. `TenantAdministrationService`: listar, ver, aprobar, crear con invitación, suspender, reactivar, cerrar y reintentar el provisioning.
1b. **Módulos por organización (P8):**
    - `Microsoft.FeatureManagement.AspNetCore`, el catálogo `Features.cs`, `platform.TenantFeatures` y `TenantFeatureFilter` con caché;
    - `DisabledFeatureHandler` (404 ProblemDetails) e `IFeatureService`;
    - `features` en `/api/me` y la sección "Módulos" en la ficha de organización de la plataforma;
    - `FeatureGateTests` y `ModuleControllersTests`;
    - en el front, `useFeature`, `<Feature>` y `feature` en las rutas y la navegación.
2. Identidades: buscar y suspender, lo que revoca todas sus sesiones, y "Dar de baja" con motivo (ADR 0035).
3. `PlatformOperatorService`; el primer dueño sale del seed.
4. `SecurityEvent` + `PlatformAuditService` y `PlatformSettings` (`ConsumerSignup`, `BusinessSignup`, límite de organizaciones).
5. Tests:
   - un operador sin `Enter` no ve datos;
   - un usuario recibe 403 en `/api/platform`;
   - suspender una organización no afecta el acceso B2C de sus usuarios, y su página pública muestra "no disponible".

**Front:** `areas/platform/{tenants, accounts, operators, audit, settings}`, dibujadas primero.

---

## Etapa 6: área B2B (organización)

**Back:**
1. **"Registrá tu empresa"** (`POST /api/auth/business-signup`, desde el portal Empresas; nunca desde el acceso B2C), con `TenantProvisioner` idempotente, compartido con la plataforma. Elegir el slug y validar los reservados.
1b. **Mi página pública:** `PublicPage` (nombre, logo, descripción, contacto, slug), en borrador o publicada, editable por quien tenga `publicpage.manage`.
2. Usuarios, que parten de `Members`:
   - listado con filtros y conteos, y ficha;
   - invitar, editar, desactivar, reactivar y reenviar la invitación;
   - roles de organización;
   - protección del último TenantAdmin (una cuenta con la baja pedida no cuenta como Dueño);
   - el estado "Baja pedida" en el listado y la ficha, y el participante de la baja para las membresías (ADR 0035).
3. Empresas (CRUD, CUIT como `TaxId` validado (P5), zona horaria) y membresías de empresa con sus roles, con protección del último CompanyAdmin. `TaxId` + `ArgentineCuitValidator` + `TaxIdTests` y `TaxIdPropertyTests`. Las ediciones de usuario, rol y empresa llevan `version` (P1).
4. Configuración (cultura, zona y moneda por defecto) y auditoría, con listado traducido.
5. Tests de aislamiento para cada ruta nueva.

**Front:** `areas/business/{home, users, companies, settings, audit}` y `areas/personal/organizations`, dibujadas primero.

---

## Etapa 7: área B2C, sitio público e interacción

**Objetivo:** dejar el área personal lista para que cada producto le sume sus funcionalidades B2C con la misma receta que las B2B.

**Back:**
1. **Sitio público:**
   - `PublicSiteResolutionMiddleware` (subdominio → organización publicada), `[PublicSite]` y `ReservedSlugs`;
   - `SubdomainRedirectUriValidator` para ingresar desde un subdominio;
   - el directorio de páginas publicadas en el dominio principal;
   - desarrollo con `*.localtest.me` y el proxy de Vite por host.
1b. **Mecánica de interacción de punta a punta con `TestFeatures`:** una persona crea un `Deal` desde la página pública, la empresa lo ve en su bandeja y cada parte cambia estados según `PartyPolicy`. Es la guía para que un producto arme su módulo.
2. `docs/features/personal.md`, la receta de un módulo B2C:
   - entidad `ITenantOwned`;
   - rutas `[Access(Consumer)]` sin permisos, porque la persona tiene los `personal.*` implícitos;
   - un módulo que interactúa con empresas usa datos compartidos (`engagement`) y `PartyPolicy`;
   - tests de aislamiento entre personas, y entre el acceso B2C y el B2B de la misma persona.
3. `TestFeatures` con controllers `[Access(Consumer)]` y `[PublicSite]` que prueban esa receta.

**Front:** `PersonalLayout`, `areas/personal/home` y `navigation/personal.ts` listos para sumar módulos, dibujados primero **a 390 px antes que a 1440** (P10: el B2C se usa sobre todo en el teléfono).

---

## Etapa 8: WhatsApp como módulo quitable (número de la plataforma)

**Objetivo:** que WhatsApp nazca como módulo y un proyecto sin WhatsApp lo quite borrando carpetas y una línea. Es el mismo diseño que la Etapa 6 de ArquitecturaBase (backend.md §15).

**Back:**
1. Las carpetas `Domain/WhatsApp` y `Modules/WhatsApp` en Application, Infrastructure y Api, con un `AddWhatsAppModule()` por capa llamado desde `Program.cs`. Las configuraciones EF del módulo las aplica el propio módulo.
2. Adaptadores de los puertos del núcleo: `WhatsAppLoginCodeChannel`, `WhatsAppInvitationChannel` y `WhatsAppPhoneLinkObserver`.
3. Cliente de Cloud API y opciones validadas, con **las mismas claves `WhatsApp:*` que ArquitecturaBase** y los valores no secretos copiados de allá (`configuracion.md` §3). Envío por el outbox (canal `"whatsapp"`), con las plantillas de [`whatsapp-plantillas.md`](../operations/whatsapp-plantillas.md), creadas en Meta al empezar la etapa (la aprobación tarda). Registro B2C por WhatsApp.
4. Webhook con firma e idempotencia, procesador de entrada, bot de ingreso con enlace de un solo uso, retención de 90 días y health check.
5. Vincular el teléfono de la identidad, con verificación.
6. `ModuleIsolationTests`: el núcleo no referencia `*.Modules.*`.
7. `docs/features/whatsapp.md` y `docs/guides/quitar-whatsapp.md`.

**Front:** ingreso y registro por WhatsApp, `PhoneField` en la cuenta y `areas/platform/whatsapp`.

**Puerta:** la general, más un recorrido real: con el webhook de Meta apuntando al túnel del multitenant (`configuracion.md` §4), ingresar con un código por WhatsApp y escribirle al bot. Además, **la prueba de fuego**: en una copia descartable, quitar el módulo siguiendo `quitar-whatsapp.md`, y el build y los tests del núcleo tienen que quedar en verde.

---

## Etapa 9: endurecimiento

- TOTP obligatorio para operadores, con reautenticación reciente en las operaciones sensibles.
- Rate limit por tenant, por identidad y por IP (más estricto en las páginas públicas); cuotas en `PlatformSettings`.
- Security headers, CSP estricta y revisión de cookies.
- OpenTelemetry con `tenant.id` y `tenant.kind`.
- `/security-review` y **fuzz de aislamiento**: IDs cruzados en todas las rutas, con los dos accesos, en datos privados, públicos y compartidos, y por subdominio.

## Etapa 10: despliegue y operación

- Dockerfile de la Api con el SPA en wwwroot.
- Migration bundle ejecutado con `mt_owner`.
- CI/CD con GitHub Actions y OIDC.
- Secretos, certificados de OpenIddict y SMTP real.
- `docs/operations/runbook.md`: alta de `mt_app` **y creación de la base con ICU `es-AR`**, backup y restore, rotación de certificados, suspensiones, purga y baja de datos personales.
- **Datos personales (P7, segunda parte):** `POST /api/me/data-export`. La baja de la cuenta ya está en la Etapa 3.

## Etapa 11 (opcional)

- Canales de WhatsApp por organización (`WhatsAppChannels`).