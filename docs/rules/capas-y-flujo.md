# Capas y flujo de un caso de uso

**Regla:** Domain ← Application ← Infrastructure; Api compone. Controller → I<X>Service → servicio → repositorio/reader o puerto. Helpers con sufijos de la lista cerrada y hasta ocho dependencias de negocio.

## Cómo se hace
- Domain: entidades/errores sin paquetes; Application: servicios/puertos/modelos/validadores; Infrastructure: EF/adaptadores; Api: HTTP.
- DI explícita por capa y área: DependencyInjection/Registration, opciones validadas al arrancar; una inscripción por clase.
- No cuentan ILogger, TimeProvider, IRequestValidator ni IUnitOfWork; IOptions y las demás dependencias sí. No partir en delegados vacíos.
- El servicio público abre la UoW; helpers y adaptadores de puertos neutrales trabajan dentro sin otra UoW.
- Un adaptador de Integrations registrado por un módulo toma nombre del contrato, conserva tope/registro y no es helper.
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
Scheduler/Canceller/Processor/Activator son los pasos nominales de baja y activación; no se amplían sin actualizar esta ficha.
- El actor background usa IExecutionActorContext/Scope/Factory neutrales y ExecutionActor (binding, cultura, zona, TenantKind y SessionStartedAtUtc opcionales).
- Capture toma identidad firmada; RevalidateAsync relee cuenta/membresía real antes del scope, servicios privados y UoW. Consumer exige Personal activo; Business, organización/membresía activas; Platform, operador exclusivo.
- Preferencias históricas deshabilitadas son legibles; no se cambia CultureInfo.CurrentCulture. El actor no acepta body, header, prompt ni argumentos de herramienta.
- Enter es síncrono y técnico: using, sin HTTP, tenant previo ni transacción. AsyncLocal anida/restaura; Dispose revoca también flujos heredados y el contenedor.
- ICurrentUser usa actor sólo sin HttpContext; sin ambos queda vacío. TenantContext conserva RLS y HTTP; ITenantScope.Enter(Guid) deja kind null y no sustituye al actor.
- Cada fase/herramienta reautoriza estados, membresía sin caché, legales, feature y permisos. El aviso IA se exige antes/después de datos; aceptar el primer aviso usa autorización de acceso.
- IdentityAccessTests fija los consumidores exactos de IUserTenantAccessReader, incluida la factory neutral y AssistantSessionPolicy; no exime namespaces.

## Prohibido
- CQRS/handlers/MediatR, repositorio genérico, Scrutor, AutoMapper o Minimal APIs de negocio.
- EF/HttpContext en Application, IQueryable/Expression públicos o repositorios/readers en controllers.
- Servicios que escriben mediante otro servicio; helpers con UoW o sufijo ajeno a la tabla.

## Copiá de
- `Application/Services/Roles/RoleService.cs` y `Api/Controllers/Organization/RolesController.cs` (E4).

## Lo verifica
- `LayerDependencyTests` (E0), `ProjectReferencesTests` (E0), `ApplicationPackagesTests` (E0), `ControllerServiceRepositoryTests` (E1), `ApplicationPublicApiTests` (E1), `ApplicationServicesTests` (E0), `ServiceDependencyCountTests` (E1), `MinimalApiRoutesTests` (E0), `IdentityAccessTests` (E0)

## Detalle
[Backend](../architecture/backend.md#3-capas-y-dependencias) y [ADR 0039](../decisions/0039-tope-de-dependencias-de-negocio.md).
