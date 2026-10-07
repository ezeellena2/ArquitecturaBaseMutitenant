Rutas de la página y del logo del subdominio: `[PublicSite][AllowAnonymous]`, solo organización Business activa con página publicada y sin bloqueo.
El directorio y su logo por slug van en el dominio principal: `DirectoryController` lleva solo `[AllowAnonymous]`, sin `[PublicSite]`, y se agrega a la lista cerrada de `AccessDeclarationTests`.
Leé [sitio público](../../../../docs/features/sitio-publico.md), [API HTTP](../../../../docs/rules/api-http.md) y [multitenancy](../../../../docs/rules/multitenancy.md).
Copiá de: `PublicSiteController.cs` para lectura pública y `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/PublicPageController.cs` para la edición privada. Inyectá solo servicios de Application; nunca cambies el tenant del token.
`DirectoryController.cs` mapea `DirectoryQuery` al pedido paginado y solo devuelve los cuatro campos públicos del directorio. `DirectoryTests` verifica el listado y el logo con sus filtros de publicación.
