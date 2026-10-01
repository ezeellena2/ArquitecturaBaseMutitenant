# Identidad e ingreso

La identidad de una persona es global. Una cuenta puede tener acceso como persona a su espacio `Personal` y, si pertenece a una organización, acceso como empresa a sus espacios `Business`. Los dos accesos no mezclan datos ni se eligen a partir del subdominio. Fuente: [multitenancy.md §2–§3](../architecture/multitenancy.md#2-quiénes-entran) y [plan maestro, Etapa 3](../plans/2026-09-27-plan-de-desarrollo.md#etapa-3-identidad-accesos-y-openiddict).

## 3a · Ingreso

- Hay dos puertas: `/login` elige `consumer`; `/login/empresa` elige `business`. Una sesión ya iniciada puede cambiar de perfil sin volver a ingresar; el servidor emite tokens nuevos para el acceso elegido. `LastBusinessTenantId` solo recuerda la última organización del lado empresa. El tenant privado sale del claim `tenant_id`, nunca del host.
- El registro como persona crea una identidad global, un método de ingreso verificado, un espacio personal y su membresía. `acceptedTerms: true` es obligatorio y la aceptación de los documentos legales vigentes se guarda en la misma transacción. `ConsumerSignup = Closed` impide el alta. Una persona no crea empresas por esta puerta.
- `PlatformSettings` recibe los modos de registro y el máximo de organizaciones propias del seed; la gracia para una baja es de 30 días. `TenantSettings` recibe cultura, zona y moneda ya validadas contra los catálogos de referencia; la entidad no tiene una lista fija ni elige sus defaults. El registro puede mandar cultura y zona IANA del navegador como datos técnicos ocultos: si no están habilitadas, se usa la cultura predeterminada o la zona del país habilitado; la moneda sale del país de la cultura habilitada. No se agregan campos visibles al tablero Registro.
- En 3a se ingresa por código de correo o Google, sin contraseña. `identity.LoginMethods` guarda los valores por tipo y tiene unicidad global `(Type, Value)`; `AspNetUsers.Email` y `PhoneNumber` son solo copias del método principal, sin unicidad propia. `Phone` es parte del modelo pero no habilita ingreso hasta que el módulo de WhatsApp registre su canal en la E8.
- La existencia de al menos un método verificado se garantiza en el registro transaccional, junto con la identidad. Las copias `Email` y `PhoneNumber` pueden quedar vacías si Google es el único método; no se usan para buscar ni para autorizar el ingreso.
- Los métodos y su ciclo de vida siguen el [ADR 0033](../decisions/0033-metodos-de-ingreso.md). Crear `Phone` no registra un canal; su disponibilidad la decide la infraestructura de mensajería.
- `DisplayName` puede estar vacío en el registro de 3a, porque el tablero Registro solo pide correo y aceptación legal. Un nombre provisto se rechaza si supera `TextLimits.PersonName`; no se recorta en silencio.
- Los métodos se verifican antes de servir para ingresar. El código es de un solo uso, caduca y limita intentos; el transporte es `ILoginCodeChannel`. `LoginAudit` conserva método, instante, `UserId` si se conoce y solo el código estable del error; no guarda el código de ingreso ni la dirección, la IP o el user agent.
- Los cambios de métodos y la baja dejan además un `SecurityEvent` global e inmutable. En la 3a se modelan los tipos de evento de la cuenta; su emisión acompaña las transacciones que implementan esas acciones.
- El acceso como empresa exige una membresía activa. Sin membresías se presenta el estado del tablero Ingreso; con una organización activa se entra directamente; con varias se elige la última organización usada dentro del lado empresa. Si todavía no hay una última usada, se elige la membresía activa con `JoinedAtUtc` más antiguo y, ante un empate, por nombre de organización. El acceso como persona crea o usa su espacio personal sin exponer los datos de empresa.
- Los documentos legales públicos se leen sin sesión. En 3a se siembra la primera versión de términos y privacidad con texto en es y en. El front implementa los estados aprobados de Ingreso, Registro, Sesión, Landing, Legal, inicio personal, inicio vacío de `/org` y errores que pertenecen a esta parte.
- Cada aceptación conserva el documento, su versión y el instante UTC. Solo al eliminar la cuenta se limpian IP y user agent, sin borrar la prueba de aceptación.
- Los correos se encolan dentro de la transacción del caso de uso en el outbox global. El payload cifrado no aparece en logs ni en `ToString()`; un fallo espera con backoff. El dispatcher confirma cada mensaje en su propia transacción y no vuelve a tomar uno marcado `Sent`; al detenerse, termina y confirma el envío ya iniciado antes de empezar otro. Como SMTP y PostgreSQL no comparten transacción, una caída exacta entre aceptación SMTP y confirmación puede producir un duplicado en un reintento.

## 3b · La cuenta

La reautenticación emite un secreto aleatorio de 256 bits y guarda solo su hash. Su comprobante dura cinco minutos y se consume una sola vez, ligado a cuenta, acción y método objetivo; quitar o elegir principal exige otro método verificado. El ticket de cancelación conserva el retorno autorizado y no concede una sesión antes de cancelarla.

La gestión conserva `LoginMethods.Value` como clave global del método. En Google es el subject del proveedor; `ContactEmail` guarda el correo verificado para presentación y avisos, sin usarlo como clave OAuth. Cambiar el principal desmarca el anterior y conserva su verificación.

`/cuenta` conserva el acceso actual y permite editar nombre, cultura y zona con `Version` de `/api/me`; un 409 conserva el borrador hasta que se elija «Ver lo nuevo» o «Seguir editando». El idioma cambia después de guardar. «Mi cuenta» aparece en el menú de identidad y en la navegación personal dibujada en el lienzo.

| Operación | Ruta |
|---|---|
| Perfil global y versión | `GET/PUT /api/me` |
| Listado y agregar correo | `GET/POST /api/me/login-methods` |
| Enviar/verificar código del método | `POST /api/me/login-methods/{id}/code`, `POST /api/me/login-methods/{id}/verify` |
| Pedir/verificar reautenticación | `POST /api/me/reauth`, `POST /api/me/reauth/verify` |
| Quitar/elegir principal | `DELETE /api/me/login-methods/{id}`, `PUT /api/me/login-methods/{id}/primary` |
| Vincular Google | `POST /api/me/external/google` con antiforgery; callback protegido vuelve a `/cuenta` |
| Aceptar documentos pendientes | `POST /api/legal/accept` |
| Pedir baja | `POST /api/me/deletion` |
| Recuperar prueba Google/cancelar baja | `POST /api/auth/deletion/pending`, `POST /api/auth/deletion/cancel` |

Agregar reserva globalmente el correo y envía su código. Solo la verificación de ese método y de esa cuenta lo activa. Quitar, desvincular y elegir principal piden un código a otro método disponible; no se puede quitar el último método propio. Las capacidades se calculan en el backend. Cada cambio deja `SecurityEvent` y avisos a contactos verificados deduplicados. Un 429 muestra la cuenta regresiva; el reintento exige pulsar el mismo botón, sin envío automático.

`LegalAcceptanceMiddleware` devuelve 403 `Legal.AcceptanceRequired` hasta aceptar exactamente las versiones pendientes. `/api/me`, las lecturas legales y la aceptación quedan disponibles. El gate conduce a `/aceptar-terminos`, con casilla desmarcada. Una publicación simultánea devuelve `Legal.Document.VersionChanged`, recarga documentos y exige marcar otra vez; las aceptaciones anteriores se conservan.

La baja exige motivo y reautenticación. Bloquea al operador y a participantes que informen un impedimento. El único Dueño se incorpora en E4. La transacción marca `PendingDeletion`, programa la fecha según `PlatformSettings.AccountDeletionGraceDays`, revoca todas las sesiones y encola el aviso. Durante la gracia, demostrar un método verificado presenta la fecha y «Cancelar la baja y entrar» antes de emitir una sesión. Google transporta la prueba mediante cookie Data Protection, HttpOnly/Secure, de cinco minutos; nunca en la URL. Cancelar consume la prueba, restaura la cuenta y recién entonces emite cookie y continúa el retorno autorizado. `Suspended`, gracia vencida o prueba vencida impiden cancelar.

`AccountDeletionWorker` se ejecuta al arrancar y cada hora. Reclama un lease de quince minutos, limpia cada alcance en una transacción independiente y retoma tras una caída. Los participantes actuales cierran y limpian Personal, marcan membresías B2B `Removed/AccountDeleted`, retiran IP/user agent de aceptaciones y cancelan mensajes pendientes de esa cuenta. Se encola el aviso final al principal antes de purgar métodos/códigos/tickets/credenciales. La identidad termina `Deleted`, anonimizada, conservando su Guid y la prueba legal/auditoría. La limpieza Personal tiene DELETE restringido por RLS; no concede borrado de membresías Business.

WhatsApp (E8), exportación (E10) y el bloqueo del único Dueño (E4) permanecen fuera de esta pantalla. La 3c sigue pendiente; `HarnessStage` permanece en 2 hasta cerrar toda la Etapa 3. Fuente: [multitenancy.md §3.1–§3.2](../architecture/multitenancy.md#31-métodos-de-ingreso-la-cuenta-no-depende-de-un-solo-correo), [ADR 0035](../decisions/README.md), [informe y recorrido manual](../reviews/2026-09-30-etapa-3b-cuenta.md).

## 3c · Invitaciones (pendiente)

Implementación en curso según el [plan de 3c](../plans/2026-10-01-etapa-3c-invitaciones.md). Una invitación reserva un `Member(Invited)` sin crear identidad: `UserId` puede estar vacío hasta vincularse y nunca se activa sin cuenta. La invitación conserva solo el hash del token, vence en el instante exacto configurado y se acepta una vez; revocarla impide usarla. Si la aceptación crea identidad, un nonce del navegador original permite recuperar su sesión durante cinco minutos ante una respuesta perdida. No concede ingreso a una cuenta previa.

`tenant.Invitations` tiene clave compuesta, vínculo al miembro del mismo tenant, destino pendiente único y RLS forzado. `identity.sync_user_tenant_access` omite miembros sin identidad y agrega su acceso al vincularlos en la misma transacción. El reader de miembros solo proyecta identidades vinculadas. La migración nueva conserva las membresías existentes; su rollback retira las reservas sin usuario antes de restaurar `UserId NOT NULL`.

La invitación permite crear una identidad sin espacio personal o vincular una identidad existente a una organización. Su emisión, aceptación y pantalla pertenecen a 3c.

## Reglas de implementación

- Seguir las fichas [datos-personales](../rules/datos-personales.md), [multitenancy](../rules/multitenancy.md), [guardado](../rules/guardado.md), [result-y-errores](../rules/result-y-errores.md), [emails](../rules/emails.md), [telefonos](../rules/telefonos.md), [api-http](../rules/api-http.md) y [tests](../rules/tests.md).
- `ApplicationUser` vive en `Infrastructure/Identity`; las reglas sin dependencia de Identity, en `Domain/Users` y `Domain/Authentication`. Los servicios usan repositorios y readers explícitos; una escritura pública tiene un solo límite `IUnitOfWork`.
- Probar las reglas puras en `Domain.UnitTests`, la cuenta Identity en `Application.UnitTests`, las rutas y el aislamiento en `Api.IntegrationTests`, y la presencia de piezas de 3a en `Stage3aInventoryTests`.
