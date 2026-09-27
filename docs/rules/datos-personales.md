# Términos, privacidad y datos personales

**Regla:** nadie usa el sistema sin haber aceptado la **versión vigente** de los términos y de la política de privacidad, y cada aceptación queda registrada. La persona puede **exportar** sus datos y **dar de baja** su cuenta (Ley 25.326 en Argentina).

## Cómo se hace
- **Documentos legales:** `platform.LegalDocuments` con tipo (`Terms` o `Privacy`), versión y fecha de vigencia, y el texto en `platform.LegalDocumentContents` (documento, cultura, contenido): **una fila por idioma**, así sumar un idioma no cambia tablas. Publicar pide el texto en todas las culturas soportadas. Los publica la plataforma y **nunca se editan**: se publica una versión nueva.
- **Aceptación:** `identity.LegalAcceptances` con la identidad, el documento y su versión, `AcceptedAtUtc`, la IP y el user agent. Es append-only.
- **Registro** (correo, WhatsApp o Google): el pedido trae `acceptedTerms: true`. Sin eso, 400 `Legal.AcceptanceRequired`. Se guarda la aceptación en la misma transacción que la identidad.
- **Versión nueva:** `LegalAcceptanceMiddleware` responde 403 `Legal.AcceptanceRequired` en todas las rutas de `/api`, salvo `GET /api/me`, `GET /api/legal/*` y `POST /api/legal/accept`, hasta que la persona acepte. El front muestra una pantalla de aceptación que bloquea.
- **Exportar mis datos** (E10): `POST /api/me/data-export` (`[Idempotent]`) prepara en segundo plano un JSON con la cuenta, los datos del espacio personal y lo compartido con empresas (de su lado), y manda por correo un enlace de descarga que vence en 48 h.
- **Dar de baja la cuenta** (E3, ADR 0035): el diseño completo está en [multitenancy.md §3.2](../architecture/multitenancy.md#32-baja-de-una-cuenta-adr-0035). En resumen:
  - `POST /api/me/deletion` (`[Idempotent]`), con motivo y `ReauthTicket` de menos de 5 minutos. `AccountDeletionPolicy` bloquea al único Dueño de una organización no cerrada (`Legal.AccountDeletion.LastAdmin`), a un operador (`…PlatformOperator`), una baja ya pedida (`…AlreadyPending`) y lo que bloquee un módulo (`…Blocked`).
  - La identidad pasa a `PendingDeletion`, se revocan todas las sesiones y se avisa en todos los métodos.
  - Durante los 30 días de gracia, ingresar responde `Identity.Account.PendingDeletion` con un `cancelTicket`, y `POST /api/auth/deletion/cancel` la cancela.
  - `AccountDeletionWorker` elimina al vencer, una cuenta por transacción: anonimiza la identidad, borra los métodos y el espacio personal, quita las membresías, anonimiza la copia en los datos compartidos y conserva la auditoría.
  - Cada módulo que guarda datos de una persona registra un `IAccountDeletionParticipant`.

## Prohibido
- Guardar un "aceptó" booleano sin versión ni fecha.
- Editar un documento publicado.
- Borrar filas de auditoría al dar de baja una cuenta.
- Borrar la fila de la identidad (se anonimiza) o saltear los días de gracia.
- Una entidad con datos de una identidad sin su `IAccountDeletionParticipant`.
- Exportar datos de una organización dentro de la exportación personal.

## Copiá de
- `Domain/Legal/LegalDocument.cs` y `LegalAcceptance.cs` (E3).

## Lo verifica
- `LegalAcceptanceTests`: el registro sin aceptar da 400; una versión nueva bloquea hasta aceptar.
- `AccountDeletionTests` y `AccountDeletionParticipantsTests` (E3).

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas) · [datos-personales en estandares.md](../architecture/estandares.md)
