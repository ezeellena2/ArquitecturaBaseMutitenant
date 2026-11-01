# E19 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E19, condiciones, cálculos y líneas, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** La E19 suma tres cosas al editor: una pregunta que aparece según otra, una pregunta «Cálculo» que no se ve en WhatsApp y un grupo de renglones. Lo que suman se ve en la planilla, el detalle, el Excel, el tablero y la tarjeta del chat. **Los tableros muestran solo lo que existe hasta acá** (78): nada de «Cargar», aprobación, PDF ni asistente. Valen las fichas del front: un dato por celda, en un renglón; sin rótulos que expliquen, y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/19-condiciones-calculos-y-lineas.md`, sobre todo «Qué se construye» y «Pantallas». La referencia es la maqueta, en `components/logic/`.

**Qué se dibuja:**
1. **`FRM-Editor-Condiciones` y `M-FRM-Editor-Condiciones`:** sobre `FRM-Editor`, «Mostrar si» en una pregunta: un renglón por condición, con la pregunta, la operación y el valor, y ✕; «Agregar condición», hasta 10. Solo se eligen preguntas anteriores. El teléfono al costado muestra cómo aparece. Estados: una condición con cada tipo de pregunta, una referencia rota, marcada en su pregunta, y sin preguntas que sirvan.
2. **`FRM-Editor-Calculo` y `M-FRM-Editor-Calculo`:** la pregunta «Cálculo». La fórmula se arma con piezas: un número, una pregunta, un campo del catálogo y la suma de una columna de líneas, con + − × ÷ y paréntesis. No hay un campo para escribirla. Se elige la moneda, si es un monto, y los decimales. No aparece en el teléfono. Estados: fórmula incompleta, con el motivo debajo, y un dato que ya no está.
3. **`FRM-Editor-Lineas` y `M-FRM-Editor-Lineas`:** el grupo de líneas: hasta 6 columnas, cada una con su nombre, su tipo y si es obligatoria, y el tope de renglones, de 1 a 10. El teléfono muestra los renglones uno debajo del otro. Cerca de 50 componentes, se ve el contador.
4. **`FRM-Respuestas-19` y `M-FRM-Respuestas-19`:** sobre `FRM-Respuestas`, una pregunta con «No se mostró», distinto de «Sin respuesta» y de «—»; la columna del cálculo, con su moneda, y la del grupo, con «3 renglones». En «Más filtros», «No se mostró» como opción.
5. **`FRM-Respuesta-19` y `M-FRM-Respuesta-19`:** sobre `FRM-Respuesta`, la tabla de líneas, el cálculo y «No se mostró», con un estado para la pantalla completa.
6. **`WA-Chats-Respuesta-19` y `M-WA-Conversacion-Respuesta-19`:** la tarjeta, con los renglones y el total.
7. **`FRM-Tablero-19` y `M-FRM-Tablero-19`:** sobre `FRM-Tablero`, un monto: «Total de Monto» con su moneda, y su gráfico por período.
8. **`FRM-Publicar-19`:** sobre `FRM-Publicar`, una condición que WhatsApp no puede mostrar y más de 50 componentes, con «Ir a la pregunta».

**Recorrido guiado.** Un pedido: «¿Se concretó la venta?», «Monto» que aparece si es Sí, un grupo con producto y cantidad, y el total. Publicar, responder y verlo en la planilla, el detalle y el tablero.

**Datos de ejemplo:** Grupo Delta. «Pedido de materiales», con el catálogo «Productos», con Precio, y el total en pesos.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
