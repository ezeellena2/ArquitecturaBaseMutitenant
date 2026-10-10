# E8b T05b · CRUD y Quick durable

Estado del corte: **DONE_WITH_CONCERNS**, listo para revisión independiente SPEC → QUALITY de Root. No equivale al cierre de T05, T08 ni E8b. Base/HEAD `09c159c2fa0a5ea602e105f01ca563fe9df3c514`, rama `main`, sin stage, commit ni push del implementador.

La autorización humana directa actual, respuesta a `call_59062ce8f7a7459cabac2dd8d9541d2c`, fue «Sí, continuar E8b T02–T09 y su migración». Resolvió la pausa histórica por rechazo automático; EF y pruebas posteriores se ejecutaron bajo esa aprobación. No hay bloqueo de autorización pendiente para este corte.

## Alcance entregado

- List/Get/Create/Update/Delete y feedback privados con propietario, acceso y organización reales; Platform usa tablas globales separadas. Ajeno o eliminado devuelve 404, versión anterior 409. Las escrituras públicas validan afuera y abren una sola UoW; el writer opera con locks, lecturas, reglas y escrituras dentro. Respuestas con xmin después de guardar.
- Create devuelve 201 y Location real; Quick el DTO 200 publicado mediante composición local T04a, persistiendo conversación, mensajes, resultado/Data y links privados. Quick no usa proveedor, cuota ni costo; el contador de fábrica de chat permaneció en cero en los recorridos probados.
- DurableReference neutral activo sólo para Create/Quick. El wrapper entrega Kind/Id/status/Location antes del JSON y el resolver devuelve valor tipado. El núcleo no importa Assistant, no analiza DTO privados ni captura su JSON en MemoryStream. BufferedJson conserva su captura y mapper/verifier T08a.
- Binding privado en la misma UoW del efecto. Reserva global: identidad, hash, ruta, estado y referencia técnica, con ResponseBody NULL para durable. Expiración 24h, fallo Complete, desconexión después de commit y dos hosts recuperan el snapshot original sin repetir herramientas. Body/ruta distintos 422; operación concurrente sin commit 409.
- Replay revalida cuenta, acceso, membresía, legales, notice vigente, gates y permisos antes de reserva/replay y vuelve a verificar objeto/links. Actor T05a no sustituye autorización; recuperación dentro del writer reautoriza el binding bajo lock.
- Historial ascendente, páginas de 50 y cursor ligado a propietario/acceso/organización/conversación. Links persistidos pasan whitelist, permisos vigentes y UUID propio. Caso dos organizaciones propias y una ajena: excluye la ajena y conserva links reales.
- Delete limpia contenido/mensajes/feedback/links y children técnicos propios; conserva tombstone mínimo sin contenido para 404/422 posteriores. No hay trabajo de proveedor admitido; no se afirma cancelación de worker ni purga física inexistentes.
- Baja T08 parcial con AccountDeletionContext y scope/UoW del núcleo, sin entrar ni confirmar por cuenta propia. Retira conversaciones/snapshots/bindings/tombstones de OwnerUserId exacto, incluso en organizaciones donde ya no es miembro; paso global retira notice. Baja real repetida conserva otro propietario y participante WhatsApp independiente.
- Migración incremental `20261010103933_AssistantDurableConversations`: cuatro tablas propias, RLS forzado en las dos privadas, grants mt_app y columnas técnicas neutrales globales. Up/Down revisados, sin modificar migraciones históricas. Aplicada en bases Docker efímeras; EF final indica cero cambios pendientes de modelo.

## Decisiones tomadas

1. Title null/omitido conserva título/manual. Clean HTTP convierte vacío/whitespace a null y normaliza NFC; sin Trim en servicio. Sólo rename explícito no nulo marca manual, máximo 80 y control de versión. Save/Unsave, GET y replay no renuevan LastActivity; Quick y rename relevante sí. Unsave conserva la última actividad real como referencia de retención; reaper completo pendiente.
2. Snapshot conserva DTO/Data originales. Reconstrucción usa xmin del binding insertado en el mismo commit para versiones originales de conversación/mensajes, sin adivinar txid, SaveChanges adicional ni versión actual. Probado replay exacto después de rename/feedback y expiración global.
3. Orden User → Assistant independiente de UUIDv7: bajo lock, timestamps cuantizados a microsegundos PostgreSQL avanzan desde el último persistido. Reloj fijo con 26 Quick reales conserva orden y dos páginas.
4. Quick HTTP seleccionado sin permiso usa 400 publicado `Assistant.QuickQuery.NotAvailable` (plan fila «key o permisos cambiados»), distinto de herramienta SDK 403 de la fila siguiente. `assistant.use` conserva 403, gate 404, legales 403 y notice su código 400. Tests verifican code y traceId además de status.
5. MarkUpdated garantiza UPDATE/xmin en toggle/feedback aunque coincida el valor. Se retiraron marcas nuevas redundantes de edición/feedback del modelo y de esta migración no publicada.
6. Metadata de archivo deja ledger técnico con StorageKey antes de retirarse, sin filename/bytes ni IO externo. Baja probada con metadata sin archivo físico; purga real/worker pendientes T07/T08.

Reglas neutrales actualizadas: `docs/rules/idempotencia.md` y `docs/rules/datos-personales.md`. Sin editar ficha/árbol/ignorados del arnés coordinador, Front, WhatsApp ni Identity. Role.cs ajeno conserva SHA256 LF `1286badfbb5c9c0a3c9e5178553cf1c42895c23d81a57ff0d579cfc13ae7138e`.

## Validación

Logs originales `.local/reviews/e8b-contract/t05b-logs/`; comandos, exits y hashes en manifiesto congelado. No sumar cohortes repetidas como tests únicos.

| Log | Resultado | Evidencia |
|---|---|---|
| 04 | exit 2, 1 fallido | RED válido Create: esperado 201, actual stub 501. |
| 11 | exit 0 | Generación EF tras autorización humana directa. |
| 14 | exit 0, 1/1 | Primer GREEN Create/replay/delete/diffbody, migración/RLS/grants reales. |
| 25 | exit 0, 8/8 | Snapshot/edit/expiry, reloj fijo/cursor, dos hosts, Complete failure, aborto, rollback y baja real repetida/exmiembro/ledger. |
| 30 | exit 0, 7/7 | Revocaciones permiso/member/gate/legal/cuenta, links propios y operador global. |
| 31 | exit 0, 25/25 | Durable/seguridad, aislamiento, feature, authorization y notice; proveedor cero. |
| 32 | exit 2, 81/82 | BufferedJson, OTP/LoginLink/Notice, mapper/verifier T08a, baja/WhatsApp y RLS. Único fallo: inventario esperaba seis tablas privadas en vez de ocho nuevas. |
| 33 | exit 0, 10/10 | Inventario RLS corregido/persistencia; NFC/80/manual/toggle, feedback xmin post-save/stale409 y writers ajenos 404. Corrige único fallo32 sin repetir sus81 verdes. |
| 34 | exit 0, 49/49 | Transacción, servicios, módulos, idempotencia, identidad, persistencia, configuración, capas y contrato HTTP. |
| 35 | exit 0, 0 warnings/errors | Build API focal con exportación OpenAPI normal. |
| 36 | exit 0 | EF sin cambios desde última migración. |
| 37 | exit 0, 4/4 | Dependencias de servicios y filtros privados sin bypass. |
| 38 | exit 0 | SQL incremental revisado: tablas/indexes, enable/force RLS, políticas privadas y grants. |
| 40 | exit 0 | git diff --check con configuración normal del repositorio. |

Diagnósticos separados: 01/02 sandbox; 03/05/12/15/26/27/29 errores de compilación corregidos; 08/24 filtro inválido con cero tests. 13 detectó grants faltantes corregidos antes de14. 16–18 y20–23 incluyen fixtures corregidas (DTO abstracto, sesión por host, OTP cooldown/returnUrl y status de baja). 28 esperaba por error403 para Quick seleccionado: se cotejó contrato400 antes de corregir, sin cambiar mapping publicado. 39 usó core.autocrlf=false y trató CRLF existente (incluido Role.cs ajeno) como trailing whitespace; diagnóstico de configuración, sustituido por40 con configuración normal, sin modificar Role.cs. Todos los logs fallidos se conservan y no se presentan como GREEN ni RED de conducta cuando no lo son. EF rechazados antes de ejecutar no tienen logs07/09 ni se sustituyeron por SQL manual.

## Límites y continuación

State/Send/Stop/Upload/Download/Export y usage Platform conservan pendientes honestos501 cuando corresponde; descarga ajena404. No se habilitaron SSE, loop Anthropic, claim/lease, reserva de cuota, costos/budget/reaper, storage, exportación ni retención completa. Sin prueba de proveedor Anthropic real en este corte local sin proveedor.

T05 CRUD/Quick y T08 baja mínima quedan parciales respecto de tareas completas de etapa. Suites completas/E2E quedan para puerta final de etapa; no ejecutadas aquí. OpenAPI SHA256 LF `ac450a7ca6ccfda23e5bd16f2f2fec0c20e3bec9176cfd785be9960f3919c9f4` intacto; CC01–03 publicados, CC04 pendiente. Freeze exacto es entrada de revisión, no aprobación ni commit.
