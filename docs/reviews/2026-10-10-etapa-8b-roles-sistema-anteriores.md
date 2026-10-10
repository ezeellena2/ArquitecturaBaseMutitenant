# E8b · Upgrade de roles de sistema anteriores

Fecha: 2026-10-10. Correctivo A1 compartido con E8/E5, autorizado junto con E8b T02–T09 y su migración. Base de despacho: `8107721dba90804820cec6235e7d925fedbd7712`. La publicación corresponde a Root, en main sin push; este informe no cierra la etapa ni reemplaza su puerta final sobre corte fijo.

## Resultado

Las organizaciones creadas antes de los permisos del Asistente podían responder `409 Tenancy.Tenant.ProvisioningConflict` al aprobar o reintentar su alta: sus roles persistidos no coincidían con `SystemRoles.CreateDefaults`. La nueva migración de datos `20261010203618_SystemRoleAssistantPermissions`, generada con CLI de EF, agrega únicamente los tokens faltantes: `assistant.use` y `assistant.configure` a `TenantAdmin` de alcance `Organization`, y sólo `assistant.use` a `CompanyAdmin` de alcance `AnyCompany`. Ambos deben ser de sistema, sin empresa y pertenecer a una organización `Business`.

No cambia el modelo EF, el snapshot, las rutas, los contratos HTTP, `Role.cs` ni las validaciones del aprovisionador. Se conservan los demás permisos, incluso sus duplicados, IDs, alcances, empresas, asignaciones, miembros y configuración. Los roles personalizados, los que coinciden sólo por nombre y los espacios Personal permanecen intactos. Las corrupciones ajenas al upgrade siguen respondiendo `ProvisioningConflict`.

`RolePermissionCache` usa `t:{tenantId}:role-permissions:v2:{roleId}` con ids en formato `N`, mediante la misma función para lectura e invalidación. La clave anterior deja de participar en nuevas lecturas.

## Decisiones tomadas

- La migración conserva RLS forzada. Ejecuta un loop de todos los Business, sin filtrar estados; fija `app.tenant_id` por organización y restaura el valor anterior. Se copia el patrón de `MemberAccessRoleProjection`; no se habilita SUPERUSER/BYPASSRLS ni se deshabilitan filtros o políticas.
- El SQL agrega sólo tokens ausentes mediante append; no usa `DISTINCT`, no reemplaza toda la plantilla y no deduplica ni repara otros permisos. Es una operación técnica de migración, no una nueva vía de edición de roles de sistema.
- El cambio de namespace de caché evita reutilizar entradas anteriores sin introducir epochs, tablas, workers, Redis ni referencias al módulo desde el núcleo. La migración debe aplicarse antes de aceptar tráfico y todas las instancias del despliegue anterior deben reiniciarse. El SQL no invalida su L1 ni ejecuta la invalidación postcommit de `RoleService`.
- `Down` no modifica datos: quitar indiscriminadamente los tokens destruiría permisos que ya existían antes de `Up`. Es rollback de historia de migración, no una reversión física completa de datos. Los binarios anteriores pueden rechazar las plantillas aditivas; el rollback exige mantener un corte compatible o preparar una migración de compensación revisada.
- M2 debe normalizar explícitamente los permisos de sistema al retirar Asistente en la copia descartable, mediante su migración nueva de retirada. Retirar constantes y tablas no elimina tokens persistidos. La nueva clase de integración depende del catálogo del Asistente y se incluye en el inventario de extracción; el caso de caché contiene comprobaciones neutrales que M2 debe conservar al separar la prueba.

## Verificación y contadores originales

| Verificación focal | Resultado original | Resultado aceptado |
|---|---|---|
| RED conducta: dos casos `Legacy*` con Docker | 2 ejecutados, 2 errores, exit 2 | RED real: permisos anteriores sin upgrade y entrada legacy de caché |
| Cohorte propia A1 | 7 ejecutados, 6 correctos, 1 error, exit 2 | 6 casos conservados sin cambios |
| Reemplazo del único caso de caché | 1 ejecutado, 1 correcto, exit 0 | 7/7 casos distintos: 6 conservados + 1 reemplazo |
| Regresión HTTP/persistencia/RLS | 32 ejecutados, 32 correctos, exit 0 | 32/32 |
| Domain: roles y asignaciones | 25 ejecutados, 25 correctos, exit 0 | 25/25 |
| Application: servicios de Roles y textos de permisos | 72 ejecutados, 72 correctos, exit 0 | 72/72 |
| Arquitectura: capas, referencias, filtros, UoW, módulos, clasificación y permisos | 26 ejecutados, 26 correctos, exit 0 | 26/26 |

Estos son filtros distintos, no una suite completa. No se suman reejecuciones ni el reemplazo de caché como casos adicionales. El fallo intermedio de caché era una expectativa de orden: `RoleReader.ListPermissionsForRoleAsync` ordena en Ordinal, mientras `Permissions.All` conserva el orden del catálogo. Se corrigió el esperado con `Order(StringComparer.Ordinal)`, sin deduplicar, y se repitió únicamente ese caso.

La prueba principal usa su propia `ApiFactory` y PostgreSQL descartable. Prepara dos Business reales, pendiente y activo, con dos roles de sistema, dos asignaciones, un miembro, una empresa, una membresía empresarial, una configuración y un rol personalizado adicional con permisos duplicados. También prepara un Personal técnico con un rol de sistema anterior. Comprueba conflictos HTTP reales antes del upgrade y éxito de approve/retry después, repetición sin duplicar, versiones reales y stale 409; los snapshots de los datos privados conservan IDs y contenido. Verifica tokens preexistentes de una actualización parcial, mt_owner NOSUPERUSER/NOBYPASSRLS, RLS habilitada y forzada, restauración del scope anterior, Up/Down/Up y vuelta a latest sin cambios de modelo pendientes. Las cinco variantes de corrupción son permiso faltante ajeno, duplicado ajeno, alcance incorrecto, empresa incorrecta y rol no system con nombre reservado.

El caso de caché precarga la key legacy, confirma lectura actual bajo RLS, comprueba que la key actual se mantiene hasta invalidarla, recarga datos nuevos después de invalidación y conserva aislado el mismo roleId consultado desde otra organización. La entrada legacy queda intacta y no participa en esas lecturas.

Los builds seriales de API/Infrastructure y los proyectos de tests afectados terminaron con 0 advertencias y 0 errores. EF generó sólo Migration y Designer; el snapshot permaneció igual. Se revisó íntegro el SQL idempotente generado y `has-pending-model-changes` indicó que no hay cambios desde la última migración.

## Comandos y evidencia

Los comandos completos, filtros, exit codes, duraciones y hashes de cada log están en `.local/reviews/e8b-contract/e8b-system-role-upgrade-files.json`. Principales comandos:

```text
dotnet build tests/ArquitecturaBaseMultitenant.Api.IntegrationTests --no-restore -m:1 -nr:false
dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests --no-build -- --filter-class '*SystemRolePermissionUpgradeTests'
dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests --no-build -- --filter-class '*SystemRolePermissionUpgradeTests' --filter-method '*Legacy_cache*'
dotnet ef migrations add SystemRoleAssistantPermissions --no-build --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output-dir Persistence/Migrations -- --environment Development --Logging:EventLog:LogLevel:Default None
dotnet ef migrations script 20261010174115_WhatsAppProtectedCodeQuota 20261010203618_SystemRoleAssistantPermissions --idempotent --no-build --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output .local/reviews/e8b-contract/a1-migration.sql -- --environment Development --Logging:EventLog:LogLevel:Default None
dotnet ef migrations has-pending-model-changes --no-build --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api -- --environment Development --Logging:EventLog:LogLevel:Default None
```

Se conservan también los intentos no aceptados: MSBuild paralelo en sandbox falló sin diagnósticos y sin casos; el serial en sandbox fue interrumpido al quedar bloqueado por IPC; el primer build elevado detectó una directiva `using` innecesaria de la prueba; la primera ejecución de integración tuvo 6 errores de fixture por redirecciones automáticas del cliente. Ninguno constituye RED de conducta. Los builds/tests útiles se ejecutaron fuera del sandbox para IPC/Docker, con builds seriales.

Los builds de API regeneraron el delta de ejemplos B7 del OpenAPI. Se preservan sus bytes y diff en `a1-generated-openapi.json` y `a1-generated-openapi.diff`; después de cerrar todos los builds se restauró el contrato congelado desde el blob de la base, usando Buffer de `git show`, `maxBuffer` de 64 MiB y `fs.writeFileSync`. No se usó reset/checkout ni se exportó un contrato intermedio para publicación. M3 conserva el único export final.

## Límites y siguiente paso

No se ejecutaron suites completas, Aspire, E2E, galería, recorrido ni retirada física M2; tampoco transportes Meta/Anthropic. Los procesos propios de build/tests están cerrados. Fuentes entregadas inmóviles, sin stage, commit ni push por el implementador. Root audita y publica el alcance propio antes de continuar con M2/M3 y la puerta completa final.
