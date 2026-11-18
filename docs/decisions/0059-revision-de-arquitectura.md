# ADR 0059 · Revisar la arquitectura de cada desarrollo, con el aislamiento entre organizaciones

Estado: aceptada el 2026-11-18 por el dueño del producto.

## Contexto

El 12 de noviembre, en ArquitecturaBase, el dueño del producto pidió revisar cada desarrollo contra unos 30 criterios de
arquitectura: que no se mezclen los datos, varias copias de la app, volumen, terceros, seguridad, datos, lo que ve el usuario
y operación. Se armó una guía y entre el 12 y el 16 se revisaron WhatsApp, ARCA, el núcleo, IA y Formularios. Cada revisión
encontró entre 25 y 55 hallazgos, y cada una usó de 11 a 20 agentes: las cinco, con sus arreglos, llevaron el cupo semanal al
98 % (traspaso 13, 06 §3; traspaso 14, 01).

El multitenant no tenía una revisión así: solo la lista de control de la puerta, que mira lo de una etapa. Y acá pesa más,
por dos motivos:

- **Varias organizaciones comparten las mismas copias desde el principio.** El E10 no fija un máximo de réplicas, así que lo
  que en ArquitecturaBase era «Varias copias» acá es «Hoy». Por ejemplo, `AssistantRetentionWorker` corre en cada copia y
  recorre todas las organizaciones.
- **Una falla de aislamiento muestra lo de una organización a otra,** y una copia mal configurada afecta a todas.

## Decisión

- **Se adopta la guía [`revisar-arquitectura.md`](../guides/revisar-arquitectura.md):** los 30 criterios de ArquitecturaBase
  y un bloque 9 propio, «Que no se mezclen las organizaciones», con seis criterios más:
  - RLS y el 404 de lo ajeno;
  - el contexto de los trabajos de fondo;
  - las colas, la caché y las cuotas por alcance;
  - los archivos;
  - Plataforma y lo público;
  - los módulos por organización.
- **Dos horizontes:**
  - **Hoy** son varias organizaciones en dos o más copias de la Api, con la misma base y el mismo Redis;
  - **Gran escala** son muchas organizaciones con su volumen.
  
  Se arregla lo de «Hoy». Lo de «Gran escala» se programa solo si el dueño del producto lo elige
  ([robustez proporcional](../rules/guardado.md)).
- **Una revisión por desarrollo terminado:** el núcleo, WhatsApp y el Asistente antes del primer despliegue (E10), y
  Formularios una vez al cerrar el bloque E12–E25, antes de la prueba integral, partido en piezas. Se repite cuando un
  desarrollo cambia mucho.
- **Solo lectura:** los revisores leen una copia del commit. Cada «No cumple» y «Con reservas» pasa por un verificador que
  intenta refutarlo, y un revisor de contraste lee el informe entero contra el código.
- **El registro** va a `docs/reviews/AAAA-MM-DD-arquitectura-<desarrollo>.md`. Al dueño del producto se le preguntan solo las
  decisiones de producto y de horizonte. Los arreglos se anexan al plan del área.
- **El costo se planifica:** antes de empezar se mira el cupo de la semana, y se corre una revisión por vez.

## Consecuencias

- `docs/rules/documentacion.md` suma la carpeta `docs/reviews/`, y el arnés `estado-docs` la reconoce.
- La tabla «Si vas a…» de `AGENTS.md` y el arnés nombran la guía.
- Es aparte de la revisión adversarial según el riesgo, que se hace en cada tarea que toca datos, permisos, efectos fuera del
  sistema o el aislamiento entre organizaciones (`AGENTS.md`, «Forma de trabajo»), y de la revisión de código del cierre de
  cada etapa.
- La primera revisión, la del núcleo, va antes de programar el E10. Sus arreglos de «Hoy» entran antes del primer despliegue.
