# ADR 0061 · Nadie da lo que no tiene

**Estado:** aceptada por el usuario el 2026-11-18 (pregunta 15 del traspaso 15). Toma la N1 de ArquitecturaBase (traspaso 14, `02-nucleo.md` §1, y su `docs/features/administracion.md`), adaptada a los tres alcances del multitenant: la organización, cada empresa y Plataforma. Programada en la B1 de la [segunda parte de la base](../plans/2026-11-19-etapa-base-de-la-12-segunda-parte.md): sus `Guard` y sus `Policy` están en la ficha [permisos](../rules/permisos.md). Nada de ArquitecturaBase pasó todavía su prueba integral, así que un detalle puede cambiar con el traspaso 16.

## Contexto

Hoy delegar «Administrar roles» equivale a ser Dueño. En la organización «Empresa A», Ana tiene `roles.manage` y `roles.assign`, pero no `settings.manage`. Igual puede armar el rol «Configurador» con `settings.manage` y dárselo a Kevin, o dárselo a ella misma con otra cuenta. Ningún servicio lo impide: `RoleService` controla solo los roles de sistema, y `OrganizationUserAdministratorGuard` cuida solo al último Dueño y al último Administrador.

Lo mismo pasa en los otros dos catálogos con delegación ([permisos](../rules/permisos.md)):
- quien tiene `company.members.manage` en una empresa le da a otro cualquier rol que valga en esa empresa;
- un operador con `platform.operators.manage` le da a otro cualquier permiso de Plataforma (`PlatformOperatorService`).

## Decisión

1. **Lo que tiene quien pide** son sus permisos efectivos en ese alcance, los mismos que mira el atributo de la ruta: los de sus roles más lo que esos incluyen (decisión 260 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md), traspaso 16). Se leen de la base dentro del límite del caso de uso, después de sus locks, y no de lo que el pedido ya memorizó. Cuentan solo los permisos del catálogo vigente: los de un módulo quitado no se comparan (B3 de ArquitecturaBase).
2. **En la organización:**
   - crear o editar un rol: cada permiso que se agrega o se quita tiene que tenerlo quien pide. Si no, 403 `Roles.Role.PermissionsNotHeld`. El nombre y la descripción no cuentan. Borrar no pide nada, porque solo se borra un rol sin personas;
   - dar o quitar roles, desde Usuarios, una invitación o una empresa: cada rol que se agrega o se quita tiene que tener solo permisos que quien pide tiene en ese lugar. Si no, 403 `Tenancy.Member.RolesNotHeld`;
   - desactivar, reactivar o quitar de la organización a una persona, o cambiarle los roles, pide tener todos sus permisos en la organización. Si no, 403 `Tenancy.Member.HasMorePermissions`. Sobre uno mismo siempre se cumple, y siguen las reglas de siempre (nadie se saca a sí mismo). Reenviar o revocar una invitación no pide nada;
   - el Dueño (`TenantAdmin`) tiene todo el catálogo de la organización: darlo o quitarlo pide tenerlo todo.
3. **En cada empresa:** quien tiene `company.members.manage` en ella solo suma a alguien con roles, o le cambia los roles, si esos roles tienen solo permisos que quien pide tiene en esa empresa, contando los de la organización como los cuenta `[HasCompanyPermission]`. Desactivar o quitar a alguien de la empresa pide tener todos sus permisos ahí. Los 403 son `Companies.Membership.RolesNotHeld` y `Companies.Membership.HasMorePermissions`. Vale igual para `CompanyAdmin`.
4. **En Plataforma:** quien tiene `platform.operators.manage` solo invita a un operador, o le cambia los permisos, con permisos que tiene. Si no, 403 `Platform.Operator.PermissionsNotHeld`. Suspender, reactivar o dar de baja a un operador pide tener todos sus permisos. Si no, 403 `Platform.Operator.HasMorePermissions`.
5. **Una sola regla.** Un solo helper por alcance la decide (un `*Guard`, de la lista cerrada de [capas-y-flujo](../rules/capas-y-flujo.md), que decide con su `*Policy` pura), y lo usan todos los casos de uso que dan, quitan o actúan sobre una cuenta. La misma `Policy` calcula `grantable` para la pantalla, y la reusan las acciones del asistente ([0068](0068-acciones-del-asistente.md)). Los roles se comparan por id. Los códigos de error son una propuesta: los fija quien programa, en el `<Entidad>Errors` de cada área.

## Lo que no cambia

- Las guardas del último Dueño, del último Administrador de empresa y del último operador con `platform.operators.manage`.
- Los roles de sistema siguen inmutables.
- El rol «User» de ArquitecturaBase no existe acá: lo personal es implícito y no se delega. No hay un `UserRoleAdminOnly`.
- La N25 de ArquitecturaBase (el administrador inicial recupera Admin en cada arranque) no aplica: `PlatformSeeder` crea el operador inicial solo si no hay ninguno.

## Consecuencias

- Los errores nuevos van en `Errors.resx` y `.en.resx`, y el front los traduce.
- La pantalla muestra el 403 con su texto. Desde la decisión 269 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md), además apaga con su motivo lo que no se puede dar, según `grantable` (segunda parte de la base, B1 y F5).
- Los tests que cambian roles con una cuenta que no es Dueña se ponen al día.

## Alternativas descartadas

- **Solo el Dueño asigna roles:** quita la delegación, que el producto ofrece.
- **Solo en la organización:** deja la misma escalada abierta por una empresa o por Plataforma.
- **Dejarlo como hoy:** delegar «Administrar roles» seguiría siendo dar el control entero.

## Verificación

Tests con cuentas que tienen solo cada permiso, en los tres alcances, como `DelegatedAdministrationTests` de ArquitecturaBase: armar un rol con un permiso ajeno, dar un rol con un permiso ajeno, desactivar a quien tiene más, y lo mismo por empresa y entre operadores. `RouteIsolationMatrixTests` no cambia.
