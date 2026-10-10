# E8b Back T08a: legales inmutables y aviso IA explícito

Fecha: 2026-10-10. Base y HEAD de implementación: `6d448268aba8bc1e9805313e63ef72fea1e64736`, rama `main`. Corte parcial autorizado por `.local/reviews/e8b-contract/authorization-e8b.md`. SPEC30 Approved; QUALITY30 pidió corregir Q1, reparado en el nuevo freeze31 para re-SPEC → re-QUALITY y publicación por Root. No se hicieron commits ni push.

## Resultado y alcance

El módulo contribuye una revisión demostrativa fija **v2** de Términos y Privacidad al seed existente. Mantiene la v1, sus textos y todas las aceptaciones; si un tipo ya tiene cualquier versión >=2, incluida una futura, no publica ni sustituye una revisión. Usa la UOW global del seeder, locks legales ordenados y el puerto neutral `IModuleDataSeedContributor`; el núcleo no importa Assistant. Las culturas se obtienen de referencia, también cuando están staged durante el seed inicial.

Los textos es/en describen Anthropic, consultas y adjuntos, retención prevista de 30 días configurable, Guardadas sin vencimiento por inactividad, borrado/baja, exportación de conversación Markdown/PDF y exportación general futura E10. Explicitan que son demostrativos y que retención, borrado y exportaciones aún deben implementarse/verificarse. No afirman que esas funciones estén entregadas.

`AcceptNoticeAsync` deja de ser un stub y acepta únicamente el aviso vigente confiable. La revisión inicial es **`"1"`**, independiente de la numeración legal. Se persiste una aceptación por **OwnerUserId/NoticeVersion**; Access conserva la procedencia de la primera aceptación, sin exigir otra para Consumer, Business o cada organización. El operador exclusivo puede aceptar legales y aviso sin perfil personal ni tenant ficticio (ADR0042).

La ruta publicada `POST /api/{lado}/assistant/notice` mantiene `{noticeVersion}`, `Idempotency-Key`, BufferedJson y respuesta vacía204. No agrega durable privado, rutas ni tipos. Registro, OTP, login, lectura State y aceptación legal no infieren consentimiento IA.

## Decisiones tomadas

- El entrypoint escritor sigue siendo `AssistantConversationService.AcceptNoticeAsync`: valida afuera y abre una sola UOW. `AssistantNoticeWriter` toma primero Account y después ambos locks Legal ordenados, relee legales, actividad, acceso, gates y permisos, y agrega sólo si falta la aceptación. La jerarquía entre esos dominios es **Account → Legal**, derivada del seed real (ver reparación Q1). `AssistantNoticeService` es exclusivamente lectura/check con su interfaz, policy, reader y repository propios; sirve a replay y futura admisión sin llamar a otro servicio escritor público.
- El gate legal del núcleo precede al aviso IA. En Platform, permiso403 precede al gate404 porque el controller usa InstalledModuleRoute/HasPlatformPermission y la disponibilidad también incluye permiso. En Consumer/Business, gate404 precede al acceso/permiso403. La vía Accept permite aviso pendiente; no exige haberlo aceptado antes de escribir. State permanece501 y depende T06.
- El reader publica la revisión confiable `"1"`. Una publicación posterior debe cambiar esa revisión confiable junto con el aviso publicado; el body sólo identifica la revisión que la persona acepta, nunca decide su vigencia. Una aceptación anterior y el replay204 anterior no acreditan una revisión nueva: el verifier relee la revisión vigente y requiere una nueva acción explícita. Esto se probó cambiando la revisión confiable de1 a2 en un fixture, sin cambiar el contrato.
- Los textos legales viven traducidos en `platform.LegalDocumentContents`, siguiendo la excepción escrita de `docs/rules/datos-personales.md` («Documentos legales»: texto por documento/cultura y revisiones que nunca se editan). No corresponden a resources UI; la revisión demostrativa es/en se incorpora usando el mecanismo real, igual que el seed legal existente.
- Migración incremental `20261010071558_AssistantNoticeUserVersion`: consolida duplicados de usuario/versión conservando la primera `AcceptedAtUtc`, con desempate determinista por Id; preserva fecha, Id y Access de esa fila. Crea índice único Owner/Version. Down restaura el índice anterior pero **no reconstruye los duplicados eliminados**. T02 y migraciones históricas no se reescribieron.

## Corrección neutral de replay

El RED con revocación real después del middleware detectó un defecto previo de `IdempotencyFilter`: la rama del verifier imponía400 y `ErrorTexts.Get` no resolvía textos Assistant. Root autorizó ajustar exclusivamente esa rama mediante `ProblemDetailsMapper.FromError` y `ProblemDetailsFactory` existentes. Conserva status contractual, traducción es/en, code, traceId y validaciones. Mapper y otras ramas permanecen intactos; no se modificó runtime Identity/WhatsApp.

El verifier propio vuelve a validar aceptación, revisión, legales, actividad, permisos y gates vigentes. Usa las opciones JSON MVC reales para que la normalización del body coincida con model binding; no inspecciona cuerpos privados de respuesta. Una respuesta rechazada no lleva `Idempotent-Replayed`. Las regresiones BufferedJson/Idempotency y LoginLink quedaron verdes.

## Reparación Q1 de QUALITY

QUALITY30 (`16287263f294cc33b0a2913786daee3acbadd1f005718847e9e7603abfb4d37f`) detectó una inversión real: el batch original de Notice ordenaba ordinalmente Legal → Account, mientras `DatabaseSeeder` ejecuta `PlatformSeeder.EnsurePhoneAsync` (Phone → Account) antes del contribuyente legal. El operador con Phone ya verificado conserva Account hasta el commit, y el contribuyente pide Legal incluso con v2 ya presente. Dos conexiones podían formar el ciclo seed retiene Account/espera Legal y notice retiene Legal/espera Account.

Root autorizó cambiar únicamente `AssistantNoticeRepository.LockAsync`: adquiere Account en una llamada y luego ambos legales ordenados en otra, dentro de la misma UOW; no cambia helper neutral, relecturas ni conducta de autorización. Se verificaron las adquisiciones reales: `PlatformLegalService.PublishAsync` toma sólo Legal mediante `LegalRepository.LockKindAsync`, sin Account posterior; `LegalAcceptanceWriter` toma sólo Account y relee documentos; el seed legal base no adquiere estos locks. El cruce Account/Legal queda alineado con el seed efectivo. No se introdujo una convención transversal nueva ni se modificó runtime Identity/WhatsApp.

`AssistantNoticeSeedRaceTests` prepara un operador exclusivo real con PhoneVerified y legales v2 ya presentes; ingresa por OTP/cookie/OIDC y acepta legales por HTTP. No sustituye CurrentUser, servicios, repository ni UOW. Un interceptor EF técnico pausa el seed después de adquirir Account y lo libera cuando la aceptación HTTP del mismo operador solicita Account en otra conexión. El RED en fuentes originales produjo **PostgreSQL40P01 y POST500**, no un timeout de fixture. Los timeouts sólo protegen la prueba; no hay sleeps para sincronizar. El GREEN exige que ambos flujos concluyan, HTTP204, dos backends distintos y una única aceptación global con Access Platform, preservando v2 y el teléfono verificado.

Antes de mutar se archivaron las **30 fuentes completas** y las cuatro metadata del freeze, más SPEC30/QUALITY30, en `.local/reviews/e8b-contract/t08a-before-quality/`. Índice LF `1e5e68f49f279831c28009a5484d7cacc0b960cc3c3d03348f07fd8a58aac7b2`. El nuevo delta contiene sólo repository, este informe y la prueba nueva; las otras28 fuentes permanecen byte-equivalentes bajo LF. Los31 logs/artifacts de comandos originales se mantienen sin reescribir; las corridas fallidas conservan sus resultados.

## Verificación

Evidencia completa, comandos, exits y hashes en `.local/reviews/e8b-contract/t08a-manifest.json`; fuentes exactas en `t08a-files.json` y snapshot `t08a-sources/`; referencias en `t08a-reference-files.json`; entrega en `t08a-delivery.json`. Los hashes normalizan UTF-8 a LF sin BOM.

| Cohorte | Resultado | Alcance |
|---|---|---|
| RED notice inicial | 3/3 fallaron por501 | Consentimiento explícito, versión desconocida, repetición/concurrencia antes de implementación |
| RED replay válido | 4 casos:1 verde,3 fallos | Revocación durante escritura/reserva; los tres replay exhibieron400 indebido |
| Integración Docker legal/seed/migración/notice | 30:29 verdes,1 fallo de fixture | El fixture de revisión nueva usaba auto redirect; corregido, esa prueba quedó verde en cohorte siguiente |
| Integración Docker replay/seed/notice/Idempotency/LoginLink | 47:46 verdes,1 fallo real | Permiso Platform revocado devolvía404; corregida precedencia local |
| Integración Docker autorización final | **5/5 verdes** | Cuatro revocaciones afectadas y operador exclusivo sin perfil/tenant |
| Arquitectura focal | **78/78 verdes** | 16 clases afectadas de capas, UOW, servicios, aislamiento, controllers, clasificación, persistencia y contrato |
| Builds focales finales | **0 advertencias/0 errores** | API e integración; arquitectura con dependencias |
| Migración drift y SQL Up/Down | **exit0** | Sin cambios pendientes del modelo; SQL incremental inspeccionado; DB upgraded con duplicados, unicidad y Down/Up probados |
| Q1 RED real seed × notice | **1/1 falló:40P01/HTTP500** | Operador existente, PhoneVerified, v2 presente; repositorio original archivado |
| Q1 GREEN Docker afectado | **18/18 verdes** | Carrera nueva1, notice9, autorización5 y seed3; ambos flujos concurrentes terminan y consentimiento único |
| Q1 build focal IT | **0 advertencias/0 errores** | Repository y regresión nueva con dependencias reales |

Las cohortes previas cubrieron66 casos de integración distintos y78 de arquitectura; con Q1 hay **67 casos de integración distintos**, sin sumar repeticiones. La corrida30 y47 conservan sus resultados originales; no se presentan como completamente verdes ni se borraron logs. Auth5 cerró el único defecto real pendiente de la47; esas corridas no cubrían Q1. La reparación añadió un caso de integración y repitió sólo notice/autorización/seed afectados por el orden de locks (18/18). No se repitieron migración, drift/SQL ni arquitectura78: no cambian modelo, migraciones, estructura, dependencias o contrato. El primer build Q1 falló por dos imports innecesarios IDE0005; corregidos antes del RED real, su log se conserva como fallo de compilación y no se cuenta como RED.

El baseline previo `t08a-production-seed-baseline.log` tenía7 casos,5 verdes y2 fallos por expectativa de2 eventos frente al tercero del módulo T03. Se mantuvo esa auditoría y se ajustaron expectativas semánticas de seed: evento inicial Assistant explícito,4 documentos/8 contenidos con módulos presentes y v1 original intacta. Las fixtures legales pasaron de versiones1→2/3 a2→3/4, conservando su prueba de vigencia, publicación concurrente e inmutabilidad; los fallos de fixtures no se atribuyeron a consentimiento.

Intentos excluidos como RED válido: build restringido con fallo ambiental silencioso; decoración inicial errónea de IIdempotencyStore (ImplementationFactory); filtro clase+método que seleccionó0 pruebas; nombre de migración anterior inexistente. Fallos de compilación de implementación/fixture corregidos permanecen en logs. El manifest distingue estos intentos de los RED de conducta.

OpenAPI publicado mantiene SHA256 LF `ac450a7ca6ccfda23e5bd16f2f2fec0c20e3bec9176cfd785be9960f3919c9f4`. Role.cs extranjero conserva LF `1286badfbb5c9c0a3c9e5178553cf1c42895c23d81a57ff0d579cfc13ae7138e` y numstat0. `git diff --check` exit0. No se editaron Front, puertos publicados, tipos, CC04 ni documentos del arnés. Se preservaron las evidencias y recibo T04a.

## Límites pendientes

T08 completo y E8/E8b permanecen abiertos. Faltan retención configurable efectiva, índice técnico de retención, cancelación/borrado/baja y participantes de cuenta, ledger físico/file cleanup con retries, workers y pruebas de esos flujos. La limpieza final de aceptaciones aviso durante baja pertenece a ese próximo corte, no está entregada aquí. Faltan T06 para State completo/cuotas/costos, T07 para exportación de conversación y E10 para exportación general. No se agregó exportport ni participante sin consumidor.

No se implementó generación, Quick HTTP ni se llamó a un proveedor real. No se ejecutaron solución/suites completas, E2E ni cierre de etapa; corresponden a la puerta final de integración. Los textos siguen siendo demostrativos y deben publicarse nuevas revisiones definitivas adaptadas al negocio antes del uso público.
