# ADR 0057 · La IA propone y una persona crea

**Estado:** aceptada el 2026-11-08, con el [plan de la E25](../plans/2026-11-08-etapa-25-documentos.md); se implementa en esa etapa. Toma la 0024 de ArquitecturaBase («La IA arma formularios y catálogos a pedido») y precisa la D14 de la [0048](0048-asistente.md).

## Contexto

El chat del Inicio solo consulta, con herramientas de solo lectura (0048). La E25 suma armar formularios y catálogos con la IA, en el Inicio de la organización y por WhatsApp, con un texto, un audio o un archivo (decisiones 75, 102, 154 a 156, 167, 171 y 192). Una configuración propuesta por un modelo no puede quedar creada, publicada ni activa sin que una persona lo decida.

## Decisión

1. **La IA propone.** Con `forms.manage`, un formulario; con `formcatalogs.manage`, un catálogo. Las herramientas `read_forms`, `read_catalogs`, `propose_form`, `propose_catalog` y, por WhatsApp, `decline_authoring` leen o proponen; ninguna escribe. Forms valida la propuesta con las reglas del editor, sin recortarla. Un dato inválido vuelve al modelo con la ruta de cada argumento, para que lo corrija en el mismo turno.
2. **Crear lo pide una persona,** con un botón, una sola vez por propuesta (`FormAuthoringOutcomes`). Lo hace el servicio de Forms, con una UoW y los permisos de ese momento. Repetir devuelve lo creado, y una propuesta reemplazada no se crea. La IA nunca publica, activa, importa ni cambia lo que existe. El formulario queda en borrador, y el catálogo, inactivo hasta «Aprobar y activar» (167).
3. **Una sola propuesta para tres lugares.** Forms la valida y la presenta. La tarjeta del Inicio, el resumen de WhatsApp y «Armar formulario con la IA» la muestran, cada uno a su manera, sin interpretar un formulario.
4. **Al proveedor** va el pedido, con el audio ya transcripto, el archivo preparado (de una planilla, los encabezados y unas filas) y los nombres y campos de lo que la persona puede ver. Nunca respuestas, opciones cargadas ni datos de personas. Lo leído es un dato, nunca una instrucción.
5. **Precisa la 0048, D14.** En el Inicio de la organización, el clip suma `.xlsx`, `.csv` y `.docx` para armar, de a uno y solo con el permiso. El micrófono va solo ahí (171). Graba WebM o MP4, reconocido por sus bytes, de hasta 1.000.000 bytes, y lo transcribe el servidor con `ISpeechTranscriber` (0055), dentro del tope de la organización. El audio vive mientras un mensaje lo nombra, y su copia se confirma después del commit.
6. **Por WhatsApp,** armar se atiende antes de los casos, en el chat de una cuenta vinculada (`Linked`) en modo Asistente. Vale solo el botón del último resumen, y un archivo va a la lectura de documentos (0056), no a armar.
7. **Un pedido vago** trae enseguida una propuesta, y la IA pregunta una sola vez, solo si no puede saber qué armar (192). Es fijo solo lo exacto: los botones, el resumen, la oferta y el mensaje al derivar.
8. **Topes.** El turno que ofrece armar tiene 8192 tokens de salida (`AuthoringMaxOutputTokens`), dentro de las 5 llamadas. Arma el modelo del turno: en el Inicio, el de la organización; por WhatsApp, el de `Assistant:Channel:ProviderModelId`.

## Consecuencias

- Forms suma `FormAuthoringOutcomes`, el catálogo por aprobar y las rutas de crear.
- Assistant suma la propuesta y el audio en sus mensajes.
- WhatsApp suma el resumen y sus botones, guardados como una acción del asistente.
- El front muestra la tarjeta por un punto de extensión, `assistantProposal`, sin que el módulo del asistente nombre a Forms.
- Una propuesta de un documento puede sugerir una plantilla de la biblioteca: «Usar la plantilla» la copia con la copia de la E22.

## Alternativas descartadas

- **Crear al proponer:** lo que entendió la IA se ve recién en la app, y cada pedido deja un borrador.
- **La propuesta en un panel al costado del hilo:** el usuario eligió la tarjeta en el hilo (154).
- **El micrófono en el asistente personal y en el de Plataforma:** su costo no es de una organización (171).

## Verificación

Antes de habilitar el perfil, se califica con casos ficticios: pedidos escritos y dictados, planillas en papel, Excel con y sin encabezados, pedidos vagos y nombres que ya existen. Los tests de la E25 prueban los nueve puntos de «Lo que ya se pagó allá» de la spec: una propuesta inválida vuelve al modelo, crear una sola vez, el reemplazo por clase, los topes, el último botón, el audio que no gasta el cupo, WebM y MP4, la forma de un turno sin propuesta y los CHECK en una línea.
