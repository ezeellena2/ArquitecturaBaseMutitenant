Validación del modelo y plantillas SQL de aislamiento. Cada entidad mapeada debe ser global explícita o tener clase, esquema y filtro correctos.
Antes de editar, leé: [multitenancy](../../../../docs/rules/multitenancy.md) · [persistencia EF](../../../../docs/rules/persistencia-ef.md) · [tests](../../../../docs/rules/tests.md).
Copiá de: `TenantIsolationModelValidator.cs`, `RlsSql.cs` y `RlsMigrationBuilderExtensions.cs`.
Lo verifican: `Api.IntegrationTests/Persistence/ModelFiltersTests.cs` y `RlsSqlTests.cs`; desde T20, `Tenancy/RlsPolicyInventoryTests.cs`.
