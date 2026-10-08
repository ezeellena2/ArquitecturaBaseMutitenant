# Alineamiento previo de las etapas 8 y 8b

Fecha: 2026-10-08. Alcance: documentos y planes, **sin implementar** E8/E8b ni crear plantillas en Meta. Base de código back a24aaf0 y front 1c6cbc33; lienzo v114 coincide con 4340891b. Se preservó el commit documental concurrente 5382197 y se reemplazó su recomendación anterior contradictoria con la aclaración expresa del usuario: el bot no emite enlaces para operadores.

## Resultado

Las seis decisiones aceptadas están en [ADR 0049](../decisions/0049-precisiones-whatsapp-y-asistente.md), maestro, propuesta y planes. «Para decidir» queda sin pendientes. Las ADR 0043/0045 reemplazan el comportamiento anterior de número ocupado y reautenticación; las reglas front ahora reflejan modo rápido desde E7, con una sola puerta final.

No hay módulos WhatsApp/Assistant productivos en esta base. Todo archivo nuevo de los planes es tarea futura, no «copiar de» existente. Las referencias a servicios, readers, outbox, permisos y layouts actuales fueron contrastadas; no se incorporan handlers/CQRS, otro modelo de Dueños ni una bandeja sintética.

| Tema | Código actual / referencia a copiar | Aclaración en los planes |
|---|---|---|
| Identidad primero | SignupAccountRegistrar, InvitationAccountRegistrar, LoginMethodVerifier, GoogleAccountMethodFinalizer, ApplicationUser | E8 T00 consolida alta/consentimiento/vínculo/intentos/transiciones antes de Phone; los helpers trabajan dentro de UoW del servicio. |
| Errores | Domain/Results/ValidationError.cs y Api/ErrorHandling/ApiErrorCodes.cs | Reutilizar **Validation.Failed** y **Http.NotFound**. Se retiraron Validation.General y General.NotFound inventados. |
| Guards/DI | ArchitectureTests/ApplicationServicesTests, ControllerServiceRepositoryTests, TransactionBoundaryTests, ServiceDependencyCountTests; Application.UnitTests/DependencyInjectionTests | E8 T02 amplía reconocimiento por forma de namespace/contrato para módulos, no por clases exentas; registrar módulos en tests y conservar controles negativos. |
| Ruta técnica frente a feature | Api/Features/DisabledFeatureHandler; ArchitectureTests/ModuleControllersTests; Domain.Features.All vacío | E8 WhatsApp técnico se enciende por configuración; no inventar TenantFeatures ni permisos E11. Metadato neutral instalado y guarda de disponibilidad son trabajo E8; E8b negocio usa contribución assistant y gestión global explícita. |
| EF y RLS | Infrastructure/Persistence/Rls/TenantIsolationModelValidator.cs, EntityConfigurationTests/ArchitectureModel | E8 crea contribuidor neutral y tipos globales explícitos, con mismo modelo runtime/DesignTime/bootstrap/tests. E8b usa tenant/ platform; el esquema assistant de la propuesta era preliminar. |
| Bot/operador | LoginCodeService y cookies vigentes | Cuentas comunes entran consumer por enlace; operadores reciben solo texto neutro sin token/LoginLink/sesión. Preview/redeem de enlace anterior también rechazan cuenta ahora operadora. Tests obligatorios y puerta real lo comprueban. |
| Avisos | Application/Models/Notifications/AccountNotice.cs, EmailAccountNoticeChannel | Tres nuevos templates organización no añaden ActionUrl inexistente; permitir volver a publicar no republica la página. Total E8 16; dos «en uso» derogados. El paquete completo de los trece originales sigue en T03; no afirmar aprobación Meta. |
| Dueños / alertas | ITenantAdminReader/UserTenantAccessReader, SystemRoles.TenantAdmin, métodos Email | Reutilizar rol y estado actuales; agregar lectura tipada si falta. OwnKey exige un receptor y alerta a todos los Dueños activos con Email verificado; claim de Google solo no basta. |
| Herramientas background | Api/RequestContext/CurrentUser.cs depende de HttpContext | E8b crea alcance neutral de actor derivado del servidor, separado de ITenantScope; revalida permiso por llamada. No HttpContext ficticio ni privilegios del worker. |
| Streaming / replay | Api/Idempotency/IdempotencyFilter.cs bufferiza MemoryStream | E8b crea DurableStream y DurableReference neutrales; solo binding/referencias en store global, reautorizar antes de leer contenido privado. Archivo de referencia real IT/Api/IdempotencyTests.cs. |
| Cuotas | No contador Assistant existente | PlatformKey global UserId/día UTC; OwnKey tenant/UserId/día UTC, reservas persistentes. Reset UTC independiente del mes civil de organización. |
| Mensajes personales | E7 dejó mecánica en TestFeatures; no fuente productiva | Puerto neutral y vacío explícito sin enlace. E11 conecta la fuente; no usar mocks de negocio como datos reales. |
| Archivos | IFileStore aún no existe | Puerto neutral/disco privado y limpieza son tareas E8b; no afirmar backend cloud entregado. |
| Front / composición | layouts, navigation/administration.ts, home, OrganizationModulesTab, httpClient/useIdempotentMutation | Extender manifests/composición removible para Assistant. Hoy /plataforma es listado; habilitado separa Inicio/Organizaciones, apagado conserva conducta actual. Sin imports entre módulos. |

## Complementos visuales y tareas todavía pendientes

El [complemento visual del front](../../../ArquitecturaBaseMutitenantFront/docs/design/alineamiento-etapas-8-y-8b.md) enumera los estados que v114 no contiene o que las ADR derogaron. No se cambió el HTML ni se declaró una nueva versión del lienzo. Diseñar los ejemplos faltantes con piezas actuales antes de sus tareas ya autorizadas; galería y comparación una vez al final.

La guía de plantillas define es/en, muestras y variables de los tres nuevos avisos. T03 completa el paquete de los trece originales y registra/aprueba/prueba las 16 en la WABA correcta. Los textos de comentarios históricos de AccountNotice.RecoveryReceived/RecoveryRequested pueden conservar referencia anti-enumeración vieja en código: su limpieza pertenece a E8 T00/T08, sin cambiar conducta al escribir estos planes.

## Verificación de esta revisión

- Detector realinear-docs previo y final (informe 20261008-1114): back y front auto 0 / modelo 0 / humano 0, cero tokens, sin worktree/cambio de rama y sin lista humana. El detector excluye planes/ADR; por eso se hizo el contraste semántico de forma separada.
- Verificación final: 19 Markdown afectados, 255 enlaces locales/anclas sin fallos y 32 bloques de código cerrados; git diff --check limpio. La única referencia a quitar-whatsapp.md que todavía no existe está marcada como tarea futura E8. Revisión independiente de ambos planes: Approved tras corregir el prompt pendiente y las referencias PlatformSettingsAuditWriter/ReferenceDataSeeder/retención E8; sin defectos accionables.
- No se corrieron suites de producto: no se modificó código ni recursos de interfaz. Las pruebas citadas son obligaciones de ejecución futura, no pruebas ya realizadas.
- La puerta de E8 exige WhatsApp/Meta/túnel reales; la de E8b E2E real con FakeChatClient más clave Anthropic real. Una prueba local no reemplaza esa evidencia.

## Decisiones tomadas

Las adaptaciones técnicas descritas resuelven límites comprobados del código existente. Los nombres/puertos nuevos se documentan en fichas al implementarlos y quedan cubiertos por controles negativos; no se crean ahora ni se eluden guardas con listas blancas. La cuenta del operador continúa exclusiva, la cuota compartida no fusiona perfiles y el asistente no ejecuta cambios de identidad ni del bot.
