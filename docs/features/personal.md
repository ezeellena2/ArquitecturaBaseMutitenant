# Personal: receta de un módulo B2C

Personal es el acceso Consumer de una cuenta. Cada persona tiene un espacio personal, separado de cada organización a la que entra con Business. Compartir la identidad no comparte los datos ni los permisos. La plantilla aporta los accesos y esta receta; los módulos de negocio los agrega cada producto. La referencia de casos de uso sigue siendo [Roles](roles.md); las reglas de aislamiento están en [multitenancy](../rules/multitenancy.md).

## El dato privado y sus rutas

Una entidad privada de la persona hereda de `Entity` e implementa `ITenantOwned`, igual que un dato privado Business. Vive en `tenant`, con clave que incluye `TenantId`, filtro EF `Tenant` y `EnableTenantRls`. La sesión Consumer apunta al espacio personal: el identificador sale exclusivamente del claim `tenant_id`. Ni el body, ni una query, ni el subdominio eligen ese espacio. La entidad nueva deja la columna de tenant vacía; la completa `TenantStampInterceptor`. Las lecturas conservan los filtros y RLS, y un identificador ajeno responde 404.

El controller declara `[Access(Access.Consumer)]` **sin `[HasPermission]`, `[HasCompanyPermission]` ni roles**. La persona tiene implícitos los `personal.*` sobre su espacio; no se siembran roles ni asignaciones personales. Una sesión Business de la misma cuenta recibe 403 `Tenancy.Access.Wrong` en esa ruta. Una ruta Business lee exclusivamente la organización del token y no puede usar el identificador personal para acceder a esos datos.

Copiá el flujo de Roles: contrato HTTP → `I<X>Service` → servicio → `I<X>Reader` / `I<X>Repository` → Infrastructure. El servicio valida con `IRequestValidator` antes de su única `IUnitOfWork.ExecuteInTransactionAsync`, devuelve `Result` y envuelve cada método público en `OperationLog.RunAsync`. EF y `HttpContext` no llegan al servicio.

Cada listado usa `PagedRequest`, su `PagedRequestValidator` y `PagedResult<T>`; el reader proyecta con `AsNoTracking`, busca con `ApplySearch`, ordena con `SortMap` y desempate por Id, y pagina con `ToPagedResultAsync`. Los índices de orden y búsqueda corresponden a esos campos. No se devuelve una lista completa ni se pagina en el front. Una entidad editable implementa `IVersioned`; edición y borrado reciben `version`, y un guardado responde `VersionResponse` con la versión del commit. Un alta o envío lleva `[Idempotent]`. Leé [paginado](../rules/paginado-y-busqueda.md), [concurrencia](../rules/concurrencia.md), [idempotencia](../rules/idempotencia.md), [guardado](../rules/guardado.md) y [API HTTP](../rules/api-http.md).

## Si el módulo es opcional

Agregá su clave real a `Domain/Features/Features.cs`, con alcance `Consumer` si sólo vive en Personal, o `Both` si tiene lados Consumer y Business. Declaralo en el catálogo y usá `[FeatureGate]` en sus controllers. El gate responde 404 cuando está apagado; no reemplaza al acceso ni a los permisos Business. Workers y mensajes consultan `IFeatureService`. La habilitación y el apagado global siguen [módulos habilitados](../rules/modulos-habilitados.md).

El núcleo no lleva gate. No agregues un módulo, una feature ni un permiso productivo para demostrar esta receta. `PersonalWidgetsController` y `Deal` existen sólo en `TestFeatures`.

## Si interactúa con empresas

Hay tres datos distintos; no conviertas el privado de una persona en una tabla visible a la empresa:

| Dato | Clasificación y esquema | Puerta y regla |
|---|---|---|
| Privado de la persona | `ITenantOwned`, `tenant`, `EnableTenantRls` | `[Access(Consumer)]`; el tenant es el del token |
| Publicado por la organización | `IPublishedByBusiness`, `public_site`, `EnablePublicRls` | lectura `[PublicSite][AllowAnonymous]`; el reader exige publicación y organización activa |
| Interacción entre persona y organización | `IConsumerBusinessShared`, `engagement`, `EnablePartiesRls` | sólo las dos partes ven la fila; cada transición pasa por `PartyPolicy` |

Lo publicado se lee desde el subdominio resuelto por `IPublicSiteContext`, con la receta de [sitio público](sitio-publico.md). La lectura vuelve a exigir la publicación; que una sesión Business vea su borrador privado no lo vuelve público. El directorio del dominio principal lleva sólo `[AllowAnonymous]` y su controller pertenece a la lista de `AccessDeclarationTests`; no lleva `[PublicSite]`.

El alta de una interacción desde una publicación combina `[PublicSite]`, `[Access(Consumer)]` e `[Idempotent]`. El espacio personal lo sella el interceptor; el `BusinessTenantId` contraparte sale de `IPublicSiteContext`, después de comprobar el sitio, nunca del body. El módulo compartido se comprueba en la organización de ese sitio. El subdominio nunca cambia el tenant firmado ni entra a un alcance privado.

La fila compartida guarda **sólo una copia de lo que la otra parte necesita ver**. El reader Business proyecta esa copia; nunca une con la cuenta, sus métodos de ingreso ni sus tablas privadas. Toda propiedad de la copia que contiene datos personales lleva `[NotAudited]`, para no retenerlos en el historial. El nombre mostrado viene armado desde la cuenta, en el único lugar que une nombre y apellido; ambos son opcionales ([ADR 0046](../decisions/0046-nombre-y-apellido.md)). `null` significa «sin dato», no «cuenta eliminada».

Antes de cada transición, el método de dominio usa `PartyPolicy` para exigir la parte Consumer o Business correspondiente. Una parte visible que intenta la acción de la otra recibe 403 `Tenancy.Party.NotAllowed`; un tercero recibe 404 antes de cambiar nada. Las transiciones conservan `version` y el módulo habilitado.

**La bandeja Business de un módulo real exige `[HasPermission]`** —o `[HasCompanyPermission]` si pertenece a una empresa— además de `[Access(Business)]` y el gate. La clave real se declara en el catálogo, en `Permissions.resx` y `.en.resx`, y en el seed del rol de sistema correspondiente. No reutilices un permiso de Roles para un módulo nuevo. La bandeja sintética de `Deal` y el lector `BusinessWidgetsController` no inventan permisos para tests; `PermissionAuthorizationTests` ya verifica los atributos y catálogos. Seguí [permisos](../rules/permisos.md) y la [guía de un permiso nuevo](../guides/permiso-nuevo.md).

## La baja y la retención de la copia

Toda entidad `IConsumerBusinessShared` define su método de reemplazo de la copia personal como parte del contrato de Domain. Ese método borra todos los campos personales copiados, deja el registro, su resumen de negocio y su estado, y puede repetirse sin alterar el resultado. Un marcador técnico distingue una copia borrada de un campo que ya era `null`. Al leer una copia borrada se traduce `Legal.AccountDeletion.DeletedDisplayName`; «Cuenta eliminada» nunca se guarda como nombre ni se infiere solamente de `ConsumerName == null`.

Un módulo con una obligación de retención declara una política `IRetainedOnConsumerDeletion` **en Application**, bajo `Interfaces/Integrations/Legal`: identifica el tipo compartido, el motivo y el plazo. Su implementación se registra explícitamente con el módulo. La entidad de Domain no implementa un puerto de Application ni conoce la política legal del worker. Sin política, la copia se reemplaza al procesar la baja; con política, se registra el vencimiento y el worker la reemplaza al vencer. El plazo no habilita lecturas de la cuenta ni cambia el aislamiento de las partes.

El participante de eliminación actúa en el espacio personal, dentro del alcance y la transacción abiertos por el worker; conserva RLS `Parties`, no llama `Enter`, no abre otra UoW y no guarda por su cuenta. La auditoría ya escrita se conserva y nunca contiene la copia personal. Si un módulo guarda otros datos privados de una identidad, registra también su `IAccountDeletionParticipant`. El contrato y la política de T07 están detallados en [el plan de esta etapa](../plans/2026-10-04-etapa-7-publico-y-personal.md#t07--back--la-baja-la-copia-en-lo-compartido-y-la-retención-legal), la [ADR 0035](../decisions/0035-baja-de-cuenta.md) y [datos personales](../rules/datos-personales.md).

## Carpetas

Los punteros Personal se crean como guías; no incorporan un módulo de negocio a producción. Cada producto agrega la entidad, sus modelos, validadores y adaptadores en las carpetas por área que define [agregar un área](../guides/agregar-un-area.md).

| Carpeta | Responsabilidad |
|---|---|
| `src/ArquitecturaBaseMultitenant.Application/Services/Personal` | casos de uso y helpers de los módulos B2C del producto; sin EF ni HTTP |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Personal` | controllers Consumer finos de los módulos B2C del producto |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/Isolation` | CRUD personal sintético, lector Business, servicio, reader/repositorio y fábrica con infraestructura real |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy` | aislamiento persona/persona y Consumer/Business; publicación e interacción |

En el front, el módulo va en `src/areas/personal/<módulo>/`, con `api/`, `components/` y `pages/` cuando hacen falta; su enlace se agrega en `navigation/personal.ts`. Lo publicado por el módulo vive en `src/areas/storefront/<módulo>/`. La bandeja Business vive en su área Business. Las piezas compartidas de presentación van en `shared/`; no se importan features de otro acceso para compartir datos. El front usa contratos generados, traducciones y los mismos gates y estados del API. Ningún módulo se agrega a la plantilla sólo para llenar el inicio de Personal.

## Referencia ejecutable y pruebas de un módulo

Copiá `TestFeatures/Isolation/PersonalWidgetsController.cs`, `PersonalWidgetService.cs`, `PersonalWidgetPersistence.cs` y `PersonalWidgetContracts.cs` para el flujo privado, y `DealsController.cs`, `DealService.cs`, `DealPersistence.cs` y `Deal.cs` para una interacción. `Widget` ya es `ITenantOwned`, auditable, versionado y de borrado lógico. Su constructor nuevo no recibe tenant; el interceptor lo sella. `PersonalIsolationFactory` compone `ApiFactory` con un modelo y application part propios, y conserva las migraciones, el rol runtime, la UoW y los cinco interceptores. Las tablas sintéticas las crea `IsolationSchema`; no llevan migraciones productivas.

Los tests de un módulo real deben probar dos personas que listan, leen, editan y borran sin alcanzar lo ajeno; Consumer y Business de la misma identidad sin mezclar sus datos; 401 sin sesión; 403 por acceso equivocado y por falta de permiso Business; versión vieja sin pisar; replay sin duplicar; y paginado real. Si hay interacción, sumá las dos partes, un tercero, transiciones, publicación, módulo apagado y copia sin datos de cuenta ni datos personales auditados. Copiá `PersonalIsolationTests` y `EngagementTests`, con los participantes de la baja cuando haya datos personales.

## Diez preguntas para la puerta de documentación a ciegas (T20)

Estas preguntas quedan escritas antes de la puerta. El agente de T20 sólo lee los `AGENTS.md` del repositorio y debe ubicar la receta y contestar:

1. ¿Qué ficha explica cómo agregar un módulo B2C y qué ejemplo permite copiar su CRUD sin agregar negocio a producción?
2. ¿Qué atributo pide una ruta privada Consumer y necesita permisos o roles personales explícitos?
3. ¿Qué interfaz y esquema usa el dato privado de una persona, y de dónde sale su tenant?
4. ¿Dónde se declara un módulo opcional Consumer o Both y qué atributo lo oculta cuando está apagado?
5. ¿Qué interfaz, esquema y atributo de ruta corresponden a lo publicado por una organización?
6. ¿Qué interfaz y esquema usa la interacción, qué ve la empresa y cómo se excluyen sus datos personales de la auditoría?
7. ¿Quién decide las transiciones de cada parte y qué autorización extra exige una bandeja Business real?
8. ¿Cómo se reemplaza la copia al dar de baja una cuenta y en qué capa se declara una retención con motivo y plazo?
9. ¿Dónde van el módulo personal y su enlace en el front, y dónde va su publicación?
10. ¿Qué pruebas verifican el aislamiento entre personas y entre accesos, y cuáles verifican la interacción con empresas?

## Lo verifica

- `PersonalIsolationTests` (E7): CRUD cruzado de Kevin y Carla con 404; Consumer/Business de Kevin con datos separados y 403 `Tenancy.Access.Wrong`; CRUD Consumer con permisos efectivos vacíos; 401 sin sesión; replay del alta; versiones viejas de edición y borrado; guardado sin cambios que avanza la versión; paginado, búsqueda con acentos y comodines literales limitada a la persona.
- `EngagementTests` (E7) y `PartyPolicyTests` (E7): las dos partes y los terceros, estados, copia mínima, auditoría sin nombre, publicación, gates e idempotencia del sitio resuelto.
- `CrossTenantIsolationTests`, `PublicAndSharedRowsTests`, `ModelFiltersTests`, `RlsBarrierTests` e `IsolationSchemaTests`: filtros, rol runtime, RLS y esquema sintético equivalentes a la barrera real.
- `AccountDeletionTests`, `AccountDeletionParticipantsTests` y `AccountDeletionProcessingTests`: reemplazo de la copia, retención al vencer y participantes repetibles de T07.
- `HarnessTests`: ficha, carpetas y punteros breves `AGENTS.md` / `CLAUDE.md`. `PermissionAuthorizationTests` y `ResourceParityTests` protegen permisos y traducciones. Las diez respuestas de T20 comprueban que los punteros permitan encontrar estas reglas.
