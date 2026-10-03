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

## Configuración global

`GET/PUT /api/platform/settings` requiere `platform.settings.manage`. Expone los modos de registro de personas y organizaciones y el límite de organizaciones propias (mínimo uno). La gracia de baja permanece en 30 días. La edición exige motivo y versión xmin; un conflicto devuelve 409 sin guardar ni agregar un evento. El cambio y su SecurityEvent con el operador se confirman juntos; la caché global se invalida después del commit. Support y los accesos Consumer/Business no pueden consultar ni cambiar esta configuración.

## Organizaciones: consulta y estado

`/api/platform/tenants/paged`, `/filter-counts` y `/{id}` requieren `platform.tenants.read` y muestran exclusivamente Business. La búsqueda compartida no distingue acentos/case y escapa comodines; incluye nombre, slug y contacto del Dueño. Cada dimensión de conteos mantiene los otros filtros. La ficha de Personal se devuelve como 404.

El registro global conserva origen, fecha y cuenta que registró, versión xmin y conteos. La fecha histórica se recupera de las incorporaciones disponibles; si no existe evidencia queda null. Los totales de empresas y miembros no retirados se mantienen mediante deltas atómicos en triggers de sus escrituras bajo RLS, sin consultar esas filas privadas desde Plataforma. El contacto se proyecta de la identidad global del Dueño actual (o del registrante autoservicio cuando aún no hay asignación).

Rechazar (PendingApproval→Closed), suspender (Active→Suspended), reactivar (Suspended→Active) y cerrar (PendingApproval/Suspended→Closed) requieren `platform.tenants.manage`, motivo y versión. Los cambios guardan SecurityEvent con actor y destino y encolan avisos a registrante/Dueños en la misma UoW. Las bajas de acceso revocan únicamente los tokens de esa organización; después del commit se invalida su estado en caché. La identidad y los accesos Personal conservan sus sesiones.

La aprobación, creación por invitación y reintento siguen en T07; E5 permanece abierta.

## Verificación

- `PlatformRoleTests`: catálogo completo de Owner, alcance de Support, inmutabilidad y asignación global/versionada.
- `PlatformPermissionServiceTests`: permisos por rol, cuenta inactiva, asignación ausente y cuenta no operadora.
- `PlatformOperatorPersistenceTests`: versión, transacción, unicidad y actualización real desde operador legado en PostgreSQL aislado, sin Personal.
- `ProductionSeedTests`: Owner inicial, repetición del seed y preservación de una sola asignación.
- `PlatformSettingsTests` de dominio y servicio: validación sin mutación, configuración versionada, evento y caché después del commit.
- `PlatformSettingsTests` de integración: permisos reales, validación, lectura actualizada y 409 sin evento adicional en PostgreSQL aislado.
- `PlatformTenantTests`: registro global inmutable/UTC y eventos con destino y operador.
- `PlatformTenantAdministrationTests`: paginado/filtros/búsqueda/contacto, 401/403/404, transiciones y 409, revocación Business con Personal operativo, y conteos actualizados sin lectura de tablas privadas.
