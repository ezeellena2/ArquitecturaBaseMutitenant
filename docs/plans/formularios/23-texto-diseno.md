# E23 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E23, el asistente con Casos, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** El asistente atiende empleados y clientes por WhatsApp, con lo que configura cada organización. **Los tableros muestran solo lo que existe hasta acá** (78): nada de varios asistentes ni de leer documentos. Valen las fichas del front: un dato por celda, sin rótulos que expliquen, y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/23-asistente.md`. La maqueta no tiene el asistente: los dibujos son tuyos, con nuestras reglas y piezas (108), y las pantallas nuevas sin modelo van con opciones para el usuario.

**Qué se dibuja:**
1. **`WA-Asistente` y su teléfono:** «Asistente» en el panel de WhatsApp, con Perfil, Conocimiento, Casos y Límites, «Probar» al costado y un solo Guardar. Se entra por Casos. Sin perfil, abre en Perfil.
2. **`WA-Asistente-Casos`:** los casos propios, el bloque fijo de formularios y «Cualquier otra cosa», con el editor al costado.
3. **`WA-Asistente-Probar`:** las acciones en orden, hasta 3, cada una con su caso, su porqué y cómo la vería la persona, y «No se mandó nada».
4. **`WA-Asistente-Limites`:** los límites por bandeja (65), el tope de ofrecimientos (66) y el conocimiento, sección por sección (46). Se edita ahí, sin salir.
5. **`WA-Chats-Casos` y su teléfono:** el porqué debajo de cada acción, derivar y «Necesita una persona», el texto exacto, los botones, el tope del día y el borrador en atención manual.
6. **`WA-Modo-Atencion` y su teléfono:** dentro de la pantalla «Asistente», por bandeja, quién atiende los chats nuevos y cuándo vuelve un chat. En el chat: cambiar el modo, fijarlo y «Resuelto».
7. **`WA-Preguntas-Sin-Respuesta` y su teléfono:** en el panel de WhatsApp, por tema, con la cantidad de preguntas y de personas, «Completar el conocimiento» y «Descartar». Sin nombres ni mensajes.
8. **`FRM-Catalogos-23`:** el estado «Por aprobar» en la lista de catálogos.
9. **`FRM-Editor-Reglas-23` y su teléfono:** «Armar desde un texto», grisado con el tope agotado, con el motivo en su tooltip.
10. **`FRM-Dar-De-Baja-23` y `FRM-Editor-Baja-23`:** «Qué deja de pasar» suma el asistente y las sugerencias.
11. **`Campana-23` y su teléfono:** las preguntas sin respuesta.
12. **`Plat-Asistente-Config-23` y su teléfono:** los máximos de la plataforma para cada límite: largo de la respuesta, respuestas por conversación y día y ofrecimientos por conversación y día.

**Qué se redibuja:** `FRM-Editor-Asistente`, `FRM-Editor-Sugerencias` y `FRM-Catalogo-Propuesta`, sin «Textos en» ni «Cambios sin guardar», como pestañas o secciones del formulario y del catálogo (79). En `FRM-Editor-Sugerencias`, un estado más: una sugerencia hacia un dado de baja. `FRM-Catalogo-Propuesta` dice «Aprobar» y «Descartar», sin «Aprobar y activar».

**Qué se revisa:** `WA-Chats-Asistente`, `WA-Chats-Sugerencia`, `WA-Chats-Audio`, `FRM-Faltan`, `FRM-Respuesta-Cadena` y sus teléfonos. `WA-Chats-Casos` reemplaza «Sin respuesta del asistente» de `WA-Chats-Asistente`. Los tres chats llevan la barra de la bandeja y las fechas como `WA-Chats`, con el buscador y los filtros en una fila y las fechas completas, también en el separador de días.

**Recorrido guiado.** Los ejemplos en el celular y en el chat del operador, con lo que hacen la persona, el sistema y la IA, y una ráfaga con tres acciones.

**Datos de ejemplo:** Grupo Delta, con un negocio de heladeras: «hola», «¿qué hora es?», «¿hacen envíos a Córdoba?», «se rompió la heladera en la sucursal Centro» y «quiero hablar con alguien».

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
