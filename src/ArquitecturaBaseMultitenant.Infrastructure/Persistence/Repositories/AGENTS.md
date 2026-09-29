Repositorios concretos de escritura. `AuditLog` agrega eventos explícitos al ChangeTracker dentro del límite de UnitOfWork; el guardado ocurre una sola vez junto con el cambio del caso de uso.

Antes de escribir, leé: [auditoría](../../../../docs/rules/auditoria.md) · [guardado](../../../../docs/rules/guardado.md) · [persistencia EF](../../../../docs/rules/persistencia-ef.md).

Copiá de `AuditLog.cs`; lo prueba `Api.IntegrationTests/Persistence/AuditTrailTests.cs`.
