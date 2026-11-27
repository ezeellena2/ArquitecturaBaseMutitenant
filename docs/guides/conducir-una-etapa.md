# Conducir una etapa de punta a punta

El conductor es un workflow que programa una etapa entera desde su plan ejecutable, o varias juntas, y la deja con el código listo. Corre en cinco fases (pedido del usuario del 2026-11-27): programar, revisar en paralelo, arreglar lo encontrado, una última revisión y el cierre con su commit. Cada etapa tiene dos hitos, que se avisan por separado: «Código listo», que da este conductor, y «Verificada», que da [la verificación](#la-verificación) sobre una copia del corte mientras la siguiente se programa en main: E12 código → (verificación E12 ∥ código E13) → (verificación E13 ∥ código E14)… Con varias etapas, la primera fase corre para cada una y de la segunda a la quinta, una sola vez. Usa un agente nuevo por tarea y para solo cuando necesita al usuario. La Guía lo lanza, le muestra al usuario lo que este decide o hace, y lo relanza. El script es [`.agents/workflows/etapa.js`](../../.agents/workflows/etapa.js). Las decisiones son la 0067 del [índice](../decisions/README.md) y la [0071](../decisions/0071-conductor-en-carriles.md), que pone el back en carriles y se enmendó el 2026-11-26 (código primero) y el 2026-11-27 (las cinco fases y los dos hitos).

## Antes de lanzarlo

La sesión que lo lanza revisa tres cosas:
1. **El cupo semanal**, con `get_usage`. Con 85 % no se lanza (`AGENTS.md`, «Sesiones y cupo»).
2. **Que nadie más programe en estos repos.** Ninguna otra sesión programando en el back ni en el front, y la app apagada: ni Aspire ni Vite.
3. **Que la PC no se suspenda** durante la corrida (`request_keep_awake`), porque Docker no se recupera de una suspensión. Cada carril del back y los tests enteros necesitan unos 4 GB de disco para su copia, y `carril.mjs` no crea una si quedan menos de 15 GB libres.

Se lanza directo con `modo: 'todo'`. El modo `planificar` sigue para mirar antes, sin tocar nada, el reparto en carriles y cuántos agentes va a usar.

## Cómo se lanza

```js
Workflow({ scriptPath: '.agents/workflows/etapa.js', args: { plan: 'docs/plans/2026-11-08-etapa-12-catalogos.md' } })
```

| Argumento | Qué hace |
|---|---|
| `plan` | la ruta del plan ejecutable. Va este o `planes` |
| `planes` | varias rutas, en orden. Programa cada plan y sigue con el siguiente solo si el anterior terminó su código sin nada pendiente para el usuario. De la fase 2 a la 5 corren una vez, al final, para todos. Va sin `solo` |
| `modo` | `todo` (por defecto): las cinco fases. `programar`: solo la 1. `cerrar`: de la 2 a la 5, con todo programado. `planificar`: solo lee |
| `solo` | los ids que corren esta vez, por ejemplo `["K0", "K1", "B1"]`. Sirve para medir el costo con pocas tareas |
| `usuarioListo` | `true` cuando el usuario ya hizo lo que necesitan los tests enteros, por ejemplo cargar sus user-secrets. Con `planes`, vale para todos |
| `carrilesBack` | cuántos carriles del back corren a la vez, de 1 a 4. Por defecto 2. Con 1, el back trabaja en main, como antes de la 0071 |
| `modelos` | otro modelo por rol: `{ front, back, revisa }`, por ejemplo `{ revisa: 'sonnet' }`. `revisa` es para los revisores de la fase 2. Por defecto, todos usan el de la sesión. Antes de usarlo en una etapa, se mide con una tarea |
| `topeCupo` | el porcentaje del cupo semanal desde el que los agentes no empiezan nada nuevo. Por defecto 85 (`AGENTS.md`, «Sesiones y cupo»). Se sube solo si el usuario lo pide, como el 2026-11-25 para terminar la base 2 |

Para retomar una corrida cortada, se lanza otra vez con el mismo plan: lo que ya está commiteado se saltea. Con `resumeFromRunId`, en cambio, se pasa el mismo script con que se lanzó. Si el conductor cambió desde entonces, se usa una copia del script viejo guardada en `.local/conductor/`, o los agentes ya hechos se vuelven a correr.

## Las cinco fases

| Fase | Qué hace |
|---|---|
| 1. Programar | Lee el plan y la spec, y arma la lista de tareas: marca como hecha la que tiene su commit y su código, como del usuario la que es suya (secretos, Meta) y como bloqueada la que espera un tablero o una decisión. Frena si la etapa anterior no tiene el código listo (su verificación puede seguir corriendo), si hay una decisión abierta, si la app está levantada, si el cupo llegó al 85 % o si las esperas forman un ciclo. Corre las tareas K en orden, y después los carriles del back (cada uno en su copia, que entrega encima de main) y el front, a la vez. Cada tarea hace su código, su test dirigido y el build, y commitea. Las tareas I del plan no corren: las cubren las fases 2 a 5 |
| 2. Revisión | Todo a la vez, sin verificadores: un revisor por cada ficha tocada, los dos transversales, uno adversarial por cada tema de riesgo tocado y los tests enteros (abajo) |
| 3. Arreglos | Los hallazgos, sin repetidos (mismo repo, archivo y línea), van a un agente por repo. Comprueba cada uno contra el código: lo que no es real lo descarta con su motivo, y lo real lo arregla con su test dirigido y el build, y commitea. Sin hallazgos, no corre |
| 4. Última revisión | Un revisor lee el diff entero de la etapa, con los arreglos, y busca lo que rompió un arreglo o lo grave que quedó. Lo chico y seguro lo arregla él, con su test; lo demás va al backlog. No hay otra ronda |
| 5. Cierre | Si la etapa quedó lista, la deja con el código listo, como se cerraron Acciones y Costuras en `82087a6a`: la spec en `cerrado` con una tabla corta de los tests enteros y la revisión, la sección «Verificación de la etapa» del plan entera al backlog, como «Verificación de la etapa <clave>», la documentación que nombra «Revisión y cierre», `git rm` del plan, la hoja de ruta con «Código listo el <fecha>; falta la verificación» y la siguiente, `ClosedStages` si la etapa tiene número y los detectores en 0, en un commit. Si quedó algo (una tarea, un arreglo bloqueado, una pregunta abierta, un revisor o los tests sin terminar), solo deja al día la tabla de estado del plan y el backlog |

Una etapa completa usa un agente para leer y uno por tarea, y después, una sola vez: el que lista los archivos, uno por ficha tocada, los dos transversales, uno por tema de riesgo, los tests enteros, hasta dos arregladores, la última revisión y el cierre.

## La revisión

Cada revisor lee los commits de la etapa en los dos repos (los que llevan `Tarea: <clave>/`) y la spec. Los riesgos que suma la sección «Revisión y cierre» del plan son ítems de su lista de control, y de la última revisión. Ninguno recibe la conclusión de quien programó.

| Revisor | Qué mira |
|---|---|
| Una ficha | Uno por cada ficha de `docs/rules/` del back y del front que tocan los archivos de la etapa. Un agente barato lista esos archivos, y el script los cruza con `REGLAS` de `etapa.js`, que une cada ficha con sus rutas. El revisor recibe la ficha y solo sus archivos con su diff, y busca únicamente incumplimientos de esa ficha. No lleva revisor una ficha que un test controla entera (hoy `capas-y-flujo` y `documentacion`) ni una que la etapa no tocó: el registro de la corrida dice cuáles y por qué. Ante la duda, una ficha lleva revisor |
| Arquitectura, clean code y SOLID | el diseño entero: capas, una responsabilidad, nada duplicado, una sola forma de guardar, `Result` y tamaño (puntos 3, 4, 5, 8 y 11 de la lista de control de `/puerta`, y los bloques 1 a 3 de [revisar la arquitectura](revisar-arquitectura.md)) |
| Lo que no está en la spec | corre `arnes.mjs conformidad --detectar --plan <plan>` de cada plan, compara lo nuevo contra la spec, el contrato y los tableros, y mira los commits que se pisan |
| Adversarial · <tema> | uno por cada tema que tocan los archivos de la etapa, según sus rutas: datos, permisos, efectos o aislamiento. Intenta romper esas tareas con los escenarios de su tema. Si no se supo qué archivos cambió la etapa, van los temas que el plan marca con (R) |
| Tests enteros | en copias de `HEAD` dentro de `.local/`, que se borran al terminar: `dotnet build` sin advertencias y `dotnet test` con Docker (los dos en la fila pesada de `carril.mjs`), `npm run build`, `lint`, `test` y `contracts:check`, y los tres detectores de documentación. Cada fallo vuelve como un hallazgo más |

Las pantallas contra el tablero, el E2E real, la galería y el recorrido no corren en el conductor: van en la verificación.

## La verificación

El segundo hito es otro workflow, [`.agents/workflows/verificar-etapa.js`](../../.agents/workflows/verificar-etapa.js), que se lanza apenas la etapa tiene el código listo, a la vez que el conductor de la siguiente:

```js
Workflow({ scriptPath: '.agents/workflows/verificar-etapa.js', args: { clave: '12-catalogos' } })
```

Va con `clave` o con `plan` (aunque ya esté retirado), y con `corte: { back, front }` si no es el `HEAD` del momento. Trabaja sobre copias del corte en `C:\e2e-<clave>\`, sin worktrees ni ramas, y sin proveedores reales:

| Fase | Qué hace |
|---|---|
| a. Preparar | Lee la sección de verificación: en el plan, si sigue; si no, en el backlog («Verificación de la etapa <clave>», o «Para después de la etapa <clave>», como la dejaba el cierre de antes de los dos hitos), o en el plan que retiró git. Un agente escribe en main los casos del E2E real, los pasos del recorrido y el JSON de la galería (scripts del front, sin tocar su `src`), y otro arma las copias con esos casos encima |
| b. Correr | El build de la copia y `contracts:check` (la suite de integración ya corrió en la fase 2), E2E real, galería, recorrido a 1440 y a 390, y quitar el módulo si la sección lo pide: cada paso entero una vez, con su agente, y cada comando pesado en su propia llamada a la fila de `carril.mjs` (`fila --dir <copia>`), que se suelta entre paso y paso para que entren los carriles de la etapa siguiente. Sobre la galería, un revisor de «Pantallas contra el tablero» y su verificador, a la vez que el recorrido |
| c. Arreglos | Cada fallo y cada diferencia confirmada, en main, por su dueño y con su test dirigido |
| d. Repetir | Trae los arreglos a las copias con `cherry-pick`, nunca el `HEAD` de main, y corre solo lo que tocan |
| e. Cierre | Verificada: la hoja de ruta pasa a «Verificada el <fecha>», la tabla corta va a la spec, la galería al front de main y la sección sale del backlog. Con fallos: la hoja de ruta lo dice y la sección se queda. En los dos casos, lo de a mano (un teléfono, un proveedor real) va a «Para después de la etapa <clave>» y se borran las copias |

Devuelve «verificada» o «verificación con fallos», con lo que falta. La prueba integral del cierre del bloque sigue como pasada final con el usuario ([`/puerta`](../../.agents/skills/puerta/SKILL.md) y [`/recorrido`](../../.agents/skills/recorrido/SKILL.md)).

## Reglas que pone

- **Un escritor por checkout.** Dos agentes del back en el mismo checkout se pisan el build, los tests y el `ModelSnapshot`. Por eso cada carril del back trabaja en su copia de `.local/copias/`, y entrega encima de main con `node scripts/arneses/carril.mjs entregar <copia>`:
  - **De a un carril por vez.** Si entró algo de otro carril, primero verifica la copia con eso.
  - **Main avanza solo con `--ff-only`,** y nunca pisa los cambios sin commitear de otra sesión.

  Las tareas de la misma área van en el mismo carril, y todas las que traen migración, juntas. El front tiene un solo escritor, en main, y en las fases 3 y 4 hay un solo escritor por repo.
- **Código primero** (pedido del usuario del 2026-11-26). Cada tarea corre su test dirigido y el build (en el front, `tsc -b` y el lint de lo que tocó), y commitea. Los tests enteros corren una vez, en la fase 2.
- **Consola callada.** El build y los tests se corren con `node scripts/arneses/verificar.mjs` en el back y `node scripts/verificar.mjs` en el front. Muestran solo lo que falla y dejan el log entero en `.local/verificar/`.
- **Cada commit lleva la línea `Tarea: <clave>/<id>`.** La clave es el nombre del plan sin la fecha ni `etapa-`, por ejemplo `base-de-la-12-segunda-parte`. Las fases suman `revision`, `ultima-revision` y `cierre`.
- **Lo que no está en la spec no se agrega.** Ni una tabla, columna, filtro, combo, ventana, ruta o permiso que no pidan la spec, el contrato o el tablero. El agente lo anota en `paraDespues` y sigue. Pregunta solo si la pantalla no funciona sin eso, con una captura si la tiene.
- **Una tarea que pasa los 80 pasos** sigue con un agente nuevo, hasta seis. Si una corrida anterior la dejó a medias, el agente nuevo mira sus commits y sigue desde ahí. Cada agente recibe además el mapa de su tarea (los archivos y el modelo que copia) y lee por partes.
- **Lo de producto vuelve como pregunta.** La tarea frena, lo que la espera no corre y el resto sigue. Si una tarea deja cambios sin commitear, se frena su repo. Con una pregunta abierta, la etapa no se cierra.
- **Con `planes`, sigue solo con el código terminado.** Frena si queda una pregunta, un paso del usuario que no sea solo mirar (secretos, Meta, una tarea suya) o una tarea sin hacer. De la fase 2 a la 5 corren sobre los planes que se programaron, si todos terminaron su código.
- **El cupo:** cada agente lo mira antes de empezar. Con 85 % no empieza nada nuevo.
- **Sin proveedores reales, sin secretos, sin tocar el lienzo y sin push.** El cierre retira el plan y suma `ClosedStages` por pedido del usuario del 2026-11-27 ([documentación](../rules/documentacion.md)).

## Qué devuelve

Devuelve el estado de cada tarea, con sus commits, y lo que el usuario decide o hace: las preguntas, sus pasos (comando por comando y sin valores) y las pruebas con Meta. El resultado dice «código listo», con la llamada a la verificación. Devuelve también los cambios al contrato, lo de para después, el reparto de la revisión (las fichas con revisor, las salteadas y por qué, y los temas de riesgo), cada revisor con sus hallazgos, la tabla de los tests enteros, los arreglos con lo arreglado y lo descartado, la última revisión, el cierre y lo que quedó pendiente. Con `planes`, devuelve eso por cada plan, dónde frenó y por qué, los planes sin correr, y una sola revisión y un solo cierre. Al usuario se le muestra solo eso, de a una decisión y con la recomendada primero.

## Lo verifica

`scripts/arneses/conductor.test.mjs` corre el script con agentes falsos, sin gastar tokens. Prueba el orden y las esperas, el reparto del back en carriles, que carriles y front corran a la vez, los modelos, y lo que frena (bloqueos, cupo, ciclos, cambios sin commitear). También prueba las cinco fases en orden, que en la revisión todo corra a la vez, un revisor por ficha con solo sus archivos y lo salteado en el registro, los tests enteros en copias de `HEAD`, los temas de riesgo según los archivos, que los repetidos se junten antes de arreglar, la última revisión con su backlog, el cierre entero o solo la tabla de estado, «Para después» en el backlog, `planes` con una sola revisión y un solo cierre, retomar, `solo` y que las fases coincidan con `meta`.

`scripts/arneses/verificar-etapa.test.mjs` prueba la verificación igual: las fases en orden, de dónde lee la sección, las copias en el corte, que cada paso tome y suelte la fila por su cuenta, las pantallas a la vez que el recorrido, los arreglos por dueño, que se repita solo lo tocado y los dos resultados.

`scripts/arneses/carril.test.mjs` prueba las copias, la entrega y la fila en repos temporales, y `verificar.test.mjs` (en los dos repos), la salida mínima.
