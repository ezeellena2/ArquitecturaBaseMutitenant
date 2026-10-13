# E17 · Texto para el chat de Diseño

Se pega tal cual en el chat de Diseño, **después de cerrar los tableros de la E12 y la E13**: el editor de la plataforma reusa sus piezas.

---

Seguimos con «Formularios por WhatsApp»: los tableros de la E17, la biblioteca de plantillas. Como siempre, solo tableros.

**Contexto.** La plataforma arma plantillas de formularios. Cada plantilla trae:
- las preguntas en es/en, el botón y el prefijo sugerido;
- sus catálogos a mano;
- más adelante, reglas de ejemplo.

Las plantillas se ordenan en categorías, se publican y se retiran. Una organización ve las plantillas publicadas y elige «Usar esta plantilla»: obtiene una **copia propia**, un formulario en borrador con sus catálogos, que revisa y publica ella. La plataforma solo ve cuántas organizaciones usaron cada plantilla, nunca cuáles ni qué hicieron con la copia.

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`:
1. **La spec:** `docs/plans/formularios/e17-biblioteca.md`. Sobre todo:
   - la §8, con los tableros, sus estados y las elecciones;
   - la §1, con el alcance;
   - la §2, con las decisiones ya tomadas;
   - la §6, con los flujos de publicar, retirar y usar.
2. **Los textos para Diseño de las etapas anteriores:** `docs/plans/formularios/e12-texto-diseno.md` y `e13-texto-diseno.md`. Ahí están los criterios y las piezas del editor y de los catálogos que se reusan.
3. **En ArquitecturaBase no hay nada de esto:** la biblioteca es propia de este proyecto. Para cómo se ven las pantallas de Plataforma, mirá las nuestras: «Legales», que tiene versiones y publicar, y las que piden motivo.

**Cómo trabajamos:** igual que en la E12 y la E13.
- Una pantalla por vez, con 2 o 3 opciones y una recomendada.
- Primero la compu y después el teléfono.
- Las piezas de nuestro lienzo y el teléfono como app.
- Voseo, y cada decisión en «Criterio · Decisiones por pantalla».
- **Dónde van los tableros:**
  - los de Plataforma, en una página «Plataforma · Formularios», con el prefijo `Plat-FRM-`, como `Plat-WA-`;
  - los de la organización, en «Organización · Formularios», con `FRM-`.

**Datos de ejemplo:**
- **Categorías:** Ventas, Logística y Mantenimiento.
- **Plantillas:**
  - «Visita a cliente» (Ventas): publicada en la versión 2, con cambios sin publicar, que la usaron 12 organizaciones;
  - «Control de vehículo» (Logística): borrador, nunca publicada;
  - «Orden de trabajo» (Mantenimiento): retirada.
- **Versiones de «Visita a cliente»:**
  - v1, publicada por un operador con el motivo «Primera versión»;
  - v2, con el motivo «Agrega el monto estimado».
- **Del lado de la organización:** Grupo Delta, con Lucía Fernández usando «Visita a cliente». El prefijo sugerido «VIS» ya está tomado, así que se propone otro.

**Tableros de Plataforma, en este orden:**
1. **`Plat-FRM-Biblioteca`:**
   - nombre, categoría, estado («Borrador», «Publicada · v2», «Retirada»), «Cambios sin publicar» y «La usaron 12»;
   - filtro por estado y por categoría, y «Nueva plantilla»;
   - una pestaña «Categorías»;
   - estados vacío y sin resultados.
2. **`Plat-FRM-Plantilla-Nueva`:** un diálogo con el nombre y la categoría.
3. **`Plat-FRM-Plantilla`,** una ficha con estas pestañas:
   - **Preguntas** e **Inglés:** las piezas del editor de la E12;
   - **Catálogos:** los catálogos a mano de la plantilla, con sus opciones (las piezas de la E13);
   - **Publicación:** el estado, la lista de versiones (número, fecha, operador y motivo), «Publicar», «Retirar» y «Volver a publicar».

   Estados: cambios sin publicar y conflicto.
4. **`Plat-FRM-Publicar`:** un diálogo con el motivo y la comprobación: problemas por pregunta o por catálogo, o sin problemas.
5. **`Plat-FRM-Retirar`:** un diálogo con el motivo. Las copias ya hechas no cambian.
6. **`Plat-FRM-Categorias`:** la lista con su orden y ⋮ con Editar, Subir, Bajar y Borrar. El alta y la edición piden motivo. Borrar una categoría en uso muestra un aviso.
7. **Ajuste en `Cuenta-Plat`:** los permisos del operador suman «Administrar la biblioteca de formularios».

**Tableros de la organización:**

8. **`FRM-Biblioteca`:** tarjetas por categoría, con un buscador. Cada tarjeta muestra el nombre, la descripción y cuántas preguntas y catálogos trae. Estado: sin plantillas.
9. **`FRM-Plantilla`:** la vista de una plantilla, con la descripción, las preguntas, los catálogos que trae, la vista del teléfono y «Usar esta plantilla». Sin permiso para administrar catálogos, el botón aparece apagado con su motivo en ⓘ.
10. **`FRM-Plantilla-Usar`:** un diálogo con el nombre y el prefijo propuesto (si el sugerido está tomado, se propone otro). Al confirmar, abre la ficha del formulario nuevo, en borrador.

Después vienen los teléfonos: `M-Plat-FRM-Biblioteca`, `M-FRM-Biblioteca` y `M-FRM-Plantilla`. En el teléfono de Plataforma, la ficha de una plantilla es solo para ver, salvo que el usuario elija otra cosa.

**Elecciones que hay que mostrar con opciones:**
- **Dónde ve la organización la biblioteca:**
  - recomiendo «Nuevo formulario» con dos caminos, «Desde cero» y «Desde una plantilla»;
  - la alternativa es una pestaña «Biblioteca» en Formularios.
- **Dónde va «Formularios» en el menú de Plataforma:** como ítem propio, o dentro de una sección de módulos.

**Textos que ya fija la spec:**
- «Publicada · vN», «Borrador», «Retirada», «Cambios sin publicar» y «La usaron N»;
- «Publicar», «Retirar», «Volver a publicar», «Usar esta plantilla», «Desde cero» y «Desde una plantilla»;
- el permiso «Administrar la biblioteca de formularios».

**No se dibuja ahora:**
- el aviso de «hay una versión nueva de la plantilla» y actualizar una copia;
- las reglas de ejemplo, que se suman cuando exista la spec de la E16;
- catálogos con filtros en una plantilla;
- borrar plantillas.

**Al terminar,** avisá la versión del lienzo y la lista de tableros aprobados.
