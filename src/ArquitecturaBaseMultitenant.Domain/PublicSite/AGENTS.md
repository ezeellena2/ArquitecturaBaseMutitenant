Entidades y reglas puras de las páginas públicas de organizaciones.
Leé la conducta de publicación y «No disponible» en [sitio público](../../../docs/features/sitio-publico.md).
Leé: [multitenancy](../../../docs/rules/multitenancy.md) · [concurrencia](../../../docs/rules/concurrencia.md) · [textos-libres](../../../docs/rules/textos-libres.md).
Copiá `Tenancy/Company.cs`; la tabla usa `IPublishedByBusiness` y pertenece a `public_site`.

Publicar recibe UTC explícito y guarda PublishedAtUtc al pasar de borrador a publicada; una publicación repetida conserva la evidencia. La columna histórica nullable no se rellena con fechas inventadas.
