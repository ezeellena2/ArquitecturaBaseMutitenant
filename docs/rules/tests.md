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
- Para aislar `Widget`, `Poster` y `Deal`, la fixture aplica primero las migraciones reales y después llama a `IsolationSchema.ApplyAsync(connection)` del proyecto de tests. Ese helper crea sus tablas con SQL generado por las mismas plantillas de `RlsSql`: `EnableTenantRls`, `EnablePublicRls` y `EnablePartiesRls`. Estas tablas no llevan migraciones productivas.
- `TestContext.Current.CancellationToken` en lugar de `CancellationToken.None`.
- Desde E3a, la puerta incluye `npm run test:e2e:real` del front con Aspire Development: navegador Playwright, front y Api reales, Postgres migrado y códigos leídos de `.eml` en un pickup temporal. Recorrido obligatorio: registro nuevo → Personal → logout → Ana por `/login/empresa` → Empresa A → F5 conserva Empresa A → Perfiles/Personal → logout. No hay mocks ni intercepción de red en esta prueba. `E2E_PICKUP_DIR` apunta al directorio absoluto configurado como `Email:PickupDirectory`; `E2E_ANA_EMAIL` coincide con `Seed:Development:AnaEmail`. El runner no imprime códigos, correos ni secretos. Se ejecuta en cada etapa desde E3a, aunque sus tests unitarios e integrados estén verdes.

## Prohibido
- Moq, NSubstitute, FluentAssertions.
- Tests que dependen del orden o del reloj real.
- Saltear el test de aislamiento de una ruta de negocio.
- Sustituir el recorrido Playwright real por dobles de API, sesión, correo o base.

## Copiá de
- `Application.UnitTests/Services/Roles/RoleServiceWriteTests.cs` y `Api.IntegrationTests/Organization/RolesTests.cs` (E4)

## Lo verifica
- CI (E0): build + test en cada push; chequeo de contratos desde la E1. `HarnessTests` (E0) exige cada clase C# `*Tests` con un método `[Fact]`/`[Theory]` y los archivos `.test.mjs`/`.test.ts[x]` de «Lo verifica» en la etapa cerrada, también en el front si está el checkout.
- `ExplicitRouteInventoryTests` (E1): una ruta sin su fila falla.
- `scripts/test-e2e-real.test.mjs` del front (E3): desde la 3a, la etapa no cierra si el recorrido real no pasa.

## Detalle
[backend.md §4.6](../architecture/backend.md#46-tests)
