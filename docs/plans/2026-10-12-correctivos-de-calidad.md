# Correctivos de calidad antes de la Etapa 11

**Cuándo y quién:** un chat nuevo de Codex, con los dos repos, en paralelo con la 8b y antes de la 11 (decisión del usuario, 2026-10-12). La 11 copia los moldes del núcleo (Roles, las listas para elegir, «Para resolver»): estos arreglos van primero para que no copie sus problemas. **Por qué:** la revisión de código del 2026-10-12 encontró que las reglas se cumplen, pero hay cosas que andan con pocos datos y se rompen con muchos, valores fijos y código copiado en vez de compartido.

**Reglas:** `AGENTS.md`, «Velocidad y cierre liviano». No hay funcionalidad nueva: cada tarea hace cumplir una regla ya escrita (paginado, valores fijos, capas, no duplicar). No se tocan `Modules/Assistant` ni `src/modules/assistant` (son de la 8b) ni `Program.cs`. Los contratos nuevos son aditivos y los coordina este mismo chat en los dos repos. Commits con pathspec, sin push.

## Tareas

| # | Qué | Dónde | Listo cuando |
|---|---|---|---|
| C1 | **«Para resolver» con tope.** Conteo más los primeros ítems de cada área y «Ver todos» hacia el listado filtrado. Los permisos se revisan una sola vez por pedido, no ítem por ítem. | back `Services/OrganizationUsers/OrganizationUserPendingItems.cs`, `Services/Home/HomeService.cs`; front `areas/platform/home/api/pending.ts`, `PlatformToResolve.tsx` | con miles de pendientes, el Inicio hace una cantidad fija de consultas |
| C2 | **Listas para elegir con búsqueda en el servidor.** Un selector con búsqueda remota en `shared`, que reemplaza las listas cortadas en 100 de roles, personas y empresas («Agregar miembro», filtros, asignaciones). Una sola consulta de opciones por recurso, con su clave. | front `roles/api/roles.ts`, `users/api/users.ts`, `companies/api/companies.ts`, `AddMemberDialog.tsx`; back, un endpoint de opciones paginado si hace falta | se puede elegir la empresa, el rol o la persona número 101 |
| C3 | **Conteos sin un pedido por empresa.** El back devuelve los conteos por empresa de una vez (`/api/roles/filter-counts` y el conteo por empresa en `users/filter-counts`). El front borra los `useQueries`. | back Roles y OrganizationUsers; front `RolesPage.tsx`, `UsersPage.tsx` | una búsqueda hace un solo pedido de conteos |
| C4 | **Concurrencia de Plataforma en `shared`.** Hooks comunes (`useMountedRef`, `useResourceAction` tipado, `useAbandonOnLeave` sobre `useBlocker`) en lugar del código copiado en 9 pantallas. Sin `UNSAFE_DataRouterContext` ni comparar `pathname`. La ficha datos-y-api pasa a decir «usá el hook». | front `areas/platform/**`, `shared/`, `docs/rules/datos-y-api.md` | ninguna pantalla repite la coordinación; la guarda de formularios no encuentra `UNSAFE_` |
| C5 | **Paginado en el servidor y tipos del contrato.** `CompanyRolesTab` pide la lista paginada con `usePagedQuery`. `PagedResult<T>` escrito a mano se reemplaza por los alias `PagedResultOf…` generados. | front `CompanyRolesTab.tsx`, `useCompanyRoles.ts`, `shared/api/pagedResult.ts` y sus usos | no queda paginado en el cliente ni un `PagedResult` propio |
| C6 | **Las reglas de la baja, en Application.** Los participantes de Membership, PersonalSpace y Recovery pasan a `Services/Legal`, como hizo WhatsApp. En Infrastructure queda solo lo genérico de EF (`EngagementCopyDeletion`). | back `Infrastructure/Identity/Deletion/*` | `ILastTenantAdminGuard` deja de ser público; tests de la baja en verde |
| C7 | **Una sola política de errores en logs y procesos de fondo.** Se registra el tipo y el stack, sin el mensaje (o con un redactor), igual en HTTP y en los workers. Una base `PeriodicWorker` reemplaza el bucle copiado. Ningún worker frena el servidor por una excepción inesperada. | back `GlobalExceptionHandler.cs`, `OperationLog.cs`, los 8 `BackgroundService` | un worker que falla registra y sigue |
| C8 | **Las rutas del front, en un solo lugar del back.** Un catálogo en `ReturnUrls` más un helper `IPublicOrigin.Absolute(path)`, en lugar de los ~20 literales (`/cuenta`, `/login/empresa`…). | back `ExternalLoginController`, `ConnectController`, `AccountDeletionNotifier`, `LoginMethodNotifier`, `PlatformRecoveryNotifier`, `IdentityRegistration` | no queda una ruta del front escrita fuera del catálogo |
| C9 | **Auditoría paginada.** Las organizaciones con eventos y los actores de la auditoría de la organización pasan a una búsqueda con tope (`PagedRequest`). | back `PlatformAuditController`, `SecurityEventReader`, `OrganizationAuditReader`; front sus filtros | ningún listado de auditoría devuelve todo |
| C10 | **Valores y textos con una sola fuente.** El CUIT del registro de empresa usa `TaxIdNumberField` con el tipo del catálogo. Los largos máximos y los rangos los publica el back en el OpenAPI y el front los genera con `npm run contracts`. Los nombres de los roles de sistema salen solo del back, y se alinean los mocks. «Cuenta eliminada» pasa a ser una etiqueta de `FormattingTexts`, sin guardarla traducida. Las categorías de auditoría van en una clase como `PlatformAuditCategories`. | front `BusinessSignupPage.tsx`, `useCuitType.ts`, formularios con `maxLength`, `roles/lib/systemRoles.ts`; back `ErrorTexts`, `AccountDeletionProcessor`, `PlatformOperatorProvisioner`, `OrganizationAuditReader` | ningún límite, CUIT ni nombre de rol vive en dos lugares |
| C11 | **Detalles.** Validación a mano y literales de error en `ExternalLoginController`, pasados a request más validador y constantes de `<X>Errors`. Frases armadas con fragmentos, pasadas a `<Trans components>`. `Date.now` y `Date.parse` fuera de `shared/time`, sumados a la guarda de formatos. Duplicados (`initials`, `toSearch`) y código muerto (`secondsUntilResend`, `personal.json`), afuera. Carpetas vacías del back, borradas. El worker de retención de WhatsApp se renombra a `…Worker`. | los nombrados | build, lint y tests del alcance en verde |

**Va a la Etapa 9, no acá:**
- los permisos con una consulta por empresa en cada pedido (`PermissionService`), junto con la caché;
- los tiempos de seguridad fijos (tokens, cookie, idempotencia, enlace de ingreso, lote de la baja) y el límite del webhook de WhatsApp, pasados a opciones con `ValidateOnStart`;
- un formateador en el CI, que reformatearía todo el repo y se pisaría con la 8b.

## Asistente: lo hace la 8b, antes de su puerta

- Una base para los tres controllers idénticos (`Api/Modules/Assistant/Controllers/{Business,Consumer,Platform}AssistantController.cs`): las derivadas declaran solo ruta, acceso, permiso y gate.
- Una base para las entidades gemelas (`AssistantConversation` y `PlatformAssistantConversation`, y lo mismo con Message, Turn y DurableOperation).
- Los números repetidos (10 MiB, 5 adjuntos, 0,8, 20 filas) en `AssistantTechnicalLimits` u opciones, y el contributor del contrato que los interpole.
- `RepositoryHelpArticleReader` y sus recursos, dentro del módulo, con las culturas del catálogo.
- `AssistantHome.tsx` partido en hooks y paneles. El módulo, autocontenido: sus tipos, textos y fixtures dentro de `src/modules/assistant`, aportados por `registration.ts`.
- Los 3 tests de arquitectura en rojo (formatos a mano, uso del tenant, datos fijos).

## Cómo se cierra

Cada tarea, con los tests de su área. Al final, una sola vez: builds, suites de los dos repos, tests de arquitectura y del arnés, `contracts:check` y el detector en 0/0/0. Arriba de este plan queda una tabla con el resultado de cada tarea.

## Texto para el chat de Codex

```
Leé AGENTS.md del back y del front y docs/plans/2026-10-12-correctivos-de-calidad.md (back). Hacé C1 a C11 en ese orden, en los dos repos. Es en paralelo con la 8b: no toques Modules/Assistant, src/modules/assistant ni Program.cs, y commiteá solo tus archivos con pathspec. Los contratos nuevos son aditivos: publicalos y regenerá los tipos del front en el mismo paso. No agregues funcionalidad: cada tarea hace cumplir una regla ya escrita. Las decisiones técnicas son tuyas: anotalas en una línea del plan. Sin push. Al final, la corrida de «Cómo se cierra» una sola vez y la tabla arriba del plan.
```
