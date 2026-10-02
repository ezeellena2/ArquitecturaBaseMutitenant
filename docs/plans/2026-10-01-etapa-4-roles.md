# Etapa 4 · Autorización y Roles — plan de implementación

> **Estado:** back y front autorizado listos; esperando aprobación explícita de los tableros de Organización para iniciar las pantallas de Roles. Trabajo en `main`, en este chat y sin push. La puerta general y el E2E real están verdes. `HarnessStage` permanece sin subir hasta aprobar e implementar T10.

## Objetivo y límites

Implementar autorización por permisos y roles como área de referencia, proteger al único Dueño durante la baja de una cuenta, llevar los componentes compartidos y `/cuenta` al Criterio validado de la versión 60, y demostrar el contrato y el recorrido de roles con el navegador, la API y una base aislada reales.

Se programan solo los siete tableros validados `Criterio-*` y los tableros validados de Perfil personal para `/cuenta`: `Cuenta.dc.html`, `M-Cuenta.dc.html`, `Cuenta-Agregar.dc.html`, `Cuenta-Verificar.dc.html` y `Cuenta-Baja.dc.html`. Las pantallas `Roles`, `Rol`, `M-Roles`, `M-Rol` y su entrada del AdminPanel pertenecen a Organización, que está «para aprobar». Se completa el back y se deja todo listo para esas pantallas, pero no se dibujan ni programan antes del visto bueno del usuario.

No se crean cuentas, espacios personales ni organizaciones de ejemplo en Development. Los tests y el E2E preparan sus propios datos en sus bases aisladas. El seed global conserva únicamente el operador.

## Decisiones tomadas

- Los Roles y RoleAssignments son privados de cada organización y llevan RLS. `SystemRoles` define las plantillas inmutables; cada organización de prueba recibe esas filas y el Dueño se asigna por `RoleAssignment`. El seed global no inventa un `TenantId` ni crea organizaciones. El provisionador de Etapa 6 reutilizará la misma preparación al nacer una organización real.
- `TenantAdmin` es la única fuente de verdad para Dueño; `Member` no recibe un flag. La proyección global de accesos puede reflejar el rol para Mi cuenta y la protección de baja, pero siempre se mantiene desde el `RoleAssignment` dentro de la misma UoW.
- `Role.CompanyId` y `RoleAssignment.CompanyId` permanecen como GUID opcionales sin FK durante E4; `Company` todavía no existe en el dominio y su FK se agregará en E6. E4 no expone asignación a empresas.
- La unicidad del nombre de rol se aplica por tenant sobre `NormalizedName`, que recorta espacios, normaliza Unicode Form C y usa mayúsculas invariantes; la base cierra duplicados concurrentes sin hacer equivalentes los acentos.
- La ficha apunta a `../ArquitecturaBase/src/ArquitecturaBase.Domain/Authorization/Role.cs`, pero esa entidad no existe allí: solo hay `RoleErrors`, `Permissions` y `SystemRoles`, con roles administrados por Identity en Infrastructure. `Role`, `RoleScope` y `RoleAssignment` se definen según el modelo local multitenant (`ITenantOwned`, `IVersioned`, RLS); la base sirve para copiar los errores, catálogo y patrones de servicio/autorización.
- El rol activo de `/api/me` se incorpora al índice de perfiles de organización que ya reserva `OrganizationSummary.RoleName`; no se leen Members ni RoleAssignments fuera de su RLS. Se actualiza en la misma transacción que cambie la asignación.
- El encabezado ordenable conserva `aria-sort` para anunciar el orden a tecnología asistiva. El tablero Criterio no dibuja una flecha de orden, así que no se agrega un indicador visible.
- Las reglas compartidas de Criterio se verifican con pruebas colocalizadas de componentes; las ocho capturas app/lienzo v60 comparan `/cuenta` y sus diálogos en escritorio y teléfono. Los demás detalles visuales del lienzo que no cambian un flujo o seguridad se anotan en backlog.
- `HarnessStage` solo sube a 4 en ambos repositorios cuando, después de la aprobación, también estén implementadas las pantallas de roles y hayan pasado todas las puertas de cierre.
- `ApiFactory` eleva los límites de códigos de ingreso solo en el host de integración porque todos los TestServer comparten la IP; las pruebas específicas de rate limit fijan sus propios límites bajos. La configuración de producción no cambia.
- El E2E respeta `retryAfter` al pedir reautenticación del dueño; la última asignación de Dueño bloquea el `POST /api/me/deletion` con `Legal.AccountDeletion.LastAdmin` antes de consumir el comprobante.

## Preparación y secuencia TDD

### T00 · Plan

- [x] Revisar plan maestro, arquitectura multitenant/backend, fichas de dominio, reglas del lienzo v60 y AGENTS de los directorios aplicables.
- [x] Revisión del plan completada; el commit aislado usa `git add -- docs/plans/2026-10-01-etapa-4-roles.md` y `git commit -m "docs: planificar etapa 4 de roles"` antes de cualquier cambio de código.

### T01 · Bajar los siete Criterio a las fichas del front

- [x] Actualizar `ArquitecturaBaseMutitenantFront/docs/rules/pantallas-y-ui.md`, `formularios.md` y `paginado-y-listados.md` para que describan los tableros Criterio v60: tamaño compacto; banda y encabezados arena; filas alternadas; estados en segunda línea sin badges; cajas por tema y campos del ancho del dato; errores/toasts según tipo; estados habilitados de Guardar/Descartar; acciones en ⋮; panel AdminPanel; paginado al pasar 10 y tabs solo para dos o más tablas.
- [x] Agregar al tema el token `--cab: oklch(0.945 0.018 76)` para encabezados de tabla, caja y diálogo, en `src/index.css` y `docs/architecture/tema.md`; el `theme-tokens.test.ts` existente verifica que esté declarado.
- [x] No ejecutar los generadores ni modificar `canvas.json`: las reglas ya están validadas y esos generadores reescriben el lienzo.
- [x] Para cada regla verificable, añadir primero una aserción conductual/accesible que falle en los tests colocalizados existentes: crear `src/shared/ui/button.test.tsx`; ampliar los tests de `DataTable`, `Pagination`, `RowActions`, `dialog.mobile`, `ConfirmDialog`, `FormField`, fields realmente afectados, `Banner`, `FormError` y `Page` en `src/shared/ui/`; ampliar `src/layouts/PrivateLayoutFrame.test.tsx`, `src/layouts/components/AdminPanel.test.tsx` y `src/layouts/navigation/navigation.test.tsx`. Agregar un test de `dropdown-menu` en `src/shared/ui/` solo si el comportamiento visible del menú no tiene cobertura existente.
- [x] Cubrir reglas comprobables de los componentes: alto/tamaño compacto; nombre accesible y teclado; fila alternada y estado sin pastilla; encabezado ordenable con `aria-sort`; confirmación irreversible sin cierre por ✕ y acción roja; rótulo/required/error del campo; aviso persistente vs transitorio; acciones secundarias dentro del menú; la banda de página y Administración como panel secundario; paginado al superar 10.
- [x] Corregir `button.tsx`, `IconButton.tsx`, `table.tsx`, `DataTable.tsx`, `Pagination.tsx`, `LoadMore.tsx`, `RowActions.tsx`, `dialog.tsx`, `ConfirmDialog.tsx`, `Sheet.tsx`, `input.tsx`, `FormField.tsx`, `fields/*`, `Banner.tsx`, `FormError.tsx`, `sonner.tsx`, `dropdown-menu.tsx`, `Page.tsx`, y los componentes de `src/layouts/` que materialicen cada regla. Mantener todas las dimensiones, colores y copys atados a `docs/design/lienzo/Criterio-*.dc.html` y `docs/architecture/tema.md`.
- [x] Resolver del backlog solo lo que toca un componente/regla afectado por este trabajo: #24, #25, #26, #27, #28, #65, #66, #67, #68, #71 y #72; revisar #2, #3 y #6 si el field correspondiente aparece en `/cuenta`. Mantener en backlog lo no utilizado por este flujo.
- [x] Correr tests dirigidos rojos y verdes por grupo, `npm run lint` y `npm run build` del front; no sumar reglas de arquitectura ni tests nuevos del arnés fuera de T09.

### T02 · Copiar Perfil personal en `/cuenta` y diálogos

- [x] Primero extender `src/areas/personal/account/pages/AccountPage.test.tsx` y los tests colocalizados para exigir composición/orden/copy aprobados en escritorio y móvil, estados de carga/error/vacío y diálogos montar/cerrar; comprobar que el perfil conserva `version`, `Guardar cambios` se habilita solo con cambios y `Descartar cambios` permanece activo.
- [x] Rehacer solo presentación y composición de `src/areas/personal/account/pages/AccountPage.tsx`, `components/AccountProfileForm.tsx`, `LoginMethodsTable.tsx`, `AddLoginMethodDialog.tsx`, `VerifyLoginMethodDialog.tsx`, `ChangeLoginMethodDialog.tsx` y `AccountDeletionDialog.tsx`, copiando `Cuenta`, `M-Cuenta`, `Cuenta-Agregar`, `Cuenta-Verificar` y `Cuenta-Baja`. Conservar los recorridos, API, seguridad, versión, tickets y navegación ya cubiertos por 3b; no agregar acciones, datos ni estados que el lienzo no muestre.
- [x] Alinear los recursos es/en solo cuando el texto existente no coincide con el tablero; mantener `ResourceParityTests`/paridad del front en verde.
- [x] Crear `scripts/capturar-etapa-4.mjs` y su test de verificación, y guardar los ocho pares app/lienzo en `docs/design/capturas/etapa-4/`: `/cuenta` y los estados de los diálogos afectados, 1440×900 y 390×844. Comparar y documentar en el informe las diferencias por Radix/Sonner o datos/funciones de etapas posteriores.
- [x] Verificar que los recorridos reales de la 3b de `/cuenta` siguen verdes en el `test:e2e:real` de cierre.

### T02b · Primer rojo del recorrido real de Roles

- [x] Extender el script E2E con el recorrido E4 contra el navegador, la API y la base aislada reales, sin mocks ni intercepts: login real por `.eml`, catálogo, crear rol, recuperar `version`, actualizar, conflicto por versión obsoleta, protección de roles de sistema y baja bloqueada para el único Dueño.
- [x] Ejecutar `MT_E2E_ONLY_CASE=4:` con Aspire, el navegador y PostgreSQL aislados antes de T03; el recorrido quedó rojo en la consulta inicial del catálogo porque `/api/permissions` aún no existe. El caso completo debe quedar en verde en T08. Evidencia del test: commit frontal `ca48815`.
- [x] No usar pantallas de Roles: siguen pendientes de aprobación y el E2E opera mediante navegador autenticado y API real.

### T03 · Dominio de autorización y roles

- [x] Crear primero tests rojos en `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization/RoleTests.cs` y `RoleAssignmentTests.cs`.
- [x] Implementar en `src/ArquitecturaBaseMultitenant.Domain/Authorization/` `Permissions`, `PersonalPermissions`, `PlatformPermissions`, `Role`, `RoleScope`, `RoleAssignment`, `SystemRoles` y `RoleErrors`; añadir `AGENTS.md` y `CLAUDE.md` a la carpeta nueva.
- [x] `Role` es `ITenantOwned` y `IVersioned`, valida nombre/ámbito/empresa/permisos, y solo usa errores `Roles.Role.*`. `TenantAdmin` y `CompanyAdmin` son inmutables; el Dueño nunca se deriva de Member.
- [x] Cubrir unidades: `Organization`, `AnyCompany`, `SpecificCompany`; empresa requerida/prohibida según alcance; límites/nombres/permisos; estado de rol de sistema y versión.

### T04 · Persistencia privada, migración, sistema de roles y perfiles

- [x] Añadir tests de modelo/migración/RLS/concurrencia antes de implementar configuraciones y repositorios.
- [x] Implementar `RoleConfiguration` y `RoleAssignmentConfiguration` en `Infrastructure/Persistence/Configurations/Tenant/`, puertos en `Application/Interfaces/Persistence/`, y `RoleRepository`/`RoleReader` en Infrastructure; crear migraciones en `Infrastructure/Persistence/Migrations/` con RLS `EnableTenantRls`, índices por campos de `SortMap`, auditoría y token de versión `xmin`.
- [x] Registrar `Role`/`RoleAssignment` en `ApplicationDbContext`; revisar el SQL de las migraciones. Los tests crean tenant y roles dentro de su Testcontainer, prueban aislamiento cruzado, assignment huérfano y concurrencia real.
- [x] Sembrar `TenantAdmin` y `CompanyAdmin` por tenant en la preparación tenant-scoped que reutilizará la Etapa 6; asignar `TenantAdmin` al Dueño de la organización. No tocar la seed global para introducir una organización.
- [x] Actualizar el índice `identity.UserTenantAccesses` y su reader/configuración/proyección para incluir el rol de sistema derivado del `RoleAssignment`, sincronizado por triggers transaccionales; ampliar tests del índice y `/api/me`. Ningún endpoint del front ve un flag de Dueño en `Member`.
- [x] Revisar `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/`, `Tenancy/` y la migración: RLS bloquea acceso cruzado incluso sin filtro EF; las rutas nuevas se agregarán en T05.

### T05 · Contratos, validación y respuestas HTTP

- [x] Agregar primero tests de contrato rojos para paginado/filtros/conteos, grupos de permisos, lectura con versión, creación 201 y PUT/DELETE con versión obligatoria y 409.
- [x] Añadir `Application/Models/Roles/` (`RoleResponse`, requests paginadas y de escritura, `PermissionGroupResponse`), validadores bajo `Application/Validation/Roles/`, errores y recursos `Errors.resx`/`Errors.en.resx` y `Permissions.resx`/`Permissions.en.resx`; crear los `AGENTS.md`/`CLAUDE.md` que exija cada carpeta nueva.
- [x] Exponer contratos en `Api/Contracts/Organization/` y controller fino en `Api/Controllers/Organization/RolesController.cs`: listado paginado con filtros/conteos, detalle con `version`, creación 201, actualización y baja con `version`; POST idempotente. Declarar `[Access(Business)]` y los permisos correspondientes, sin ruta de negocio anónima.
- [x] Generar el OpenAPI del back, sincronizar `ArquitecturaBaseMutitenantFront/docs/contracts/openapi.json` y `src/shared/api/generated/`, y crear `src/test/roles-contract.test.ts`: el test lee los contratos del back, el OpenAPI y el esquema del front.

### T06 · RoleService y autorización

- [x] Escribir primero tests rojos de `RoleServiceWriteTests.cs` y `RoleServiceTests.cs`; cubrir Result/error, transacción única, concurrencia y caché después del commit.
- [x] Implementar `IRoleService`, `RoleService`, `PermissionService` y `PlatformPermissionService` en Application. Una sola UoW por método público de escritura; validación afuera; invalidación de caché después del commit; listado filtrado/paginado, catálogo ordenado y roles de sistema protegidos.
- [x] Implementar `HasPermission`, `HasCompanyPermission`, `HasPlatformPermission`, requirements/handlers y provider en `Api/Authorization/`; cubrir autorización, acceso incorrecto, empresa ajena, permiso inexistente y ProblemDetails.
- [x] Actualizar `AccessDeclarationTests`, inventario HTTP/OpenAPI, `ServiceDependencyCountTests`, `ResourceParityTests` y los tests de invalidación pertinentes; no crear una nueva guarda arquitectónica.
- [x] Añadir `PermissionTextsTests` para comprobar textos es/en de cada permiso.
- [x] Pruebas de integración rojas→verdes para listar/filtrar, crear, editar, borrar, 404 ajeno, rol de sistema, idempotencia y concurrencia por `version` → 409 `General.ConcurrencyConflict`.

### T07 · Bloqueo de la baja del único Dueño

- [x] Añadir primero el caso HTTP y la unidad rojos: el último Dueño recibe `Legal.AccountDeletion.LastAdmin`, el ticket no se consume y conserva su cuenta; con otro Dueño activo, la baja continúa.
- [x] Implementar lectura/serialización de Dueños activos sobre `RoleAssignment` y la proyección permitida en Application/Infrastructure. El reader queda listo para reutilización en E6.
- [x] Aplicar los estados definidos: solo Dueño y Member activos de una organización abierta cuentan; no agregar un flag a `Member`.
- [x] En la baja, retirar `RoleAssignments` dentro del tenant y en la misma UoW antes de borrar `Member`, respetando la FK `RESTRICT`.
- [x] Registrar `Legal.AccountDeletion.LastAdmin` en recursos es/en; la integración verifica el bloqueo sin filtrar datos ni consumir el ticket.

### T08 · E2E real y contratos entre repos

- [x] Preparar en la base exclusiva el tenant, las filas `TenantAdmin`/`CompanyAdmin` y el Dueño en el setup existente; completar el caso iniciado en T02b contra el front/API reales.
- [x] `npm run test:e2e:real` completo verde con base y pickup aislados, cero intercepts/mocks y cero omitidos: 11/11, 0 fallidos, 0 omitidos.
- [x] El test de contrato `src/test/roles-contract.test.ts` lee los dos repositorios; `npm run contracts:check` confirma OpenAPI y TypeScript. No se agregan cuentas u organizaciones para Development.

### T09 · Recetas, feature docs y puerta documental corta

- [x] Crear `docs/guides/agregar-un-area.md`, `docs/guides/permiso-nuevo.md` y `docs/features/roles.md` ajustados a B2B/B2C y a datos públicos/compartidos.
- [x] Agregar punteros a la ficha en las carpetas backend nuevas. No crear la carpeta front de Roles ni el enlace de Organización antes de aprobar las pantallas.
- [x] Ampliar solo los casos E4 nombrados de `HarnessTests`/`harness.test.ts`: mapa feature↔carpetas y punteros a recetas. No sumar otra guarda global.
- [x] Puerta documental breve A: generar el esqueleto temporal `Tags`, ejecutar la verificación mínima y eliminarlo; el diff temporal terminó limpio.
- [x] Puerta documental breve B: diez preguntas de ubicación y comparación de respuestas leyendo solo `AGENTS.md`; resultado registrado en el informe.

### T10 · Pantallas de Roles (bloqueadas por aprobación)

- [x] **No iniciar todavía.** El back está verificado; se informa al usuario y se espera aprobación explícita de los tableros de Organización.
- [ ] Tras aprobación: implementar `areas/business/roles/{pages,api}` desde los tableros `Roles`, `Rol`, `M-Roles` y `M-Rol`; añadir enlace «Roles y permisos» dentro de «Gestión de usuarios» en `src/layouts/navigation/business.ts`, con contrato/permiso correctos, tests de pantalla y estados de conflicto 409; comparar todos los estados en `docs/design/capturas/etapa-4/`.
- [ ] Solo al terminar este bloque se sube `HarnessStage` a 4 en ambos repositorios y se considera cerrada E4.

## Puerta de cierre

- Back: `dotnet build ArquitecturaBaseMultitenant.slnx` con cero advertencias; `dotnet test` completo con Docker/Testcontainers y cero omitidos; generador de datos de referencia 30/30; nuevos tests de Role, permisos, RLS/aislamiento, migración, concurrencia, AccountDeletion y `HarnessTests` en verde.
- Front: `npm test`, `npm run lint`, `npm run build`, `npm run contracts:check`, tests de componentes/contrato y `harness.test.ts` en verde. El lint puede conservar warnings ya documentados de lienzo v35 y `InvitationPage`; no se ocultan ni se agregan como deuda nueva si el flujo los usa.
- E2E real: todos los recorridos 3a–3c y nuevos casos E4, contra front/API/navegador reales y PostgreSQL/pickup exclusivos; cero mocks, fallos u omitidos.
- Visual: las reglas de Criterio se verifican con tests colocalizados de componentes; capturas app/lienzo de `/cuenta` y sus diálogos en escritorio/teléfono; después de la aprobación, Roles/Rol y sus estados en ambas medidas. Toda diferencia tiene corrección o motivo en el informe.
- Documentación: las dos puertas cortas T09 pasan y `docs/reviews/2026-10-01-etapa-4-roles.md` registra tareas/commits, salida literal sanitizada del E2E, diferencias visuales y «Decisiones tomadas».
- Estado de esta entrega: `main`, commits locales con rutas explícitas, sin push, repos limpios y sin AppHost de Aspire activo. T10 queda pendiente de aprobación; por eso `HarnessStage` no se sube todavía y E4 no se declara cerrada.
