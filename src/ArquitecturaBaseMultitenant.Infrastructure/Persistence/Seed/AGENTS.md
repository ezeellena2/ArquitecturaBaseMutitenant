Seed de datos globales de referencia. Lee los cinco JSON generados, hace upsert en las once tablas de `platform`, deshabilita códigos y asociaciones retirados y nunca borra filas.

Antes de editar, leé: [datos-de-referencia](../../../../docs/rules/datos-de-referencia.md) · [datos-de-referencia.md §§2–4](../../../../docs/architecture/datos-de-referencia.md#2-las-tablas-esquema-platform-globales-sin-rls) · [tests](../../../../docs/rules/tests.md).

Copiá de: `Infrastructure/Persistence/Seed/ReferenceDataSeeder.cs` y `Infrastructure/Persistence/Seed/ReferenceData/currencies.json` como muestra de las fuentes generadas. Lo verifica `Api.IntegrationTests/Persistence/ReferenceDataSeederTests.cs`.
