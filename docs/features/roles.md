# Roles y permisos

Área de referencia para administrar autorización dentro de una organización. El catálogo es global y estable; los roles y sus asignaciones son privados del tenant, con RLS. Una cuenta puede usar sus permisos del acceso Business sin mezclar su acceso Consumer. Las reglas generales y la clasificación de datos están en [AGENTS.md](../../AGENTS.md), [backend.md](../architecture/backend.md) y [multitenancy.md](../architecture/multitenancy.md).

## Conducta

- `Permissions.All` define las claves disponibles y su orden. Cada clave tiene nombre y descripción en español e inglés; `GET /api/permissions` entrega los grupos en ese orden.
- `Role` es un recurso de organización, no un rol de Identity. `TenantAdmin` (Dueño) y `CompanyAdmin` son roles de sistema inmutables. Solo un `RoleAssignment` de `TenantAdmin` representa a un Dueño; `Member` no duplica ese dato.
- El Dueño nace con la organización y es asignado por el provisionador. Roles de sistema se preparan dentro del tenant; la seed global conserva solo al operador y no crea organizaciones ni cuentas de ejemplo.
- Las rutas Business piden `[HasPermission]`. La sesión determina el tenant mediante `tenant_id`; un recurso de otro tenant responde 404. La autorización de empresa y plataforma usa atributos propios, sin reutilizar permisos de organización.
- La baja de una cuenta no puede dejar sin Dueño activo a una organización no cerrada, incluidas las suspendidas: al reactivarla, nadie podría administrarla (ADR 0035, D5). Bloquea la organización donde la cuenta es Dueña y no queda otra Dueña con cuenta y membresía activas. Es una sola regla, `LastTenantAdminGuard`, para la baja que pide la persona, la que inicia Plataforma, quitar a una Dueña y los avisos previos de Mi cuenta y de la ficha de Cuentas. La verificación se serializa con cambios de asignación mediante el lock compartido `ITenantAdminLock`. La baja de la cuenta responde 409 `Legal.AccountDeletion.LastAdmin`. Quitarle a una Dueña el rol, la membresía o el acceso en Usuarios mira solo esa organización (`CheckAnotherOwnerAsync`, que toma el lock de esa organización) y responde 409 `Tenancy.Member.LastTenantAdmin`: que la cuenta sea la única Dueña de otra organización no lo impide.
- `GET /api/roles/paged` filtra por `scope` (`Organization` o `Company`) y por `companyId`: los roles de esa empresa y los que valen en cada empresa, como el filtro «Vale en» del tablero Roles. La pantalla ofrece «Toda la organización» o una empresa por nombre; «Cada empresa» queda solo en la API y en los roles de sistema.
- `GET /api/companies/{companyId}/roles/paged` lista los roles que se pueden asignar en esa empresa: los que valen en cada empresa y los propios de esa empresa, con búsqueda y paginado. Pide `company.members.read` de esa empresa, no `roles.read` de la organización: así un Administrador de empresa sin roles de la organización elige roles al sumar un miembro o cambiar los suyos (B4 de la consolidación). Una empresa inexistente da 404.
- `/api/me` trae en `effectivePermissions.companies` los permisos de cada empresa activa donde la persona es miembro activo y tiene alguno, con los de la organización incluidos, como los calcula `[HasCompanyPermission]` (`IPermissionService.GetPermissionsByCompanyAsync`). Son las mismas empresas que «Tus empresas» del inicio.
- Las escrituras de roles validan antes de la transacción, usan una única `IUnitOfWork`, preservan `Version` y devuelven 409 ante concurrencia. Los permisos cacheados se invalidan después del commit.
- El nombre de un rol se repite entre empresas, como en el tablero Roles («Solo lectura» en cada empresa), pero no dentro de una misma empresa. Los roles sin empresa, los de toda la organización y los que valen en cada empresa, son únicos entre sí. Repetirlo da 409 `Roles.Role.AlreadyExists`. Lo hace cumplir el índice único organización + empresa + nombre normalizado, con `NULLS NOT DISTINCT` (`UniqueKeys.RoleName`), decisión D3 del 2026-10-03. Dos altas o dos cambios de nombre simultáneos con el mismo nombre pasan juntos el control previo: `RoleService` traduce el choque del índice al mismo 409, en lugar de un 500 (B10).
- Dónde vale un rol al asignarlo: el de la organización, sin empresa; el de cada empresa, en cualquiera; el de una empresa, solo en la suya. Es una sola regla, `RoleAssignment.Check` (Domain), que Usuarios y Empresas usan por `RoleAssignmentGuard`, y responde lo mismo desde las dos áreas: 400 `Roles.RoleAssignment.CompanyNotAllowed` (un rol de la organización en una empresa), `Roles.RoleAssignment.CompanyRequired` (un rol de empresa sin empresa) o `Roles.RoleAssignment.CompanyMismatch` (el rol de otra empresa). `RoleAssignment.Assign` recibe el id, el alcance y la empresa del rol, sin cargar la entidad. Los permisos efectivos (`ListRoleIdsForUserAsync`) cuentan cada asignación solo donde la regla dice que vale.
- Un rol con personas asignadas no cambia dónde vale (su alcance o su empresa), porque esas asignaciones dejarían de valer en silencio. Es el mismo criterio que para borrarlo (`Roles.Role.HasUsers`): al guardar, la respuesta es 409 `Roles.Role.ScopeChangeHasUsers` con `userCount`. Sin personas asignadas, el alcance cambia; el nombre, la descripción y los permisos se editan siempre (decisión D2 de la [revisión de calidad](../reviews/2026-10-03-calidad-de-diseno.md)).
- Una persona asignada a un rol es toda la que tiene una asignación de ese rol: activa, invitada o desactivada (decisión 42 de la consolidación). Desactivar a alguien no le quita sus roles, que vuelven a valer si se lo reactiva. Es una sola cuenta, la de `userCount` en la lista, la ficha y los roles de una empresa, y la que usan cambiar dónde vale y borrar: un rol con asignaciones responde 409 `Roles.Role.HasUsers` en lugar de chocar con la clave foránea de `RoleAssignments`. Si alguien asigna el rol en el mismo instante en que otro lo borra, los dos pasan el control previo y choca con esa clave foránea el que guarda segundo (`ForeignKeys.RoleAssignmentRole`): el borrado responde 409 `Roles.Role.HasUsers` con `userCount`, y la asignación (en Usuarios, Empresas o una invitación) responde 404 `Roles.Role.NotFound`, lo mismo que si hubiera llegado después, en lugar de un 500 (decisión 49).

## Carpetas

| Carpeta | Responsabilidad |
|---|---|
| `src/ArquitecturaBaseMultitenant.Domain/Authorization` | catálogo, roles, asignaciones y errores de dominio |
| `src/ArquitecturaBaseMultitenant.Application/Models/Roles` | pedidos y respuestas de casos de uso |
| `src/ArquitecturaBaseMultitenant.Application/Services/Roles` | consultas, escrituras y cálculo de permisos |
| `src/ArquitecturaBaseMultitenant.Application/Validation/Roles` | validación de pedidos |
| `src/ArquitecturaBaseMultitenant.Api/Authorization` | políticas y handlers HTTP |
| `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization` | entradas HTTP |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization` | rutas HTTP finas |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Tenant` | esquema privado y RLS |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers` | lecturas EF de roles y accesos globales permitidos |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories` | escrituras EF y lock de Dueño |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authorization` | reglas puras |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/Roles` | casos de uso y autorización |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization` | HTTP, aislamiento, versión y flujo real |

Las pantallas de Roles y Editar rol ya están en el front, según los tableros aprobados de Organización.

## Recetas

- Para agregar otra área de negocio, seguí [`docs/guides/agregar-un-area.md`](../guides/agregar-un-area.md) y usá Roles como ejemplo concreto.
- Para sumar una clave al catálogo, seguí [`docs/guides/permiso-nuevo.md`](../guides/permiso-nuevo.md).
- La definición del dominio y la protección de la baja están detalladas en el [plan de E4](../plans/2026-10-01-etapa-4-roles.md).
- Las membresías, los roles de empresa y la protección del último Administrador están detallados en [organizaciones.md](organizaciones.md).

## Lo verifica

- Domain: `RoleTests` (el Dueño tiene todo el catálogo) y `RoleAssignmentTests` (la matriz de dónde vale un rol). Que cada clave del catálogo tenga su texto en es y en lo controla `PermissionTextsTests`.
- Application: `RoleServiceTests`, `RoleServiceWriteTests`, `PermissionTextsTests` y los tests de políticas de baja.
- API: `RolesTests`, `PermissionAuthorizationTests`, `RolePersistenceTests` (la matriz de permisos efectivos), `UserTenantAccessReaderTests`, `AccountDeletionTests`, `OrganizationUsersTests` (la última Dueña de cada organización) y `CompaniesTests` (el mismo código de alcance desde Usuarios y Empresas) (Docker/Testcontainers).
- Contrato cruzado: `ArquitecturaBaseMutitenantFront/src/test/roles-contract.test.ts` lee controller, contratos y OpenAPI del back, además de los tipos generados del front.
