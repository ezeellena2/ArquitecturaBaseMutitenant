# Etapa 4 · Autorización y Roles — plan de implementación

> **Estado:** plan revisado; ejecución pendiente. Se trabaja en `main`, en este chat y sin push. Cada tarea deja primero su prueba dirigida roja cuando agrega conducta, termina con la prueba y el build del alcance en verde y se commitea con rutas explícitas. La puerta general se corre al cierre. No se agregan guardas de arquitectura; las únicas verificaciones del arnés que se amplían son las enumeradas en T09.

## Objetivo y límites

Implementar autorización por permisos y roles como área de referencia, proteger al único Dueño durante la baja de una cuenta, llevar los componentes compartidos y `/cuenta` al Criterio validado de la versión 60, y demostrar el contrato y el recorrido de roles con el navegador, la API y una base aislada reales.

Se programan solo los siete tableros validados `Criterio-*` y los tableros validados de Perfil personal para `/cuenta`: `Cuenta.dc.html`, `M-Cuenta.dc.html`, `Cuenta-Agregar.dc.html`, `Cuenta-Verificar.dc.html` y `Cuenta-Baja.dc.html`. Las pantallas `Roles`, `Rol`, `M-Roles`, `M-Rol` y su entrada del AdminPanel pertenecen a Organización, que está «para aprobar». Se completa el back y se deja todo listo para esas pantallas, pero no se dibujan ni programan antes del visto bueno del usuario.

No se crean cuentas, espacios personales ni organizaciones de ejemplo en Development. Los tests y el E2E preparan sus propios datos en sus bases aisladas. El seed global conserva únicamente el operador.

## Decisiones tomadas

- Los Roles y RoleAssignments son privados de cada organización y llevan RLS. `SystemRoles` define las plantillas inmutables; cada organización de prueba recibe esas filas y el Dueño se asigna por `RoleAssignment`. El seed global no inventa un `TenantId` ni crea organizaciones. El provisionador de Etapa 6 reutilizará la misma preparación al nacer una organización real.
- `TenantAdmin` es la única fuente de verdad para Dueño; `Member` no recibe un flag. La proyección global de accesos puede reflejar el rol para Mi cuenta y la protección de baja, pero siempre se mantiene desde el `RoleAssignment` dentro de la misma UoW.
- El rol activo de `/api/me` se incorpora al índice de perfiles de organización que ya reserva `OrganizationSummary.RoleName`; no se leen Members ni RoleAssignments fuera de su RLS. Se actualiza en la misma transacción que cambie la asignación.
- La comparación visual cubre Criterio y los estados de `/cuenta` afectados, en escritorio y teléfono. Los demás detalles visuales del lienzo que no cambian un flujo o seguridad se anotan en backlog.
- `HarnessStage` solo sube a 4 en ambos repositorios cuando, después de la aprobación, también estén implementadas las pantallas de roles y hayan pasado todas las puertas de cierre.

## Preparación y secuencia TDD

### T00 · Plan

- [x] Revisar plan maestro, arquitectura multitenant/backend, fichas de dominio, reglas del lienzo v60 y AGENTS de los directorios aplicables.
- [x] Revisión del plan completada; el commit aislado usa `git add -- docs/plans/2026-10-01-etapa-4-roles.md` y `git commit -m "docs: planificar etapa 4 de roles"` antes de cualquier cambio de código.

### T01 · Bajar los siete Criterio a las fichas del front

- [ ] Actualizar `ArquitecturaBaseMutitenantFront/docs/rules/pantallas-y-ui.md`, `formularios.md` y `paginado-y-listados.md` para que describan los tableros Criterio v60: tamaño compacto; banda y encabezados arena; filas alternadas; estados en segunda línea sin badges; cajas por tema y campos del ancho del dato; errores/toasts según tipo; estados habilitados de Guardar/Descartar; acciones en ⋮; panel AdminPanel; paginado al pasar 10 y tabs solo para dos o más tablas.
- [ ] No ejecutar los generadores ni modificar `canvas.json`: las reglas ya están validadas y esos generadores reescriben el lienzo.
- [ ] Para cada regla verificable, añadir primero una aserción conductual/accesible que falle en los tests colocalizados existentes: `shared/ui/button.test.tsx` (nuevo), `DataTable.test.tsx`, `Pagination.test.tsx`, `RowActions.test.tsx`, `dialog.mobile.test.tsx`, `ConfirmDialog.test.tsx`, `FormField.test.tsx`, tests de los fields realmente afectados, `Banner.test.tsx`, `FormError.test.tsx`, `Page.test.tsx`, `PrivateLayoutFrame.test.tsx`, `AdminPanel.test.tsx` y `navigation.test.tsx`. Agregar `dropdown-menu.test.tsx` solo si el comportamiento visible del menú no tiene cobertura existente.
- [ ] Cubrir reglas comprobables de los componentes: alto/tamaño compacto; nombre accesible y teclado; fila alternada y estado sin pastilla; encabezado ordenable con dirección visible; confirmación irreversible sin cierre por ✕ y acción roja; rótulo/required/error del campo; aviso persistente vs transitorio; acciones secundarias dentro del menú; la banda de página y Administración como panel secundario; paginado al superar 10.
- [ ] Corregir `button.tsx`, `IconButton.tsx`, `table.tsx`, `DataTable.tsx`, `Pagination.tsx`, `LoadMore.tsx`, `RowActions.tsx`, `dialog.tsx`, `ConfirmDialog.tsx`, `Sheet.tsx`, `input.tsx`, `FormField.tsx`, `fields/*`, `Banner.tsx`, `FormError.tsx`, `sonner.tsx`, `dropdown-menu.tsx`, `Page.tsx`, y los componentes de `src/layouts/` que materialicen cada regla. Mantener todas las dimensiones, colores y copys atados a `docs/design/lienzo/Criterio-*.dc.html` y `docs/architecture/tema.md`.
- [ ] Resolver del backlog solo lo que toca un componente/regla afectado por este trabajo: #24, #25, #26, #27, #28, #65, #66, #67, #68, #71 y #72; revisar #2, #3 y #6 si el field correspondiente aparece en `/cuenta`. Mantener en backlog lo no utilizado por este flujo.
- [ ] Correr tests dirigidos rojos y verdes por grupo, `npm run lint` y `npm run build` del front; no sumar reglas de arquitectura ni tests nuevos del arnés fuera de T09.

### T02 · Copiar Perfil personal en `/cuenta` y diálogos

- [ ] Primero extender `src/areas/personal/account/pages/AccountPage.test.tsx` y los tests colocalizados para exigir composición/orden/copy aprobados en escritorio y móvil, estados de carga/error/vacío y diálogos montar/cerrar; comprobar que el perfil conserva `version`, `Guardar cambios` se habilita solo con cambios y `Descartar cambios` permanece activo.
- [ ] Rehacer solo presentación y composición de `src/areas/personal/account/pages/AccountPage.tsx`, `components/AccountProfileForm.tsx`, `LoginMethodsTable.tsx`, `AddLoginMethodDialog.tsx`, `VerifyLoginMethodDialog.tsx`, `ChangeLoginMethodDialog.tsx` y `AccountDeletionDialog.tsx`, copiando `Cuenta`, `M-Cuenta`, `Cuenta-Agregar`, `Cuenta-Verificar` y `Cuenta-Baja`. Conservar los recorridos, API, seguridad, versión, tickets y navegación ya cubiertos por 3b; no agregar acciones, datos ni estados que el lienzo no muestre.
- [ ] Alinear los recursos es/en solo cuando el texto existente no coincide con el tablero; mantener `ResourceParityTests`/paridad del front en verde.
- [ ] Crear `scripts/capturar-etapa-4.mjs` y su test de verificación, y guardar los pares app/lienzo modificados en `docs/design/capturas/etapa-4/`: `/cuenta` y los diálogos afectados, 1440×900 y 390×844. Revisar diferencias reales; ajustar las copiables y anotar motivo únicamente para las que dependen de Radix/Sonner o de datos contractuales.
- [ ] Verificar que los recorridos reales de la 3b de `/cuenta` siguen verdes en el `test:e2e:real` de cierre.

### T02b · Primer rojo del recorrido real de Roles

- [ ] Extender el script/setup E2E con el recorrido E4 contra el navegador, la API y la base aislada reales, sin mocks ni intercepts: login real por `.eml`, catálogo, crear rol, recuperar `version`, actualizar, conflicto por versión obsoleta, protección de roles de sistema y baja bloqueada para el único Dueño.
- [ ] Ejecutar solo ese caso antes de comenzar T03 y guardar el resultado rojo por ausencia de la API de Roles. Mantener el resto de la suite verde; el caso completo debe quedar en verde en T08.
- [ ] No usar pantallas de Roles: siguen pendientes de aprobación y el E2E opera mediante navegador autenticado y API real.

### T03 · Dominio de autorización y roles

- [ ] Crear primero tests rojos en `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization/RoleTests.cs` y `RoleAssignmentTests.cs`.
- [ ] Implementar en `src/ArquitecturaBaseMultitenant.Domain/Authorization/` `Permissions`, `PersonalPermissions`, `PlatformPermissions`, `Role`, `RoleScope`, `RoleAssignment`, `SystemRoles` y `RoleErrors`; añadir `AGENTS.md` y `CLAUDE.md` a la carpeta nueva.
- [ ] `Role` es `ITenantOwned` y `IVersioned`, valida nombre/ámbito/empresa/permisos, y solo usa errores `Roles.Role.*`. `TenantAdmin` y `CompanyAdmin` son inmutables; el Dueño nunca se deriva de Member.
- [ ] Cubrir unidades: `Organization`, `AnyCompany`, `SpecificCompany`; empresa requerida/prohibida según alcance; límites/nombres/permisos; estado de rol de sistema y versión.

### T04 · Persistencia privada, migración, sistema de roles y perfiles

- [ ] Añadir tests de modelo/migración/RLS/concurrencia antes de implementar configuraciones y repositorios.
- [ ] Implementar `RoleConfiguration` y `RoleAssignmentConfiguration` en `Infrastructure/Persistence/Configurations/Tenant/`, puertos en `Application/Interfaces/Persistence/`, y `RoleRepository`/`RoleReader` en Infrastructure; crear una migración nueva en `Infrastructure/Persistence/Migrations/` con RLS `EnableTenantRls`, índices por campos de `SortMap`, auditoría y token de versión `xmin`.
- [ ] Registrar `Role`/`RoleAssignment` en `ApplicationDbContext` y en clasificación/seed de esquema; revisar el SQL completo antes de commitear. Los tests crean tenant y roles dentro de su Testcontainer, prueban aislamiento cruzado, assignment huérfano y concurrencia real.
- [ ] Sembrar `TenantAdmin` y `CompanyAdmin` por tenant en la preparación tenant-scoped que reutiliza la futura Etapa 6; asignar `TenantAdmin` al Dueño de la organización. No tocar la seed global para introducir una organización.
- [ ] Actualizar el índice `identity.UserTenantAccesses` y su reader/configuración/proyección para incluir el rol de sistema derivado del `RoleAssignment`. Mantenerlo sincronizado en la misma UoW al asignar, quitar o cambiar el rol. Ampliar los tests del índice, `/api/me` y baja; ningún endpoint del front ve un flag de Dueño en `Member`.
- [ ] Revisar `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/`, `Tenancy/` y la migración: las políticas RLS bloquean a otra organización aun cuando EF omite un filtro, y todas las rutas nuevas reciben declaración explícita.

### T05 · Contratos, validación y respuestas HTTP

- [ ] Agregar primero tests de contrato rojos para paginado/filtros/conteos, grupos de permisos, lectura con versión, creación 201 y PUT/DELETE con versión obligatoria y 409.
- [ ] Añadir `Application/Models/Roles/` (`RoleResponse`, requests paginadas y de escritura, `PermissionGroupResponse`), validadores bajo `Application/Validation/Roles/`, errores y recursos `Errors.resx`/`Errors.en.resx` y `Permissions.resx`/`Permissions.en.resx`; crear los `AGENTS.md`/`CLAUDE.md` que exija cada carpeta nueva.
- [ ] Exponer contratos en `Api/Contracts/Organization/` y controller fino en `Api/Controllers/Organization/RolesController.cs`: listado paginado con filtros/conteos, detalle con `version`, creación 201, actualización y baja con `version`; POST idempotente. Declarar `[Access(Business)]` y los permisos correspondientes, sin ruta de negocio anónima.
- [ ] Generar el OpenAPI del back, sincronizar `ArquitecturaBaseMutitenantFront/docs/contracts/openapi.json` y `src/shared/api/generated/`, y crear `src/test/roles-contract.test.ts` siguiendo `invitation-contract.test.ts`: el test lee controller/contratos del back, snapshot OpenAPI y tipos/esquema generados del front.

### T06 · RoleService y autorización

- [ ] Escribir primero tests rojos de `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/Roles/RoleServiceWriteTests.cs` (métodos que escriben) y `RoleServiceTests.cs` para lecturas, siguiendo `../ArquitecturaBase/src/ArquitecturaBase.Application/Services/Roles/RoleService.cs` y sus tests reales; cubrir Result/error, transacción única, concurrencia y caché solo después del commit.
- [ ] Implementar `IRoleService`, `RoleService`, `PermissionService` y `PlatformPermissionService` en Application. Una sola `IUnitOfWork.ExecuteInTransactionAsync` por método público de escritura; validar afuera; invalidar la caché solo después del commit confirmado; listado filtrado/paginado y grupos en el orden del catálogo; protección de los roles de sistema.
- [ ] Implementar `HasPermission`, `HasCompanyPermission`, `HasPlatformPermission`, requirements/handlers y provider en `Api/Authorization/`; cubrir autorización de cada tipo, acceso incorrecto, empresa ajena, permiso inexistente y respuestas ProblemDetails en `PermissionAuthorizationTests`.
- [ ] Actualizar los tests existentes `AccessDeclarationTests`, inventario HTTP/OpenAPI, `ServiceDependencyCountTests`, `ResourceParityTests` y los tests de invalidación pertinentes; no crear una nueva guarda arquitectónica.
- [ ] Añadir `PermissionTextsTests` para comprobar que cada permiso del catálogo tenga texto en español e inglés, siguiendo `docs/rules/permisos.md` y `textos-y-traducciones.md`.
- [ ] Pruebas de integración rojas→verdes para listar/filtrar, create 201, editar, borrar, 404 ajeno, rechazo de rol de sistema, idempotencia y concurrencia por `version` → 409 `General.ConcurrencyConflict`.

### T07 · Bloqueo de la baja del único Dueño

- [ ] Añadir primero un caso HTTP rojo a `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/AccountDeletionTests.cs` y unidad roja a `AccountDeletionPolicyTests.cs`: la última persona Dueña de una organización no cerrada recibe `Legal.AccountDeletion.LastAdmin`, no consume el ticket y conserva su cuenta; con otro Dueño activo, la baja continúa.
- [ ] Implementar la lectura/reutilización de Dueños activos sobre `RoleAssignment` y la proyección permitida, en Application/Infrastructure con puerto propio. El mismo reader queda listo para `LastTenantAdminGuard` de Etapa 6; la decisión se serializa con el cambio de roles y no abre tenant scope dentro de una transacción ya abierta.
- [ ] `Active` cuenta; `PendingDeletion`, `Suspended`, `Deleted`, Member inactivo y organización cerrada no cuentan como Dueños activos según `multitenancy.md` y el punto 7 del plan maestro. No agregar un flag a la entidad `Member`.
- [ ] Registrar `Legal.AccountDeletion.LastAdmin` en recursos es/en y cubrir también el reauth/request sin filtrar datos de otras organizaciones ni consumir un ticket al bloquear.

### T08 · E2E real y contratos entre repos

- [ ] Preparar en la base exclusiva el tenant, las filas `TenantAdmin`/`CompanyAdmin` y el Dueño en el setup existente; al finalizar el back, completar el caso iniciado en T02b y dejar `node --test --test-name-pattern="4:" scripts/test-e2e-real.test.mjs` en verde.
- [ ] Al final `npm run test:e2e:real` completo debe pasar con base y pickup aislados, cero intercepts/mocks y cero omitidos.
- [ ] El test de contrato `src/test/roles-contract.test.ts` lee lados back y front; `npm run contracts:check` confirma el OpenAPI y TypeScript regenerados. No introducir una cuenta u organización para Development.

### T09 · Recetas, feature docs y puerta documental corta

- [ ] Crear `docs/guides/agregar-un-area.md`, `docs/guides/permiso-nuevo.md` y `docs/features/roles.md`, copiando orden y convención desde ArquitecturaBase, ajustados a B2B, B2C y datos públicos/compartidos.
- [ ] En las carpetas backend nuevas, cada `AGENTS.md` apunta a `docs/features/roles.md` y cada `CLAUDE.md` contiene solo `@AGENTS.md`. No crear aún la carpeta front `areas/business/roles` ni el enlace «Roles y permisos» de Organización: pertenecen a la etapa de pantallas posterior a aprobación. T01 sí puede adaptar el comportamiento genérico del `AdminPanel` cubierto por `Criterio-Marco` y `Criterio-Menus`, sin agregar contenido de Roles ni enlaces nuevos.
- [ ] Ampliar solo los casos E4 nombrados de los `HarnessTests`/`harness.test.ts` existentes: mapa feature↔carpetas y punteros a recetas. No sumar otra guarda global.
- [ ] Puerta documental breve A: una ejecución aislada sigue `agregar-un-area.md` y genera el esqueleto temporal `Tags`, corre la verificación mínima del área y lo elimina; el diff del repositorio termina limpio.
- [ ] Puerta documental breve B: preescribir diez preguntas “¿dónde va X?” y pedir a un revisor sin contexto de implementación que lea solo `AGENTS.md`; comparar sus respuestas con la ubicación esperada y guardar resultado en el informe de etapa.

### T10 · Pantallas de Roles (bloqueadas por aprobación)

- [ ] **No iniciar todavía.** Después de completar y verificar el back, avisar al usuario y esperar su aprobación explícita para los tableros de Organización marcados «para aprobar».
- [ ] Tras aprobación: implementar `areas/business/roles/{pages,api}` desde los tableros `Roles`, `Rol`, `M-Roles` y `M-Rol`; añadir enlace «Roles y permisos» dentro de «Gestión de usuarios» en `src/layouts/navigation/business.ts`, con contrato/permiso correctos, tests de pantalla y estados de conflicto 409; comparar todos los estados en `docs/design/capturas/etapa-4/`.
- [ ] Solo al terminar este bloque se sube `HarnessStage` a 4 en ambos repositorios y se considera cerrada E4.

## Puerta de cierre

- Back: `dotnet build ArquitecturaBaseMultitenant.slnx` con cero advertencias; `dotnet test` completo con Docker/Testcontainers y cero omitidos; generador de datos de referencia 30/30; nuevos tests de Role, permisos, RLS/aislamiento, migración, concurrencia, AccountDeletion y `HarnessTests` en verde.
- Front: `npm test`, `npm run lint`, `npm run build`, `npm run contracts:check`, tests de componentes/contrato y `harness.test.ts` en verde. El lint puede conservar warnings ya documentados de lienzo v35 y `InvitationPage`; no se ocultan ni se agregan como deuda nueva si el flujo los usa.
- E2E real: todos los recorridos 3a–3c y nuevos casos E4, contra front/API/navegador reales y PostgreSQL/pickup exclusivos; cero mocks, fallos u omitidos.
- Visual: capturas app/lienzo de Criterio y `/cuenta` en escritorio/teléfono; después de la aprobación, Roles/Rol y sus estados en ambas medidas. Toda diferencia tiene corrección o motivo en el informe.
- Documentación: las dos puertas cortas T09 pasan y `docs/reviews/2026-10-01-etapa-4-roles.md` registra tareas/commits, salida literal sanitizada del E2E, diferencias visuales y «Decisiones tomadas».
- Estado final: `main`, commits locales con rutas explícitas, sin push, ambos repos limpios, sin AppHost de Aspire activo y `HarnessStage=4` en ambos repositorios.
