# Roles

El servicio es [`RoleService`](../../src/Demo.Application/Services/Roles/RoleService.cs) y se prueba en `tests/Demo.ArchitectureTests/HarnessStage.cs`.
También en `Application/Services/Roles/RoleService.cs` (con la convención de capa) y en `Services/Roles/RoleService.cs` (parcial).

Desvíos plantados:
- Ver [una ficha rota](../rules/no-existe.md) que no tiene reemplazo.
- Movido: `src/Demo.Application/Services/RoleService.cs` pasó a otra carpeta.
- Ambiguo: `Services/Helper.cs` aparece en dos carpetas con otro prefijo.
- Ancla: [sección](../rules/guardado.md#no-esta) que no existe.
- Ancla buena: [regla](../rules/guardado.md#regla).
- Planeado: [la receta](../guides/agregar-un-area.md) (E4) todavía no existe.
- Un rango abierto E1–E5: `docs/features/futuro.md`.
- Símbolos: `RolleService` con un typo, `RolFantasmaService` que no existe y `RoleService.Borrar` que no es un miembro; `RoleService.CreateAsync` sí.
- Catálogos: `roles.view` existe y `roles.vew` no; con dos puntos, `company.members.read` existe y `company.members.reed` no; `Roles.Role.NameRequired` existe y `Roles.Role.NameReqired` no; `GET/POST /api/roles` existe, `POST /api/rolez` no y `DELETE /api/roles` no admite ese método.
- Una URL [externa](https://ejemplo.com/x.md) y un `placeholder/<tema>.md`.
