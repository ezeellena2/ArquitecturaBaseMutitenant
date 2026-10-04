# Etapa 5: Plataforma — plan ejecutable

> **Para agentes:** aplicar `writing-plans` y ejecutar con `executing-plans` en este chat. El usuario autorizó `main`, sin worktree ni push. Revisar el plan con un revisor de documentos, no delegar programación. Las casillas registran evidencia real; una tarea termina con su commit explícito y verificado.

**Objetivo:** completar la administración global de organizaciones, cuentas, recuperaciones, operadores, módulos, documentos legales, auditoría y configuración, con API real y pantallas elegidas del lienzo.

**Arquitectura:** Domain ← Application ← Infrastructure; Api compone. Copiar Roles, E3 y Organización E6. Los datos de Plataforma son globales; los datos privados de una organización se consultan solo después de autorizar, registrar el SecurityEvent y entrar con ITenantScope, antes de abrir su transacción. En lo que falta de E5, la transacción la abre el método público del servicio, como RoleService; los helpers no reciben IUnitOfWork y no se crean clases que solo reenvían ni excepciones en TransactionBoundaryTests. Lo ya implementado se consolida antes de E7, según la revisión de calidad y ADR 0039.

**Tecnologías:** .NET 10, EF Core/Npgsql, PostgreSQL 18, Aspire 13.5, Microsoft.FeatureManagement.AspNetCore, HybridCache, React/TypeScript, TanStack Query, i18next, Vitest y Playwright.

**Cambio de orden (2026-10-03, aprobado por el usuario).** Codex dejó la etapa abierta después de T09, con T10 sin commitear, y la sigue Claude en dos partes:

1. **Back, ya:** T10 a T18 y el punto pendiente de T08 (`features` en `/api/me`), sin pantallas.
2. **Front, después de la [consolidación](2026-10-03-consolidacion.md):** T19 a T25. Así las pantallas de Plataforma nacen con las piezas comunes del front (errores, permisos, paginado, accesos y edición) y no repiten la duplicación que encontró la revisión de calidad.

La etapa cierra con T25.

## Fuentes, restricciones y punto de partida

- Back: `C:/Users/ezequ/source/repos/ArquitecturaBaseMutitenant`, main limpio, `a12edb7`.
- Front: `C:/Users/ezequ/source/repos/ArquitecturaBaseMutitenantFront`, main limpio, `44bf951`.
- E4 y E6 cerradas. Conservar ClosedStages `{0,1,2,3,4,6}` hasta cerrar E5; el salto no reabre E6.
- Fuente visual: ver los tableros vigentes de front `docs/design/lienzo/` (v73 o posterior), escritorio y teléfonos validados en formato app. Solo la conversación de diseño modifica tableros/canvas/README. La propuesta descartada no es fuente. Ningún tablero «por refinar» se programa. Las capturas de implementación se envían al usuario antes del cierre y no se autoaprueban.
- Teléfono: formato app del Criterio-Marco actual; barra de estado, título y contexto, navegación inferior con Más, listas de borde a borde, acción fija y hojas inferiores. No copiar el teléfono anterior como una web reducida.
- WhatsApp, WA-*, M-WA-* y Meta fuera del alcance; no añadir sus rutas, módulos, permisos ni semillas.
- Development solo conserva el operador real configurado en user-secrets. Tests, capturas con datos y E2E usan PostgreSQL/pickup aislados. No imprimir direcciones, códigos, tokens ni secretos.
- No repetir la puerta E4/E6 al inicio. No añadir guardas de arquitectura ni tests de arnés fuera de FeatureGateTests y ModuleControllersTests exigidos por E5; actualizar inventarios/whitelists existentes que cambien.
- Leer AGENTS de cada carpeta y fichas antes de tocarla. Técnicas menores: primero ArquitecturaBase, luego la alternativa más simple; registrar decisiones. Solo una elección de producto o contradicción real se consulta.

## Protocolo de cada tarea de lógica

1. Escribir casos de conducta en el proyecto/carpeta correspondiente y ejecutar el focal: registrar rojo por la conducta ausente (un error de entorno no cuenta como rojo).
2. Implementar lo mínimo con los contratos descritos abajo; validar antes del límite, locks → lecturas → reglas → escrituras, invalidación tras commit, OperationLog y recursos es/en.
3. Repetir focal en verde; build afectado sin advertencias. Si hay HTTP, identidad, persistencia o aislamiento, añadir integración PostgreSQL y controles 401/403/aislamiento/concurrencia/idempotencia aplicables.
4. Mantener docs afectadas en el mismo commit; `git diff --check`; `git add -- <rutas de la tarea>` y conventional commit en español. No usar `git add .`.

Comandos focales base:

```text
dotnet test --project tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj -- --filter-class "<namespace>.<Clase>"
dotnet test --project tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj -- --filter-class "<namespace>.<Clase>"
dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class "<namespace>.<Clase>"
dotnet build src/ArquitecturaBaseMultitenant.Api/ArquitecturaBaseMultitenant.Api.csproj
npm test -- <ruta.test.tsx>
npm run lint
npm run build
```

Si Visual Studio mantiene bloqueados bin/obj, usar `-p:ArtifactsPath=<directorio temporal único>` en restore/build/test, sin modificar proyectos. Docker/Testcontainers requiere acceso al pipe fuera del sandbox. No desactivar advertencias ni declarar verde la integración por un test sin base.

## Mapa de implementación y contratos

| Pieza | Archivos y responsabilidad |
|---|---|
| Operadores exclusivos | Migrar T01/T03 a permisos individuales, asignación única por cuenta y catálogo PlatformPermissions (9 claves); sin Owner/Support. Invitación por correo a identidad exclusivamente Platform, rechazo de cuenta previa, protección del último operador con operators.manage |
| Datos opcionales | `Domain/Features/{Features,FeatureDefinition,TenantFeature,FeatureSource}.cs`; clave, acceso, default, origen Plan/Manual/Trial y vencimiento UTC |
| Recuperación | `Domain/Platform/{AccountRecoveryRequest,AccountRecoveryStatus,AccountRecoveryErrors}.cs`; Pending/Approved/Rejected, método anterior y nuevo verificado, decisión con motivo; valores personales fuera de logs/auditoría |
| Modelos y validación | `Application/Models/Platform/` y `Application/Validation/Platform/`: requests y responses explícitos; listados paginados y auditoría por cursor; cada cambio visible conserva version |
| Servicios | `Application/Services/Platform/`: TenantAdministrationService, PlatformAccountService, PlatformOperatorService, PlatformAuditService, PlatformSettingsService, PlatformLegalService, PlatformRecoveryService y PlatformActionGuard; puertos en `Application/Interfaces/Services/` |
| Persistencia | `Application/Interfaces/Persistence/{IPlatformReader,IPlatformRoleAssignmentRepository,IPlatformRoleAssignmentReader,ITenantFeatureRepository,ITenantFeatureReader,IAccountRecoveryRepository,IAccountRecoveryReader,ISecurityEventReader}.cs`; implementaciones específicas en Infrastructure/Persistence/{Repositories,Readers,Configurations/Platform,Migrations} |
| Borde HTTP | `Api/Controllers/Platform/` y `Api/Contracts/Platform/`; controllers separados por área; recuperación pública en `Api/Controllers/Auth/AccountRecoveryController.cs` con AllowAnonymous |
| Integraciones | `Infrastructure/Features/{TenantFeatureFilter,FeatureService,DisabledFeatureHandler}.cs`, registro DI; recuperación/cambios globales encolan por los puertos existentes de correo y avisos de cuenta |
| Front | `src/areas/platform/{tenants,accounts,recoveries,legal,audit,settings}/{api,pages}/`; cuenta incluye operadores. Recuperación pública en `src/areas/public/auth/pages/RecoverAccountPage.tsx` |
| Marco y módulos | `src/layouts/navigation/{types,platform}.ts`, PrivateLayoutFrame, Sidebar, AdminPanel; `src/auth/{useFeature,Feature,FeatureRoute}.tsx`, rutas y filtrado de navegación |
| Textos y docs | resources es/en del back; namespaces `platform*` es/en del front; `docs/features/plataforma.md`; docs identidad/organizaciones/guías/reglas realmente afectadas; punteros AGENTS/CLAUDE de carpetas nuevas |

Rutas definitivas que se implementan y prueban (no sustituir las ya fijadas por el maestro):

- `/api/platform/tenants/paged`, `/filter-counts`, `/{id}`; POST raíz (crear con invitación); POST `/{id}/approve`, `/reject`, `/suspend`, `/reactivate`, `/close`, `/retry-provisioning`; GET `/{id}/features`, PUT `/{id}/features/{featureKey}`.
- GET `/api/platform/tenants/{id}/public-site`; POST `/unpublish` y `/allow-publish` con motivo y version. GET `/api/platform/tenants/{id}/domains`; POST `/{domain}/verify`; DELETE `/{domain}` con motivo y version.
- `/api/platform/accounts/paged`, `/filter-counts`, `/{id}`; POST `/{id}/suspend`, `/reactivate`, `/revoke-sessions`, `/deletion`; POST `/api/platform/operators/invitations` (correo y permisos individuales), PUT/DELETE `/api/platform/accounts/{id}/operator` (version/motivo y permisos, sin roles).
- POST `/api/account-recovery/code`, `/verify` y raíz `/api/account-recovery` (idempotente, respuesta pública indistinguible); GET `/api/platform/recoveries` paginado; POST `/{id}/approve` y `/reject`.
- GET `/api/platform/audit` por cursor y `/{id}`; GET/PUT `/api/platform/settings`; GET/POST `/api/platform/legal-documents` y GET `/{id}`; publicación inmutable e idempotente.
- Front: `/plataforma`, `/plataforma/organizaciones/:id`, `/plataforma/cuentas`, `/plataforma/cuentas/:id`, `/plataforma/recuperaciones`, `/plataforma/legales`, `/plataforma/auditoria`, `/plataforma/configuracion`, `/recuperar`.

## Seguimiento y tareas verificables

### T00 · Línea de base y plan

- [x] Confirmar clean/main/últimos commits en ambos repos, leer maestros y aprobaciones. Aspire no está corriendo; Docker responde fuera del sandbox.
- [x] Revisión independiente del documento: Approved, sin bloqueos. Commit inicial exclusivo del plan antes de programar.

### T01 · Roles globales puros (primera tarea)

Registro histórico: esta implementación Owner/Support se reemplaza por permisos individuales en R00, por decisión del usuario del 03/10. No usarla como modelo para código nuevo.

Archivos: `Domain/Platform/{PlatformRole,PlatformRoleAssignment,PlatformRolePermissions}.cs`, AGENTS/CLAUDE; `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Platform/PlatformRoleTests.cs`; `docs/features/plataforma.md` inicial.

- [x] Rojo: tipos ausentes (CS0234) tras restaurar NuGet fuera del sandbox; Owner/Support, inmutabilidad y asignación global cubiertos por cinco pruebas.
- [x] Enum Owner/Support, catálogo inmutable y asignación global IVersioned con fábrica y cambio de rol. Sin permisos nuevos ni datos de muestra.
- [x] Verde focal: 5/5; build Domain: 0 advertencias/errores. Ficha inicial documenta roles y límites. Commit `feat(plataforma): definir roles globales`.

### T02 · Recorrido E5 real inicialmente rojo

Archivos: front `scripts/test-e2e-real.test.mjs` (runner y recorridos actuales en un único archivo); back `tools/ArquitecturaBaseMultitenant.RealE2ESetup/{Program,PlatformCommand}.cs`.

- [x] Reutilizar operador del seed aislado y códigos por pickup; sin interceptaciones. Support se agrega al preparador cuando T03 habilita su persistencia.
- [x] Recorrido `/plataforma` sin sesión → código real → listado y APIs de configuración/organizaciones/cuentas. Rojo confirmado esperando el encabezado Organizaciones (pantalla E5 ausente), 2 tests fallidos, duración 91,56 s. Una ejecución cruzada con generación de migración se descartó.
- [x] Evidencia registrada. El verde final es T25; E5 sigue abierta. Front `696c2b3`; Node check y lint focal sin errores. Lint general retorna 0 con advertencias previas de componentes y lienzo-v35.

### T03 · Persistencia y autorización de operadores

Registro histórico: la persistencia de roles y su seed se migran en R00; conservar los permisos efectivos, sin conservar Owner/Support en el modelo nuevo.

Archivos: PlatformRoleAssignmentConfiguration/Repository/Reader, ApplicationDbContext, PlatformSeeder, PlatformPermissionService y puertos; migración nueva; tests `PlatformPermissionServiceTests`, `PlatformOperatorPersistenceTests` e inventarios existentes.

- [x] Rojo por reader ausente; permisos limitados y pérdida de autorización con cuenta inactiva. Regresión real de actualización desde operador legado sin Personal; el seed se repite sin duplicar asignaciones. La protección append-only de auditoría conserva su test existente.
- [x] `platform.PlatformRoleAssignments`: unicidad UserId/FK identidad/xmin. Migración convierte operadores existentes a Owner y otorga al runtime permisos explícitos sobre esta tabla; seed conserva roles existentes. IsPlatformOperator selecciona acceso, no concede permisos.
- [x] Unit 6/6; PostgreSQL roles 3/3, ProductionSeed 1/1, Migrations 4/4; clasificación/configuración existentes 4/4. Build Api/AppHost/preparador 0 advertencias. SQL revisado; modelo sin cambios pendientes. Commit `feat(plataforma): persistir permisos de operadores`.

### T04 · Ver los tableros vigentes del lienzo (v73 o posterior)

Archivos: solo lectura de `docs/design/lienzo` vigente y sus Criterio-*; la conversación de diseño es la única que escribe esos archivos.

- [x] Escritorios validados desde v73 (`423891e`) y teléfonos app validados; verificar la versión vigente al implementar, incluidos sus estados y diálogos.
- [ ] Archivar el informe de la propuesta descartada. Su generador lo retira la conversación de diseño; este chat no toca el lienzo.

### A00 · Alineación previa a las pantallas de Plataforma (instrucción del 03/10)

Fuente: `docs/reviews/2026-10-03-alineacion-lienzo.md`, ya copiado sin cambios del informe temporal en `cc2835b`. Las marcas ✔ del informe significan hallazgos verificados, no correcciones terminadas.

- [ ] Paquete 0 completo, responsabilidad de la conversación «Paquete 0 · Errores visibles», incluido registro de empresa. Este chat no toca sus archivos ni incluye cambios ajenos en commits.
- [ ] Paquete 1 (excepto 1.11, asignado a este chat) y Paquete 3: responsabilidad de otra conversación. Este chat no los implementa ni modifica sus archivos por esos hallazgos.
- [x] 0.7: error propio `Tenancy.Member.LastTenantAdmin` para quitar/eliminar roles del último Dueño y `Tenancy.Member.CannotDisableSelf` para el acceso propio; la baja de cuenta conserva `Legal.AccountDeletion.LastAdmin`. Rojo dirigido → Usuarios 3/3, baja de cuenta 6/6, recursos 6/6 y build Api 0 advertencias.
- [ ] Antes de T20–23, después de diseño validado y Paquete 0: actualizar frontend.md §4/Teléfono, responsive, formularios, pantallas-y-ui y AGENTS del front desde los ocho Criterio-* vigentes (Paquete 5). Sin pruebas de documentación nuevas.
- [ ] Paso 4c, después de las fichas: Paquete 4 pasos 2–4 (piezas compartidas de app, navegación declarativa/Más/E2E a390 y todas las hojas), **más Paquete 2 completo** (criterio en botones, menús, Page, FormField, diálogos y avisos) y **punto 1.11** (migas y panel de Administración indican la ruta actual; el panel arranca abierto en rutas de administración). Resolver juntos los archivos de `shared/ui` y `layouts` antes de T20–23, sin tocar Paquetes 0, 1 (salvo 1.11) y 3 de las otras conversaciones.
- [ ] Capturas solo de pantallas nuevas y estados principales contra el lienzo vigente, enviadas al usuario antes del cierre para su revisión; no se autoaprueban. E2E real en verde y puerta general completa.

### A01 · Recuperar main en verde antes de seguir (instrucción del 03/10)

- [x] Corregir los 12 rojos informados: fichas Features/configuración EF/correo tipado/inventario/avisos/límite de Identity y regresiones Me, LegalAcceptance, InvitationAcceptance, UserTenantAccessIndex y UserTenantAccessReader. El cambio de notificationEmail del registro de empresa pertenece al otro chat (`5335ac7`) y se preserva.
- [x] Build completo: 0 advertencias/errores; `dotnet test` completo: **1452/1452**, 0 omitidos, **6m 23s 287ms**, antes del siguiente commit o tarea E5. Evidencia local: `.artifacts/etapa-5/main-verde-build-2.log` y `main-verde-test-2.log`. Primera ejecución: 1446/1452; los seis fallos restantes se corrigieron y verificaron focalmente antes de repetir.

### R00–R08 · Decisión de operadores y revisión prioritaria (03/10)

Antes de continuar T09–T16 o programar pantallas, completar estas correcciones. Cada punto tiene TDD donde hay lógica, focal verde, suite completa sin rojos antes de commit y rutas explícitas. T09 ya estaba commiteada al recibirse la revisión; T10 queda sin commit y se adapta también a R04. No tocar Paquetes 0, 1 (salvo 1.11), 3 ni el lienzo.

- [x] R00 · Modelo: nueve permisos individuales, sin roles Owner/Support. `PlatformPermissionAssignment` (una por cuenta, `text[]`, xmin) exige al menos un permiso, solo del catálogo y sin repetir. Seed inicial con el catálogo completo; la migración `PlatformOperatorPermissions` renombra la tabla y conserva los permisos efectivos (Owner → 9, Support → 4), con un Down con pérdida documentada. Rojo por tipos ausentes (CS0103) en worktree aislado → dominio 8/8, servicio 6/6, Application 433/433, arquitectura 154/154 y PostgreSQL 32/32 (migración en las dos direcciones, seed, alcance, configuración, organizaciones, alta, moderación, módulos, dominios y avisos). Las siete copias de `GrantAsync` con roles pasan a `Support/PlatformOperators`. Ficha, `backend.md`, `arbol.md` y AGENTS actualizados; el maestro y `permisos.md` ya lo decían.
- [ ] R00 · Conducta: invitación por correo sin Personal/Business, rechazo de toda cuenta previa con «Un operador no puede tener perfiles de persona» y último operador activo con operators.manage protegido bajo lock global al quitar, suspender o iniciar baja. Se entregan con T12, T13 y T16.
- [x] R01 · Avisos: solo rechazo y moderación. Dueños activos desde índice e identidad Active; principal verificado permitido o métodos del resolver de Organización. Nunca correo administrado por otra organización ni registrante retirado; ningún motivo interno en el correo. Test real de outbox/destinos y ausencia de avisos en otros estados.
- [x] R02 · Separar contadores en tabla global 1:1: los triggers no actualizan platform.Tenants ni su xmin. Migrar datos conservando conteos. Test real: sumar miembro entre GET y Suspender conserva version y la acción no da 409; cambios de estado simultáneos siguen dando 409. Commit `0250037`.
- [x] R03 · Definir ahora las lecturas de ficha: Dueños por índice de accesos e identidad; Historial por auditoría filtrada por organización; último cambio de estado con fecha y motivo. Sin consultar Members/Invitations privadas. La implementación, el contrato y la conexión permanecen pendientes en T21.
- [x] R04 · SecurityEvent conserva resumen de claves y antes/después sin datos personales, destino organización/cuenta e IP enmascarada. Eventos existentes de T05/T08/T09 ajustados, migración y pruebas verificadas. Commit `7e56aba`. La lectura de Ingresos desde LoginAudits permanece pendiente en T17.
- [x] R05 · Retry recibe solo version; motivo fijo de resources, sin pedir texto al usuario. Test HTTP del cuerpo sin reason y evento con motivo traducido. Back `ad8c74a`, contrato front `70a6bdc`.
- [ ] R06 · E2E operador: aceptación legal con texto real o en preparador; solo pasos de pantalla para Gestión/Administración/filas/acción con motivo/F5. Segundo operador con permisos reducidos y límites. Rojo por pantalla ausente; verde en T21–T23. No GET de API como reemplazo del recorrido.
- Avance R06: preparador legal back `0a36f5f`; recorrido front incluido por la conversación paralela en `a0f094c`. Rojo real en el listado Organizaciones ausente (`e5-r06-ui-red-4.log`); Aspire apagado. Falta el segundo operador con permisos reducidos después de R00; no marcar R06 completo todavía.
- [x] R07 · Fuente visual T04 es exclusivamente el lienzo vigente; informe de propuesta archivado en front `fe4cfbf`. El generador pertenece a diseño, sin tocarlo aquí.
- [x] R08 · Registrar en backlog el comentario desactualizado «El backoffice comienza en E9» en navigation/platform.ts.

### ADR 0039 · Tope de dependencias de negocio

- [x] Control rojo→verde del test existente: ILogger<T>, TimeProvider, IRequestValidator e IUnitOfWork no cuentan; ocho dependencias de negocio cumplen y nueve se pasan. Todo lo demás cuenta, incluido IOptions<T>. Focal 2/2.
- [x] Alinear capas-y-flujo.md, guardado.md y backend.md §4. Commit aparte `0f4e5a3` con suite completa 1485/1485 y build sin advertencias. No modificar ahora la lista blanca transaccional ni reacomodar Plataforma; consolidación previa a E7.

### T05 · Configuración global

Archivos: PlatformSettings/Configuration/Repository/Reader, modelos/validadores/PlatformSettingsService, controller/contratos; `PlatformSettingsServiceTests`, `PlatformSettingsTests`.

- [x] Rojo por contrato ausente en dominio/servicio; inválidos no mutan; stale version 409; Support 403; cambios dejan motivo y SecurityEvent mismo commit.
- [x] IVersioned/xmin; GET/PUT con exactamente los tres campos del tablero; enums vigentes, gracia fija. Recursos es/en y OpenAPI generado.
- [x] Dominio 12/12, servicio 4/4, HTTP PostgreSQL 3/3, recursos 6/6, accesos 2/2 y contratos de entrada 13/13. Build Api 0 advertencias; SQL solo registra xmin (columna nativa de PostgreSQL); sin cambios de modelo pendientes. Commit `feat(plataforma): administrar configuración global`.

### T06 · Listados globales de organizaciones y estado

Archivos: IPlatformReader/PlatformReader, Tenant (IVersioned si edición), modelos/validadores/servicio/controller TenantAdministration, SecurityEvent; tests TenantAdministrationRead/State y Tenancy/PlatformAccess.

- [x] Rojo: metadatos/eventos ausentes y rutas HTTP 404 donde debe haber 401. Dominio 3/3 nuevo y 7/7 regresiones. PostgreSQL 5/5: Business, paginado/búsqueda/conteos, contacto del Dueño, transiciones, 404/409, cierre y B2C operativo al suspender Business.
- [x] Metadatos globales y contacto desde identidad/índice explícito, sin leer filas privadas. Totales mantenidos por deltas bajo RLS; registro histórico recuperado cuando existe. Eventos y outbox en mismo commit; revocación sólo tenant e invalidación TenantStatusCache después.
- [x] 401/403 por ruta, 409 sin evento/cambio; OpenAPI e inventario actualizado. Recursos 6/6, lectores de identidad 3/3, transacciones/dependencias/accesos/contratos 25/25. Build Api 0 advertencias; SQL revisado y sin cambios EF pendientes. Commit `feat(plataforma): administrar organizaciones`.

### T07 · Aprobar, crear con invitación y reprovisionar

Archivos: TenantAdministrationService/helpers, Auth/TenantProvisioner/TenantSpaceProvisioner/TenantProvisioningReplayVerifier, Invitations/InvitationIssuer, contratos y tests de provisioning/invitación.

Provisioner `d27d7d3`: entrada explícita `StageBusinessOwnerAsync` para Member Invited/Active de Business, conserva `StageAsync` de E3/E6. Rojo por método ausente → PostgreSQL 2/2; unitarias 3/3; regresiones alta E6 2/2 e invitaciones 10/10. Acceso privado auditado `b375f04`.

- [x] Rojo HTTP: 4/4 rutas ausentes; verde final 8/8. Crear no agrega identidad/Personal; aceptar por HTTP concede Dueño; replay 201 no repite invitación; aprobación/retry conservan roles/settings y 409 con versión vieja. Slug automático: cinco pruebas (acentos, tomado, reservado, nombre no ASCII y corto).
- [x] Reutilizar provisioner/InvitationIssuer; permiso/evento confirmado antes de Enter, único writer después. El emisor relee Tenant Added por el repositorio transaccional y recibe preferencias preparadas para el correo, sin guardar temprano ni crear Personal.
- [x] Rojo→verde del alta incompleta; retry recupera el miembro de la identidad registrante activa, roles/settings faltantes y no inventa una primera empresa sin datos. Sin responsable recuperable da 409 y conserva Provisioning. Dos retries simultáneos: uno 204 y otro 409, un solo evento/roles/settings. Contadores diferidos al commit evitan conflicto propio de xmin. Acciones que encolan avisos son idempotentes. Regresiones aceptación 21/21, invitaciones 10/10, estado/listado 5/5; recursos 6/6, inventarios existentes 32/32. Build Api 0 advertencias, SQL revisado y sin cambios EF pendientes. Commit `feat(plataforma): provisionar organizaciones e invitar dueños`.

### T08 · Módulos y FeatureGate

Archivos: Domain/Features, Configuration/Features, puerto IFeatureService; Infrastructure/Features; TenantFeatures configuración/reader/repo/migración; MeResponse/ProfileSnapshotBuilder; tests FeatureGateTests y ModuleControllersTests requeridos, contratos y feature tests front.

Avance de dominio: rojo por tipos ausentes → 4/4 verdes (límite exacto de vencimiento, apagado manual, inválidos sin mutación y accesos separados), commit 152b3a3. Catálogo productivo vacío; recursos es/en 6/6 y códigos 6/6; build Domain 0 advertencias.

Avance backend: FeatureGate rojo por tipos ausentes y administración rojo por ruta ausente → recorridos reales verdes en PostgreSQL aislado. Registro scoped, filas globales versionadas, cache de datos crudos, límite exacto con TimeProvider y middleware antes de permisos. Administración con motivo/evento, Support de lectura, Personal 404, carrera inicial 204/409. Perfil 13/13; auditoría 9/9; contrato que lee ambos repos 3/3; inventario de rutas 3/3; ModuleControllersTests 1/1; API 0 advertencias. SQL revisado y modelo sin cambios pendientes. Consumidores front, nuevos tipos de Plataforma y E2E visual siguen pendientes por el orden acordado.

- [x] Rojo: apagado 404 ProblemDetails, prendido 200; emergencia global gana; Trial vence en límite exacto; caché invalida; alcance Consumer/Business no se mezcla.
- [x] Agregar paquete central y registro Microsoft.FeatureManagement.AspNetCore, filtro Tenant/caché t:{id}:features, DisabledFeatureHandler; persistir origen/vencimiento/version, motivo y SecurityEvent al cambiar desde Plataforma.
- [x] Catálogo productivo inicialmente vacío: núcleo siempre disponible, sin módulos de ejemplo ni WhatsApp. Gates de recorrido viven en TestFeatures; el framework soporta catálogo de nuevos módulos reales siguiendo guía.
- [x] Back: `features` de `/api/me` siempre presente, lista vacía cuando corresponde. La conducta ya estaba en `2e5a06b` (FeatureGateTests: Business vacío, habilitado y vencido, Consumer solo lo suyo; OpenApiContractTests lee ambos repos). Faltaba evidencia de Plataforma y de un espacio no disponible: `ProfileServiceTests` suma organización suspendida o cerrada, miembro inactivo y acceso Platform, todos con `[]` y sin consultar `IFeatureService`. Rojo de control en un worktree aislado, quitando la guarda `mayReadSettings`: 4 de 18 fallidos; verde **18/18**. Commit `test(modulos): /api/me sin módulos en Plataforma ni en espacios no disponibles`.
- [ ] Front, en T19–T20 o el bloque 5 de la consolidación (manifiesto de rutas): useFeature/Feature/FeatureRoute y `feature` en rutas/navegación.

### T09 · Entrada privada y moderación de página

Archivos: PlatformActionGuard y servicio de registro de acceso; TenantScope; PublicPage y writer de moderación; PlatformPublicSiteController; SecurityEvent/recursos; tests PlatformScope/PublicPageModeration.

Avance del acceso privado (prerrequisito de T07): prueba roja por tipos ausentes → 3/3 unitarias y 2/2 con PostgreSQL. Comprueba permisos persistidos, evento visible desde otra conexión antes de Enter, ausencia de transacción abierta y restauración del alcance ante excepción. Build Api 0 advertencias e inventarios existentes 12/12. Moderación sigue pendiente.

- [x] Rojo HTTP por ruta ausente (404 frente a 401 esperado); permiso/motivo preceden evento confirmado y Enter sin transacción. Scope 3/3 unitarias y 2/2 PostgreSQL conservan restauración. Según el catálogo, metadatos privados/moderación exigen tenants.manage: Support 403; su listado global permanece disponible.
- [x] GET proyecta solo metadatos; Unpublish vuelve Draft con bloqueo/actor/motivo/fecha; AllowPublish retira bloqueo sin publicar. La organización no publica bloqueada. Avisos a Dueños, versión y replay verificados; el borrador queda oculto fuera del alcance autorizado y otra organización permanece intacta.
- [x] Moderación real 1/1; PublicPage dominio 4/4; regresión E6 1/1; inventario 3/3 y OpenAPI 3/3. Build completo 0 advertencias/errores; suite completa **1454/1454**, 0 omitidos, **6m 36s 429ms** (`.artifacts/etapa-5/t09-full.log`). Commit `feat(plataforma): moderar páginas públicas`.

### T10 · Dominio verificado desde Plataforma

Archivos: PlatformDomainController/contracts; PlatformTenantDomainService/adaptador del writer OrganizationDomainService; tests PlatformDomainAdministration y regresiones métodos administrados.

- [x] Rojo en un worktree limpio de `2f41ab7` con solo los tests: `GET .../domains` 404 en vez de 401 y el inventario sin las tres rutas (2 de 4 fallidos). El recorrido cubre TXT por el puerto, solo el dominio elegido (otro pendiente queda intacto), unión de métodos existentes y nuevos, quitar conserva `ManagedByTenantId` y bloquea al exmiembro, y motivo, versión (ausente 400, vieja 409), permiso (Consumer, Business y Support 403), alcance (otra organización, id inexistente y Personal 404, sin evento de entrada a Personal) y reenvío idempotente de verificar (204 `Idempotent-Replayed`, un solo evento).
- [x] Reutiliza la lógica de E6 sin writer intermedio: `PlatformDomainService` y `OrganizationDomainService` abren cada uno su transacción; `OrganizationDomainVerifier.PrepareAsync` consulta el DNS afuera y `StageAsync` (verificador y removedor) trabaja adentro, sin `IUnitOfWork`. El evento de entrada se confirma antes de `Enter`; no se llama un servicio público dentro de la UoW ni se superponen alcances. El `PlatformDomainWriter` de Codex, que solo reenviaba, se borró, y el verificador y el removedor salieron de la lista blanca de `TransactionBoundaryTests`.
- [x] Verde: PlatformDomainAdministration + OrganizationDomains (E6) + inventario + OpenAPI **10/10**; TransactionBoundary + ServiceDependencyCount **10/10**; build Api 0 advertencias. OpenAPI regenerado solo agrega las tres rutas, con el 503 del DNS en verificar. Revisión adversarial (2 lentes): sin bloqueantes; sus tres hallazgos menores (Personal 404, 503 sin declarar, reenvío) se corrigieron. Commit `feat(plataforma): administrar dominios verificados`.

### T11 · Consulta global de cuentas

Archivos: IPlatformReader/PlatformReader (identidad global explícita), Account models/filter counts/controller; tests PlatformAccountReader y NeedsRecovery.

- [x] Rojo en worktree aislado por miembros ausentes (CS1501 del evento de fin de membresía con la cuenta, CS0103 `PersonalDataMask`, CS1061 `PendingTenantOwners`). Base aislada con nueve cuentas: los cinco estados (NeedsRecovery solo con cuenta Active sin método útil; el correo administrado deja de servir al terminar la membresía; una suspendida sin métodos queda Suspended), tipo Persona/Operador, búsqueda sin acentos por nombre y por un correo secundario, comodín escapado, página fuera de rango y conteos que conservan el otro filtro.
- [x] Ficha con métodos enmascarados con el formato del tablero, organizaciones del índice global con la fecha de fin de membresía (el evento ahora guarda la cuenta), baja pedida, único Dueño, permisos de operador, alta y «Cuenta eliminada» traducida; 404 para un id inexistente. `PlatformAccountReader` lee identidad, métodos, accesos, ingresos y eventos sin Members ni Invitations; entra en la lista de `IdentityStorageAccessTests`. El NeedsRecovery en SQL usa `LoginMethodAvailability.SignInTypes`, la misma regla del ingreso. La búsqueda no se registra (`PlatformAccountsQuery.ToString`).
- [x] Contacto del Dueño invitado: `platform.PendingTenantOwners` se agrega en la UoW del alta y la lista y la búsqueda de organizaciones lo usan sin Dueño activo ni registrante. Un trigger sobre `tenant.Invitations` borra la copia al aceptar, revocar o eliminar la invitación (también cuando la baja de una cuenta la revoca); runtime solo agrega y lee.
- [x] Verde: cuentas 3/3 (401, Consumer 403, operador sin `accounts.read` 403), alta y aceptación con contacto y limpieza, revocación sin copia; integración afectada **77/77**, Application **440/440**, dominio **264/264**, arquitectura **154/154**; build 0 advertencias. OpenAPI: solo agrega las tres rutas y sus esquemas (comparación semántica contra `HEAD`). Revisión adversarial (2 lentes), sin bloqueantes; se corrigió la búsqueda por métodos (ahora `ApplySearch`, con un secundario acentuado buscado sin acento), se agregó una cuenta con solo un método administrado vigente, los índices trigram de `LoginMethods.Value` y `PendingTenantOwners.Address` y el (DisplayName, Id) del orden, y `multitenancy.md` §3.1. La lista de único Dueño de la ficha sigue la regla de Mi cuenta hasta T13. Verde después de los arreglos: cuentas, alta, organizaciones, migraciones y métodos **32/32**, arquitectura **154/154**. Commit `feat(plataforma): consultar cuentas globales`.

### T12 · Suspender, reactivar y cerrar sesiones de cuenta

Archivos: PlatformAccountService/writer, IUserRepository/UserRepository/ApplicationUser, ITokenRevoker/ISignInService, cache/events/notices; tests PlatformAccountState y integración de tokens.

- [x] Rojo en worktree aislado por tipos ausentes (CS0117: `AccountSuspended`, `AccountReactivated`, `AccountSessionsRevoked`, `LastManagerCode`). Cubre: 401 sin sesión, 403 con solo `accounts.read`, 400 sin motivo o versión, 404 para un id inexistente; suspender revoca el token de la persona (401 en `/api/me`) y repetirla da 409 `Users.User.InvalidTransition`; reactivar con versión vieja da 409 y con la vigente vuelve a Active y deja ingresar con un código nuevo; cerrar sesiones revoca sin cambiar el estado, cambia la versión y su reenvío es idempotente; una cuenta suspendida con baja programada vuelve a PendingDeletion; el último operador con `operators.manage` no se suspende (409 `Platform.Operator.LastManager`) hasta que hay otro, y un operador no se suspende a sí mismo (403 `Users.User.CannotModifySelf`).
- [x] Motivo, versión y lock por cuenta; `PlatformOperatorGuard` toma antes el lock global de operadores. SecurityEvent con operador, cuenta, motivo, diff de `status` e IP enmascarada en la misma transacción; caché de estado después del commit. No hay avisos previstos (el tablero no los muestra). `PlatformTenantAudit` pasa a `PlatformAudit` porque registra organizaciones y cuentas; `PlatformAccountStateService` tiene 7 dependencias de negocio y abre su transacción.
- [x] Verde: estado de cuentas **3/3**; cuentas, organizaciones, moderación, dominios, inventario y OpenAPI **20/20**; Application **441/441**; arquitectura **154/154**; build 0 advertencias. OpenAPI solo agrega las tres rutas y el contrato del pedido. Commit `feat(plataforma): administrar estado y sesiones de cuentas`.

### T13 · Baja por Plataforma (ADR 0035)

Archivos: ApplicationUser/UserRepository, PlatformAccountService, AccountDeletionPolicy/Requester/Processor, models/notice resources; tests PlatformDeletion y AccountDeletionWorker.

- [ ] Rojo: cuenta activa→PendingDeletion; suspendida conserva Suspended y fecha; DeletionRequestedByOperatorId queda guardado; no reauth usuario; único Dueño bloquea; no saltar gracia ni cancelar por operador.
- [ ] Reutilizar planner/participantes/notificador existentes; actor PlatformOperator en evento; reactivar antes de fecha→PendingDeletion y persona puede cancelar; métodos siguen reservados durante gracia.
- [ ] Verde PostgreSQL/autenticación/regresiones 3b/E4. Commit `feat(plataforma): iniciar bajas con gracia`.

### T14 · Recuperación pública y modelo persistido

Archivos: AccountRecoveryRequest/Status/errors/config/migración/reader/repo; Recovery service/validators/options; AccountRecoveryController; tests RecoveryPublic/RecoveryPrivacy/OTP.

- [ ] Rojo: respuesta pública idéntica exista o no cuenta; solo nuevo método verificado permite pedido, OTP ligado a finalidad/destino/nonce, vence y consume; método ya ocupado no filtra existencia; rate limits y anti replay.
- [ ] Reutilizar generador/verificador/canal OTP de E3 con purpose recovery; token/ticket solo cuerpo/memoria, hashes en base, ningún enlace API con token. Guardar pedido Pending y encolar lo pertinente; no entregar sesión.
- [ ] Pruebas JSON/errores/antiforgery y aislamiento base; recursos/contratos. Commit `feat(recuperacion): recibir solicitudes verificadas`.

### T15 · Revisar recuperación y limpieza de baja

Archivos: PlatformRecoveryService/Controller, Recovery writer, AccountRecoveryReader, Infrastructure/Identity/Deletion/RecoveryDeletionParticipant; tests RecoveryDecision/RecoveryDeletion.

- [ ] Rojo: operador con recoveries.manage puede revisar; aprobar agrega método propio verificado y quita NeedsRecovery sin duplicar ni emitir sesión; rechazar conserva métodos; motivo/version y repetición protegida; pedidos de cuenta Deleted no restauran identidad.
- [ ] Locks por pedido/cuenta/método, validación de unicidad con índices; SecurityEvent/aviso. Participante de eliminación cierra/purga datos personales de pedidos pendientes conforme ADR0035.
- [ ] Verde integración y pruebas de privacidad. Commit `feat(recuperacion): revisar pedidos y cerrar los de cuentas eliminadas`.

### T16 · Gestión de operadores

Archivos: PlatformOperatorService/guard/repo/controller cuentas e invitaciones; tests PlatformOperators/LastOperatorAdministrator.

- [ ] Rojo: invitar por correo con selección individual de las nueve claves; rechazar cuenta previa con el error indicado. Aceptar crea identidad exclusivamente Platform, sin Personal/Business. Permisos inválidos o repetidos rechazados, invitación/replay de un solo uso.
- [ ] Cuentas incluye operadores; editar/quitar permisos versionado, sin roles. IsPlatformOperator y permisos se confirman juntos, con SecurityEvent y revocación de autorizaciones. No quitar/suspender/dar de baja al último operador activo con operators.manage; lock global serializa esa regla.
- [ ] Verde permisos/cookies/seed/migración. Commit `feat(plataforma): administrar operadores`.

### T17 · Auditoría de seguridad

Archivos: ISecurityEventReader/SecurityEventReader, PlatformAuditService/controllers/query; traducciones Audit/recursos; tests PlatformSecurityAudit.

- [ ] Rojo: cursor estable fecha/Id, filtro actor/acción/objetivo; detalle con fecha y campos lectura, acciones traducidas, actores eliminados/objetivo conservado; payload sin correo/teléfono/token completos.
- [ ] SecurityEvent conserva claves con antes/después sin datos personales, destino tenant/account e IP enmascarada. T05/T08 y eventos administrativos usan ese contrato; el tipo Ingresos lee identity.LoginAudits y conserva método/resultado/código sin dirección ni código OTP.
- [ ] Join a nombres actuales o resumen histórico no personal según datos existentes; evento inmutable, no total de tabla creciente. GET listado/detalle separados; el historial de organización filtra el destino en SQL.
- [ ] Verde PostgreSQL/permissions/build. Commit `feat(plataforma): consultar auditoría de seguridad`.

### T18 · Versiones legales inmutables

Archivos: PlatformLegalService/writer, LegalDocument/Content/repo/reader, contracts/controller/validators; tests PlatformLegalPublication y LegalAcceptance.

- [ ] Rojo: todas las culturas soportadas obligatorias; faltante 400; fecha UTC válida; número de versión serializado por tipo; POST idempotente sin duplicar; nueva versión vigente obliga aceptación, futura no bloquea aún.
- [ ] Publicar sin UPDATE/DELETE del documento; contenido/motivo/resources sin logs; permiso legal.manage + SecurityEvent. Mostrar versiones y lectura, no inventar edición de históricas.
- [ ] Verde integración de aceptación simultánea/regresiones 3b. Commit `feat(plataforma): publicar versiones legales`.

### T19 · Contratos y recuperación pública validada

Archivos: OpenAPI/generated/types, `src/areas/public/auth/{api,pages}/RecoverAccount*`, link en LoginPage, locales; tests de pantalla + `src/shared/api/platformContract.test.ts`.

- [ ] Contrato lee controller/request/OpenAPI y consumidor/types de ambos repos para TODAS las operaciones E5. Regenerar, no editar generated.
- [ ] Copiar Recuperar/M-Recuperar validado: método anterior, nuevo correo/código, solicitud/success/error, sin revelar identidad; errores por code y useIdempotentMutation.
- [ ] Test foco rojo→verde, frontend lint/build y contracts:check. Commit `feat(recuperacion): conectar la pantalla pública`.

### T20 · Marco de Plataforma aprobado y gate de módulos

Archivos: navigation/types/platform, Sidebar/AdminPanel/PrivateLayoutFrame, routes y auth gates, locales/test.

- [ ] Desktop tiene Gestión arriba y Administración al pie, único segundo panel, actual marcado, permisos consistentes ruta/enlace; Cuentas contiene operadores y WhatsApp no aparece.
- [ ] Mobile solo después de T04 aprobado: formato app elegido, Más y hoja de navegación; F5/Salir/Mi cuenta conservan acceso. Módulo apagado es NotFound, enlace oculto y permiso no sustituye módulo.
- [ ] Tests navegación/route/feature/axe, lint/build. Commit `feat(plataforma): conectar navegación y permisos`.

### T21 · Organizaciones: listado y ficha real

Archivos: `src/areas/platform/tenants/{api,pages,components}/`, locales y tests.

- [ ] Copiar los tableros vigentes de Organizaciones/Organizacion y sus app móviles: filtros/conteos/1 y 50 filas, ficha y diálogos aprobar/rechazar/crear/suspender/reactivar/cerrar/retry, Modules/PublicSite/Domain.
- [ ] Conectar las lecturas globales de R03: Dueños desde índice e identidad, Historial desde auditoría filtrada por organización y fecha/motivo del último estado. No leer Members/Invitations fuera de RLS. Retry no agrega motivo escrito.
- [ ] Backend de esta tarea: ampliar la lectura de `GET /api/platform/tenants/{id}` a un detalle propio, conservando el contrato paginado. `owners` devuelve id, nombre, principal Email y estados de identidad/membresía de las asignaciones TenantAdmin vigentes del índice global, sin usar el registrante histórico como Dueño actual. `lastStateChange` devuelve fecha UTC, acción y motivo interno del último cambio efectivo de Status desde SecurityEvents; sin evidencia histórica queda null. La versión proviene únicamente de Tenants.
- [ ] `GET /api/platform/tenants/{id}/history` usa cursor fecha/Id y filtra TargetTenantId en SQL sobre SecurityEvents; proyección con acción traducida, actor, motivo y resumen no personal de R04. Permiso tenants.read, 401/403 y 404 para Personal/id inexistente; no abre ITenantScope ni lee AuditEntries privadas. Agregar la ruta al inventario y el contrato generado.
- [ ] TDD real de estas lecturas: dos Dueños y registrante retirado, identidad eliminada/sin nombre, historial de otra organización excluido, último estado no reemplazado por cambios de módulos o entrada técnica, y null sin evidencia. Contrato que lee ambos repos; conectar sus campos reales a los estados del tablero vigente en esta tarea.
- [ ] Conectar API real y versiones; no duplicar handlers de E6 ni inventar ediciones ajenas; 409 conserva selección/draft, motivo obligatorio y acción irreversible roja.
- [ ] Pantalla/red/errores/permisos, lint/build. Commit(s) coherentes `feat(plataforma): conectar organizaciones`.

### T22 · Cuentas y operadores: listado y ficha real

Archivos: `src/areas/platform/accounts/{api,pages,components}/`, locales/test.

- [ ] Copiar Cuentas/Cuenta-Plat y móviles aprobados: estados derivados, métodos/organizaciones leídos, suspender/reactivar/cerrar sesiones/baja e invitar operador exclusivo con permisos individuales; bloquear el retiro del último administrador de operadores.
- [ ] Motivo/version obligatorios en los diálogos, fechas mediante format, errores/409 conservan datos. No editar identidad desde Plataforma si el tablero no lo pide.
- [ ] 1/50 filas, tests/lint/build. Commit(s) `feat(plataforma): conectar cuentas y operadores`.

### T23 · Recuperaciones, legales, auditoría y configuración reales

Archivos: `src/areas/platform/{recoveries,legal,audit,settings}/{api,pages,components}/`, locales/test.

- [ ] Copiar todos los estados aprobados, decisión de recuperación con motivo, lectura/publicación legal/culturas, cursor y modal Audit aprobado (fecha al cuerpo; mobile hoja), settings/version/draft/409.
- [ ] Ninguna pantalla por refinar se implementa antes de elección; todas las salidas reales vienen del contrato generado.
- [ ] Tests dirigidos/lint/build por unidad. Commits separados por feature.

### T24 · Documentación y capturas comparadas

Archivos: `docs/features/plataforma.md`, identidad/organizaciones/reglas/guías afectadas, AGENTS/CLAUDE nuevos, `docs/reviews/2026-10-03-etapa-5-plataforma.md`; front scripts/capturar-etapa-5.mjs y docs/design/capturas/etapa-5/.

- [ ] Completar ficha/rutas/permisos/roles/estados/razones/eventos/recuperación/baja; actualizar pasos módulo/permiso nuevo y punteros existentes de E5.
- [ ] Comparar cada estado nuevo o modificado real y lienzo a 1440×900 y 390×844; filtros y listas con 1 y 50 elementos, diálogos, errores y vacíos. Usar datos propios de base aislada, no Development.
- [ ] Corregir diferencias; registrar motivo y aceptación de toda desviación que cambie diseño. Inventarios de contratos/HTTP actualizados, sin nuevas guardas innecesarias.

### T25 · E2E completo y puerta de cierre

- [ ] E2E E5 real: operador entra/F5 y navega Gestión/Administración por pantalla; operador con permisos reducidos muestra sus límites; crear organización con invitado sin cuenta y aceptar sin Personal; aprobar pendiente/retry sin duplicados; suspender Business deja Personal; módulos gate; despublicación bloquea E6; dominio conserva vínculos al quitar; suspender/reactivar/revocar/baja cuenta; recuperación nuevo correo aprobada; legal nueva versión aceptada; audit/settings guardados.
- [ ] Mantener recorridos E3a/b/c, E4 y E6. Sin mocks/interceptación y PostgreSQL/pickup efímeros. Si una integración externa no puede correr localmente, anotarla pendiente sin inventar verde.
- [ ] `dotnet build ArquitecturaBaseMultitenant.slnx` cero advertencias; `dotnet test` completo con Docker.
- [ ] Front `npm test`, `npm run lint`, `npm run build`, `npm run contracts:check`, `npm run test:e2e:real` completos.
- [ ] Generador de referencias/tests scripts y arneses existentes según CI; HarnessTests/front harness y estructura; ModuleControllersTests/FeatureGateTests; no arnés adicional.
- [ ] Agregar solo E5 a ClosedStages en ambos repos cuando TODO lo requerido esté verde; conservar E6. Informe con commits, salidas, decisiones, diferencias visuales y pendientes reales. Aspire stop si se levantó; repos clean/main, sin push.

## Decisiones tomadas

1. Catálogo productivo de módulos vacío en E5: los servicios del núcleo no son módulos y WhatsApp queda excluido. Los gates se ejercitan con TestFeatures y catálogo inyectado de test, nunca datos productivos de ejemplo.
2. Decisión del usuario del 03/10 reemplaza Owner/Support: permisos individuales entre las nueve claves, identidad exclusiva de Plataforma e invitación por correo nuevo. Se rechaza una cuenta previa; el último operador activo con operators.manage no se puede quitar. R00 migra T01/T03 y preserva los permisos efectivos anteriores.
3. El guard de acceso privado confirma un SecurityEvent antes de Enter mediante un servicio transaccional específico; el writer privado corre después, con una sola UoW propia. No Enter dentro de una transacción, ni helper con UoW, ni transaction anidada.
4. Crear con invitación prepara roles/settings sin crear identidad: una entrada explícita del TenantProvisioner permite dueño pendiente, InvitationIssuer reserva Member(Invited) con TenantAdmin y la aceptación vigente vincula/crea cuenta sin Personal.
5. El refinamiento visual pendiente se revisa como grupo concreto; aprobaciones desktop/menu/modal previas se conservan. Inicios de Organización y WhatsApp no se rediseñan en E5.
6. Configuración exige motivo y versión; su evento se guarda en la misma transacción y la caché se invalida después. La gracia de baja de 30 días no es editable.
7. R02 mueve los conteos a una tabla global 1:1, mantenida con deltas atómicos desde escrituras privadas bajo RLS. Esos triggers no modifican Tenants ni su version. Plataforma lee solo metadatos y contacto global explícito; no entra a cada organización para armar un listado. La fecha histórica sin evidencia queda null.
8. Crear copia los cinco campos del tablero (nombre, primera empresa, correo del Dueño, cultura y zona). No agrega un campo de slug ni de motivo: genera la dirección desde el nombre y, ante reserva/colisión/nombre inválido, usa `org-{operationId}`; la descripción fija de la acción sale de resources. Los otros cambios administrativos exigen motivo escrito. Muestra móvil corregida en front `d4d0627`.
9. El retry incompleto solo reconstruye un miembro cuando existe registrante autoservicio activo; nunca convierte al operador creador en Dueño ni adivina el nombre de una empresa perdida. Sin responsable verificable conserva Provisioning y devuelve conflicto.
10. Los deltas de conteos conservan sus constraint triggers `DEFERRABLE INITIALLY DEFERRED`, dentro de la misma transacción. Desde R02 escriben solo la proyección técnica TenantCounters, por lo que tampoco invalidan la versión de una pantalla entre dos transacciones. La creación de la fila 1:1 es un trigger inmediato al alta del espacio; runtime tiene solo SELECT sobre la proyección. La migración copia los totales existentes antes de retirar las columnas viejas y el rollback los conserva.
11. El filtro de módulos conserva filas crudas en caché y evalúa la vigencia con TimeProvider por petición; el apagado global gana. El gate se evalúa antes de los permisos, después de autenticación/acceso/tenant, para devolver 404 cuando el módulo está apagado. Una configuración inicial usa versión 0; la carrera de inserción se traduce a 409. Cambiar módulos exige SecurityEvent del destino en la misma UoW.
12. Los tests que retroceden a una migración histórica preparan datos con SQL parametrizado de ese esquema, sin usar el modelo actual. La recarga de xmin que usaban las fixtures ante los contadores anteriores dejó de ser necesaria por ese motivo con R02; los cambios reales a Tenants siguen requiriendo lectura de su versión actual.
13. Moderación utiliza la página de E6 y su versión, sin copiar contenido. Permitir publicación retira el bloqueo y deja la decisión de publicar a la organización. RLS permite lo publicado y oculta el borrador fuera del alcance. La consulta privada exige tenants.manage, conforme al catálogo de E5, y audita su motivo traducido antes de Enter.
14. Las rutas de dominio conservan `{domain}` como nombre normalizado, conforme al plan maestro. E6 y Plataforma comparten la preparación DNS y la escritura Stage, que no abre otra transacción; cada servicio de entrada abre la suya y confirma sus propios eventos (ADR 0039), así que el verificador y el removedor de E6 dejan de recibir `IUnitOfWork` ya en T10, adelantando esa parte del bloque 3 de la consolidación. La versión vista se verifica antes del DNS y de nuevo mediante xmin al guardar. El retiro conserva los vínculos ManagedByTenantId aprobados. El DELETE recibe motivo y versión en el cuerpo, porque el motivo es texto libre y no va en la URL.
15. Los avisos administrativos solo cubren rechazo y moderación. Se seleccionan Dueños desde el índice global con identidad Active; el principal debe ser verificado, tener canal y no ser administrado por otra organización. Si no cumple, se reutiliza el resolver de métodos disponibles con el mismo filtro de organización. El contrato del aviso no acepta el motivo interno.
16. Decisión del usuario del 03/10, para T14: «Recuperar mi cuenta» no revela si el correo nuevo ya es de otra cuenta. La respuesta pública es la misma; a ese correo le llega un aviso de que ya tiene una cuenta en lugar de un código, así que el pedido no avanza. El texto del tablero «Ese correo ya lo usa otra cuenta.» lo ajusta la conversación de diseño.
17. Decisión del usuario del 03/10, para T16: un operador nunca entra como persona ni a una organización. Con su correo solo se entra a Plataforma: la conexión rechaza los accesos Consumer y Business de una cuenta operadora sin crearle Personal, e invitar a un operador a una organización se rechaza. Un Personal previo queda sin uso y no se borra; para usar el producto como persona o empresa se usa otro correo.

## Registro de resultados

| Tarea | Estado/evidencia | Commit back | Commit front |
|---|---|---|---|
| T00 | Línea de base limpia, Aspire parado, Docker disponible; revisión Approved | `fff99c9` | — |
| T01 | Rojo CS0234 → 5/5 verde, build Domain 0 advertencias | `f03c1b1` | — |
| T02 | E2E real rojo por pantalla E5 ausente; Aspire detenido | `4ce80b7` (registro) | `696c2b3` |
| T03 | Permisos 6/6; roles PostgreSQL 3/3; seed 1/1; migraciones 4/4; inventarios 4/4 | `4ce80b7` | — |
| T04 | Ver tableros vigentes del lienzo, v73 o posterior; diseño externo validado, propuesta descartada | seguimiento de esta tarea | `423891e` (diseño externo); `e670892` archivado |
| T05 | Dominio 12/12; servicio 4/4; HTTP 3/3; recursos 6/6; accesos/contratos 15/15; migración/build verificados | `6a49970` | — |
| T06 | Dominio nuevo 3/3 y regresiones 7/7; HTTP/RLS/tokens 5/5; recursos 6/6; inventarios 28/28; build/migración verificados | `f785e5b` | — |
| T07 | Scope 3/3 + PG 2/2; core 3/3 + PG 2/2; HTTP 8/8; slug 5/5; regresiones 36/36; recursos 6/6; inventarios 32/32; build/SQL verificados | `b375f04`, `d27d7d3`, `f05c9df`, `6fa09c4` | `d4d0627` (muestra) |
| T08 backend / A01 | Gates/edición real y regresiones; build completo 0 advertencias, suite completa 1452/1452 (6m23s); front de módulos pendiente | `2e5a06b` | — |
| T09 | Moderación/avisos/replay/versión/aislamiento HTTP; regresiones E6 y contratos; suite completa 1454/1454 (6m36s), build 0 advertencias | `cd3fedb` | — |
| R01 | Outbox real rojo (4/5 envíos en vez de 2) → focal 2/2; HTTP de estados/moderación y contrato de canales; build 0 advertencias, suite completa 1473/1473, 0 omitidas (6m22s564ms) | `02184ca` | — |
| ADR 0039 | Control rojo 2/2 → verde 2/2; dependencias técnicas excluidas, controles 0/8/9; tres fichas alineadas; build 0 advertencias y suite completa 1485/1485 | `0f4e5a3` | — |
| Helper autorizado | Solo lectura del correo propio en outbox con payloads inválidos ajenos; focal 5/5 y suite completa 1485/1485 | `d911d15` | — |
| R02 | Contadores globales 1:1, migración conservadora; HTTP 6/6, contadores 2/2 y suite completa 1485/1485 | `0250037` | — |
| R03 | Definición de detalle e historial con fuentes globales y tests previstos; implementación/contrato pendientes en T21 | este seguimiento | — |
| R04 | Diff técnico/destinos/IP; dominio 8/8, Application 4/4, HTTP 3+1+2 y trail 10/10; build 0 advertencias y suite completa 1485/1485 | `7e56aba` | — |
| R05 | HTTP rojo 400 → 8/8; contrato ambos repos 1/1; suite completa back 1486/1486 y front 751/751 | `ad8c74a` | `70a6bdc` |
| R06 (parcial) | Ingreso por .eml y /api/me 200; rojo por pantalla Organizaciones ausente, 94,326 s. Falta operador reducido y verde en T21–T23 | `0a36f5f` | `a0f094c` (incluido por otra conversación) |
| R07 | Propuesta archivada; fuente exclusiva lienzo vigente, sin modificarlo; suite front 751/751 | este seguimiento | `fe4cfbf` |
| R08 | Comentario E9 de navigation/platform.ts registrado en backlog; sin cambios ejecutables, misma suite 1473/1473 de R01 y revisión del diff | `4ad757c` | — |
| T10 | Rojo en worktree limpio (404 por 401, inventario) → dominios Plataforma + E6 + inventario + OpenAPI 10/10; arquitectura 10/10; build Api 0 advertencias | `4bb5c35` | — |
| T08 back | `/api/me.features` vacío en Plataforma y en espacios no disponibles: rojo de control 4/18 → 18/18; front pendiente | `93e2304` | — |
| R00 modelo | Permisos individuales y migración que conserva los efectivos; dominio 8/8, servicio 6/6, Application 433/433, arquitectura 154/154, PostgreSQL 32/32 | `f2b3e9a` | — |
| Alta: Empresa y Roles | Correo del Dueño invitado con la primera empresa y los roles (alineación 1.4, pedido de la Guía): rojo → 10/10 | `ddd1572` | — |
| T11 | Cuentas globales con estados derivados, ficha y contacto del Dueño invitado; integración 77/77, Application 440/440, dominio 264/264, arquitectura 154/154 | `8cb28e2` | — |
| T12 | Suspender, reactivar y cerrar sesiones con el último administrador de operadores protegido; 3/3 + 20/20, Application 441/441, arquitectura 154/154 | commit de esta tarea | — |

No declarar cierre a partir de esta tabla sin completar T25.
