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
- Una URL [externa](https://ejemplo.com/x.md) y un `placeholder/<tema>.md`.
