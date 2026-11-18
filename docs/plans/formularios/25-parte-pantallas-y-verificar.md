# E25 · Documentos por WhatsApp: pantallas y qué verificar (spec, parte)

**Estado:** pendiente · parte de la [spec de la E25](25-documentos.md), que dice qué leer y cómo se cierra.

## Pantallas

Aprobados y publicados en el lienzo v168, con sus tres recorridos guiados: el comprobante que se reconoce y queda en la planilla, el que no se reconoce y termina en «Armar formulario con la IA», y el catálogo «Proveedores de obra», pedido con un Excel («Proveedores» ya existe desde la 12). Muestran lo que existe hasta acá. Las del catálogo propuesto vienen de la E23, que las retiró (167).

**Con el traspaso 15** (lienzo v175, sin publicar) suman sus estados: en la bandeja, «Venció», «Protegido», el tope del día y pasar un repetido; en la lectura, «Lo escribió la IA» y los ejemplos solo para personas; en el chat, el tope del día, el PDF protegido y las planillas; en el Inicio, armar desde un documento, el prefijo corregido, importar en un catálogo que existe, la propuesta que no entra y borrar con sus audios, y por WhatsApp, el motivo cuando no crea y la planilla con un pedido.

**Valen las de toda la app** (160 a 164, 173 a 175 y 180): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez.

| Tablero | Qué muestra |
| --- | --- |
| `WA-Chats-Documento` y su teléfono | los tres casos, los otros formatos y los casos difíciles, con «Por qué lo propuso» |
| `M-WA-Flow-Prellenado` | el formulario prellenado: «Revisá», «No se leyó» y el documento adjunto |
| `FRM-Documentos` y su teléfono | en el panel de «Gestión de formularios», debajo de «Formularios»: la bandeja, con su panel, «Cargada automáticamente» y los permisos |
| `FRM-Editor-Lectura` y su teléfono | la sección en el editor (DOC1): de qué plantilla viene, los ejemplos, qué pregunta recibe cada dato, las validaciones, «Cargar solo si es seguro» y la precisión por formulario y por dato |
| `Plat-FRM-Plantilla-Lectura` | la sección en la plantilla, con candado |
| `FRM-Plantilla-25` y su teléfono | la vista de una plantilla de la biblioteca: qué lee y sus validaciones fijas, con candado |
| `FRM-Armar-Con-IA` y su teléfono | la propuesta, con los documentos a la vista y lo que se repite como un bloque, y «Crear borrador» |
| `FRM-Respuesta-25` y `M-FRM-Respuesta-25` | de qué documento salió y quién la revisó |
| `Campana-25` y su teléfono | los documentos por revisar y «Llegaron N documentos parecidos» |
| `WA-Asistente-Casos-25` | el tipo de entrada «documento» y el bloque fijo de armar, arriba de los casos |
| `Asistente-Org-25` y su teléfono | en el chat del Inicio de la organización, la tarjeta de un formulario y la de un catálogo, «Ver cómo queda», «Nueva», «Reemplazada», «Borrador creado» y «Catálogo creado», sin permiso y «ya existe», el clip y el micrófono |
| `M-WA-Armar-Con-IA` | por WhatsApp, el pedido con un audio, el resumen de un formulario y el párrafo de un catálogo, sin permiso, «ya existe», un botón viejo y el enlace |
| `FRM-Catalogo-Propuesta-25` | el catálogo que deja «Crear catálogo», que se edita como cualquiera, con «Propuesta del asistente», quién lo pidió, «Descartar propuesta» y «Aprobar y activar» |
| `FRM-Formularios-25`, `FRM-Catalogos-25` y `FRM-Faltan-25` | «Armar con la IA» junto a «Nuevo formulario», junto a «Nuevo catálogo» y en cada tema; en la lista de catálogos, «Propuesta del asistente» |

**Mandan sobre esta spec en:** las fichas del front y la decisión 43. Los mensajes del sistema van en el idioma de la cuenta (42). «Dar de baja» no aplica: un documento se descarta.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | Antes de planificar: si un Flow acepta un archivo precargado. | **Si no se puede,** el Flow saltea esa pregunta, y al capturar la respuesta toma el documento guardado. La persona ve «Adjuntamos tu documento», sin «Cambiar documento». |
| [Meta] | Los botones y las listas: 3 y 10. | Lo de la API de hoy. |
| [E18] | El archivo del chat. | Se lee por un puerto, y Forms guarda su copia. |
| [E20] | La carga de la web. | «Pasar al formulario» la usa a nombre de quien mandó el documento. |
| [E22] | `FormTemplateDraft` y la copia. | Una sección más se copia sola. |
| [E23] | La ráfaga, el prellenado y la conversación de una cuenta vinculada. | Están, y el prellenado deja afuera «elegir varias» y las preguntas del bloque. |
| ADR | Las de la lectura de documentos y de la IA que arma, que `/plan-etapa` escribe al planificar, desde la 0022 y la 0024 de allá. | Siguen esas dos: la IA propone y una persona crea (154). |
| Asistente | El `Message` y el `done` del SSE de la E8b, su clip y los topes del turno: 5 llamadas, contando el título, y 2048 tokens de salida. | `Message` suma `proposal?` y `audio?` sin romper lo que lee el front, y una propuesta de 40 preguntas entra en 2048 tokens; si no entra, se sube solo el tope del turno que arma. |
| Bibliotecas | Con qué se saca el texto de `.docx` y `.xlsx` y las páginas de un PDF. | `.xlsx` con ClosedXML; lo demás se elige al planificar, sin ejecutar nada. |
| Parecidos | Cómo se mide que dos documentos se parecen. | Por la clasificación que devuelve la IA. |
| Allá | «La IA arma, etapas 2 y 3» y la revisión de la IA, que llegaron con el traspaso 15 sin la prueba integral de allá. | Están en esta spec (215 a 223 y la 0064); al planificar se contrasta con `origin/main`, por si el traspaso 16 cambió algo. El pedido vago ya está decidido acá (192). |
