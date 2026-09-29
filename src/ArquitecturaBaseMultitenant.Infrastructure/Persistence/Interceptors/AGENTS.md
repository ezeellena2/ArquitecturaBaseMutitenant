Interceptores EF de persistencia. `TenantConnectionInterceptor` fija `app.tenant_id` al abrir cada conexión y `TenantStampInterceptor` sella la columna de la parte activa antes de guardar. `SoftDeleteInterceptor`, `AuditableEntityInterceptor` y `AuditTrailInterceptor` se ejecutan en ese orden y dejan el cambio y el rastro en el mismo guardado. El subdominio nunca autoriza datos privados.

Antes de escribir, leé: [multitenancy](../../../../docs/rules/multitenancy.md) · [persistencia-ef](../../../../docs/rules/persistencia-ef.md) · [auditoria](../../../../docs/rules/auditoria.md) · [tests](../../../../docs/rules/tests.md).

Copiá de: `TenantStampInterceptor.cs` y `AuditTrailInterceptor.cs`; los prueban `Api.IntegrationTests/Persistence/TenantStampTests.cs`, `TenantConnectionTests.cs`, `SoftDeleteTests.cs`, `AuditingTests.cs` y `AuditTrailTests.cs`.
