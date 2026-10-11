# E8b M3: contenido durable y copias personales

M3 está implementado y verificado con controles focales. No completa T08 ni cierra E8/E8b. No modifica Front, el contrato HTTP ni el retiro del módulo de T09.

## Decisiones tomadas

El contenido del replay vence a las 24 horas desde la creación original, también en conversaciones guardadas. Renombrar, guardar o dar feedback no renueva el plazo. Por decisión del usuario, el vínculo técnico owner/access/organización/key/body/route se conserva sin contenido hasta la baja de la cuenta, para impedir repetir un efecto confirmado. Cambiar cuerpo o ruta conserva 422 incluso después del vencimiento; la operación sin contenido devuelve 404.

Un original confirmado con copias de una cuenta cuya baja empezó se deniega y se purga completo. No se transforma para continuar siendo legible. El estado neutral de baja considera lease incluso vencido, identidad eliminada o ausente; una baja pendiente en gracia sin lease todavía no empezó. Replay revalida autorización y contenido, consulta privacidad sobre los sujetos de su última lectura durable y evalúa TTL después de esa espera, sin abrir UoW. Esto no ofrece atomicidad de los bytes HTTP frente a una baja posterior a la decisión final.

Quick prepara una sola composición fuera de UoW. El Writer bloquea las identidades conocidas ordenadas antes de binding/conversación, revalida autorización y versión, y protege únicamente los nombres tipados de una composición nueva. Los sidecars internos provienen de UserId/ActorId reales y no se serializan; no usan MemberId, RecoveryId ni TargetAccountId como identidad. Un ganador concurrente con sujetos no preparados devuelve el 409 `Request.InProgress` existente, para repetir el preflight sin ejecutar otro efecto ni tomar shares tardíos.

## Persistencia y limpieza

Las dos variantes durables guardan sujetos nominales y un ancla interna de versión del snapshot. El índice global del módulo contiene sólo Id, OperationId, ScopeTenantId y expiración. La baja elimina los vínculos propios y purga el contenido de terceros conservando sus vínculos. Todos los caminos de purga bloquean índices ordenados antes de filas durables; el participante de baja no toma shares de otras identidades.

El worker drena páginas de 100 con cutoff fijo y cursor `(ExpiresAtUtc, Id)`. Avanza aunque una página esté bloqueada y reinicia desde el principio cada cinco minutos para reintentar. Entra al scope antes de UoW y funciona con los gates apagados. La lectura vence exactamente a las 24 h; la eliminación física es eventual y un lock persistente puede demorar una fila.

La migración CLI `20261011141438_AssistantSnapshotRetention`, con Designer y ModelSnapshot de EF 10.0.12, tiene Up/Down revisados. Reconstruye sujetos mediante QuickKey, variantes tipadas y relaciones reales bajo RLS; captura el xmin original antes de modificar metadatos. Purga contenido vencido, comprometido o irreconstruible, conservando el vínculo. Down no reconstruye contenido purgado. El modelo no tiene cambios pendientes; su aplicación a ambientes corresponde a Integración.

## Verificación

| Control | Estado |
|---|---|
| Builds focales | Hecho: API.IntegrationTests, ArchitectureTests, Domain.UnitTests y Application.UnitTests, sin advertencias ni errores. |
| TTL y privacidad unitarios | Hecho: 14 casos verdes; RED dirigido del TTL reproducido. |
| Carreras de Claim | Hecho: 8/8; RED/GREEN de última lectura Business/Platform, composición y shares, lease vigente y vencido. |
| Retención y backlog | Hecho: 5/5, gates OFF, rollback/retry, dos workers y 203 registros con primera página bloqueada. |
| Replay general | Hecho: 7/7 con el orden final de privacidad. |
| Bajas, reader neutral y upgrade legacy | Hecho: 9/9; upgrade de diez Quick en conversación nueva/existente y 22 replay por Writer.Existing. |
| Seguridad y aislamiento | Hecho: 36/36 en Docker, durable/transporte/autorización/baja/historial/RLS/actores. |
| Arquitectura, Domain y Quick | Hecho: arquitectura 67/67 y unitarios Domain/Quick 9/9; especificación y calidad aprobadas. |
| Documentación | Hecho: detector Back 0 auto, 0 modelo y 0 humano, 0 tokens. |

Los comandos, fallos de preparación, RED/GREEN, SQL, hashes y manifiesto de rutas están en `.local/reviews/e8b-retention-20261011/`. No se cuentan filtros de cero tests ni una corrida con DLL anterior. Los controles suman 65 casos de integración en Docker, 67 de arquitectura y 23 unitarios afectados, sin omitidos. La puerta completa de E8b sigue pendiente. El paquete se commitea en main después de verificarlo, sin push.

## Cambios al contrato

Ninguno. Se reutilizan 404, 422 y 409 publicados. Docs y obj OpenAPI son idénticos: SHA256 `96e0f10b7aefdf085ea8ceca0cd2bf57e83261e9eb452ba4f5ad379522575a45`. Las proyecciones internas no aparecen en HTTP/SDK. Los textos del resumen Business de Uso se coordinan con Front conforme al contrato existente, sin CC07.

## Límites pendientes

Proveedor/turnos, State/Uso que conservan 501, archivos/export, retención ordinaria de conversaciones y anonimización financiera siguen fuera de M3. Guardar una conversación no la exime del replay de 24 h ni de la baja de cuentas. La integral Front/Back, la puerta y el recorrido Meta real corresponden a Integración.
