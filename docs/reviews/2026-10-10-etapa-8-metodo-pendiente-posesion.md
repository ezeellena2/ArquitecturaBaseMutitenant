# E8 B8 · reclamar métodos pendientes después de probar posesión

Sobre `7f95e5b0f2ff7c444816b3112292ed3b6f2e19dd`, agregar Email o Phone conserva las reservas pendientes de otras cuentas. Una verificación correcta reclama todos los pendientes ajenos del mismo tipo y valor después de completar las reglas del vínculo. La reemisión propia conserva su comportamiento y el destino ajeno ya verificado sigue rechazándose antes de emitir.

## Decisiones tomadas

- Se retiró únicamente el borrado ajeno de `LoginMethodIssuer.AddAsync`. La limpieza vive en `LoginMethodLinker.VerifyAsync`, después de propiedad, dominio y principal; no queda antes de un `Result` fallido que pudiera persistirse con `OnAnyResult`.
- La búsqueda global exacta pertenece a `IUserLookup`/`Infrastructure.Identity.UserLookup`: proyecta todos los pendientes, sin contacto Email de Google. `IdentityStorageAccessTests` impide colocar esa comparación global en un repositorio. Se usa `GetByIdAsync` tracked existente y se revalida tipo, valor, propietario ajeno y `VerifiedAtUtc == null`, tolerando resultados desaparecidos o desactualizados. No se agregó excepción a las guardas ni `IgnoreQueryFilters`.
- El costo es una consulta de proyección por destino y hasta una recuperación por cada candidato ajeno. Está acotado al conjunto de reservas de ese destino, pero no tiene límite numérico nuevo ni es una optimización en lote. El lock de identidad y la UoW existentes siguen perteneciendo al llamador.
- La limpieza solo corre en la transición a verificado de Email/Phone. Conserva los métodos propios y verificados, el principal previo y una llamada al observador Phone. Observador y aviso continúan dentro de la transacción original; una excepción revierte reservas, vínculo, contactos, eventos, outbox y consumo del código.
- Se conservó la carrera Email con `usable=false`/`Invalid` y el rechazo Phone actual con `errors.phone`. El comentario heredado se volvió factual; ADR 0043 sigue reemplazando ADR 0041.
- Los fixtures nuevos usan PostgreSQL Docker y reloj privados. Avanzan 61 segundos entre emisiones para respetar el cooldown global de 60 segundos y mantener vigentes las pruebas anteriores. No se modifican fechas ni `xmin` por SQL. Los códigos reales se leen del outbox cifrado después del commit, con propietario, cursor, canal y destino; no se imprimen.
- La composición Phone permanece en `Modules.WhatsApp`. `PickupCodeReader` puede despachar todos los senders aun con `Testing`; los hosts afectados retiran el sender externo y usan `ForbiddenSender`, que falla visiblemente ante cualquier intento. Se preservan disponibilidad, capacidades, cuota y outbox cifrado. No se modifica el adaptador productor ni las pruebas Cloud.
- Se actualizaron las expectativas antiguas de retiro al emitir. Una prueba WhatsApp que establece propiedad mediante el propio Linker ahora exige retiro posterior a posesión y seguimiento 404, conservando el dueño verificado y la ausencia de aviso al dueño existente. Los estados históricos con verified + pending se verifican en casos independientes que mantienen el rechazo vigente.

## Resultados

Los comandos completos, salidas originales, duración de pared, conteos y SHA-256 están en `.local/reviews/e8b-contract/e8-pending-method-possession-files.json`; los logs están en `.local/reviews/e8b-contract/b8-evidence/`. No se suman ejecuciones superpuestas.

| Ejecución | Casos originales | Exit | Resultado |
|---|---:|---:|---|
| RED HTTP real + PostgreSQL | 2; 0 correctos, 2 fallidos | 2 | Email y Phone: se esperaba coexistencia de 3 pendientes y había 1. |
| RED unitario Linker | 7; 5 correctos, 2 fallidos | 2 | Faltaba reclamar todos los pendientes después de posesión, para ambos tipos. |
| GREEN HTTP intermedio | 15; 15 correctos | 0 | Histórico; anterior a la decisión final de lookup. No se usa como aceptación final. |
| HTTP final | 15; 15 correctos, 0 omitidos | 0 | Email 7 y Phone 8: coexistencia A/B/C, código incorrecto, vencimiento, reemisión propia, país retirado, verified owner, principal, observador, carrera y rollback. |
| Unitarios finales | 24; 24 correctos, 0 omitidos | 0 | Linker 8 y InvitationService 16, con proyección stale/null y revalidación tracked. |
| Arquitectura final | 31; 31 correctos, 0 omitidos | 0 | Acceso global Identity, UoW, filtros, DI, dependencias y servicios. |
| Cohorte de regresiones | 109; 108 correctos, 1 fallido, 0 omitidos | 2 | Único fallo: expectativa WhatsApp antigua de 400 tras posesión que ya había retirado la reserva; se corrigió a 404 y se reejecutó su clase. |
| Regresión WhatsApp corregida | 27; 27 correctos, 0 omitidos | 0 | Clase modificada verde; reemplaza sus resultados anteriores y no se suma a la cohorte. |

La cohorte cubre LoginMethods, AccountMethodsHttp, LoginCodeConcurrency, IdentityConsolidation, WhatsAppIdentity, WhatsAppPhoneLinkObserver, Signup, AccountRecovery, PlatformRecovery, WhatsAppRecovery, AccountNotice, LoginPersistence, ServiceProviderValidation y UnitOfWork. Las regresiones de Signup y recuperaciones ejercitan los llamadores compartidos del Linker.

La aceptación de esas regresiones conserva 82 casos correctos de las clases sin cambios y reemplaza los 27 de WhatsAppIdentity por su ejecución final correcta: 109 casos distintos, sin afirmar una segunda ejecución de toda la cohorte. Los 15 nuevos de posesión son otro conjunto; unitarios (24) y arquitectura (31) se informan en sus proyectos separados.

Los builds focales de integración/API/Application, unitarios y arquitectura terminaron con 0 advertencias y 0 errores. La última compilación de integración posterior al ajuste de expectativa también pasó. Los intentos de tooling con 0 casos se conservan y no cuentan como RED: MSBuild paralelo falló sin diagnóstico de código; el runner dentro del sandbox falló por NamedPipe/IPC. La ejecución real con Docker se hizo con elevación y builds seriales `-m:1 -nr:false`. Un build intermedio detectó `IDE0005` y se corrigió antes de los builds finales.

## Límites y entrega

Este correctivo no cierra E8 ni reemplaza la puerta final. No ejecutó suites completas, Aspire, E2E, galería, Meta ni retiro físico de módulos. Los builds que exportaron OpenAPI produjeron un artefacto generado conservado como evidencia; después de todos los builds se restaura el blob exacto del contrato congelado. La exportación de un build registró una restricción de acceso al keyring de DataProtection en el sandbox aunque la compilación terminó correctamente; no es evidencia de funcionamiento externo del proveedor.

Contrato protegido: SHA-256 `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`. `Role.cs` ajeno protegido: SHA-256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. No se incluye ninguno como fuente propia. Owner entrega fuentes inmóviles sin stage, commit ni push; Root audita y publica.
