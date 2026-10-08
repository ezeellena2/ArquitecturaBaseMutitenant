# Capas y flujo de un caso de uso

**Regla:** un request recorre siempre `Api/Controllers → I<X>Service → <X>Service → I<X>Repository / I<X>Reader / puertos → Infrastructure`. Cada pieza vive en una sola carpeta.

## Cómo se hace
- **Domain:** entidad, value objects y `<X>Errors`. Sin paquetes.
- **Application:** `Interfaces/Services/I<X>Service.cs` + `Services/<Área>/<X>Service.cs` (`internal sealed partial`), modelos, validadores y puertos en `Interfaces/{Persistence,Integrations}`.
- **Infrastructure:** implementaciones de EF (`Persistence/{Repositories,Readers}`) y adaptadores técnicos (`<Tema>/`).
- **Api:** el controller inyecta **solo** `I<X>Service`; contratos en `Api/Contracts/<Área>`.
- Registro en DI: cada capa en su `DependencyInjection.cs`, que llama a un `*Registration.cs` por área. En Application es `Services/<Área>/<Área>Registration.cs` y registra también las opciones del área (`BindConfiguration`, `ValidateDataAnnotations` y `ValidateOnStart`). Cada clase se registra una sola vez, uno por uno y explícito. `Program.cs` solo compone.
- Lo que crece se parte por responsabilidad en helpers. El sufijo dice qué hace cada uno y sale de la lista cerrada de abajo; un sufijo nuevo se suma a la tabla, con lo que hace. **Máximo 8 dependencias de negocio** por clase de servicios. No cuentan `ILogger<T>`, `TimeProvider`, `IRequestValidator` ni `IUnitOfWork`; todo lo demás cuenta, incluido `IOptions<T>` ([ADR 0039](../decisions/0039-tope-de-dependencias-de-negocio.md)). No crear clases que solo reenvían para cumplir el tope.
- El método público del servicio abre la transacción. Los helpers trabajan dentro de ella y no reciben `IUnitOfWork`.

**Sufijos de los helpers** (lista cerrada; la lee `ApplicationServicesTests`). Un helper es una clase de `Services` que arma el contenedor y no termina en `Service`.

| Sufijo | Qué hace |
|---|---|
| `Policy` | Decide sin escribir: devuelve la decisión o el error. |
| `Guard` | Protege una regla: toma el lock, relee o cuenta, y devuelve el error. |
| `Issuer` | Emite algo que sale: un código, un ticket, una invitación o un aviso. |
| `Verifier` | Verifica algo que entra (un código, un ticket, un dominio o una repetición) y lo consume si corresponde. |
| `Linker` | Vincula un método de ingreso con algo de afuera: Google o el dominio de la organización. |
| `Notifier` | Arma y encola los avisos de un caso. |
| `Resolver` | Lee y arma lo que el caso necesita para decidir, sin escribir. |
| `Builder` | Arma un modelo con varias fuentes: una respuesta de lectura o el contexto de un aviso. |
| `Writer` | Escribe un cambio adentro de la transacción del servicio: asignaciones, estados, aceptaciones o eventos. |
| `Creator` | Crea un agregado nuevo con sus partes. |
| `Editor` | Cambia un agregado que ya existe. |
| `Remover` | Quita un agregado o los accesos que dependen de él. |
| `Recorder` | Registra el consentimiento explícito dentro de la UoW del llamador, sin abrir otra transacción. |
| `Registrar` | Da de alta una cuenta o un dominio. |
| `Provisioner` | Prepara un espacio completo con sus valores iniciales: una organización, un espacio personal o un operador. |
| `Requester` | Controla un pedido (estado o límites) y dispara lo que pide. |
| `Restorer` | Vuelve a armar la sesión después de un cambio de la cuenta. |
| `Finalizer` | Termina el vínculo con Google cuando vuelve el desafío. |
| `Scheduler` | Programa la baja de una cuenta ya autorizada. |
| `Canceller` | Cancela una baja programada. |
| `Processor` | Ejecuta la baja vencida de una cuenta. |
| `Activator` | Activa una organización pendiente: la aprueba y prepara a su Dueña. |
| `PendingItems` | Aporta los pendientes de un área a «Para resolver» (`IPendingItemSource`, ADR 0040). |

`Scheduler`, `Canceller`, `Processor` y `Activator` tienen una sola clase cada uno: nombran pasos que los demás sufijos no describen, los de la baja de una cuenta (ADR 0035) y la activación de una organización.

## Prohibido
- `ICommand`, `IQuery`, handlers, MediatR, `Application/Features`, repositorio genérico, Scrutor, AutoMapper, Minimal APIs de negocio.
- Un controller que inyecta EF, un repositorio o un lector.
- `IQueryable` o `Expression<>` en firmas públicas de Application.
- Un servicio que llama a métodos que escriben de otro servicio (lo compartido baja a un helper).
- Un helper con un sufijo que no está en la tabla, o una fila de la tabla que ya no usa nadie.

## Copiá de
- El área Roles completa (E4): `Domain/Authorization/Role.cs`, `Application/Services/Roles/RoleService.cs`, `Infrastructure/Persistence/Readers/RoleReader.cs`, `Api/Controllers/Organization/RolesController.cs`.
- La receta: [`docs/guides/agregar-un-area.md`](../guides/agregar-un-area.md) (E4).

## Lo verifica
- `LayerDependencyTests` (E0), `ProjectReferencesTests` (E0), `ApplicationPackagesTests` (E0): dependencias entre capas y paquetes permitidos.
- `ControllerServiceRepositoryTests` (E1): los controllers inyectan solo `I*Service`.
- `ApplicationPublicApiTests` (E1): sin `IQueryable` ni `Expression`.
- `ApplicationServicesTests` (E1): cada servicio tiene su interfaz, cada helper termina con un sufijo de la tabla y cada sufijo de la tabla se usa. `ServiceDependencyCountTests` (E1), `MinimalApiRoutesTests` (E0).
- `DependencyInjectionTests` (E6): cada clase de `Services` se registra una sola vez, y Application registra y valida sus opciones. `ServiceProviderValidationTests` (E6): `ApiFactory` construye cada registro al arrancar (`ValidateOnBuild`).

## Detalle
[backend.md §3–§5 y §7](../architecture/backend.md#3-capas-y-dependencias)
