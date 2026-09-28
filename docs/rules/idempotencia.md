# Idempotencia: un doble clic o un reintento no duplica

**Regla:** todo `POST` que **crea** algo o **dispara** un envío (alta, invitación, reenvío, crear organización) lleva `[Idempotent]`. El front manda `Idempotency-Key`, **generada al abrir el formulario** y repetida en cada reintento.

## Cómo se hace
- **Back:** `[Idempotent]` en la acción. `IdempotencyFilter` hace el resto:
  1. **Reserva** `(TenantId?, UserId, Key)` en `platform.IdempotencyKeys` (índice único) **en su propia transacción**, junto con el hash del cuerpo y la ruta.
  2. **Clave nueva:** ejecuta la acción. Si la respuesta es 2xx o 4xx, **después del commit del caso de uso** guarda status + cuerpo en la misma fila. Si es 5xx, libera la reserva para permitir el reintento.
  3. **Clave terminada:** devuelve la respuesta guardada, con el encabezado `Idempotent-Replayed: true`, sin ejecutar nada.
  4. **Clave en curso:** 409 `Request.InProgress`.
  5. **Misma clave con otro cuerpo u otra ruta:** 422 `Request.IdempotencyKeyReused`.
  6. **Sin encabezado** en una acción `[Idempotent]`: 400 `Request.IdempotencyKeyRequired`.
- La clave es un UUID, válido 24 h. Las vencidas las borra un worker.
- **Front:** `useIdempotentMutation(fn)`. Crea la clave al montar el formulario o el diálogo, la manda en cada intento (también en el reintento automático del `httpClient`) y la renueva solo después de un éxito o al cerrar. Un 409 `Request.InProgress` no muestra error: espera y reintenta.

## Prohibido
- Generar la clave en cada envío: el reintento traería otra y se duplicaría igual.
- Chequear "¿ya existe?" sin reserva con índice único: dos pedidos simultáneos pasarían los dos.
- Guardar la respuesta antes de que el caso de uso confirme.
- Confiar solo en deshabilitar el botón.

## Copiá de
- `Api/Idempotency/IdempotencyFilter.cs` (E2) · front `../ArquitecturaBaseMutitenantFront/src/shared/api/useIdempotentMutation.ts` (E1).

## Lo verifica
- `IdempotencyTests` (E2): dos pedidos iguales en paralelo crean uno solo; el reintento devuelve la misma respuesta; otro cuerpo da 422; un 5xx libera la clave.
- `IdempotentActionsTests` (E1): todo `POST` que devuelve 201 o 202 tiene `[Idempotent]` (test de arquitectura).

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
