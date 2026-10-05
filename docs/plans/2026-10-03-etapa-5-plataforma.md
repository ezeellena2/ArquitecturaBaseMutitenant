# Etapa 5: Plataforma — plan ejecutable

> **Para agentes:** aplicar `writing-plans` y ejecutar con `executing-plans` en este chat. El usuario autorizó `main`, sin worktree ni push. Revisar el plan con un revisor de documentos, no delegar programación. Las casillas registran evidencia real; una tarea termina con su commit explícito y verificado.

**Objetivo:** completar la administración global de organizaciones, cuentas, recuperaciones, operadores, módulos, documentos legales, auditoría y configuración, con API real y pantallas elegidas del lienzo.

**Arquitectura:** Domain ← Application ← Infrastructure; Api compone. Copiar Roles, E3 y Organización E6. Los datos de Plataforma son globales; los datos privados de una organización se consultan solo después de autorizar, registrar el SecurityEvent y entrar con ITenantScope, antes de abrir su transacción. En lo que falta de E5, la transacción la abre el método público del servicio, como RoleService; los helpers no reciben IUnitOfWork y no se crean clases que solo reenvían ni excepciones en TransactionBoundaryTests. Lo ya implementado se consolida antes de E7, según la revisión de calidad y ADR 0039.

**Tecnologías:** .NET 10, EF Core/Npgsql, PostgreSQL 18, Aspire 13.5, Microsoft.FeatureManagement.AspNetCore, HybridCache, React/TypeScript, TanStack Query, i18next, Vitest y Playwright.

**Cambio de orden (2026-10-03, aprobado por el usuario).** Codex dejó la etapa abierta después de T09, con T10 sin commitear, y la sigue Claude en dos partes:

1. **Back, ya:** T10 a T18 y el punto pendiente de T08 (`features` en `/api/me`), sin pantallas.
2. **Front, después de la [consolidación](2026-10-03-consolidacion.md):** T19 a T25. Así las pantallas de Plataforma nacen con las piezas comunes del front (errores, permisos, paginado, accesos y edición) y no repiten la duplicación que encontró la revisión de calidad.

La etapa cierra con T25. La Etapa 7 espera a que cierre la 5.

El 2026-10-05 el usuario decidió que las pantallas (T19 a T25) las programa **Codex**, con el plan ejecutable [2026-10-05-etapa-5-pantallas.md](2026-10-05-etapa-5-pantallas.md). Ese plan dice:
- qué tablero copiar en cada pantalla;
- de qué pantalla del código se toma el patrón;
- las partes de back que suma: T18c, T20a, T21a y el comienzo de T22a, T23c y T23d.

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
| Persistencia | `Application/Interfaces/Persistence/{IPlatformReader,IPlatformPermissionAssignmentRepository,IPlatformPermissionAssignmentReader,ITenantFeatureRepository,ITenantFeatureReader,IAccountRecoveryRepository,IAccountRecoveryReader,ISecurityEventReader}.cs`; implementaciones específicas en Infrastructure/Persistence/{Repositories,Readers,Configurations/Platform,Migrations} |
| Borde HTTP | `Api/Controllers/Platform/` y `Api/Contracts/Platform/`; controllers separados por área; recuperación pública en `Api/Controllers/Auth/AccountRecoveryController.cs` con AllowAnonymous |
| Integraciones | `Infrastructure/Features/{TenantFeatureFilter,FeatureService,DisabledFeatureHandler}.cs`, registro DI; recuperación/cambios globales encolan por los puertos existentes de correo y avisos de cuenta |
| Front | `src/areas/platform/{tenants,accounts,recoveries,legal,audit,settings}/{api,pages}/`; cuenta incluye operadores. Recuperación pública en `src/areas/public/auth/pages/RecoverAccountPage.tsx` |
| Marco y módulos | `src/layouts/navigation/{types,platform}.ts`, PrivateLayoutFrame, Sidebar, AdminPanel; `feature` en el manifiesto de rutas y `visibleNavigation`, con `hasFeature` de `usePermissions` (consolidación F5) |
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
- [x] R00 · Conducta: invitación por correo sin Personal/Business, rechazo de toda cuenta previa con «Un operador no puede tener perfiles de persona» y último operador activo con operators.manage protegido bajo lock global al quitar, suspender o iniciar baja. Se entregan con T12, T13 y T16. Entregada: T12 (`f487ed1`), T13 (`a9a0a8c`) y T16.
- Avance R00: el último operador activo con `operators.manage` queda protegido bajo `global:platform-operators` al suspender (T12, `f487ed1`) y al iniciar la baja (T13), con 409 `LastManager`. Faltan la invitación, el rechazo de cuentas previas y quitar permisos, que llegan con T16.
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
- [x] Ampliación (decisión 6 y Config-Plat v81): correos de ventas y de soporte en `PlatformSettings`, como `Email` y vacíos hasta que se cargan (migración `PlatformContactEmails`); el PUT los exige y un correo vacío o mal escrito da «Ingresá un correo válido.» en su campo; el diff del SecurityEvent dice `salesEmailChanged`/`supportEmailChanged` sin el valor. Lectura pública sin sesión `GET /api/contact` para Ingreso y Registro (pedido de la revisión de decisiones). La caché guarda los correos como texto (su serializador no conoce el value object). Rojo: Configuración 2/3 y baja por Plataforma con el texto viejo; verde: Configuración 3/3, dominio 269/269, Application 441/441, arquitectura 154/154 e integración afectada 64/64. Commit `feat(plataforma): correos de contacto en la configuración`.

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
- [x] Front: lo trajo la consolidación (F5, front `5ee936c` y `5fcf5c3`). `feature` va en el manifiesto de rutas; `RequirePermission` responde como un 404 si el módulo está apagado (`RequirePermission.test.tsx`); el menú lo filtra `visibleNavigation` con `hasFeature` de `usePermissions`. No hay `useFeature`, `<Feature>` ni `FeatureRoute`.

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
- [x] Corrección pedida por la revisión de decisiones del 03/10: la búsqueda de cuentas usaba la copia `ApplicationUser.Email`, contra emails.md. Ahora busca por nombre y por los métodos de ingreso (correo, teléfono y el correo de contacto de Google). Rojo en worktree: una copia vieja encontraba la cuenta (cuentas 2/3); verde 3/3. Commit `fix(plataforma): buscar cuentas por sus métodos de ingreso`.

### T12 · Suspender, reactivar y cerrar sesiones de cuenta

Archivos: PlatformAccountService/writer, IUserRepository/UserRepository/ApplicationUser, ITokenRevoker/ISignInService, cache/events/notices; tests PlatformAccountState y integración de tokens.

- [x] Rojo en worktree aislado por tipos ausentes (CS0117: `AccountSuspended`, `AccountReactivated`, `AccountSessionsRevoked`, `LastManagerCode`). Cubre: 401 sin sesión, 403 con solo `accounts.read`, 400 sin motivo o versión, 404 para un id inexistente; suspender revoca el token de la persona (401 en `/api/me`) y repetirla da 409 `Users.User.InvalidTransition`; reactivar con versión vieja da 409 y con la vigente vuelve a Active y deja ingresar con un código nuevo; cerrar sesiones revoca sin cambiar el estado, cambia la versión y su reenvío es idempotente; una cuenta suspendida con baja programada vuelve a PendingDeletion; el último operador con `operators.manage` no se suspende (409 `Platform.Operator.LastManager`) hasta que hay otro, y un operador no se suspende a sí mismo (403 `Users.User.CannotModifySelf`).
- [x] Motivo, versión y lock por cuenta; `PlatformOperatorGuard` toma antes el lock global de operadores. SecurityEvent con operador, cuenta, motivo, diff de `status` e IP enmascarada en la misma transacción; caché de estado después del commit. No hay avisos previstos (el tablero no los muestra). `PlatformTenantAudit` pasa a `PlatformAudit` porque registra organizaciones y cuentas; `PlatformAccountStateService` tiene 7 dependencias de negocio y abre su transacción.
- [x] Verde: estado de cuentas **3/3**; cuentas, organizaciones, moderación, dominios, inventario y OpenAPI **20/20**; Application **441/441**; arquitectura **154/154**; build 0 advertencias. OpenAPI solo agrega las tres rutas y el contrato del pedido. Commit `feat(plataforma): administrar estado y sesiones de cuentas`.

### T13 · Baja por Plataforma (ADR 0035)

Archivos: ApplicationUser/UserRepository, PlatformAccountService, AccountDeletionPolicy/Requester/Processor, models/notice resources; tests PlatformDeletion y AccountDeletionWorker.

- [x] Rojo en worktree aislado con el test sobre `f487ed1`: 0/3, la ruta no existe (404 donde se esperaba 400, 204 y 403). Cubre: activa → PendingDeletion con 30 días, sesiones revocadas, `DeletionRequestedByOperatorId`, métodos reservados, un solo `AccountDeletionRequested` con el operador como actor, aviso de Plataforma sin el motivo, segunda baja 409 `AlreadyPending` y la persona cancela ingresando; suspendida → sigue Suspended con la fecha, aviso «no podés cancelar», el ingreso da `Identity.Account.Suspended` y al reactivarla pasa a PendingDeletion; base aislada: sin `accounts.manage` 403, único Dueño de una organización suspendida 409 `LastAdmin` (la cerrada no cuenta, y la ficha lo muestra), último operador 409 `LastManager`, la cuenta propia 403 y, cerrada la organización, la baja pasa.
- [x] `AccountDeletionScheduler` reúne lo que comparten las dos bajas (bloqueos de módulos, fecha según la gracia, sesiones, participantes y aviso); `AccountDeletionRequester` queda con lo de la persona (estado, bloqueos, reautenticación). `PlatformAccountDeletionService` (8 dependencias de negocio) abre su transacción: guarda de operadores, lock de la cuenta, versión, estado, `PlatformSoleOwnerGuard` (organizaciones no cerradas, cualquier estado de la cuenta, con el lock de asignaciones) y el evento con diff de `status` y `deletion.scheduledForUtc`. El aviso `DeletionRequested` suma la variante de Plataforma con textos provisorios hasta el tablero de Mensajes (pedido a Diseño por la Guía); solo correo. La ficha usa la misma regla de único Dueño; Mi cuenta y la baja de la persona conservan la suya, y `SoleOwnerOrganizations` vuelve a `AccountLoginMethodsService`.
- [x] Verde: baja por Plataforma **3/3**; con estado de cuentas, cuentas, bajas de E3b (pedido, ingreso, proceso y participantes), avisos, métodos, inventario y OpenAPI **45/45**; Application **441/441**; arquitectura **154/154**; build 0 advertencias. Commit `feat(plataforma): iniciar bajas con gracia`.
- [x] Textos finales del aviso (lienzo v85, aprobados por el usuario): «La plataforma inició la baja de tu cuenta»; la variante de cuenta suspendida no dice que se cerraron las sesiones, no tiene botón y suma «Si creés que es un error, escribí a {correo de soporte}» cuando el correo de soporte está cargado. Va con la ampliación de T05.

### T14 · Recuperación pública y modelo persistido

Archivos: AccountRecoveryRequest/Status/errors/config/migración/reader/repo; Recovery service/validators/options; AccountRecoveryController; tests RecoveryPublic/RecoveryPrivacy/OTP.

- [x] Rojo de dominio: `LoginCodeTests` y `AccountRecoveryRequestTests` no compilaban sin `AccountRecovery`, `BindRecoveryNonce`, `VerifyRecovery` ni `IsVerifiedRecovery` (`t14-red-domain.log`). Rojo de integración con la tabla ya creada: `AccountRecoveryTests` **0/3**, las rutas no existían (404). Cubre: misma respuesta (código y cuerpo) para un correo libre y uno de otra cuenta; a ese dueño le llega «Ya tenés una cuenta con este correo» y ningún código; el código no mandado falla como uno mal escrito (mismos intentos restantes); otro nonce no gasta intentos; sin verificar no hay pedido; un código registra un solo pedido y el reenvío con la misma clave responde `Idempotent-Replayed`; nunca hay sesión; método de antes desconocido o teléfono en E.164 crean el pedido sin cuenta; mismo correo, los dos métodos, nonce mal formado y formulario (415) se rechazan. Tras la revisión se sumaron: el `/verify` errado y el 429 de reenvío iguales para un correo libre y uno ocupado, el correo de contacto de Google cuenta como ocupado y dos registros simultáneos con el mismo código dejan un 202, un 400 y una sola fila.
- [x] Se reutilizan `LoginCodeIssuer`, `LoginCodeVerifier`, `ILoginCodeChannel` (plantilla «Código para verificar un método») y los límites por destino, con el propósito `AccountRecovery` (sin cuenta) y el hash del nonce en `identity.LoginCodes`. El nonce lo genera el navegador y hace de comprobante: viaja solo en el cuerpo, la base guarda su hash y ninguna respuesta lo devuelve, así que `/verify` responde 204 y el registro exige el código verificado con ese nonce y vigente. `platform.AccountRecoveryRequests` (Pending, método de antes, correo nuevo, cuenta encontrada o null, índice único por código) con `AccountRecoveryService` (7 dependencias de negocio, una UoW por paso; la verificación guarda los intentos fallidos) y `AccountRecoveryMessenger`. Avisos `RecoveryReceived` y `RecoveryMethodInUse` con los textos del tablero de Mensajes; lo que va al correo nuevo sale en la cultura por defecto y sin cuenta en el outbox. Revisión de solo lectura sin críticos; se corrigieron: correo ocupado con `FindVerifiedUsersByEmailAsync` (correo o contacto de Google verificados, como el resto), código buscado por su nonce para que otro pedido al mismo correo no corte el flujo, ventana para registrar contada desde la verificación, `previousEmail` vacío tratado como ausente.
- [x] Verde: `AccountRecoveryTests` **5/5** con `LoginCodeConcurrencyTests` **2/2**; dominio **269/269**; Application **441/441**; arquitectura **154/154**; integración de códigos, registro, avisos, plantillas, métodos, límites, inventario y OpenAPI **74/74**; persistencia (migraciones, modelo, versión, RLS, aislamiento, índices) **22/22**; build 0 advertencias. El contrato solo agrega las tres rutas y sus tres esquemas. Commit `feat(recuperacion): recibir solicitudes verificadas`.

### T15 · Revisar recuperación y limpieza de baja

Archivos: PlatformRecoveryService/Controller, Recovery writer, AccountRecoveryReader, Infrastructure/Identity/Deletion/RecoveryDeletionParticipant; tests RecoveryDecision/RecoveryDeletion.

- [x] Rojo: dominio sin `Approve`/`Reject` ni campos de decisión (no compilaba, `t15-red-domain.log`); integración **0/4** con la tabla ya migrada: rutas 404, sin aviso a la cuenta y 4 participantes de baja en lugar de 5. `PlatformRecoveryTests` cubre: listado con métodos enmascarados, búsqueda y conteos; motivo vacío 400; aprobar deja el correo nuevo verificado y principal (uno solo), SecurityEvent `AccountRecoveryApproved` con la cuenta y el motivo, aviso «Recuperaste tu cuenta» al correo nuevo, sin sesión, repetir da 409 y la persona entra con el correo nuevo; sin cuenta solo se rechaza (aprobar 409 `NoAccount`, rechazar 204 con evento sin destino y aviso «No pudimos recuperar tu cuenta»); correo nuevo que ya es de otra cuenta 409 `MethodInUse`; sin sesión 401, Consumer 403 y operador sin `recoveries.manage` 403.
- [x] `PlatformRecoveryService` (lectura) y `PlatformRecoveryDecisionService` (8 dependencias de negocio, abre su UoW): pedido con versión (vieja: 409), lock de la cuenta y del destino, correo verificado de otra cuenta 409, correo sin verificar de otra cuenta retirado como en Mi cuenta, método nuevo verificado y principal con la regla de dominios verificados, copia del correo principal, evento y aviso; rechazar no toca métodos. `PlatformRecoveryNotifier` avisa la decisión; `AccountRecoveryMessenger` suma el aviso «Alguien pidió recuperar tu cuenta» a los métodos que siguen andando de la cuenta encontrada, con el correo de soporte (decisión del usuario del 03/10 vía la Guía; texto provisorio hasta el tablero de Mensajes). `RecoveryDeletionParticipant` borra los pedidos de una cuenta eliminada (cierra y purga) en el tramo global.
- [x] Verde: recuperaciones **3/3**, participantes de baja **1/1**, recuperación pública **5/5**; dominio **270/270**; Application **441/441**; arquitectura **154/154**; integración de inventario, OpenAPI, avisos, plantillas, migraciones, bajas, cuentas y métodos **83/83**; build 0 advertencias. Commit `feat(recuperacion): revisar pedidos y cerrar los de cuentas eliminadas`.

### T16 · Gestión de operadores

Archivos: PlatformOperatorService/guard/repo/controller cuentas e invitaciones; tests PlatformOperators/LastOperatorAdministrator.

- [x] Rojo: Application **11/13** (`ConnectServiceTests`: un operador elegía Consumer o Business y se le creaba el Personal), `PlatformOperatorTests` **0/3** (rutas 404) y `InvitationAcceptanceTests` del operador **0/1** (aceptaba la invitación). Cubren: invitar con permisos de a uno (201, reenvío idempotente), el invitado aparece enseguida en Cuentas como operador sin Personal y con los permisos en el orden del catálogo, correo verificado y principal, evento `PlatformOperatorInvited` con el diff de claves y sin el correo, aviso de invitación, entra con código a Plataforma y Consumer o Business responden `access_denied` con `Tenancy.Access.PlatformOnly` sin crearle Personal; correo con cuenta previa 409 con «Ese correo ya tiene una cuenta. Un operador no puede tener perfiles de persona.»; permisos vacíos, desconocidos o repetidos 400; operador sin `operators.manage` 403; cambiar permisos con la versión de la asignación (vieja 409), sobre sí mismo 403, quitar el operador con motivo y su evento.
- [x] `PlatformOperatorInvitationService` (7 dependencias de negocio) crea con `PlatformOperatorProvisioner` la identidad exclusiva (cuenta operadora, correo verificado como principal, permisos; cultura y zona de los datos de referencia, sin Personal), bajo el lock del destino del ingreso. `PlatformOperatorService` (5) cambia los permisos o quita el operador con la versión de la asignación (expuesta como `operatorVersion` en la ficha), nunca sobre sí mismo, y llama a `PlatformOperatorGuard` cuando el cambio deja sin `operators.manage`; quitar borra la asignación, desmarca la cuenta y cierra sus sesiones. Los permisos rigen por pedido, sin caché. `ConnectService` responde `Tenancy.Access.PlatformOnly` a un operador en Consumer y Business (no se le crea Personal) y la aceptación de una invitación de organización por un operador da `Invitations.Invitation.PlatformOperator`.
- [x] Verde: operadores **3/3**, aceptación del operador **1/1**, Application **443/443**, dominio **270/270**, arquitectura **154/154**; integración completa en la tabla de registro. Commit `feat(plataforma): administrar operadores`.

### T16b · La invitación de un operador vence (agregada el 2026-10-04)

Fuente: [ADR 0042](../decisions/0042-operadores-de-plataforma.md), lienzo v90 (front `b9a4cf2`), Cuentas, M-Cuentas y Mensajes › «Invitación como operador». Corrige lo que entregó T16, que crea la cuenta activa en el momento. La programa la consolidación del back, después de su primera tanda, para no pisar `Notifications.resx`.

- [x] Rojo:
  - la invitación queda pendiente hasta el primer ingreso con código a Plataforma y vence a los 7 días;
  - mientras está pendiente no hay sesiones, y vencida no se puede entrar;
  - «Reenviar invitación» renueva los 7 días y manda el correo de nuevo;
  - «Revocar invitación» la borra y libera el correo;
  - en Cuentas (y en el filtro Estado), «Invitación pendiente» y «Invitación vencida», cuyo ⋮ trae solo esas dos acciones;
  - los permisos y el último administrador siguen como en T16.

  Evidencia: con solo el andamiaje (valores de enum, códigos de error y la columna), Application **6/7** (`PlatformPermissionServiceTests`: la invitación pendiente tenía permisos) e integración focal **19/26**: `PlatformOperatorTests` 2/6 (la invitada quedaba Active, la respuesta no traía `expiresAtUtc`, suspender una pendiente daba 204 y no había rutas para reenviar ni revocar), `GoogleLoginTests` (Google entraba y se vinculaba a la invitación pendiente), `PlatformAccountStateTests` (una invitación pendiente con `operators.manage` contaba: suspender al último administrador daba 204) y `ExplicitRouteInventoryTests` (rutas ausentes). Comando: `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/... -- --filter-class <clase>` por cada clase.
- [x] El aviso `OperatorInvited` suma quién invita y los permisos por nombre, con los textos de v90:
  - asunto: «{quien invita} te invitó como operador de {app}»;
  - título: «Te invitaron como operador»;
  - datos: Permisos y Vence;
  - botón «Ingresar»;
  - las dos notas;
  - sale solo por correo.

  `PlatformOperatorInvitationNotifier` lo encola solo en el canal de correo, en la cultura y la zona de la cuenta invitada; el renderer usa la plantilla de las invitaciones (datos en grilla, botón, notas y pie). Los permisos salen por nombre en el idioma del aviso (`PermissionTexts.PermissionIn`), unidos con «, » y «y».
- [x] Verde, y `docs/features/plataforma.md` deja de decir «crea enseguida la identidad». Commit `feat(plataforma): la invitación de operador vence` (`8604c94`).

  La cuenta guarda el vencimiento (`OperatorInvitationExpiresAtUtc`, migración `OperatorInvitationExpiry`, una columna nula sin cambio de datos). `PlatformOperatorInvitationService` invita, reenvía (`POST /api/platform/operators/{id}/invitation/resend`, 200 con `id` y `expiresAtUtc`) y revoca (`DELETE /api/platform/operators/{id}/invitation`, 204), con `PlatformOperatorProvisioner` bajo el lock de la cuenta. `LoginAccountGuard` acepta la invitación con el primer ingreso con código a Plataforma; `LoginCodeRequester` no manda el código a una vencida; `GoogleAccountGuard` la rechaza. Cuentas suma los estados `Invited` e `InvitationExpired` y `invitationExpiresAtUtc`; una revocada no figura. Decisiones 16 a 22 de la consolidación.

  Verde: Application **451/451**, dominio **272/272**, arquitectura **141/142** (falla `HarnessTests`: `formularios.md:48` del front nombra `useEditPage.test.tsx` y `SettingsPage.test.tsx` sin la marca (E#) desde el commit del front `a6ced5c`; no es de esta tarea), integración focal **26/26** (`PlatformOperatorTests` 6/6, `GoogleLoginTests`, `PlatformAccountStateTests`, `ExplicitRouteInventoryTests`) y relacionada **62/62** (ingreso con código, OpenAPI, avisos, migraciones, Cuentas, bajas, auditoría, identidad e invitaciones); `dotnet build` de la solución con 0 advertencias; `dotnet test` completo con Docker **1564/1565** (7m 41s), con la misma única falla de `HarnessTests`.

### T17 · Auditoría de seguridad

Archivos: ISecurityEventReader/SecurityEventReader, PlatformAuditService/controllers/query; traducciones Audit/recursos; tests PlatformSecurityAudit.

- [x] Rojo en worktree sobre `c4bfc9d` con el test nuevo: `PlatformSecurityAuditTests` **0/3** (la ruta no existía, 404 donde se esperaba 401). Cubren: tres acciones sobre una cuenta salen de la más nueva a la más vieja de a dos con cursor estable; «Suspendió la cuenta», rol «Operador», la cuenta como destino con el correo enmascarado y el motivo; filtro por tipo; detalle por Id; ningún correo completo en la respuesta; los ingresos salen de LoginAudits («Ingresó con un código por correo» y «No pudo ingresar: El código no es válido.»), sin IP, sin el código ni el correo; sin sesión 401, Consumer 403, operador sin `audit.read` 403, cursor o tipo inválidos 400 y un Id inexistente 404.
- [x] El contrato del SecurityEvent ya estaba (R04). `SecurityEventReader` une en SQL las claves (fecha e Id) de SecurityEvents y LoginAudits (UNION ALL) y pagina con el `ToCursorResultAsync` existente; después completa la página con los nombres actuales. El tipo «Ingresos» lee LoginAudits con método, resultado y código de error traducido, sin dirección ni código OTP.
- [x] Nombres actuales por subconsulta; una cuenta eliminada se muestra «Cuenta eliminada» (traducida al leer) y una sin nombre, con su correo enmascarado. Sin totales. Listado `GET /api/platform/audit` y detalle `GET /{id}` separados; el filtro por organización va en SQL (`TargetTenantId`), lo mismo el de cuenta. Índices nuevos (OccurredAtUtc, Id) en las dos tablas (migración `AuditCursorIndexes`).
- [x] Verde: auditoría **3/3**; dominio **270/270**; Application **443/443**; arquitectura **154/154**; inventario, OpenAPI, migraciones, modelo, cursor y auditoría de organización **28/28**; build 0 advertencias. Commit `feat(plataforma): consultar auditoría de seguridad`.

### T18 · Versiones legales inmutables

Archivos: PlatformLegalService/writer, LegalDocument/Content/repo/reader, contracts/controller/validators; tests PlatformLegalPublication y LegalAcceptance.

- [x] Rojo: `PlatformLegalPublicationTests` **0/2** con el tipo de evento ya agregado (la ruta no existía, 404). Cubren, en bases aisladas: publicar una versión futura (201, número 2, reenvío idempotente con el mismo Id) que todavía no rige ni bloquea, el listado con la vigente y todas las versiones, el detalle con el texto en cada cultura, el evento `LegalDocumentPublished` con el operador, el motivo fijo y el diff sin el texto; publicar una vigente hoy obliga a aceptarla (403 `Legal.AcceptanceRequired`); falta una cultura, texto vacío, cultura desconocida, fecha anterior a hoy o tipo inválido 400; dos publicaciones simultáneas reciben las versiones 2 y 3; sin `legal.manage` 403 y sin sesión 401.
- [x] `PlatformLegalService` (6 dependencias de negocio) agrega la versión y sus textos bajo el lock global del tipo (`global:legal-document:{tipo}`), con número = último + 1; nunca actualiza ni borra (el runtime solo tiene SELECT e INSERT). La vigencia es el comienzo de la fecha civil elegida en la zona del operador; si es hoy, rige desde el momento de publicar. Exige el texto una vez en cada cultura habilitada (de los datos de referencia) y una fecha de hoy en adelante. El evento `LegalDocumentPublished` lleva tipo, versión y vigencia, con un motivo fijo de los recursos; el texto no va a logs ni a la auditoría. `GET /api/platform/legal-documents` lista por tipo la vigente y todas las versiones; `GET /{id}` muestra una con sus textos; no hay edición de versiones.
- [x] Verde: publicación **2/2**; aceptación legal de la 3b, `/api/me`, inventario, OpenAPI, auditoría, repositorios legales y locks **34/34**; dominio **270/270**; Application **443/443**; arquitectura **154/154**; build 0 advertencias. Commit `feat(plataforma): publicar versiones legales`.

### T18b · Antes de las pantallas: las acciones sobre una cuenta sin 409 falsos (agregada el 2026-10-05)

Lo encontró la consolidación (tanda «versión al guardar», back `4ea3d53`).
- **El problema:** las acciones de Plataforma sobre una cuenta (suspender, reactivar, cerrar sesiones y la baja) comparan el `xmin` de la fila del usuario. Esa fila cambia con cualquier escritura: un ingreso, el reinicio de intentos fallidos, el security stamp o el contacto principal. Por eso un ingreso del usuario mientras el operador mira la ficha da un 409 falso. Es la misma causa que tenía «Tus datos», que se resolvió con `ProfileVersion`.
- **La decisión (la Guía, 2026-10-05):** suspender, reactivar, cerrar sesiones y la baja **no llevan versión**. Las protege el control de estado de cada transición, dentro de la transacción y con el lock de la cuenta: suspender solo una cuenta activa, reactivar solo una suspendida, la baja solo una activa o suspendida sin baja programada. Una acción repetida o contradictoria da el error de estado que ya existía (`Users.User.InvalidTransition`, `Legal.AccountDeletion.AlreadyPending`), no un 409 falso.
- [x] Rojo sobre `3b2f3d1`, con la versión todavía en el cuerpo: `PlatformAccountStateTests` **0/2** nuevos. «Una escritura en la cuenta entre la ficha y la acción»: la persona escribe mal el código y después ingresa (un intento fallido y su reinicio cambian el `xmin`), y suspender con la versión que mostraba la ficha da 409 (`ConcurrencyConflictException`: el falso). «Dos acciones contradictorias a la vez»: dos «Cerrar sesiones» a la vez, y la segunda da 409 de versión. Verde, con el cuerpo `{ reason }`: las cuatro acciones con la fila cambiada entre la ficha y la acción; suspender, reactivar y la baja dos veces a la vez (una gana y la otra recibe `Users.User.InvalidTransition` o `Legal.AccountDeletion.AlreadyPending`); y repetir «Cerrar sesiones» no choca.
- [x] Sin `version` en las cuatro acciones: el cuerpo es `{ reason }` (`PlatformAccountReasonHttpRequest` → `PlatformAccountReasonRequest`, con su validador: el motivo es obligatorio). `PlatformAccountStateService` y `PlatformAccountDeletionService` ya no comparan `account.Version`; el estado lo controlan el dominio (`Suspend` exige Active, `Reactivate` exige Suspended), el servicio (una cuenta eliminada) y la baja (sin una programada y Active o Suspended), sin errores ni textos nuevos. Recuperaciones y quitar a un operador siguen con `PlatformAccountActionRequest`: su versión es la de su propia fila (el pedido, la asignación de permisos), que un ingreso no cambia. Un cliente viejo que mande `version` no falla: el campo se ignora. OpenAPI regenerado: las cuatro rutas usan `PlatformAccountReasonHttpRequest`; el front regenera sus tipos en T19. La `version` del listado y de la ficha de cuentas (el `xmin` de la fila) queda sin consumidor; retirarla cambia la lectura y lo decide la Guía.
- [ ] Lo mismo para las demás acciones de Plataforma con motivo que hoy responden 204: organizaciones, página pública, dominios y recuperaciones. Responden como los guardados (`VersionResponse`) si llevan versión. No entraba en esta entrega, que cubre solo las cuatro acciones sobre una cuenta.
  - La Guía lo decidió el 2026-10-05: las 11 acciones `POST` con versión responden 200 con `VersionResponse`, y la cuenta deja de exponer su `version`.
  - **Queda conocido, sin tarea:**
    - **El problema:** hay escritores de la fila de la cuenta que no toman su lock; por ejemplo, `ConnectService` recuerda la última organización de empresa. Si uno confirma justo entre la lectura y el `UPDATE` de una de las cuatro acciones, EF responde `General.ConcurrencyConflict`.
    - **Por qué no se arregla ahora:** es una ventana de milisegundos, ya existía antes de T18b y se resuelve reintentando.
    - **Cómo se cerraría:** que esos escritores tomen el mismo lock, o que guarden sin el token de concurrencia. Sería una tarea aparte.
  - Pasa a **T18c** del [plan de las pantallas](2026-10-05-etapa-5-pantallas.md#t18c--back--toda-acción-con-versión-responde-la-versión-nueva-la-primera-tarea).
- [x] La regla, en `concurrencia.md`: las transiciones de estado sin formulario sobre una fila que otras escrituras también cambian se protegen con el estado, no con la versión (la excepción, lo prohibido y «Lo verifica» con los dos tests, (E7)); y `docs/features/plataforma.md` sin la versión en las cuatro acciones.
- [x] Verde: cuentas, bajas, operadores, auditoría, inventario y OpenAPI **27/27**; arquitectura **148/148** (con `ControllerInputContractTests` y `VersionedContractTests`); Application **476/476**; build de la solución 0 advertencias y `dotnet test` completo con Docker **1659/1659** (dominio 286, Application 476, arquitectura 148, integración 749). Commit `fix(plataforma): suspender, reactivar, cerrar sesiones y la baja sin versión` (`6bd4ff7`).

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
- [ ] Motivo obligatorio en los diálogos; la versión solo donde la acción la lleva (cambiar o quitar los permisos de un operador y las recuperaciones), no en suspender, reactivar, cerrar sesiones ni la baja (T18b). Fechas mediante format, errores/409 conservan datos. No editar identidad desde Plataforma si el tablero no lo pide.
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
18. El único Dueño que bloquea la baja iniciada por Plataforma sigue ADR 0035: cualquier organización no cerrada (PendingApproval, Provisioning, Active o Suspended) y aunque la cuenta esté suspendida. Mi cuenta y la baja de la persona conservan su regla de E3b (organizaciones activas); la diferencia queda para la consolidación, que unifica las reglas duplicadas.
19. Suspender, reactivar y cerrar sesiones no tienen aviso a la persona: el tablero no lo muestra y no existe una variante previa. La baja por Plataforma sí avisa por todos los métodos, con los textos del lienzo v85.
20. T14, recuperación pública: el comprobante entre verificar y registrar es el nonce que genera el navegador (su hash queda en el código), no un ticket emitido por el servidor; así ninguna respuesta lleva un secreto y las tres rutas pueden ser idempotentes sin guardar secretos en `IdempotencyKeys`. Las rutas usan las políticas por IP del ingreso por código (`login-code` para pedir, `login-verify` para verificar y registrar). El método de antes puede ser correo o teléfono (E.164 con el parser); el nuevo, solo correo hasta la Etapa 8. El registro no vuelve a revisar si el correo nuevo quedó ocupado entre el código y el pedido: lo resuelve la aprobación (T15). La protección contra formularios de otro sitio es exigir JSON (415): el flujo no emite sesión ni usa cookies, como el ingreso por código. Con un correo ocupado el código se guarda (solo su hash) pero no se manda y no sirve: así los límites, los intentos y la respuesta son iguales, que es lo que pide ADR 0041; su línea de tests («no se emite ningún código») se lee como «no se manda ni se puede usar» y queda para que la consolidación ajuste la redacción. Se acepta la diferencia de milisegundos de leer la cultura del dueño, como en `LoginCodeRequester`. El pedido público no deja SecurityEvent (no hay actor ni motivo); lo dejan las decisiones del operador en T15. Queda como pregunta de producto para T15 si al dueño del método de antes le llega un aviso cuando alguien registra un pedido: el tablero de Mensajes no lo tiene.
21. Decisión del usuario del 03/10 (vía la Guía, `52fda79`): la regla de única Dueña de la baja por Plataforma (organizaciones no cerradas, incluidas las suspendidas) queda como está; la baja que pide la persona se unifica en la consolidación (D5). La decisión de operadores es ahora la ADR 0042.
22. T15, recuperaciones: las rutas siguen la forma de Organizaciones y Cuentas (`/paged` y `/filter-counts`). Aprobar se permite con la cuenta Active, Suspended o PendingDeletion: suma el método y el estado de la cuenta sigue mandando en el ingreso; no cierra sesiones. Al operador sí se le dice que el correo nuevo ya es de otra cuenta (409), como en Cuentas, que ya las muestra. La decisión reutiliza `PlatformAccountActionRequest` (versión y motivo). Al eliminar una cuenta, sus pedidos se borran con sus datos (cerrar y purgar en un solo paso); los SecurityEvent de las decisiones se conservan. Queda abierto cuánto se guardan los pedidos sin cuenta o rechazados: ningún participante los alcanza.
23. T16, operadores (ADR 0042): la invitación crea enseguida la identidad, como muestra el tablero Cuentas («Invitación enviada»), y el operador acepta entrando con código a `/plataforma`; no hay enlace ni pantalla de aceptación. **Corregido el 04/10 por el usuario (ADR 0042, lienzo v90):** la invitación queda pendiente hasta el primer ingreso y vence a los 7 días; Cuentas la muestra como «Invitación pendiente» o «Invitación vencida», con «Reenviar invitación» y «Revocar invitación», y el correo dice quién invita, los permisos y el vencimiento. Falta programarlo. El correo queda verificado al invitar porque lo da un operador de confianza, y el primer ingreso con código prueba la posesión. Un correo que ya es de cualquier cuenta (método de correo, verificado o no, o contacto de Google) se rechaza. El rechazo a un operador en una organización se hace al aceptar la invitación: rechazarla al invitar le diría a la organización que ese correo es de un operador (ADR 0041). Quitar el operador deja la cuenta sin acceso de operador y con las sesiones cerradas; si después entra como persona, ya no es operador. La invitación no pide motivo en el tablero: el evento usa un motivo fijo de los recursos. El aviso de invitación tiene texto provisorio hasta el tablero de Mensajes. Con los permisos de `operators.manage`, la regla del último administrador solo se alcanza en carreras; el lock global la serializa.
24. T17, auditoría: los filtros siguen el tablero (período 0/7/30/todo, tipo Ingresos/Organizaciones/Cuentas/Operadores/Configuración, organización y búsqueda libre por motivo y nombres) y se suma el de cuenta, que pedía el plan como actor u objetivo. «Hoy» es el día del operador en su zona horaria. No hay total ni conteos por filtro, como fija el plan («no total de tabla creciente»), aunque el tablero los dibuja. El tablero muestra correos completos y un método «autenticador» que no existe: se muestran el nombre o el correo enmascarado, y los métodos reales. El detalle muestra los mismos campos que el tablero, sin el diff técnico. El rol sigue el tablero: «Operador», «Automático» y «Persona». En los ingresos, quién y sobre qué son la misma cuenta.
25. T18, documentos legales: el tablero publica sin motivo, así que el evento usa uno fijo de los recursos. «Vigente desde» es una fecha civil; el back la toma en la zona del operador y, si es hoy, la versión rige desde el momento de publicarla (el comienzo del día podría quedar antes que la vigente y la versión no regiría nunca). No se aceptan fechas anteriores a hoy. Entre versiones pendientes rige la de vigencia más reciente ya alcanzada y, a igual vigencia, el número mayor, como ya leía la aceptación de la 3b. El texto de cada cultura admite hasta `TextLimits.LegalDocument` (100.000 caracteres). Publicar una versión que rige hoy bloquea también al operador hasta que la acepte, como a cualquier cuenta.
26. Revisión de solo lectura de T15 a T18 (04/10). Corregido: una cuenta de operador no se recupera desde Recuperaciones (409 `Platform.AccountRecovery.OperatorAccount`), porque quien solo tiene `recoveries.manage` podría quedarse con la cuenta de un operador con más permisos; «Registrá tu empresa» y el registro con el correo de un operador responden `Tenancy.Access.PlatformOnly` (ADR 0042); la vigencia de una publicación de hoy se lee con el lock tomado; un correo que ya es de la misma cuenta tiene su propio error. Aceptado y anotado: invitar operador y aprobar una recuperación toman solo el lock del destino, igual que agregar un correo en Mi cuenta, y no el de Google (`google:email:`), así que un alta con Google simultánea con el mismo correo podría dejar ese correo como contacto de otra cuenta; queda para la consolidación junto con Mi cuenta. El aviso a la cuenta cuando se registra un pedido agrega trabajo solo si la cuenta existe (una diferencia de tiempo mayor que la de la decisión 20): queda como riesgo a medir en la consolidación o a pasar a un trabajo diferido. Pregunta de producto abierta: avisar a los métodos que ya tenía la cuenta cuando se aprueba una recuperación (hoy solo se avisa al correo nuevo, como dice el tablero).
27. T18b, decisión de la Guía (05/10): suspender, reactivar, cerrar sesiones y la baja de una cuenta no llevan versión. La fila de la cuenta cambia con un ingreso, los intentos fallidos, el sello de seguridad y el contacto principal, y su `xmin` daba un 409 falso; las protege el estado de cada transición, bajo el lock de la cuenta (la misma idea que `ProfileVersion` de «Tus datos», sin una columna más). Una acción repetida o contradictoria recibe `Users.User.InvalidTransition` o `Legal.AccountDeletion.AlreadyPending`; cerrar sesiones no cambia el estado y repetirla no choca. El cuerpo es `{ reason }` (`PlatformAccountReasonHttpRequest`); las recuperaciones y quitar a un operador conservan `PlatformAccountActionRequest` con la versión de su propia fila. Quedan abiertos: la `version` del listado y de la ficha de cuentas, que ya no usa ninguna acción (retirarla cambia la lectura), y las demás acciones con motivo de Plataforma (segundo ítem de T18b).

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
| T12 | Suspender, reactivar y cerrar sesiones con el último administrador de operadores protegido; 3/3 + 20/20, Application 441/441, arquitectura 154/154 | `f487ed1` | — |
| T13 | Baja por Plataforma con gracia, aviso provisorio y bloqueos de único Dueño y último operador; 3/3 + 45/45, Application 441/441, arquitectura 154/154 | `a9a0a8c` | — |
| T14 | Recuperación pública: código atado al nonce, misma respuesta con correo ocupado, pedido Pending; 5/5 + 74/74 + 22/22, dominio 269/269, Application 441/441, arquitectura 154/154 | `aa43e8b` | — |
| T11 fix | Búsqueda de cuentas por métodos de ingreso, sin la copia del correo; 3/3 | `750b8a3` | — |
| T05 + T13 | Correos de contacto con lectura pública y textos finales del aviso de baja por Plataforma; 3/3 + 64/64 | `edb8415` | — |
| T15 | Revisión de recuperaciones, aviso a la cuenta y cierre en la baja; 3/3 + 1/1 + 5/5 + 83/83, dominio 270/270, Application 441/441, arquitectura 154/154 | `6fee6ce` | — |
| T16 | Operadores: invitación, permisos, exclusividad de Plataforma y bloqueo en invitaciones; 3/3 + 1/1, Application 443/443, arquitectura 154/154, integración completa 671/671 | `c4bfc9d` | — |
| T17 | Auditoría de seguridad por cursor con los ingresos; 3/3 + 28/28, dominio 270/270, Application 443/443, arquitectura 154/154 | `3e0a646` | — |
| T18 | Versiones legales inmutables con lock por tipo; 2/2 + 34/34, dominio 270/270, Application 443/443, arquitectura 154/154 | `1dfb159` | — |
| Seed de invitaciones | El ayudante de `InvitationAcceptanceTests` siembra antes de crear documentos de prueba (los 4 rojos que dependían del orden); 29/29 | `c1ce7f6` | — |
| Revisión T15–T18 | Aprobar una recuperación sobre una cuenta de operador da 409; un operador no crea organizaciones por «Registrá tu empresa» ni pasa el registro con su correo; la vigencia legal de hoy se calcula con el lock; mensaje propio cuando el correo ya es de la misma cuenta. Rojo 0/2, verde 20/20 | `9b2b0e3` | — |
| Cierre del back (T10–T18) | `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker: **1545/1545** (dominio 270, Application 443, arquitectura 154, integración 678). El front de la etapa (T19–T25) sigue después de la consolidación | este commit | — |
| T18b | Suspender, reactivar, cerrar sesiones y la baja sin versión, protegidas por el estado bajo el lock de la cuenta: rojo 0/2 (409 falso por un ingreso y por dos pedidos a la vez) → verde; cuentas, bajas, operadores, auditoría, inventario y OpenAPI 27/27, arquitectura 148/148, Application 476/476; build 0 advertencias y suite completa **1659/1659** (dominio 286, Application 476, arquitectura 148, integración 749). Queda abierto el segundo ítem (las demás acciones con motivo) | `6bd4ff7` | — |

No declarar cierre a partir de esta tabla sin completar T25.
