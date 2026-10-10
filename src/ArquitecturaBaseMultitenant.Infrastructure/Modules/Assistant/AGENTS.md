Persistencia del Asistente por contribución explícita: configuración en Persistence/Configurations, repositorios con propietario obligatorio y readers con proyecciones privadas. Cada global se declara por tipo y esquema; las entidades ITenantOwned usan tenant y heredan filtros/RLS del núcleo.

Leé docs/features/asistente.md, docs/rules/modulos.md, docs/rules/persistencia-ef.md, docs/rules/multitenancy.md y docs/rules/auditoria.md. No Identity en readers/repositorios, no IgnoreQueryFilters ni acceso administrativo a contenido.
