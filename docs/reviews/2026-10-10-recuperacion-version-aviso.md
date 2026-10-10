# Recuperación — versión del pedido y aviso diferido

Auditoría de lectura, sin cambios de producto, runtime Meta ni guarda de concurrencia. Integración reportó un rechazo 409 mientras el aviso AccountNotified estaba en curso; cancelar, recargar y rechazar con la versión actual pasó, sin otro actor manual.

## Hallazgo verificable

El worker puede volver vieja la versión del formulario aunque no intervenga otro operador:

1. AccountRecoveryNoticeWorker ejecuta NotifyAccountsAsync cada 30 segundos.
2. AccountRecoveryNoticeService toma pedidos sin AccountNotifiedAtUtc bajo lock y llama MarkAccountNotified dentro de su UoW.
3. MarkAccountNotified escribe AccountNotifiedAtUtc en la misma fila AccountRecoveryRequests.
4. AccountRecoveryRequest implementa IVersioned. VersionedConvention mapea Version a xmin/IsRowVersion; cada UPDATE de PostgreSQL cambia ese token.
5. PlatformRecoveryDecisionService relee el pedido bajo lock y compara su versión con request.Version antes de decidir. Si el aviso se confirmó entre la lectura y la acción, devuelve General.ConcurrencyConflict. Recargar permite decidir con la nueva versión, siempre que el estado aún lo permita.

Los cinco archivos de este recorrido tienen blobs idénticos entre el snapshot Meta Back `16662d87fbd2c93696a286a8b428d26b7d70d996` y el código actual auditado. El hallazgo no depende de T02 ni CC01.

## Alcance y conducta prevista

La recarga observada coincide con el mecanismo actual y la ficha de concurrencia: el formulario conserva la versión con la que empezó y no toma silenciosamente una relectura de fondo para guardar. El mensaje «Otra persona cambió esto» no demuestra otro actor manual; un worker también escribe la fila.

La lectura de fuentes confirma el mecanismo posible. Para atribuir **ese intento concreto** al aviso, Integración debe conservar la versión leída, la versión vigente tras el aviso y AccountNotifiedAtUtc antes/después, sin valores de contacto ni códigos. Esta auditoría no consultó la BD/los secretos del entorno Meta ni ejecutó otro recorrido o test Docker mientras T02 estaba en verificación.

No se relajó la comparación de versión. Evitar que un cambio técnico de aviso invalide la decisión requiere una decisión adicional sobre el límite de concurrencia o el almacenamiento del aviso; no se aplica unilateralmente. La regla actual declara ProfileVersion como su única versión manual excepcional.

## Fuentes locales

- `Infrastructure/BackgroundJobs/AccountRecoveryNoticeWorker.cs` y `Application/Services/Auth/AccountRecoveryNoticeService.cs`.
- `Domain/Platform/AccountRecoveryRequest.cs`, `Infrastructure/Persistence/Conventions/VersionedConvention.cs` y `Persistence/Repositories/AccountRecoveryRepository.cs`.
- `Application/Services/Platform/PlatformRecoveryDecisionService.cs`.
- `docs/rules/concurrencia.md` y fichas Identidad/Plataforma.
