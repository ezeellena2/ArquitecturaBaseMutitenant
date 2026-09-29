Interceptores EF de persistencia. `TenantConnectionInterceptor` fija `app.tenant_id` al abrir cada conexión y `TenantStampInterceptor` sella la columna de la parte activa antes de guardar. El subdominio nunca autoriza datos privados.

Antes de escribir, leé: [multitenancy](../../../../docs/rules/multitenancy.md) · [persistencia-ef](../../../../docs/rules/persistencia-ef.md) · [tests](../../../../docs/rules/tests.md).

Copiá de: `TenantStampInterceptor.cs`; lo prueban `Api.IntegrationTests/Persistence/TenantStampTests.cs` y `TenantConnectionTests.cs`.
