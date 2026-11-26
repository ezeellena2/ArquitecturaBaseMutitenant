# Conducir una etapa de punta a punta

El conductor es un workflow que programa una etapa entera desde su plan ejecutable, o varias seguidas. Corre las tres partes de [una etapa en paralelo](etapa-en-paralelo.md) y la skill [`/puerta`](../../.agents/skills/puerta/SKILL.md) con un agente nuevo por tarea, y para solo cuando necesita al usuario. La Guía ya no abre cada chat a mano: lanza el conductor, le muestra al usuario lo que este decide o hace, y lo relanza. El script es [`.agents/workflows/etapa.js`](../../.agents/workflows/etapa.js). Las decisiones son la 0067 del [índice](../decisions/README.md) y la [0071](../decisions/0071-conductor-en-carriles.md), que pone el back en carriles y se enmendó el 2026-11-26 con código primero, por pedido del usuario. Los revisores por premisa, «Para después» y seguir solo con `planes` salen de las decisiones del usuario del 2026-11-23.

## Antes de lanzarlo

La sesión que lo lanza revisa cuatro cosas, en este orden:
1. **El cupo semanal**, con `get_usage`. Con 85 % no se lanza (`AGENTS.md`, «Sesiones y cupo»).
2. **Que nadie más programe en estos repos.** Ninguna otra sesión programando en el back ni en el front, y la app apagada: ni Aspire ni Vite.
3. **Que la PC no se suspenda** durante la corrida (`request_keep_awake`), porque Docker no se recupera de una suspensión. Cada carril del back necesita unos 4 GB de disco para su copia, y `carril.mjs` no crea una si quedan menos de 15 GB libres.
4. **El modo `planificar`.** Corre un solo agente por plan, que solo lee. Devuelve las tareas en el orden en que van a correr, sus carriles, riesgos y esperas, lo que bloquea y cuántos agentes va a usar.

## Cómo se lanza

```js
Workflow({ scriptPath: '.agents/workflows/etapa.js', args: { plan: 'docs/plans/2026-11-19-etapa-base-de-la-12-segunda-parte.md' } })
```

| Argumento | Qué hace |
|---|---|
| `plan` | la ruta del plan ejecutable. Va este o `planes` |
| `planes` | varias rutas, en orden. Corre cada plan con todos sus controles y sigue con el siguiente solo si el anterior cerró con la puerta en verde y sin nada pendiente para el usuario. Va con `modo` `todo` o `planificar`, sin `solo` |
| `modo` | `planificar`: solo lee. `programar`: contrato, back, front e integración. `cerrar`: revisión del cierre y puerta, con todo programado. `todo` (por defecto): los dos, y frena antes de la puerta si el usuario tiene algo que hacer antes |
| `solo` | los ids que corren esta vez, por ejemplo `["K0", "K1", "B1"]`. Sirve para medir el costo con pocas tareas |
| `usuarioListo` | `true` cuando el usuario ya hizo lo que la puerta necesita, por ejemplo cargar sus user-secrets. Con `planes`, vale solo para el primero |
| `carrilesBack` | cuántos carriles del back corren a la vez, de 1 a 4. Por defecto 2. Con 1, el back trabaja en main, como antes de la 0071 |
| `modelos` | otro modelo por rol: `{ front, back, verifica }`, por ejemplo `{ front: 'sonnet' }`. Por defecto, todos usan el de la sesión. Antes de usarlo en una etapa, se mide con una tarea |
| `topeCupo` | el porcentaje del cupo semanal desde el que los agentes no empiezan nada nuevo. Por defecto 85 (`AGENTS.md`, «Sesiones y cupo»). Se sube solo si el usuario lo pide, como el 2026-11-25 para terminar la base 2 |

Para retomar una corrida cortada, se lanza otra vez con el mismo plan: lo que ya está commiteado se saltea. Con `resumeFromRunId`, en cambio, se pasa el mismo script con que se lanzó. Si el conductor cambió desde entonces, se usa una copia del script viejo guardada en `.local/conductor/`, o los agentes ya hechos se vuelven a correr. Si una cadena frenó, se relanza con los planes que faltan, después de resolver lo que la frenó.

## Qué hace

| Fase | Qué hace |
|---|---|
| Leer | Lee el plan y la spec, y arma la lista de tareas. Marca como hecha la que tiene su commit y su código, como del usuario la que es suya (secretos, Meta), y como bloqueada la que espera un tablero o una decisión. Frena si la etapa anterior no cerró, si hay una decisión abierta, si la app está levantada, si el cupo llegó al 85 % o si las esperas forman un ciclo |
| Contrato | Las tareas K, en orden. Si una no queda hecha, no corre nada más |
| Programar | Los carriles del back y el front, a la vez. Cada carril del back trabaja en su copia y entrega encima de main. Cada tarea, con riesgo o sin él, hace su código, su test dirigido y el build, y commitea. El front tiene un solo agente escribiendo |
| Integrar | El front contra el contrato real. Cada diferencia se arregla en el repo de su dueño. Deja listo lo que corre la puerta: los pasos del recorrido, los casos de la galería y los E2E de la etapa |
| Revisión | Cinco revisores, uno por premisa, más uno adversarial por cada tema de riesgo de las tareas con (R), cada uno con su verificador, y un arreglador por repo con lo confirmado |
| Puerta | `/puerta` una sola vez, con los tests enteros, la integración y la seguridad. Después, el revisor de pantallas mira la galería, con su verificador. Los fallos y lo confirmado de pantallas vuelven a su dueño, y con el arreglo se corre solo lo que toca |
| Cierre | Actualiza en el plan la tabla de estado, la de la puerta y «Cambios al contrato», escribe lo de para después en el [backlog](../backlog.md) y borra las copias |

Con la etapa completa, una corrida de cierre usa hasta 20 agentes: integrar, 6 revisores, 6 verificadores, hasta 4 arregladores, la puerta, lo que tocan sus arreglos y el cierre. Cada tema de riesgo suma dos: su revisor adversarial y su verificador.

## Las premisas de la revisión

Cada revisor ve los commits de la etapa en los dos repos (los que llevan `Tarea: <clave>/`), la spec y solo las reglas y los puntos de la lista de control de `/puerta` de su premisa:

| Premisa | Qué mira |
|---|---|
| Textos y errores | voseo, «Organización», textos de resources en es y en, ProblemDetails con `code`, el front decide por `code`, un solo dueño por error (puntos 1, 2, 7 y 14) |
| Arquitectura, clean code y SOLID | capas y sufijos, una responsabilidad, nada duplicado, una sola forma de guardar, `Result`, tamaño y complejidad (puntos 3, 4, 5, 8 y 11, y los bloques 1 a 3 de [revisar la arquitectura](revisar-arquitectura.md)) |
| Caché, cuotas y módulos | claves de `CacheKeys` con generaciones, cuotas por alcance, módulos por puertos y quitables, los límites de módulos del front (punto 6 en caché y cuotas, y 15) |
| Seguridad y aislamiento | RLS, `ITenantScope`, accesos y permisos, 404 ajeno, trabajos de fondo con su organización, la pantalla sigue al servidor (puntos 6 y 16) |
| Lo que no está en la spec | corre `arnes.mjs conformidad --detectar --plan <plan>`, compara lo nuevo contra la spec y los tableros, y mira los commits que se pisan |
| Adversarial · <tema> | una por tema de riesgo de las tareas con (R): datos, permisos, efectos o aislamiento. Intenta romper esas tareas con los escenarios de su tema, sin la conclusión de quien programó |
| Pantallas contra el tablero | después de la galería: piezas compartidas, ventanas, colores, filtros y estados (puntos 10 y 13) |

Los puntos que no tiene ninguna premisa, como el 9 y el 12 y los de datos y proveedores, los miran las premisas adversariales de cada tema de riesgo; el contrato lo mira la puerta.

## Reglas que pone

- **Un escritor por checkout.** Dos agentes del back en el mismo checkout se pisan el build, los tests y el `ModelSnapshot`. Por eso cada carril del back trabaja en su copia de `.local/copias/`, y entrega encima de main con `node scripts/arneses/carril.mjs entregar <copia>`:
  - **De a un carril por vez.** Si entró algo de otro carril, primero verifica la copia con eso.
  - **Main avanza solo con `--ff-only`,** y nunca pisa los cambios sin commitear de otra sesión.

  Las tareas de la misma área van en el mismo carril, y todas las que traen migración, juntas. El front tiene un solo escritor, en main.
- **Código primero** (pedido del usuario del 2026-11-26). Cada tarea corre su test dirigido y el build (en el front, `tsc -b` y el lint de lo que tocó), y commitea. Los tests rápidos enteros, la integración y la seguridad corren una vez, en la puerta.
- **Consola callada.** El build y los tests se corren con `node scripts/arneses/verificar.mjs` en el back y `node scripts/verificar.mjs` en el front. Muestran solo lo que falla y dejan el log entero en `.local/verificar/`.
- **Cada commit lleva la línea `Tarea: <clave>/<id>`.** La clave es el nombre del plan sin la fecha ni `etapa-`, por ejemplo `base-de-la-12-segunda-parte`. Las fases suman `integracion`, `revision`, `puerta` y `cierre`.
- **La revisión adversarial va una vez, en el cierre.** Una premisa por tema de riesgo lee los commits de las tareas con (R) de ese tema, con sus escenarios y sin la conclusión de quien programó. Su verificador confirma y el arreglador del repo corrige (`AGENTS.md`, «La revisión adversarial va una vez por etapa, según el riesgo»).
- **Solo lo confirmado se arregla.** Cada premisa pasa por un verificador nuevo apenas termina su revisor. Recibe las afirmaciones sin el razonamiento e intenta refutarlas contra el código. Lo que no confirma, o lo que queda sin verificar, no llega al arreglo y se devuelve como descartado.
- **Lo que no está en la spec no se agrega.** Ni una tabla, columna, filtro, combo, ventana, ruta o permiso que no pidan la spec, el contrato o el tablero. El agente lo anota en `paraDespues` y sigue. Pregunta solo si la pantalla no funciona sin eso, con una captura si la tiene.
- **Una tarea que pasa los 80 pasos** sigue con un agente nuevo, hasta seis. Si una corrida anterior la dejó a medias, el agente nuevo mira sus commits y sigue desde ahí. Cada paso relee todo lo que el agente lleva, así que uno corto gasta mucho menos. Cada agente recibe además el mapa de su tarea (los archivos y el modelo que copia) y lee por partes.
- **Lo de producto vuelve como pregunta.** La tarea frena, lo que la espera no corre y el resto sigue. Si una tarea deja cambios sin commitear, se frena su repo.
- **Con `planes`, sigue solo con la puerta en verde.** Frena si queda una pregunta, un paso del usuario que no sea solo mirar (secretos, Meta, una tarea suya), una revisión sin terminar o un arreglo bloqueado. El plan siguiente no cuenta como bloqueo que el anterior todavía no figure cerrado.
- **El cupo:** cada agente lo mira antes de empezar. Con 85 % no empieza nada nuevo.
- **Sin proveedores reales, sin secretos, sin tocar el lienzo y sin push.** El conductor no cierra la etapa: `ClosedStages` y retirar el plan van cuando el usuario lo pide ([documentación](../rules/documentacion.md)).

## Qué devuelve

Devuelve el estado de cada tarea, con sus commits, y lo que el usuario decide o hace: las preguntas, sus pasos (comando por comando y sin valores), la galería para mirar y las pruebas con Meta. Devuelve también los cambios al contrato, lo de para después, cada premisa con sus confirmados y descartados, la tabla de la puerta y lo que falta. Con `planes`, devuelve eso por cada plan que corrió, dónde frenó y por qué, y los planes sin correr. Al usuario se le muestra solo eso, de a una decisión y con la recomendada primero.

## Lo verifica

`scripts/arneses/conductor.test.mjs` corre el script con agentes falsos, sin gastar tokens. Prueba el orden y las esperas, el reparto del back en carriles, que carriles y front corran a la vez, que la integración no corra por carril, los modelos, que una tarea con riesgo commitee como cualquiera y su premisa adversarial en el cierre, y lo que frena (bloqueos, cupo, ciclos, cambios sin commitear). También prueba un revisor y un verificador por premisa, que lo no confirmado no llegue al arreglo, las pantallas después de la galería, «Para después» en el backlog, `planes` y cuándo frena, retomar, `solo`, la puerta con sus arreglos y que las fases coincidan con `meta`.

`scripts/arneses/carril.test.mjs` prueba las copias y la entrega en repos temporales, y `verificar.test.mjs` (en los dos repos), la salida mínima.
