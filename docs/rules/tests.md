# Tests

**Regla:** TDD donde hay lógica. Cada pieza tiene su test en el proyecto y la carpeta equivalentes. Nada se da por terminado sin `dotnet build` sin advertencias y `dotnet test` en verde.

## Cómo se hace
| Qué | Dónde | Con qué |
|---|---|---|
| Regla de una entidad o value object | `Domain.UnitTests/<Área>/` | puro |
| Servicio | `Application.UnitTests/Services/<Área>/` | `ServiceFixture`: `FakeUnitOfWork`, `FakeTimeProvider`, `FakeLogger`, repositorios `InMemory*` |
| Ruta, reader, RLS | `Api.IntegrationTests/<Área>/` y `Tenancy/` | `ApiFactory` (Testcontainers), `AuthFlow` o `TestAuthHandler`, `TenantFixture` |
| Regla de arquitectura | `ArchitectureTests/` | NetArchTest + Mono.Cecil |

- **Nombres:** en inglés y como frase: `Deleted_rows_are_hidden_from_queries_and_endpoints`.
- **Ruta nueva:** fila en `ExplicitRouteInventoryTests`, test de éxito, errores, 401/403 y **un test de aislamiento** (el id de otro tenant da 404).
- **Datos de prueba:** se preparan dentro de un límite real (`factory.InTransactionAsync`).
- Lo que existe solo para probar va en `TestFeatures/`, nunca en `src/`.
- `TestContext.Current.CancellationToken` en lugar de `CancellationToken.None`.

## Prohibido
- Moq, NSubstitute, FluentAssertions.
- Tests que dependen del orden o del reloj real.
- Saltear el test de aislamiento de una ruta de negocio.

## Copiá de
- `Application.UnitTests/Services/Roles/RoleServiceWriteTests.cs` y `Api.IntegrationTests/Organization/RolesTests.cs` (E4)

## Lo verifica
- CI: build + test en cada push.
- `ExplicitRouteInventoryTests`: una ruta sin su fila falla.

## Detalle
[backend.md §4.6](../architecture/backend.md#46-tests)
