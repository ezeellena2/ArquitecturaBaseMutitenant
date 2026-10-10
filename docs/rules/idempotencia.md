# Idempotencia: un doble clic o un reintento no duplica

**Regla:** todo `POST` que **crea** algo o **dispara** un envío (alta, invitación, reenvío, crear organización) lleva `[Idempotent]`. El front manda `Idempotency-Key`, **generada al abrir el formulario** y repetida en cada reintento.

## Cómo se hace
- **Back:** `[Idempotent]` en la acción. `IdempotencyFilter` depende de `IIdempotencyStore` en Application; Infrastructure implementa la reserva. El filtro hace el resto:
  1. **Reserva** `(TenantId?, UserId, Key)` en `platform.IdempotencyKeys` (índice único) **en su propia transacción**, junto con el hash del cuerpo y la ruta.
  2. **Clave nueva:** ejecuta la acción. Si la respuesta es 2xx o 4xx, **después del commit del caso de uso** guarda status + cuerpo en la misma fila. Si es 5xx, libera la reserva para permitir el reintento.
     La escritura de la respuesta usa un token independiente del pedido: si el cliente corta después del commit, la clave igual queda completada para el replay.
  3. **Clave terminada:** devuelve la respuesta guardada, con el encabezado `Idempotent-Replayed: true`, sin ejecutar nada.
     Una acción que necesita revalidar una prueba sensible declara `[IdempotencyReplay(typeof(...))]`. Su verificador recibe estado/cuerpo originales y comprueba la autorización vigente antes de devolver cualquier resultado cacheado, también 4xx. No se decide por nombre de controller o URL. El canje de enlaces (E8) usa ese hook para no repetir una cookie ni revelar un `cancelTicket` después de retirar el método o convertir la cuenta en operadora; conserva el mismo ticket sólo mientras la baja sigue pendiente.
  4. **Clave en curso:** 409 `Request.InProgress`.
  5. **Misma clave con otro cuerpo u otra ruta:** 422 `Request.IdempotencyKeyReused`.
     En una ruta `[PublicSite]`, la organización resuelta por `IPublicSiteContext` forma parte de la ruta lógica: repetir la clave en otro sitio da 422. Nunca se usa el `Host` sin resolver ni se cambia el tenant firmado. Los filtros de publicación, acceso y módulo se evalúan antes del replay.
  6. **Sin encabezado** en una acción `[Idempotent]`: 400 `Request.IdempotencyKeyRequired`.
- **Respuestas privadas de módulos:** `[Idempotent(Mode = IdempotencyMode.DurableReference)]` para JSON reconstruible y `DurableStream` para admisión de un stream. El store global sólo conserva scope/key/hash/ruta/estado/refKind/refId/status/Location; jamás título, texto, resultado de herramienta, nombre de archivo o bytes. `IdempotencyDurableAttribute` declara el verificador del módulo: autoriza antes de reservar y busca su binding privado antes de consultar la reserva global. El binding se confirma en la misma UoW que el efecto y conserva la respuesta original o su referencia durable. Vencer las 24 h, perder Complete, liberar una reserva o cortar la respuesta después del commit no habilita repetir un efecto ya confirmado. Borrar contenido conserva una tombstone técnica sin snapshot; otra ruta/body mantiene422, y la misma operación borrada devuelve404.
  El resultado HTTP implementa `IIdempotentReferencedResult`: entrega `Reference(Kind, Id, StatusCode, Location)` al filtro antes de escribir JSON. Los puertos genéricos están en `Application/Interfaces/Integrations/Idempotency/` y `Application/Models/Idempotency/`: el resolver devuelve `Result<IdempotentReplayResponse>`, cuyo valor concreto es `IdempotentReplayResponse<T>`, con la referencia y el DTO tipado. El núcleo transporta el valor sin importar el módulo, parsear su body privado ni usar reflexión para reconstruirlo. El módulo revalida dueño/acceso/organización/cuenta/membresía/legales/aviso/habilitación/permisos y objeto/enlaces antes del replay. La reserva global en curso no autoriza contenido. Los protocolos sin adaptador conservan su stub501; `DurableStream` no queda habilitado por este mecanismo. `BufferedJson` conserva su captura, mapper y verificador previos.
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
- `EngagementTests` (E7): el reintento en el mismo sitio crea un solo pedido; cambiar de sitio con la misma clave y cuerpo da 422, y retirar la publicación o apagar el módulo impide el replay.
- `LoginLinkTests` de integración (E8): replay revalida cuenta, método y canal para éxito y baja pendiente; no repite cookie/ticket y rechaza al operador sin datos.
- `AssistantDurableConversationTests`, `AssistantDurableTransportTests` y `AssistantDurableAuthorizationTests` (E8; E8b T05b): DTO/version originales después de editar, vencimiento global, rollback, aborto después del commit, fallo de Complete, dos hosts, body/ruta distintos, tombstone y autorización vigente antes de reserva/replay.

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
