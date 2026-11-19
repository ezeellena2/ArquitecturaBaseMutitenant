# Conducir una etapa de punta a punta

El conductor es un workflow que programa una etapa entera desde su plan ejecutable. Corre las tres partes de [una etapa en paralelo](etapa-en-paralelo.md) y la skill [`/puerta`](../../.agents/skills/puerta/SKILL.md) con un agente nuevo por tarea, y para solo cuando necesita al usuario. La Guía ya no abre cada chat a mano: lanza el conductor, le muestra al usuario lo que este decide o hace, y lo relanza. El script es [`.agents/workflows/etapa.js`](../../.agents/workflows/etapa.js) y la decisión, la 0067 del [índice](../decisions/README.md).

## Antes de lanzarlo

La sesión que lo lanza revisa cuatro cosas, en este orden:
1. **El cupo semanal**, con `get_usage`. Con 85 % no se lanza (`AGENTS.md`, «Sesiones y cupo»).
2. **Que nadie más programe en estos repos.** Ninguna otra sesión programando en el back ni en el front, y la app apagada: ni Aspire ni Vite.
3. **Que la PC no se suspenda** durante la corrida (`request_keep_awake`), porque Docker no se recupera de una suspensión.
4. **El modo `planificar`.** Corre un solo agente, que solo lee. Devuelve las tareas en el orden en que van a correr, sus carriles, riesgos y esperas, lo que bloquea y cuántos agentes va a usar.

## Cómo se lanza

```js
Workflow({ scriptPath: '.agents/workflows/etapa.js', args: { plan: 'docs/plans/2026-11-19-etapa-base-de-la-12-segunda-parte.md' } })
```

| Argumento | Qué hace |
|---|---|
| `plan` | la ruta del plan ejecutable (obligatorio) |
| `modo` | `planificar`: solo lee. `programar`: contrato, back, front e integración. `cerrar`: revisión del cierre y puerta, con todo programado. `todo` (por defecto): los dos, y frena antes de la puerta si el usuario tiene algo que hacer antes |
| `solo` | los ids que corren esta vez, por ejemplo `["K0", "K1", "B1"]`. Sirve para medir el costo con pocas tareas |
| `usuarioListo` | `true` cuando el usuario ya hizo lo que la puerta necesita, por ejemplo cargar sus user-secrets |

Para retomar una corrida cortada, se lanza otra vez con el mismo plan: lo que ya está commiteado se saltea.

## Qué hace

| Fase | Qué hace |
|---|---|
| Leer | Lee el plan y la spec, y arma la lista de tareas. Marca como hecha la que tiene su commit y su código, como del usuario la que es suya (secretos, Meta), y como bloqueada la que espera un tablero o una decisión. Frena si la etapa anterior no cerró, si hay una decisión abierta, si la app está levantada, si el cupo llegó al 85 % o si las esperas forman un ciclo |
| Contrato | Las tareas K, en orden. Si una no queda hecha, no corre nada más |
| Programar | El back y el front a la vez. Cada repo tiene un solo agente escribiendo |
| Integrar | El front contra el contrato real. Cada diferencia se arregla en el repo de su dueño |
| Revisión | La lista de control de `/puerta`, una vez por repo y sobre todos los commits de la etapa, con sus arreglos |
| Puerta | `/puerta` una sola vez. Cada fallo vuelve a su dueño, y con el arreglo se corre solo lo que toca |
| Cierre | Actualiza en el plan la tabla de estado, la de la puerta y «Cambios al contrato», y borra las copias |

## Reglas que pone

- **Un escritor por repo.** Dos agentes del back en el mismo checkout se pisan el build, los tests y el `ModelSnapshot`. Por eso, si el plan tiene dos carriles del back, se intercalan en un solo orden que respeta sus reglas, y el front va a la vez que el back.
- **Cada commit lleva la línea `Tarea: <clave>/<id>`.** La clave es el nombre del plan sin la fecha ni `etapa-`, por ejemplo `base-de-la-12-segunda-parte`. Las fases suman `integracion`, `revision`, `puerta` y `cierre`.
- **Una tarea con (R) no se commitea antes de su revisión adversarial.** El revisor recibe el código y los escenarios de su riesgo, sin la conclusión de quien programó. Corrige y commitea un tercer agente (`AGENTS.md`, «La revisión adversarial va según el riesgo»).
- **Una tarea que pasa los 150 pasos** sigue con un agente nuevo, hasta tres.
- **Lo de producto vuelve como pregunta.** La tarea frena, lo que la espera no corre y el resto sigue. Si una tarea deja cambios sin commitear, se frena su repo.
- **El cupo:** cada agente lo mira antes de empezar. Con 85 % no empieza nada nuevo.
- **Sin proveedores reales, sin secretos, sin tocar el lienzo y sin push.** El conductor no cierra la etapa: `ClosedStages` y retirar el plan van cuando el usuario lo pide ([documentación](../rules/documentacion.md)).

## Qué devuelve

Devuelve el estado de cada tarea, con sus commits, y lo que el usuario decide o hace: las preguntas, sus pasos (comando por comando y sin valores), la galería para mirar y las pruebas con Meta. Devuelve también los cambios al contrato, la tabla de la puerta y lo que falta. Al usuario se le muestra solo eso, de a una decisión y con la recomendada primero.

## Lo verifica

`scripts/arneses/conductor.test.mjs` corre el script con agentes falsos, sin gastar tokens. Prueba el orden y las esperas, que back y front corran a la vez, la revisión adversarial y lo que frena (bloqueos, cupo, ciclos, cambios sin commitear). También prueba retomar, `solo`, la puerta con sus arreglos y que las fases coincidan con `meta`.
