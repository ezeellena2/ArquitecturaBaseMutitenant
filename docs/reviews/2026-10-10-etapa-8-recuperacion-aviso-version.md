# E8 N1 · El aviso de recuperación conserva la versión del pedido

La decisión humana del 2026-10-10 reemplaza la conducta anterior: leer un pedido Pending, ejecutar su aviso automático y aprobarlo o rechazarlo con esa misma versión funciona. Dos decisiones humanas con la misma versión siguen produciendo un solo efecto y un 409 `General.ConcurrencyConflict`.

## Decisiones tomadas

- `AccountRecoveryRequest` conserva `IVersioned` y su `xmin`. Se retiró su columna técnica `AccountNotifiedAtUtc`; no se introdujo un contador manual ni se modificó el contrato HTTP.
- La marca vive en `platform.AccountRecoveryNotices`: entidad global explícita, sin tenant ni RLS, con PK y FK al pedido y fecha UTC. Se borra en cascada cuando se elimina el pedido, también en la baja de la cuenta.
- El worker conserva una UoW `OnSuccess`, tanda de 50 y lock `FOR UPDATE SKIP LOCKED` del pedido. Encola el aviso y agrega la marca en esa misma transacción, sin actualizar la fila del pedido. Sin `UserId`, sólo agrega la marca.
- Después de tomar los locks, el repositorio relee las marcas en una consulta por tanda. En READ COMMITTED otra tanda puede confirmar su marca después del snapshot inicial, sin editar el pedido; esa relectura evita preparar nuevamente el aviso.
- `20261010154933_AccountRecoveryNoticeReceipt` copia las fechas ya notificadas antes de retirar la columna, conserva los pendientes y otorga los grants de runtime. Down restaura las fechas desde la tabla técnica antes de borrarla: volver a subir no reenvía avisos históricos.

## Verificación

- Rojo dirigido: ambos casos de la prueba HTTP nueva fallaron con la implementación anterior porque el aviso cambiaba `xmin`.
- Verde: `PlatformRecoveryTests`, `AccountRecoveryTests` y `AccountRecoveryNoticeMigrationTests`, **17/17**, sin errores ni omitidos, PostgreSQL Docker y HTTP reales. Incluye aprobación/rechazo con versión previa al aviso; decisiones humanas simultáneas iguales y contradictorias; aviso posterior a decisión terminal; dos workers y repetición sin duplicados; rollback efectivo de marca y outbox juntos, con reintento único; y pedido histórico sin cuenta.
- Los ajustes finales de fixtures pasaron aparte, **1/1 cada uno**: el caso sin cuenta aísla el aviso del pedido propio; la migración comprueba individualmente los cuatro grants y que el backfill no modifica el `xmin` de los pedidos.
- Builds seriales de API/Infrastructure y del proyecto de integración: **0 advertencias, 0 errores**, con analyzers habilitados.
- La prueba de migración usa una base efímera propia: vacía y con pedidos ya avisados y pendientes, Up/Down/Up, runtime real, clasificación global, FK/cascada y **sin cambios de modelo pendientes**. No modifica bases de desarrollo.
- Guardas del modelo pertinentes a la tabla nueva: `DataClassificationTests` y `EntityConfigurationTests`, **6/6**, sin omitidos. SQL idempotente incremental generado y revisado, con backfill antes de retirar la columna y grants/FK correctos.
- OpenAPI generado por el build permanece idéntico: SHA256 `fb006c3a326231e09676970a075419cd68969877372b1af4b4352f6ce66da077`. `version` sigue siendo `uint` obligatorio; no requiere regenerar tipos del front.

## Alcance y límites

Este corte implementa únicamente N1. Las decisiones, permisos, estados de cuenta, prueba de posesión y límites transaccionales mantienen sus guardas. La migración debe aplicarse antes de iniciar el runtime actualizado; el código anterior usa la columna retirada. La puerta completa de E8, las revisiones finales y el recorrido real pertenecen al cierre de la etapa, no a este arreglo focal. Evidencia local y lista propia: `.local/reviews/e8b-contract/n1-*` y `e8-recovery-notice-version-files.json`.
