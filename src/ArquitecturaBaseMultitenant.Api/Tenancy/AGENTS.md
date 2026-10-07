Adaptadores HTTP de los accesos y del sitio público. `TenantResolutionMiddleware` valida claims y estados antes de fijar el contexto privado. `PublicSiteResolutionMiddleware` resuelve un único slug sobre el host y puerto del issuer solo para rutas `[PublicSite]`: el sitio publicado y activo queda en `IPublicSiteContext`, sin tocar `ITenantContext` y sin caché.

Antes de editar, leé: [multitenancy](../../../docs/rules/multitenancy.md) · [permisos](../../../docs/rules/permisos.md) · [api-http](../../../docs/rules/api-http.md).

Copiá de: `AccessAttribute.cs` y `AccessFilter.cs` para un filtro de acceso; `PublicSiteAttribute.cs` y `PublicSiteFilter.cs` para exigir sitio público. `SubdomainTests`, `AccessTests` y `SuspensionTests` verifican hosts, aislamiento y estados.
