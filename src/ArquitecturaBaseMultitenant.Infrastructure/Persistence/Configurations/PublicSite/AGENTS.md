Configuraciones de páginas públicas en el esquema `public_site`: clasificalas como `IPublishedByBusiness` y aplicá el filtro `Public` con el helper de persistencia.
Leé [sitio público](../../../../../docs/features/sitio-publico.md); el filtro Public permite el borrador a su dueño, por eso la lectura pública exige publicación y Business activo explícitamente.

Leé: [persistencia-ef](../../../../../docs/rules/persistencia-ef.md) · [multitenancy](../../../../../docs/rules/multitenancy.md) · [auditoria](../../../../../docs/rules/auditoria.md). Copiá de `../Tenant/RoleConfiguration.cs` para configuración y de `PublicPageConfiguration.cs` para publicación.
