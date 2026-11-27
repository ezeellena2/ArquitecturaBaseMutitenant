---
name: plan-etapa
description: Arma el plan ejecutable de una etapa (contrato; back y front en paralelo; lo que la prueba integral del cierre del bloque recorre de ella) con el texto listo para cada chat que programa, y resuelve con el usuario lo que queda abierto en tandas de preguntas, cada una con opciones y una recomendada. Usala cuando pidan planificar una etapa o una funcionalidad nueva.
---

# /plan-etapa <etapa o tema>

El plan tiene que poder ejecutarse sin adivinar. Las decisiones de producto las toma el usuario. Las técnicas las toma quien planifica, y quedan anotadas en el plan.

## Qué hacer

1. **Corré el control:** `node scripts/arneses/arnes.mjs alinear-etapas --detectar`. La etapa no puede tener hallazgos propios. Si a su spec le falta algo, se completa con `docs/plans/formularios/plantilla.md` antes de planificar.
2. **Leé las fuentes:**
   - la spec de la etapa en `docs/plans/formularios/`, empezando por su sección «Qué leer»;
   - las decisiones de la §5 de la hoja de ruta de formularios que aplican;
   - las fichas de `docs/rules/` que aplican, con la tabla de `AGENTS.md`;
   - la sección de la etapa en `docs/plans/2026-09-27-plan-de-desarrollo.md`;
   - las ADR de `docs/decisions/`, con su índice;
   - el lienzo vigente (`docs/design/lienzo/` del front) y sus recorridos guiados (`docs/design/recorridos/`);
   - la guía `docs/guides/etapa-en-paralelo.md`;
   - como modelo del largo (unas 4.000 palabras) y del texto para cada chat, el plan de la 11, que ya se retiró (`git show ed39e63e:docs/plans/2026-10-13-etapa-11-whatsapp.md`), sin su «Integración y puerta», que ya no va (paso 5).
3. **Si falta un tablero, pedilo antes de escribir el plan.** Lo dibuja «Multitenant · Diseño», la única que toca el lienzo. Si no le podés escribir, dale el texto al usuario.
4. **Contrastá la spec con el código real.** Revisá cada fila de «Qué verificar al arrancar». Si la spec ya trae contrato, datos y pasos, el plan no los reescribe: los toma como están y anota solo lo que cambió.
   **Los planes del bloque de formularios se escriben todos antes de programar** (pedido del usuario, 2026-11-08). Lo que deja una etapa anterior todavía no existe: sale de su spec y de su plan, y va a la tarea **K0 · Lo que dejó la etapa anterior**, una lista cerrada que el chat del contrato confirma al arrancar. Lo que difiera se anota en «Cambios al contrato» antes de programar.
5. **Escribí el plan en `docs/plans/<fecha>-etapa-<n>-<tema>.md`, corto (unas 4.000 palabras como mucho, sin historial; `AGENTS.md`, «Velocidad y cierre liviano») y con estas secciones:**
   - **Forma de trabajo:** modo rápido, con las cinco fases del conductor ([conducir una etapa](../../../docs/guides/conducir-una-etapa.md)): programar (el contrato y después back y front a la vez, cada tarea con su test dirigido y el build); la revisión en paralelo, con los tests enteros y los detectores; los arreglos; la última revisión de código, y el cierre. No hay puerta por etapa ni tareas de integración (I1, I2, I3).
   - **Contrato:** rutas, cuerpos, códigos de error y permisos. Va primero y completo.
   - **Back** y **Front:** tareas con sus archivos, el archivo real del que se copia y sus tests. Cada parte lleva el texto listo para pegar en su chat. Marcá las tareas que llevan revisión adversarial: las que tocan datos, permisos, efectos fuera del sistema o el aislamiento entre organizaciones (`AGENTS.md`, «Forma de trabajo»). Una tarea la siguen hasta seis agentes de unos 80 pasos cada uno (ADR 0071); la que no entra en seis se parte en dos.
   - **Acciones del asistente:** la tarea que declara las acciones de las pantallas de la etapa que crean, editan o eliminan, con la [receta](../../../docs/guides/agregar-una-accion-al-asistente.md) (268). Si la spec dice que no hay, el plan lo repite en una línea.
   - **Revisión y cierre:** sin tareas, porque los hace el conductor. Dice solo lo propio de la etapa: lo que la revisión suma a su lista de control (los riesgos de la [prueba integral de ArquitecturaBase](../../../docs/backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase) que la tocan), lo que el cierre suma a la documentación (`docs/features/` y los `AGENTS.md` nuevos) y lo que queda para la I1 de la 10.
   - **Al cierre del bloque E12–E25:** lo que la prueba integral tiene que recorrer de esta etapa, porque el E2E real, la galería y el recorrido ya no corren en cada etapa (pedido del usuario, 2026-11-27): los casos del E2E real con su script, los pasos del recorrido con su script y los tableros de la galería con su JSON, a 1440, 390 y 320; y lo que pida un teléfono, una corrida con un proveedor real o quitar el módulo con la base vacía. Cuando el cierre retira el plan, esta sección pasa a `docs/backlog.md`, a «Para después de la etapa <clave>», como las de Acciones y Costuras. La última etapa del bloque suma la revisión de arquitectura de Formularios y la prueba integral con el usuario.
   - **Relación con otras etapas:** «Usa de las anteriores» y «Deja para las siguientes», con nombres concretos, para cruzar los planes.
   - **Cambios al contrato:** vacía; la usan los chats si algo cambia.
   - **Para decidir:** cada punto con opciones y una recomendada.

   El plan lo escribe Claude, porque no hay Codex desde el 2026-11-08: la Guía, o los chats que ella abre por grupo de etapas. La Guía revisa los planes juntos antes de commitearlos.
6. **Revisá el plan antes de dárselo al usuario.** Tiene que aplicar las ADR nuevas, el contrato tiene que estar completo y nada puede quedar condicional.
   - **La etapa usa solo lo que dejaron las anteriores** (pedido del usuario, 2026-10-25). Lo que usa, prueba o dibuja tiene que estar hecho antes. Lo que necesita una etapa posterior pasa a esa etapa.
   - **Lo que suma la etapa aparece en todas las pantallas que ya existen.** Por ejemplo, un tipo de pregunta nuevo se ve en la planilla, el detalle y el PDF, si ya están.
7. **Resolvé «Para decidir» con el usuario:**
   - en tandas de cuatro preguntas como mucho, con opciones y la recomendada primero;
   - al usuario se le preguntan solo las decisiones de producto: lo que ve o puede hacer una persona. Las técnicas las decide quien planifica y las anota en una línea del plan. Si una de producto tiene una parte técnica, primero explicá qué es, para qué sirve y un ejemplo real del repo;
   - anotá cada respuesta en el plan, y también en una ADR si cambia una regla;
   - commiteá solo esos archivos.
8. **Entregá el orden de los chats y sus textos:** primero el Contrato; después Back y Front a la vez. La revisión, los arreglos y el cierre no llevan chat propio: los corre el conductor. Los chats son sesiones de Claude Code: la Guía las abre con esos textos, con tres o cuatro programando a la vez, y archiva las que terminaron solo con el visto bueno del usuario. Antes de abrirlas mira el cupo de la semana (`AGENTS.md`, «Sesiones y cupo»).

## Nunca

- **Programar** desde el chat que planifica.
- **Decidir por el usuario** algo que cambia lo que ve o puede hacer una persona.
- **Dejar condicionales** del estilo «si el usuario quiere…»: el plan sale cerrado.
- **Planificar algo que dependa de una etapa posterior,** ni un tablero que muestre algo que todavía no existe.
