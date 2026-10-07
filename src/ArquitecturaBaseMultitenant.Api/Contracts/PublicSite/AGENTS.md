Contratos HTTP del sitio público: el correo legible y el teléfono E.164 se mapean desde sus value objects; ningún id ni estado privado sale en la página.
`DirectoryQuery` recibe page, pageSize, sort y search; el controller los mapea a `ListDirectoryRequest`. Cada tarjeta sale como `DirectoryEntryRow` de Application, con slug, name, description y logoUrl.
Leé [sitio público](../../../../docs/features/sitio-publico.md) y [API HTTP](../../../../docs/rules/api-http.md).
Copiá de: `PublicSiteHttpResponse.cs` y `src/ArquitecturaBaseMultitenant.Api/Contracts/Organization/PublicPageHttpRequests.cs` para los mapeos HTTP.
