# E21 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tres tableros nuevos de la E21, reglas, aprobación, PDF y avisos, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Tres cosas de la E21 no tienen tablero: el chat de WhatsApp en «Mi cuenta», con hasta cuándo le llegan los avisos a la persona (159 y 170); las reglas de un formulario con un bloque, que preguntan por «algún ítem» (177), y la tarjeta del chat de una respuesta que después se corrigió o se anuló (166). Valen las fichas del front y las del traspaso 5 que tocan acá: las reglas guardadas se leen en pasos (138) y el mensaje se arma con «Agregar dato» y «Así llega» (149). Y las de toda la app del traspaso 10 (160 a 164, 173 a 175 y 180): lo que carga dibuja su encabezado y el aro, sin filas esqueleto; una acción que no se deshace pide confirmar, con el foco en «Cancelar»; cambiar algo no pierde lo escrito sin avisar; todo el texto a la vista, sin cortar con «…»; sin el cuadrado del logo, pastillas ni sombras fuera de los menús; la escala de ArquitecturaBase, con la letra y las filas más chicas y las esquinas de 3 px como mucho, y el estado dicho una vez, arriba.

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/21-reglas-y-aprobacion.md`, «Reglas» y «El chat de WhatsApp en «Mi cuenta»», y su parte `21-parte-corregir-y-anular.md`, el último punto. Para mirar cómo quedó allá, en `ArquitecturaBaseFront`:
- `docs/design/visual-baseline.md`, «Mi perfil y el chat de WhatsApp», con las capturas `profile-chat-es-1440`, `-390` y `-320` de `src/test/visual/screenshots/`;
- `docs/design/renglones-en-formularios.md`, RG5-A, y `formularios-whatsapp.md`, «Reglas: el mensaje con datos, MD1-A» y «Reglas y sugerencias en pasos, LR1-A»;
- `docs/design/carga-desde-la-app.md`, «Corregir y anular», el último punto.

Se copia el armado, con nuestras piezas (126).

**Qué se dibuja:**
1. **`Cuenta-21` y su teléfono** (nuevos): sobre `Cuenta`, «Mi cuenta» suma, debajo de «Cómo entrás», la zona «Chat de WhatsApp», con su (i): «Con el chat vinculado, te llegan por WhatsApp los avisos y los formularios de esa organización.». Va un renglón por cada organización de la persona, con las columnas fijas de «Cómo entrás»: el nombre, el vínculo y los avisos, cada uno con su acción.
   - **El vínculo:** «Vinculado»; «Sin vincular», con «Vincular», que confirma el número con un código, como hoy, o «Sin chat todavía», con «Abrir el chat».
   - **Los avisos:** «Llegan hasta el 08/11/2026 10:28». Pasadas las 24 horas, «Solo con plantilla», con «Abrir el chat» y su (i): «WhatsApp deja mandarte cualquier aviso hasta 24 horas después de tu último mensaje; después, solo los que la organización manda con una plantilla aprobada. Si escribís al chat, vuelven todos.». Sin vincular, «No llegan».
   - «Verificado», en «Cómo entrás», sigue siendo entrar con el número: el vínculo de cada chat va aparte.
   - Estados: Lucía con cuatro organizaciones, una en cada caso: «Grupo Delta», vinculado, hasta el …; «Agro Sur», vinculado y «Solo con plantilla»; «Transportes Norte», sin vincular, y «Lácteos del Valle», sin chat todavía. Además: vinculando, con el código; al volver a la pestaña se relee, y con el módulo de WhatsApp apagado la zona no está.
   - En el teléfono, cada organización va en su bloque, con las acciones a lo ancho, también a 320.
2. **`FRM-Editor-Reglas-Bloque` y `M-FRM-Reglas-Bloque`** (nuevos): sobre `FRM-Editor-Reglas`, como se redibuja (en pasos, con el historial detrás de su botón y «Agregar dato», 138, 140 y 149), las reglas de «Pedido de materiales», con el bloque «Productos» (Producto, Cantidad y ¿Urgente?):
   - **la condición:** en «Pregunta», las del bloque van debajo de su nombre, con sangría, y se leen «Productos › ¿Urgente?». Al elegir una, la condición empieza con «Algún ítem con». Dos del mismo bloque buscan el mismo ítem: «Algún ítem con Producto es Caño de 1/2 pulgada y Cantidad desde 100»;
   - **la regla guardada,** en pasos: «Si algún ítem de Productos con ¿Urgente? es Sí», con la pregunta y el valor en negrita, y la acción «Avisar a un rol: Compras»;
   - **«Agregar dato»:** «Productos» es un dato más de la lista, que trae todos sus ítems, y sus preguntas no aparecen sueltas;
   - **«Así llega»:** el mensaje con el bloque, un ítem por línea, numerado, con sus valores separados por « · »: «1. Caño de 1/2 pulgada · 10 · Sí» y «2. Codo de 90° · 25 · No».

   Estados: la condición abierta, con «Algún ítem con»; la regla guardada; «Agregar dato» abierto, y «Así llega». En el teléfono, lo mismo en una columna.
3. **`WA-Chats-Respuesta-21` y `M-WA-Conversacion-Respuesta-21`** (nuevos): sobre `WA-Chats-Respuesta`, la tarjeta de una respuesta que llegó por el chat conserva lo que llegó y suma al pie, en gris y con el ícono de información, una línea: «Después se corrigió: VIS-000126 quedó reemplazada por su corrección.» o «Después se anuló VIS-000127.». Dibujá las dos y, para comparar, una que no cambió. El número de la tarjeta abre la respuesta: la corregida, en su versión anterior, con «Ver la actual», y la anulada, con su estado. La copia de «Mis mensajes» de quien la mandó no cambia.

**Recorrido guiado.** Suma pasos a los dos de la etapa: a `armar-las-reglas`, la regla con «Algún ítem de Productos…», y a `aprobar-o-rechazar`, Ana corrige VIS-000126 y la tarjeta del chat lo dice. Y Lucía revisa en «Mi cuenta» hasta cuándo le llegan los avisos.

**Datos de ejemplo:** Grupo Delta. «Visita a cliente», con VIS-000126 y VIS-000127, que llegaron por el chat de Tomás Acosta, y «Pedido de materiales», con el bloque «Productos». Siempre datos ficticios.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
