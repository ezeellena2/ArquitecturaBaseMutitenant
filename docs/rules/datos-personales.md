# Términos, privacidad y datos personales

**Regla:** nadie usa el sistema sin haber aceptado la **versión vigente** de los términos y de la política de privacidad, y cada aceptación queda registrada. La persona puede **exportar** sus datos y **dar de baja** su cuenta (Ley 25.326 en Argentina). Los correos y teléfonos de una cuenta viven solo en sus métodos de ingreso (ADR 0033).

## Cómo se hace
- **Documentos legales:** `platform.LegalDocuments` con tipo (`Terms` o `Privacy`), versión y fecha de vigencia, y el texto en `platform.LegalDocumentContents` (documento, cultura, contenido): **una fila por idioma**, así sumar un idioma no cambia tablas. La versión base se siembra en la E3a. Publicar una nueva versión (E5) pide el texto en todas las culturas soportadas. Los publica la plataforma y **nunca se editan**.
- **Aceptación:** `identity.LegalAcceptances` con la identidad, el documento y su versión, `AcceptedAtUtc`, la IP y el user agent. Es append-only, salvo la eliminación de la cuenta (ADR 0035), que borra la IP y el user agent y conserva el documento, la versión y la fecha.
- **Registro:** el pedido trae `acceptedTerms: true`. El validador del alta lo exige (`AcceptedTerms` debe ser `true`, con el mensaje de `ValidationTexts`); sin eso, 400 `Validation.Failed` con el error en el campo `acceptedTerms`. La aceptación de la versión vigente se guarda en la misma transacción que crea la identidad. Si la identidad ya existía, no se pide de nuevo: una versión nueva la cubre `LegalAcceptanceMiddleware`. `Legal.AcceptanceRequired` queda solo para el 403 del middleware. Vale en los tres lugares donde nace una identidad:
  - «Creá tu cuenta» (E3a): `POST /api/auth/signup` por correo o Google iniciado **desde Registro**. Ingreso con Google solo acepta cuentas existentes; una cuenta nueva requiere la casilla de términos de Registro, que también bloquea Google. El registro por WhatsApp llega con el módulo en la E8.
  - «Registrá tu empresa» (E6): `POST /api/auth/business-signup`. Tiene la misma casilla, que también bloquea «Seguir con Google».
  - Aceptar una invitación sin cuenta (E3, parte 3c): `POST /api/invitations/accept`, que crea la identidad **sin** espacio personal. No hay casilla: el botón «Aceptar», con la leyenda «Al aceptar creamos tu cuenta… y aceptás los Términos y la Política de privacidad», es la aceptación, y el front manda `acceptedTerms: true`.
- **Versión nueva (E3b):** `LegalAcceptanceMiddleware` actúa solo con identidad autenticada y responde 403 `Legal.AcceptanceRequired` en sus rutas de `/api`, salvo `GET /api/me`, `GET /api/legal/*` y `POST /api/legal/accept`, hasta que la persona acepte. Las rutas anónimas nunca pasan por él. El front muestra una pantalla de aceptación que bloquea.
- **Métodos de ingreso** (E3, ADR 0033): el diseño está en [multitenancy.md §3.1](../architecture/multitenancy.md#31-métodos-de-ingreso-la-cuenta-no-depende-de-un-solo-correo). En resumen:
  - En la E3a nace `identity.LoginMethods` (`Type` Email|Phone|Google, `Value`, `IsPrimary`, `VerifiedAtUtc`, `ManagedByTenantId?`), la **única** fuente de correos y teléfonos, con índice único (`Type`, `Value`) en todo el sistema. El registro y el ingreso usan un método verificado.
  - `AspNetUsers.Email` y `PhoneNumber` son solo una copia del método principal, sin índice único. Los mantiene el servicio de métodos de ingreso.
  - En la E3b se puede sumar, verificar, elegir el principal y quitar, siempre que quede al menos un método propio o activo. Quitar o cambiar pide un `ReauthTicket` de menos de 5 minutos, obtenido con un código en **otro** método verificado (`ReauthVerifier`, el mismo que pide la baja). Todo cambio va a `SecurityEvents` y se avisa en todos los métodos, por `IAccountNoticeChannel` y el outbox. La E3 ofrece correo y Google; `Phone` es solo modelo hasta que el módulo de WhatsApp registra su canal en la E8. La opción aparece si `GET /api/auth/methods` trae ese canal ([modulos](modulos.md)).
  - Los correos del dominio verificado de una organización quedan administrados (`ManagedByTenantId`). Al terminar la membresía dejan de servir para ingresar y se avisa por los otros métodos (E6, junto con `TenantDomains` y «Quitar de la organización»).
  - Aviso fijo "Agregá un correo personal o tu WhatsApp para no perder tu cuenta si dejás la empresa" mientras la cuenta dependa solo de métodos de una empresa.
  - El dominio verificado (`platform.TenantDomains`) llega en E6; "Recuperar mi cuenta" (`platform.AccountRecoveryRequests`), en E5.
- **Exportar mis datos** (E10): `POST /api/me/data-export` (`[Idempotent]`) prepara en segundo plano un JSON con la cuenta, los datos del espacio personal y lo compartido con empresas (de su lado), y manda por correo un enlace de descarga que vence en 48 h.
- **Dar de baja la cuenta** (E3, ADR 0035): el diseño completo está en [multitenancy.md §3.2](../architecture/multitenancy.md#32-baja-de-una-cuenta-adr-0035). En resumen:
  - `POST /api/me/deletion` (`[Idempotent]`), con motivo y `ReauthTicket` de menos de 5 minutos. `AccountDeletionPolicy` bloquea a un operador (`…PlatformOperator`), una baja ya pedida (`…AlreadyPending`) y lo que bloquee un módulo (`…Blocked`) desde la E3, y al único Dueño de una organización no cerrada (`Legal.AccountDeletion.LastAdmin`) desde la E4, cuando existen `SystemRoles` y `RoleAssignment`. El Dueño sale solo del rol de sistema `TenantAdmin`, nunca de un flag de `Member`; "Dueños activos" son los `TenantAdmin` con la identidad `Active` (una baja pedida no cuenta), la misma lectura que reusa `LastTenantAdminGuard` (E6).
  - Desde la plataforma (E5): un operador la inicia con `POST /api/platform/accounts/{id}/deletion` sobre una cuenta activa o suspendida, con motivo y sin reautenticar a la persona. La bloquea igual la regla del único Dueño de una organización no cerrada (`Legal.AccountDeletion.LastAdmin`): primero se suma otro Dueño o la plataforma cierra la organización. A un operador lo da de baja otro operador. Sigue los mismos 30 días de gracia y los mismos avisos, y la cuenta muestra "La plataforma inició la baja el dd/mm/aaaa". La plataforma no puede saltear la gracia ni cancelar la baja: cancelarla es solo de la persona. Una cuenta **suspendida** sigue `Suspended` con la fecha de eliminación: como no puede ingresar, no puede cancelar. Si la plataforma la reactiva antes de la fecha, pasa a `PendingDeletion` (no a `Active`), y ahí sí puede ingresar y cancelar.
  - El estado y la baja se guardan por separado: `Status` y `DeletionScheduledForUtc`. Una identidad activa pasa a `PendingDeletion` con la fecha, se revocan todas las sesiones y se avisa en todos los métodos.
  - Durante los 30 días de gracia, ingresar responde `Identity.Account.PendingDeletion` con un `cancelTicket`, y `POST /api/auth/deletion/cancel` la cancela.
  - `AccountDeletionWorker` elimina toda cuenta con `DeletionScheduledForUtc` vencido, esté `PendingDeletion` o `Suspended`, una por transacción: anonimiza la identidad, borra los métodos y el espacio personal, quita las membresías, anonimiza la copia en los datos compartidos, borra la IP y el user agent de las aceptaciones legales y conserva la auditoría.
  - Cada módulo que guarda datos de una persona registra un `IAccountDeletionParticipant`.

## Prohibido
- Guardar un "aceptó" booleano sin versión ni fecha.
- Editar un documento publicado.
- Borrar filas de auditoría al dar de baja una cuenta.
- Borrar la fila de la identidad (se anonimiza) o saltear los días de gracia.
- Una entidad con datos de una identidad sin su `IAccountDeletionParticipant`.
- Exportar datos de una organización dentro de la exportación personal.
- Escribir `AspNetUsers.Email` o `PhoneNumber` a mano o ponerles índice único.
- Guardar un correo o teléfono de ingreso fuera de `LoginMethods`.
- Quitar o cambiar un método sin `ReauthTicket`, o dejar la cuenta sin un método propio o activo.

## Copiá de
- `Domain/Legal/LegalDocument.cs` y `LegalAcceptance.cs` (E3).
- `Domain/Authentication/LoginMethod.cs` y `Application/Services/Identity/ReauthVerifier.cs` (E3).

## Lo verifica
- `AuthEndpointsTests` y `GoogleLoginTests` (3a): el registro sin aceptar da 400 `Validation.Failed` con `acceptedTerms` en correo y Google. `LegalAcceptanceTests` (3a) comprueba lectura de Términos y Privacidad vigentes en ambas culturas. La invitación (3c), el alta de empresa (E6) y WhatsApp (E8) suman sus casos cuando nacen; en 3b se comprueba que una versión nueva exija `Legal.AcceptanceRequired` hasta aceptarla.
- `AccountDeletionTests` (E3) y `AccountDeletionParticipantsTests` (E3); el caso del único Dueño se suma en la E4.
- `LoginMethodsTests` (E3) cubre la gestión; `ManagedEmailTests` (E6) comprueba que un exmiembro no pueda ingresar con el correo de la empresa.

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas) · [datos-personales en estandares.md](../architecture/estandares.md)
