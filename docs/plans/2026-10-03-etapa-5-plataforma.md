# Etapa 5: Plataforma — plan ejecutable

> **Para agentes:** aplicar `writing-plans` y ejecutar con `executing-plans` en este chat. El usuario autorizó `main`, sin worktree ni push. Revisar el plan con un revisor de documentos, no delegar programación. Las casillas registran evidencia real; una tarea termina con su commit explícito y verificado.

**Objetivo:** completar la administración global de organizaciones, cuentas, recuperaciones, operadores, módulos, documentos legales, auditoría y configuración, con API real y pantallas elegidas del lienzo.

**Arquitectura:** Domain ← Application ← Infrastructure; Api compone. Copiar Roles, E3 y Organización E6. Los datos de Plataforma son globales; los datos privados de una organización se consultan solo después de autorizar, registrar el SecurityEvent y entrar con ITenantScope, antes de abrir su transacción. Una escritura delegada tiene un único propietario transaccional, siguiendo el patrón existente PublicPageService/PublicPageEditor.

**Tecnologías:** .NET 10, EF Core/Npgsql, PostgreSQL 18, Aspire 13.5, Microsoft.FeatureManagement.AspNetCore, HybridCache, React/TypeScript, TanStack Query, i18next, Vitest y Playwright.

## Fuentes, restricciones y punto de partida

- Back: `C:/Users/ezequ/source/repos/ArquitecturaBaseMutitenant`, main limpio, `a12edb7`.
- Front: `C:/Users/ezequ/source/repos/ArquitecturaBaseMutitenantFront`, main limpio, `44bf951`.
- E4 y E6 cerradas. Conservar ClosedStages `{0,1,2,3,4,6}` hasta cerrar E5; el salto no reabre E6.
- Fuente visual: front `docs/design/lienzo/`, v71. El escritorio Gestión/Administración y el modal Auditoría están aprobados. Recuperar/M-Recuperar están validados. El resto de Plataforma figura por refinar: presentar propuesta conjunta y esperar elección para esas pantallas, avanzando mientras tanto con back y piezas independientes.
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
| Roles globales | `Domain/Platform/{PlatformRole,PlatformRoleAssignment,PlatformRolePermissions,PlatformErrors}.cs`; Owner/Support, asignación única por cuenta, permisos desde PlatformPermissions; último Owner en guard de servicio |
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
- `/api/platform/accounts/paged`, `/filter-counts`, `/{id}`; POST `/{id}/suspend`, `/reactivate`, `/revoke-sessions`, `/deletion`; PUT `/{id}/operator` y DELETE `/{id}/operator` (version/motivo).
- POST `/api/account-recovery/code`, `/verify` y raíz `/api/account-recovery` (idempotente, respuesta pública indistinguible); GET `/api/platform/recoveries` paginado; POST `/{id}/approve` y `/reject`.
- GET `/api/platform/audit` por cursor y `/{id}`; GET/PUT `/api/platform/settings`; GET/POST `/api/platform/legal-documents` y GET `/{id}`; publicación inmutable e idempotente.
- Front: `/plataforma`, `/plataforma/organizaciones/:id`, `/plataforma/cuentas`, `/plataforma/cuentas/:id`, `/plataforma/recuperaciones`, `/plataforma/legales`, `/plataforma/auditoria`, `/plataforma/configuracion`, `/recuperar`.

## Seguimiento y tareas verificables

### T00 · Línea de base y plan

- [x] Confirmar clean/main/últimos commits en ambos repos, leer maestros y aprobaciones. Aspire no está corriendo; Docker responde fuera del sandbox.
- [x] Revisión independiente del documento: Approved, sin bloqueos. Commit inicial exclusivo del plan antes de programar.

### T01 · Roles globales puros (primera tarea)

Archivos: `Domain/Platform/{PlatformRole,PlatformRoleAssignment,PlatformRolePermissions}.cs`, AGENTS/CLAUDE; `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Platform/PlatformRoleTests.cs`; `docs/features/plataforma.md` inicial.

- [ ] Rojo: Owner coincide con PlatformPermissions.All; Support tiene exclusivamente tenants.read/accounts.read/recoveries.manage/audit.read; asignación retiene UserId, valida Guid/enum y cambia de rol; no es ITenantOwned.
- [ ] Crear enum Owner/Support, catálogo de permisos inmutable y asignación global IVersioned con fábrica y cambio de rol. No introducir un permiso nuevo ni datos de muestra.
- [ ] Verde focal y build Domain; documentar roles/permisos. Commit `feat(plataforma): definir roles globales`.

### T02 · Recorrido E5 real inicialmente rojo

Archivos: front `scripts/test-e2e-real.test.mjs` (runner y recorridos actuales en un único archivo); back `tools/ArquitecturaBaseMultitenant.RealE2ESetup/{Program,PlatformCommand}.cs`.

- [ ] Preparar operador y Support solo en base aislada y leer códigos por pickup; reutilizar helpers de login del runner, sin interceptaciones.
- [ ] Añadir recorrido operador → `/plataforma` → configuración → organizaciones y cuentas. Ejecutar focal: debe fallar por API/pantalla de E5 ausente, preservando anteriores.
- [ ] Registrar evidencia del rojo en el seguimiento. El verde final es T25; no marcar E5 cerrada ahora. Commit `test(plataforma): iniciar recorrido real`.

### T03 · Persistencia y autorización de operadores

Archivos: PlatformRoleAssignmentConfiguration/Repository/Reader, ApplicationDbContext, PlatformSeeder, PlatformPermissionService y puertos; migración nueva; tests `PlatformPermissionServiceTests`, `PlatformOperatorPersistenceTests` e inventarios existentes.

- [ ] Rojo: Support obtiene solo sus cuatro permisos, suspendido/deleted pierde acceso, seed previo IsPlatformOperator migra a Owner, segunda ejecución no duplica y mt_app no puede editar auditoría.
- [ ] Persistir `platform.PlatformRoleAssignments` con unicidad UserId/FK identidad/xmin. Migración convierte operadores existentes a Owner; seed agrega asignación al crear/habilitar operador. Mantener IsPlatformOperator como indicador de acceso sincronizado transaccionalmente, sin usarlo para conceder todos los permisos.
- [ ] Verde focal/PostgreSQL/autorización y build. Commit `feat(plataforma): persistir permisos de operadores`.

### T04 · Propuesta visual para elección conjunta

Archivos front: `docs/design/lienzo/generadores/plataforma_*.py`, tableros propuestos en página propia y visor `.local/etapa-5-propuesta/`; informe visual inicial.

- [ ] Inventariar estados de los 8 tableros desktop y los 4 móviles actuales; identificar móviles de ficha/recuperaciones/legales que faltan para la puerta.
- [ ] Refinar con Criterio-* actual manteniendo contenidos/acciones; copiar el marco app de Organización; mostrar listas con 1 y 50 filas. Conservar menú desktop y modal Audit aprobados.
- [ ] Presentar juntos navegación inferior/Más, fichas y diálogos app, lista/tabla/densidad y legales; pedir solo elecciones pendientes con enlaces concretos. No implementar estas pantallas hasta respuesta; continuar T05–T16/T18–T19 independientes.
- [ ] Registrar elección y actualizar solo tableros correspondientes/canvas; commit de diseño explícito.

### T05 · Configuración global

Archivos: PlatformSettings/Configuration/Repository/Reader, modelos/validadores/PlatformSettingsService, controller/contratos; `PlatformSettingsServiceTests`, `PlatformSettingsTests`.

- [ ] Rojo: modos ConsumerSignup/BusinessSignup/límite >=1; stale version 409; Support 403; cambios dejan motivo y SecurityEvent mismo commit.
- [ ] IVersioned/xmin; GET/PUT con exactamente los tres campos del tablero; usar catálogos y enums vigentes, sin editar la gracia en UI. Recursos es/en y contrato.
- [ ] Verde focal/integración/build. Commit `feat(plataforma): administrar configuración global`.

### T06 · Listados globales de organizaciones y estado

Archivos: IPlatformReader/PlatformReader, Tenant (IVersioned si edición), modelos/validadores/servicio/controller TenantAdministration, SecurityEvent; tests TenantAdministrationRead/State y Tenancy/PlatformAccess.

- [ ] Rojo: solo Business, paginado/búsqueda/conteos compartidos, ficha 404 inexistente, PendingApproval→Closed al rechazar, Active→Suspended→Active, cierre desde estados permitidos; B2C sigue operativo al suspender Business.
- [ ] Listado/ficha global sin consultar datos privados; eventos con motivo/operador y aviso al creador/Dueños por outbox. Invalidar AccessStatusCache después del commit.
- [ ] 401/403 para cada ruta, versión vieja 409, transiciones inválidas sin cambios; inventario y OpenAPI. Commit `feat(plataforma): administrar organizaciones`.

### T07 · Aprobar, crear con invitación y reprovisionar

Archivos: TenantAdministrationService/helpers, Auth/TenantProvisioner/TenantSpaceProvisioner/TenantProvisioningReplayVerifier, Invitations/InvitationIssuer, contratos y tests de provisioning/invitación.

- [ ] Rojo: aprobar pendiente prepara roles/settings una sola vez; retry idempotente no duplica; crear con invitación a persona sin cuenta no crea identidad/Personal y asigna el rol Dueño al aceptar; inválidos/slug tomado conservan estado.
- [ ] Extender provisioner con entrada explícita para dueño invitado, conservando restricciones de StageAsync de E3/E6. Autorización/evento confirmado antes de Enter; entrar fuera de UoW y después provisionar/encolar en un único writer. Aprobar crea lo pendiente o valida replay, sin anidar transacciones.
- [ ] Probar recuperación después de falla y concurrencia; regresiones E6. Commit `feat(plataforma): provisionar organizaciones e invitar dueños`.

### T08 · Módulos y FeatureGate

Archivos: Domain/Features, Configuration/Features, puerto IFeatureService; Infrastructure/Features; TenantFeatures configuración/reader/repo/migración; MeResponse/ProfileSnapshotBuilder; tests FeatureGateTests y ModuleControllersTests requeridos, contratos y feature tests front.

- [ ] Rojo: apagado 404 ProblemDetails, prendido 200; emergencia global gana; Trial vence en límite exacto; caché invalida; alcance Consumer/Business no se mezcla.
- [ ] Agregar paquete central y registro Microsoft.FeatureManagement.AspNetCore, filtro Tenant/caché t:{id}:features, DisabledFeatureHandler; persistir origen/vencimiento/version, motivo y SecurityEvent al cambiar desde Plataforma.
- [ ] Catálogo productivo inicialmente vacío: núcleo siempre disponible, sin módulos de ejemplo ni WhatsApp. Gates de recorrido viven en TestFeatures; el framework soporta catálogo de nuevos módulos reales siguiendo guía.
- [ ] `features` de `/api/me` siempre presente, lista vacía cuando corresponda; front useFeature/Feature/FeatureRoute y `feature` en rutas/navegación. Contrato lee ambos repos. Commit `feat(modulos): habilitar funcionalidades por organización`.

### T09 · Entrada privada y moderación de página

Archivos: PlatformActionGuard y servicio de registro de acceso; TenantScope; PublicPage y writer de moderación; PlatformPublicSiteController; SecurityEvent/recursos; tests PlatformScope/PublicPageModeration.

- [ ] Rojo: operador sin Enter no ve datos; motivo/permiso preceden SecurityEvent confirmado y Enter sin transacción; excepciones restauran alcance. Support lectura conforme permiso, mutación 403.
- [ ] GET solo metadatos de página; Unpublish vuelve Draft con bloqueo/actor/motivo/fecha, AllowPublish retira bloqueo; organización no publica bloqueada. Avisar Dueños sin exponer cuerpo/logo a plataforma si no lo requiere tablero.
- [ ] Estados/409/aislamiento y regresión PublicPage E6; commit `feat(plataforma): moderar páginas públicas`.

### T10 · Dominio verificado desde Plataforma

Archivos: PlatformDomainController/contracts; PlatformTenantDomainService/adaptador del writer OrganizationDomainService; tests PlatformDomainAdministration y regresiones métodos administrados.

- [ ] Rojo: verificación DNS TXT real vía puerto, dominio único, unión de existentes/nuevos; quitar conserva ManagedByTenantId de anteriores y bloquea exmiembros; motivo/version/permiso/alcance exigidos.
- [ ] Reutilizar lógica de E6 mediante writer compartido, guard/evento antes de Enter. No llamar un servicio público dentro de UoW ni abrir dos scopes superpuestos.
- [ ] Verde focal/integración/build. Commit `feat(plataforma): administrar dominios verificados`.

### T11 · Consulta global de cuentas

Archivos: IPlatformReader/PlatformReader (identidad global explícita), Account models/filter counts/controller; tests PlatformAccountReader y NeedsRecovery.

- [ ] Rojo: búsqueda nombres/métodos enmascarados según contrato; paginado y filtros Active/Suspended/PendingDeletion/Deleted/NeedsRecovery; estado derivado solo Active sin método disponible, membresía terminada invalida correo administrado, resto de estados tiene prioridad.
- [ ] Proyectar ficha con métodos/membresías/operador sin leer Members fuera de RLS; usar proyección global autorizada explícitamente para consulta de plataforma y adaptar whitelist existente. Nunca loguear valores de búsqueda ni direcciones.
- [ ] 401/403, filtros/conteos y contrato. Commit `feat(plataforma): consultar cuentas globales`.

### T12 · Suspender, reactivar y cerrar sesiones de cuenta

Archivos: PlatformAccountService/writer, IUserRepository/UserRepository/ApplicationUser, ISessionRevoker, cache/events/notices; tests PlatformAccountState y integración de tokens.

- [ ] Rojo: suspensión revoca todos los accesos/sesiones; reactivación con baja programada deja PendingDeletion; revoke-sessions mantiene estado; Support no muta; último Owner/self protections según rol y reglas de acceso.
- [ ] Motivo y version en cambios, lock por cuenta; SecurityEvent con actor operador y avisos existentes en mismo commit; caché tras guardar.
- [ ] Tokens/cookie/F5 regresión y build. Commit `feat(plataforma): administrar estado y sesiones de cuentas`.

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

- [ ] Rojo: Support puede revisar; aprobar agrega método propio verificado y quita NeedsRecovery sin duplicar ni emitir sesión; rechazar conserva métodos; motivo/version y repetición protegida; pedidos de cuenta Deleted no restauran identidad.
- [ ] Locks por pedido/cuenta/método, validación de unicidad con índices; SecurityEvent/aviso. Participante de eliminación cierra/purga datos personales de pedidos pendientes conforme ADR0035.
- [ ] Verde integración y pruebas de privacidad. Commit `feat(recuperacion): revisar pedidos y cerrar los de cuentas eliminadas`.

### T16 · Gestión de operadores

Archivos: PlatformOperatorService/guard/repo/controller cuentas; tests PlatformOperators/LastOwner.

- [ ] Rojo: conceder Owner/Support solo a cuenta activa con método propio verificado; cambiar/quitar roles versionado; no quitar/suspender/eliminar último Owner ni revocar autorización sin invalidar caché/tokens.
- [ ] Cuentas incluye operadores; no pantalla/ruta nueva fuera del plan. Grant/revoke mantiene IsPlatformOperator en misma UoW y agrega evento con motivo. Último Owner serializado por lock global.
- [ ] Verde permisos/cookies/seed/migración. Commit `feat(plataforma): administrar operadores`.

### T17 · Auditoría de seguridad

Archivos: ISecurityEventReader/SecurityEventReader, PlatformAuditService/controllers/query; traducciones Audit/recursos; tests PlatformSecurityAudit.

- [ ] Rojo: cursor estable fecha/Id, filtro actor/acción/objetivo; detalle con fecha y campos lectura, acciones traducidas, actores eliminados/objetivo conservado; payload sin correo/teléfono/token completos.
- [ ] Join a nombres actuales o resumen histórico no personal según datos existentes; evento inmutable, no total de tabla creciente. GET listado/detalle separados.
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

- [ ] Copiar estados elegidos de Organizaciones/Organizacion y sus app móviles: filtros/conteos/1 y 50 filas, ficha/tabs y diálogos aprobar/rechazar/crear/suspender/reactivar/cerrar/retry, Modules/PublicSite/Domain.
- [ ] Conectar API real y versiones; no duplicar handlers de E6 ni inventar ediciones ajenas; 409 conserva selección/draft, motivo obligatorio y acción irreversible roja.
- [ ] Pantalla/red/errores/permisos, lint/build. Commit(s) coherentes `feat(plataforma): conectar organizaciones`.

### T22 · Cuentas y operadores: listado y ficha real

Archivos: `src/areas/platform/accounts/{api,pages,components}/`, locales/test.

- [ ] Copiar Cuentas/Cuenta-Plat y móviles aprobados: estados derivados, métodos/organizaciones leídos, suspender/reactivar/cerrar sesiones/baja/operador con roles disponibles y bloqueos del último Owner.
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

- [ ] E2E E5 real: operador entra/F5; Support restricciones; crear organización con invitado sin cuenta y aceptar sin Personal; aprobar pendiente/retry sin duplicados; suspender Business deja Personal; módulos gate; despublicación bloquea E6; dominio conserva vínculos al quitar; suspender/reactivar/revocar/baja cuenta; recuperación nuevo correo aprobada; legal nueva versión aceptada; audit/settings guardados.
- [ ] Mantener recorridos E3a/b/c, E4 y E6. Sin mocks/interceptación y PostgreSQL/pickup efímeros. Si una integración externa no puede correr localmente, anotarla pendiente sin inventar verde.
- [ ] `dotnet build ArquitecturaBaseMultitenant.slnx` cero advertencias; `dotnet test` completo con Docker.
- [ ] Front `npm test`, `npm run lint`, `npm run build`, `npm run contracts:check`, `npm run test:e2e:real` completos.
- [ ] Generador de referencias/tests scripts y arneses existentes según CI; HarnessTests/front harness y estructura; ModuleControllersTests/FeatureGateTests; no arnés adicional.
- [ ] Agregar solo E5 a ClosedStages en ambos repos cuando TODO lo requerido esté verde; conservar E6. Informe con commits, salidas, decisiones, diferencias visuales y pendientes reales. Aspire stop si se levantó; repos clean/main, sin push.

## Decisiones tomadas

1. Catálogo productivo de módulos vacío en E5: los servicios del núcleo no son módulos y WhatsApp queda excluido. Los gates se ejercitan con TestFeatures y catálogo inyectado de test, nunca datos productivos de ejemplo.
2. Owner/Support se persisten en asignación global única por cuenta; IsPlatformOperator conserva la selección de acceso pero no resuelve permisos por sí solo. Seed/migración preservan al operador existente como Owner.
3. El guard de acceso privado confirma un SecurityEvent antes de Enter mediante un servicio transaccional específico; el writer privado corre después, con una sola UoW propia. No Enter dentro de una transacción, ni helper con UoW, ni transaction anidada.
4. Crear con invitación prepara roles/settings sin crear identidad: una entrada explícita del TenantProvisioner permite dueño pendiente, InvitationIssuer reserva Member(Invited) con TenantAdmin y la aceptación vigente vincula/crea cuenta sin Personal.
5. El refinamiento visual pendiente se revisa como grupo concreto; aprobaciones desktop/menu/modal previas se conservan. Inicios de Organización y WhatsApp no se rediseñan en E5.

## Registro de resultados

| Tarea | Estado/evidencia | Commit back | Commit front |
|---|---|---|---|
| T00 | Línea de base limpia, Aspire parado, Docker disponible | pendiente del plan | — |

No declarar cierre a partir de esta tabla sin completar T25.
