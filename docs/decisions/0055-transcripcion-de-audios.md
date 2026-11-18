# ADR 0055 · Transcripción de audios

**Estado:** aceptada con el plan de la E23, 2026-11-08. Cumple la decisión 27 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md#5-decisiones-de-producto), que deja elegir el proveedor al planificar el asistente.

## Contexto

El asistente de WhatsApp atiende audios, que llegan en OGG con Opus. El proveedor del asistente, Anthropic ([0048](0048-asistente.md), D6), no transcribe. El costo tiene que entrar en el tope mensual de IA de la organización ([0051](0051-precision-tecnica-de-costos-ia.md)), y el núcleo no puede conocer al módulo Assistant. ArquitecturaBase transcribe con OpenAI en su piloto, con OGG, WebM, MP4 y WAV.

## Decisión

- **Proveedor:** OpenAI, `/v1/audio/transcriptions`, con el modelo de `Speech:OpenAI:Model` (por defecto `gpt-4o-mini-transcribe`).
- **Puerto del núcleo:** `ISpeechTranscriber.TranscribeAsync(Stream audio, string mediaType, CancellationToken ct)` devuelve `Result<string>`, en `Application/Interfaces/Integrations/Speech/` (E23). El apagado vive en `Infrastructure/Speech/` y dice «No pudimos pasar este audio a texto».
- **Quién lo implementa:** el módulo Assistant (`AssistantSpeechTranscriber`, E23). Reserva y cierra el costo en el tope de la organización activa y llama al adaptador HTTP. Sin el módulo queda el apagado, y WhatsApp lo usa sin nombrarlo.
- **Clave:** la de la plataforma, en `Speech:OpenAI:ApiKey`, en user-secrets o variables de entorno, cargada con `scripts/secretos/`. Nunca va en el repo ni en los logs.
- **Precio:** el modelo de transcripción entra en el catálogo de modelos y precios (D26 de la 0048), por millón de tokens, y se cobra con el uso que devuelve el proveedor (0051).
- **Privacidad:** el audio y su transcripción quedan con el chat. Los Términos y la Privacidad nombran a OpenAI (D22 de la 0048): la versión nueva la publica Plataforma desde «Legales», y es un pendiente del usuario en la puerta de la E23.

## Consecuencias

- Un tope agotado, una falla o la falta de clave dejan el audio sin transcripción, con «No pudimos pasar este audio a texto», y el chat sigue. El asistente no contesta un audio que no pudo leer.
- **Precisión 2026-11-18 ([0064](0064-reglas-de-toda-la-ia.md)):** una transcripción que venció sin llegar al proveedor devuelve el cupo, y la que llegó cuenta aunque termine incierta; sin cupo no se transcribe, y en el Inicio el audio ni se guarda. El mismo audio dos veces con el mismo pedido da una sola transcripción. Las fallas del proveedor llevan su código (0064 §7); con la clave de la plataforma, las arregla el operador.
- La transcripción la ve también el operador, debajo del audio.
- Sumar otro proveedor es otro adaptador del mismo puerto, sin tocar WhatsApp.

## Alternativas descartadas

- **Transcribir con el modelo del chat:** Anthropic no recibe audio.
- **Un proveedor sin referencia probada:** ArquitecturaBase ya paga la puesta en marcha con OpenAI, con los mismos formatos.
- **Que la organización cargue su clave de transcripción:** la 27 la deja en la configuración de la plataforma; el costo igual entra en el tope de la organización.
