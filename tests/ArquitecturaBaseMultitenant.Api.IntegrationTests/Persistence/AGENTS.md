Tests del modelo, migraciones y comportamiento real de persistencia. Los tests de modelo sin consultas no requieren Docker; los de barreras sí.
Leé: [tests](../../../docs/rules/tests.md) · [persistencia-ef](../../../docs/rules/persistencia-ef.md) · [multitenancy](../../../docs/rules/multitenancy.md).
Copiá de `DbContextModelTests.cs`; para RLS usá Testcontainers y la fixture de aislamiento.
