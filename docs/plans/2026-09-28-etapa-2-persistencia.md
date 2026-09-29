# Etapa 2: persistencia multitenant Implementation Plan

> **Para agentes:** usar `writing-plans` para mantener este plan y ejecutar las casillas tarea por tarea. La implementación sigue en esta sesión, en `main`, con TDD y un commit local por tarea; no hacer push.

**Goal:** Impedir lecturas y escrituras cruzadas entre organizaciones aun cuando una consulta omita los filtros de EF, y dejar listas las tablas globales de referencia, el límite transaccional y la persistencia de la Etapa 3.

**Architecture:** Domain clasifica los datos como privados, públicos o compartidos. Application define el contexto y el único límite de guardado. Infrastructure usa PostgreSQL 18, EF Core 10, filtros con nombre y RLS `FORCE`; el runtime opera como `mt_app`, y solo el bootstrap de Development usa `appdb-admin`. La migración productiva crea esquemas y tablas de E2; las entidades `Widget`, `Poster` y `Deal` viven únicamente en el proyecto de tests, después de aplicar las migraciones reales.

**Tech Stack:** .NET 10, EF Core/Npgsql 10, PostgreSQL 18 con ICU `es-AR`, Aspire 13.5, xUnit v3, Testcontainers, HybridCache y React/Vite para la puerta general.

---

## Decisiones técnicas anticipadas

- `ApplicationDbContext` debe heredar de `IdentityUserContext<ApplicationUser, Guid>` en E2, mientras que el modelo completo de `ApplicationUser` y su tabla son E3 (`arbol.md` §§Infrastructure/Identity y Migrations). E2 incorpora solo la clase CLR mínima y excluye su mapeo del modelo; E3 agrega campos y tabla. Se aclara la etapa de esa clase en `arbol.md` al implementarla. Esto no habilita identidad ni cambia el producto.
- `platform.IdempotencyKeys` necesita una entidad técnica que no aparece nombrada en el árbol. Se crea `Infrastructure/Idempotency/IdempotencyKey.cs`, interna, junto con su configuración E2. El guard de guardado permite las rutas explícitas de migración y seed, que no son servicios de negocio.
- `ReferenceDataReader` conserva el contrato recién aprobado de E1: **todas** las filas con `isEnabled`; los selectores filtran habilitadas y los formateadores pueden leer una fila histórica. El seed nunca borra filas. No se crean pantallas front en E2.
- Aspire 13.5.4 `AddDatabase("appdb")` crea la base antes de arrancar Api y no garantiza ICU `es-AR`; E2 lo reemplaza por referencias explícitas a `appdb`, `appdb-admin` y la conexión bootstrap del superusuario, todas con parámetros secretos. El bootstrap crea la base con ICU. Si ya existe la base del volumen E0, solo la recrea tras comprobar que no contiene tablas de usuario; si contiene datos, falla sin borrarlos y explica el paso de migración.
- Los tests de §13 que dependen de autenticación, subdominio, cuenta o rutas E3/E7 empiezan en su etapa. E2 ejecuta los de RLS, rol, clases pública/compartida, columnas, políticas y ámbito con `TestFeatures/Isolation`, sin fingir HTTP de identidad.
- Para la carga de `TestFeatures`, el proyecto de tests agrega las tres entidades al modelo de un contexto de test derivado del productivo. `IsolationSchema.ApplyAsync` crea solo sus tablas después de las migraciones reales y reutiliza `RlsSql`; ninguna migración productiva contiene `Widget`, `Poster` o `Deal`.

## Método para cada tarea

1. Antes de comenzar, releer `docs/plans/2026-09-27-plan-de-desarrollo.md` («Reglas para todas las etapas» y «Etapa 2») y las entradas `[E2]` de `docs/architecture/arbol.md`; consultar la ficha indicada por la tarea y el `AGENTS.md` de la carpeta. `../ArquitecturaBase` y `../ArquitecturaBaseFront` son de solo lectura.
2. Escribir primero la prueba especificada, ejecutarla y comprobar el fallo por la conducta ausente; crear solo el armazón mínimo si hace falta compilar. Implementar, repetir hasta verde, revisar `git diff --check` y compilar el proyecto afectado. Cada tarea se cierra con **un commit convencional en español**, sin adelantar archivos de la siguiente.
3. En las tareas de base real, usar `Api.IntegrationTests` con Docker/Testcontainers; no omitirlas cuando Docker esté disponible. `dotnet test --project tests/<Proyecto>/<Proyecto>.csproj -- --filter-class "<Clase>"` ejecuta una clase focal. El test rojo debe ser de comportamiento, política o SQL, no solo de existencia del archivo.
4. Las rutas son relativas a este repo salvo prefijo `../`. `←` indica copia de archivo real y adaptación. Cada carpeta nueva del mapa recibe `AGENTS.md` y `CLAUDE.md` (`@AGENTS.md`) en la misma tarea. Los cambios de documentación de una regla se commitean con su código. Si algo técnico falta en las fuentes, se elige la opción más simple compatible y se registra en «Decisiones tomadas»; solo se pregunta si cambia el producto o contradice una regla escrita.

## Tareas

### Tarea 1. Contratos de las tres clases y política de partes

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Domain/Common/{IAuditable,ISoftDeletable,ITenantOwned,ICompanyOwned,IPublishedByBusiness,IConsumerBusinessShared,IVersioned,NotAuditedAttribute,Party,PartyPolicy}.cs`; `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Tenancy/PartyPolicyTests.cs`. Completar los punteros `AGENTS.md`/`CLAUDE.md` de `Domain/Common` si se amplía su regla.

**Respaldo:** plan maestro E2.1 y E2.11; `multitenancy.md` §4; `arbol.md` Domain/Common y ADR 0030–0032; fichas `multitenancy.md`, `auditoria.md`, `concurrencia.md`.

**TDD:** `PartyPolicyTests` falla cuando una parte distinta de la activa intenta actuar; luego pasa. `IVersioned` expone `uint Version`; las interfaces llevan las columnas indicadas y ninguna referencia a EF. **Commit:** `feat: definir clases de datos y política de partes`.

### Tarea 2. Estados puros de organización y miembro

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Domain/Tenancy/{Tenant,TenantKind,TenantStatus,TenantErrors,Member,MemberStatus,MemberErrors,AccessErrors}.cs`; `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Tenancy/{TenantTests,MemberTests}.cs`; agregar las claves nuevas a `src/ArquitecturaBaseMultitenant.Application/Resources/{Errors,Errors.en}.resx`; punteros de `Domain/Tenancy`.

**Respaldo:** plan maestro E2.1; `arbol.md` Domain/Tenancy; `multitenancy.md` §§2, 7; fichas `multitenancy.md`, `result-y-errores.md`.

**TDD:** transiciones permitidas de `PendingApproval`, `Provisioning`, `Active`, `Suspended`, `Closed` y de `Invited`, `Active`, `Inactive`, `Removed`; se rechazan transiciones inválidas. `Member` no tiene `IsOwner`. **Commit:** `feat: modelar estados de organizaciones y miembros`.

### Tarea 3. Modelo de auditoría append-only

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Domain/Auditing/{AuditEntry,AuditAction,AuditActorKind}.cs`; `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Auditing/AuditEntryTests.cs`; punteros de `Domain/Auditing`.

**Respaldo:** plan maestro E2.4; `arbol.md` Domain/Auditing; ficha `auditoria.md`.

**TDD:** una entrada conserva actor, tenant, acción, instante UTC y diff sin poder mutarse ni borrar su historia. **Commit:** `feat: definir entradas de auditoría inmutables`.

### Tarea 4. Entidades globales de referencia

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Domain/ReferenceData/{Currency,CurrencyTranslation,Country,CountryTranslation,ReferenceTimeZone,TimeZoneCountry,TimeZoneTranslation,Culture,CultureTranslation,TaxIdType,TaxIdTypeTranslation}.cs`; `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ReferenceData/ReferenceEntitiesTests.cs`; punteros de `Domain/ReferenceData`.

**Respaldo:** plan maestro E2.15; `datos-de-referencia.md` §§2, 4, 8; `arbol.md` Domain/ReferenceData; ficha `datos-de-referencia.md`.

**TDD:** claves/traducciones y `TimeZoneCountries` de muchos a muchos conservan `IsEnabled`, vigencia y orden; las entidades globales no implementan interfaces RLS. **Commit:** `feat: modelar catálogos globales de referencia`.

### Tarea 5. Puertos de persistencia y dobles de pruebas

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/{IUnitOfWork,CommitPolicy,CommitPolicyExtensions,ITenantScope,IAuditLog}.cs`, `src/ArquitecturaBaseMultitenant.Application/Interfaces/Integrations/Request/ITenantContext.cs`, `src/ArquitecturaBaseMultitenant.Application/Common/Exceptions/{UniqueConstraintViolationException,ConcurrencyConflictException}.cs`; `tests/ArquitecturaBaseMultitenant.Application.UnitTests/TestDoubles/{FakeUnitOfWork,FakeTenantContext}.cs`; `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Persistence/FakeUnitOfWorkTests.cs`; punteros de carpetas nuevas. Copiar y adaptar `IUnitOfWork`, `CommitPolicy`, `CommitPolicyExtensions`, `UniqueConstraintViolationException` y `FakeUnitOfWork` desde `../ArquitecturaBase/src/ArquitecturaBase.Application/Interfaces/Persistence/`, `../ArquitecturaBase/src/ArquitecturaBase.Application/Common/Exceptions/` y `../ArquitecturaBase/tests/ArquitecturaBase.Application.UnitTests/TestDoubles/` respectivamente.

**Respaldo:** plan maestro E2.2, E2.5 y E2.11; `backend.md` §5; `arbol.md` Application/Interfaces; fichas `guardado.md`, `tests.md`, `concurrencia.md`.

**TDD:** el fake cuenta commits/rollbacks y respeta `OnSuccess`/`OnAnyResult`; un límite anidado falla. **Commit:** `feat: definir límite de persistencia y dobles`.

### Tarea 6. Contexto y scope de tenant

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/TenantContext.cs`; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/TenantContextTests.cs`; punteros de `Infrastructure/Persistence` si faltan.

**Respaldo:** plan maestro E2.2; `multitenancy.md` §§8–9, §12; `arbol.md` Infrastructure/Persistence; ficha `multitenancy.md`.

**TDD:** `Enter` restaura el contexto anterior al salir, no admite scope anidado incompatible ni entrada durante una transacción; el tenant no sale de host, header o cuerpo. **Commit:** `feat: implementar contexto y scope de tenant`.

### Tarea 7. DbContext único y clase mínima de Identity

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/ApplicationDbContext.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Identity/ApplicationUser.cs` (solo shell CLR E2), `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/PersistenceRegistration.cs`; actualizar `src/ArquitecturaBaseMultitenant.Infrastructure/DependencyInjection.cs`, `docs/architecture/arbol.md` para aclarar shell E2/modelo E3; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/DbContextModelTests.cs`. Adaptar patrón de `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Persistence/{ApplicationDbContext,PersistenceRegistration}.cs` sin copiar su IdentityDbContext ni `EnsureCreated`.

**Respaldo:** plan maestro E2.3; `backend.md` §§4.5, 9; `arbol.md` Infrastructure/Persistence e Identity; ficha `persistencia-ef.md`.

**TDD:** contexto `IdentityUserContext<ApplicationUser,Guid>` y `IDataProtectionKeyContext`, cinco esquemas declarados, sin tabla de usuarios E2; `DependencyInjection` registra un contexto scoped. **Commit:** `feat: crear contexto único de persistencia`.

### Tarea 8. Filtros nombrados y clasificación del modelo

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Extensions/ModelBuilderExtensions.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Rls/TenantIsolationModelValidator.cs`; modificar `ApplicationDbContext.cs`; crear temprano `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/Isolation/{Widget,Poster,Deal,IsolationModelCustomizer}.cs` y `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/ModelFiltersTests.cs`; punteros de `Extensions`, `Rls` y `TestFeatures/Isolation`.

**Respaldo:** plan maestro E2.3 y E2.6; `multitenancy.md` §§4, 9; `backend.md` §9; ficha `multitenancy.md`.

**TDD:** modelo de test con `Widget` (`IVersioned`), `Poster` y `Deal` recibe los filtros `Tenant`, `Public`, `Parties`, `SoftDelete` según interfaz; una entidad sin clase, esquema o filtro falla en validación. La propiedad `Email` de prueba se agrega recién en Tarea 10, junto con su conversor; la tabla de estas entidades aún no existe. **Commit:** `feat: aplicar filtros nombrados por clase de dato`.

### Tarea 9. Configuraciones EF de referencias

**Archivos:** crear once configuraciones bajo `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/ReferenceData/`: `{Currency,CurrencyTranslation,Country,CountryTranslation,TimeZone,TimeZoneCountry,TimeZoneTranslation,Culture,CultureTranslation,TaxIdType,TaxIdTypeTranslation}Configuration.cs`; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/ReferenceDataModelTests.cs`; punteros de `Configurations/Platform/ReferenceData`.

**Respaldo:** plan maestro E2.15; `datos-de-referencia.md` §2; `arbol.md` Configurations/Platform/ReferenceData; ficha `persistencia-ef.md`.

**TDD:** todas las claves, traducciones, FK anulables, vigencia, `IsEnabled`, `SortOrder` y asociación múltiple se mapean en `platform`, sin RLS; una configuración por entidad. **Commit:** `feat: mapear catálogos de referencia en platform`.

### Tarea 10. Configuraciones de auditoría, idempotencia y convenciones

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant/AuditEntryConfiguration.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/IdempotencyKey.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/IdempotencyKeyConfiguration.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Conventions/{VersionedConvention,EmailConvention}.cs`; modificar `ApplicationDbContext.cs` y el `TestFeatures/Isolation/Widget.cs` de Tarea 8 para agregar una propiedad `Email` mapeada por la convención; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/{AuditAndIdempotencyModelTests,VersionedModelTests}.cs`; punteros de carpetas nuevas.

**Respaldo:** plan maestro E2.11, E2.13–14; `arbol.md` Configurations/Conventions/P1/P3/P6; `backend.md` §9; fichas `auditoria.md`, `idempotencia.md`, `concurrencia.md`.

**TDD:** `AuditEntry` en `tenant`, clave idempotente única en `platform`, `IVersioned` mapea `xmin` como row version, `Email` usa conversor y todo `decimal` tiene precisión explícita. **Commit:** `feat: mapear auditoría idempotencia y convenciones EF`.

### Tarea 11. Plantillas RLS y triggers

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Rls/{RlsSql,RlsMigrationBuilderExtensions}.cs`; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/RlsSqlTests.cs`; punteros `Rls`.

**Respaldo:** plan maestro E2.6; `multitenancy.md` §§4, 9, 13; `backend.md` §9; ficha `multitenancy.md`.

**TDD:** `EnableTenantRls`, `EnablePublicRls`, `EnablePartiesRls` generan políticas con `FORCE ROW LEVEL SECURITY`, `nullif(current_setting('app.tenant_id', true), '')::uuid`, `WITH CHECK` y `prevent_tenant_change`; audit append-only usa `prevent_update_delete`. En `public_site`, SELECT admite publicado o propio, pero INSERT/UPDATE/DELETE solo propio: una fila publicada ajena jamás puede mutarse. Identificadores SQL se validan/citan. **Commit:** `feat: generar políticas RLS y triggers reutilizables`.

### Tarea 12. Primera migración y guía de migraciones

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/<ts>_InitialSchema.cs`, `ApplicationDbContextModelSnapshot.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/{AGENTS,CLAUDE}.md`, `docs/guides/migracion.md`; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/MigrationsTests.cs`. Usar `dotnet ef migrations add InitialSchema --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output-dir Persistence/Migrations` y revisar el SQL generado.

**Respaldo:** plan maestro E2.6, E2.14–15; `backend.md` §9; `arbol.md` Migrations y docs/guides; fichas `persistencia-ef.md`, `paginado-y-busqueda.md`.

**TDD:** la migración crea `platform`, `identity`, `tenant`, `public_site`, `engagement`, funciones/triggers, `unaccent`, `pg_trgm`, `f_unaccent`, grants a `mt_app`, tablas de referencias, `AuditEntries` e `IdempotencyKeys`; no crea tablas de TestFeatures ni `AspNetUsers`. `MigrationsTests` detecta una migración ausente. **Commit:** `feat: crear migración inicial con esquemas y RLS`.

### Tarea 13. Bootstrap, roles y collation ICU

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/{DatabaseBootstrapExtensions,DesignTimeApplicationDbContextFactory,UniqueViolations}.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Rls/RuntimeRoleValidator.cs`; modificar `src/ArquitecturaBaseMultitenant.AppHost/AppHost.cs`, `src/ArquitecturaBaseMultitenant.Api/Program.cs`, `docs/architecture/backend.md` y adelantar en `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Support/ApiFactory.cs` la creación mínima de Testcontainers para los hosts Development; crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/BootstrapTests.cs`.

**Respaldo:** plan maestro E2.7 y E2.12; `backend.md` §§4.5, 9, 16; `multitenancy.md` §9; `arbol.md` DatabaseBootstrapExtensions; ficha `persistencia-ef.md`.

**TDD:** AppHost retira `AddDatabase("appdb")` para que no cree una base con collation equivocada y entrega las tres conexiones secretas; bootstrap superusuario crea roles y base ICU `es-AR`, `appdb-admin` migra como dueño y `appdb` usa `mt_app`. Una base E0 ya existente solo se recrea si está vacía de tablas de usuario; con datos, se detiene sin borrarlos. El hook de seed se conecta recién en Tarea 26. El validador rechaza rol privilegiado o collation incorrecta antes de servir requests. Los tests E1 de OpenAPI que levantan Development usan el contenedor de `ApiFactory` desde esta tarea, sin depender de la fixture completa de T14. **Commit:** `feat: separar bootstrap administrador y rol de runtime`.

### Tarea 14. Fixture real con Docker y tablas de aislamiento

**Archivos:** completar `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Support/ApiFactory.cs` (Testcontainers mínimo iniciado en T13); crear `Support/{TenantFixture,RuntimeRoleConnection,FailingCommitUnitOfWork}.cs`, `TestFeatures/Isolation/IsolationSchema.cs`; completar el `IsolationModelCustomizer.cs` de Tarea 8; crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/IsolationSchemaTests.cs`; punteros de `Support` y `TestFeatures/Isolation`. Adaptar `../ArquitecturaBase/tests/ArquitecturaBase.Api.IntegrationTests/Support/{ApiFactory,FailingCommitUnitOfWork}.cs`, sin su `EnsureCreated`.

**Respaldo:** plan maestro E2.10; `multitenancy.md` §13; `rules/tests.md` «Cómo se hace» (IsolationSchema); `arbol.md` tests/Support/TestFeatures.

**TDD:** Testcontainers PostgreSQL 18 crea base ICU `es-AR`, aplica las migraciones productivas y después `IsolationSchema.ApplyAsync(connection)`; las tres tablas de prueba reutilizan `RlsSql`, no aparecen en migraciones productivas. **Commit:** `test: levantar PostgreSQL real para aislamiento`.

### Tarea 15. Sello y contexto de conexión

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Interceptors/{TenantConnectionInterceptor,TenantStampInterceptor}.cs`; modificar `PersistenceRegistration.cs`; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/TenantStampTests.cs`; punteros de `Interceptors`.

**Respaldo:** plan maestro E2.4; `multitenancy.md` §9; `backend.md` §§5, 9; ficha `multitenancy.md`.

**TDD:** al abrir conexión se fija `app.tenant_id` o `''`; al agregar se sellan columnas de la clase activa y una actualización no puede cambiarlas. El test usa Testcontainers y verifica que un connection pool no hereda el tenant anterior. **Commit:** `feat: sellar tenant y contexto de conexión`.

### Tarea 16. Borrado lógico y auditoría automática

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Interceptors/{SoftDeleteInterceptor,AuditableEntityInterceptor,AuditTrailInterceptor}.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/AuditLog.cs`; modificar `PersistenceRegistration.cs`; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/{SoftDeleteTests,AuditingTests,AuditTrailTests}.cs`; punteros de `Repositories`. Adaptar `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Persistence/Interceptors/{SoftDeleteInterceptor,AuditableEntityInterceptor}.cs`.

**Respaldo:** plan maestro E2.4; `backend.md` §§5, 9; `arbol.md` Interceptors/Repositories; fichas `auditoria.md`, `persistencia-ef.md`.

**TDD:** un delete pasa a marca lógica; auditoría usa `TimeProvider`, actor y diff sin propiedades `[NotAudited]`; `AuditEntry` se inserta en la misma transacción y no se actualiza/borra. Un `Deal` compartido genera una entrada por cada parte; el interceptor deriva los tenant IDs de contraparte del `ChangeTracker` y los fija en `app.audit_counterpart_tenant_ids` como GUC local de la transacción. Una política RLS adicional permite solo esos INSERT en `AuditEntries`; SELECT, UPDATE y DELETE conservan la barrera normal. El límite de confianza de esa GUC es el mismo de `app.tenant_id` en el runtime. **Commit:** `feat: aplicar borrado lógico y auditoría transaccional`.

### Tarea 17. UnitOfWork único, errores de base y concurrencia

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/UnitOfWork.cs`; modificar `PersistenceRegistration.cs`, `src/ArquitecturaBaseMultitenant.Api/ErrorHandling/{ApiErrorCodes,GlobalExceptionHandler}.cs`, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Support/TestControllerApplicationPart.cs`; crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/{UnitOfWorkTests,ConcurrencyTests}.cs`, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/Isolation/WidgetsController.cs`, `tests/ArquitecturaBaseMultitenant.ArchitectureTests/VersionedContractTests.cs`. Adaptar `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Persistence/{UnitOfWork,UniqueViolations}.cs` (`UniqueViolations` ya nació en T13).

**Respaldo:** plan maestro E2.5 y E2.11; `backend.md` §5; `arbol.md` UnitOfWork/P1; fichas `guardado.md`, `concurrencia.md`.

**TDD:** un límite `READ COMMITTED`, un `SaveChanges`, `set_config(..., true)` al comenzar, `OnSuccess`/`OnAnyResult`, rollback y tracker limpio ante fallo, anidación prohibida; 23505 → excepción única, conflicto `xmin` → 409 `General.ConcurrencyConflict` y contrato de edición con versión. **Commit:** `feat: implementar UnitOfWork y conflictos de concurrencia`.

### Tarea 18. Aislamiento privado incluso sin filtros EF

**Archivos:** crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/{CrossTenantIsolationTests,RlsBarrierTests}.cs`; completar `TenantFixture`, `RuntimeRoleConnection`, `Widget` e `IsolationSchema` según los casos. Documentar cualquier ajuste en las fichas en el mismo commit.

**Respaldo:** plan maestro E2.10; `multitenancy.md` §§4, 9, 13; `rules/tests.md` IsolationSchema; ficha `multitenancy.md`.

**TDD:** `Rls_blocks_cross_tenant_even_with_filters_ignored`: como `mt_app`, ignorar el filtro EF o consultar SQL crudo no expone ni permite editar un `Widget` ajeno; tenant A y espacio personal de Kevin siguen separados. **Commit:** `test: probar barrera RLS para datos privados`.

### Tarea 19. Aislamiento público y compartido

**Archivos:** crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/PublicAndSharedRowsTests.cs`; completar `TestFeatures/Isolation/{Poster,Deal}.cs`, `TenantFixture` y `IsolationSchema`.

**Respaldo:** plan maestro E2.10; `multitenancy.md` §§4, 9, 13; `arbol.md` PublicAndSharedRowsTests; ficha `multitenancy.md`.

**TDD:** `Poster` en borrador solo para su empresa; publicado visible al público; `Deal` visible únicamente a persona y empresa participantes, con copia propia de datos y `PartyPolicy`; otra empresa/persona no ven ni mutan filas. **Commit:** `test: probar barreras para datos públicos y compartidos`.

### Tarea 20. Rol runtime, políticas y columnas inmutables

**Archivos:** crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/{RuntimeRoleTests,RlsPolicyInventoryTests,TenantColumnsImmutabilityTests}.cs`; ajustar `RuntimeRoleValidator` o `RlsSql` solo si el test rojo revela un defecto.

**Respaldo:** plan maestro E2.6–7 y E2.10; `multitenancy.md` §§9, 13; `arbol.md` tests/Tenancy; ficha `multitenancy.md`.

**TDD:** `Runtime_role_is_not_privileged` confirma `mt_app` sin superuser, owner ni `BYPASSRLS`; inventario confirma `FORCE` y política en cada tabla protegida; trigger bloquea cambio de columnas tenant y update/delete de auditoría. **Commit:** `test: vigilar rol runtime y políticas de aislamiento`.

### Tarea 21. Claves de caché, locks y transacciones

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Caching/{CachingRegistration,CacheKeys}.cs`, `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Extensions/{AdvisoryLockKeys,AdvisoryLockExtensions,TransactionExtensions}.cs`; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/AdvisoryLockKeysTests.cs`, `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/CacheKeyScopeTests.cs`; punteros de `Caching`. Adaptar `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Persistence/Extensions/{AdvisoryLockKeys,AdvisoryLockExtensions,TransactionExtensions}.cs`.

**Respaldo:** plan maestro E2.8; `multitenancy.md` §12; `backend.md` §5; fichas `multitenancy.md`, `guardado.md`.

**TDD:** toda clave privada incluye tenant y prefijo `t:`, pública `s:`, de usuario `u:`, global `p:`; lock siempre incluye tenant y exige transacción abierta. **Commit:** `feat: aislar claves de caché y locks por tenant`.

### Tarea 22. Ejecución de jobs por tenant

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/BackgroundJobs/{BackgroundJobsRegistration,TenantJobRunner}.cs`; `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/TenantJobRunnerTests.cs`; punteros de `BackgroundJobs`.

**Respaldo:** plan maestro E2.8; `arbol.md` BackgroundJobs; `multitenancy.md` §§8, 12; ficha `multitenancy.md`.

**TDD:** cada job abre scope DI y `ITenantScope.Enter(tenantId)`, no hereda contexto al siguiente; E2 recibe IDs de la prueba, E3 conectará `ITenantReader` de organizaciones activas. **Commit:** `feat: ejecutar trabajos dentro de un tenant`.

### Tarea 23. Búsqueda PostgreSQL e índices de orden

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Extensions/SearchFunctions.cs`; ampliar `QueryableExtensions.cs`; crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/{SearchTests,PaginationTests,SortIndexTests,CollationTests}.cs`; actualizar migración y guía si el SQL revela una omisión.

**Respaldo:** plan maestro E2.9 y E2.12; `backend.md` §9 «Paginado, orden y búsqueda»; `arbol.md` Extensions/tests; fichas `paginado-y-busqueda.md`, `persistencia-ef.md`.

**TDD:** `perez` encuentra `Pérez`, `%` y `_` no son comodines del usuario; orden y página estables, sort no permitido rechazado; índice `(TenantId, campo, Id)` por `SortMap`; collation ICU ordena Álvarez, Nuñez, Ñandú, Zapata. **Commit:** `feat: buscar y ordenar con PostgreSQL e ICU`.

### Tarea 24. Cursor estable para listados cambiantes

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Extensions/CursorCodec.cs`; ampliar `QueryableExtensions.cs`; crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/CursorPaginationTests.cs`.

**Respaldo:** plan maestro E2.9; `backend.md` §9 «Paginado, orden y búsqueda»; `arbol.md` CursorCodec; ficha `paginado-y-busqueda.md`.

**TDD:** cursor base64url codifica campo de orden + Id, rechaza datos inválidos y no duplica ni salta filas cuando se inserta entre páginas. **Commit:** `feat: paginar con cursor estable`.

### Tarea 25. Reserva, replay y limpieza de idempotencia

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Idempotency/{IdempotencyStore,IdempotencyCleanupWorker}.cs`, `src/ArquitecturaBaseMultitenant.Api/Idempotency/IdempotencyFilter.cs`; modificar DI, atributo E1 y registro de filtro; crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Api/IdempotencyTests.cs`; punteros de `Idempotency` en Api e Infrastructure.

**Respaldo:** plan maestro E2.14; `backend.md` §20; `arbol.md` P6; ficha `idempotencia.md`.

**TDD:** dos pedidos paralelos con la misma clave ejecutan uno, replay conserva status/cuerpo y header, mismo UUID con otro cuerpo/ruta da 422, en curso da 409, sin clave 400, 5xx libera reserva y worker vence a 24 h. La respuesta se guarda después del commit del UoW. **Commit:** `feat: persistir reservas y respuestas idempotentes`.

### Tarea 26. Seed oficial idempotente de referencias

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/ReferenceDataSeeder.cs`; conectar en `DatabaseBootstrapExtensions.cs`; crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/ReferenceDataSeederTests.cs`; punteros de `Seed`.

**Respaldo:** plan maestro E2.15; `datos-de-referencia.md` §§2–4, 8; `arbol.md` Seed; ficha `datos-de-referencia.md`.

**TDD:** upsert de cinco JSON, segunda corrida sin duplicados ni cambios espurios, traducciones y `TimeZoneCountries` multipaís; al retirar un código/asociación queda deshabilitado, nunca borrado. `IsEnabled` conserva vigencia y overrides generados. **Commit:** `feat: sembrar referencias oficiales sin borrar historia`.

### Tarea 27. Reader de referencias con HybridCache

**Archivos:** crear `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/ReferenceDataReader.cs`; modificar `DependencyInjection.cs`, `PersistenceRegistration.cs` y `CachingRegistration.cs`; crear `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/ReferenceDataReaderTests.cs`. Los tests y aserciones E1 de comportamiento de endpoint/formatos quedan intactos; el test E1 `ReferenceDataCatalogTests.Five_catalogs_are_registered_as_one_json_adapter`, que exige una implementación DI E1, se reemplaza por una prueba de composición de `ReferenceDataReader` contra base real. Solo se adaptan fixtures de infraestructura si la base lo exige. `JsonReferenceDataCatalog` queda como lector de entrada del seed, no como implementación DI de los cinco puertos.

**Respaldo:** plan maestro E2.15; `datos-de-referencia.md` §§4, 8; `backend.md` §4.5; `arbol.md` Readers; ficha `datos-de-referencia.md`.

**TDD:** reader implementa cinco puertos sobre `platform`, usa claves `p:ref:<catálogo>` e invalida al cambiar el seed; devuelve todas las filas con `IsEnabled`, incluidas históricas, sin alterar GET ni los 74 casos de formato. **Commit:** `feat: leer referencias desde PostgreSQL con caché`.

### Tarea 28. Guardas de arquitectura E2

**Archivos:** crear `tests/ArquitecturaBaseMultitenant.ArchitectureTests/{TransactionBoundaryTests,EntityConfigurationTests,DataClassificationTests,TenantScopeUsageTests,QueryFilterBypassTests}.cs`; ampliar `HarnessTests.cs` y fichas/punteros afectados. Adaptar `EntityConfigurationTests` de `../ArquitecturaBase/tests/ArquitecturaBase.ArchitectureTests/EntityConfigurationTests.cs`.

**Respaldo:** plan maestro E2.10; `arbol.md` ArchitectureTests; `rules/tests.md`, `guardado.md`, `multitenancy.md`, `persistencia-ef.md`.

**TDD:** solo servicios públicos de Application reciben UoW; cada entidad tiene una configuración; ninguna entidad queda sin clase o excepción explícita; `ITenantScope` y `IgnoreQueryFilters` solo se usan en listas blancas. Los tests fallan ante una violación deliberada en fixture/IL. **Commit:** `test: cerrar guardas de arquitectura de persistencia`.

### Tarea 29. Cierre del arnés E2

**Archivos:** actualizar `tests/ArquitecturaBaseMultitenant.ArchitectureTests/HarnessStage.cs` a 2 y `HarnessTests.cs`; completar `AGENTS.md`/`CLAUDE.md` de todas las carpetas E2 del mapa y las fichas `docs/rules/`; regenerar `docs/contracts/openapi.json` si cambió (no hay pantalla ni API nueva planificada en E2).

**Respaldo:** plan maestro «Reglas para todas las etapas» y E2 «Puerta»; `docs/architecture/arnes.md` §3; `arbol.md` [E2]; `rules/tests.md`.

**TDD:** `HarnessTests` falla primero por Stage 2 y luego pasa con enlaces, archivos y pruebas reales; build/test y contratos finales verdes. **Commit:** `chore: cerrar arnés de Etapa 2`.

### Tarea 30. Cierre del arnés frontend E2

**Archivos:** actualizar `../ArquitecturaBaseMutitenantFront/src/test/HarnessStage.ts` a 2; regenerar `../ArquitecturaBaseMutitenantFront/src/shared/api/generated/schema.d.ts` solo si OpenAPI cambió; ajustar `../ArquitecturaBaseMutitenantFront/src/test/harness.test.ts` únicamente si descubre un enlace real faltante.

**Respaldo:** plan maestro «Reglas para todas las etapas»; `../ArquitecturaBaseMutitenantFront/docs/architecture/arnes.md` y `arbol.md` (HarnessStage sube al cerrar **cada** etapa).

**TDD:** `harness.test.ts` pasa con Stage 2; `npm run contracts:check`, build, lint y test completos verdes. **Commit front:** `chore: cerrar arnés frontend de Etapa 2`.

## Puerta completa de Etapa 2

1. **Back:** `dotnet build ArquitecturaBaseMultitenant.slnx` termina con 0 advertencias y 0 errores; `dotnet test` pasa completo **con Docker y Testcontainers**, sin saltar suites de persistencia. Ejecutar y registrar expresamente `Rls_blocks_cross_tenant_even_with_filters_ignored` y `Runtime_role_is_not_privileged`. El workflow de CI debe ejecutar esos tests sin skip; con commits solo locales y sin push, se corre aquí la misma puerta con Docker y la evidencia de CI remoto queda para el primer push autorizado.
2. **Base y aislamiento:** PostgreSQL 18 real tiene los cinco esquemas; la base de Development y la fixture se crean con ICU `es-AR`; el validador confirma collation y que `mt_app` no es privilegiado. Migración inicial real aplica `FORCE RLS`, políticas y triggers a cada tabla protegida; `Widget`, `Poster` y `Deal` se crean después con `IsolationSchema` y las mismas plantillas; SQL crudo como `mt_app` no atraviesa la barrera. `CollationTests`, `RlsPolicyInventoryTests`, `TenantColumnsImmutabilityTests`, `PublicAndSharedRowsTests` y `MigrationsTests` pasan.
3. **Guardado y estándares:** `UnitOfWorkTests` cubre política, transacción, rollback, 23505 y concurrencia 409; auditoría y soft delete pasan; búsqueda, cursor, índices, ICU e idempotencia pasan. Guardas de arquitectura E2 verdes.
4. **Referencias:** migración crea las once tablas globales y traducciones en `platform`, con `TimeZoneCountries` multipaís; `ReferenceDataSeederTests` demuestra dos corridas idempotentes, upsert, deshabilitación sin borrado e invalidación; `ReferenceDataReader` con HybridCache reemplaza al adaptador JSON en DI. GET sigue entregando todas las filas con `isEnabled`; tests E1 de catálogo, API y los 74 formatos siguen verdes.
5. **Front y contratos:** `npm run build`, `npm run lint`, `npm test` limpios; `docs/contracts/openapi.json` y `src/shared/api/generated/schema.d.ts` reproducibles con `npm run contracts` y `npm run contracts:check`. Inventario de rutas actualizado si alguna ruta cambió; no se crean pantallas.
6. **Arnés y operación:** todas las carpetas E2 tienen `AGENTS.md` + `CLAUDE.md`, fichas enlazan archivos/tests reales, `HarnessTests` y front `harness.test.ts` verdes; `HarnessStage` pasa a **2 en ambos repos**. `aspire run` levanta PostgreSQL migrado, Api y front; `GET /alive` devuelve 200; luego `aspire stop`. Ambos repos quedan en `main`, con commits locales por tarea y sin push.
