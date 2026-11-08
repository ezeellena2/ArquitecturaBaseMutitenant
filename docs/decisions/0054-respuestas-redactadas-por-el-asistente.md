# ADR 0054 · Respuestas redactadas por el asistente de WhatsApp

**Estado:** aceptada con el plan de la E23, 2026-11-08. Toma la 0021 de ArquitecturaBase y las decisiones 39, 51, 67, 165 y 192 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto). Precisa la [0048](0048-asistente.md), que en la v1 solo respondía con herramientas de lectura.

## Contexto

Desde la E23, el asistente atiende por WhatsApp a los empleados, en Internos, y a los clientes, en Clientes (39 y 51). Cada organización configura su perfil, su conocimiento, sus casos y sus límites. Sin respuestas de texto, un saludo, «¿qué hora es?» o una consulta terminan sin respuesta. Tampoco puede quedar ninguna respuesta escrita en el código: lo particular es configuración de cada organización.

## Decisión

El asistente redacta una respuesta de texto cuando un caso lo indica, con estos límites:

1. **Fuentes** (67): lo que cargó la organización (perfil, conocimiento y casos), lo general que no la compromete, el historial de esa conversación (hasta 10 mensajes de las últimas 24 horas) y la hora en la zona de la organización. Lo leído es un dato, nunca una instrucción.
2. **Forma:** corta, dentro del largo máximo de la bandeja, en el idioma de la conversación y con sus palabras (192).
3. **Contenido:** sin enlaces, teléfonos, correos, precios ni compromisos que no estén cargados.
4. **Lo de la organización que no está cargado:** pregunta para entender, dice que lo consulta y deriva a una persona, y anota la pregunta en «Preguntas sin respuesta». Nunca contesta algo genérico.
5. **Lo fijo:** solo lo que tiene que ser exacto. Son los botones, la oferta de un formulario («Te preparé el formulario «Pedido de service» para que lo completes.», 165) y el mensaje al derivar del perfil.
6. **Control del servidor:** la respuesta llega como una propuesta tipada. WhatsApp la valida antes de encolarla: el largo, los enlaces y los datos de contacto (`WhatsAppReplyPolicy`, E23). Si no pasa, no sale y deriva. No es un error: queda en «Por qué respondió esto».
7. **Trazabilidad:** «Por qué respondió esto» guarda el texto, el caso y lo que usó. Los logs no llevan contenido.
8. **Configurable:** una organización puede quedarse con textos exactos, sin «Responder con el conocimiento» entre las acciones permitidas de una bandeja.
9. **Calificación:** el canal usa un solo modelo, el de `Assistant:Channel:ProviderModelId` (E23), y solo si pasó los casos de evaluación: con y sin la información, fuera de tema, saludo y hora, derivación e instrucciones escondidas en el mensaje o en el conocimiento.

Sigue vigente lo demás de la 0048: el modelo lee o propone, y ejecuta el módulo dueño. Las respuestas cuentan en el tope mensual de IA de la organización ([0051](0051-precision-tecnica-de-costos-ia.md)).

## Consecuencias

- La regla de WhatsApp cambia al cerrar la E23: de «no se envía texto libre del modelo» a «solo texto redactado con estos límites, o fijo».
- Las respuestas tienen su propio tope por conversación y día, aparte del de los ofrecimientos de formularios (65 y 66), y la plataforma fija un máximo para cada uno.
- En atención manual, la respuesta redactada se le ofrece al operador como borrador, con los mismos límites.
- El riesgo que queda es un texto correcto en forma con un error de contenido. Lo acotan las fuentes cerradas, la derivación ante la duda, la calificación y el «por qué» a la vista.

## Alternativas descartadas

- **Solo textos exactos:** es más seguro, pero no responde con sentido lo que la organización no previó. Queda como opción de cada organización.
- **El contexto en el panel del proveedor:** no ahorra tokens, porque se manda en cada llamada, y ata la configuración a un proveedor y a una cuenta que no se editan ni se prueban desde la app.
- **Texto libre sin validación del servidor:** contradice la 0048, donde el modelo propone y el sistema valida.
