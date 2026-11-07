# E20 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso un tablero nuevo de la E20, cargar una respuesta desde la web, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Un formulario que no se puede mandar por WhatsApp se publica solo para la app, y los empleados lo cargan desde la web (98). Hasta ahora el chat no lo mostraba; desde el traspaso 10, «Enviar formulario» lo muestra apagado, con su motivo, así quien atiende sabe por qué no lo puede mandar (166 y 184). Valen las fichas del front y las de toda la app del traspaso 10 (160 a 164, 173 a 175 y 180): lo que carga dibuja su encabezado y el aro, sin filas esqueleto; una acción que no se deshace pide confirmar, con el foco en «Cancelar»; cambiar algo no pierde lo escrito sin avisar; todo el texto a la vista, sin cortar con «…»; sin el cuadrado del logo, pastillas ni sombras fuera de los menús; la escala de ArquitecturaBase, con la letra y las filas más chicas y las esquinas de 3 px como mucho, y el estado dicho una vez, arriba.

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/20-cargar-desde-la-web.md`, «Formularios solo para la app». El diálogo de siempre es `WA-Chats-Formulario`, que se redibuja con el mensaje que nombra el formulario (165). Para mirar cómo quedó allá, en `ArquitecturaBaseFront`: `docs/design/carga-desde-la-app.md`, «Listas dependientes, CW7», el último punto. Se copia el armado, con nuestras piezas (126).

**Qué se dibuja:**
1. **`WA-Chats-Formulario-20` y `M-WA-Chats-Formulario-20`** (nuevos): sobre `WA-Chats-Formulario`, el diálogo «Enviar un formulario a Tomás Acosta», en Internos. En la lista, cada formulario que se carga solo desde la app queda en su lugar, apagado, y en vez de «Versión 4 · 9 preguntas» dice su motivo, entero y bajando de renglón (174):
   - «Pedido de materiales», con una lista dependiente: ««Proveedor» depende de otra respuesta: se carga desde la app.»;
   - «Pedido de repuesto», con un catálogo a mano de 240 opciones: ««Repuesto» tiene 240 opciones, y WhatsApp admite hasta 200 al elegir una. Se carga desde la app.»;
   - «Inventario de equipos», con un bloque que pide al menos 15 ítems: ««Equipos» pide al menos 15 ítems, y WhatsApp carga hasta 10. Se carga desde la app.».

   Uno apagado no se elige, tampoco con el teclado. Los demás se ven como siempre, y al elegir «Visita a cliente» el mensaje propone «Completá el formulario «Visita a cliente».» (165). Los dos últimos motivos son los mismos que da publicar, y los tres dicen «desde la app», como el canal (90), no «desde la web». Estados: la primera página, con uno apagado entre los que se pueden mandar; los tres motivos; una búsqueda que encuentra solo uno apagado, y uno elegido con otro apagado debajo. En el teléfono, el diálogo ocupa la pantalla, también a 320.

**Recorrido guiado.** Suma un paso a `cargar-una-respuesta`: después de publicar «Pedido de materiales» solo para la app, Lucía abre el chat de Tomás, toca «Enviar formulario» y lo ve apagado, con su motivo.

**Datos de ejemplo:** Grupo Delta. Lucía Fernández atiende a Tomás Acosta en Internos. «Pedido de materiales», versión 4, con «Zona», «Proveedor» con su lista dependiente, «Foto del remito» y el bloque «Productos». Siempre datos ficticios.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
