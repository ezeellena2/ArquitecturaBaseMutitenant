# Recuperación — versión del pedido y aviso diferido

Auditoría de lectura, sin cambios de producto, runtime Meta ni guarda de concurrencia. Integración registró un rechazo 409; cancelar, recargar y rechazar con la versión actual pasó, sin otro actor manual reportado. No se conservaron las versiones ni la marca del aviso antes/después de ese intento, por lo que su causa no está atribuida.

## Hallazgo verificable

El worker puede volver vieja la versión del formulario aunque no intervenga otro operador:

1. AccountRecoveryNoticeWorker ejecuta NotifyAccountsAsync cada 30 segundos.
2. AccountRecoveryNoticeService toma pedidos sin AccountNotifiedAtUtc bajo lock y llama MarkAccountNotified dentro de su UoW.
3. MarkAccountNotified escribe AccountNotifiedAtUtc en la misma fila AccountRecoveryRequests.
4. AccountRecoveryRequest implementa IVersioned. VersionedConvention mapea Version a xmin/IsRowVersion; cada UPDATE de PostgreSQL cambia ese token.
5. PlatformRecoveryDecisionService relee el pedido bajo lock y compara su versión con request.Version antes de decidir. Si el aviso se confirmó entre la lectura y la acción, devuelve General.ConcurrencyConflict. Recargar permite decidir con la nueva versión, siempre que el estado aún lo permita.

Los cinco archivos de este recorrido tienen blobs idénticos entre el snapshot Meta Back `16662d87fbd2c93696a286a8b428d26b7d70d996` y el código actual auditado. El hallazgo no depende de T02 ni CC01.

## Alcance y conducta prevista

La recarga observada es compatible con el mecanismo actual y la regla general de concurrencia: el formulario conserva la versión con la que empezó y no toma silenciosamente una relectura de fondo para guardar. La ficha contiene una premisa incorrecta al describir el pedido de recuperación como una fila que sólo cambia por acciones del operador: el worker también la modifica, como documenta la ficha de Plataforma. Esa salvedad no elimina la exigencia expresa de versión del contrato de decisión. El mensaje «Otra persona cambió esto» no demuestra otro actor manual; un worker también escribe la fila.

La lectura de fuentes confirma el mecanismo posible. Para atribuir **ese intento concreto** al aviso, Integración debe conservar la versión leída, la versión vigente tras el aviso y AccountNotifiedAtUtc antes/después, sin valores de contacto ni códigos. Esta auditoría no consultó la BD/los secretos del entorno Meta ni ejecutó otro recorrido mientras T02 estaba en verificación.

El selector de avisos no filtra el estado del pedido; también puede escribir un pedido terminal cuyo aviso siga pendiente. Por eso el token terminal puede quedar viejo tras otra escritura del worker. La repetición con versión vigente conserva el error de estado previsto; no hay una promesa de inmutabilidad absoluta del token terminal.

No se relajó la comparación de versión. Evitar que un cambio técnico de aviso invalide la decisión requiere una decisión adicional sobre el límite de concurrencia o el almacenamiento del aviso; no se aplica unilateralmente. La regla actual declara ProfileVersion como su única versión manual excepcional.

## Verificación dirigida posterior

Integración ejecutó `PlatformRecoveryTests` por HTTP contra PostgreSQL Docker en una copia aislada del corte Back `62879d7bce49bcad4a51c582455a77e9b5c9e9c8`, con binarios propios. El pase fue **6/6, exit 0, sin fallos ni omitidos**, en 46,158 segundos. Incluye dos casos nuevos, aprobar y rechazar, commiteados en `630cbbee1b507fcf665fd999baa81b032330091e`; no modifica código productivo, guarda, contrato ni runtime Meta.

Ambos casos recorren Email previo → Email nuevo: lectura HTTP Pending/v0 → worker real que marca el aviso y persiste v1 distinto → decisión con v0 que devuelve `409 General.ConcurrencyConflict`, conservando estado, métodos, eventos y outbox → recarga HTTP/v1 → decisión exitosa con versión nueva y un aviso al correo nuevo. El contenido del archivo de prueba coincide con el recibo local (SHA-256 de bytes `f739472d71b34f6a1a1d47347f45f42078768bff9f591ecd8b533b4d9367fd75`; normalizado LF `7671c3123e3fe6acb1dbc4154cd9483114a57f36b3aea1b33323fdf69c3b9f8b`). Evidencia: `.local/reviews/2026-10-10-e8-recuperacion-409-recibo.json`.

Este pase demuestra el mecanismo actual de concurrencia de fila para Email. **No atribuye el 409 histórico de Meta ni reproduce la variante Phone previo → Email nuevo**. Tampoco agrega al caso nuevo la segunda pasada del worker ni el rechazo terminal repetido: esos controles permanecen en pruebas existentes y no se declaran cubiertos por esta intercalación nueva. Las suites completas E8 previas y los focales E8b se informan por separado.

## Fuentes locales

- `Infrastructure/BackgroundJobs/AccountRecoveryNoticeWorker.cs` y `Application/Services/Auth/AccountRecoveryNoticeService.cs`.
- `Domain/Platform/AccountRecoveryRequest.cs`, `Infrastructure/Persistence/Conventions/VersionedConvention.cs` y `Persistence/Repositories/AccountRecoveryRepository.cs`.
- `Application/Services/Platform/PlatformRecoveryDecisionService.cs`.
- `docs/rules/concurrencia.md` y fichas Identidad/Plataforma.
