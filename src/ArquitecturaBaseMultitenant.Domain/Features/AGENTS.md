Catálogo de módulos opcionales y habilitaciones globales por espacio. No contiene módulos de ejemplo ni lógica de EF.
Leé docs/rules/modulos-habilitados.md, fechas-y-zonas.md, result-y-errores.md, concurrencia.md y persistencia-ef.md.
TenantFeature es global (platform), no ITenantOwned: Plataforma administra planes, habilitaciones y pruebas; un módulo no sustituye un permiso.

ExpiresOn conserva la fecha civil que recibió Plataforma; ExpiresAtUtc conserva el instante técnico exclusivo para gates y auditoría. Legacy sin fecha civil sigue null, sin inferirla desde UTC.
