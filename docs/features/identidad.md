# Identidad e ingreso

La identidad de una persona es global. Una cuenta puede tener acceso como persona a su espacio `Personal` y, si pertenece a una organización, acceso como empresa a sus espacios `Business`. Los dos accesos no mezclan datos ni se eligen a partir del subdominio. Fuente: [multitenancy.md §2–§3](../architecture/multitenancy.md#2-quiénes-entran) y [plan maestro, Etapa 3](../plans/2026-09-27-plan-de-desarrollo.md#etapa-3-identidad-accesos-y-openiddict).

## 3a · Ingreso

- Hay dos puertas: `/login` elige `consumer`; `/login/empresa` elige `business`. Una sesión ya iniciada puede cambiar de perfil sin volver a ingresar; el servidor emite tokens nuevos para el acceso elegido. `LastBusinessTenantId` solo recuerda la última organización del lado empresa. El tenant privado sale del claim `tenant_id`, nunca del host.
- El registro como persona crea una identidad global, un método de ingreso verificado, un espacio personal y su membresía. `acceptedTerms: true` es obligatorio y la aceptación de los documentos legales vigentes se guarda en la misma transacción. `ConsumerSignup = Closed` impide el alta. Una persona no crea empresas por esta puerta.
- `PlatformSettings` recibe los modos de registro y el máximo de organizaciones propias del seed; la gracia para una baja es de 30 días. `TenantSettings` recibe cultura, zona y moneda ya validadas contra los catálogos de referencia; la entidad no tiene una lista fija ni elige sus defaults.
- En 3a se ingresa por código de correo o Google, sin contraseña. `identity.LoginMethods` guarda los valores por tipo y tiene unicidad global `(Type, Value)`; `AspNetUsers.Email` y `PhoneNumber` son solo copias del método principal, sin unicidad propia. `Phone` es parte del modelo pero no habilita ingreso hasta que el módulo de WhatsApp registre su canal en la E8.
- La existencia de al menos un método verificado se garantiza en el registro transaccional, junto con la identidad. Las copias `Email` y `PhoneNumber` pueden quedar vacías si Google es el único método; no se usan para buscar ni para autorizar el ingreso.
- Los métodos y su ciclo de vida siguen el [ADR 0033](../decisions/0033-metodos-de-ingreso.md). Crear `Phone` no registra un canal; su disponibilidad la decide la infraestructura de mensajería.
- `DisplayName` puede estar vacío en el registro de 3a, porque el tablero Registro solo pide correo y aceptación legal. Un nombre provisto se rechaza si supera `TextLimits.PersonName`; no se recorta en silencio.
- Los métodos se verifican antes de servir para ingresar. El código es de un solo uso, caduca y limita intentos; el transporte es `ILoginCodeChannel`. `LoginAudit` conserva método, instante, `UserId` si se conoce y solo el código estable del error; no guarda el código de ingreso ni la dirección, la IP o el user agent.
- Los cambios de métodos y la baja dejan además un `SecurityEvent` global e inmutable. En la 3a se modelan los tipos de evento de la cuenta; su emisión acompaña las transacciones que implementan esas acciones.
- El acceso como empresa exige una membresía activa. Sin membresías se presenta el estado del tablero Ingreso; con una organización activa se entra directamente; con varias se elige la última organización usada dentro del lado empresa. El acceso como persona crea o usa su espacio personal sin exponer los datos de empresa.
- Los documentos legales públicos se leen sin sesión. En 3a se siembra la primera versión de términos y privacidad con texto en es y en. El front implementa los estados aprobados de Ingreso, Registro, Sesión, Landing, Legal, inicio personal, inicio vacío de `/org` y errores que pertenecen a esta parte.
- Cada aceptación conserva el documento, su versión y el instante UTC. Solo al eliminar la cuenta se limpian IP y user agent, sin borrar la prueba de aceptación.

## 3b · La cuenta (pendiente)

Gestión de métodos de ingreso, aceptación bloqueante de versiones legales nuevas y baja con gracia. Los estados `PendingDeletion` y `Deleted` y sus fechas se reservan en el modelo de 3a sin implementar todavía el flujo de baja. Fuente: [multitenancy.md §3.1–§3.2](../architecture/multitenancy.md#31-métodos-de-ingreso-la-cuenta-no-depende-de-un-solo-correo).

## 3c · Invitaciones (pendiente)

La invitación permite crear una identidad sin espacio personal o vincular una identidad existente a una organización. Su emisión, aceptación y pantalla pertenecen a 3c.

## Reglas de implementación

- Seguir las fichas [datos-personales](../rules/datos-personales.md), [multitenancy](../rules/multitenancy.md), [guardado](../rules/guardado.md), [result-y-errores](../rules/result-y-errores.md), [emails](../rules/emails.md), [telefonos](../rules/telefonos.md), [api-http](../rules/api-http.md) y [tests](../rules/tests.md).
- `ApplicationUser` vive en `Infrastructure/Identity`; las reglas sin dependencia de Identity, en `Domain/Users` y `Domain/Authentication`. Los servicios usan repositorios y readers explícitos; una escritura pública tiene un solo límite `IUnitOfWork`.
- Probar las reglas puras en `Domain.UnitTests`, la cuenta Identity en `Application.UnitTests`, las rutas y el aislamiento en `Api.IntegrationTests`, y la presencia de piezas de 3a en `Stage3aInventoryTests`.
