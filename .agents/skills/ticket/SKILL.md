---
name: ticket
description: Toma un ticket de Jira del proyecto KAN (un error, una mejora o una decisión) y lo trabaja en su propio chat. Primero lo vuelve a analizar contra el código de hoy, lo muestra fallando en vivo en la app cuando se puede (en un entorno aislado, fuera de main) y explica en lenguaje llano qué pasa, si es real, qué tan probable es y con qué otros tickets se cruza. Después recomienda arreglarlo, dejarlo para más adelante o descartarlo, y con el visto bueno del usuario lo programa con su test y lo deja en In Review. También hace un repaso rápido de varios tickets, para encontrar los que casi nunca pasan o están mal analizados. Usala cuando pidan tomar, analizar, arreglar o repasar un ticket KAN-….
---

# /ticket KAN-<n> · /ticket repaso <filtro>

Sirve igual para Claude y para Antigravity.

- **Jira:** el sitio es `ezequielellena0003.atlassian.net` y el proyecto, `KAN`.
  - Claude entra con el conector de Atlassian.
  - Antigravity entra con `twg`, de Atlassian Teamwork Graph CLI. Si no está, se instala con `/twg-setup`.
- **Cómo se escribe y se carga un ticket:** [estandar.md](estandar.md), en esta carpeta.
- **Reglas del código:** el `AGENTS.md` de los dos repos y las fichas de `docs/rules/`.
- **Si sos Antigravity,** además: `.local/antigravity/desarrollo.md`, que tiene la convivencia en `main` con las sesiones de Claude.

**Un chat por ticket.** El chat empieza con la clave y el título del ticket, por ejemplo «KAN-12 · Un correo corporativo revocado…», así se llama como el ticket. Si una misma tarea de un plan cubre otros tickets, este chat los trabaja juntos y lo avisa al empezar.

## 1. Tomar el ticket

1. **Leé el ticket entero,** con sus comentarios.
2. **Movelo:** si está en «To Do», pasalo a «In Progress» y comentá «En análisis (Claude)» o «En análisis (Antigravity)». Si ya está en «In Progress» con otro, frená y avisale al usuario.
3. **Buscá con qué se cruza,** en tres lugares:
   - **En Jira:** los tickets que nombran los mismos archivos, con JQL `project = KAN AND text ~ "<Archivo>"`, los de su mismo épico y los que su comentario marca como «se cruza con…».
   - **En los planes:** `grep -n "<T0N-MM>\|KAN-<n>" docs/plans/*.md`, con los números de sus etiquetas `antigravity-T0N-MM`. Si una tarea de un plan lo cubre, esa tarea manda: sus archivos, su test, su línea `Tarea:` y lo que el plan espera antes de arrancar. Por ejemplo, `docs/plans/2026-11-27-etapa-arreglos-revision.md` espera el código listo de la E12. Los otros tickets de esa tarea van en este mismo chat.
   - **En `git log`:** si alguien ya tocó esos archivos después de la fecha del ticket.

## 2. Volver a analizarlo, sin cambiar el código

No te fíes del ticket: el código cambia cada hora, y la revisión que generó estos tickets exageró la gravedad y se equivocó en varios. Comprobalo de nuevo:

1. **Seguí el recorrido real en el código de hoy** y anotá cada `archivo:línea`:
   - en el back: controller → servicio → regla o error → repositorio o puerto;
   - en el front: pantalla → hook → llamada.
2. **Buscá la barrera que lo frena:** un validador, un permiso, RLS, una cuota, un lock, un tope o un test que ya lo cubre. Muchas veces existe.
3. **Si no queda claro,** escribí el test que lo reproduce y corrélo con el test dirigido de `AGENTS.md`. En rojo, prueba que es real. No lo commitees todavía.
4. **Medí qué tan probable es en la vida real:** quién tiene que hacer qué, cada cuánto pasa, y si hace falta alguien con mala intención, datos raros o un proveedor que falle.

## 3. Mostrarlo en vivo, cuando se pueda

Si el problema se ve en la app (una pantalla, un ingreso, un chat, una importación), mostralo fallando de verdad antes de recomendar. Se hace con el mismo mecanismo que la verificación de las etapas (`.agents/workflows/verificar-etapa.js`). Antes de arrancar, avisale al usuario que vas a levantar la app y que tarda unos minutos.

1. **Fijate que se pueda.** Si algo de esto falla, salteá la prueba en vivo y explicá por qué:
   - Docker responde (`docker info`);
   - quedan más de 15 GB libres en el disco C:;
   - no hay otro AppHost ni otro Vite levantado: mirá `aspire ps` y los procesos `dcp` y `node` con `vite`. La verificación y el recorrido usan los mismos puertos;
   - nadie está corriendo otra cosa pesada: mirá los procesos y el `git log` de los últimos minutos.
2. **Nunca sobre el checkout de `main`:** mientras la app está levantada, nadie puede editar el `src` del front. Armá copias del `HEAD` de hoy fuera del repo:
   - `git clone --local <back> C:/e2e-KAN-<n>/back` y `git clone --local <front> C:/e2e-KAN-<n>/front`, las dos en detached HEAD, sin ramas ni worktrees;
   - en la copia del front, `npm ci`.
3. **Levantá el entorno aislado del recorrido** desde la copia del front, con `createEnvironment()` de `scripts/recorrido-usuario-entorno.mjs`. Trae una base propia que se borra al terminar, los correos a archivos (con `pickupCode` se leen los códigos) y la app en `https://localtest.me:5174/`.
   - En `scripts/recorrido-usuario.mjs` está cómo entra una persona y cómo se arma una organización.
   - El build del back va por la fila pesada: `node scripts/arneses/carril.mjs fila --dir <copia del back> "<comando>"`, desde el back de `main`.
4. **Hacé en el navegador los pasos de «Cómo reproducirlo»,** tal cual los haría una persona, hasta ver la falla:
   - Claude usa Claude en Chrome en una pestaña nueva, o Playwright si la extensión no está conectada; Antigravity usa su navegador.
   - Solo con personas de prueba que creás en esa base, con correos `@acme.example`. Nunca el correo, el teléfono ni las cuentas reales del usuario.
   - Sin proveedores reales: WhatsApp y la IA van con sus falsos, que el entorno aislado ya usa. Si el problema necesita a Meta o a la IA de verdad, no va en vivo.
5. **Guardá lo que se vio** en `C:/e2e-KAN-<n>/evidencia/`: una captura por paso, otra del momento de la falla, o un video corto.
   - Mostráselo al usuario en el chat.
   - Si se puede, subí la captura de la falla al ticket como adjunto.
6. **Apagá y borrá todo:**
   - `aspire stop --apphost <la copia del back>/src/ArquitecturaBaseMultitenant.AppHost/ArquitecturaBaseMultitenant.AppHost.csproj`;
   - cerrá el navegador de la prueba;
   - borrá `C:/e2e-KAN-<n>/` entera, porque las copias ocupan unos 6 GB.

   Nunca dejes un AppHost levantado.

Si el problema no se ve en una pantalla (concurrencia, escala, despliegue, configuración o un proveedor real), mostralo con el test que lo reproduce: en rojo, con su salida. Decí que esa es la forma en que se ve.

## 4. Explicarlo y recomendar

Mostrale al usuario en el chat, en lenguaje llano, sin jerga y con un ejemplo de personas y pantallas:

- **Qué pasa:** dos o tres oraciones, como se lo contarías a alguien que no programa.
- **Cómo se ve:** el paso a paso para verlo, con lo esperado y lo que pasa de verdad, y lo que mostraste en vivo (las capturas o el video) o el test en rojo.
- **¿Es real?** Sí, en parte o no, con la prueba: el `archivo:línea` o el test en rojo.
- **¿Qué tan probable es?** Frecuente, posible, rara o casi imposible, y por qué.
- **Con qué se cruza:** los tickets y las tareas que tocan lo mismo, y si conviene arreglarlos juntos.
- **Qué hacer,** con la opción recomendada primero: arreglar ahora, dejarlo para más adelante o descartarlo. Si es arreglar, el cambio en dos o tres líneas y el test que lo prueba.

Dejá lo mismo como comentario en el ticket, con el título «Análisis del <fecha>». Después frená y esperá la respuesta del usuario.

## 5. Lo que decida el usuario

- **Descartar:**
  - comentá el motivo; si la revisión se equivocó, decilo así: «La revisión se equivocó: …»;
  - sumá la etiqueta `descartado`;
  - movelo a «Done».
- **Más adelante:** comentá el motivo, bajale la prioridad si corresponde y devolvelo a «To Do».
- **Arreglar:**
  1. **Lo que espera el plan:** si el plan de la tarea espera algo antes de arrancar, confirmá que ya se cumplió o que el usuario dijo que arranques igual.
  2. **Programalo** con `AGENTS.md` y la tarea del plan, si hay una:
     - el test dirigido en rojo, el código y el test en verde;
     - el build de la solución; en el front, `npx tsc -b` y el lint de lo que tocaste;
     - sin suites enteras: corren una vez, en la revisión.
  3. **Commiteá** solo tus rutas: `git commit -m "<tipo>(<área>): <qué hace>" -m "Jira: KAN-<n>" -- <tus rutas>`. Si el ticket es de la tarea de un plan, sumá también `-m "Tarea: <clave>/<id>"`.
  4. **Mostrá que ya no falla:** si lo mostraste en vivo, repetí la misma prueba sobre una copia nueva con el arreglo y mostrale al usuario que ahora anda. Apagá y borrá todo igual que en el paso 3.
  5. **Cerrá tu parte:** comentá en el ticket qué cambió, los commits, el test y la prueba en vivo, y movelo a «In Review».
  6. **No lo pasás a «Done»:** eso lo hace quien lo revisa.

## Repaso rápido: /ticket repaso <filtro>

Sirve para limpiar el tablero de tickets que casi nunca pasan o que están mal analizados. No arregla nada.

1. **Traé los tickets del filtro con JQL,** por ejemplo `project = KAN AND priority = Low AND statusCategory != Done`, o los de un épico.
2. **Pasá cada uno por lo mínimo del paso 2:** si sigue en el código, si hay una barrera que lo frena y qué tan probable es.
3. **Devolvé una tabla** con una fila por ticket:
   - la clave y, en una línea, qué dice;
   - el veredicto: real, en parte, no es real o ya arreglado;
   - la probabilidad;
   - la recomendación (arreglar, más adelante o descartar) y su motivo.
4. **No cambies los tickets.** El usuario elige cuáles descartar, y recién ahí se comentan y se cierran con la etiqueta `descartado`.

## Revisión, la hace otro agente

La revisa alguien que no programó el arreglo:
1. Lee el comentario del ticket y el diff de los commits con `Jira: KAN-<n>`.
2. Corre el test dirigido.
3. Si está bien, lo pasa a «Done» con un comentario. Si no, comenta lo que falta y lo devuelve a «In Progress».

Las tareas de un plan con conductor se revisan juntas, con `etapa.js` en modo `cerrar`.
