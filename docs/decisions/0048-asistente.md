# ADR 0048 · El asistente: un LLM en el inicio, como módulo quitable

Estado: aceptada el 2026-10-07 por el dueño del producto. Las decisiones D1–D26 están en la tabla de esta ADR../plans/propuesta-asistente-llm.md); D6 se resuelve en esta ADR. La [0049](0049-precisiones-whatsapp-y-asistente.md) precisa mensajes personales, destinatarios de alertas y alcance diario el 2026-10-08.

## Contexto

El dueño del producto quiere un chat como Claude o Codex en el inicio de Plataforma, de la empresa y de la persona, que responda solo sobre el sistema. La plataforma lo provee y cada organización puede, si la plataforma la habilita, usar su propia clave.

## Decisión

- **Un módulo quitable,** «Asistente», con el patrón de la Etapa 8. Se programa justo después de ella, en tres partes ([etapa-en-paralelo](../guides/etapa-en-paralelo.md)). Se habilita:
  - por organización, desde Plataforma › Módulos;
  - para las personas, desde Configuración de Plataforma.
- **Solo responde, con herramientas de solo lectura** que llaman a los servicios existentes en nombre de quien pregunta, con su organización y sus permisos. Nunca escribe en la v1.
- **Las piezas:**
  - el puerto es `IChatClient` (Microsoft.Extensions.AI), con el SDK oficial de Anthropic para C# como adaptador y un `FakeChatClient` para los tests y el E2E;
  - el streaming va por SSE de .NET 10;
  - en el front, assistant-ui con nuestro tema.
- **El modelo se elige con un desplegable** de un catálogo de modelos y precios, que es dato de referencia:
  - con la clave de la plataforma, la organización elige entre los modelos que la plataforma permite;
  - con su propia clave, elige cualquiera del catálogo.
- **Las conversaciones:**
  - solo las lee quien las creó;
  - vencen a los 30 días sin actividad, salvo las guardadas;
  - la baja de la cuenta las borra todas;
  - nunca hay texto en los logs.
- **Lo que trae la v1:**
  - consultas fijas que no usan el modelo;
  - topes de 50 mensajes por persona por día y un tope mensual en dólares por organización, con aviso solo cerca del tope;
  - 👍/👎, enlaces a la pantalla, exportar en Markdown y PDF, y adjuntar imágenes y PDF (10 MB, 5 por mensaje).
- **Los adjuntos** van por un puerto de archivos del núcleo, `IFileStore`.
- **También explica cómo usar la app,** con artículos de ayuda que escribe la plataforma.
- **Aviso la primera vez,** con una versión nueva de Términos y Privacidad que nombra al proveedor de IA.
- **Correos al 80 % y al 100 %** del tope mensual.
- **Títulos automáticos** con el modelo más barato.
- **Dónde se ve:** en la compu, el asistente es el inicio; en el teléfono, una pestaña de la barra de abajo.

## Consecuencias

- **Etapas que toca:**
  - el inicio de las etapas 4, 6 y 7;
  - Plataforma (E5): Módulos, Configuración y Uso;
  - la E9, que lo endurece;
  - la E10, que define los secretos, el almacenamiento en la nube y la exportación de datos personales.
- **Permisos nuevos:**
  - `assistant.use` y `assistant.configure`;
  - `platform.assistant.use`, `platform.assistant.configure` y `platform.assistant.read`.
- **Diseño dibuja** el inicio de los tres lados, el teléfono, Configuración, Uso y Módulos antes del plan ejecutable.

## Decisiones aceptadas D1–D26

Las precisiones posteriores de ADR 0049, 0050 y 0051 y del usuario conservan su precedencia.

| Fecha | Decisión | Respuesta |
|---|---|---|
| 2026-10-07 | D1 · Lugar | **En la compu, el asistente es el inicio.** Lo de hoy («Para resolver», tarjetas) pasa a ser su bienvenida. En el teléfono, una pestaña aparte. |
| 2026-10-07 | D3 · Habilitar | **Módulo habilitable, apagado por defecto:** por organización desde Plataforma › Organización › Módulos, y para las personas desde Configuración de Plataforma. |
| 2026-10-07 | D2 · Clave propia | **Solo si la plataforma la habilita** por organización («puede usar su clave»). Por defecto, la de la plataforma. |
| 2026-10-07 | D5 · Privacidad | **Solo quien creó la conversación la lee.** El Dueño y la plataforma ven el uso, nunca el texto. |
| 2026-10-07 | D7 · Modelo | **Totalmente configurable en Plataforma,** con un desplegable de modelos. El catálogo de modelos y precios es dato de referencia (`ModelPrices`), no una lista en el código. Una organización con su clave elige del mismo desplegable. |
| 2026-10-07 | D4 · Retención | **Se borran solas a los 30 días sin actividad,** salvo las que la persona **guarda** (fijar o archivar): esas no vencen nunca. Se borran a mano, y todas, guardadas incluidas, con la baja de la cuenta. |
| 2026-10-07 | D9 · Consultas fijas | **Atajos sin modelo:** botones que llaman directo a una herramienta y muestran el resultado con un texto armado, gratis e instantáneos. Fijos por lado en la v1. |
| 2026-10-07 | D8 · Acciones | **Solo responde en la v1,** con enlaces a la pantalla donde se hace cada cosa. Las acciones con confirmación quedan para la v2. |
| 2026-10-07 | D11 · Quién lo usa | **Todos, por permiso:** `assistant.use` viene en todos los roles de sistema y el Dueño se lo puede quitar a un rol. Cada uno consulta solo lo que ya puede ver. |
| 2026-10-07 | D12 · Personas | **Sí en la v1,** con pocas herramientas (mi cuenta, mis organizaciones, mis interacciones). Se prende desde Configuración de Plataforma, con la clave de la plataforma. |
| 2026-10-07 | D13 · Extras | **Todo en la v1:** 👍/👎 por respuesta, enlaces a la pantalla ya filtrada, **exportar la conversación** y **adjuntar archivos o imágenes**. |
| 2026-10-07 | D14 · Adjuntos | **Imágenes y PDF** (PNG, JPEG, WebP y PDF), hasta **10 MB** y **5 por mensaje**. Se borran con su conversación. |
| 2026-10-07 | D15 · Archivos | **Un puerto de archivos del núcleo, `IFileStore`:** disco en desarrollo y nube en producción (S3 o Azure Blob, se elige en la E10). Lo reutiliza cualquier módulo que suba archivos. |
| 2026-10-07 | D16 · Exportar | **Markdown y PDF,** a elección de la persona. El PDF suma una librería en el back, que se elige en el plan. |
| 2026-10-07 | D17 · Teléfono | **Una pestaña «Asistente» en la barra de abajo,** con el chat a pantalla completa y el historial en una hoja. El inicio sigue siendo la billetera. |
| 2026-10-07 | D18 · Cuándo | **Justo después de la Etapa 8,** copiando su patrón de módulo quitable. |
| 2026-10-07 | D19 · Modelos con la clave de la plataforma | **La plataforma fija qué modelos se pueden elegir con su clave,** y la organización elige entre esos. Con su propia clave, elige cualquiera del catálogo. |
| 2026-10-07 | D20 · Contador | **Solo cerca del tope:** al quedar 10 mensajes, «Te quedan 10 consultas hoy». |
| 2026-10-07 | D21 · Ayuda | **También explica cómo usar la app.** Una herramienta lee artículos de ayuda que escribe la plataforma (uno por tema, en es y en) y responde con los pasos y el enlace a la pantalla. Los artículos son contenido configurable, no texto en el código. Cada área nueva suma los suyos. |
| 2026-10-07 | D22 · Aviso legal | **Aviso la primera vez** que alguien usa el asistente («Tus consultas las procesa un proveedor de IA; no subas datos sensibles», con «Entendido», que queda registrado) **y una versión nueva de Términos y Privacidad** que lo menciona. |
| 2026-10-07 | D23 · Alertas | **Correo al 80 % y al 100 % del tope mensual:** al Dueño si la organización usa su propia clave, y a la plataforma si usa la de la plataforma. Por el outbox, en es y en. |
| 2026-10-07 | D24 · Títulos | **Automáticos con IA:** después de la primera respuesta, el modelo más barato del catálogo le pone un título corto. Se puede renombrar, y el costo cuenta en el uso. |
| 2026-10-07 | D25 · Dónde vive la ayuda | **Archivos en el repo:** un `.md` por tema y por idioma (es y en), al lado del código de cada área (por ejemplo `Infrastructure/Modules/Assistant/Help/Articles/<area>/<tema>.es.md`). Los escribe quien programa el área y viajan con cada versión. Un test exige que cada área con pantallas tenga los suyos. No hay pantallas nuevas. |
| 2026-10-07 | D26 · Catálogo de modelos | **Una pantalla en Plataforma** (dentro de Configuración del asistente): una tabla de modelos con nombre, proveedor, ID, precio de entrada y de salida, y activo sí o no. Arranca con los modelos de Claude vigentes y la plataforma los mantiene sin programar. Diseño la suma al tablero de configuración. |
| 2026-10-07 | D10 · Topes | **Por persona y por organización:** 50 mensajes por persona por día y un tope mensual en dólares por organización, con un aviso claro al llegar. Los cambia la plataforma; una organización con su clave fija los suyos. |
| 2026-10-07 | D6 · Proveedor | Microsoft.Extensions.AI IChatClient con SDK oficial Anthropic en C#, SSE .NET 10 y assistant-ui; FakeChatClient sólo para tests/E2E. |
