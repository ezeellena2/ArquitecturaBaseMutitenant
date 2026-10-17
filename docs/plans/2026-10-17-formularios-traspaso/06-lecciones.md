# 06 · Lecciones: errores a no repetir

Cada línea es algo que apareció en un code review o en un piloto de ArquitecturaBase, junto con su corrección. Sirven como lista de control al planificar y al revisar cada etapa de acá. El detalle, con el review y el número de punto, está en el [anexo C §3](anexo-c-reglas.md#3-lecciones-de-los-code-reviews-errores-a-no-repetir).

## Contratos entre backend y front

- [ ] **Campos nuevos:** cada campo nuevo de un DTO se exporta y el front lo consume en el mismo corte. Un esquema estricto rechazó `origin` y mostró error con el envío hecho.
- [ ] **Rutas de error:** tienen la misma forma en los dos lados (`rules[0].actions[0]` frente a `rules.0.actions.0`). Una fixture exportada y sin usar delata un test que falta.
- [ ] **`""` no es `null`:** mandar `null` cuando falta un valor y omitir una propiedad vacía en lo que va a Meta.
- [ ] **El `Accept`:** un helper HTTP no lo pisa, porque los errores pierden ProblemDetails, `retryAfter` y `traceId`.
- [ ] **Entradas inválidas:** son 400, nunca 500 (orden ajeno, `sort` con espacios, intercambio de códigos, timeout de vista previa).
- [ ] **Campos omitidos:** un campo omitido no apaga nada; `isActive` es obligatorio.
- [ ] **OpenAPI:** declara solo lo que pasa de verdad. El 429 lleva `retryAfter`.

## Estados inciertos, colas y reintentos

- [ ] **Errores definitivos:** un error definitivo del proveedor termina en Failed visible, nunca en un Uncertain eterno.
- [ ] **Final de toda cola:** reintento acotado por **cantidad y por tiempo**: 5 fallas y 15 minutos desde la primera. Sin la antigüedad, una caída de Redis de 3 minutos rechazaba respuestas válidas.
- [ ] **Bloqueos:** un elemento trabado no frena a los demás. Plazo total por elemento, saltear lo que falló hace poco y drenar el lote aislando fallas.
- [ ] **Salidas de estado:** todo estado tiene salida. «Sin confirmar» se consulta y, si sigue incierto, una persona resuelve «salió» o «no salió». No ofrecer «Reintentar» sobre lo incierto.
- [ ] **Errores no transitorios:** no se reintentan. Un documento dañado o cifrado se rechaza enseguida, y los tests usan tamaños reales.
- [ ] **Configuración usada por un efecto:** se valida al reservar. Un rol borrado dejaba una aprobación colgada para siempre.
- [ ] **Destinatarios:** un destinatario inalcanzable no tumba a todos. Se manda a los que se puede y se muestra a quién no.
- [ ] **Lo sellado manda:** el tope de tamaño sellado al publicar es el que vale, no el vigente.
- [ ] **Workers sin test:** un worker apagado en los tests esconde bugs. Su bucle necesita su test.

## Avisos y mensajes en pantalla

- [ ] **Un solo dueño por error.** Nada de un toast global más un aviso local, un toast por archivo o el mismo error en dos pantallas.
- [ ] **Errores generales:** van en el toast común con un id por operación; junto al campo o en pantalla queda solo lo vigente (409, espera, «tu propia respuesta»).
- [ ] **Reintentar:** solo ante errores de red.
- [ ] **Pedidos al servidor:** nunca un pedido por tecla con datos crudos. Normalizar (coma decimal, espacios), validar y esperar antes de pedir conteos.
- [ ] **Textos verdaderos:** las consecuencias de una baja, «volvé a guardar» tras un error de red, plurales y KB.

## Cachés y red

- [ ] **Miniaturas privadas:** se retienen mientras dure el ámbito, con purga por dueño y permisos, y se cargan solo las visibles. Ni `staleTime: 0` con refetch ni `gcTime: 0` que las baje en cada visita.
- [ ] **Carga sin perder el contexto:** no reemplazar el editor con un spinner al cargar; conservar los datos anteriores.
- [ ] **Relojes:** ninguno que redibuje toda la pantalla cada segundo.
- [ ] **Dependencias en dirección correcta:** el caparazón no importa la app.
- [ ] **Lo privado:** fuera de la URL, del storage y de las cachés persistentes, con claves por actor, cultura y permisos.

## Rendimiento

- [ ] **Exportar:** sin repetir la lectura por página. Una consulta de IDs ordenados, hidratación por lotes, definición y columnas una vez, y un test con varias páginas y el borde del tope.
- [ ] **JSON de esquema:** se deserializa una vez por versión, no por fila. El esquema y el nombre no viajan por fila.
- [ ] **SQL estable:** con parámetros, nunca literales por combinación.
- [ ] **Exportación:** con límite propio (una a la vez por usuario).
- [ ] **Sin bucles de una consulta por elemento:** usar `= ANY(@keys)` por lote.
- [ ] **Copias:** no copiar la respuesta entera en cada ejecución; fijar solo lo que cambia.
- [ ] **EXPLAIN:** sobre el SQL que genera el ORM, no sobre uno escrito a mano.

## Archivos

- [ ] **Nombre de descarga:** se arma con la extensión del tipo verificado y se quitan los caracteres de formato (U+202E).
- [ ] **Dimensiones de imagen:** se rechazan por las del encabezado **antes** de decodificar.
- [ ] **Datos del proveedor:** no confiar en sus tipos. Un tamaño puede no ser número y un ID de medio puede ser un número JSON.
- [ ] **Blob faltante:** responde 404.
- [ ] **Antivirus** antes de producción.

## Privacidad

- [ ] **Datos de más:** no mandar al navegador datos que la pantalla no usa. La preparación del envío exponía los nombres de 200 personas a quien no podía verlas; ahora va solo la cantidad.
- [ ] **`ToString`:** redactado en todo modelo con valores, nombres o códigos.
- [ ] **Retención:** lo sellado (ofertas, payloads) también entra en ella.
- [ ] **Sin permiso de ver respuestas:** nada de valores en avisos, previews, aria-labels ni caché tras revocar.

## Modelo e integración

- [ ] **Vistas:** nunca sobre tablas de otro dueño (frenan sus migraciones). Usar un SELECT fijo en el lector.
- [ ] **Referencias por Id,** no por nombre (rol renombrado → catálogo vacío).
- [ ] **Opciones vacías:** no viajan, y el texto de una opción sale de un campo que siempre tiene valor.
- [ ] **Límites del canal:** se validan **al guardar**, no al mandar (30/300 en WhatsApp), y la vista previa marca lo que no entra.
- [ ] **Paridad:** la misma regla en memoria y en SQL (p. ej. «sin respuesta» sobre una pregunta inexistente).
- [ ] **Lectores y servicios:** el lector devuelve filas y el servicio decide.
- [ ] **Lo decidido tiene que existir:** un estado que no tiene ruta para producirse es un bug.
- [ ] **Error de red o 5xx:** reintentar con la misma revisión; solo un 409 pide revisar.
- [ ] **Idiomas en lo capturado:** guardar **todas** las variantes de idioma, para que la copia fija no mezcle idiomas.

## Interfaz

- [ ] **Foco:** nunca en `body`, ni al perder un permiso, ni tras Listo, quitar o mover, ni al decidir.
- [ ] **Navegación:** no desmonta la página; conservar el contenido con `aria-busy`.
- [ ] **Paneles:** no desaparecen con la acción que muestran; un 409 se avisa y se ve.
- [ ] **PDF:** sin iframe con sandbox (Chrome y Edge no abren su visor ahí). Usar `<object>` o un iframe sin sandbox, con descarga como alternativa.
- [ ] **403 de una acción:** no oculta la pantalla de lectura.
- [ ] **Cambios sin tablero:** ningún cambio de disposición sin tablero elegido.
- [ ] **Acciones que pueden no hacer nada:** se ocultan o se explican.
- [ ] **Inputs numéricos:** enteros acotados; el vacío no es 0.
- [ ] **Botones sin cambios:** no piden confirmación ni suben la revisión.

## Tests y documentación

- [ ] **Aislamiento:** cada test con sus propios datos (prefijos únicos), sin `Single` sobre toda la base ni fixtures que dependan del orden.
- [ ] **Arneses:** un arnés de navegador no es un test.
- [ ] **De punta a punta:** recorridos completos (captura → tarea → decisión → PDF → aviso, en otro idioma y con caída al original), 403 en cada ruta y gramática negativa.
- [ ] **Documentación:** se actualiza con el cambio.

## Pilotos con Meta

Ningún test con dobles los detectó; solo el piloto:
- Meta rechazó el perfil r2.
- Los IDs de pantalla tienen que ser de solo letras y guiones bajos.
- El ID de medio puede llegar como número.
- Una pregunta de archivo opcional vacía llega omitida.
- `12.50` sí y `12,50` no en la preview.
- 141006 sin medio de pago.
- 131030 con el 9 argentino en el número de prueba.

Se corrigen con test rojo primero y solo para publicaciones futuras, sin recompilar lo sellado.

## Proceso

- [ ] **Robustez de efecto externo en configuración interna, suite completa en cada corte y contratos en Markdown antes del código** hicieron que la Etapa 1 tardara cuatro días. Ver [02](02-forma-de-trabajo.md).
- [ ] **Una suite verde no acredita el diseño:** la revisión cubre lo que los tests no demuestran.
