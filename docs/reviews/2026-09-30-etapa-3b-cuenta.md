# Etapa 3b · La cuenta — informe de ejecución

Estado: en ejecución. La 3c queda pendiente. No declarar cerrada esta etapa sin puerta completa y E2E real verde.

## Tareas y commits

| Tarea | Commit back | Commit front | Evidencia |
|---|---|---|---|
| Plan previo | `c60cd18` | — | 30 tareas, TDD, alcance y matriz visual |
| T01 · recorridos reales rojos | — | `20e2e8b` | Los cinco fallaron antes de implementar /cuenta |
| T02 · reglas de métodos | `61ffe6c` | — | Rojo CS1501/CS1061; verde LoginMethodTests 6/6 |
| T03 · comprobante de reautenticación | `414cb47` | — | Rojo CS0103; verde ReauthTicketTests 4/4 |
| T04 · persistencia de cuenta | este commit | — | Rojo CS1061; Testcontainers LoginMethodsTests 3/3; modelo sin cambios pendientes; SQL revisado |

## Evidencia del E2E real antes de programar

`npm run test:e2e:real`, 30/09/2026. AppHost y PostgreSQL efímero exclusivos; datos del preparador y de /registro en esa base. Registro/puerta empresa/F5/cambio Personal/logout de 3a se recorrieron antes de los casos nuevos.

```text
✖ 3b: sumar correo personal con código leído del .eml (18744.9227ms)
✖ 3b: quitar un método con código enviado a otro (19940.3442ms)
✖ 3b: pedir baja y cancelarla ingresando durante la gracia (19921.7423ms)
✖ 3b: cambiar el idioma a en-US desde /cuenta (20112.2356ms)
✖ 3b: aceptar versión nueva de términos que bloquea el ingreso (19950.0975ms)
Error en cada caso: La cuenta 3b: /cuenta no muestra la pantalla Mi cuenta.
tests 6 · pass 0 · fail 6 · cancelled 0 · skipped 0 · duration_ms 140946.0782
```

La sexta prueba es el padre, que falla porque sus cinco subrecorridos fallan. Se mantienen activas: el resultado final debe ser verde en todos. El runner cerró su AppHost y eliminó el pickup propio al terminar.

## Puerta

Pendiente de la implementación.

## Diferencias con el lienzo

Ocultaciones solicitadas: WhatsApp (E8), exportación/sugerencia de exportar (E10) y bloqueo del único Dueño (E4). Comparación visual todavía pendiente.

## Decisiones tomadas

- Ejecución directa en main, en este chat, según autorización expresa. Revisión del plan sin subagentes.
- Google conserva subject como clave y suma contacto Email verificado para visualización y avisos; la dirección no reemplaza el subject.
- La nueva versión legal del E2E la publica su preparador en la base propia por comando de archivo. No se agrega endpoint productivo ni se toca Development.
- La limpieza de una cuenta real respeta la gracia: pedir baja impide su ingreso normal y el worker anonimiza al vencer; no se borra Identity ni auditoría a mano.
- HarnessStage sigue en 2 hasta cerrar 3c; solo se actualizan inventarios existentes exigidos y se agrega el contrato cruzado funcional.
- T03 entrega entidad y puerto; los helpers de emisión/verificación se conectan en T07 después de la persistencia T04, para probar el flujo completo con sus códigos y locks reales.
- T04: índice parcial único de principal. Como PostgreSQL lo comprueba al emitir cada UPDATE, el repositorio retira la marca anterior dentro de la UoW antes de fijar la nueva; la fila no es auditable ni soft-deletable y rollback revierte todo. `xmin` usa la columna de sistema (el SQL de Npgsql no crea una columna manual).

## Recorrido manual

Pendiente de rutas y contratos finales.
