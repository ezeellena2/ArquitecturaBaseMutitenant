Servicio de entrega del outbox: `OutboxDispatchService` es el único límite `IUnitOfWork`; la selección con `SKIP LOCKED` vive detrás de `IOutboxDispatchStore`.
Leé: [guardado](../../../../docs/rules/guardado.md) · [logs](../../../../docs/rules/logs.md).
Claim y Complete tienen una UoW cada uno. El worker confirma el lease durable antes de descifrar/enviar sin transacción y usa otro scope para confirmar el receipt. Sent, Cancelled y falla terminal limpian el payload; leases abandonados consumen intentos. Verificá `OutboxDispatcherTests`, `OutboxMessageTests` y `TransactionBoundaryTests`.
