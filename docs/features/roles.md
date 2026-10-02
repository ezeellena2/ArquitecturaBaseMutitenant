# Roles y permisos

Área de referencia para administrar autorización dentro de una organización. El catálogo es global y estable; los roles y sus asignaciones son privados del tenant, con RLS. Una cuenta puede usar sus permisos del acceso Business sin mezclar su acceso Consumer. Las reglas generales y la clasificación de datos están en [AGENTS.md](../../AGENTS.md), [backend.md](../architecture/backend.md) y [multitenancy.md](../architecture/multitenancy.md).

## Conducta

- `Permissions.All` define las claves disponibles y su orden. Cada clave tiene nombre y descripción en español e inglés; `GET /api/permissions` entrega los grupos en ese orden.
- `Role` es un recurso de organización, no un rol de Identity. `TenantAdmin` (Dueño) y `CompanyAdmin` son roles de sistema inmutables. Solo un `RoleAssignment` de `TenantAdmin` representa a un Dueño; `Member` no duplica ese dato.
- El Dueño nace con la organización y es asignado por el provisionador. Roles de sistema se preparan dentro del tenant; la seed global conserva solo al operador y no crea organizaciones ni cuentas de ejemplo.
- Las rutas Business piden `[HasPermission]`. La sesión determina el tenant mediante `tenant_id`; un recurso de otro tenant responde 404. La autorización de empresa y plataforma usa atributos propios, sin reutilizar permisos de organización.
- La baja de una cuenta no puede dejar sin Dueño activo a una organización abierta. El conteo incluye solo cuenta, miembro y organización activos. La verificación se serializa con cambios de asignación mediante el lock compartido `ITenantAdminLock`; la etapa 6 lo debe tomar al asignar o quitar `TenantAdmin`.
- Las escrituras de roles validan antes de la transacción, usan una única `IUnitOfWork`, preservan `Version` y devuelven 409 ante concurrencia. Los permisos cacheados se invalidan después del commit.

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

Las pantallas de Roles y el enlace de Administración esperan la aprobación de sus tableros de Organización. Sus carpetas de front no se crean antes de ese visto bueno.

## Recetas

- Para agregar otra área de negocio, seguí [`docs/guides/agregar-un-area.md`](../guides/agregar-un-area.md) y usá Roles como ejemplo concreto.
- Para sumar una clave al catálogo, seguí [`docs/guides/permiso-nuevo.md`](../guides/permiso-nuevo.md).
- La definición del dominio y la protección de la baja están detalladas en el [plan de E4](../plans/2026-10-01-etapa-4-roles.md).

## Lo verifica

- Domain: `RoleTests`, `RoleAssignmentTests` y `PermissionsTests`.
- Application: `RoleServiceTests`, `RoleServiceWriteTests`, `PermissionTextsTests` y los tests de políticas de baja.
- API: `RolesTests`, `PermissionAuthorizationTests`, `RolePersistenceTests`, `UserTenantAccessReaderTests` y `AccountDeletionTests` (Docker/Testcontainers).
- Contrato cruzado: `ArquitecturaBaseMutitenantFront/src/test/roles-contract.test.ts` lee controller, contratos y OpenAPI del back, además de los tipos generados del front.
