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
   - **En los planes:** `grep -n "<T0N-MM>\|KAN-<n>" docs/plans/*.md`, con los números de sus etiquetas `antigravity-T0N-MM`. Si una tarea de un plan lo cubre, esa tarea manda: sus archivos, su test, su línea `Tarea:` y lo que el plan espera antes de arrancar. Por ejemplo, `docs/plans/2026-11-08-etapa-13-campos.md` espera el código listo de los arreglos de la revisión externa. Los otros tickets de esa tarea van en este mismo chat.
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
5. **Guardá la evidencia del «antes» en una carpeta persistente (fuera de `C:/e2e-KAN-<n>/`, nunca commiteada al repo):**
   - Antigravity guarda en `.local/antigravity/evidencia-kan-<n>/antes/` y Claude en `.local/evidencia-kan-<n>/antes/`: una captura numerada por paso (`01-…png`, `02-…png`), la captura del momento de la falla y el video `.webm` de la corrida cuando se grabe.
   - Mostrale las capturas/video al usuario en el chat (con rutas clickeables).
   - Si se puede, subí la captura de la falla al ticket como adjunto y nombrá la carpeta de evidencia en el comentario de Jira.
6. **Apagá y borrá las copias pesadas (conservando la evidencia):**
   - `aspire stop --apphost <la copia del back>/src/ArquitecturaBaseMultitenant.AppHost/ArquitecturaBaseMultitenant.AppHost.csproj`;
   - cerrá el navegador de la prueba;
   - borrá `C:/e2e-KAN-<n>/` entera, porque las copias ocupan unos 6 GB (la evidencia ya quedó a salvo en `.local/…/evidencia-kan-<n>/`).

   Nunca dejes un AppHost levantado.

Si el problema no se ve en una pantalla (concurrencia, escala, despliegue, configuración o un proveedor real), mostralo con el test que lo reproduce: en rojo, con su salida guardada en `.local/…/evidencia-kan-<n>/antes/test-rojo.txt` (o `.log`). Decí que esa es la forma en que se ve.

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
- **Arreglar (flujo ticket por ticket + Claude Ship, pedido del usuario del 2026-11-28):**
  1. **Un ticket por vez (sin tandas en paralelo sobre «To Do»):** se toma y se resuelve únicamente este ticket (salvo si el plan de una etapa explícitamente agrupa otros bajo la misma tarea y el usuario pide correrlo por el conductor de etapas).
  2. **Lo que espera el plan:** si el plan de la tarea espera algo antes de arrancar, confirmá que ya se cumplió o que el usuario dijo que arranques igual.
  3. **Programalo** con `AGENTS.md` y la tarea del plan, si hay una:
     - el test dirigido en rojo, el código y el test en verde;
     - el build de la solución; en el front, `npx tsc -b` y el lint de lo que tocaste;
     - sin suites enteras: corren en el PR / revisión.
  4. **Guardá y mostrá la evidencia del «después» (obligatorio en cada ticket):**
     - Si se puede ver en la app, repetí la misma prueba en vivo sobre una copia nueva con el arreglo, guardá capturas numeradas + video `.webm` en `.local/antigravity/evidencia-kan-<n>/despues/` (o `.local/evidencia-kan-<n>/despues/` en Claude), mostráselo al usuario en el chat, y apagá y borrá `C:/e2e-KAN-<n>/` igual que en el paso 3.
     - Si no se ve en la app (o la prueba en vivo está bloqueada por otro AppHost/puertos, explicando el motivo), guardá la salida del test dirigido en verde y del build/lint en `.local/…/evidencia-kan-<n>/despues/` (junto con el `antes/test-rojo.txt` del paso 3).
     - La evidencia **nunca se commitea al repo** (vive en `.local/`, ignorada por git), pero su ruta exacta y un resumen de lo comprobado quedan en el comentario de Jira y en el mensaje al usuario.
  5. **No commitees directo a `main` ni abras el siguiente ticket:** dejá los cambios verificados en el working tree (o, si el usuario pidió explícitamente commit en `main`, solo con `git commit -m "<tipo>(<área>): <qué hace>" -m "Jira: KAN-<n>" -- <tus rutas>`), comentá en Jira qué cambió, qué tests pasaron y la evidencia guardada (antes/después), y **avisale al usuario** con:
     - la **lista exacta de archivos tocados** por este ticket;
     - el enlace a la **carpeta de evidencia** (`.local/…/evidencia-kan-<n>/`, antes y después) y las capturas/video en el chat;
     - aviso explícito si en ese momento hay cambios sin commitear de otra sesión en el mismo repo (para que no se mezclen al armar la rama);
     - confirmación de que está listo para que el usuario le pida a **Claude Code** crear la rama, abrir el PR y revisarlo con **Claude Ship**.
  6. **No lo pasás a «Done»:** eso lo hace Claude tras revisar y aprobar el PR.

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

## Revisión y entrega por PR (Claude Ship)

El flujo estándar para tickets individuales tomados de «To Do» (decisión del usuario del 2026-11-28) es:
1. El agente que resuelve el ticket trabaja **únicamente ese ticket**, deja el código + test dirigido + build/lint en verde sin commitear en `main`, y avisa al usuario con la lista exacta de rutas tocadas.
2. Con el aviso del usuario, **Claude Code** crea la rama con solo esas rutas (cuidando que no haya otra sesión editando `main` en ese mismo directorio, o usando su propia copia/worktree), arma el PR con **Claude Ship**, analiza/revisa el PR con los cambios realizados y pasa el ticket a «In Review».
3. Si la revisión del PR está bien, se aprueba/mergea y el revisor lo pasa a «Done» con su comentario. Si encuentra algo, comenta lo que falta y devuelve el ticket a «In Progress».

Las tareas de un plan de etapa con conductor se revisan juntas con `etapa.js` en modo `cerrar`.
