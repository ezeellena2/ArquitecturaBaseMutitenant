# E8 M2 · Extracción independiente de módulos

Fecha: 2026-10-10. Base de despacho `cc4cc231043fe0fca6830c34f27fda6594ea1dfb`; HEAD al entregar `cb42c3b956cad97a1793ba116444066491acc537`, con un commit concurrente de documentación de coordinación. No se presenta como una cohorte sobre un corte fijo. Root audita y publica el alcance propio sin push; M3 conserva el único export HTTP final.

## Resultado y decisiones tomadas

Las referencias CLR, bindings e inventarios exclusivos de WhatsApp y Asistente en las pruebas están en árboles quitables. Las clases mixtas conservan archivos núcleo y aportes `partial` dentro de `Modules/<Módulo>`; al retirar un módulo sólo desaparecen sus métodos/entradas explícitos. No se cambió comportamiento productivo, HTTP, migraciones, snapshot EF, Role.cs ni el aprovisionador.

La lista anónima mantiene exactamente su webhook autorizado; los inventarios de rutas aportan 56 operaciones Assistant y dos WhatsApp mediante partial. El escaneo productivo de `ModuleIsolationTests` sigue usando los módulos reales instalados, mientras el clasificador puro de controles sintéticos recibe prefijos explícitos. Sus positivos Domain.WhatsApp/Assistant no dependen de que esos módulos estén instalados. No se agregaron whitelists ni Skip.

`ControllerServiceRepositoryTests` usa un reader sintético prohibido de Modules.Control. Los controles reales de respuestas del Asistente y de su versión anidada permanecen en partial quitables; los controles neutrales y el escaneo de todos los controllers permanecen en núcleo. `ApplicationServicesTests`, `ModuleControllersTests` y `DependencyInjectionTests` conservan sus guardas y registros concretos en bindings quitables. El control negativo de duplicados DI usa un servicio sintético de módulo y funciona sin módulos productivos.

Los inventarios no se deducen de la misma lista que se está comprobando: baja compara seis participantes núcleo por nombre más las contribuciones esperadas según presencia del módulo en Application; recursos compara un inventario explícito de managers de módulos instalados con los manifests reales. Faltantes, extras y duplicados siguen fallando. Los cinco managers núcleo, placeholders, limpieza, privacidad e idempotencia permanecen.

## Preservación de pruebas núcleo

| Área | Núcleo que permanece | Parte quitable |
|---|---|---|
| Auditoría y modelo | `AssistantPersistenceModelTests.A_global_audit_contract_cannot_remove_the_audit_trail_from_a_private_entity`, con su probe privado | Sólo el mapa de conversaciones instalado, en `InstalledAssistantPersistenceModelTests` |
| Outbox | Tres carreras completas de selección/locks/cancelación/lease/Sent; `Support/OutboxPrivacyFixture` conserva TimeProvider falso 2030-10-08, retirada de workers y UoW | El seed WhatsApp sigue en su fixture modular; no se copió al núcleo |
| Plataforma y avisos | Cuatro casos Email, acción real y replay sin duplicado ni razón interna en payload | Cuatro casos Phone y sus bindings/payload WhatsApp |
| Destino de recuperación B7 | Cuatro casos Email/Google, own/foreign, es/en, snapshots reales de identidad, petición, outbox, auditoría y avisos | Dos Phone con el transporte prohibido y lector de código WhatsApp |
| Caché y roles A1 | `RolePermissionCacheIsolationTests` mantiene key legacy/current, invalidación mediante `roles.manage` y mismo roleId aislado por RLS en otra organización | Migración Assistant y cinco corrupciones conservadas en `Modules/Assistant/SystemRolePermissionUpgradeTests` |
| Inicio | Assertions completas de tarjetas, roles, empresas, invitaciones, permisos y acceso a fichas | Dos variantes de disponibilidad Assistant y rol sólo assistant.use |
| Hosting/probes | Rechazo real de una dependencia DI no registrada y todos los probes núcleo | Resolución de puertos Assistant; probes de errores/cuota WhatsApp y su binding |
| Enlaces | Identidad, lockout, cookies, baja y replay con Phone técnico exclusivamente de test; `Host(false)` conserva host normal sin ese canal | Caso real del cambio de países WhatsApp |
| Contratos E8 | Phone sin canal, enlace sin prueba, contacto, errores e inventario original del núcleo | Webhook técnico y sus assertions OpenAPI |
| Roles de sistema | Inmutabilidad, alcances y permiso exacto del catálogo/proyección neutral | Assertions específicas de los permisos Assistant |

`Support/PendingMethodJourney` y los tests Email de posesión no se cambiaron; la clase B8 y su transporte ya estaban dentro del árbol WhatsApp. `ProductionSeedTests` conserva igualdad exacta con LegacyBootstrap y ahora rechaza cada permiso restante del catálogo, cubriendo también los módulos instalados sin depender de sus constantes. La respuesta neutra al operador de WhatsApp se verifica en su árbol; `LoginLinkIssuerTests` conserva el rechazo neutral al operador.

## Contratos legacy corregidos

Se ejecutó primero el caso original `Consumer_without_roles_reaches_501_with_an_explicit_available_feature_and_private_protocol_keys` con PostgreSQL propio: **1 caso, 1 error, exit 2**. Fue un RED real: esperaba NotImplemented y recibió BadRequest por `Assistant.Notice.AcceptanceRequired`, porque create/quick ya estaban implementados por T05b.

La clase completa se movió a `Api.IntegrationTests/Modules/Assistant/AssistantContractTests.cs`. El caso corregido usa una ApiFactory propia, conserva 401, perfil consumer sin roles, feature explícita, falta de clave 400 y State/messages 501. Comprueba Notice400 en ambas operaciones, aceptación explícita, create201 con Location/versión/id, quick200 con dos mensajes/result sin correo completo y replay con cuerpo idéntico. Comprueba en la fila global real que `ResponseBody` sea null y sólo haya referencia durable del tipo correcto e ID. La consulta usa `my_signin`, clave real del catálogo; `account` era una clave histórica inválida y es el discriminador del dato, no un alias nuevo. Los otros tres casos Usage/auth, configure sin use y plataforma sin clave permanecen; no se implementó runtime para acomodar tests.

La primera cohorte de integración encontró otro setup anterior a T05b: Home habilitado esperaba listado200 sin aceptar el aviso. Se corrigió sólo el partial Assistant: verifica primero el rechazo exacto por Notice, acepta explícitamente y luego conserva el listado200, proyección de permisos y todas las assertions del inicio. El caso núcleo independiente permanece en el archivo Organization/HomeTests.

## Verificación y contadores originales

| Ejecución | Resultado original | Resultado aceptado |
|---|---|---|
| RED del contrato legacy | 1 ejecutado, 1 error, exit 2 | RED de conducta documentado |
| Arquitectura focal | 57 ejecutados, 57 correctos, exit 0 | 57/57 |
| Application focal | 14 ejecutados, 14 correctos, exit 0 | 14/14 |
| Domain focal | 9 ejecutados, 9 correctos, exit 0 | 9/9 |
| Integración de 14 clases afectadas | 77 ejecutados, 76 correctos, 1 error Home, exit 2 | Se conservan los 76 originales |
| Reemplazo de la teoría Home | 2 ejecutados, 2 correctos, exit 0 | Reemplaza sólo el caso fallido; el caso false ya verde no se suma |
| Probes HTTP reubicados | 5 ejecutados, 5 correctos, exit 0 | 5/5 distintos de los anteriores |

Resultado aceptado: **77 casos distintos de la cohorte de integración**, más los cinco probes. Ninguna ejecución útil tuvo omitidos. No se suman reejecuciones, RED ni intentos sin casos como cobertura adicional.

Los cuatro proyectos afectados se compilaron serialmente con `--no-restore -m:1 -nr:false`: Api.IntegrationTests (incluye API/Infrastructure), ArchitectureTests, Application.UnitTests y Domain.UnitTests, todos con **0 advertencias y 0 errores**. Después del ajuste de Home, su build dirigido también quedó en 0/0. Los tests usan `--no-build`. Docker/MSBuild se ejecutaron fuera del sandbox por los requisitos de IPC del entorno.

Se conservan dos builds iniciales no aceptados: 14 y 8 errores de analizadores por imports de archivos separados y el retorno concreto de una lista (IDE0005/CA1859); no fueron RED de conducta. El primer filtro exacto de la teoría Home seleccionó cero casos y devolvió exit 8; se corrigió a wildcard y se ejecutaron sus dos variantes. También queda registrado como tooling, sin atribuirle verde. Una comprobación de diff con override `core.autocrlf=false` marcó los CRLF existentes, incluido Role.cs ajeno, como whitespace; se corrigió el comando conservando la configuración normal del repositorio, sin editar ese archivo.

## Comandos y evidencia

Inventario propio: `.local/reviews/e8b-contract/e8-module-extraction-files.json`. Contiene cada ruta y SHA-256, las bajas con `deleted: true`/`sha256: null` y el hash original, más comandos completos, logs con hash, exits, duraciones y contadores. Las reubicaciones aparecen con **ambas rutas**, anterior borrada y nueva agregada; Root debe publicarlas juntas.

Los filtros completos se conservan en los JSON de `.local/reviews/e8b-contract/m2/`; los scripts `run-builds.ps1` y `run-tests.ps1` documentan la selección. La integración incluye ServiceProviderValidation, Home, OutboxDeletionRace, AccountDeletionParticipants, PlatformOrganizationPhoneNotice, PlatformRecoveryDestinationConflict, RolePermissionCacheIsolation, SystemRolePermissionUpgrade, AssistantContract, AssistantErrorContract, ExplicitRouteInventory, Etapa8Contract, LoginLink y ProductionSeed. Los probes usan sus cinco tests existentes de localización es/en y replay de cuota.

Los builds autoexportaron el delta B7 del OpenAPI, preservado en `m2/generated-openapi.json` y `.diff`, SHA-256 `469bd1ec819166a88a19fa6218d93b501cea605deaf0c6cadfca9cd5e872f6ad`. Una vez cerrados todos los builds se restauraron los bytes exactos del contrato desde la base con Node `execFileSync('git', ['show', ...])` como Buffer, límite 64 MiB y `fs.writeFileSync`: SHA-256 final `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`. Role.cs ajeno conserva `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`; no se tocó ni se incluyó en la entrega.

## Recetas y límite de la entrega

La guía WhatsApp se actualizó y se agregó `quitar-asistente.md`: tres vías EF, última histórica `20261010203618_SystemRoleAssistantPermissions`, scaffolding por CLI con fuentes temporales, historia/Designers intactos, SQL/Down/grants revisados, conexión PostgreSQL descartable explícita y conservación de tablas núcleo, DP, outbox, columnas durables y legales v2. Asistente exige normalizar exclusivamente sus tokens de roles system en la migración nueva de retiro, preservando IDs/asignaciones/custom y la guarda exacta de provisión; Down aditivo previo no lo hace.

La retirada de WhatsApp conserva el ProjectReference de RealE2ESetup a integración y su host núcleo; precisa la rama de Program y los cuatro bindings de RecorridoHost que sí salen en la copia. Email/Google, PG externa, E7Preparation, certificado, comandos núcleo y email pump permanecen.

**No se ejecutó retirada física ni copia mínima** en M2, ni suites completas, detector de documentación en 0, Aspire, E2E, galería, recorrido o transportes Meta/Anthropic. Las tres retiradas físicas con base vacía y previamente migrada pertenecen a la única puerta final de Integración sobre el par fijo. Las referencias históricas de informes anteriores y la actualización global del árbol documental se revisan allí, sin editar archivos de coordinación desde este correctivo.

La entrega deja fuentes inmóviles y todos los procesos propios de build/test cerrados, sin stage, commit ni push del implementador. Root audita/publica M2 y continúa M3; no se declara E8 cerrada.
