# E20 · Cargar una respuesta desde la web (spec)

**Estado:** pendiente · spec de la etapa 20 del bloque (decisiones 73, 78, 83 a 90, 94, 95, 98, 101, 153, 166, 177, 183, 184, 229, 230, 232 a 235, 261 a 264, 266 y 269; la 229 reemplaza la 96, la 104, la 178 y la 200, la 234 cambia la 97, la 261 cambia la 83 en «Cargar» y la 262, la 141), con el [formato](plantilla.md) de las etapas. Corresponde, en ArquitecturaBase, a las etapas 1, 2 (lo propio), 4 (listas dependientes) y 5 (títulos de sección) de «Cargar formularios desde la app», a la etapa 2 de sus renglones (T12), a SA1-A a SA5 («Dónde se completa») y a UB1-A a UB4-A y W1-A («Ubicación»).

**Partes.** Lo que no figura en esta tabla está acá.

| Secciones | Archivo |
| --- | --- |
| «Cómo se arma cada pregunta», «El bloque en la carga», «Los textos», «Desde el celular», Contrato, Datos, Pasos y «Lo que ya se pagó allá» | [armado, contrato, datos y pasos](20-parte-contrato-datos-y-pasos.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T4, T22, T23, T24 y la §6, punto 7; la [E15](15-respuestas.md): rutas, filtros y [«Cómo se lee una respuesta»](15-parte-datos-y-contrato.md#cómo-se-lee-una-respuesta-263-266-y-269); la [E16](16-datos-de-las-personas.md): los filtros por destinatario; la [E17](17-ciclo-de-vida.md): la disponibilidad; la [E18](18-fotos-y-documentos.md): verificar y guardar archivos, y la [E19](19-condiciones-calculos-y-lineas.md): la captura y el bloque.
2. **Decisiones** de la hoja de ruta: 9, 23, 35, 37, 43, 50, 54, 73, 78, 83 a 90, 94, 95, 98, 101, 141, 147, 153, 166, 177, 183, 184, 207, 229, 230, 232 a 235, 261 a 264, 266, 267, 269 y 270.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [idempotencia](../../rules/idempotencia.md) | el `POST` con archivos, en modo durable |
   | [guardado](../../rules/guardado.md) y [multitenancy](../../rules/multitenancy.md) | subir fuera de la UoW; quien carga sale de la sesión |
   | [permisos](../../rules/permisos.md) y [api-http](../../rules/api-http.md) | rutas aparte para cada permiso |
   | [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) | las opciones, de a 50, y lo propio |
   | [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | cada error en su pregunta, también el de un ítem |
   | [textos-y-traducciones](../../rules/textos-y-traducciones.md) y [tests](../../rules/tests.md) | «App», «ítem» y qué lleva test |

4. **Código para copiar:** la columna «Copia de» de los [pasos](20-parte-contrato-datos-y-pasos.md#pasos).
5. **Referencia de producto,** solo para leer: en ArquitecturaBase, `docs/plans/2026-10-30-traspaso-multitenant-carga-desde-la-app.md`, `docs/plans/2026-10-29-cargar-formularios-desde-la-app.md` y, en `docs/features/formularios.md`, «Carga desde la app», «Lo propio», «Listas dependientes», «Títulos de sección», «Cálculos», «Formularios sólo en la app» y «La pregunta «Ubicación»», y en `whatsapp.md`, «La ubicación antes del Flow»; para el bloque, el traspaso 12 (`docs/plans/2026-11-11-traspaso-multitenant-renglones.md`) y «El bloque de renglones en la carga». En `ArquitecturaBaseFront`, `docs/design/carga-desde-la-app.md`, `renglones-en-formularios.md`, `formularios-solo-en-la-app.md` y `ubicacion-en-formularios.md`. El código está en `origin/main`. Mandan esta spec y las reglas y los diseños de este proyecto (108).

## Qué se construye

**Un canal más para responder:**
- un empleado completa un formulario desde la web, a su nombre. Es un miembro activo con `formrecords.create`, sin teléfono verificado (89), y solo en los formularios activos que son para empleados o para los dos (50). Un cliente sigue respondiendo por WhatsApp;
- la respuesta guarda el canal `web`, el `SourceKey` `web:<clave>` y las empresas activas de quien carga (9);
- **la misma captura que WhatsApp:** un solo lugar valida y numera contra la versión sellada, con catálogos, archivos, condiciones, cálculos, la ubicación y el bloque, y revisa las opciones elegidas por lote, no de a una;
- **un número escrito** se lee con la región del idioma de quien carga: en español, coma decimal y punto de miles. Lo que se lee distinto con y sin los miles («1.500») es un error de su pregunta, `numberAmbiguous`, que pide escribirlo sin separador de miles, y al servidor llega el número con punto (207);
- **solo la versión activa.** Si se publicó otra, el front ofrece pasar a la nueva sin perder lo cargado;
- **la oferta sella solo lo elegido:** cada opción tiene que estar activa y pasar los filtros para esa persona, también en un catálogo de miembros o de contactos. La lista completa no se guarda;
- **los filtros por persona:** si le falta el dato, la pregunta frena y lo nombra. Si los filtros dejan una sola opción, ya viene elegida. Las opciones no tienen el tope de 200: se piden de a 50, con búsqueda, y los filtros corren en SQL antes de paginar.

**Las listas dependientes** (94): un tercer filtro, «según otra respuesta»:
- la pregunta de la que depende va antes, es de elegir una y elige del mismo catálogo que el campo de papel «Filtro». No oculta la pregunta: achica sus opciones. En el editor se elige con la pieza (262), y guardar sin elegirla se frena en la pantalla, marcando solo ese campo (269);
- al guardar se comprueba que la opción corresponda a esa respuesta. Si la pregunta de la que depende quedó oculta, la lista también;
- va en una pregunta suelta, no en una del bloque. Copiar un formulario la copia;
- no lleva los límites de WhatsApp: ni el aviso del máximo por persona ni el freno de «elegir varias» con más de 20.

**Archivos:** viajan en el mismo `POST`, con los límites de la E18, y se verifican por contenido, igual que los de WhatsApp. Completa o nada.

**Reintentos:** la misma clave con las mismas respuestas devuelve el primer resultado, aunque después se haya publicado otra versión o se haya dado de baja el formulario. El número y los cálculos son privados, por eso no va en `BufferedJson`. La misma clave con otras respuestas es 422; para el front, otras respuestas son otro envío. **Un rechazo pasajero** (`Forms.Record.NotAllowed`, 403) **no deja recibo** (lección 10 de la parte). Después de un 400 sin errores por pregunta, el front arma otra clave. Ningún texto habla de «la sesión».

**«Dónde se completa»** (229; precisa la 98 y la 184):
- **un ajuste del formulario,** en «El formulario», al lado del prefijo, en la pieza (262): «En la app y por WhatsApp», el de uno nuevo, o «Solo en la app». Va con el borrador y cada versión lo sella. Aparece solo en una organización con WhatsApp conectado: sin número, todo se carga en la app (98) y se publica sin Meta, con el aviso «Queda solo para la app», y uno solo para clientes no se publica, con «Ir a «Para quién es»». En uno solo para clientes, «Solo en la app» está apagada, con «Los clientes responden solo por WhatsApp.»;
- **con «En la app y por WhatsApp»,** lo que WhatsApp no puede mostrar frena al publicar, en «Problemas», con «Ir a la pregunta» y, si no es solo para clientes, «Pasar a «Solo en la app»»: una lista dependiente, un catálogo a mano de más de 200 opciones en «elegir una» o de más de 20 en «elegir varias», un bloque con un mínimo de más de 10 ítems, una condición que compara números o fechas, demasiadas preguntas en una pantalla, una respuesta demasiado grande o una segunda «Ubicación». En el editor, el aviso de la E19 debajo de una condición que WhatsApp no puede mostrar suma la misma salida (235);
- **con «Solo en la app»** se va todo lo de WhatsApp: no pasa por Meta, `Check` ni el tope de opciones de un filtro fijo; valen los límites de la app, y publicar es al instante: sella la versión y la activa en la misma UoW, sin trabajo durable ni Flow. El editor no muestra «En WhatsApp» en las preguntas de catálogo, la (i) de WhatsApp del máximo del bloque ni «Ver vista oficial»;
- **pasar de un lado al otro** es publicar con el otro valor: hacia la app, al instante; hacia WhatsApp, espera a Meta, y mientras tanto, o si Meta la rechaza, sigue la versión anterior. Uno publicado sin número se manda por WhatsApp al volver a publicar con un número: `NothingToPublish` no aplica. Cambia lo que la E14 dice de `ChannelUnavailable` y `CatalogTooLarge`;
- **en el chat,** «Enviar formulario» muestra apagado en la pieza (184 y 262), con su motivo, «Se completa solo en la app.», el formulario cuya versión activa se completa solo en la app. Uno publicado sin número, cuando después se conecta uno, aparece como en un número sin el Flow de su versión (103), hasta volver a publicar.

**La pregunta «Ubicación»** (230): un punto al completar, con latitud, longitud y precisión, que no prueba presencia:
- **en el editor,** es el último tipo y solo lleva «Obligatoria». No entra en el bloque, donde aparece apagada como Foto y Documento, y ninguna condición ni cálculo se apoya en ella;
- **en la carga,** va en «Datos», en una columna. «Usar mi ubicación» la pide al tocar, con alta precisión y hasta 20 segundos, y la muestra en un recuadro como un archivo, con la precisión en gris, la ✕, «Ver en el mapa» y «Volver a pedirla». Con más de 100 m, la precisión va en ámbar con su (i), y se puede enviar igual. Si el navegador no la da, dice por qué;
- **en la planilla,** el punto es un enlace al mapa, sin ordenar (264) ni filtrar, como las fotos y los documentos (143); el contrato la acepta solo con `answered`, `unanswered` y `notAsked`. En el detalle es su control, en un renglón con su precisión y «Ver en el mapa» (266), y en el Excel, en tres columnas numéricas, las de los textos de la parte;
- **por WhatsApp,** antes del Flow sale por el número de la organización, dentro de las 24 horas, el pedido de ubicación de Meta, con «Seguir sin ubicación» si es opcional. Al llegar sale el Flow, y todo se guarda en una sola captura. Si no llega, el envío vence con su sesión, en el tiempo para contestar de la Configuración de WhatsApp de la organización (270, E14). La actividad suma sus renglones, «Se pidió la ubicación» y «Llegó la ubicación» o «Siguió sin la ubicación» (267). La ubicación que llegó se vacía al cerrar la captura o al vencer el envío;
- **es un dato personal** (23): se borra con la baja de la cuenta de quien la mandó, aunque la respuesta queda, y «Ver en el mapa» manda el punto a Google solo al tocarlo.

**Lo propio** (85): quien tiene `formrecords.create` y no `formrecords.read` lee solo sus respuestas, en la planilla, el detalle y los archivos, también lo que respondió por WhatsApp. El servidor filtra por `RespondentUserId`, y lo ajeno responde 404, también en una dirección directa. Quien solo carga ve «Formularios» con sus formularios, «Cargar» en el ⋮ (261) y tres columnas, un dato en cada una (263), que ordenan (264): «Mis cargas», «Mi última carga» (su número, que la abre, o «Todavía no cargaste») y «Recibida», su fecha; nunca los totales de la organización (153, que cambia la 83). El nombre abre «Respuestas» con lo suyo: la planilla de siempre, con «Empresa» (24), sin la columna «Cargó», sin «Cargó» en «Filtros» y sin «Exportar a Excel», con «Agregar respuesta». Los conteos también se acotan a lo suyo (lección 7 de la parte). **El filtro «Estado»** suma «Dados de baja», con los formularios dados de baja en los que cargó algo: ve lo suyo, sin «Cargar» (101).

**Las entradas** (83 y 261): «Cargar», la primera opción del ⋮ de cada fila de «Formularios», y en el teléfono, en la fila, con nuestra forma; «Agregar respuesta», en el encabezado de la planilla, abre en la compu el panel y en el teléfono la página. Ninguna aparece en un borrador ni en un dado de baja, y en uno solo para clientes quedan apagadas, con su motivo (43).

**La forma** (266): la carga es la forma con que la E15 lee una respuesta (zonas, grilla y anchos), vuelta editable. El detalle, con lo que se elige en la pieza (262), en la [parte](20-parte-contrato-datos-y-pasos.md#cómo-se-arma-cada-pregunta-86-87-141-262-y-266).

**La página «Nueva respuesta»** (84), en `/org/formularios/:id/cargar`, para la compu y el teléfono:
- Cancelar y el botón del formulario van en la banda; en el teléfono, ese botón queda fijo al pie;
- se valida al enviar: cada error en su pregunta y el foco en la primera. Después, en vivo. Salir con algo cargado pide confirmación;
- mientras sube, el botón dice «Enviando…» con su aro, y una barra de avance, sin palabras, muestra cuánto falta (95 y 115);
- lo cargado vive en la página: nunca va a la URL, al storage ni a la caché;
- **«Mantener las respuestas para la siguiente»** (147) va en la banda, antes de Cancelar y Enviar, y en el teléfono, en el pie fijo, arriba de Enviar. Marcada, al enviar el aviso dice el número y el formulario queda con lo cargado, sin los archivos y con el foco en la primera pregunta; sin marcar, se ve «Enviada». No se recuerda al volver a entrar.

**El panel** (84), al costado de la planilla, solo en la compu, con la misma forma que la página (266): «Mantener» conserva todo menos los archivos, y «Pantalla completa» pasa lo cargado a la página. Al enviar, el aviso dice el número y la planilla se relee con la respuesta marcada. **Cada envío es otra respuesta** (lección 5 de la parte).

**Las listas de catálogo:** una pregunta de catálogo es la pieza con las opciones del servidor, de a 50 (262). **Una lista dependiente espera** apagada, con «Elegí primero «Zona»» en su campo, y en el teléfono, en su botón (166). Con la respuesta elegida trae solo lo suyo, y se limpia si esa respuesta cambia.

**Las fotos y los documentos:** se arrastran o se eligen, con un contador. Tipo, tamaño y cantidad se revisan antes de enviar. En el teléfono, «Tomar foto» va primero, y «Solo cámara» no ofrece la galería.

**Los cálculos, en gris** (234, que cambia la 97): una vista previa que no se edita y cambia al escribir, también la de un ítem, en una columna más del bloque (233) y en los recuadros: el front evalúa la fórmula con la regla y el redondeo al par de la E19 (232), y al enviar vale la del servidor. Por WhatsApp sigue sin verse (54).

**El bloque** (146 y 177): una tabla en la página y un recuadro por ítem en el panel y en el teléfono, con «Agregar ítem», sus errores y sus textos, en la [parte](20-parte-contrato-datos-y-pasos.md#el-bloque-en-la-carga-146-177-y-183).

**«Enviada»:** lo que se mandó, los cálculos, el número y la hora, con «Cargar otra», «Volver a Formularios» y «Ver en la planilla» (166). Se arma con lo enviado y con lo que devuelve el `POST`.

**En lo que ya existe:** «App» en «Canal» y en su filtro, en los conteos, la navegación y el Excel (90); en el detalle, la línea gris dice que la cargó en la app, sin «Abrir conversación», y la actividad suma el renglón «La cargó», con quién y el canal «App» (267); en el editor, «Según otra respuesta». **En «Formularios»** (229), con WhatsApp conectado, la columna «En WhatsApp» pasa a «Estado» y se suma «Dónde se completa», con lo de la versión activa o, en un borrador, dónde se va a publicar; las dos ordenan (264). «Dar de baja» uno que se carga en la app dice «App · No se carga más».

## Pantallas

Aprobados en el lienzo como dice la tabla, con el armado del front real de ArquitecturaBase (126 y 134) y el recorrido guiado `cargar-una-respuesta`. El ejemplo es «Pedido de materiales», versión 4, que suma «Zona», «Proveedor» con su lista dependiente, «Foto del remito» y el bloque «Productos», y por eso se completa «Solo en la app»; el de la ubicación es «Visita a cliente».

| Tablero | Qué muestra | Estado |
| --- | --- | --- |
| `FRM-Formularios-20` y su teléfono | «Cargar», «Estado», «Dónde se completa» y lo que ve quien solo carga | Redibujados en la v175 (229). **Se redibujan:** «Cargar» en el ⋮ (261), un dato por columna (263) y el encabezado que ordena (264) |
| `FRM-Editor-20` y su teléfono | «Dónde se completa» al lado del prefijo, apagado en uno solo para clientes, y el aviso de una condición con «Pasar a «Solo en la app»» | Nuevos en la v175 (229 y 235); se redibujan: el aviso también debajo de una fecha anterior o posterior (229) y «Dónde se completa» en la pieza (262) |
| `FRM-Editor-Ubicacion` y su teléfono | el tipo «Ubicación», último, solo con «Obligatoria», y apagado en el bloque | Nuevos en la v175 (230). **Se redibujan:** el tipo en la pieza (262) |
| `FRM-Cargar-Respuesta` y su teléfono | la página, con zonas, grilla, títulos, lo condicional, el bloque, «Mantener», errores y salir | Redibujados en la v175: «Usar mi ubicación» con sus estados (pidiendo, el punto, más de 100 m, sin permiso y sin señal) y los cálculos en gris, también en el bloque (230, 233 y 234). **Se redibujan:** la forma de la E15, editable (266), lo que se elige en la pieza (262) y 🗑 en cada ítem del teléfono (261) |
| `FRM-Cargar-Panel` | el panel junto a la planilla | Redibujado en la v175 (230 y 234). **Se redibuja:** como `FRM-Cargar-Respuesta` (261, 262 y 266) |
| `FRM-Cargar-Lista` y su teléfono | la lista, una sola opción, sin el dato, elegir varias y la que espera otra respuesta | Aprobados en la v168: «Elegí primero «Zona»» (166). **Se redibujan:** la lista en la pieza (262) |
| `FRM-Cargar-Archivos` y su teléfono | agregar, los límites, la barra de avance, el corte y la cámara | Aprobado |
| `FRM-Cargar-Enviada` y su teléfono | «Enviada», versión nueva, dado de baja, opción dada de baja y sin conexión | Aprobados en la v168: «Volver a Formularios» y el bloque en una línea (166 y 177) |
| `FRM-Respuestas-20` y `FRM-Respuesta-20`, con sus teléfonos | «App», su filtro y el origen en el detalle | Redibujados en la v175: el punto como enlace al mapa, y en el detalle, con la precisión y los momentos del pedido (230). **Se redibujan:** el detalle como el formulario (266), los momentos como renglones de la actividad (267) y «Canal» en la pieza (262) |
| `FRM-Respuestas-Propias` y su teléfono | la misma planilla con lo suyo | Aprobados en la v168: «Filtros» y los chips (144). **Se redibujan:** como `FRM-Respuestas` de la E15 (262 y 264) |
| `FRM-Editor-Listas` y su teléfono | «Según otra respuesta» | Se redibujan: «Dónde se completa» en «Solo en la app», «Publicar» y sin «Ver vista oficial» (229), y la pregunta de la que depende, en la pieza (262 y 269) |
| `FRM-Publicar-20` y su teléfono | publicar «Solo en la app», al instante, y lo que WhatsApp no puede mostrar en «Problemas» | Redibujados en la v175: «Problemas» con «Pasar a «Solo en la app»», sin esa salida en uno solo para clientes, la fila «Dónde se completa», el aviso sin número y el freno de uno solo para clientes (98 y 229) |
| `WA-Chats-Formulario-20` y `M-WA-Chats-Formulario-20` | en «Enviar formulario», los formularios de la app apagados, y el pedido de ubicación antes del Flow | Redibujados en la v175: «Se completa solo en la app.» (229) y el pedido, con sus estados, «Seguir sin ubicación» y la ubicación que llega (230). **Se redibujan:** el formulario, en la pieza (262) |
| `WA-Chats-Respuesta` y `M-WA-Conversacion-Respuesta` | la tarjeta de la E14 | No cambian: la ubicación que llega y el Flow que sale después se dibujan en `WA-Chats-Formulario-20` (78) |

**Sin el dato de la persona,** un cartel lo nombra en la página, porque bloquea, y Enviar queda apagado con su motivo.

**Publicar «Solo en la app»** va como en la E16, con la fila «Dónde se completa» en «Qué se publica», sin «Ver vista oficial» ni «Publicando». Cada fila de «Problemas» dice el problema como la E14 (T26). «Pasar a «Solo en la app»» guarda el borrador con ese ajuste y su versión y vuelve a comprobar en el mismo diálogo, listo para publicar al instante; debajo de una condición, en el editor, solo cambia el ajuste, que se guarda con lo demás (134).

**«Agregar filtro ▾»,** el de la E16, suma «Según otra respuesta» (134).

**«Canal»** se suma a los filtros de la barra, en la pieza y sin cantidades (262).

**Valen** las fichas del front, la 43 en cada botón y las de toda la app (160 a 164, 173 a 175, 180, 261 a 264 y 269).

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | La captura y la publicación. | Se pueden separar lo común y una publicación sin trabajo durable. |
| [E15] | Las lecturas y `RecordForm.tsx`. | Admiten un alcance «solo lo mío», sin copiar las consultas, y la forma de solo lectura, un modo para cargar (266). |
| [E16] | La preparación de la oferta. | Sirve también paginada, sin el tope de 200. |
| [E18] | `IFileContentInspector` y el diario. | Sirven igual para un archivo que llega por multipart. |
| [E19] | `FormConditionEvaluator`, `lib/conditions.ts`, la fórmula (`FormFormulaEvaluator`, `lib/formula.ts`) y el bloque en la captura. | El front y el servidor dan lo mismo, también el cálculo con el redondeo al par, y el bloque viaja en el mismo envío. |
| Allá | La etapa 2 de renglones, probada con Meta el 9/11 (T12); `draft.appOnly`, `FormQuestionType.Location`, `WhatsAppFlowLocationService` y `calculation` en la carga, en `origin/main`. | Se contrasta con el código de allá al planificar, y lo que allá cambió se adopta si no choca con las reglas de acá (126). |
| [Meta] | El pedido de ubicación (`location_request_message`) y la ubicación que llega, con el número de una organización. | Se prueban con la I1 de la E10 (123). |
| Front | `useIdempotentMutation`, `useUnsavedChangesGuard` y el panel al costado. | Están, o llegan con la base de la 12. |
| Celular | Que el reenvío por USB llegue a `localtest.me` (ver «Cómo se cierra»). | Anda sin cambiar la configuración. |

## Decisiones tomadas (no se vuelven a preguntar)

- **Un empleado, a su nombre** (73), sin permisos nuevos ni teléfono verificado (89).
- **La misma captura que WhatsApp.**
- **La carga es la lectura de la E15, editable** (266), con lo que se elige en la pieza (262).
- **Sin borradores guardados:** lo cargado no sobrevive a la página.
- **El canal se llama «App»** (90); el valor técnico sigue siendo `web`.
- **«Dónde se completa» es un ajuste** (229): sin número, todo es de la app (98); con número, lo que WhatsApp no puede mostrar frena al publicar y no pasa solo a la app.
- **Las listas dependientes** van acá y solo en la web (94).
- **El bloque se carga como allá** (177), sin contador y con «Ítem 2» en la banda de cada recuadro (183).
- **Un formulario de la app aparece apagado en el chat,** con «Se completa solo en la app.» (184 y 229).
- **«Ubicación» entra acá** (230), en la app y por WhatsApp, y **el cálculo se ve en gris al cargar** (234).
- **Lo propio tiene sus rutas:** `[HasPermission]` pide un solo permiso, y el mismo servicio les da el alcance.

## Acciones del asistente

**Ninguna** (268 y [ADR 0068](../../decisions/0068-acciones-del-asistente.md)). Una respuesta no entra en los tipos cerrados de una acción: sus preguntas salen de cada versión, y no de una declaración fija, y trae fotos, documentos, la ubicación y el bloque.

## Fuera de esta etapa

- Cargar a nombre de otra persona desde la web (73), importar muchas respuestas desde un Excel, borradores guardados y carga sin conexión.
- La carga en filas, y ofrecer en la web qué cargar después.
- Las listas dependientes en WhatsApp, con el intercambio cifrado con Meta.
- Elegir primero por un campo y una lista dependiente dentro del bloque, que allá son LD1-A a LD3-A: quedan para después de la E25, con las listas dependientes en WhatsApp (191).
- El estado, el PDF y el motivo de rechazo en lo propio, y corregir en el lugar y anular, también con ítems, que suma la E21.
- La ubicación en el PDF, en los avisos y en las condiciones de una regla (E21), en el asistente (E23) y en los documentos (E25).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más un **recorrido real**:
1. cargar una respuesta con una condición, dos ítems, un cálculo y una foto, y ver que queda igual que una por WhatsApp, con el canal «App»;
2. repetir el envío después de un corte: devuelve el primer número, sin saltos;
3. publicar otra versión con la página abierta: pasa a la nueva sin perder lo cargado, ni los ítems;
4. entrar con una cuenta que solo tiene `formrecords.create`: ve sus formularios, carga y ve solo lo suyo. Una dirección directa a una respuesta ajena da 404;
5. en una organización sin número conectado, publicar, cargar desde la web y, al conectar un número, volver a publicar y mandarlo por WhatsApp;
6. elegir en una lista dependiente, cambiar la respuesta de la que depende y ver que se limpia; con WhatsApp, ver que frena al publicar, pasar a «Solo en la app», publicar al instante y, en el chat, verlo apagado con «Se completa solo en la app.»;
7. en un teléfono real, sacar una foto con la cámara, cargar dos ítems en sus recuadros y usar la ubicación;
8. en «Visita a cliente», cargar la ubicación y ver el punto en la planilla, el detalle y el Excel, y en un pedido, ver el cálculo en gris cambiar al escribir y quedar igual al del servidor.

**Desde el celular:** un Android por USB, como allá; los pasos están en la [parte](20-parte-contrato-datos-y-pasos.md#desde-el-celular).

## Requisitos previos

La E19 cerrada.

## En el multitenant

- **Datos de la organización:** las tablas de la E14, con RLS. Cada ruta nueva lleva su test de aislamiento, y lo propio, el suyo: otra persona de la misma organización recibe 404.
- **Idioma:** el contenido no tiene idioma (35), así que `PresentationCulture` de allá no se toma.
- **Datos de las personas:** los de esa persona en esa organización (42). La ubicación sigue la 23 y la sacan el participante de la baja y la retención de WhatsApp.
- **Feature `forms`:** las rutas la llevan.
- **Permisos:** `formrecords.create`, sin permisos nuevos. `formrecords.create` y `forms.publish` completan su texto.
- **Almacenamiento:** los archivos van en `IFileStore`, como los de la E18.
- **Cuotas:** las rutas cuentan en las cuotas generales.
