# Etapa 8b: el asistente — trabajo pendiente

| Tarea | Estado |
|---|---|
| T00–T01 · Decisiones y contrato | Hecha. |
| T02–T04 · Instalación, configuración, catálogo y consultas fijas | Hecha; verificación integral en T15. |
| T05 · Runtime durable y SSE | Pendiente de terminar, verificar y commitear el trabajo local. |
| T06 · Continuaciones financieras y título | State y Uso hechos; pendiente conectarlos al runtime, título y alertas. |
| T07–T08 · Adjuntos, exportación, retención y baja | Pendiente completar las continuaciones del runtime y eliminar los 501. |
| T09 · Guardas y receta de quitar | Pendiente, incluidos los siete fallos del retiro. |
| T10–T13 · Front | Hecha la implementación; pendiente integración y puerta. |
| T14 · Integración con API real y FakeChatClient | Pendiente ejecutar y resolver desajustes. |
| T15 · Puerta | Pendiente. |
| Anthropic real | Pendiente del usuario: cargar su clave y realizar la prueba externa. |

Lo hecho se consulta en git; este plan no reproduce implementaciones ni corridas anteriores. Back y Front trabajan en paralelo sobre main, cada uno sólo en su repo. Commits coherentes verificados, en español y con pathspec; sin push. Preservar cambios y procesos ajenos. No crear worktrees fuera de la puerta. Sólo se consulta al usuario por producto; las decisiones técnicas las toma su dueño y las comunica al coordinador. Una revisión de código al cierre, sin revisores por tarea o arreglo.

## Contrato y límites vigentes

El contrato ya publicado es [openapi.json](../contracts/openapi.json); Front usa `src/shared/api/generated/schema.d.ts`. No se vuelve a publicar una parte 1 ni se copian aquí sus DTO. Las rutas y SSE se consultan también en [asistente](../features/asistente.md). Un cambio técnico necesario se coordina antes de regenerar ambos repos y se registra en «Cambios al contrato»; sólo los cambios de producto requieren consulta.

Rigen D1–D26 de [ADR 0048](../decisions/0048-asistente.md), las precisiones aceptadas de [0049](../decisions/0049-precisiones-whatsapp-y-asistente.md), las ADR posteriores de su área y los acuerdos vigentes: turnos continúan al cambiar conversación/pestaña; RequestAborted cierra sólo transporte; Detener solicita cancelación durable. Catálogo técnico editable con Avanzado plegado. State y Uso leen períodos financieros persistidos; al cambiar zona el período abierto queda intacto y el siguiente comienza en su cierre, sin presupuesto adicional. El resumen Business puede incluir ambas financiaciones y no afirma una clave única ni un límite global fijo de 50; cuota personal sólo con dailyUsage/dailyLimit. No ampliar E11: «Mis mensajes con empresas» conserva el vacío aprobado mientras no tenga fuente productiva.

Tableros: base v116, catálogo avanzado completo v118, resumen desktop Asistente-Uso-Org v119; su móvil conserva el tablero vigente. Resolver fuentes desde `Front/docs/design/lienzo/README.md` y `docs/design/alineamiento-etapas-8-y-8b.md`. No rediseñar pantallas ni agregar estados de producto. Los seis recorridos aprobados están en `Front/docs/design/recorridos/`: primera vez, consulta fija, pregunta con herramienta, guardar/exportar, clave propia y tope.

Abreviaturas: B y F son los repos Back y Front del pedido; D/A/I/P son `B/src/ArquitecturaBaseMultitenant.{Domain,Application,Infrastructure,Api}`; AU/IT/AR son `B/tests/ArquitecturaBaseMultitenant.{Application.UnitTests,Api.IntegrationTests,ArchitectureTests}`. Leer AGENTS de carpetas y fichas pertinentes antes de editar. Copiar Roles para servicio/UoW, patrón WhatsApp para módulo y trabajo durable, y los puertos neutrales existentes para actor/idempotencia/archivos.

## Back · T05 y continuaciones

### T05 · Pregunta al modelo y respuesta por SSE

**Archivos:** `A/Modules/Assistant/Services/AssistantTurn*.cs`, `AssistantConversationService.cs`, modelos e interfaces del módulo; `D/Assistant/AssistantTurn*.cs` y variantes Platform; `I/Modules/Assistant/{AssistantTurnWorker,AssistantTurnContextGuard,AssistantChatContext,AnthropicChatClientFactory,FakeChatClient}.cs`, repositorios/configuraciones del módulo; `P/Modules/Assistant/AssistantStreamResult.cs` y controllers de los tres accesos. Adaptaciones neutrales existentes de actor/replay en Application, Infrastructure y Api; migración durable y snapshot.

Terminar y verificar el trabajo local antes de considerarlo implementado. La admisión guarda mensajes, turno, binding y reservas en una UoW: locks, relecturas, dueño/versión/legales/aviso/permisos/adjuntos, reglas y escrituras. Un turno vivo por conversación. Worker con lease y fases públicas separadas; proveedor y herramientas externas fuera de transacciones. Actor derivado del servidor, restaurable y sin HttpContext, autorización viva antes y después de cada herramienta. Sin datos de chat en índices globales, idempotencia ni logs.

SSE nativo .NET 10, flush inmediato, sin MemoryStream. Replay durable por referencia reautorizada, sin otra llamada/costo. Desconexión no cancela turno admitido; stop, revocación y baja sí. Recuperación no repite una llamada incierta: conserva parcial/costo conocido o estimación conservadora y libera sólo reserva no consumida. FakeChatClient guionado sólo Testing/E2E; herramientas reales, deltas/usage/error/stop/título y contador de llamadas. No herramientas de escritura ni nuevos permisos.

**Tests:** IT/Modules/Assistant: Streaming, Idempotency, TurnRecovery, WorkerActor, TurnAuthorization, TurnContextGuard/Privacy, ProviderRoundtrip, DeletionConcurrency; AU/Modules/Assistant/AssistantPromptTests; guardas TransactionBoundary, IdempotentActions y ModuleIsolation. Probar primer delta antes de done, dos réplicas, key/cuerpo distinto, stop persistido, desconexión, actor sin HttpContext sin contaminación, revocación entre herramientas, privacidad histórica y logs. Build del proyecto afectado y pruebas del área/seguridad al terminar; las suites completas pertenecen a T15.

### T06 · Conectar consumo, título y alertas al runtime

**Archivos:** `A/Modules/Assistant/Services/{AssistantLimitGuard,AssistantUsageWriter,AssistantTitleIssuer,AssistantBudgetNotifier}.cs`, interfaces/ledger; entidades y repositorios financieros del módulo; `I/Modules/Assistant/Messaging/`, workers y resources es/en.

Conservar State/Uso ya implementados; conectar reserva, BeginCall, consumo por cada ciclo/cache, completado y cancelación sin duplicación. PlatformKey comparte UserId/día UTC entre accesos/organizaciones; OwnKey separa organización/UserId/día UTC. Reservas concurrentes respetan presupuesto/período/snapshot admitidos. Título tras primera respuesta completa, modelo elegible más barato, hasta 80 caracteres/64 tokens, sin herramientas ni pregunta extra, con costo y CAS que no pisa renombrado. Si no hay presupuesto/proveedor conservar título determinista. Alertas 80/100 por outbox una vez por organización/período/umbral: OwnKey a todos los Dueños activos con Email verificado utilizable; PlatformKey al soporte. Si no quedan destinatarios mantener pendiente y bloquear nueva admisión OwnKey según contrato.

**Tests:** AU/Modules/Assistant/{AssistantLimitsTests,AssistantCostTests,AssistantTitleTests,AssistantBudgetAlertsTests}; IT/Modules/Assistant/{AssistantLimitConcurrencyTests,AssistantUsageTests,AssistantBudgetOutboxTests}. Ejecutar sólo los que toca la conexión: cuota/reserva/replay/stop, título sin consulta extra y CAS, consumo histórico y alertas sin duplicados. No repetir cálculos ya verdes sin cambio.

### T07–T08 · Adjuntos, exportación, retención y baja

**Archivos:** `A/Interfaces/Integrations/Files/IFileStore.cs`, `A/Models/Files/`, `I/Files/`; servicios AssistantAttachment/Export/Retention/Notice y sus interfaces; `I/Modules/Assistant/Export/`, fuentes licenciadas, workers de retención/borrado y participante de baja; entidades/configuraciones privadas y ledger técnico de limpieza; controllers y seed legal del módulo.

Completar los 501 actuales de upload/download/delete/export y envío multimodal. PNG/JPEG/WebP/PDF, firma real, máximo 10 MiB/archivo y cinco por mensaje. Archivo ligado a dueño/chat, storage fuera del webroot, clave opaca, containment y escritura atómica. Storage fuera de UoW; metadata/binding/compensación y limpieza durable. Adaptador entrega contenido multimodal al SDK sin URL pública ni Files API persistente externa.

MD UTF-8 y PDF Unicode paginado, privados y sin system/tool/secrets/bytes originales, sin red remota ni HTML ejecutado. Exportar no llama al modelo ni consume cuota. Abrir ambos en T15. Copiar exportador vigente del módulo y DisplayFormatter.

Completar purga de chats vencidos, guardados y baja con participantes reales: scope antes de UoW; relectura bajo lock; lectura no renueva actividad; guardados no vencen. Borrar/cancelar y encolar borrado físico atómicamente; worker tras commit con reintentos. Replay/terceros se purgan a 24 h y al iniciar baja pertinente; última tombstone técnica sin contenido hasta baja, sin repetir efectos confirmados. Mantener uso agregado necesario sin vínculo personal. Aviso IA y legales no se aceptan implícitamente. Inventario E10 de exportación personal en feature, sin puerto sin consumidor.

**Tests:** IT/Modules/Assistant/{AssistantAttachmentsTests,AssistantExportTests,AssistantRetentionTests,AssistantAccountDeletionTests}; IT/Files/LocalFileStoreTests; pruebas existentes de notice/legales y baja. Límites exactos/+1, 5/6, MIME engañoso, dueño cruzado, rollback/purga/404, exportes con acentos y varias páginas; reloj falso, guardar contra retención y baja concurrente con turno/archivo. No diferir seguridad o límites a E9/E10.

### T09 · Siete fallos del retiro y receta

**Archivos y focales pendientes:**

| Fallo | Arreglo y test |
|---|---|
| Índice AssistantModel | Mover constante de `A/Interfaces/Persistence/UniqueKeys.cs:11` a `A/Modules/Assistant/Interfaces/Persistence/UniqueKeys.cs`; actualizar consumidores/config EF. Mantener nombre del índice, sin migración por mover constante. DbContextModelTests. |
| Inventario AssistantSessionPolicy | Contribución partial removible de `AR/IdentityAccessTests.cs:24`; mantener inventario núcleo cerrado y guarda de consumidores. |
| Publicación legal, dos casos | `IT/Platform/PlatformLegalPublicationTests.cs:36,110` (resolver ruta real): expectativas contra seed instalado, concurrencia/aceptación/idempotencia intactas; assertions v2 específicas dentro del módulo. |
| Seed producción, dos variantes | `ProductionSeedTests.cs:116`: núcleo conserva dos eventos/v1; evento Assistant, v2 y aviso Anthropic se verifican desde tests removibles del módulo. Seed repetido sin duplicados. |
| Fixture de teléfono | `PlatformAccountPrimaryPhoneTests.cs:172`: validar número sintético con parser real. Caso recuperación línea 104 mantiene 202/aprobación/identidad/máscaras; no cambiar validación productiva. |

Completar `docs/guides/quitar-asistente.md`, `agregar-una-herramienta-al-asistente.md`, feature/fichas afectadas y guardas de aislamiento sólo cuando cambie contrato/regla. Usar quitar-whatsapp como modelo. Retiro físico sólo en copia descartable de T15: excluir contribución runtime/DesignTime/bootstrap antes de generar Drop; conservar migraciones históricas/Designer; retirar fuentes/partial/paquetes/assets exclusivos, conservar puertos neutrales, Phone/Outbox/RLS/núcleo. Normalizar sólo permisos Assistant mediante nueva migración bajo scopes reales; no borrar roles/asignaciones ni conceder permisos automáticamente. Preflight aborta si un operador quedaría sin permiso. Legales publicados/aceptaciones se conservan. Regenerar contrato de copia y retirar UI sin tocar E8. Una base vacía por escenario, sin variantes adicionales de retiro.

**Tests:** focales de la tabla, ModuleIsolation, modelo/inventarios/seed instalado y recipe; prueba física en T15 de Assistant y de ambos módulos. WhatsApp solo ya aprobado en E8, no repetir.

### Texto para el chat Back

```text
Terminá T05–T09 del plan docs/plans/2026-10-08-etapa-8b-asistente.md en C:\Users\ezequ\source\repos\ArquitecturaBaseMutitenant. Leé AGENTS y las fichas/carpeta pertinentes. Sólo Back, main, commits convencionales en español con pathspec, sin push ni worktrees fuera de puerta. Revisá y completá los cambios locales Assistant sin incluir limpieza documental ajena. Prioridad: pregunta real a IChatClient y SSE durable; después continuaciones de consumo/título/alertas, adjuntos/exportes/retención/baja sin 501 y T09 con sus siete fallos incluida UniqueKeys.AssistantModel. D1–D26/ADR y contrato vigentes; técnico decidilo y avisalo, producto al coordinador. No rehagas lo terminado. Build y tests focales/área y seguridad pertinentes, sin suite completa antes de puerta ni revisores por arreglo. Coordiná con Front el protocolo Fake/herramientas reales y cualquier delta técnico de contrato. Entregá commits, resultado del área y pendientes concretos; no cierres etapa. La retirada física se ejecuta una vez en puerta por escenario.
```

## Front · T14

**Archivos:** `F/src/modules/assistant/{api,runtime,components}/` y contribuciones de accesos sólo si requieren conexión; `scripts/{assistant-open-stream,test-e2e-real-assistant,test-e2e-real-case,test-e2e-real-ui,test-e2e-real}.mjs`, `recorrido-usuario-asistente.mjs` y integración con recorrido-usuario; `scripts/capturas/etapa-8b.json`, `capturar-etapa.mjs`. Fixtures Back requeridas: `tools/RealE2ESetup/` y `IT/TestFeatures/AssistantTurns/`, coordinadas con Back.

Conectar con API real y FakeChatClient como único reemplazo del modelo: front, auth, servicios/herramientas, PostgreSQL/RLS, outbox y FileStore reales. Validar SSE por proxy/Vite, continuaciones al navegar, recuperación/stop, replay, uploads/binarios, feedback/versión/legal y cero 501. No MSW en E2E ni contenido sintético presentado como mensajes productivos. Fixtures de cuota/reloj sólo entorno E2E, sin cincuenta llamadas ni rebajar seguridad.

Preparar ejecución 1440/390 de los seis recorridos: tres accesos, legal/Entendido, atajo sin modelo, herramienta/deltas/stop/error/replay, límites de adjuntos, MD/PDF, guardar/renombrar/borrar, permisos/owner/tenant404, cuotas y financiación, alerta EML/título, retención/baja. Demostrar llamadas con contador Fake. Mantener regresiones E8 y núcleo en el runner completo. Galería sólo pantallas nuevas/cambiadas E8b; excluir los dos estados de Mensajes empresas pertenecientes a E11. No recapturar E8.

**Tests:** focales `src/modules/assistant/`, parser/stream y scripts assistant/recorrido; lint y tsc al terminar. Build/suite/E2E/galería/recorrido completos una vez en T15, cuando runtime y continuaciones estén listos. Informar al coordinador comandos/selectores para repetir sólo casos fallidos.

### Texto para el chat Front

```text
Ejecutá T14 del plan Back docs/plans/2026-10-08-etapa-8b-asistente.md en C:\Users\ezequ\source\repos\ArquitecturaBaseMutitenantFront. Leé AGENTS; sólo Front, main, commits convencionales en español con pathspec, sin push/worktrees fuera de puerta. T10–T13 se conservan. Conectá API real y FakeChatClient del Back: SSE/stop/replay/durabilidad, herramientas, adjuntos/exportes/legal/uso. Prepará runner E2E y recorrido reales, fixtures coordinadas con Back, sin MSW ni simulación de servicios. Copiá tableros v116/v118/v119 según README, no agregues producto/E11. Preservá cambios documentales/package-lock ajenos. Tests focales de conexión y scripts, lint/tsc; no corras suite/E2E/recorrido/galería completos antes de la puerta. Avisá cualquier incompatibilidad al coordinador y Back; técnico se resuelve, producto se consulta. Entregá commits y comandos de ejecución/filtros pertinentes. Nada fuera D1–D26 y pendientes del plan.
```

## Coordinador · T15, única puerta

Usar [/puerta](../../.agents/skills/puerta/SKILL.md) y [/recorrido](../../.agents/skills/recorrido/SKILL.md). Partir una vez de los commits finales Back/Front; worktrees hermanos cortos y npm ci real. Cada paso completo una sola vez; arreglos al dueño y después sólo test/área/casos afectados. Suite completa de un repo se repite sólo si cambió autenticación, persistencia base o composición. Tests Front, E2E y recorrido sin carga concurrente del Back. Timeout: una repetición aislada. Salidas en `.local/puerta-8b/`, sin actas/evidencia versionada. Revisar código una vez al cierre.

| Paso | Resultado |
|---|---|
| Back: dotnet build sin advertencias y dotnet test con Docker | Pendiente |
| Front: build, lint, test y contracts:check | Pendiente |
| E2E real completo, Assistant y regresiones incluidas | Pendiente |
| Detector documental en 0 para ambos repos | Pendiente |
| Galería lado a lado E8b revisada; desktop/móvil y responsive | Pendiente |
| Recorrido de usuario con sesiones conservadas y seis guiones E8b | Pendiente |
| Quitar Assistant: base vacía, migraciones, build/tests núcleo/WhatsApp e ingreso | Pendiente |
| Quitar ambos: otra copia/base vacía, migraciones, build/tests núcleo e ingreso Email/Google | Pendiente |
| Revisión única de código | Pendiente |
| Anthropic real | Pendiente del usuario |

Retiros según recetas, sin modificar copias compartidas. Conservar WhatsApp al quitar Assistant; luego escenario de ambos sin repetir retiro WhatsApp solo. UI vuelve a contribuciones núcleo correspondientes. Parar Aspire propio y retirar worktrees al terminar.

La prueba Anthropic no bloquea desarrollo ni puerta determinística: no pedir otra vez la clave ni declararla hecha. Cuando el usuario la cargue según `docs/operations/configuracion.md`, probar sin Fake pregunta/herramienta, imágenes/PDF, stop, título y consumo; abrir exportes, verificar propia clave/presupuesto/alertas y regresión WhatsApp real según su guía, sin publicar secretos o texto privado. Si falta esa prueba u otro paso, informar lo pendiente y no declarar cerrada la etapa.

## Cambios al contrato

Sin nuevos cambios durante esta continuación. Registrar aquí sólo deltas que se necesiten para completar pendientes y sincronizarlos con el otro dueño antes de publicar.
