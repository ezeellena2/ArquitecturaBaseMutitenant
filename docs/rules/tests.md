# Tests

**Regla:** TDD donde hay lógica. Cada conducta con reglas propias tiene un test en el proyecto y la carpeta equivalentes; los DTO, la documentación y las delegaciones simples no necesitan un test que replique su implementación. Durante la etapa se verifican los cambios coherentes con tests dirigidos y build del proyecto afectado; `dotnet build` de la solución sin advertencias y `dotnet test` completos son puerta de cierre de etapa y de CI.

## Cómo se hace
- Para un cambio de lógica, observar rojo por la conducta ausente, implementar, repetir el test focal en verde y commitear el cambio coherente. Los pasos internos no necesitan un commit ni una suite completa cada uno.
- Si la tarea cambia una ruta, persistencia, autenticación o aislamiento, al terminarla (no antes de cada commit) se corren los tests de integración y de seguridad de esa área. Las pruebas con Testcontainers usan Docker. La suite completa corre una vez, en la puerta.
- Para documentación, capturas o configuración sin lógica, usar la comprobación pertinente; no escribir un test que solo repita el cambio.

| Qué | Dónde | Con qué |
|---|---|---|
| Regla de una entidad o value object | `Domain.UnitTests/<Área>/` | puro |
| Servicio | `Application.UnitTests/Services/<Área>/` | `ServiceFixture`: `FakeUnitOfWork`, `FakeTimeProvider`, `FakeLogger`, repositorios `InMemory*` |
| Ruta, reader, RLS | `Api.IntegrationTests/<Área>/` y `Tenancy/` | `ApiFactory` (Testcontainers), `AccountJourney` u `OrganizationJourney` (registro e ingreso reales), `TenantFixture` |
| Regla de arquitectura | `ArchitectureTests/` | NetArchTest + Mono.Cecil |

- **Nombres:** en inglés y como frase: `Deleted_rows_are_hidden_from_queries_and_endpoints`.
- **Ruta nueva:** fila en `ExplicitRouteInventoryTests`, test de éxito, errores, 401/403 y **un test de aislamiento** (el id de otro tenant da 404).
- **Datos de prueba:** se preparan dentro de un límite real (`factory.InTransactionAsync`).
- Lo que existe solo para probar va en `TestFeatures/`, nunca en `src/`.
- `ApiFactory` arranca con `ValidateOnBuild`, como Development: una clase que pide algo sin registrar rompe el arranque de toda la suite. `ValidateScopes` queda apagado porque hay pruebas que leen servicios scoped desde la raíz.
- La configuración base de `ApiFactory` fija `Email:Delivery=PickupDirectory`, `WhatsApp:PhoneNumberId` vacío, `Seed:PlatformOwner:Phone` vacío y Google deshabilitado (`ClientId`/`ClientSecret` vacíos), también para hosts Development: no heredan transportes externos, Google ni el Phone del operador local. Una fixture puede sobrescribirlos explícitamente; los tests del proveedor declaran sus valores sintéticos y los del seed y del módulo verifican sus overrides. Los hosts Development de SPA/documentación usan `UseDevelopmentTestOwner` para el operador sintético.
- Para aislar `Widget`, `Poster` y `Deal`, la fixture aplica primero las migraciones reales y después llama a `IsolationSchema.ApplyAsync(connection)` del proyecto de tests. Ese helper crea sus tablas con SQL generado por las mismas plantillas de `RlsSql`: `EnableTenantRls`, `EnablePublicRls` y `EnablePartiesRls`. Estas tablas no llevan migraciones productivas.
- `ApiFactory` configura un pickup `.eml` único por fixture bajo el directorio temporal del sistema y lo borra al terminar; los tests leen los códigos mediante `Support/PickupCodeReader`, filtrando por destinatario y por el párrafo OTP de seis dígitos propio de la plantilla de prueba. Un número dentro de una URL de invitación no es el código. Se consume sólo el archivo Authentication seleccionado. Los lectores de avisos del outbox no descifran payloads vacíos ya purgados al enviar.
- `Tenancy/SuspensionJourneyTests` ejerce estado de organización, membresía y cuenta en base real, cachés reales, token B2B, ruta protegida, `/api/me` y refresh.
- `TestContext.Current.CancellationToken` en lugar de `CancellationToken.None`.
- Desde E3a, la puerta incluye `npm run test:e2e:real` del front: navegador Playwright, front y Api reales, una base PostgreSQL migrada exclusiva del E2E y códigos leídos de `.eml` en un pickup temporal. El runner crea allí la persona y la organización que necesita. Recorrido obligatorio: registro nuevo → Personal → logout → cuenta propia del E2E por `/login/empresa` → `/org` → F5 conserva el acceso empresa → Perfiles/Personal → logout. No hay mocks ni intercepción de red en esta prueba. `E2E_PICKUP_DIR` apunta al directorio absoluto configurado como `Email:PickupDirectory`. La prueba no lee ni modifica la base Development de Aspire y no imprime códigos, correos ni secretos. Se ejecuta en cada etapa desde E3a, aunque sus tests unitarios e integrados estén verdes.

## Prohibido
- Moq, NSubstitute, FluentAssertions.
- Tests que dependen del orden o del reloj real.
- Saltear el test de aislamiento de una ruta de negocio.
- Sustituir el recorrido Playwright real por dobles de API, sesión, correo o base.

## Copiá de
- `Application.UnitTests/Services/Roles/RoleServiceWriteTests.cs` y `Api.IntegrationTests/Organization/RolesTests.cs` (E4)

## Lo verifica
- CI (E0): build + test en cada push; desde la E1 falla si `docs/contracts/openapi.json` no está al día. La comparación con los tipos del front se corre en local (`contracts:check`) hasta que el CI del front la sume en la E10 (ADR 0016, enmienda). `HarnessTests` (E0) exige cada clase C# `*Tests` con un método `[Fact]`/`[Theory]` y los archivos `.test.mjs`/`.test.ts[x]` de «Lo verifica» en la etapa cerrada, también en el front si está el checkout.
- `ExplicitRouteInventoryTests` (E1): una ruta sin su fila falla.
- `scripts/test-e2e-real.test.mjs` del front (E3): desde la 3a, la etapa no cierra si el recorrido real no pasa.
- `PickupCodeReaderTests` y `WhatsAppOwnerSeedTests` (E8): selección MIME del OTP en es/en y override explícito Phone de la fixture.

## Detalle
[backend.md §4.6](../architecture/backend.md#46-tests)
