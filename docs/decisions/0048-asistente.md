# ADR 0048 · El asistente: un LLM en el inicio, como módulo quitable

Estado: aceptada el 2026-10-07 por el dueño del producto. El detalle de D1 a D26 está en «Decidido con el usuario» de la [propuesta](../plans/propuesta-asistente-llm.md); D6 se resuelve en esta ADR. La [0049](0049-precisiones-whatsapp-y-asistente.md) precisa mensajes personales, destinatarios de alertas y alcance diario el 2026-10-08.

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
