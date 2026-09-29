Marcá con `[Idempotent]` cada POST que crea o envía. `IdempotencyFilter` aplica reserva, replay y errores ProblemDetails antes y después de ejecutar la acción.
Leé `docs/rules/idempotencia.md` y `docs/architecture/backend.md` §20.
Las guardas son `tests/ArquitecturaBaseMultitenant.ArchitectureTests/IdempotentActionsTests.cs` y `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Api/IdempotencyTests.cs`.
