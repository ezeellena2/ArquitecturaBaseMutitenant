Casos de uso de Usuarios: listado, ficha, invitaciones, roles y empresas de cada persona, estado, salida de la organización y sus pendientes de «Para resolver».
Leé `docs/features/organizaciones.md` (Usuarios e invitaciones), `docs/features/roles.md`, `docs/features/inicio.md` y las fichas guardado, result-y-errores, validacion, paginado-y-busqueda, concurrencia, multitenancy, permisos, datos-personales y logs.
`OrganizationUserService` abre la transacción en cada método que escribe; sus helpers (Editor, Writer, Remover, Guard, Notifier) trabajan adentro, sin `IUnitOfWork`. A la última Dueña la protege `ILastTenantAdminGuard`.
Copiá de: `Services/Roles/RoleService.cs` para validar afuera, abrir una transacción por método público y controlar la versión.
