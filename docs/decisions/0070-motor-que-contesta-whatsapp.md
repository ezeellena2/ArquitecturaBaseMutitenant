# ADR 0070 · El motor que contesta WhatsApp: un turno con pasos fijos y un menú de acciones declaradas

**Estado:** aceptada el 2026-11-23. Es una decisión técnica, tomada al planificar porque el usuario pidió que la IA que contesta WhatsApp use patrones de diseño, sea robusta, no esté «atada con pinzas» y permita sumar desarrollos nuevos fácil. No cambia lo que ve una persona: eso lo fija la spec de la [E23](../plans/formularios/23-asistente.md) y su [parte de datos y contrato](../plans/formularios/23-parte-datos-y-contrato.md#cómo-responde).
- **Lo que precisa:** las tareas B4 y B5 de la E23 y la B3 de la E24.
- **De qué parte:** la [0054](0054-respuestas-redactadas-por-el-asistente.md) (las respuestas redactadas), la [0064](0064-reglas-de-toda-la-ia.md) (las reglas de toda la IA) y la [0068](0068-acciones-del-asistente.md) (el alcance de solo lectura y el catálogo que filtra y sella).

## Contexto

La spec de la E23 ya dice qué hace un turno: siete pasos, seis acciones en un menú cerrado («un caso nuevo es configuración; una acción nueva es código»), los topes que derivan y lo que pasa cuando la IA no contesta. No dice cómo se reparte en clases.

Sin ese reparto, el servicio del turno junta un `switch` por acción y un `if` por tope en un solo método. Así, cada acción nueva toca el mismo archivo, y una falla en una arrastra a las otras. Eso es lo que frenan el umbral de complejidad (CA1502) y el revisor de arquitectura del conductor.

## Decisión

1. **Un servicio ordena el turno, y cada paso lo hace un helper con una sola tarea.**
   - **Sin handlers ni un pipeline genérico** (regla 1 de `AGENTS.md`): los pasos son helpers de la lista cerrada de [capas y flujo](../rules/capas-y-flujo.md).
   - **El orden es siempre el mismo:**

     | Paso | Pieza | Qué hace |
     |---|---|---|
     | Tomar el chat | `WhatsAppAssistantWorker` | toma los chats con `TurnDueAtUtc` vencido, usando esa fecha como lease. La cola es una sola y durable, y corre una vez entre copias ([concurrencia](../rules/concurrencia.md)); no toma nada si el anillo de esta copia no coincide (`IKeyRingReadiness`, [0064](0064-reglas-de-toda-la-ia.md) §11) |
     | Ver si corresponde | `WhatsAppAssistantTurnPolicy` | el modo Asistente, la ventana de 24 horas y el tope del mes |
     | Elegir quién contesta | `WhatsAppAgentResolver` (E24) | el asistente del equipo, o el principal |
     | Armar el turno | `WhatsAppConversationTurnBuilder` | arma `ConversationTurn` con los mensajes, los candidatos que da Formularios por su puerto, lo ya hecho y las acciones permitidas en la bandeja, con su descripción. El pedido al modelo lo arma el Assistant (`AssistantTurnBuilder`, B4) |
     | Proponer | `IConversationAgent.ProposeAsync` | en el Assistant, con el alcance de solo lectura y la organización del turno (0068 §5) |
     | Validar | la estrategia de cada acción (punto 2) y `WhatsAppReplyPolicy` | el largo, el idioma, los enlaces y los datos de contacto |
     | Contar | `WhatsAppAssistantCounterGuard` | los topes del día, con lock, en la zona de la organización |
     | Mandar y anotar | `WhatsAppAssistantActionWriter` | las acciones, el porqué cifrado (0065) y el outbox, en una sola transacción |
     | Derivar | `WhatsAppHandoffWriter` | el mensaje fijo, la atención manual, «Necesita una persona» y su motivo, de una lista cerrada |

   - **Los tests:** cada helper tiene el suyo, y el del servicio prueba el orden. Sumar un paso es sumar un helper y su línea en el servicio, sin tocar los demás.
2. **Las seis acciones son estrategias declaradas en un registro.**
   - **Las clases:** cada acción es una clase `<Acción>ConversationAction` del módulo WhatsApp, que implementa `IConversationAction`. Sus clases están en el enum cerrado `ConversationActionKind`: mandar un formulario, texto exacto, responder con el conocimiento, preguntar con botones, derivar y no responder.
   - **Cada acción declara:**
     - **lo que ve el modelo:** su descripción y sus parámetros, sacados de los resources (`Action.Channel.<clase>`), sin prompts escritos a mano;
     - **su validación:**
       - mandar un formulario: que sea un candidato y quepa en el tope de ofrecimientos (278);
       - botones: hasta 3, con los textos del caso;
       - texto exacto: el texto actual del caso, no el que vio el modelo;
       - responder con el conocimiento: `WhatsAppReplyPolicy`.
       Lo que no pasa devuelve un motivo de derivar, nunca una excepción;
     - **su plan:** los mensajes que salen y los contadores que gasta. Los textos fijos (la oferta, los botones, el mensaje de derivar) salen del caso, del perfil o de los resources. Solo «responder con el conocimiento» lleva texto del modelo;
     - **si va sola, y su orden:** el formulario va solo (154).
   - **El registro:** `ConversationActionCatalog`, como `AssistantActionCatalog` de la 0068.
     - **Al arrancar,** junta las acciones por DI y comprueba que haya una por clase.
     - **En cada turno,** filtra las permitidas en la bandeja y sella las que ofreció.
     - **Lo propuesto** que no estaba sellado deriva.
   - **Una acción nueva** es una clase, sus textos y su escenario de la batería genérica. El servicio, el writer y el Assistant no cambian.
   - **Un valor nuevo del enum** se lee un despliegue antes de escribirse ([0060](0060-dos-versiones-conviven.md)).
3. **Cada módulo hace lo suyo.**
   - **El modelo** propone un `ConversationProposal` tipado y nunca escribe: lo ejecuta WhatsApp.
   - **El Assistant** no conoce WhatsApp ni Formularios. Recibe las acciones permitidas como datos dentro de `ConversationTurn`.
   - **Formularios** da los candidatos por su puerto.
   - **Cualquiera se puede quitar:** cada puerto tiene su apagado en el núcleo ([módulos](../rules/modulos.md)).
   - **El proveedor de IA** sigue detrás de su adaptador (`IChatClientFactory`), con el modelo calificado del canal (0054 §9).
4. **Las fallas: cada respuesta del proveedor es definitiva, transitoria o desconocida** (lista de control de `/puerta`, punto 12).
   - **Transitoria:** se reintenta en la misma cola. El turno es idempotente por `TurnId` y no cobra dos veces (B4).
   - **Definitiva o vencida:** deriva con «El asistente no pudo responder». Lo que vence sin llegar al proveedor devuelve el cupo.
   - **Desconocida** («Sin confirmar», [0066](0066-lo-que-no-se-sabe-si-salio.md)): deriva y no se repite.
5. **El aislamiento.**
   - El worker entra con la organización del chat.
   - `ChannelConversationId` va en el contexto, en la recuperación y en la cancelación, así tres personas a la vez no se mezclan.
   - Las lecturas del modelo corren en el alcance de solo lectura de la 0068, con la organización del turno.
   - La caché lleva el alcance `t:` ([caché](../rules/cache.md)).
6. **Cómo se suma lo que venga.** Son caminos escritos, no código de esta etapa:
   - **una acción nueva:** el punto 2;
   - **otro asistente o otra regla de quién contesta:** otra estrategia en `WhatsAppAgentResolver` (E24);
   - **otro canal, como un chat web:** implementa el mismo `IConversationAgent` con su menú de acciones, y el Assistant no cambia;
   - **otra fuente de hechos de un módulo, como un catálogo o ARCA:** un puerto lector del núcleo que suma hechos confirmados al turno (0068 §5). Lo decide la etapa que lo necesite.

## Consecuencias

- **Un sufijo nuevo:** la E23 suma `ConversationAction` a la lista cerrada de [capas y flujo](../rules/capas-y-flujo.md), igual que la etapa de acciones suma `ActionSource`.
- **Ningún método es largo:** el servicio del turno queda corto, y las reglas de cada acción viven en su clase. El umbral de complejidad baja con el tiempo sin reescribir el turno.
- **Más clases chicas:** es más fácil de probar y de revisar, a costa de más archivos.

## Alternativas descartadas

- **Un `switch` por acción dentro del servicio:** cada acción nueva toca el centro del turno.
- **Un pipeline genérico de pasos (`IStep`) o handlers por mensaje:** contradice la regla 1 de `AGENTS.md` y suma una abstracción que el resto del sistema no usa.
- **Que el modelo arme los textos fijos o elija el formulario libremente:** la 0054 los deja exactos, y el modelo solo elige entre lo sellado.

## Verificación

- **`ConversationActionContractTests`:** una batería genérica, con una teoría por acción. Prueba que:
  - una acción no permitida en la bandeja deriva;
  - cada tope deriva con su motivo;
  - las acciones salen en su orden, y el formulario va solo;
  - los textos fijos no salen del modelo;
  - no escribe filas fuera del outbox y del porqué;
  - lo propuesto y no sellado deriva;
  - las instrucciones escondidas en un mensaje no cambian el menú.
- **`ConversationActionCatalogTests`:** una acción por clase, con sus textos en es y en.
- **Tests de arquitectura:** el Assistant no referencia WhatsApp ni Formularios, y nadie llama al modelo fuera de las piezas del asistente (E23, «Cómo se cierra»).
- **Calificación:** los casos de evaluación del modelo del canal cubren cada acción (0054 §9).
