# Plataforma

Administración global operada con `access=platform`, separada de la administración Business de cada organización. El desarrollo y su puerta están en el [plan de E5](../plans/2026-10-03-etapa-5-plataforma.md); E5 permanece abierta hasta completar esa puerta.

## Operadores y permisos

Una cuenta operadora tiene una asignación global versionada de `PlatformRole`, independiente de los roles privados de organización. El catálogo de permisos sigue siendo `PlatformPermissions`; no se agregan claves para representar roles.

| Rol | Permisos |
|---|---|
| Owner | Todas las claves de `PlatformPermissions.All` |
| Support | `platform.tenants.read`, `platform.accounts.read`, `platform.recoveries.manage`, `platform.audit.read` |

Las colecciones de permisos son inmutables. `platform.PlatformRoleAssignments` tiene una asignación única por UserId, FK a la identidad global y versión xmin. `PlatformPermissionService` consulta el rol persistido sin cachear permisos revocables: exige cuenta Active y el indicador IsPlatformOperator; un indicador sin asignación nunca concede permisos. Las rutas y la protección del último Owner se completan en T16.

La migración de E5 convierte los operadores anteriores a Owner sin crear cuentas ni espacios personales. El seed técnico preserva una asignación existente (incluido Support) y solo agrega Owner al primer operador o a un indicador legado sin asignación. Se ejecuta dentro de la transacción/lock global del seed.

## Lo verifica

- `PlatformRoleTests`: catálogo completo de Owner, alcance de Support, inmutabilidad y asignación global/versionada.
- `PlatformPermissionServiceTests`: permisos por rol, cuenta inactiva, asignación ausente y cuenta no operadora.
- `PlatformOperatorPersistenceTests`: versión, transacción, unicidad y actualización real desde operador legado en PostgreSQL aislado, sin Personal.
- `ProductionSeedTests`: Owner inicial, repetición del seed y preservación de una sola asignación.
