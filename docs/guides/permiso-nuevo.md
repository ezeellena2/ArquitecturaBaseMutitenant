# Permiso nuevo

Usá esta receta para agregar un permiso a un área o crear el catálogo de otra. Las rutas piden permisos, no roles. La referencia es [Roles](../features/roles.md); las reglas canónicas están en [permisos](../rules/permisos.md) y en [AGENTS.md](../../AGENTS.md).

1. **Declaralo en Domain.** Agregá `Permissions.<Area>.<Action>` con nombre en minúsculas y un punto, por ejemplo `catalog.read`. Sumalo a `Permissions.All` en el orden en que debe aparecer en el selector de roles.
2. **Traducilo.** En `Application/Resources/Permissions.resx` y `Permissions.en.resx`, agregá `Permission.<código>` y `PermissionDescription.<código>`. Para un área nueva, agregá también `Area.<prefijo>`. Usá voseo en español y mantené las claves de ambos idiomas en paridad.
3. **Pedilo en la ruta.** Usá `[HasPermission(Permissions.<Area>.<Action>)]` para organización, `[HasCompanyPermission]` para empresa y `[HasPlatformPermission]` para plataforma. No compares roles o claims dentro del controller.
4. **Calculalo desde el servidor.** Los permisos de organización salen de las asignaciones activas del tenant de `tenant_id`; nunca aceptes el tenant desde el cuerpo o la URL. La plataforma conserva su permiso específico.
5. **Invalidá si cambió un rol.** `RoleService` invalida la caché de permisos después de confirmar el commit. Agregar una constante al catálogo no pide invalidación.
6. **Probalo antes de cerrar.** Actualizá el test de catálogo del Domain, `PermissionTextsTests`, el orden de grupos en `RoleServiceTests` y el test de autorización HTTP. Si el área es nueva, cubrí el 403 por permiso ausente y el 404 de tenant ajeno; verificá el catálogo traducido con integración.

No cambies la seed global para inventar usuarios u organizaciones. Los roles de sistema se preparan por tenant cuando una organización real nace; los tests crean los suyos en su propia base.
