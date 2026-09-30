# ArquitecturaBaseMultitenant

Plantilla multitenant **B2B + B2C**: una persona tiene una sola cuenta con dos accesos que no se mezclan, **como persona** y **como empresa**, y cada organización puede tener su página pública en un subdominio. Trae los accesos y la mecánica; no trae módulos de negocio. Usa .NET 10, Aspire 13.5 y PostgreSQL. El front vive en `../ArquitecturaBaseMutitenantFront`.

## Documentación

- [`AGENTS.md`](AGENTS.md): índice de reglas y del arnés.
- [`docs/architecture/backend.md`](docs/architecture/backend.md): capas, patrones y carpetas.
- [`docs/guides/leer-backend.md`](docs/guides/leer-backend.md): cómo seguir un recorrido del controller a las reglas y la base.
- [`docs/architecture/multitenancy.md`](docs/architecture/multitenancy.md): organizaciones, empresas y aislamiento.
- [`docs/architecture/arbol.md`](docs/architecture/arbol.md): estructura objetivo, archivo por archivo.
- [`docs/rules/README.md`](docs/rules/README.md): fichas de reglas.
- [`docs/decisions/README.md`](docs/decisions/README.md): decisiones.
- [`docs/plans/2026-09-27-plan-de-desarrollo.md`](docs/plans/2026-09-27-plan-de-desarrollo.md): plan de desarrollo por etapas.
- [`docs/operations/configuracion.md`](docs/operations/configuracion.md): Google, Gmail, WhatsApp y secretos.

## Levantar, probar y desplegar

Estas secciones se completan a medida que avanza el plan: cómo levantar y probar, en la Etapa 0; cómo desplegar, en la Etapa 10.
