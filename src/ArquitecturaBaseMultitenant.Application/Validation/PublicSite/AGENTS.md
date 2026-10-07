Validadores de la edición de la página pública y del listado anónimo de organizaciones publicadas.
Leé [validación](../../../../docs/rules/validacion.md), [paginado y búsqueda](../../../../docs/rules/paginado-y-busqueda.md) y [sitio público](../../../../docs/features/sitio-publico.md).
Copiá de: `src/ArquitecturaBaseMultitenant.Application/Validation/Roles/ListRolesRequestValidator.cs` para el paginado. `ListDirectoryRequestValidator` hereda `PagedRequestValidator`, con name como único orden y los mensajes comunes de cada regla. El servicio recibe solo IRequestValidator.
