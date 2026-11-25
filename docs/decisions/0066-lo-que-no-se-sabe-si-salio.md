# ADR 0066 · Lo que no se sabe si salió por WhatsApp queda «Sin confirmar»

**Estado:** aceptada por el usuario el 2026-11-18 (decisión 240 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), pregunta N1 de la tanda 5 del traspaso 15). Toma H7, H2, H8, H9 y H31 de la revisión de WhatsApp de ArquitecturaBase (su traspaso 13, `03-whatsapp-y-asistente.md`). Cambia la decisión 80 y precisa la [0038](0038-whatsapp-de-atencion.md). Programada en la B5, la B6 y la B7 de la [base de la 12, segunda parte](../plans/2026-11-19-etapa-base-de-la-12-segunda-parte.md) (`8bf60727`, `802a9819` y `08c5d7a7`). Nada de ArquitecturaBase pasó todavía su prueba integral.

## Contexto

La salida de WhatsApp va por el outbox del núcleo: reclama con lease, llama a Meta fuera de la transacción y confirma en otra ([WhatsApp](../features/whatsapp.md#operación-y-entrega)). Hoy cualquier falla vuelve a pendiente y se reintenta con espera hasta `MaxAttempts`, y `OutboxStatus` tiene solo `Pending`, `Sent`, `Failed` y `Cancelled`. Si Meta recibió el mensaje y la respuesta se cortó, el cliente lo recibe dos veces.

La 80 lo había aceptado («la cola del núcleo no suma un estado incierto»), y por eso salieron «Envío sin confirmar» y su revisión de la E14 y la E21. El correo ya va para el otro lado: el núcleo deja de repetir lo incierto y la invitación suma `Uncertain` (N5, N8 y O3, en el [backlog](../backlog.md#traspaso-15-el-núcleo-para-programar)).

## Decisión

1. **«No salió» y «no se sabe» se separan.** El transporte de WhatsApp clasifica cada falla:
   - **no salió:** no se conectó, Meta limitó (429) o respondió un error con código. Se reintenta a los 30 s, dos veces como mucho, y ese chat espera para no desordenarse;
   - **no se sabe:** se venció la espera, la respuesta llegó cortada o hubo un error del servidor sin código. Queda «Sin confirmar» y no se repite solo;
   - **definitivo:** un error de Meta que no cambia al repetir. Falla con su motivo (punto 3).
2. **Lo revisa una persona** (H2): cualquiera que puede enviar en esa bandeja; en Plataforma, con `platform.whatsapp.attend`. Revisa por el id del envío, y queda quién y cuándo. Revisar no reenvía ni dice que llegó: reenviar es mandar otro. Vuelven al envío de formularios (E14) y a los avisos de las reglas (E21) el estado y su revisión, con sus textos y tableros al planificar esas etapas.
3. **Por qué no salió** (H9): un código estable debajo de «No se pudo enviar», guardado en `FailureReason` y con su texto en es y en. Son `WhatsApp.Send.NotAllowed`, `WindowClosed`, `Undeliverable`, `RateLimited`, `TokenInvalid`, `PermissionMissing`, `Unavailable` y `Rejected`, además de `WindowClosed`, `ReadOnly` y `PaymentRequired`, que ya se responden al enviar. Los logs van sin el número.
4. **Una invitación por WhatsApp dudosa** dice «Sin confirmar» en Usuarios y pide revisar con la persona antes de reenviarla, como la de correo.
5. **Apagar la Api no corta envíos** (H8): lo que esperaba su turno vuelve a la cola sin llamar a Meta, y lo que se corta después de llamar queda «Sin confirmar».
6. **Lo que contesta sale antes que los avisos** (H31): el operador, el asistente y la confirmación de un toque van antes que los avisos de las reglas y las difusiones. Dentro de un chat se respeta el orden.
7. **El estado es del núcleo y neutral:** el outbox suma «Sin confirmar» como capacidad de la cola, y cada transporte clasifica sus fallas. El núcleo no nombra a WhatsApp, y el correo usa el mismo estado.

## Consecuencias

- Vuelven a la E14 y a la E21 el estado y una revisión que la 80 había sacado. Sus specs y tableros se ajustan al planificarlas.
- Un mensaje dudoso puede no haber llegado, y nadie lo repite solo: decide la persona.
- Con dos versiones ([0060](0060-dos-versiones-conviven.md)), el estado nuevo se lee un despliegue antes de escribirse.
- Las métricas de la cola van sin la organización ([logs](../rules/logs.md)); el detalle de cada organización está en su bandeja.

## Alternativas descartadas

- **Mantener la 80:** el cliente puede recibir dos veces el mismo mensaje.
- **Pausar cada chat 6 s entre mensajes** (H12 de ArquitecturaBase): se evalúa después, al ver si la salida de acá ya ordena por chat.
