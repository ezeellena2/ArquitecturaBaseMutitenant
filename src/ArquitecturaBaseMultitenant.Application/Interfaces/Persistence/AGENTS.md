Puertos de persistencia de Application: un límite transaccional por método público que escribe, repositorios y readers por entidad, scope de tenant y auditoría explícita. Sin EF ni SQL.

Antes de escribir, leé: [guardado](../../../../docs/rules/guardado.md) · [persistencia-ef](../../../../docs/rules/persistencia-ef.md) · [multitenancy](../../../../docs/rules/multitenancy.md).

Copiá de: `IUnitOfWork.cs` y `CommitPolicy.cs`; Infrastructure implementa estos contratos. `FakeUnitOfWorkTests` verifica la política de commit.
