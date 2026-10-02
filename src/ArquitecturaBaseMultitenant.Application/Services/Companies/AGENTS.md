Servicios de empresas y membresías por empresa.

Leé `docs/features/organizaciones.md`, `docs/features/roles.md` y las fichas guardado, result-y-errores, validacion, paginado-y-busqueda, concurrencia, multitenancy, permisos y logs.

Copiá `../Roles/RoleService.cs` para validación, UoW única y concurrencia; `CompanyService.cs` para el alcance tenant. Los cambios que puedan quitar `CompanyAdmin` usan `ILastCompanyAdminGuard` bajo lock transaccional.
