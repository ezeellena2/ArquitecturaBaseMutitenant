# Plataforma

Administración global operada con `access=platform`, separada de la administración Business de cada organización. El desarrollo y su puerta están en el [plan de E5](../plans/2026-10-03-etapa-5-plataforma.md); E5 permanece abierta hasta completar esa puerta.

## Operadores y permisos

Una cuenta operadora tiene una asignación global versionada de `PlatformRole`, independiente de los roles privados de organización. El catálogo de permisos sigue siendo `PlatformPermissions`; no se agregan claves para representar roles.

| Rol | Permisos |
|---|---|
| Owner | Todas las claves de `PlatformPermissions.All` |
| Support | `platform.tenants.read`, `platform.accounts.read`, `platform.recoveries.manage`, `platform.audit.read` |

Las colecciones de permisos son inmutables. Los cambios de rol conservarán la identidad de la asignación y usarán su versión de concurrencia. La persistencia y la resolución de autorización se conectan en T03 del plan; las rutas y la protección del último Owner se completan en T16.

## Lo verifica

- `PlatformRoleTests`: catálogo completo de Owner, alcance de Support, inmutabilidad y asignación global/versionada.
