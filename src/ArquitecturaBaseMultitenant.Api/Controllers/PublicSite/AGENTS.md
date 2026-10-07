Rutas de la página y del logo del subdominio: `[PublicSite][AllowAnonymous]`, solo organización Business activa con página publicada y sin bloqueo.
Leé [sitio público](../../../../docs/features/sitio-publico.md), [API HTTP](../../../../docs/rules/api-http.md) y [multitenancy](../../../../docs/rules/multitenancy.md).
Copiá de: `PublicSiteController.cs` para lectura pública y `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization/PublicPageController.cs` para la edición privada. Inyectá solo servicios de Application; nunca cambies el tenant del token.
