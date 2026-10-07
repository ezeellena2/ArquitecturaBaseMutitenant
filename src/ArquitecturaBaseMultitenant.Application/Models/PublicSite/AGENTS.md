Modelos de la página pública: el borrador de la organización y las proyecciones del sitio. `PublicSiteRow` identifica el sitio publicado y activo con BusinessTenantId y Slug; no es un acceso privado ni contiene una entidad EF.

Antes de editar, leé: [multitenancy](../../../../docs/rules/multitenancy.md) · [api-http](../../../../docs/rules/api-http.md).
La respuesta publicada y sus límites están en [sitio público](../../../../docs/features/sitio-publico.md). `PublicSiteResponse` conserva Email y PhoneNumber; su salida HTTP omite ids, versión y estado.
`ListDirectoryRequest` hereda `PagedRequest` y admite solo name como orden. `DirectoryEntryRow` contiene únicamente slug, name, description y logoUrl; no se crea otra carpeta de modelos de lectura.

Copiá de: `PublicSiteRow.cs` para identificar un sitio y `PublicPageModels.cs` para los datos del borrador. `SubdomainTests` comprueba la resolución pública y su independencia del token.
