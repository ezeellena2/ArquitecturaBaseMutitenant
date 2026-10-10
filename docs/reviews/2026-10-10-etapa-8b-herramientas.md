# E8b · T04a · Herramientas y ayuda

Fecha: 2026-10-10. Base: `97882a465a94d5af3871bcd7767e4c2a236fac13`, rama `main`.
Estado: fundamentos T04a entregados para SPEC → QUALITY de Root; sin commit ni push del implementador. **T04 completo y E8b siguen abiertos.**

## Alcance entregado

El módulo registra las 19 herramientas aprobadas: 9 Business, 6 Platform, 3 Consumer y `help_article` común. Cada una declara acceso, permisos adicionales, descripción bilingüe y esquema JSON cerrado. Se rechazan parámetros desconocidos o duplicados, identidad elegida por el modelo, UUID vacío, enums desconocidos, rangos no UTC o invertidos y búsquedas de más de 100 caracteres. Los tamaños de página admitidos son 10 y 20, reutilizando el conjunto del núcleo dentro del máximo 20; cursor acotado a 2048. Los resultados proyectan como máximo 20 filas y conservan total/continuación cuando corresponde.

El descubrimiento del catálogo no autoriza la ejecución. Antes de leer y antes de entregar un resultado exitoso se comprueban actor/acceso, estado de cuenta, clase y estado de organización, membresía, legales vigentes, feature y permisos. Se usan los evaluadores actuales del núcleo, incluyendo permiso de empresa para el UUID solicitado. Un recurso extranjero responde el error 404 del núcleo. La comprobación legal sólo lee pendientes: no acepta documentos ni adelanta el aviso IA de T08.

Las lecturas propias del núcleo incorporan filtros internos `RoleId` en Usuarios, `SystemRoleKey` en Roles y límites UTC explícitos en auditorías. Los filtros ocurren antes del paginado. Ningún adaptador HTTP expone los nuevos campos; no se amplían permisos ni se desactiva RLS. Dueños se identifica mediante `SystemRoles.TenantAdmin`, con UUID leído del rol real de la organización.

Mi cuenta proyecta métodos enmascarados antes de devolverlos al consumidor de herramientas y conserva verificación/disponibilidad actual. Mis organizaciones proviene de `IProfileService`, sin leer directamente el índice global. Cada enlace autorizado lleva el UUID de esa fila en `parameters.tenantId`; se relee pertenencia/selección antes de construir enlaces. Docker verificó dos organizaciones propias con UUID distintos y exclusión de una organización ajena.

Se reutiliza `IConsumerInteractionSource` de T01. El agregador ordena fuentes por clave estable, pagina sus contribuciones sin extraerlas completas y limita la página final. Sin fuentes registradas devuelve datos vacíos reales; renderer es/en usa el literal aprobado y no genera enlaces.

Ayuda dispone de manifiesto confiable, 21 temas y 42 archivos Markdown es/en. El reader recibe tema/búsqueda, nunca rutas del chat. Filtra acceso, rutas autorizadas, permisos y módulos antes de abrir el contenido; devuelve máximo tres artículos. Sólo puede abrir archivos declarados y embebidos por el manifiesto. `WithCulture=false` evita que MSBuild convierta los sufijos de idioma de Markdown en recursos satélite que el reader no puede cargar.

El catálogo contiene las once consultas exactas. `IAssistantQuickService` es composición interna: ejecuta herramientas, arma título determinista, texto de recursos con `DisplayFormatter` y enlaces tipados con rótulos localizados. Seguridad de hoy toma el intervalo civil de la zona del operador mediante `ITimeZoneService`. Las diez consultas cuya lectura está implementada se verificaron directamente con contador de fábrica de proveedor en cero. La de uso delega al servicio real existente y propaga `NotImplemented`, sin fabricar estadísticas.

`AssistantPromptBuilder` mantiene instrucciones de sistema separadas de artículos, PDF y resultados de herramientas como datos no confiables. Los límites técnicos declarados son entrada 51200, salida 2048, título 64 y cinco llamadas por turno incluyendo el título; este corte no implementa el loop del proveedor.

## Defecto previo corregido

Los validadores recibidos limitaban mensaje a 4000 y título a 60, frente al plan aprobado 8000/80. El test dirigido RED confirmó el rechazo del borde 8000 antes de la corrección. Se incorporaron `TextLimits.AssistantMessage` y `AssistantTitle` y se ajustaron los validadores.

Ambas columnas de título también eran `varchar(60)`. La migración incremental `20261010052138_AssistantTitleLimit` las amplía a 80 en `tenant.AssistantConversations` y `platform.PlatformAssistantConversations`; no se reescribió ninguna migración histórica. SQL desde T03 revisado: sólo dos ALTER TYPE y registro de migración. No cambia RLS, grants ni datos existentes. Down vuelve a 60 y requiere que títulos existentes respeten ese límite. Docker confirmó persistencia y relectura de 80 caracteres en ambos lados; el modelo no tiene cambios pendientes. Las columnas de mensajes ya admiten el límite mayor mediante `TextLimits.LegalDocument`.

## Verificación

La tabla siguiente conserva la evidencia del corte previo de119fuentes, archivado antes de SPEC. Esa revisión resultó `Changes Required` por reautorización dinámica; los verdes históricos no acreditan la reparación posterior. Sus archivos e inventarios se preservan bajo `t04a-before-spec-*`. La evidencia del corte corregido se separa más abajo.

No se suman corridas solapadas como pruebas diferentes.

| Evidencia | Resultado |
|---|---|
| RED catálogo ausente, ejecución directa DLL | 1 ejecutado, 1 fallo esperado |
| RED límites previos, ejecución directa DLL | 1 ejecutado, 1 fallo esperado |
| Unit final: schemas, catálogo, ayuda, renderer, agregación, límites y ResourceParity | 48/48, exit 0 |
| Arquitectura focal: capas/DI/helpers, módulos, contratos, formato, límites, filtros y migraciones | 56/56, exit 0 |
| Docker final: herramientas4 + autorización4 | 8/8, exit 0; ScopeAnyCompany y SpecificCompany, revocaciones, empresa no asignada, RLS, rangos UTC |
| Docker persistencia + autorización anterior | 10/10, exit 0; incluye prueba nueva de título80 en ambos esquemas; se conserva como evidencia histórica, con solapamiento |
| Builds focales Application.UnitTests, Api.IntegrationTests, ArchitectureTests y Api | exit 0, 0 warnings/errors |
| Model drift | exit 0, modelo coincide con última migración |
| SQL incremental desde AssistantConfiguration de T03 | exit 0, revisado |
| OpenAPI exportado temporalmente | exit 0, hash LF exacto al publicado |
| git diff --check | exit 0 |

La primera ejecución Docker falló antes de preparar la fixture porque el sandbox bloqueó `docker_engine`; se repitió con permiso focal y PostgreSQL aislado. Los primeros intentos SDK/build sin serialización no ejecutaron pruebas y no se acreditan como RED ni verde. Se conservaron los errores de compilación/analyzers intermedios.

Un filtro clase+método produjo cero pruebas y se considera inválido. Otro filtro con comodín interno fue rechazado por el runner. La preparación inicial de SpecificCompany devolvió `Roles.Role.PermissionScopeInvalid`: el fixture intentaba asignar `assistant.use` a un rol personalizado de empresa. El fixture final respeta el núcleo: rol Organization de sólo chatuse y rol SpecificCompany de sólo lectura de miembros. Ese fallo de preparación no es RED productivo; la conducta final está verde.

El exportador OpenAPI registró un error de permisos del key ring de DataProtection durante su startup en sandbox, separado de los builds sin warnings/errors. Aun así generó el documento completo y devolvió exit 0; su SHA-256 UTF-8 LF sin BOM es `ac450a7ca6ccfda23e5bd16f2f2fec0c20e3bec9176cfd785be9960f3919c9f4`, igual al archivo publicado. No se modificó DataProtection/Identity para ocultar ese diagnóstico.

## Decisiones tomadas

- Los tamaños 10/20 de herramientas siguen la validación paginada existente del núcleo; no se amplía ese contrato.
- La agregación futura de interacciones concatena fuentes por clave estable para paginar con lecturas acotadas; el puerto neutral original no fue redefinido y no hay fuentes productivas registradas.
- La autorización se reparte entre SessionPolicy y AccessPolicy para mantener responsabilidades y el límite de ocho dependencias; el resultado exitoso se reautoriza después de la lectura.
- La ayuda es recurso embebido del repositorio con manifiesto confiable y filtros previos; no hay CRUD, embeddings ni rutas externas.
- El servicio local de consultas queda separado de persistencia de Quick; no llama al SDK, no escribe historial ni consume cuota.

## Cambios al contrato

Ninguno. OpenAPI publicado y exportado conservan exactamente el freeze CC01–03. No se tocaron Front, controllers, contratos HTTP ni rutas. Se preservó `Domain/Authorization/Role.cs` extranjero, con numstat cero.

## Pendientes y límites de esta entrega

- T04b/T05: componer Quick HTTP con persistencia de mensaje/resultado, binding durable, idempotencia/replay e historial; revalidar y persistir enlaces autorizados. Las rutas Quick y State siguen 501.
- T05: actor productivo neutral con sesión confiable y tenant/acceso/kind enlazados. Los tests usan `ICurrentUser` neutral de fixture y `ITenantAccessInitializer.SetFromAccess`; no prueban el binding productivo. `ITenantScope.Enter(Guid)` deja kind null y por sí solo no habilita la feature Consumer/Business.
- T06: UsageService y la herramienta de uso con agregados/costos reales; hoy sigue 501.
- T08: admisión y aviso IA previos a herramientas/generación.
- Cierre de etapa: suites completas, proveedor real autorizado, E2E y documentación final corresponden a la puerta de E8b; no se ejecutaron ni se declaran cubiertos aquí.

## Entrega congelada

### Reparación posterior a SPEC

La revisión inicial detectó una brecha P1: `help_article` y `org_pending` tenían permisos fijos vacíos y su post-check sólo revalidaba chat. Un permiso específico revocado durante la lectura podía dejar pasar artículos o `Subject` de pendientes. La corrección conserva `assistant.use` como permiso de chat y revalida el requisito real de cada resultado.

`IPendingItemSource.CanResolveAsync` es obligatorio, sin implementación por defecto. `HomeService` conserva la fuente de cada fila y la invoca al terminar la agregación. Usuarios relee los permisos con `IPermissionService`; para invitaciones de empresa relee el `MemberId` real de `TargetId`, verifica el permiso de esa empresa y mantiene `CompanyInvitationPolicy`. Configuración y Página pública revalidan sus permisos propios. Las fuentes deniegan tipos desconocidos y una fuente futura debe comprobar también su módulo. Ninguna autorización se reconstruye desde `Subject`. El ajuste queda documentado en la ficha de Inicio y aplica tanto a HTTP como al consumidor interno, sin cambiar el cuerpo HTTP.

El addendum de volumen aceptó reutilizar `IHomeService.GetAsync` con `{}`. Inicio continúa recorriendo sus pendientes completos; el máximo20 corresponde a la salida del tool y no acredita un límite de filas leídas. No se optimizó ni se cambió el agregado.

Un segundo addendum detectó P2: E8 aporta WhatsApp por `ILoginCodeChannel`, no como feature; la ayuda nunca podía mostrar ese artículo mediante `IFeatureService` solamente. `AssistantHelpAvailabilityPolicy` usa el catálogo como autoridad: una feature definida exige su gate vigente; sólo una clave sin feature consulta `Key`/`IsAvailable` de canales registrados. Desconocidos, retirados y apagados se deniegan. La misma policy corre antes y después de `SearchAsync`, con claves del manifiesto confiable. No hay literal ni import de WhatsApp en el código productivo de Assistant, ni cambios de identidad, `/api/me` o runtime del canal.

Los tests suspenden la lectura con señales deterministas, revocan únicamente `roles.read`, `users.invite`, `users.manage` o la asignación `SpecificCompany`, y reanudan. El permiso de chat permanece vigente. El caso de empresa usa la proyección de chat desde otra empresa, conserva la exclusión de una tercera empresa y no amplía el rol específico. El gate sintético de `TestFeatures` comprueba una feature definida apagada que no puede ser rehabilitada por un canal coincidente; no representa el catálogo productivo E8.

| Evidencia final de la reparación | Resultado |
|---|---|
| Unit disponibilidad/ayuda/Quick/tools y ResourceParity | 32/32, exit 0 |
| Arquitectura focal sobre el helper y puertos actuales | 38/38, exit 0 |
| Docker: revocación9 + autorización4 + herramientas4 + Inicio/aislamiento/empresa11 | 28/28, exit 0 |
| Builds focales de Unit, IT y Architecture, incluidas capas afectadas | exit 0, 0 warnings/errors |
| OpenAPI regenerado de la reparación en directorio temporal separado | exit 0; hash LF exacto `ac450a7ca6ccfda23e5bd16f2f2fec0c20e3bec9176cfd785be9960f3919c9f4` |
| git diff --check con rutas ajenas excluidas | exit 0; avisos de normalización LF/CRLF no son defectos del diff |

El startup del nuevo export volvió a registrar `UnauthorizedAccessException` del key ring de DataProtection. Se conserva en su log propio, separado de los builds limpios y de la generación exacta; no se modificó ese runtime.

La composición real E8 comprueba artículo es/en con canal configurado y vacío con canal apagado. No hay sender WhatsApp registrado en la fixture. Una envoltura neutral cambia disponibilidad durante `SearchAsync` y falla ante cualquier método de envío/render/validación; sus contadores y la fábrica IA quedan en cero. Esta prueba no afirma que `IOptions` permita recarga de configuración. Las pruebas unitarias cubren canal retirado, clave desconocida y precedencia del gate de feature.

La primera corrida de revocación tuvo3REDválidos y1fixture de módulo inválido: el artículo ya estaba oculto. La segunda tuvo4REDválidos (incluye feature sintética) y1fixture de empresa inválido por invertir argumentos de `RoleAssignment.Assign`; se corrigió respetando el alcance del núcleo. El RED de canal real ejecutó4casos:2fallos válidos por artículo oculto con canal configurado es/en y2verdes con canal apagado. Los errores intermedios de compilación permanecen como diagnósticos, sin acreditarlos como RED de conducta. Los manifiestos distinguen cada intento y los cohorts finales sin sumar solapamientos.

El baseline sigue siendo T03 `97882a465a94d5af3871bcd7767e4c2a236fac13`. Root publicó por separado sólo una propuesta documental CC04; HEAD actual difiere en `docs/reviews/2026-10-09-etapa-8b-contrato.md`, ajeno al corte y excluido de su inventario. CC04 permanece sin aprobación/publicación y este corte mantiene el contrato CC01–03. La deuda comunicada de `ProductionSeedTests` pertenece a T03/T08a y no se modificó ni se ejecutó dentro de esta reparación.

Inventario explícito: `.local/reviews/e8b-contract/t04a-files.json`. Manifiesto de comandos, exits, resúmenes y hashes: `t04a-manifest.json`. Recibo de entrega: `t04a-delivery.json`. SQL: `t04a-title-only.sql`; export temporal: `t04a-openapi/ArquitecturaBaseMultitenant.Api.json`. Hashes de fuentes y evidencia normalizados a UTF-8 LF sin BOM. Root hará SPEC → QUALITY → commit. No se mutará este corte sin avisar.

### Reparación posterior a QUALITY

QUALITY del freeze129 detectó P2 en `org_invitations`: Usuarios selecciona el estado y después hidrata la invitación pendiente en otra consulta. Una aceptación o revocación entre esas lecturas puede conservar `Invited`/`Expired` en la fila y entregar `InvitationId`/`InvitationExpiresAtUtc` nulos. El adaptador nuevo los asumía presentes y lanzaba `Nullable.Value`.

La herramienta ahora comprueba ambos campos explícitamente. Si falta cualquiera, devuelve el `Result` existente `Assistant.QuickQuery.NotAvailable` para toda la página. Esa transición requiere una consulta nueva: no significa ausencia definitiva de invitaciones. No entrega filas parciales, no fabrica UUID/fecha ni recalcula totales desde un subconjunto. Una página completa o fuera de rango conserva exactamente `TotalCount` y `HasNext` del servicio. No hay reintentos automáticos, cambios del reader/HTTP ni permisos nuevos.

La regresión determinista usa un doble de `IOrganizationUserReader` que reproduce esa respuesta nullable detrás del servicio real y del actor, sesión y autorización reales de la fixture. Cubre Pending/Expired con ID, vencimiento o ambos ausentes, además de páginas completas20/1 y vacías fuera de rango con total21. La fábrica IA queda en cero y sólo se realiza una lectura por ejecución.

| Evidencia de esta reparación | Resultado |
|---|---|
| RED dirigido de hidratación | 12 casos, 6 fallos `Nullable.Value` esperados y 6 páginas completas/vacías verdes; exit1 |
| Build focal IT y capas afectadas, antes y después | exit0, cero advertencias/errores |
| Docker final: hidratación12 + autorización4 + herramientas4 | 20/20, exit0 |
| Diffcheck de rutas propias | exit0 |

El primer intento Docker no pudo preparar la fixture por acceso sandbox a `docker_engine`; se conserva como ejecución inválida de entorno, no como RED productivo. La repetición con permiso focal produjo los seis fallos válidos antes de modificar la herramienta. No se repitieron suites ajenas ni se suman los cohorts históricos32/38/28 con el nuevo20. El export OpenAPI anterior permanece como evidencia histórica; el contrato publicado sigue con hash LF exacto CC01–03, sin afirmar un nuevo export de esta reparación.

Antes de editar se archivaron los metadatos del freeze129, SPEC Approved y QUALITY ChangesRequired bajo `t04a-before-quality-*`, con hashes LF/raw y copias de las dos fuentes existentes modificadas. Es un archivo parcial de fuentes, no un snapshot completo130; los archives119 y el anterior archivo130 de ownership permanecen intactos. El nuevo inventario integral contiene130 fuentes; `t04a-quality-repair-files.json` distingue las tres diferencias frente129: herramienta, test nuevo e informe propio. `docs/features/asistente.md` sigue restaurado a HEAD y excluido del corte. Los pendientes T04b/T05, UsageT06, noticeT08 y la puerta completa E8b permanecen abiertos.

