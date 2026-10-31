# E24 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E24, varios asistentes, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Cada equipo de la bandeja puede tener su asistente, con su perfil, sus casos y sus límites. El conocimiento lo comparten. **Los tableros muestran solo lo que existe hasta acá** (78): nada de leer documentos. Valen las fichas del front: un dato por celda, sin rótulos que expliquen, y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/24-varios-asistentes.md`.

**Qué se dibuja:**
1. **`WA-Asistentes` y su teléfono:** la lista, con el nombre, el equipo de cada uno y el estado, «Nuevo asistente» y el ⋮ (Editar, Dar de baja o Reactivar). El principal no se da de baja. Cada uno abre `WA-Asistente`.
2. **`WA-Equipo-Asistente` y su teléfono:** «Asistente de este equipo», en la configuración del equipo, con un desplegable. Sin elegir, contesta el principal.
3. **`WA-Chats-Casos-24` y su teléfono:** sobre `WA-Chats-Casos`, el nombre del asistente que contesta, también en «Por qué respondió esto», y el menú de equipos en un chat nuevo, aunque la bandeja esté en modo Asistente.

**Recorrido guiado.** Dos equipos con su asistente: una persona elige «Soporte», un operador pasa el chat a Ventas, y un chat sin equipo lo contesta el principal.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
