# Idempotencia: un doble clic o un reintento no duplica

**Regla:** todo `POST` que **crea** algo o **dispara** un envío (alta, invitación, reenvío, crear organización) lleva `[Idempotent]`. El front manda `Idempotency-Key`, **generada al abrir el formulario** y repetida en cada reintento.

## Cómo se hace
- **Back:** `[Idempotent]` en la acción. `IdempotencyFilter` depende de `IIdempotencyStore` en Application; Infrastructure implementa la reserva. El filtro hace el resto:
  1. **Reserva** `(TenantId?, UserId, Key)` en `platform.IdempotencyKeys` (índice único) **en su propia transacción**, junto con el hash del cuerpo y la ruta.
  2. **Clave nueva:** ejecuta la acción. Si la respuesta es 2xx o 4xx, **después del commit del caso de uso** guarda status + cuerpo en la misma fila. Si es 5xx, libera la reserva para permitir el reintento.
     La escritura de la respuesta usa un token independiente del pedido: si el cliente corta después del commit, la clave igual queda completada para el replay.
  3. **Clave terminada:** devuelve la respuesta guardada, con el encabezado `Idempotent-Replayed: true`, sin ejecutar nada.
  4. **Clave en curso:** 409 `Request.InProgress`.
  5. **Misma clave con otro cuerpo u otra ruta:** 422 `Request.IdempotencyKeyReused`.
  6. **Sin encabezado** en una acción `[Idempotent]`: 400 `Request.IdempotencyKeyRequired`.
- La clave es un UUID, válido 24 h. Las vencidas las borra un worker.
- **Front:** `useIdempotentMutation(fn)`. Crea la clave al montar el formulario o el diálogo y la manda en cada intento (también en el reintento automático del `httpClient`). Conserva la clave ante red o 5xx; la renueva después de un éxito o un 4xx definitivo para permitir corregir el formulario. Un 409 `Request.InProgress` conserva la clave, espera y reintenta hasta 30 veces; al desmontar cancela la espera y no vuelve a enviar.

## Prohibido
- Generar la clave en cada envío: el reintento traería otra y se duplicaría igual.
- Chequear "¿ya existe?" sin reserva con índice único: dos pedidos simultáneos pasarían los dos.
- Guardar la respuesta antes de que el caso de uso confirme.
- Confiar solo en deshabilitar el botón.

## Copiá de
- `Api/Idempotency/IdempotencyFilter.cs` (E2) · front `../ArquitecturaBaseMutitenantFront/src/shared/api/useIdempotentMutation.ts` (E1).

## Lo verifica
- `IdempotencyTests` (E2): dos pedidos iguales en paralelo crean uno solo; el reintento devuelve la misma respuesta incluso si `RequestAborted` se cancela después de la acción; otro cuerpo da 422; un 5xx libera la clave.
- `IdempotentActionsTests` (E1): todo `POST` que declara 201/202 o llama a `ToCreatedResult`/`ToAcceptedResult` (también desde una acción async) tiene `[Idempotent]` (test de arquitectura).

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
