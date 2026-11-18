# ADR 0060 · Dos versiones conviven en cada despliegue

Estado: aceptada técnica el 2026-11-18 (traspaso 15; toma la regla de versiones de ArquitecturaBase, N27 del núcleo y H25 de
WhatsApp).

## Contexto

En cada despliegue, el migration bundle corre antes que la imagen nueva, y la revisión vieja de Container Apps atiende hasta
que la nueva pasa readiness. Durante ese rato, y también al volver a la imagen anterior, dos versiones comparten la base, la
cola de salidas y Redis. Al volver atrás, además, la versión vieja lee lo que escribió la nueva.

En el multitenant pesa más que en ArquitecturaBase:

- los enums se guardan como texto ([persistencia-ef](../rules/persistencia-ef.md)), y EF lanza al leer un nombre que no
  conoce;
- una sola copia vieja atiende a todas las organizaciones, así que lo que no puede leer falla para todas.

## Decisión

- **El esquema tolera las dos versiones.** Las reglas están en [migración](../guides/migracion.md#convivencia-de-versiones):
  - primero se agrega y lo viejo se borra en el despliegue siguiente;
  - un CHECK nuevo sobre una tabla que ya existía va en el despliegue siguiente;
  - una columna que pasa a aceptar nulos se tolera antes de escribirse;
  - un renombre es un agregado y un borrado.
- **Un valor nuevo se lee en un despliegue y se escribe en el siguiente:** un campo, un tipo de mensaje o un valor de un enum
  guardado como texto. Un enum guardado por nombre lee un nombre desconocido como su valor neutro, cuando lo tiene; si no lo
  tiene, la regla de los dos despliegues es la única protección.
- **Las colas no pierden lo que una copia no entiende.** Lo que una copia no puede leer no se borra ni se marca como fallido:
  vuelve a la cola con una espera y un tope de intentos. La reserva toma solo los canales y los tipos que su versión conoce.
  El outbox ya reserva solo los canales registrados (`OutboxDispatchStore`).
- **Lo que la app vuelve a abrir** (un JSON guardado, un comprobante sellado) ignora los miembros que no conoce.
- **Volver a la imagen anterior** se hace solo con un esquema compatible; las migraciones no se revierten automáticamente
  ([manual de operación](../operations/runbook.md)).

## Consecuencias

- La regla vale para el núcleo y para cada módulo, desde el primer despliegue (E10).
- Lo que pide código va como tareas del [plan del E10](../plans/2026-10-16-etapa-10-despliegue.md):
  - `MigrationCoexistenceTests`, que revisa las migraciones posteriores a la última aplicada;
  - la lista de enums guardados como texto;
  - el conversor que lee un nombre desconocido como el valor neutro;
  - que el outbox devuelva a la cola, con espera, lo que su versión no puede leer.
- Una excepción (un valor que se lee y se escribe en la misma versión) se anota en la migración y en el control, con su motivo
  y con lo que pasaría al volver a la imagen anterior.
- «Nadie da lo que no tiene», la otra regla de plataforma de ese traspaso, la decide el núcleo aparte.
