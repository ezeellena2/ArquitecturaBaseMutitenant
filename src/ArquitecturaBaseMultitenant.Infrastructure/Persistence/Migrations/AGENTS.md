Migraciones productivas de EF Core. Se generan en esta única carpeta; cada tabla de `tenant`, `public_site` o `engagement` recibe el helper RLS de su clase, y el SQL se revisa antes del commit.

Antes de editar, leé: [persistencia-ef](../../../../docs/rules/persistencia-ef.md) · [multitenancy](../../../../docs/rules/multitenancy.md) · [paginado-y-busqueda](../../../../docs/rules/paginado-y-busqueda.md).

Copiá de: [docs/guides/migracion.md](../../../../docs/guides/migracion.md). Lo verifican `MigrationsTests`, `RlsPolicyInventoryTests` y `SortIndexTests` (E2).
