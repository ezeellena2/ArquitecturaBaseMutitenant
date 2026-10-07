Pruebas HTTP y de reader del contenido publicado: 404 uniforme, logo y ETag, borrador del propio miembro y token de B en subdominio de A.
`DirectoryTests` verifica publicación activa, búsqueda por nombre, paginado y cuatro campos exactos; el logo aplica los mismos filtros incluso al revalidar. Usá un prefijo único de nombre y Search por caso para que la base compartida no altere los totales; no borres datos de otra clase.
Leé [sitio público](../../../docs/features/sitio-publico.md), [tests](../../../docs/rules/tests.md) y [multitenancy](../../../docs/rules/multitenancy.md).
Copiá de: `PublicSiteTests.cs` para datos en una UoW real y `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/SubdomainTests.cs` para resolución del host; inicializá Business antes de insertar una página.
