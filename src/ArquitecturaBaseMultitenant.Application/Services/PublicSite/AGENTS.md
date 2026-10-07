Casos de uso de la página pública de una organización.
La edición privada y la lectura del subdominio están en [sitio público](../../../../docs/features/sitio-publico.md); `PublicSiteService` solo lee publicación activa desde `IPublicSiteContext`.
`DirectoryService` valida el paginado con `IRequestValidator` y lee `IDirectoryReader`; su listado y logo son anónimos, sin resolver un subdominio. Los dos servicios usan Result y OperationLog, sin caché ni acceso privado.
Leé `docs/rules/guardado.md`, `result-y-errores.md`, `validacion.md`, `logs.md`, `multitenancy.md`, `concurrencia.md` y `auditoria.md`.
Copiá de: `src/ArquitecturaBaseMultitenant.Application/Services/Roles/RoleService.cs` para la escritura y `PublicSiteService.cs` para la lectura; logo y contenido viven en `public_site`.
