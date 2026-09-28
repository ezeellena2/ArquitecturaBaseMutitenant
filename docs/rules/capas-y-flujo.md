# Capas y flujo de un caso de uso

**Regla:** un request recorre siempre `Api/Controllers → I<X>Service → <X>Service → I<X>Repository / I<X>Reader / puertos → Infrastructure`. Cada pieza vive en una sola carpeta.

## Cómo se hace
- **Domain:** entidad, value objects y `<X>Errors`. Sin paquetes.
- **Application:** `Interfaces/Services/I<X>Service.cs` + `Services/<Área>/<X>Service.cs` (`internal sealed partial`), modelos, validadores y puertos en `Interfaces/{Persistence,Integrations}`.
- **Infrastructure:** implementaciones de EF (`Persistence/{Repositories,Readers}`) y adaptadores técnicos (`<Tema>/`).
- **Api:** el controller inyecta **solo** `I<X>Service`; contratos en `Api/Contracts/<Área>`.
- Registro en DI: cada capa en su `DependencyInjection.cs`, uno por uno y explícito. `Program.cs` solo compone.
- Lo que crece se parte en helpers con sufijo fijo: `Policy` (decide), `Guard` (protege una regla), `Issuer` (emite), `Verifier` (verifica) y `Linker` (vincula). **Máximo 8 dependencias** por servicio.

## Prohibido
- `ICommand`, `IQuery`, handlers, MediatR, `Application/Features`, repositorio genérico, Scrutor, AutoMapper, Minimal APIs de negocio.
- Un controller que inyecta EF, un repositorio o un lector.
- `IQueryable` o `Expression<>` en firmas públicas de Application.
- Un servicio que llama a métodos que escriben de otro servicio (lo compartido baja a un helper).

## Copiá de
- El área Roles completa (E4): `Domain/Authorization/Role.cs`, `Application/Services/Roles/RoleService.cs`, `Infrastructure/Persistence/Readers/RoleReader.cs`, `Api/Controllers/Organization/RolesController.cs`.
- La receta: [`docs/guides/agregar-un-area.md`](../guides/agregar-un-area.md) (E4).

## Lo verifica
- `LayerDependencyTests` (E0), `ProjectReferencesTests` (E0), `ApplicationPackagesTests` (E0): dependencias entre capas y paquetes permitidos.
- `ControllerServiceRepositoryTests` (E1): los controllers inyectan solo `I*Service`.
- `ApplicationPublicApiTests` (E1): sin `IQueryable` ni `Expression`.
- `ApplicationServicesTests` (E1), `ServiceDependencyCountTests` (E1), `MinimalApiRoutesTests` (E0).

## Detalle
[backend.md §3–§5 y §7](../architecture/backend.md#3-capas-y-dependencias)
