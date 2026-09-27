# Términos, privacidad y datos personales

**Regla:** nadie usa el sistema sin haber aceptado la **versión vigente** de los términos y de la política de privacidad, y cada aceptación queda registrada. La persona puede **exportar** sus datos y **dar de baja** su cuenta (Ley 25.326 en Argentina).

## Cómo se hace
- **Documentos legales:** `platform.LegalDocuments` con tipo (`Terms` o `Privacy`), versión, fecha de vigencia y contenido por cultura (es y en). Los publica la plataforma y **nunca se editan**: se publica una versión nueva.
- **Aceptación:** `identity.LegalAcceptances` con la identidad, el documento y su versión, `AcceptedAtUtc`, la IP y el user agent. Es append-only.
- **Registro** (correo, WhatsApp o Google): el pedido trae `acceptedTerms: true`. Sin eso, 400 `Legal.AcceptanceRequired`. Se guarda la aceptación en la misma transacción que la identidad.
- **Versión nueva:** `LegalAcceptanceMiddleware` responde 403 `Legal.AcceptanceRequired` en todas las rutas de `/api`, salvo `GET /api/me`, `GET /api/legal/*` y `POST /api/legal/accept`, hasta que la persona acepte. El front muestra una pantalla de aceptación que bloquea.
- **Exportar mis datos** (E10): `POST /api/me/data-export` (`[Idempotent]`) prepara en segundo plano un JSON con la cuenta, los datos del espacio personal y lo compartido con empresas (de su lado), y manda por correo un enlace de descarga que vence en 48 h.
- **Dar de baja la cuenta** (E10): `DELETE /api/me`, con motivo y reautenticación reciente.
  - No se permite si la persona es el **único Dueño** de una organización activa (`Legal.AccountDeletion.LastAdmin`).
  - Se revocan las sesiones.
  - Pasados 30 días de gracia, se borran los datos del espacio personal y se anonimiza la identidad: nombre "Cuenta eliminada", sin email ni teléfono.
  - La auditoría conserva el `ActorId`, sin datos personales.

## Prohibido
- Guardar un "aceptó" booleano sin versión ni fecha.
- Editar un documento publicado.
- Borrar filas de auditoría al dar de baja una cuenta.
- Exportar datos de una organización dentro de la exportación personal.

## Copiá de
- `Domain/Legal/LegalDocument.cs` y `LegalAcceptance.cs` (E3).

## Lo verifica
- `LegalAcceptanceTests`: el registro sin aceptar da 400; una versión nueva bloquea hasta aceptar.
- `AccountDeletionTests` (E10).

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas) · [datos-personales en estandares.md](../architecture/estandares.md)
