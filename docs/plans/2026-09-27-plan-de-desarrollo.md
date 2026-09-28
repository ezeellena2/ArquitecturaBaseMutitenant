# Plan de desarrollo: ArquitecturaBaseMultitenant (back + front)

> **Para agentes:** este es el plan maestro. La Etapa 0 está al nivel ejecutable. Cada una de las demás se detalla **al arrancarla**, con `writing-plans`, en `docs/plans/AAAA-MM-DD-etapa-N-<tema>.md` (TDD, pasos de 2 a 5 minutos), porque depende de cómo quedó la anterior. Casillas `- [ ]` para el seguimiento.

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

**Puerta de cada etapa.** No se pasa a la siguiente sin cumplir los puntos cuyas piezas ya nacieron; cada verificación empieza en la etapa que se indica:
1. `dotnet build ArquitecturaBaseMultitenant.slnx` sin advertencias y `dotnet test` en verde.
2. En el front, `npm run build && npm run lint && npm test` limpios.
3. Desde la E1, el inventario de rutas (`ExplicitRouteInventoryTests`) actualizado, con un test por cada ruta nueva.
4. Si la etapa toca datos de negocio: los tests de aislamiento de `Tenancy/` en verde y ampliados a las rutas nuevas.
5. Desde la E1, si la etapa muestra datos nuevos: se usan `shared/ui/format` y `shared/ui/fields`, y cada tipo nuevo tiene sus casos en `format-cases.json`.
6. Desde la E1, `docs/contracts/openapi.json` y `src/shared/api/generated` regenerados y commiteados, con su chequeo en CI.
7. La documentación de la etapa, actualizada **en el mismo commit** que el código. **Arnés:**
   - toda carpeta nueva del mapa (`arnes.md` §3) tiene su `AGENTS.md` + `CLAUDE.md`;
   - toda regla nueva tiene su ficha en `docs/rules/` y su verificación;
   - los "Copiá de" marcados con esta etapa ya apuntan a archivos reales;
   - `HarnessTests` y `harness.test.ts` están en verde; `HarnessStage` se sube al cerrar la etapa y exige los tests nombrados en las fichas hasta esa etapa.
8. `aspire stop` si se levantó el AppHost.
9. Desde la E3, si la etapa programa pantallas: **comparación visual con el lienzo**. Por cada pantalla y cada estado de su tablero en `docs/design/lienzo/`, una captura de la pantalla real y otra del tablero, las dos a 1440 × 900 y a 390 × 844 (Playwright), guardadas en `docs/design/capturas/etapa-N/`. Tienen que coincidir: la estructura, los textos, el orden, los colores (tokens de tema.md), los controles y los estados. Cualquier diferencia se corrige, o se anota con su motivo en el informe de la etapa para que el usuario la apruebe. **No se inventa nada que el tablero no tenga:** ni campos, ni textos, ni pantallas, ni acciones. Si falta algo, se dibuja primero.

**Forma de trabajo:**
- Commits chicos, en español, con conventional commits. **Cada tarea termina en un commit.**
- En el front, toda pantalla se dibuja y el usuario la aprueba antes de programarla. **Las pantallas ya aprobadas son los tableros de `docs/design/lienzo/`:** se programan copiando su estructura, sus textos y sus estados.

---

## Mapa de etapas

| Etapa | Tema | Back | Front | Depende de |
|---|---|---|---|---|
| 0 | Esqueleto de las dos soluciones | ✔ | ✔ | — |
| 1 | Núcleo transversal: Result, errores, i18n y cultura, UTC, validación, logging, OpenAPI, **formatos unificados** | ✔ | ✔ | 0 |
| 2 | Persistencia multitenant: DbContext, interceptores, UoW, RLS, roles de BD | ✔ | — | 1 |
| 3 | Identidad global, OpenIddict, **accesos B2C / B2B / plataforma**, **registro de personas** (con código y con Google), cambio de acceso, **métodos de ingreso, términos y baja de la cuenta**, invitaciones, outbox, /api/me. En tres partes: 3a Ingreso, 3b La cuenta y 3c Invitaciones | ✔ | ✔ | 2 |
| 4 | Autorización y Roles (área de referencia) | ✔ | ✔ | 3 |
| 5 | Plataforma: organizaciones, aprobaciones, **módulos por organización**, moderación de páginas públicas, dominio verificado, identidades (con la baja iniciada por la plataforma), recuperaciones de cuenta, documentos legales, operadores, auditoría de seguridad | ✔ | ✔ | 4 y 6 |
| 6 | Área B2B: **"Registrá tu empresa"**, usuarios, empresas, membresías, configuración, auditoría, **mi página pública** | ✔ | ✔ | 4 |
| 7 | Área B2C y **sitio público**: el área personal lista para sumar módulos (receta `personal.md`), páginas por subdominio, directorio, ingreso desde un subdominio, y la **mecánica de interacción** persona ↔ empresa probada de punta a punta | ✔ | ✔ | 6 |
| 8 | WhatsApp (número de la plataforma: códigos, invitaciones, avisos de la cuenta, bot de ingreso) | ✔ | ✔ | 3 |
| 9 | Endurecimiento: TOTP para operadores, rate limit, headers, observabilidad, fuzz de aislamiento | ✔ | ✔ | 5–8 |
| 10 | Despliegue y operación: CI/CD, migration bundle, backup, runbook, exportar mis datos | ✔ | ✔ | 9 |
| 11 | Opcional: canales de WhatsApp por organización | ✔ | ✔ | 8 |

Las etapas 6, 7 y 8 pueden avanzar en paralelo una vez cerrada la 4; la 5 va después de la 6. La 5 usa `TenantProvisioner` de la 6 (aprobar, crear con invitación y reintentar el provisioning), `PublicPage` (moderar la página pública) y `TenantDomains` de la 6 (administrar el dominio verificado desde la plataforma). El dominio de correo y `TenantDomains` nacen y se cierran en la Configuración de la 6. La 7 necesita la página pública de la 6 (`PublicPage` publicada, el slug y los reservados). Los módulos de negocio del producto (B2B y B2C) se suman después, cada uno con su plan, copiando Roles, **como módulos con `[FeatureGate]`** (P8).

**Pantallas por etapa:** la fuente es el [lienzo versionado en el front](../../../ArquitecturaBaseMutitenantFront/docs/design/lienzo/README.md) (versión 35, 67 tableros `.dc.html`, en escritorio y teléfono), con el tema de [`tema.md`](../../../ArquitecturaBaseMutitenantFront/docs/architecture/tema.md). Cada etapa programa las suyas **copiando el tablero**, que manda sobre las descripciones textuales, sin rediseñar. Si una pantalla necesita algo que el tablero no tiene, primero se dibuja y se aprueba.

| Etapa | Tableros del lienzo |
|---|---|
| 0–1 | el tema, los componentes base, Botones, Palabras y Avisos (errores genéricos) |
| 3 | Landing, Recorridos y Mapa-Ingreso (de referencia, no se programan), Ingreso (las dos puertas y sus estados, con el paso del código, salvo los de WhatsApp y los del operador), Registro (salvo WhatsApp), Sesion, Invitacion (todos sus estados), Inicio-Personal, Cuenta (métodos de ingreso y baja, salvo WhatsApp), Aceptar-Terminos, Legal, Perfil-Suspendido, Error-Org y Mensajes por correo: Código de ingreso, Código para verificar un método, Invitación a una organización, Método de ingreso agregado, quitado y principal cambiado, Membresía terminada, Baja pedida, Baja cancelada y Cuenta eliminada |
| 4 | Roles y Rol |
| 5 | Organizaciones, Organizacion, Cuentas, Cuenta-Plat, Recuperaciones, Recuperar, Legales, Auditoria-Plat y Config-Plat |
| 6 | Registro-Empresa, Inicio-Org, Inicio-Miembro, Usuarios, Usuario, Empresas, Empresa, Configuracion (con el dominio de correo), Pagina-Org y Auditoria-Org |
| 7 | Pagina-Publica y Directorio |
| 8 | Ingreso, Registro y Cuenta (los estados de WhatsApp), Enlace y Mensajes (canal WhatsApp, incluido «Enlace para entrar (bot)») |
| 9 | Ingreso: los estados del operador (segundo factor, código del autenticador incorrecto, código de recuperación, configurar el autenticador y guardar los códigos) |
| 10 | Cuenta (Privacidad: Exportar mis datos) y Mensajes (Exportación de datos lista, por correo) |

**El login primero:** la prioridad es que el ingreso completo funcione al 100 %. Por eso la Etapa 3 se hace en tres partes, cada una con su puerta (ver la etapa), y la 3a es el primer recorrido real de punta a punta.

**Estándares transversales:** P1 a P10 de [`estandares.md`](../architecture/estandares.md), adoptados el 2026-09-27, ya están repartidos en las tareas de cada etapa (marcados con su P#).

---

## Etapa 0: esqueleto (medio día a un día)

**Objetivo:** que las dos soluciones compilen vacías, con toda la maquinaria de calidad encendida, y que el AppHost levante Postgres, la Api y el front.

### Tarea 0.1: repo y archivos de raíz (back)
- [x] `.gitignore`: la plantilla de VisualStudio más `.local/`, `*.env`, `**/appsettings.*.local.json` y `.artifacts/`.
- [x] `global.json`, `Directory.Build.props`, `Directory.Packages.props` (copiados de ArquitecturaBase y completados con los paquetes que falten, según backend.md §1), `BannedSymbols.txt`, `.editorconfig` y `aspire.config.json`.
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
- [ ] `LayerDependencyTests`, `ProjectReferencesTests`, `ApplicationPackagesTests`, `MinimalApiRoutesTests` y `Support/CallSites.cs`, copiados y adaptados de la base. Commit `test: reglas de capas`.

### Tarea 0.5: CI
- [ ] `.github/workflows/ci.yml` en los dos repos: en la E0, el back hace build + test (con Docker); el front, `npm ci`, build, lint y test. El chequeo de contratos se suma en la E1. Commit `ci: build y test`.

### Tarea 0.6: front
- [ ] Borrar `node_modules`, `dist` y `.npm-cache` del scaffold viejo.
- [ ] `package.json` con las dependencias y versiones de ArquitecturaBaseFront, más `openapi-typescript` y `libphonenumber-js`. Además: `tsconfig*`, `vite.config.ts` (proxy, puerto 5174), `.oxlintrc.json`, `components.json`, `index.html` y `public/favicon.svg`. `silent-renew.html` llega en la E3.
- [ ] Copiar de la base solo las primitivas de shadcn de `shared/ui`, `shared/lib/utils.ts` y los hooks que compilan sin piezas posteriores. Al copiar, sustituir todo `--color-*` por los tokens de [`tema.md`](../../../ArquitecturaBaseMutitenantFront/docs/architecture/tema.md); armar `index.css` y `theme-tokens.test.ts`. No copiar `Pagination` (E1), `shared/lib/dateTime.ts` (nunca), `PhoneField`, `countries` ni `test/mocks` (E1/E3). Sumar a `shared/ui` `FilterBar`, `StatusDot`, `Avatar`, `FormError`, `Surface` y `tabs.tsx` (shadcn).
- [ ] `App.tsx` con un router mínimo: `providers.tsx` solo con `QueryClientProvider`, `routes.tsx` con una ruta vacía y `test/setup.ts` mínimo, con jest-dom. Auth, i18n, rutas por host y módulos se agregan en sus etapas. Build, lint y test limpios. Commit `chore: esqueleto del SPA`.

### Tarea 0.7: el arnés
- [ ] `HarnessTests` en ArchitectureTests y `src/test/harness.test.ts` en el front, con las verificaciones de `arnes.md` §5 (back) y §4 (front) y una constante `HarnessStage` que se sube al cerrar cada etapa.
- [ ] Un `AGENTS.md` + `CLAUDE.md` (`@AGENTS.md`) en cada carpeta que crea la Etapa 0, según el mapa de `arnes.md` §3 (back) y §2 (front).
- [ ] `structure.test.ts` en el front (sin imports entre features ni entre áreas) y la regla `react/jsx-no-literals` en `.oxlintrc.json`.
- [ ] **Accesibilidad (P9):** `vitest-axe` instalado, `extend-expect` en `test/setup.ts` (sin i18n todavía) y el primer `toHaveNoViolations` en `App.test.tsx`.
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
   - `ErrorTexts` y `ValidationTexts`, `ResourceParityTests` y `ErrorCodeTests` (`NotificationTexts` llega en la E3, `PermissionTexts` en la E4 y `AuditTexts` en la E6).
4. **Tiempo:** `TimeProvider`, `UtcDateTimeConverter`, conversores de `DateOnly` y `TimeOnly`, `ITimeZoneService` y `GET /api/time-zones`.
5. **Formatos:**
   - `Application/Common/Formatting/DisplayFormatter.cs`, con los perfiles `es-AR` y `en-US` (backend.md §18);
   - `docs/contracts/format-cases.json` con los casos del catálogo de `formatos.md`;
    - `DisplayFormatterTests`, que los recorre (el formato de `TaxId` se suma con su value object en la E6);
   - `MoneyJsonConverter`. La convención EF de `decimal` llega en la Etapa 2, con el `DbContext`.
6. **Validación y paginado:** `IRequestValidator`, `ValidationRules` (con `ValidCulture`, `ValidCurrency` y `ValidTimeZone`) y `FieldErrors`. También `PagedRequest`/`PagedResult`, `CursorRequest`/`CursorResult`, sus validadores y `SortMap` + `ApplySort` (backend.md §9, "Paginado, orden y búsqueda").
7. **Logging:** `OperationLog` con `[LoggerMessage]`.
8. **OpenAPI:** la convención de problemas, la exportación a `docs/contracts/openapi.json` y `OpenApiContractTests`.
9. **Hosting:** ForwardedHeaders, SecurityHeaders, SpaExtensions y el pipeline completo, y `docs/guides/prefijo-de-backend.md` (cómo sumar un prefijo a `BackendPrefixes`, a `SpaHostingTests` y al proxy de `vite.config.ts`).
10. `TestFeatures/TestController` y los tests de errores, localización, JSON UTC y `Money`.
11. **Datos de entrada (P3, P4):**
    - `TextNormalizer` + `NormalizedStringJsonConverter` global + `[RawText]`;
    - `Domain/Common/TextLimits.cs` y los tipos de texto de `ValidationRules`;
    - los value objects `Email` y `PhoneNumber` con sus casos en `format-cases.json`;
    - tests `TextNormalizerTests`, `NormalizedInputTests`, `TextLimitsTests`, `EmailTests` y `EmailPropertyTests`.
12. **Idempotencia (P6):** el atributo `[Idempotent]` (sin comportamiento) e `IdempotentActionsTests` (todo `POST` que responde 201 o 202 lleva `[Idempotent]`). La tabla, el filtro y el worker llegan en la Etapa 2.

**Front:**
1. `shared/api`: httpClient, ApiError, queryClient y formErrors.
2. `shared/i18n` (`common`, `errors`, `enums`) y `parity.test.ts`.
3. **`shared/format` completo:**
   - `cultureProfiles`, `formatters`, `parsers`, `useFormat` y `statusTones`;
   - `formatters.test.ts`, que recorre el **mismo** `format-cases.json` del back;
   - `format-usage.test.ts`.
4. **Paginado:** `usePagination` (vuelve a la página 1 y corrige una página fuera de rango), `useCursorList` y `useDebouncedValue` (ambos nuevos en esta etapa), `Pagination` con el selector 10/20/50/100 (10 por defecto) y `LoadMore`.
5. **`shared/ui/format`** (DateText, MoneyText, NumberText, PercentText, EnumText, StatusBadge, EmptyValue, TimeZoneText y CultureText) y **`shared/ui/fields`** (DateField, MoneyField, NumberField, PercentField, PhoneField…). `shared/format` incluye `formatTimeZone` y `formatCulture`, y `shared/time/timeZones.ts` nace acá. `DataTable` ya resuelve el formato por el `type` de cada columna.
6. `scripts/generate-contracts.mjs`, `npm run contracts` y `contracts:check`; `shared/api/generated/` contiene solo `schema.d.ts` y `shared/api/types.ts` declara los alias a mano reexportando desde él.
7. **Campos con forma propia:** `EmailField`, `PhoneField` con `shared/phone` (países, banderas SVG y `CountrySelect`) y `TaxIdField` (`stdnum`), más `useIdempotentMutation` (P6).
8. **Diseño adaptable (P10):** `DataTable` con columnas marcadas `mobile` ("primary" y "status", las demás se ven al entrar), `FilterBar` con los filtros debajo del buscador, `Dialog` como hoja desde abajo, y `columns-mobile.test.ts`.
9. **Errores genéricos** (frontend.md, "Errores"): sin conexión (toast y franja), 5xx con código de seguimiento, 429 con cuenta regresiva en el botón (nunca se reintenta solo), sesión vencida (vuelve a `/login` con `returnUrl`), 409 "Alguien cambió esto" (`ConcurrencyBanner` para `General.ConcurrencyConflict`, con «Ver lo nuevo» y «Seguir editando», sin perder lo escrito ni reintentar solo; P1), "¿Salir sin guardar?" y "Hay una versión nueva". Se prueban en el tablero Avisos; el caso "Módulo apagado" se conecta en la Etapa 5 (1b).

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
   - `TenantIsolationModelValidator`;
   - `docs/guides/migracion.md` (el comando, `EnableTenantRls` y los índices del `SortMap`; revisar el SQL generado), junto con el `AGENTS.md` de `Infrastructure/Persistence/Migrations/`, que la usa como «Copiá de».
7. Roles de BD: la cadena `appdb-admin`, `DatabaseBootstrapExtensions` y `RuntimeRoleValidator`. El AppHost entrega las dos cadenas.
8. `AdvisoryLockKeys` y `CacheKeys`, con prefijo obligatorio. `TenantJobRunner`.
9. **Búsqueda y paginado sobre Postgres:** `unaccent` + `pg_trgm` + `f_unaccent`, `ApplySearch`, `CursorCodec`, `ToCursorResultAsync` y los tests `PaginationTests`, `CursorPaginationTests`, `SearchTests` y `SortIndexTests`.
10. `TestFeatures/Isolation`: `Widget : ITenantOwned`, `Poster : IPublishedByBusiness` y `Deal : IConsumerBusinessShared` (la referencia de las tres clases, porque la plantilla no trae módulos de negocio), más los tests de [multitenancy.md §13](../architecture/multitenancy.md#13-tests-obligatorios-de-aislamiento) que no necesitan HTTP, `TransactionBoundaryTests`, `EntityConfigurationTests` y `TenantScopeUsageTests`. La fixture aplica las migraciones reales y luego `IsolationSchema.ApplyAsync(connection)` del proyecto de tests crea las tres tablas con SQL generado por las mismas plantillas `RlsSql.EnableTenantRls`, `EnablePublicRls` y `EnablePartiesRls`; no hay migraciones productivas para ellas.
11. **Concurrencia (P1):** `IVersioned` con la convención a `xmin`, `ConcurrencyConflictException` en `UnitOfWork`, el 409 en el mapper, `ConcurrencyTests` y `VersionedContractTests`. `Widget` es `IVersioned`.
12. **Orden alfabético (P2):** base creada con ICU `es-AR` en `DatabaseBootstrapExtensions` y en el contenedor de los tests; `RuntimeRoleValidator` verifica la collation; `CollationTests`.
13. **Convenciones EF:** `decimal` con `HasPrecision` explícito (test: ningún `decimal` sin `HasPrecision`) y `Email` (`EmailConvention`).
14. **Idempotencia (P6), la parte con base:**
    - la tabla `platform.IdempotencyKeys`, creada en la migración de la Etapa 2, con `IdempotencyKeyConfiguration`;
    - `IdempotencyStore`, que reserva la clave en su propia transacción;
    - `IdempotencyFilter`, que guarda la respuesta después del commit del UoW;
    - `IdempotencyCleanupWorker` e `IdempotencyTests`.

**Puerta:** la general, con `Rls_blocks_cross_tenant_even_with_filters_ignored` y `Runtime_role_is_not_privileged` en verde **en el CI**.

---

## Etapa 3: identidad, accesos y OpenIddict

**Se hace en tres partes, cada una con su puerta**, porque es la etapa más grande y la prioridad es que el ingreso funcione de punta a punta cuanto antes:
- **3a · Ingreso:** los puntos 1 a 6c, la base de 6c-bis y 6d, y los puntos 8, 9 y 10 del back; en el front, los puntos 1 y 4, los layouts y el inicio personal, las páginas de error del punto 3, el ingreso (con el paso del código), el registro con casilla de términos y el callback del punto 2, y la portada y las páginas legales del punto 5. Puerta: el recorrido manual de abajo (registro e ingreso reales, las dos puertas, cambio de lado, F5, logout).
- **3b · La cuenta:** gestión de 6c-bis, versión nueva bloqueante de 6d y 6e (baja). En el front: la aceptación bloqueante, el estado de ingreso con la baja pedida y `areas/personal/account` (métodos de ingreso, Privacidad y la baja). Puerta: sumar un correo personal, quitar un método con código en otro, aceptar términos nuevos, pedir la baja y cancelarla ingresando.
- **3c · Invitaciones:** el punto 7. En el front: la pantalla `/invitacion` con todos los estados del tablero Invitacion. Puerta automática: `InvitationsTests` emite con `InvitationIssuer` una invitación a alguien sin cuenta y otra a alguien con cuenta, y las acepta. El recorrido manual (invitar desde Usuarios, que llegue de verdad por Gmail y aceptar las dos) pasa a la puerta de la Etapa 6, porque la 3c no tiene una ruta para invitar.

**Back:**
1. `ApplicationUser` **global** (IsPlatformOperator, Status, Culture, TimeZoneId, DisplayName, LastBusinessTenantId), sin índice único de email ni teléfono: esos valores viven en `LoginMethods` (su base nace en la 3a, ver 6c-bis), y `Email`/`PhoneNumber` son solo una copia del método principal.
   - `LastBusinessTenantId` solo elige qué organización abrir cuando alguien con varias entra por «Ingresá como empresa»; nunca elige el lado.
   - **Sin setters públicos:** las reglas de la cuenta (correo o teléfono obligatorio, largo del nombre, restaurar) viven en métodos de la entidad, con tests unitarios.
   - Un nombre demasiado largo es un error de validación, no un recorte silencioso.
   - `ISignInService` es solo técnico, con 12 miembros como máximo; los datos de la cuenta van por `IUserRepository`. Así queda como en las Etapas 2 y 7 del plan de ArquitecturaBase.
1b. **Ajustes de plataforma y eventos de seguridad:** `PlatformSettings` (`ConsumerSignup` Open | Closed, `BusinessSignup` Open | RequiresApproval | Closed, `MaxOwnedOrganizations` y `AccountDeletionGraceDays` = 30), con su configuración EF, `IPlatformSettingsRepository`/`PlatformSettingsRepository`, `IPlatformSettingsReader`/`PlatformSettingsReader` con caché `p:` y `PlatformSeeder`, que los siembra (punto 9). Nacen acá porque los usan el registro, la baja y el seed de Production. También `SecurityEvent` + `SecurityEventType`, con `ISecurityEventRepository`/`SecurityEventRepository`. Las dos tablas van en la migración de la Etapa 3; el servicio, el controller y la pantalla para editar los ajustes, y el listado de eventos, llegan en la Etapa 5.
2. OpenIddict:
   - code + PKCE + refresh y el cliente `web`;
   - `OpenIdPrincipalFactory` (`access`, `tenant_id`, `tenant_kind`);
   - `ConnectController` con `tenant=` en authorize.
3. `TenantResolutionMiddleware` (una organización no disponible responde 403 `Tenancy.Tenant.Suspended`, `Tenancy.Tenant.PendingApproval` o `Tenancy.Tenant.Closed`), `[Access]`, `CurrentUser` y `RequestInfo`. Las rutas anónimas del dominio principal llevan solo `[AllowAnonymous]` y su controller va en la lista explícita de `AccessDeclarationTests`. `ConnectService` con `access=` y `tenant=` en authorize (cambio de acceso sin volver a ingresar).
4. Ingreso sin contraseña por correo: códigos, `LoginAudit` y rate limits (los enlaces de un solo uso del bot llegan con la Etapa 8). El canal del código, el de la invitación y el de los avisos de la cuenta pasan por `ILoginCodeChannel`, `IInvitationChannel` e `IAccountNoticeChannel` (en `Application/Interfaces/Integrations/Messaging/`): el núcleo trae solo `"email"`, también para los avisos, y WhatsApp se enchufa en la Etapa 8. `IAccountNoticeChannel` recibe un `AccountNotice` cerrado (`LoginMethodChanged`, `ReviewLoginMethods`, `DeletionRequested`, `DeletionCancelled`, `AccountDeleted`, `RecoveryReceived`, `RecoveryApproved` y `RecoveryRejected`), siempre por el outbox y cifrado. `IPhoneLinkObserver` avisa los cambios de teléfono.
5. **Registro B2C:** identidad + tenant `Personal` + `Member` (la persona es la única miembro y sus permisos son implícitos, `PersonalPermissions` de la Etapa 4) + `TenantSettings` (cultura, zona y moneda del navegador). `acceptedTerms` es obligatorio y se guarda en la misma transacción que el alta. Respeta `ConsumerSignup`: si está cerrado, el tablero Registro muestra el estado «Registro cerrado».
6. Outbox persistente y **Gmail por SMTP**, que en desarrollo también envía de verdad (pickup `.eml` como opción), con plantillas de `Notifications.resx` y `DisplayFormatter`.
6b. **Ingreso y registro con Google** (`Authentication:Google:*`): si la cuenta no existe, crea la identidad y su espacio personal, con `acceptedTerms` en la misma transacción, como el registro por código (solo en el ingreso como persona). Vincular y desvincular Google desde la cuenta llega en la 3b.
6c. **Configuración lista para pegar** ([`docs/operations/configuracion.md`](../operations/configuracion.md)): las mismas claves que ArquitecturaBase; `appsettings.Development.json` con los valores no secretos; los scripts de `scripts/secretos/` probados (importar de ArquitecturaBase, cargar desde un archivo, verificar); validación al arrancar con el nombre de la clave que falta.
6c-bis. **Métodos de ingreso (ADR 0033):**
   - 3a: entidad `identity.LoginMethods`, índice único `(Type, Value)`, registro e ingreso con un método verificado (correo o Google). `Phone` nace solo como modelo; sumar y verificar teléfonos se habilita cuando WhatsApp registra su canal en la E8;
   - 3b: sumar, verificar, elegir el principal y quitar métodos, con código en otro método, aviso en todos y su `SecurityEvent`; aviso "Agregá un correo personal…" cuando la cuenta depende solo de métodos de una empresa; `LoginMethodsTests`;
   - E6: con `TenantDomains` y "Quitar de la organización", al terminar una membresía se desactivan los correos administrados y se avisa; `ManagedEmailTests` verifica que un exmiembro no ingrese con ese correo.

   El **dominio verificado** de la organización (registro TXT) va en la Etapa 6 (Configuración), y "Recuperar mi cuenta" asistida por la plataforma en la Etapa 5. En la E3, la cuenta ofrece correo y Google; WhatsApp aparece solo si `GET /api/auth/methods` trae ese canal (E8).
6d. **Términos y privacidad (P7):** en 3a nacen `LegalDocuments` y `LegalDocumentContents` (versión 1 de términos y privacidad, con su texto en es y en, sembrada por `LegalDocumentSeeder`), `LegalAcceptances` y `acceptedTerms` en el registro por correo o Google, guardada en la misma transacción, con la casilla del front, las páginas legales públicas y `LegalAcceptanceTests` para el alta. En 3b se suma una versión nueva bloqueante, `LegalAcceptanceMiddleware` (solo para identidad autenticada; las rutas anónimas lo evitan), la pantalla Aceptar-Terminos y los casos nuevos de `LegalAcceptanceTests`. El registro por WhatsApp llega en la E8.
6e. **Baja de la cuenta (ADR 0035, multitenancy.md §3.2):** estados `PendingDeletion` y `Deleted` en la identidad, con la fecha aparte (`DeletionScheduledForUtc`), `ReauthTicket`, `AccountDeletionPolicy` (bloquea a un operador, una baja ya pedida y lo que bloquee un módulo; el bloqueo del único Dueño, `Legal.AccountDeletion.LastAdmin`, se suma en la Etapa 4), `POST /api/me/deletion` y `POST /api/auth/deletion/cancel`, el `SecurityEvent` `AccountDeletionRequested`, el ingreso durante la gracia (los días salen de `PlatformSettings.AccountDeletionGraceDays`), `AccountDeletionWorker`, `IAccountDeletionParticipant` con los participantes del núcleo que ya tienen tabla (espacio personal, aceptaciones legales, outbox y membresías), y los avisos por `IAccountNoticeChannel`. Los demás participantes del núcleo llegan con su tabla: pedidos de recuperación en la Etapa 5 (junto con el "Dar de baja" de la plataforma), datos compartidos en la Etapa 7 (con `IRetainedOnConsumerDeletion`) y exportaciones en la Etapa 10. Tests `AccountDeletionTests` y `AccountDeletionParticipantsTests`.
7. **Invitaciones a una organización** (3c), en `Invitations/` (nunca `UserInvitation`):
   - `Invitation` + `Member(Invited)`;
   - `InvitationIssuer`, que emite y encola por `IInvitationChannel`. **No tiene ruta propia:** lo usan `TenantAdministrationService` en la Etapa 5 y `UserService` en la 6 (`POST /api/users/invitations`);
   - `InvitationService` con la vista previa `POST /api/invitations/preview` (el token va en el cuerpo, nunca en la URL) y `POST /api/invitations/accept`, con cuenta previa y sin ella (aceptar sin cuenta crea la identidad **sin** espacio personal);
   - `InvitationsTests`: emite con `InvitationIssuer` una invitación a alguien sin cuenta y otra a alguien con cuenta, y las acepta.
8. `GET /api/me` (cuenta, acceso activo, espacio personal, organizaciones, permisos y las preferencias efectivas de cultura, zona y moneda) y `PUT /api/me`.
9. Seed idempotente en **todos** los ambientes, dentro de un límite y con el advisory lock `seed:` para que dos réplicas no choquen. En desarrollo, además: operador (entra sin segundo factor hasta la Etapa 9); Empresa A con Ana y Kevin (el rol `TenantAdmin` de Ana se siembra en la Etapa 4, cuando existen los roles); Kevin y Carla como personas. Test: arrancar en `Production` contra una base migrada y vacía deja el cliente `web`, los ajustes de plataforma y el operador inicial.
10. Tests:
    - el recorrido real de ingreso;
    - el registro;
    - cambio de acceso u organización válido e inválido;
    - acceso equivocado → 403, y una persona no puede crear una empresa;
    - suspensiones.

**Front:**
1. `auth/` y `tenancy/`: las dos puertas (`/login` y `/login/empresa`), "Ir a mi empresa" / "Ir a Personal" en el menú de la cuenta, elegir organización, y `queryClient.clear()` al cambiar.
2. `areas/public/auth`: ingreso (`/login` y `/login/empresa`) con los estados del tablero Ingreso, incluido el paso del código, que no tiene ruta propia (también la cuenta con la baja pedida y la sesión vencida; los de WhatsApp y el enlace del bot llegan en la Etapa 8, y los cinco del operador en la Etapa 9), registro con la casilla de términos y callback en 3a; invitación en 3c y aceptación bloqueante de términos nuevos en 3b.
3. Los tres layouts con el menú lateral del tema (`BusinessLayout` y `BusinessHomePage` dan en 3a el inicio vacío de `/org`, como su tablero; el `AdminPanel` suma «Gestión de usuarios › Roles y permisos» en la Etapa 4 y queda completo en la Etapa 6), `areas/personal/home` con el inicio personal, las páginas de `areas/public/errors` (`ForbiddenPage`, `NotFoundPage` y `OrganizationUnavailablePage`, con sus estados Suspendida, Espera aprobación y Cerrada para `Tenancy.Tenant.Suspended`, `.PendingApproval` y `.Closed`) y `areas/personal/account` con los métodos de ingreso, Privacidad (exportar llega en la Etapa 10) y la baja.
4. `useFormat` conectado a las preferencias de `/api/me`.
5. `SiteLayout` y `areas/public/site` con la portada (`/` sin sesión, tablero Landing, con «Para empresas», que lleva a «Ingresá como empresa»; el directorio de empresas publicadas se suma en la Etapa 7 y «Registrá tu empresa» se enlaza al llegar la Etapa 6), en la 3a. También en 3a, `areas/public/legal` con `/terminos` y `/privacidad` (tablero Legal), que leen el documento vigente sin sesión (`LegalController`, 6d).

**Documentación:** `docs/features/identidad.md`, y un `AGENTS.md` de una línea (más su `CLAUDE.md` con `@AGENTS.md`) en cada carpeta de código del área, que apunta a su documento. Nace en la 3a con las dos puertas, el registro de personas y la aceptación inicial de términos, los accesos y el cambio de lado. La 3b le suma la gestión de métodos de ingreso, la aceptación bloqueante de términos nuevos y la baja, y la 3c las invitaciones. Cada parte lo actualiza en su propio commit.

**Puerta:** la general, más un recorrido manual:
0. `./scripts/secretos/importar-desde-arquitecturabase.ps1` y `verificar.ps1` con todo `[ok]`;
1. registrarse como persona con un código que llega **de verdad** por Gmail, y otra vez con Google → queda en su espacio personal;
2. entrar por "Ingresá como empresa" con Ana → Empresa A (del seed);
3. F5 → sigue en Empresa A;
4. volver a Personal;
5. cambiar la cultura a en-US → fechas y números cambian en todas partes;
6. logout.

---

## Etapa 4: autorización y Roles (área de referencia)

**Back:**
1. `Permissions` (organización y empresa, sin prefijo: `users.read`, nunca `tenant.users.read`), `PersonalPermissions`, `PlatformPermissions` (los `platform.*` de la Etapa 5; `platform.whatsapp.manage` no entra hasta la Etapa 11), `Role`, `RoleScope`, `RoleAssignment`, `SystemRoles` y `Permissions.resx`.
2. `PermissionService` (efectivos por organización y empresa, con caché e invalidación) y `PlatformPermissionService`.
3. Los atributos `HasPermission`, `HasCompanyPermission` y `HasPlatformPermission`, su policy provider y handlers, y `PermissionAuthorizationTests`.
4. **RoleService completo como referencia:** listado paginado con filtros y conteos, get by id (con `version`), create (201), update y delete con `version` obligatoria (P1: `Role` es `IVersioned`, 409 `General.ConcurrencyConflict`), catálogo agrupado y protección de los roles de sistema.
5. Tests unitarios, de integración y de aislamiento, y el 409 de concurrencia del rol (`ConcurrencyTests`).
6. **El Dueño:** sale solo del rol de sistema `TenantAdmin` (`RoleAssignment`), nunca de un flag de `Member`. El seed le da `TenantAdmin` a Ana en la Empresa A. `AccountDeletionPolicy` suma el bloqueo del único Dueño de una organización no cerrada (`Legal.AccountDeletion.LastAdmin`), con su caso en `AccountDeletionTests`. "Dueños activos" son los `TenantAdmin` con la identidad `Active` (una baja pedida no cuenta); `LastTenantAdminGuard` (Etapa 6) reusa esa misma lectura.

**Front:** `areas/business/roles` (RolesPage con "Vale en" y RoleEditorPage, que manda la `version` de la ficha y muestra el `ConcurrencyBanner` ante un 409), usando `DataTable` con columnas tipadas: es la feature de referencia. Suma su enlace «Roles y permisos» dentro de «Gestión de usuarios» en el `AdminPanel` (`layouts/navigation/business.ts`).

**Documentación de la receta** (como las Etapas 4 y 5 de ArquitecturaBase):
- `docs/guides/agregar-un-area.md`: los pasos en orden, con la ruta de cada archivo y un enlace al equivalente de Roles, más una lista de verificación. Cubre B2B, B2C y los datos públicos o compartidos: cambian el `[Access]`, los permisos y la clase del dato.
- `docs/guides/permiso-nuevo.md`.
- `docs/features/roles.md`, y un `AGENTS.md` de una línea (más su `CLAUDE.md` con `@AGENTS.md`) en cada carpeta de código del área, que apunta a su documento.

**Puerta:** la general, más dos pruebas:
- **Probar la receta:** un subagente sin contexto sigue `agregar-un-area.md` y agrega un área de prueba (por ejemplo `Tags`) sin preguntar nada que la guía no responda. Se corrige la guía y se descarta el área.
- **Puerta de documentación a ciegas:** un agente que lee solo `AGENTS.md` contesta bien diez preguntas del tipo "¿dónde va X?". Las preguntas se escriben antes.

---

## Etapa 5: plataforma

**Back:**
1. `TenantAdministrationService`: listar, ver, aprobar, rechazar (con motivo), crear con invitación, suspender, reactivar, cerrar y reintentar el provisioning. Aprobar, crear y reintentar usan `TenantProvisioner` de la Etapa 6; crear con invitación, `InvitationIssuer` de la 3c. Rechazar deja la organización en `Closed` (no hay un estado de rechazo), registra el `SecurityEvent` con el motivo y le avisa a quien la registró; su cuenta y su espacio personal siguen funcionando.
1b. **Módulos por organización (P8):**
    - `Microsoft.FeatureManagement.AspNetCore`, el catálogo `Features.cs`, `platform.TenantFeatures` y `TenantFeatureFilter` con caché;
    - `DisabledFeatureHandler` (404 ProblemDetails) e `IFeatureService`;
    - `features` en `/api/me` y la sección "Módulos" en la ficha de organización de la plataforma;
    - `FeatureGateTests` y `ModuleControllersTests`;
    - en el front, `useFeature`, `<Feature>` y `feature` en las rutas y la navegación; una ruta de un módulo apagado muestra `NotFoundPage`, igual que un 404 (caso "Módulo apagado" del tablero Avisos).
1c. **Moderar páginas públicas (multitenancy.md §11):** desde la pestaña «Página pública» de la ficha de organización (tablero Organizacion), que muestra solo metadatos (`GET /api/platform/tenants/{id}/public-site`). Despublicar con motivo (`POST /api/platform/tenants/{id}/public-site/unpublish`): la página vuelve a borrador y queda bloqueada, y la organización no puede volver a publicarla hasta que la plataforma use «Permitir publicar» (`POST /api/platform/tenants/{id}/public-site/allow-publish`, también con motivo). Las dos acciones registran el `SecurityEvent` y les avisan a los Dueños. Usa `PublicPage` de la Etapa 6, con su bloqueo (`PublishBlockedAtUtc`, `PublishBlockedReason` y `PublishBlockedByUserId`).
1d. **Dominio verificado desde la plataforma (ADR 0033):** la pestaña «Dominio verificado» de la ficha de organización lo muestra, lo verifica (`POST /api/platform/tenants/{id}/domains/{domain}/verify`) y lo quita con motivo, con `SecurityEvent`, sobre `TenantDomainService` de la Etapa 6.
2. Identidades: buscar, suspender (revoca todas sus sesiones), "Cerrar sesiones" con motivo (`POST /api/platform/accounts/{id}/revoke-sessions`, sin cambiar el estado) y "Dar de baja" con motivo (ADR 0035). Una cuenta suspendida a la que se le inicia la baja sigue `Suspended` con la fecha de eliminación y no puede cancelarla; si la plataforma la reactiva antes de la fecha, pasa a `PendingDeletion` (no a `Active`) y ahí sí puede ingresar y cancelar.
2b. **«Recuperar mi cuenta» (ADR 0033):** `platform.AccountRecoveryRequests` (`Pending | Approved | Rejected`). El pedido es público (`POST /api/account-recovery`, desde `/recuperar`, sin revelar si la cuenta existe) y lleva el método de antes y uno nuevo, verificado con código. Un operador lo revisa en la bandeja Recuperaciones (`GET /api/platform/recoveries`) y lo aprueba o lo rechaza con motivo (`POST /api/platform/recoveries/{id}/approve` y `/reject`), con `SecurityEvent`. Aprobar suma el método nuevo y saca la cuenta de «Necesita recuperación», el estado que muestra su ficha. Suma el participante `Recovery` de la baja, que cierra los pedidos pendientes (ADR 0035).
3. `PlatformOperatorService`; el primer dueño sale del seed.
4. `PlatformAuditService` + `ISecurityEventReader`/`SecurityEventReader` (listado de Auditoria-Plat) y `PlatformSettingsService` y su controller para la pantalla Config-Plat, que edita lo que muestra su tablero: el registro de personas, el alta de organizaciones y las organizaciones propias por persona. `PlatformSettings` (con su configuración EF, repositorio, reader y `PlatformSeeder`) y `SecurityEvent` ya existen desde la Etapa 3 (1b).
4b. **Documentos legales (P7):** listar las versiones de términos y privacidad (`GET /api/platform/legal-documents`) y publicar una versión nueva (`POST /api/platform/legal-documents`) con su tipo, su fecha de vigencia y el texto en todas las culturas soportadas (hoy es y en), sobre `LegalDocuments` y `LegalDocumentContents`. Una versión nunca se edita. El `POST` es `[Idempotent]`, se protege con `[HasPlatformPermission]` (`platform.legal.manage`) y registra un `SecurityEvent` con motivo. Desde la vigencia, `LegalAcceptanceMiddleware` pide aceptar la versión nueva.
5. Tests:
   - un operador sin `Enter` no ve datos;
   - un usuario recibe 403 en `/api/platform`, también en las rutas nuevas;
   - suspender una organización no afecta el acceso B2C de sus usuarios (que su página pública muestre «No disponible» se prueba en la Etapa 7, con el sitio público);
   - el pedido de recuperación no revela si la cuenta existe, y aprobarlo deja la cuenta con el método nuevo;
   - el dominio es único, y verificarlo deja administrados los correos de ese dominio;
   - publicar un documento legal sin alguna cultura da 400, y una versión nueva bloquea hasta aceptarla;
   - una página despublicada por la plataforma no se puede volver a publicar desde la organización.

**Permisos** (`PlatformPermissions`, [permisos](../rules/permisos.md)):
- `platform.tenants.read` para ver organizaciones, y `platform.tenants.manage` para aprobar, rechazar, suspender, reactivar y cerrar, prender y apagar módulos, ver el dominio verificado y moderar la página pública;
- `platform.accounts.read` para buscar y ver cuentas, y `platform.accounts.manage` para suspender, reactivar, cerrar sesiones e iniciar la baja;
- `platform.recoveries.manage`, `platform.legal.manage`, `platform.operators.manage`, `platform.audit.read` y `platform.settings.manage`, cada uno para su pantalla;
- roles de plataforma: **Owner** con todos y **Support** con `platform.tenants.read`, `platform.accounts.read`, `platform.recoveries.manage` y `platform.audit.read`.

**Front:** `areas/platform/{tenants, accounts, recoveries, legal, audit, settings}` (`accounts` incluye los operadores; sin `home` ni `whatsapp`) y «Recuperar mi cuenta» (`/recuperar`, `RecoverAccountPage` en `areas/public/auth`), copiando los tableros de la Etapa 5. `/plataforma` es el listado de organizaciones (el inicio del operador), la ficha es `/plataforma/organizaciones/:id`, y además `/plataforma/cuentas` (cuentas y operadores), `/plataforma/cuentas/:id`, `/plataforma/recuperaciones`, `/plataforma/legales`, `/plataforma/auditoria` y `/plataforma/configuracion`. Menú: Organizaciones, Cuentas, Recuperaciones, Auditoría, Documentos legales y Configuración.

**Documentación:** `docs/features/plataforma.md`, con los punteros de una línea en las carpetas del área.

---

## Etapa 6: área B2B (organización)

**Back:**
1. **"Registrá tu empresa"** (`POST /api/auth/business-signup`, en `/registro/empresa`, desde la portada y la puerta de empresas; nunca desde el acceso B2C), con `TenantProvisioner` idempotente, compartido con la plataforma. El slug es obligatorio: se elige ahí (`Slug`, `ReservedSlugs`), se comprueba su disponibilidad y se validan los reservados. Respeta `BusinessSignup` y `MaxOwnedOrganizations` (estados «Espera aprobación», «Alta cerrada» y «Llegó al límite» del tablero Registro-Empresa).
1b. **Mi página pública:** `PublicPage` (nombre, logo, descripción y contacto; el slug es de la organización y vive en `Tenants`), en borrador o publicada, editable por quien tenga `publicpage.manage`. El slug se puede cambiar después en «Página pública», con la misma comprobación de disponibilidad. Si la plataforma la despublicó, queda en borrador bloqueada, con fecha y motivo (`PublishBlockedAtUtc`, `PublishBlockedReason` y `PublishBlockedByUserId`; `PublicPageStatus` sigue `Draft | Published`), y no se puede volver a publicar (`PublicSite.PublicPage.PublishBlocked`) hasta que la plataforma lo permita (estado «Despublicada por la plataforma» del tablero Pagina-Org). Ruta: `api/public-site`.
2. Usuarios (`api/users`), que parten de `Members`:
   - listado con filtros y conteos, y ficha;
   - invitar (`POST /api/users/invitations`, con `InvitationIssuer` de la 3c), reenviar y revocar la invitación (bajo `api/users/invitations/{id}`), editar, deshabilitar, habilitar y quitar de la organización;
   - roles de organización;
   - protección del último TenantAdmin con `LastTenantAdminGuard`, que reusa la lectura de "Dueños activos" de la Etapa 4 (una cuenta con la baja pedida no cuenta como Dueño);
   - el estado "Baja pedida" en el listado y la ficha (el participante de la baja para las membresías existe desde la E3, ADR 0035); al quitar a alguien de la organización, se desactivan sus correos administrados y se avisa, con `ManagedEmailTests`.
3. Empresas (`api/companies`: CRUD, CUIT como `TaxId` validado (P5), zona horaria) y membresías de empresa con sus roles (`api/companies/{companyId}/members`), con protección del último CompanyAdmin. `TaxId` + `ArgentineCuitValidator` + `TaxIdTests` y `TaxIdPropertyTests`. Las ediciones de usuario y empresa llevan `version` (P1), como Roles.
4. Configuración (`api/settings`: nombre, cultura, zona y moneda por defecto, y el dominio de correo: registrarlo, ver el registro TXT, comprobarlo y quitarlo, con `settings.manage`. Acá nacen `platform.TenantDomains` (dominio único en todo el sistema, token TXT, `Pending | Verified`), `TenantDomainService` y el puerto de consulta DNS TXT con su adaptador: al verificarse, los `LoginMethods` de ese dominio (los que ya existen y los nuevos) quedan con `ManagedByTenantId`) y auditoría, con listado traducido.
5. Tests de aislamiento para cada ruta nueva.

**Front:** el `areas/business/home` vacío de la E3 suma la administración; nacen `areas/business/{users, companies, settings, audit, public-page}` y el `AdminPanel` completo (tema.md), copiando los tableros de la Etapa 6.

**Documentación:** `docs/features/organizaciones.md` (usuarios, empresas, membresías, filtros y conteos), con los punteros de una línea en las carpetas del área, y la sección del registro de empresas ("Registrá tu empresa") en `docs/features/identidad.md`.

**Puerta:** la general, más un recorrido manual: invitar desde Usuarios a alguien sin cuenta y a alguien con cuenta, que las dos invitaciones lleguen **de verdad** por Gmail, y aceptarlas.

---

## Etapa 7: área B2C, sitio público e interacción

**Objetivo:** dejar el área personal lista para que cada producto le sume sus funcionalidades B2C con la misma receta que las B2B.

**Back:**
1. **Sitio público:**
   - `PublicSiteResolutionMiddleware` (subdominio → organización publicada, usa `ReservedSlugs` de la Etapa 6) y `[PublicSite]`;
   - una organización suspendida o con la página despublicada muestra «No disponible» en su subdominio (tablero Pagina-Publica), probado en `SubdomainTests` con los estados sembrados directamente (`TenantStatus.Suspended` y la página bloqueada), sin depender de los servicios de la Etapa 5;
   - `SubdomainRedirectUriValidator` para ingresar desde un subdominio: el issuer es fijo (el dominio principal), `authorize` y `logout` navegan al dominio principal, y el canje del código, la renovación y `userinfo` van a `/connect/*` del propio subdominio;
   - el directorio de páginas publicadas en el dominio principal (`DirectoryController`, con `[AllowAnonymous]` y no `[PublicSite]`);
   - el SPA y la Api en el mismo origen en cada host, sin CORS y con `BackendPrefixes` igual en todos; en desarrollo, `*.localtest.me` y el proxy de Vite por host.
1b. **Mecánica de interacción de punta a punta con `TestFeatures`:** una persona crea un `Deal` desde la página pública, la empresa lo ve en su bandeja y cada parte cambia estados según `PartyPolicy`. Es la guía para que un producto arme su módulo. Suma el participante de la baja para los datos compartidos (`engagement`), que reemplaza la copia de los datos personales por "Cuenta eliminada", e `IRetainedOnConsumerDeletion` para la retención legal que declare un módulo (ADR 0035, multitenancy.md §3.2). El caso se prueba con el `Deal` en `AccountDeletionTests`.
2. `docs/features/personal.md`, la receta de un módulo B2C:
   - entidad `ITenantOwned`;
   - rutas `[Access(Consumer)]` sin permisos, porque la persona tiene los `personal.*` implícitos;
   - un módulo que interactúa con empresas usa datos compartidos (`engagement`) y `PartyPolicy`;
   - tests de aislamiento entre personas, y entre el acceso B2C y el B2B de la misma persona.
3. `TestFeatures` con controllers `[Access(Consumer)]` y `[PublicSite]` que prueban esa receta.

**Front:** el sitio público, copiando Pagina-Publica y Directorio con sus tableros de teléfono (P10: las páginas públicas se piensan primero para el teléfono):
- el árbol de rutas por host en `routes.tsx` (con un subdominio arma `storefront`) y `usePublicSite`;
- `areas/storefront` con la página pública del subdominio (publicada, sin sesión o con sesión, y «No disponible») y «Ingresá para continuar», que ingresa como persona y vuelve a la misma página;
- el directorio de empresas publicadas en `areas/public/site`, al lado de la portada de la Etapa 3;
- todo dentro de `SiteLayout`.

Además, `navigation/personal.ts` queda listo para sumar módulos B2C. El `PersonalLayout` y el inicio personal ya están desde la Etapa 3.

---

## Etapa 8: WhatsApp como módulo quitable (número de la plataforma)

**Objetivo:** que WhatsApp nazca como módulo y un proyecto sin WhatsApp lo quite borrando carpetas y una línea. Es el mismo diseño que la Etapa 6 de ArquitecturaBase (backend.md §15).

**Back:**
1. Las carpetas `Domain/WhatsApp` y `Modules/WhatsApp` en Application, Infrastructure y Api, con un `AddWhatsAppModule()` por capa llamado desde `Program.cs`. Las configuraciones EF del módulo las aplica el propio módulo.
2. Adaptadores de los puertos del núcleo: `WhatsAppLoginCodeChannel`, `WhatsAppInvitationChannel`, `WhatsAppAccountNoticeChannel` (los avisos de la cuenta a los métodos `Phone`, con las plantillas `aviso_metodo_ingreso`, `revisa_metodos_ingreso`, `baja_cuenta_pedida`, `baja_cuenta_cancelada` y `cuenta_eliminada`; "Exportación lista" sigue solo por correo) y `WhatsAppPhoneLinkObserver`. También nacen acá `LoginLinkService`, `LoginLinkIssuer`, su controller, contratos y tests del enlace de un solo uso del bot.
3. Cliente de Cloud API y opciones validadas, con **las mismas claves `WhatsApp:*` que ArquitecturaBase** y los valores no secretos copiados de allá (`configuracion.md` §3), salvo las plantillas: `WhatsApp:Templates:Invitation` pasa a `invitacion_organizacion` (reemplaza a `invitacion_acceso`) y cada plantilla nueva de [`whatsapp-plantillas.md`](../operations/whatsapp-plantillas.md) suma su clave `WhatsApp:Templates:<Nombre>`. Envío por el outbox (canal `"whatsapp"`, cifrado), con esas plantillas, creadas en Meta al empezar la etapa (la aprobación tarda). Registro B2C por WhatsApp.
4. Webhook con firma e idempotencia, procesador de entrada, bot de ingreso con enlace de un solo uso, retención de 90 días y health check.
5. **Teléfonos como métodos de ingreso (ADR 0033).** Con el canal `"whatsapp"` registrado, el flujo de métodos de ingreso del núcleo (Etapa 3, 6c-bis) permite sumar a `LoginMethods` un método de tipo `Phone` y verificarlo con un código por WhatsApp. Ese teléfono sirve para ingresar y como el "otro método" que recibe el código para quitar o cambiar un método. Cuando un teléfono se quita o cambia, `WhatsAppPhoneLinkObserver` suelta el contacto e invalida los enlaces. Sin el módulo no se puede verificar un teléfono.
5b. **Países de WhatsApp:** los controla el módulo en sus adaptadores y flujos (código por WhatsApp, vínculo y registro por WhatsApp), leyendo `WhatsApp:AllowedCountries` solo dentro de `Modules/WhatsApp`, con su propio error sobre el campo `phone`, y solo para un número **nuevo** (achicar la lista no invalida uno existente). `PhoneUsage` del núcleo sigue en `Any | Mobile`. El módulo aporta sus países a `GET /api/auth/methods` (`channels: [{ key: "whatsapp", countries: [...] }]`), de donde los lee `PhoneField usage="whatsapp"` en el front.
6. `ModuleIsolationTests`: el núcleo no referencia `*.Modules.*`.
7. `docs/features/whatsapp.md` y `docs/guides/quitar-whatsapp.md`.

**Front:** ingreso, registro y enlace por WhatsApp (`/login/enlace`, tablero Enlace), y `PhoneField` en la cuenta (vincular y desvincular), copiando los estados de WhatsApp de Ingreso, Registro, Cuenta y Enlace y el canal WhatsApp de Mensajes. No hay pantalla de WhatsApp de plataforma: si algún día hace falta, primero se dibuja y se aprueba.

**Puerta:** la general, más un recorrido real: con el webhook de Meta apuntando al túnel del multitenant (`configuracion.md` §4), ingresar con un código por WhatsApp y escribirle al bot. Además, **la prueba de fuego**: en una copia descartable, quitar el módulo siguiendo `quitar-whatsapp.md`, y el build y los tests del núcleo tienen que quedar en verde.

---

## Etapa 9: endurecimiento

- TOTP obligatorio para operadores, con reautenticación reciente en las operaciones sensibles. Hasta esta etapa, el operador del seed entra sin segundo factor. En el front, los cinco estados «Operador: …» del tablero Ingreso (segundo factor, código del autenticador incorrecto, código de recuperación, configurar el autenticador y guardar los códigos).
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
- **Datos personales (P7, segunda parte):** `POST /api/me/data-export` (`[Idempotent]`), `platform.DataExports`, la preparación en segundo plano del JSON y el correo «Exportación de datos lista» (solo por correo), con un enlace de descarga que vence a las 48 h ([datos-personales](../rules/datos-personales.md)), y su participante de la baja, que borra los archivos. En el front, «Exportar mis datos» en la sección Privacidad de `/cuenta`. La baja de la cuenta ya está en la Etapa 3.

## Etapa 11 (opcional)

- Canales de WhatsApp por organización (`WhatsAppChannels`). Recién acá entra al catálogo `platform.whatsapp.manage`, porque es la primera ruta que lo usa.
