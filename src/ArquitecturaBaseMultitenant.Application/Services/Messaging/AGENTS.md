Servicio de entrega del outbox: `OutboxDispatchService` es el único límite `IUnitOfWork`; la selección con `SKIP LOCKED` vive detrás de `IOutboxDispatchStore`.
Leé: [guardado](../../../../docs/rules/guardado.md) · [logs](../../../../docs/rules/logs.md).
El envío se mantiene bajo el lock hasta que UnitOfWork guarda Sent o Failed; verificá `OutboxDispatcherTests` y `TransactionBoundaryTests`.
