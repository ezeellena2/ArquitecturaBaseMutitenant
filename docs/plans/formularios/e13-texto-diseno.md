# E13 · Texto para el chat de Diseño

Se pega tal cual en el chat de Diseño, **después de cerrar los tableros de la E12**: usa su página, sus criterios y sus datos de ejemplo.

---

Seguimos con «Formularios por WhatsApp»: los tableros de la E13, los catálogos. Como siempre, solo tableros.

**Contexto.** Un catálogo es una lista de opciones que comparten varios formularios. Hay de dos orígenes:
- **a mano:** opciones con texto, detalle y código;
- **con filtros:** se toma de los miembros de la organización, con condiciones como «tiene el rol Supervisor».

Una pregunta nueva, «Elegir de un catálogo», usa esas listas. Al mandar el formulario por WhatsApp, las opciones se arman para cada persona. Cuando responde, se guarda el texto que vio.

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`:
1. **La spec:** `docs/plans/formularios/e13-catalogos.md`. Sobre todo:
   - la §10, con los tableros, sus estados y las elecciones;
   - la §1, con el alcance;
   - la §4, con la fuente «Miembros»: columnas y operadores;
   - la §5, con los estados y los motivos de «No se puede usar»;
   - la §13, con las decisiones ya tomadas.
2. **Para los criterios y los datos de ejemplo de la etapa anterior:** `docs/plans/formularios/e12-texto-diseno.md`.
3. **ArquitecturaBase, solo como referencia de contenido:** en `ArquitecturaBaseFront`, `git show origin/main:docs/design/formularios-whatsapp.md` (antes, `git fetch`), con sus tableros K1 a K7, y la página «Formularios por WhatsApp» de su lienzo: https://claude.ai/artifact/HPbmDPLnr8JZ9TxevtTqJJ.

**Cómo trabajamos:** igual que en la E12.
- Una pantalla por vez, con 2 o 3 opciones y una recomendada.
- Primero la compu y después el teléfono.
- Las piezas de nuestro lienzo y el teléfono como app.
- Voseo, y cada decisión en «Criterio · Decisiones por pantalla».
- Los tableros van en la misma página, «Organización · Formularios», con el prefijo `FRM-`.

**Datos de ejemplo** (además de los de la E12):
- **«Motivos de visita»,** a mano: Seguimiento, Venta nueva, Reclamo y Cobranza (dada de baja). Lo usa «Visita a cliente».
- **«Supervisores»,** con filtros:
  - condiciones: «Rol tiene alguno de: Supervisor» y «Sin incluir a quien lo recibe»;
  - el detalle muestra la empresa;
  - incluye a «Florencia Fernández de la Fuente», un nombre de más de 30 caracteres.
- **«Clientes mayoristas»,** a mano, que no se puede usar porque no tiene opciones activas.
- **«Visita a cliente»** suma la pregunta «Supervisor a cargo», de tipo «Elegir de un catálogo», con «Supervisores».

**Tableros, en este orden:**
1. **`FRM-Catalogos`:**
   - nombre, origen («A mano» o «Con filtros»), opciones, «Lo usan» y estado;
   - el estado es «Se puede usar» o «No se puede usar», con el motivo en ⓘ;
   - filtro Todos, A mano y Con filtros;
   - «Nuevo catálogo», y los estados vacío y sin resultados.
2. **`FRM-Catalogo-Nuevo`:** un diálogo con el nombre y el origen en el control segmentado «Escribirlas a mano | Tomarlas de los miembros, con filtros», la pieza de Plantilla nueva, Difusión nueva, el bot y el asistente (decisión del usuario del 2026-10-13). El ⓘ dice: «No se puede cambiar después.»
3. **`FRM-Catalogo`** (a mano), una ficha con dos pestañas:
   - **Opciones:**
     - una tabla con posición, texto, detalle y código;
     - ⋮ con Editar, Subir, Bajar y Dar de baja o Reactivar;
     - filtro Activas, Dadas de baja y Todas, y «Agregar opción»;
     - estados: sin opciones, 500 alcanzadas y conflicto («alguien la cambió»).
   - **Datos:** nombre y descripción en es/en, y «Lo usan».
4. **`FRM-Catalogo-Opcion`:** un diálogo con el texto en es/en (hasta 30 caracteres, con contador), el detalle en es/en y el código. Estados: código repetido y texto largo.
5. **`FRM-Catalogo-Filtros`:**
   - **Condiciones:** filas «Si» / «y» con columna, operador y valor. Las columnas y sus operadores son:
     - Nombre: contiene, es igual a;
     - Rol: tiene alguno de, no tiene ninguno de;
     - Empresa: está en alguna de, no está en ninguna de;
     - Activo: es.
   - **Otras opciones:** «Sin incluir a quien lo recibe», el detalle (ninguno, roles o empresas) y el orden.
   - **Al costado, «Como si lo recibiera»:** una persona elegida, «Probar» y el resultado.
   - **Estados del resultado:**
     - N opciones;
     - ninguna;
     - «Más de 200: no se va a poder mandar», que es un aviso y no impide guardar;
     - un rol o una empresa que ya no existe.
   - **El nombre largo:** que se vea cómo queda, «Florencia Fernández de la Fu…», con el nombre completo en el detalle.
6. **`FRM-Pregunta`** (variante): el tipo «Elegir de un catálogo», con un selector de catálogo. Los que no se pueden usar aparecen apagados, con su motivo.
7. **`WA-Chats-Formulario`** (variante): avisos por pregunta («más de 200 opciones», «ninguna opción en una pregunta obligatoria», «el catálogo no se puede usar»), y «Mandar» apagado.
8. **`WA-Chats-Respuesta`** (variante):
   - la tarjeta con un valor de catálogo;
   - la respuesta rechazada porque «una opción se dio de baja», nombrando la pregunta y no la opción, con «Mandar de nuevo».
9. **Ajuste en `Rol`:** el área Formularios suma «Ver catálogos» y «Administrar catálogos».

Después vienen los teléfonos: `M-FRM-Catalogos`, `M-FRM-Catalogo` y `M-FRM-Catalogo-Opcion`, y los filtros según lo que elija el usuario.

**Dónde viven los catálogos** (decisión del usuario del 2026-10-13): en el segundo panel de Formularios, con «Formularios» y «Catálogos», y en el grupo de «Más» del teléfono.

**Elecciones que hay que mostrar con opciones:**
- **El editor de filtros en el teléfono:** completo, o solo para ver.

**Textos que ya fija la spec:**
- «A mano», «Con filtros», «Escribirlas a mano» y «Tomarlas de los miembros, con filtros»;
- «Se puede usar» y «No se puede usar». Los motivos son «No tiene opciones activas», «Un rol de los filtros ya no existe», «Una empresa de los filtros ya no existe» y «Los filtros quedaron viejos»;
- «Sin incluir a quien lo recibe», «Como si lo recibiera», «Probar» y «Más de 200: no se va a poder mandar»;
- «Mandar de nuevo».

**No se dibuja ahora:**
- catálogos con una consulta SQL;
- dar de baja un catálogo entero;
- otras fuentes, como clientes o productos;
- importar desde Excel;
- la búsqueda en vivo.

**Al terminar,** avisá la versión del lienzo y la lista de tableros aprobados.
