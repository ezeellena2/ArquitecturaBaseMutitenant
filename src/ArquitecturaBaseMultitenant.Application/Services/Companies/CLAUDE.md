Casos de uso de empresas y membresías company-scoped.

Leé `AGENTS.md`, `docs/features/organizaciones.md`, `docs/features/roles.md` y las reglas guardado, errores, validación, paginado, concurrencia, multitenancy, permisos y logs. Copiá `RoleService.cs` y `CompanyService.cs`. Cada escritura valida antes y usa una única UoW; el último `CompanyAdmin` activo se protege bajo lock.
