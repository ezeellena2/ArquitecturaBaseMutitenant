Modelos de la página pública: el borrador de la organización y las proyecciones del sitio. `PublicSiteRow` identifica el sitio publicado y activo con BusinessTenantId y Slug; no es un acceso privado ni contiene una entidad EF.

Antes de editar, leé: [multitenancy](../../../../docs/rules/multitenancy.md) · [api-http](../../../../docs/rules/api-http.md).

Copiá de: `PublicSiteRow.cs` para identificar un sitio y `PublicPageModels.cs` para los datos del borrador. `SubdomainTests` comprueba la resolución pública y su independencia del token.
