# Etapa 5: Plataforma — plan ejecutable

> **Para agentes:** aplicar `writing-plans` y ejecutar con `executing-plans` en este chat. El usuario autorizó `main`, sin worktree ni push. Revisar el plan con un revisor de documentos, no delegar programación. Las casillas registran evidencia real; una tarea termina con su commit explícito y verificado.

**Objetivo:** completar la administración global de organizaciones, cuentas, recuperaciones, operadores, módulos, documentos legales, auditoría y configuración, con API real y pantallas elegidas del lienzo.

**Arquitectura:** Domain ← Application ← Infrastructure; Api compone. Copiar Roles, E3 y Organización E6. Los datos de Plataforma son globales; los datos privados de una organización se consultan solo después de autorizar, registrar el SecurityEvent y entrar con ITenantScope, antes de abrir su transacción. Una escritura delegada tiene un único propietario transaccional, siguiendo el patrón existente PublicPageService/PublicPageEditor.

**Tecnologías:** .NET 10, EF Core/Npgsql, PostgreSQL 18, Aspire 13.5, Microsoft.FeatureManagement.AspNetCore, HybridCache, React/TypeScript, TanStack Query, i18next, Vitest y Playwright.

## Fuentes, restricciones y punto de partida

- Back: `C:/Users/ezequ/source/repos/ArquitecturaBaseMutitenant`, main limpio, `a12edb7`.
- Front: `C:/Users/ezequ/source/repos/ArquitecturaBaseMutitenantFront`, main limpio, `44bf951`.
- E4 y E6 cerradas. Conservar ClosedStages `{0,1,2,3,4,6}` hasta cerrar E5; el salto no reabre E6.
- Fuente visual: front `docs/design/lienzo/` vigente (v72 al elegir T04). El usuario eligió los ocho escritorios con ajustes el 03/10; registrar esa elección en tableros/canvas/README y mostrar ocho capturas antes de programarlos. Los móviles son los validados en v72 (`9841dbd`), tal cual; se descartan los móviles de la propuesta. Ningún tablero «por refinar» se programa.
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

- [x] Rojo: tipos ausentes (CS0234) tras restaurar NuGet fuera del sandbox; Owner/Support, inmutabilidad y asignación global cubiertos por cinco pruebas.
- [x] Enum Owner/Support, catálogo inmutable y asignación global IVersioned con fábrica y cambio de rol. Sin permisos nuevos ni datos de muestra.
- [x] Verde focal: 5/5; build Domain: 0 advertencias/errores. Ficha inicial documenta roles y límites. Commit `feat(plataforma): definir roles globales`.

### T02 · Recorrido E5 real inicialmente rojo

Archivos: front `scripts/test-e2e-real.test.mjs` (runner y recorridos actuales en un único archivo); back `tools/ArquitecturaBaseMultitenant.RealE2ESetup/{Program,PlatformCommand}.cs`.

- [x] Reutilizar operador del seed aislado y códigos por pickup; sin interceptaciones. Support se agrega al preparador cuando T03 habilita su persistencia.
- [x] Recorrido `/plataforma` sin sesión → código real → listado y APIs de configuración/organizaciones/cuentas. Rojo confirmado esperando el encabezado Organizaciones (pantalla E5 ausente), 2 tests fallidos, duración 91,56 s. Una ejecución cruzada con generación de migración se descartó.
- [x] Evidencia registrada. El verde final es T25; E5 sigue abierta. Front `696c2b3`; Node check y lint focal sin errores. Lint general retorna 0 con advertencias previas de componentes y lienzo-v35.

### T03 · Persistencia y autorización de operadores

Archivos: PlatformRoleAssignmentConfiguration/Repository/Reader, ApplicationDbContext, PlatformSeeder, PlatformPermissionService y puertos; migración nueva; tests `PlatformPermissionServiceTests`, `PlatformOperatorPersistenceTests` e inventarios existentes.

- [x] Rojo por reader ausente; permisos limitados y pérdida de autorización con cuenta inactiva. Regresión real de actualización desde operador legado sin Personal; el seed se repite sin duplicar asignaciones. La protección append-only de auditoría conserva su test existente.
- [x] `platform.PlatformRoleAssignments`: unicidad UserId/FK identidad/xmin. Migración convierte operadores existentes a Owner y otorga al runtime permisos explícitos sobre esta tabla; seed conserva roles existentes. IsPlatformOperator selecciona acceso, no concede permisos.
- [x] Unit 6/6; PostgreSQL roles 3/3, ProductionSeed 1/1, Migrations 4/4; clasificación/configuración existentes 4/4. Build Api/AppHost/preparador 0 advertencias. SQL revisado; modelo sin cambios pendientes. Commit `feat(plataforma): persistir permisos de operadores`.

### T04 · Propuesta visual para elección conjunta

Archivos front: `docs/design/lienzo/generadores/plataforma_*.py`, tableros propuestos en página propia y visor `.local/etapa-5-propuesta/`; informe visual inicial.

- [x] Inventario: 8 desktop y 4 móviles antiguos; muestras de fichas/recuperación/legales preparadas en página separada sin modificar canvas ni generador ajeno.
- [x] Propuesta compacta con estados interactivos y marco app de Organización, listas 1/50 (1 y 10 filas visibles respectivamente). Menú desktop y modal Audit conservados.
- [x] Visor conjunto en `http://127.0.0.1:61405/.artifacts/etapa-5-propuesta/index.html`: 8 desktop y 16 móviles/estados; pregunta de elección presentada. Continuar back mientras espera; no implica aprobación. Front `e670892`.
- [ ] Registrar elección y actualizar solo tableros correspondientes/canvas; commit de diseño explícito.

### A00 · Alineación previa a las pantallas de Plataforma (instrucción del 03/10)

Fuente: `docs/reviews/2026-10-03-alineacion-lienzo.md`, ya copiado sin cambios del informe temporal en `cc2835b`. Las marcas ✔ del informe significan hallazgos verificados, no correcciones terminadas.

- [ ] Paquete 0 completo. Otro chat corrige el front; este chat trabaja exclusivamente el backend mientras dure esa coordinación, sin modificar ni commitear cambios ajenos.
- [x] 0.7: error propio `Tenancy.Member.LastTenantAdmin` para quitar/eliminar roles del último Dueño y `Tenancy.Member.CannotDisableSelf` para el acceso propio; la baja de cuenta conserva `Legal.AccountDeletion.LastAdmin`. Rojo dirigido → Usuarios 3/3, baja de cuenta 6/6, recursos 6/6 y build Api 0 advertencias.
- [ ] Después de 0: actualizar frontend.md §4/Teléfono, responsive, formularios, pantallas-y-ui y AGENTS del front desde los ocho Criterio-* vigentes. Sin pruebas de documentación nuevas.
- [ ] Después de las fichas: Paquete 4 pasos 2–4, piezas compartidas de app, navegación declarativa/Más/E2E a390 y todas las hojas; antes de T20–23.
- [ ] Capturas solo de pantallas nuevas y estados principales contra el lienzo vigente; E2E real en verde. Mantener las verificaciones focales y la puerta funcional de cierre.

Elección T04: Organizaciones combina Dueño/correo; ficha Organización usa resumen (Dueño, dominio, página, módulos) e Historial como lista; Cuentas combina nombre/correo y último ingreso/método sin teléfono; métodos en Cuenta son filas como Mi cuenta; Recuperaciones y Configuración conservan propuesta; Legales es lista sin columnas, versión/vigencia debajo; Auditoría abre desde Qué pasó, sin ojo ni conteo en la banda.

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

Avance de dominio: rojo por tipos ausentes → 4/4 verdes (límite exacto de vencimiento, apagado manual, inválidos sin mutación y accesos separados). Catálogo productivo vacío; recursos es/en 6/6 y códigos 6/6; build Domain 0 advertencias. Persistencia, filtro y HTTP siguen pendientes.

- [ ] Rojo: apagado 404 ProblemDetails, prendido 200; emergencia global gana; Trial vence en límite exacto; caché invalida; alcance Consumer/Business no se mezcla.
- [ ] Agregar paquete central y registro Microsoft.FeatureManagement.AspNetCore, filtro Tenant/caché t:{id}:features, DisabledFeatureHandler; persistir origen/vencimiento/version, motivo y SecurityEvent al cambiar desde Plataforma.
- [ ] Catálogo productivo inicialmente vacío: núcleo siempre disponible, sin módulos de ejemplo ni WhatsApp. Gates de recorrido viven en TestFeatures; el framework soporta catálogo de nuevos módulos reales siguiendo guía.
- [ ] `features` de `/api/me` siempre presente, lista vacía cuando corresponda; front useFeature/Feature/FeatureRoute y `feature` en rutas/navegación. Contrato lee ambos repos. Commit `feat(modulos): habilitar funcionalidades por organización`.

### T09 · Entrada privada y moderación de página

Archivos: PlatformActionGuard y servicio de registro de acceso; TenantScope; PublicPage y writer de moderación; PlatformPublicSiteController; SecurityEvent/recursos; tests PlatformScope/PublicPageModeration.

Avance del acceso privado (prerrequisito de T07): prueba roja por tipos ausentes → 3/3 unitarias y 2/2 con PostgreSQL. Comprueba permisos persistidos, evento visible desde otra conexión antes de Enter, ausencia de transacción abierta y restauración del alcance ante excepción. Build Api 0 advertencias e inventarios existentes 12/12. Moderación sigue pendiente.

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
- [ ] Completar el contacto del Dueño de organización creada por Plataforma cuando la invitación aún no tiene identidad: el tablero muestra el correo desde el alta. Proyección administrativa explícita y limpieza al eliminar la identidad, sin leer Invitaciones fuera de RLS ni conservar un correo personal huérfano.
- [ ] 401/403, filtros/conteos y contrato. Commit `feat(plataforma): consultar cuentas globales`.

### T12 · Suspender, reactivar y cerrar sesiones de cuenta

Archivos: PlatformAccountService/writer, IUserRepository/UserRepository/ApplicationUser, ITokenRevoker/ISignInService, cache/events/notices; tests PlatformAccountState y integración de tokens.

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
6. Configuración exige motivo y versión; su evento se guarda en la misma transacción y la caché se invalida después. La gracia de baja de 30 días no es editable.
7. Conteos globales de organizaciones se mantienen con deltas atómicos desde escrituras privadas bajo RLS. Plataforma lee solo metadatos y contacto global explícito; no entra a cada organización para armar un listado. La fecha histórica sin evidencia queda null.
8. Crear copia los cinco campos del tablero (nombre, primera empresa, correo del Dueño, cultura y zona). No agrega un campo de slug ni de motivo: genera la dirección desde el nombre y, ante reserva/colisión/nombre inválido, usa `org-{operationId}`; la descripción fija de la acción sale de resources. Los otros cambios administrativos exigen motivo escrito. Muestra móvil corregida en front `d4d0627`.
9. El retry incompleto solo reconstruye un miembro cuando existe registrante autoservicio activo; nunca convierte al operador creador en Dueño ni adivina el nombre de una empresa perdida. Sin responsable verificable conserva Provisioning y devuelve conflicto.
10. Los deltas de conteos son constraint triggers `DEFERRABLE INITIALLY DEFERRED`, dentro de la misma transacción: se ejecutan después del SaveChanges para no adelantar xmin antes del UPDATE de estado. Se conserva el rechazo de versiones viejas. Referencia: [CREATE TRIGGER, PostgreSQL 18](https://www.postgresql.org/docs/current/sql-createtrigger.html).

## Registro de resultados

| Tarea | Estado/evidencia | Commit back | Commit front |
|---|---|---|---|
| T00 | Línea de base limpia, Aspire parado, Docker disponible; revisión Approved | `fff99c9` | — |
| T01 | Rojo CS0234 → 5/5 verde, build Domain 0 advertencias | `f03c1b1` | — |
| T02 | E2E real rojo por pantalla E5 ausente; Aspire detenido | `4ce80b7` (registro) | `696c2b3` |
| T03 | Permisos 6/6; roles PostgreSQL 3/3; seed 1/1; migraciones 4/4; inventarios 4/4 | `4ce80b7` | — |
| T04 | Propuesta conjunta verificada, elección pendiente; canvas intacto | seguimiento de esta tarea | `e670892` |
| T05 | Dominio 12/12; servicio 4/4; HTTP 3/3; recursos 6/6; accesos/contratos 15/15; migración/build verificados | `6a49970` | — |
| T06 | Dominio nuevo 3/3 y regresiones 7/7; HTTP/RLS/tokens 5/5; recursos 6/6; inventarios 28/28; build/migración verificados | `f785e5b` | — |
| T07 | Scope 3/3 + PG 2/2; core 3/3 + PG 2/2; HTTP 8/8; slug 5/5; regresiones 36/36; recursos 6/6; inventarios 32/32; build/SQL verificados | `b375f04`, `d27d7d3`, `f05c9df`, `6fa09c4` | `d4d0627` (muestra) |

No declarar cierre a partir de esta tabla sin completar T25.
