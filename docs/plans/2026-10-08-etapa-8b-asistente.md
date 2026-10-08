# Etapa 8b: el asistente — plan ejecutable

**Estado:** plan para ejecutar después de cerrar E8; este documento no implementa nada. P1–P3 fueron aceptadas el 2026-10-08 y están en la ADR 0049; no reabrirlas. Los complementos visuales se completan antes de sus tareas.

**Objetivo:** un asistente privado en los tres accesos, de solo lectura, con consultas fijas gratuitas, herramientas autorizadas, historial, adjuntos, exportación, configuración, uso y topes. Se puede apagar o quitar sin romper el núcleo ni WhatsApp.

**Arquitectura:** `Domain/Assistant` y `Modules/Assistant` en Application, Infrastructure y Api; puertos neutrales del núcleo y contribución EF de E8. `IChatClient` es el puerto del modelo, Anthropic el adaptador inicial; controllers y SSE en Api, herramientas/casos de uso en Application, archivos/proveedor/EF/workers en Infrastructure. Ninguna llamada externa corre dentro de la UoW.

**Base verificada:** back `a06530d` (plan E8), código anterior `a24aaf0`; front `1c6cbc33`. `docs/design/lienzo/` es v114 y no tiene diferencias contra `4340891b`. La [revisión de alineamiento](../reviews/2026-10-08-alineamiento-etapas-8-y-8b.md) registra las adaptaciones de guardas/DI y lo que existe hoy. La [revisión de alineamiento](../reviews/2026-10-08-alineamiento-etapas-8-y-8b.md) registra las adaptaciones de guardas/DI y lo que existe hoy. E8 todavía no está implementada: las referencias a sus contribuidores, guardas y capacidades son dependencias a recibir, no archivos existentes hoy.

## Forma de trabajo

Manda sobre protocolos anteriores. Copiar la [Forma de trabajo de E7](2026-10-04-etapa-7-publico-y-personal.md) y [etapa-en-paralelo](../guides/etapa-en-paralelo.md).

- Programar directo, por dependencias. Tests junto al código donde importan: permisos, aislamiento, privacidad, carreras, topes, streaming, limpieza y recorridos nuevos. Sin rojo ceremonial ni test por cada Tweak visual; conservar las guardas existentes.
- **Tres partes:** 1. Contrato, un chat en back que regenera tipos del front; 2. Back y Front en paralelo, un chat por repo; 3. Integración y puerta, un chat en ambos repos. Cuatro textos para pegar al final de cada sección.
- Contrato congelado y commiteado en ambos repos antes del paralelo. Cada chat de parte 2 toca solo su repo; cambios necesarios se anotan en «Cambios al contrato», el usuario los pasa al otro chat y se sincronizan antes de seguir.
- Sin aprobaciones por pantalla ni capturas por avance. Copiar v114 y sus estados. Galería una sola vez al final. Si falta un estado que cambia el producto y las fuentes no lo resuelven, Diseño lo dibuja antes de esa tarea; las independientes avanzan.
- Commits coherentes en español, `main`, sin push, solo rutas propias: `git add -- <rutas>`; `git commit -m "…" -- <rutas>`. El commit sigue la tarea; al final, informe corto. Leer AGENTS de raíces/carpetas y las fichas antes de editar.
- Build y pruebas focales durante el trabajo; suites completas en la única puerta final. No cerrar con 501, FakeChatClient en producción, documentación pendiente ni prueba real omitida. Para ejecución tarea por tarea usar `executing-plans`, subordinada a esta forma de trabajo.
- Una sola puerta: suites completas, E2E front/API/BD reales con FakeChatClient, prueba manual Anthropic real, detectores en 0, galería, sesiones anteriores y prueba de quitar. Apagar solo el Aspire propio.

## Fuentes, precedencia y paquetes verificados

1. Pedido del usuario; [ADR 0048](../decisions/0048-asistente.md) y [0049](../decisions/0049-precisiones-whatsapp-y-asistente.md); tabla **«Decidido con el usuario», D1–D26**, de la [propuesta](propuesta-asistente-llm.md). D6 no tiene fila de respuesta allí: Anthropic oficial queda resuelto por 0048. No reabrir D1–D26 por recomendaciones viejas.
2. [Maestro E8b](2026-09-27-plan-de-desarrollo.md#etapa-8b-el-asistente-un-llm-en-el-inicio), [E8](2026-10-07-etapa-8-whatsapp.md), [E11](2026-10-03-etapa-11-whatsapp.md), ADR [0033](../decisions/0033-metodos-de-ingreso.md), [0041 reemplazada](../decisions/0041-no-revelar-cuentas.md), [0042](../decisions/0042-operadores-de-plataforma.md), [0043](../decisions/0043-las-pantallas-dicen-si-hay-cuenta.md), [0044](../decisions/0044-sesion-de-plataforma-aparte.md), [0045](../decisions/0045-mi-cuenta-sin-codigo-de-confirmacion.md), [0046](../decisions/0046-nombre-y-apellido.md), [0047](../decisions/0047-el-ingreso-acepta-la-invitacion.md) e [índice](../decisions/README.md).
3. Front v114: Asistente-Org/Personal/Plat, todos los estados del Tweak **asistente** y sus M-*; Asistente-Config-Org/Uso-Org, Plat-Asistente-Config/Uso y sus M-*; Organizacion/M-Organizacion (Módulos), Mensajes (cuatro alertas de correo), navegación general v114. No usar Archivo ni generadores como especificación.
4. Arquitectura y reglas: [backend](../architecture/backend.md), [multitenancy](../architecture/multitenancy.md), [Roles](../features/roles.md), [Personal](../features/personal.md); fichas capas-y-flujo, guardado, persistencia-ef, multitenancy, modulos, modulos-habilitados, permisos, API, idempotencia, concurrencia, paginado, datos-de-referencia, datos-personales, logs, fechas-y-zonas, números/moneda, resources y tests; [configuración](../operations/configuracion.md), [plantillas](../operations/whatsapp-plantillas.md). Front: reglas de datos-y-api, estructura, formatos, accesos, responsive, errores y tests.

**Verificación de versiones, 2026-10-08:** se consultaron los índices vivos de NuGet (`v3-flatcontainer/<paquete>/index.json`, última estable) y npm (`/<paquete>/latest`), sin instalar. Las páginas cacheadas tenían parches anteriores. Fijar estas versiones, no `latest` ni prereleases, cuando se ejecute T00.

| Paquete nuevo | Versión confirmada | Capa / fuente oficial |
|---|---|---|
| Microsoft.Extensions.AI.Abstractions | 10.10.1 | Application, solo dentro del módulo: IChatClient/AIFunction; [NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions/10.10.1) |
| Microsoft.Extensions.AI | 10.10.0 | Infrastructure, función/telemetría; [NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI/10.10.0) |
| Anthropic | 12.54.1 | Infrastructure; [NuGet](https://www.nuget.org/packages/Anthropic/12.54.1), [SDK oficial y AsIChatClient](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/csharp#ichatclient-integration). No hace falta un paquete Anthropic.Extensions.AI. |
| PDFsharp-MigraDoc | 6.2.4 | Infrastructure, exportar PDF multiplataforma, licencia MIT; [NuGet](https://www.nuget.org/packages/PDFsharp-MigraDoc/6.2.4). Sin GDI/WPF ni licencia comercial. PDFsharp llega como dependencia. |
| @assistant-ui/react | 0.15.25 | Front; [registro npm](https://registry.npmjs.org/@assistant-ui/react/latest), [ExternalStoreRuntime](https://www.assistant-ui.com/docs/runtimes/custom/external-store); peers React 18/19. |
| @assistant-ui/react-markdown | 0.14.19 | Front; [registro npm](https://registry.npmjs.org/@assistant-ui/react-markdown/latest); peer assistant-ui ^0.15.0. |
| react-markdown | 10.1.0 | Front; [registro npm](https://registry.npmjs.org/react-markdown/latest) |
| remark-gfm | 4.0.1 | Front; [registro npm](https://registry.npmjs.org/remark-gfm/latest) |

SSE ya viene con .NET 10: [TypedResults.ServerSentEvents](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.typedresults.serversentevents), devuelto desde controller. No añadir servidor Node, AI SDK, SignalR, MCP ni paquetes de RAG. Versiones back solo en `Directory.Packages.props`; front en package.json/lock. Verificar compatibilidad por restore/build al ejecutar, sin actualizar paquetes ajenos.

Rutas abreviadas: `B/` y `F/` son los dos repos del pedido; `D/`, `A/`, `I/`, `P/` son `B/src/ArquitecturaBaseMultitenant.{Domain,Application,Infrastructure,Api}/`. `AU/`, `IT/`, `AR/`, `DU/` son los proyectos `B/tests/ArquitecturaBaseMultitenant.{Application.UnitTests,Api.IntegrationTests,ArchitectureTests,Domain.UnitTests}/`. Archivos nuevos se nombran concretamente aquí; los de E8 se reciben al cerrarla.

## Tableros contra decisiones

Cada nombre refiere a `F/docs/design/lienzo/<nombre>.dc.html`; los M-* se copian también. La correspondencia visual no sustituye la prueba de reglas invisibles ni inventa pantallas para decisiones de infraestructura.

| Decisión cerrada | Tablero / Tweak y estado | Qué se verifica al ejecutar |
|---|---|---|
| D1 Lugar | Asistente-Org/Personal/Plat: asistente=Bienvenida; M-*: Bienvenida | Desktop reemplaza el contenido del inicio conservando su bienvenida/Para resolver. Móvil tiene ruta/pestaña aparte, no sustituye billetera. |
| D2 Clave propia | Organizacion: pestana=Módulos; M-Organizacion: Módulos: acciones del Asistente; Config-Org: Con su clave / La plataforma no habilitó su clave | Permitir/impedir clave propia desde Plataforma, default plataforma. |
| D3 Habilitar | Organizacion: Módulos; Plat-Asistente-Config: Configurada (Personas) y M-*: Configurada | Business por módulo, Consumer por interruptor global; ambos apagados por defecto. Sin módulo vuelve UI anterior. |
| D4 Retención | Tres Asistente: Menú de una conversación; M-*: Acciones de una conversación; Guardadas y Guardar; Plat-Config: Configurada (30 días) | Guardada no vence; no guardada vence tras 30 días sin actividad. Borrar/baja borra incluso guardadas. |
| D5 Privacidad | Tres Asistente: Historial plegado / Menú; M-*: Historial; ambos Uso: Por persona/Por organización | Historial propio; Uso no enlaza ni devuelve conversaciones. 404 también entre compañeros y para Dueño/operador. |
| D6 Proveedor | Config-Org: Con su clave; Plat-Config: Configurada / Sin clave; sus M-* | Campo Proveedor=Anthropic; IChatClient/Fake invisibles, resueltos por ADR0048. |
| D7 Modelo | Config-Org: Elegir el modelo; Plat-Config: Configurada / Catálogo de modelos; M-*: Configurada/Catálogo | Desplegable de referencias, no modelo hardcodeado de la vieja recomendación. |
| D8 Solo lectura | Tres Asistente: Respuesta; M-*: Respuesta | Enlaces a Usuarios/organización/uso; no botones que ejecuten invitaciones, cambios, bajas ni recuperación desde IA. |
| D9 Consultas fijas | Tres Asistente y M-*: Bienvenida / Respuesta | Exactamente los atajos de la tabla siguiente; no invocan modelo ni título IA. |
| D10 Topes | Org: Quedan 10 consultas / Tope del día / Tope del mes; Personal/Plat: Quedan 10 consultas / Tope del día; Config y Uso | 50 diarios iniciales compartidos por UserId/día UTC con clave plataforma; own-key por UserId/organización/día UTC. Mes civil organización en su zona; aviso al llegar (0049). |
| D11 Permisos | Org: Sin configurar (Dueño) / Sin configurar (otro rol); Config/Uso-Org | Revisar configuración solo con assistant.configure; usar con assistant.use en roles de sistema. Permisos de herramientas siguen vigentes aunque el chat exista. |
| D12 Persona | Asistente-Personal/M-*: Bienvenida / Respuesta / Sin configurar; Plat-Config: Configurada | Cuenta y organizaciones propias; P1 resuelta: atajo de mensajes con puerto neutral y vacío explícito hasta recibir fuente productiva, sin exponer tablas Business. |
| D13 Extras | Tres Asistente: Respuesta / Menú; M-*: Respuesta / Acciones de una conversación | Copiar, 👍/👎, enlaces filtrados, Exportar Markdown/PDF, adjuntos. |
| D14 Adjuntos | Tres Asistente: Con adjuntos / Adjunto de más de 10 MB; M-*: Con adjuntos | PNG/JPEG/WebP/PDF, 10 MB por archivo, 5 por mensaje. Móvil reutiliza error del campo, sin inventar pantalla. |
| D15 IFileStore | Con adjuntos / Borrar, desktop y M-* | Puerto/disco no tiene tablero propio; descarga privada y borrar archivos con conversación. Nube se decide E10. |
| D16 Exportación | Menú de una conversación / M-*: Acciones de una conversación | Exportar en Markdown y PDF son dos acciones reales, privadas. |
| D17 Teléfono | Todos los M-Asistente: Bienvenida / Historial; barras v114 | Asistente segunda pestaña; Inicio/billetera permanece; Difusiones y Recuperaciones pasan a Más donde existan. |
| D18 Orden | Asistente-* y sus M-*; no estado visual de dependencia | Se ejecuta después de E8 cerrada y copia su módulo; prueba de quitar ambos por separado. |
| D19 Modelos permitidos | Plat-Config: Elegir los modelos permitidos; Config-Org: Elegir el modelo / Con su clave | Plataforma-key restringida; own-key todo catálogo activo compatible. |
| D20 Contador | Tres Asistente/M-*: Quedan 10 consultas / Bienvenida | Mostrar restante solo si ≤ 10; no mostrar contador permanente con 50. |
| D21 Ayuda | Personal: Respuesta (por qué no entra a Beta); tres Bienvenida/caja | Herramienta help_article para pasos + enlace; artículos es/en, no conocimientos externos. |
| D22 Legal | Tres Asistente/M-*: Primera vez | Aviso y Entendido persistido, además versiones nuevas Términos/Privacidad y gate legal vigente. |
| D23 Correos | Mensajes: Asistente: 80 % del tope (su clave), tope alcanzado (su clave), 80 % del tope (plataforma), tope alcanzado (plataforma) | Todos los Dueños activos con Email verificado para own-key; soporte de Config-Plat para platform-key. Sin receptor OwnKey: Config-Org exige correo/Mi cuenta (complemento 0049). Email/outbox es/en, sin plantilla Meta. |
| D24 Títulos | Tres Asistente: Bienvenida/Respuesta/menú Renombrar; M-*: Historial/Renombrar | Los títulos del historial muestran el resultado; generación no tiene estado propio. Modelo elegible más barato, después de primera respuesta, costo registrado. |
| D25 Ayuda repo | Bienvenida/Respuesta, sin pantalla de edición | docs/ayuda por área/tema/idioma y test de cobertura; manda sobre «contenido configurable» antiguo. |
| D26 Catálogo | Plat-Config: Catálogo de modelos / Agregar un modelo; M-*: Catálogo de modelos | Nombre, proveedor, ID, entrada/salida USD, activo; CRUD con versiones. No precios/arrays en front. |

Inventario de estados a comparar en galería: Org tiene 18 valores de asistente; Personal/Plat 16 cada uno. M-Org 16, M-Personal/Plat 14 cada uno. Incluir también Historial plegado, Detenida, Falló la respuesta, Renombrar, Borrar y ambos Sin configurar; mobile usa Historial/Acciones. Config-Org tiene 7 estados y Uso-Org 5; Plat-Config 7 y Plat-Uso 2, con sus variantes M-* más reducidas. Lo que no está en un Tweak móvil sigue la pieza compartida del estado desktop, sin flujo nuevo.

## Relación con la otra etapa

E8 deja composición por capa, contribuidor EF neutral, guardas/receta de quitar, capacidades de `/api/me`, Módulos existente de Plataforma, navegación/Administración/barra móvil compartidas y outbox/correos cifrados. E8b **extiende** esos puntos: no los reemplaza ni importa clases de WhatsApp. Un contribuidor/registro no puede borrar el otro.

E8b no cambia Phone, países, OTP, principal/quitar sin reauth, cookies pública/Plataforma, legal/registro, recuperación ni LoginLink/state/PKCE. No mueve `/login/enlace`, webhook ni callbacks de Meta, ni añade IA al bot. El enlace del bot entra como persona para cuentas comunes; operador recibe texto neutro sin enlace. E8b conserva ese test, no emite enlaces ni cambia posesión/cookies. La baja ejecuta participantes de ambos módulos; ningún participante abre UoW/ITenantScope propios.

Asistente activo añade Administración › Asistente › Configuración/Uso, Inicio de Plataforma en `/plataforma`, Organizaciones en `/plataforma/organizaciones`, y segunda pestaña móvil. **Sin el módulo o sin habilitación, todo vuelve a la conducta y navegación E8 actual**. El registro modular de rutas permite retirar la contribución; no quedan imports a módulos en wrappers del núcleo. Los avisos de costo siguen solo email; los de identidad WhatsApp conservan su catálogo y outbox. La integración IA con atención E11 queda para después.

## Orden y dependencias

`E8 cerrada → T00 → T01 (contrato commiteado B/F) → (T02–T09 Back ∥ T10–T13 Front) → T14 → T15`.

P1–P3 están resueltas por 0049. T01 congela el puerto de interacciones con vacío explícito, validación de correo de Dueño para OwnKey y cuotas compartidas UTC. Los estados visuales complementarios se dibujan antes de sus tareas usando las piezas existentes; no se cambia el producto aprobado.

## Parte 1 · Contrato

### T00 · Confirmar E8 y fijar las decisiones técnicas

**Archivos:** este plan (Cambios al contrato solo si hace falta), `B/Directory.Packages.props`, `A/ArquitecturaBaseMultitenant.Application.csproj`, `I/ArquitecturaBaseMultitenant.Infrastructure.csproj`, `F/package.json` y `package-lock.json`; `AR/ApplicationPackagesTests.cs`, `ProjectReferencesTests.cs`; ficha `B/docs/rules/capas-y-flujo.md` si una convención necesaria no existe.

**Copiar de:** cierre E8/informe y ModuleIsolationTests; paquetes centralizados existentes; importación lazy y temas actuales del front.

- [ ] Confirmar cierre de E8 y sus hashes, sin 501 ni puerta Meta pendiente. Leer los AGENTS/fichas. Preservar cambios/procesos ajenos.
- [ ] Fijar versiones verificadas arriba. Application admite **solo** AI.Abstractions, por ADR0048; añadir esa entrada exacta a ApplicationPackagesTests y una guarda que restringe tipos AI al namespace del módulo. Domain sigue sin paquetes, SDK/AI/PDF en Infrastructure. No ampliar a cualquier Microsoft.Extensions.*.
- [ ] Elegir helpers con sufijos vigentes: `AssistantTurnRequester`, `AssistantLimitGuard`, `AssistantPromptBuilder`, `AssistantTitleIssuer`, `AssistantToolResolver`; no copiar Runner/Catalog como helper de Services ni ampliar Processor (reservado a la baja de cuenta). El catálogo vive en Tools, el PDF en Infrastructure.
- [ ] Aplicar P1–P3 de 0049 y el informe de alineamiento; confirmar recepción de las extensiones de guardas/DI/EF de E8. No reabrir D1–D26. Restore/build afectados al ejecutar; este documento no prueba compatibilidad por haber consultado registros.

**Tests que importan:** paquetes/capas/DI, build afectado; ninguno por mero cambio de texto. **Hecho cuando:** E8 cerrada y dependencias compatibles/fijadas, con decisiones dependientes resueltas antes del freeze.

### T01 · Publicar HTTP, SSE, permisos, puertos y modelos

**Archivos:** `B/docs/contracts/openapi.json`; `P/Modules/Assistant/{AssistantApiModule.cs,Controllers/*AssistantController.cs,Contracts/*HttpRequest.cs,Contracts/AssistantStreamHttpResponse.cs}`; interfaces/modelos/validadores `A/Modules/Assistant/{Interfaces,Models,Validation}/`; puertos neutrales detallados en T02/T07; `F/src/shared/api/generated/schema.d.ts` y `types.ts`; `B/docs/features/asistente.md` (contrato SSE). Esqueletos nuevos pueden devolver 501 solo en esta parte.

**Copiar de:** RolesController/contratos mapeados, OrganizationAuditController/CursorResult, PlatformFeaturesController/VersionResponse, el exportador MSBuild actual y `IT/Contracts/OpenApiContractTests.cs`. SSE es protocolo de Api, nunca Minimal API ni EF en controller.

- [ ] Declarar las tablas completas siguientes con responses, ejemplos, permisos, acceso y todos los errores es/en. Congelar tipos de eventos también en OpenAPI (`oneOf`, discriminator event) y regenerar F con `npm run contracts`.
- [ ] Contrato solo usa `owner`/tenant desde contexto firmado, nunca body; neutralizar las contribuciones de módulos, control de instalación y streaming como se describe abajo. No hacer que Mi cuenta importe Assistant.
- [ ] Commits B/F y hashes del contrato iguales antes del paralelo. Las guardas de rutas pasan incluso con 501; la parte 2 los elimina.

**Tests que importan:** OpenApiContractTests, AccessDeclarationTests, PermissionAuthorizationTests, ModuleControllersTests y contratos del front. Cubrir acceso Consumer sin roles, gestión accesible sin clave, errores por campo y variantes SSE/replay tipadas, sin snapshots de implementación. **Hecho cuando:** ambas partes pueden implementar sin inventar rutas, eventos, permisos, cuerpos ni errores y todos los esquemas están commiteados.

### Accesos y permisos

`{lado}` se materializa en tres controllers: `me`=[Access(Consumer)], `organization`=[Access(Business)]+assistant.use, `platform`=[Access(Platform)]+platform.assistant.use. Consumer no tiene roles ni HasPermission: uso personal implícito, con habilitación global. Business siembra assistant.use en **todos** los roles de sistema y assistant.configure solo en Dueño; Plataforma añade use/configure/read al catálogo individual, sin roles de operador ni concederlos automáticamente a operadores existentes. Textos en Permissions.resx y .en.resx.

Chat Business/Consumer exige `[FeatureGate("assistant")]`, con clave real de catálogo Both apagada por defecto; Consumer lee habilitación de configuración global, no filas inventadas por persona. Platform chat usa disponibilidad de instalación/emergencia y su permiso. Los endpoints de **gestión de Plataforma** deben seguir accesibles para configurar una clave ausente/habilitar módulo: gate neutral de instalación + permisos configure/read/tenants.manage, sin exigir que el chat ya esté habilitado. Actualizar ModuleControllersTests para validar **esta categoría de metadatos**, no exceptuar una lista de controllers por nombre. Módulo retirado: sus rutas no existen; módulo apagado: chat/Business settings/usage 404. `/api/me` solo añade assistant a features cuando efectivo; datos de capacidades de módulos de Plataforma van por un puerto neutral, sin comprobar una cadena Assistant en ProfileService.

### Rutas y cuerpos

Todas las mutaciones de creación/envío llevan Idempotency-Key UUID; todas las ediciones/borrados version uint/xmin. JSON estricto, UTC con Z, DateOnly ISO, Money `{amount:decimal,currency:"USD"}`, null para vacío. Los nombres son el contrato definitivo propuesto, no nombres heredados automáticamente de §5 de la propuesta.

Las respuestas privadas idempotentes **no se copian al store global**: creación de conversación, quick y upload usan el nuevo modo neutral `DurableReference` (JSON); messages usa `DurableStream`. El binding global contiene scope/hash/estado/refKind/refId/status/Location, sin título, mensajes, QuickResult, nombre de archivo ni bytes. El resolver reconstruye el JSON desde el dato privado después de revalidar dueño/lado/tenant, legales, módulo y permisos; ajeno o eliminado responde 404. La respuesta repetida conserva sus IDs y éxito original, sin ejecutar de nuevo la herramienta, guardar otro archivo ni invocar IA. `stop`/`notice` sin cuerpo de respuesta pueden conservar el modo existente; también revalidan autorización en replay.

**Actor del trabajo durable:** el turno persiste `{ownerUserId,access,tenantId?,culture,timeZoneId}` derivados del contexto firmado en la admisión, nunca del body/herramienta. No almacena token ni copia permisos. El worker abre un alcance neutral `IExecutionActorScope.Enter(ExecutionActor)` antes de resolver los servicios y un `ITenantScope` independiente para Business/Consumer, antes de UoW. `ICurrentUser` toma la identidad explícita de ese alcance solamente cuando no hay HttpContext; sin request ni alcance sigue devolviendo null. Relee cuenta, acceso/membresía y permisos actuales antes de cada herramienta (`IPermissionService` / `IPlatformPermissionService`), además de notice/legal/módulo. El operador permanece global. Dispose/finally restaura ambos contextos y cada trabajo usa su scope DI; no simular HttpContext, sesión nueva ni principal con permisos elevados.

| Método/ruta | Permiso extra al uso del lado | Petición | Éxito |
|---|---|---|---|
| GET `/api/{lado}/assistant` | uso del lado | — | 200 `AssistantState`: configured, canConfigure, models, quickQueries, dailyLimit, dailyResetAtUtc, remainingMessages (null si >10), monthlyBudget/usedCost (null sin presupuesto org), noticeVersion/noticeAccepted, retentionDays, maxAttachmentBytes=10485760, maxAttachments=5. Apagado→404, habilitado sin clave→configured=false. |
| POST `/api/{lado}/assistant/notice` | uso | `{noticeVersion}` + key | 204; Entendido por usuario/versión, tras aceptar documentos legales vigentes; no registra ese consentimiento por inferencia. |
| GET `/api/{lado}/assistant/conversations` | uso | `cursor?,limit?=20,saved?`, máximo 100 | 200 CursorResult<ConversationSummary>; orden LastActivityAtUtc/Id descendente, cursor ligado al lado/tenant/owner/filtro. |
| POST `/api/{lado}/assistant/conversations` | uso | `{}` + key | 201 `{id,title:null,isSaved:false,version,createdAtUtc,lastActivityAtUtc}` + Location. Sin llamada IA. |
| GET `/api/{lado}/assistant/conversations/{id}` | uso | `cursor?,limit?=50` | 200 `{conversation:summary,messages:CursorResult<Message>}` en orden cronológico; se revalida propiedad. |
| PUT `/api/{lado}/assistant/conversations/{id}` | uso | `{title:string|null,isSaved:bool,version}` | 200 VersionResponse; renombrar/Guardar/Dejar de guardar. Guardar no modifica owner/tenant. |
| DELETE `/api/{lado}/assistant/conversations/{id}` | uso | `?version=` | 204; elimina textos/adjuntos y cancela turno activo; limpieza física diferida fiable. |
| POST `/api/{lado}/assistant/conversations/{id}/messages` | uso | `{text:string,attachmentIds:uuid[],conversationVersion:uint}` + key | 200 text/event-stream primera vez; replay terminal 200 application/json `TurnResult`, sin invocar modelo ni streaming. En curso409; preflight falla antes de abrir stream. |
| POST `/api/{lado}/assistant/conversations/{id}/turns/{turnId}/stop` | uso | `{}` + key | 204; cancela idempotentemente; resultado durable Stopped. También RequestAborted pide stop. |
| POST `/api/{lado}/assistant/quick/{key}` | uso + permisos de herramienta | `{conversationId?:uuid,version?:uint}` + key | 200 `{conversation:summary,messages:[user,assistant],result:QuickResult}`; crea conversación si falta; sin proveedor/título IA/cuota/costo. |
| PUT `/api/{lado}/assistant/conversations/{id}/messages/{messageId}/feedback` | uso | `{rating:"up"|"down"|null,version}` | 200 VersionResponse; solo respuesta assistant propia, no prompt ni detalle libre. |
| POST `/api/{lado}/assistant/conversations/{id}/attachments` | uso | multipart **un archivo** `file`, `conversationVersion`; key | 201 `{id,fileName,mediaType,byteLength,version}`; privado, todavía no consumido por mensaje. No devuelve URL pública/key de storage. |
| GET `/api/{lado}/assistant/conversations/{id}/attachments/{attachmentId}` | uso | — | 200 binario, Content-Type verificado, Content-Disposition attachment, Cache-Control no-store. |
| DELETE `/api/{lado}/assistant/conversations/{id}/attachments/{attachmentId}` | uso | `?version=` | 204 si pendiente; si ya es de un mensaje, se elimina con la conversación. |
| GET `/api/{lado}/assistant/conversations/{id}/export` | uso | `format=markdown|pdf` | 200 descarga privada .md/.pdf, no-store; solo conversación propia, sin tool payloads, claves ni bytes originales de adjuntos. |
| GET `/api/organization/assistant/settings` | assistant.configure | — | 200 `OrganizationAssistantSettings` |
| PUT `/api/organization/assistant/settings` | assistant.configure | `{mode:"PlatformKey"|"OwnKey",modelId,effort:"low"|"medium"|"high",dailyMessageLimit?,monthlyBudget?:Money,keyAction:"keep"|"replace"|"clear",apiKey?:string,version}` | 200 VersionResponse. Topes editables solo own-key; platform-key rechaza intentos de cambiarlos. Validar allowOwnKey/model/key y al menos un Dueño activo con método Email verificado utilizable antes de guardar OwnKey; sin él, Assistant.Settings.AlertRecipientRequired. |
| GET `/api/organization/assistant/usage` | assistant.configure | `month:YYYY-MM,groupBy=day|person,page=1,pageSize=20` | 200 UsagePage, resumen mensual + página de filas/costo, sin texto ni conversationId. |
| GET `/api/platform/assistant/settings` | platform.assistant.configure; gestión | — | 200 PlatformAssistantSettings |
| PUT `/api/platform/assistant/settings` | platform.assistant.configure; gestión | `{defaultModelId,allowedModelIds:uuid[],effort,dailyMessageLimit,monthlyOrganizationBudget:Money,personalEnabled,retentionDays,keyAction,apiKey?,version}` | 200 VersionResponse; personal default false, día 50, retención de 30 días; clave write-only. |
| GET `/api/platform/assistant/usage` | platform.assistant.read; gestión | `month:YYYY-MM,page=1,pageSize=20` | 200 UsagePage por organización + Personas/Operadores; costo platform-key separado de own-key. Sin abrir tenant privado ni historial. |
| GET `/api/platform/assistant/models` | platform.assistant.configure; gestión | `page,pageSize,search?,active?` | 200 PagedResult<ModelPrice> |
| POST `/api/platform/assistant/models` | mismo; gestión | `{name,provider:"Anthropic",providerModelId,inputPricePerMillion:Money,outputPricePerMillion:Money,isActive}` + key | 201 `{id,version}` |
| PUT `/api/platform/assistant/models/{id}` | mismo; gestión | campos de alta + `version` | 200 VersionResponse; unicidad provider/ID y revalidación de opciones elegidas. |
| GET `/api/platform/tenants/{tenantId}/assistant/policy` | platform.tenants.read; gestión | — | 200 `{allowOwnKey:bool,version}`; false/version0 si no existe. |
| PUT `/api/platform/tenants/{tenantId}/assistant/policy` | platform.tenants.manage; gestión | `{allowOwnKey,version,reason}` | 200 VersionResponse + SecurityEvent, sin leer/devolver clave. Cambio de política revalida configuración/turnos. |
| GET `/api/platform/tenants/{tenantId}/features` y PUT `/api/platform/tenants/{tenantId}/features/{key}` | permisos vigentes | contrato **existente** de Módulos, source/expiry/version/reason; key=assistant | Existente; Asistente es contribución al catálogo. No reemplazar rutas, cuerpo ni trial de WhatsApp/futuros módulos. |

No ruta nueva para editar ayuda (D25), no endpoint público de adjuntos, no herramientas de escritura, no borrado de modelos referidos: desactivar es el mecanismo dibujado. Los endpoints existentes legales y de `/api/me` mantienen sus cuerpos y solo capacidades aditivas neutrales.

### Esquemas de respuesta y SSE

`ConversationSummary={id,title:null|string,isSaved,version,createdAtUtc,lastActivityAtUtc}`. `Message={id,role:"user"|"assistant",text,status:"Complete"|"Stopped"|"Failed"|"Generating",createdAtUtc,version,attachments:[AttachmentSummary],links:[AssistantLink],rating:null|"up"|"down"}`; el historial propio puede incluir tokens/modelId, nunca mensajes de sistema ni resultados crudos de herramientas. `AssistantLink={routeKey,parameters,query,labelKey}` solo de la lista permitida de pantallas; front arma href desde manifiesto y exige permiso. No aceptar URL o HTML que invente el modelo.

`ModelPrice={id,name,provider,providerModelId,inputPricePerMillion:Money,outputPricePerMillion:Money,isActive,version}`. Settings devuelven mode/modelId/effort/topes/allowOwnKey/hasAlertRecipient (solo settings org)/retention/version y **solo** `hasKey`/`maskedKeySuffix` (últimos 4), jamás apiKey/encryptedKey. State.models tiene solo opciones activas elegibles. `UsagePage={month,cost:Money,platformKeyCost?:Money,budget?:Money,totalQueries,peopleCount,rows:PagedResult<UsageRow>}`; UsageRow organización/persona/día contiene claves/nombre mostrado, consultas/tokens/costo/topes, ningún título ni archivo.

`QuickResult={text,links:[AssistantLink],data:proyección tipada de la herramienta}`; no estructuras arbitrarias de Domain ni ValueObjects sin mapeo. `TurnResult={turnId,message:Message,conversationVersion,usage:{inputTokens,outputTokens,cacheWriteTokens,cacheReadTokens,cost:Money,remainingMessages,monthlyUsedCost?:Money}}`. Usar esquemas para cada QuickResult en OpenAPI, no object libre que obligue any en F.

SSE usa UTF-8, eventos nombrados, datos JSON tipados y `id=<turnId>:<sequence>`; `Cache-Control:no-store`, flush por fragmento, sin buffering de proxy/Vite. No EventSource anónimo: POST con httpClient/stream transport y bearer en memoria. Heartbeat comentario cada 15 s; termina con done o error, y cierra.

| Evento | Datos | Conducta |
|---|---|---|
| `start` | `{turnId,userMessageId,assistantMessageId,conversationVersion}` | IDs persistidos antes del primer delta. |
| `delta` | `{turnId,sequence,text}` | Texto acumulativo por fragmento, solo respuesta, nunca argumentos de herramientas. |
| `tool` | `{turnId,toolKey,state:"running"|"complete"|"failed",labelKey}` | Estado localizado tipo Consultando usuarios; sin resultados/PII/argumentos. |
| `title` | `{conversationId,title,version}` | Título auto si no renombró el usuario; si llega después del stream, query de historial lo recupera. |
| `done` | `TurnResult` | Resultado durable Complete o Stopped, cuota/costo definitivo. |
| `error` | `{turnId?,code,traceId,retryAfter?:int,status:"Failed"}` | Tras abrir HTTP 200, nunca cambiar status HTTP ni incrustar ProblemDetails textual; guardar Failed con parcial/uso conocido. |

RequestAborted no deshace mensaje ya admitido: solicita stop y finaliza con token interno acotado. Reabrir conversación lee Stopped/parcial desde BD. Crash de worker recupera lease y marca Failed si una llamada pudo facturarse; no vuelve a facturar automáticamente. Reintentar en UI es **un turno nuevo** con key nueva; redelivery/red es misma key y mismo cuerpo. No prometer exactamente una llamada externa si el proveedor aceptó y cayó el proceso; reservar costo y registrar esa incertidumbre.

### Errores y textos

ProblemDetails pre-stream con code/traceId y errors de campos; códigos post-stream en `error`. Publicarlos en OpenAPI/resources es/en, sin textos del proveedor ni prompt/key/archivo en detail. 401/403 de permisos, acceso y legales, validación común, Idempotency-Key requerido/reutilizado (422)/en curso 409 y `General.ConcurrencyConflict`409 conservan catálogo vigente.

| HTTP / código | Campo/uso | Español / inglés |
|---|---|---|
| 404 `Http.NotFound` | módulo apagado/retirado | Texto común vigente; `Assistant.Module.Disabled` solo diagnóstico interno, sin revelar módulo en HTTP. |
| 404 `Assistant.Conversation.NotFound` | id/ajena | «No encontramos esa conversación.» / “Conversation not found.” |
| 404 `Assistant.Attachment.NotFound` | archivo ajeno/inexistente | «No encontramos ese adjunto.» / “Attachment not found.” |
| 400 `Assistant.Settings.NotConfigured` | chat sin clave válida | «El asistente todavía no está configurado.» / “The assistant is not configured yet.” |
| 400 `Assistant.Settings.KeyRejected` | apiKey | «La clave no es válida.» / “The key is invalid.” |
| 403 `Assistant.Settings.OwnKeyNotAllowed` | mode | «La plataforma no habilitó tu propia clave.» / “The platform has not enabled your own key.” |
| 400 `Assistant.Settings.AlertRecipientRequired` | mode | «Agregá y verificá un correo de un Dueño para usar tu clave.» / “Add and verify an owner's email to use your own key.” Acción: Mi cuenta; no se guarda OwnKey. |
| 400 `Assistant.Model.NotAllowed` | modelId | «Elegí un modelo habilitado.» / “Choose an enabled model.” |
| 409 `Assistant.Model.AlreadyExists` | providerModelId | «Ese modelo ya está en el catálogo.» / “That model is already in the catalog.” |
| 404 `Assistant.Model.NotFound` | id | «No encontramos ese modelo.» / “Model not found.” |
| 400 `Assistant.Settings.PlatformLimitsReadOnly` | topes platform-key | «Estos topes los fija la plataforma.» / “These limits are set by the platform.” |
| 400 `Assistant.Notice.AcceptanceRequired` | noticeVersion | «Leé el aviso antes de tu primera consulta.» / “Read the notice before your first query.” |
| 429 `Assistant.Limit.DailyReached` | Retry-After hasta 00:00Z siguiente | «Llegaste al tope de consultas de hoy.» / “You reached today's query limit.” |
| 429 `Assistant.Limit.MonthlyReached` | Retry-After hasta próximo mes | «Llegaste al tope del mes.» / “You reached the monthly limit.” |
| 409 `Assistant.Turn.InProgress` | conversación | «Esperá o detené la respuesta actual.» / “Wait for or stop the current response.” |
| 404 `Assistant.Turn.NotFound` | turnId/ajeno | «No encontramos esa respuesta.» / “Response not found.” |
| 400 `Assistant.Attachment.UnsupportedType` | file | «Adjuntá una imagen o un PDF.» / “Attach an image or a PDF.” |
| 413 `Assistant.Attachment.TooLarge` | file | «El archivo pesa más de 10 MB.» / “The file is larger than 10 MB.” |
| 400 `Assistant.Attachment.TooMany` | attachmentIds | «Podés adjuntar hasta 5 archivos por mensaje.» / “You can attach up to 5 files per message.” |
| 409 `Assistant.Attachment.AlreadyUsed` | attachmentIds/borrar | «Ese adjunto ya es de un mensaje.» / “That attachment already belongs to a message.” |
| 400 `Assistant.QuickQuery.NotAvailable` | key o permisos cambiados | «Esa consulta no está disponible.» / “That query is unavailable.” |
| 503 `Assistant.Provider.Unavailable` | reintentable, Retry-After | «El asistente no responde ahora.» / “The assistant is unavailable now.” |
| 400 `Assistant.Provider.ContextTooLarge` | message | «La conversación o los adjuntos son demasiado grandes. Empezá una nueva.» / “The conversation or attachments are too large. Start a new one.” |
| 500 `Assistant.Export.Failed` | descarga | «No pudimos exportar la conversación.» / “We could not export the conversation.” |

Errores de archivo inválido, text vacío/largo, título largo, month inválido, precios negativos/no USD, IDs duplicados, modelo inactivo y version requerida son `Validation.Failed` con mensajes propios por campo. Herramienta fuera de permisos devuelve el código 403 vigente; el modelo recibe denegación genérica, no datos. Módulo/permiso se revalida en replay y cada llamada de herramienta.

### Texto listo para el chat de Contrato

```text
Ejecutá SOLO Parte 1 (T00–T01) del plan C:\Users\ezequ\source\repos\ArquitecturaBaseMutitenant\docs\plans\2026-10-08-etapa-8b-asistente.md. E8 debe estar cerrada. Leé Forma de trabajo, ADR0048, tabla Decidido con el usuario D1–D26 y v114. Confirmá dependencias del plan; fijá HTTP/permisos/esquemas/SSE/errores y puertos neutrales completos en OpenAPI con guardas verdes, pudiendo dejar 501 nuevos temporalmente. Aplicá ADR0049 y el informe de alineamiento antes del freeze; no inventes interacciones productivas ni topes. Regenerá y commiteá tipos en el front hermano. No implementes asistentes/pantallas ni arranques otros chats. Todo main sin push y commits solo rutas propias. Devolvé hashes B/F y contrato congelado para los dos chats paralelos; no cierres E8b.
```

## Herramientas y consultas fijas exactas de v1

Cada herramienta implementa `IAssistantTool` en `A/Modules/Assistant/Tools/`, declara key, lado, permiso, esquema cerrado y resource de descripción. El catálogo entrega solo las disponibles; **cada ejecución vuelve a autorizar**, incluso si el permiso cambió durante streaming. No confiar en el filtro inicial ni en HasPermission del controller para llamadas internas a servicios. Usar el evaluador de autorización Application existente, mismo companyId/tenant/usuario que en pantalla, sin construir un principal privilegiado.

Esquemas comunes: `page` desde 1, `pageSize` máximo 20; texto de búsqueda normalizado/máximo 100; UUIDs, enums existentes y rangos de fecha validados. Todas las salidas tienen como máximo 20 filas/resumen/links, flags de más resultados; continuar solo con página/cursor validado. No SQL libre, expresiones, URLs, UserId/TenantId elegidos por el modelo, dump de entidades ni `IQueryable`. Buscar una persona significa **miembros de la organización**, nunca cuentas globales.

| Lado / key y archivo `.cs` | Parámetros específicos / servicio real a copiar | Permiso adicional a usar asistente |
|---|---|---|
| Business `org_users` / Business/OrganizationUsersTool | `status?,page,pageSize`; IOrganizationUserService.ListAsync/GetFilterCountsAsync | users.read |
| Business `org_find_person` / Business/FindOrganizationPersonTool | `search,page,pageSize`; mismo servicio, búsqueda de miembros | users.read |
| Business `org_roles` / Business/OrganizationRolesTool | `roleId?,companyId?,page,pageSize`; IRoleService.ListAsync/GetAsync/GetPermissionsAsync, con scope válido | roles.read; companyId no concede acceso por sí solo |
| Business `org_role_members` / Business/RoleMembersTool | `roleId,page,pageSize`; lectura de asignaciones de IOrganizationUserService/reader, por rol existente dentro del tenant | roles.read **y** users.read |
| Business `org_companies` / Business/OrganizationCompaniesTool | `status?,search?,page,pageSize`; ICompanyService.ListAsync/GetFilterCountsAsync | companies.read |
| Business `org_company_members` / Business/CompanyMembersTool | `companyId,status?,page,pageSize`; ICompanyMembershipService.ListAsync | company.members.read para **esa** empresa (evaluador AnyCompany/SpecificCompany vigente) |
| Business `org_invitations` / Business/OrganizationInvitationsTool | `status:Pending|Expired,page,pageSize`; proyección del listado de Usuarios/invitaciones, no IInvitationService.Preview público | users.read; no exige users.invite para leer ni puede reenviar |
| Business `org_audit` / Business/OrganizationAuditTool | `cursor?,fromUtc?,toUtc?`; IOrganizationAuditService.ListAsync | audit.read |
| Business `org_pending` / Business/OrganizationPendingTool | `{}`; IHomeService.GetAsync, proyección PendingItems | assistant.use; cada IPendingItemSource exige además su permiso real de resolver |
| Platform `platform_organizations` / Platform/PlatformOrganizationsTool | `status?,page,pageSize`; ITenantAdministrationService.ListAsync/CountAsync | platform.tenants.read |
| Platform `platform_find_organization` / Platform/FindPlatformOrganizationTool | `search,page,pageSize`; mismo servicio global | platform.tenants.read |
| Platform `platform_accounts` / Platform/PlatformAccountsTool | `status?,search?,page,pageSize`; IPlatformAccountService.ListAsync/CountAsync | platform.accounts.read |
| Platform `platform_recoveries` / Platform/PlatformRecoveriesTool | `status=Pending,page,pageSize`; IPlatformRecoveryService.ListAsync | platform.recoveries.manage; nunca llama DecisionService |
| Platform `platform_security` / Platform/PlatformSecurityTool | `cursor?,fromUtc?,toUtc?`; IPlatformAuditService.ListAsync, metadatos permitidos | platform.audit.read |
| Platform `platform_assistant_usage` / Platform/PlatformAssistantUsageTool | `month,page,pageSize`; IAssistantUsageService.ListPlatformAsync, global agregada | platform.assistant.read |
| Consumer `my_account` / Personal/MyAccountTool | `{}`; IProfileService.GetAsync + IAccountLoginMethodsService.ListAsync | Consumer propio implícito; proyección enmascarada **antes** de enviar al modelo, sin Value ni backup completo |
| Consumer `my_organizations` / Personal/MyOrganizationsTool | `{}`; Organizations de IProfileService.GetAsync | Consumer propio; no consumir directamente IUserTenantAccessReader fuera de su lista blanca |
| Consumer `my_interactions` / Personal/MyInteractionsTool | `page,pageSize`; puerto neutral IConsumerInteractionSource por módulo productivo, con permisos/gates propios | Consumer propio/PartyPolicy y feature de cada fuente; sin fuentes devuelve vacío explícito |
| Los tres `help_article` / HelpArticleTool | `topicKey?,search?`, máximo3 artículos; IHelpArticleReader, filtro por lado/ruta/permisos | uso del lado y permisos de la pantalla explicada; artículo no habilita datos |

Los servicios actuales no tienen todos los filtros de roles/invitaciones de esas consultas. Añadir **lecturas tipadas** a sus readers/servicios propios, conservando permisos, filtros, paginado e índices; no extraer todo y filtrar en Assistant, ni usar métodos que escriben para leer. Las herramientas comparten mecanismos de autorización con HTTP. Proyecciones contienen únicamente datos ya visibles para ese usuario: teléfono/email de terceros según el DTO de pantalla, métodos de mi cuenta siempre tapados. Tokens, secretos, IP completa, cuerpos de SecurityEvent y adjuntos de otras áreas no se envían a Anthropic.

Las herramientas de v1 son exactamente las 9 Business, 6 Platform, 3 Consumer y `help_article` común de esta tabla; no añadir herramientas de WhatsApp E11 ni llamar endpoints web desde el modelo. Contexto/adjuntos/artículos/resultados son **datos no confiables**, nunca instrucciones de sistema. Prompt es/en fija alcance del sistema, prohíbe escribir y dice que si falta herramienta lo explique.

| Lado / consulta fija key | Rótulo de Bienvenida v114 | Herramienta/filtro exacto | Permiso |
|---|---|---|---|
| Business `active_users` | Usuarios activos | org_users, status=Active; resumen/conteo | users.read |
| Business `expired_invitations` | Invitaciones vencidas | org_invitations, status=Expired | users.read |
| Business `owners` | Quiénes son Dueños | org_role_members, rol de sistema Dueño identificado por clave estable del catálogo de Roles | users.read + roles.read |
| Business `recent_changes` | Últimos cambios | org_audit, últimos20 | audit.read |
| Platform `pending_organizations` | Organizaciones por aprobar | platform_organizations, estado pendiente vigente | platform.tenants.read |
| Platform `pending_recoveries` | Recuperaciones pendientes | platform_recoveries, Pending | platform.recoveries.manage |
| Platform `today_security` | Seguridad de hoy | platform_security, rango UTC del día de la cultura/zona efectiva del operador con ITimeZoneService | platform.audit.read |
| Platform `assistant_usage` | Uso del asistente | platform_assistant_usage, mes actual | platform.assistant.read |
| Consumer `my_organizations` | Mis organizaciones | my_organizations | Consumer propio |
| Consumer `my_signin` | Cómo entro a mi cuenta | my_account, métodos tapados + artículo de ingreso | Consumer propio |
| Consumer `my_messages` | Mis mensajes con empresas | my_interactions; sin fuente, «Todavía no tenés mensajes con empresas», sin enlace | Consumer propio y fuente habilitada |

State.quickQueries ya viene filtrado por permisos; repetir chequeo al ejecutar. Texto/plantilla determinista en resources es/en con DisplayFormatter, datos tipados y enlaces reales filtrados, sin LLM ni título automático ni sumar a cuota o costo. Funciona aun al agotar cuotas del modelo. Reusar key/nonce de mutación evita duplicar mensajes/historial. No poner «quién entró esta semana» como atajo nuevo solo porque sea un título de historial de ejemplo.

## Parte 2a · Back

### T02 · Módulo, instalación, permisos y persistencia privada

**Archivos:** `D/Assistant/{AssistantFeature,AssistantConversation,PlatformAssistantConversation,AssistantMessage,PlatformAssistantMessage,AssistantAttachment,PlatformAssistantAttachment,AssistantTurn,PlatformAssistantTurn,AssistantOrganizationSettings,AssistantPlatformSettings,AssistantOrganizationPolicy,AssistantUsageDaily,AssistantPlatformUsageDaily,AssistantModelPrice,AssistantNoticeAcceptance,AssistantErrors}.cs`; `A/Modules/Assistant/{AssistantModule.cs,Interfaces/I*Service.cs,Interfaces/I*Repository.cs,Interfaces/I*Reader.cs}`; `I/Modules/Assistant/{AssistantModule.cs,Persistence/AssistantModelContributor.cs,Persistence/{Configurations,Repositories,Readers}/*.cs}`; `P/Modules/Assistant/AssistantApiModule.cs`; composición `P/Program.cs`; migración nueva `<timestamp>_AssistantModule.cs` y snapshot.

Puntos neutrales a extender: `A/Interfaces/Integrations/Features/{IFeatureDefinitionContributor,IFeatureAvailabilityPolicy,IModuleCapabilityContributor}.cs`; `I/Features/{FeatureCatalog,FeatureService}.cs`, `P/Features/{InstalledModuleManagementAttribute,FeatureGateMiddleware}.cs`, `A/Models/Profile/MeResponse.cs` y su builder; `Domain/Authorization/{Permissions,PlatformPermissions}.cs`, resources/seed; guardas ModuleControllersTests/PermissionAuthorizationTests/ApplicationPackagesTests.

**Copiar de:** E8 AddWhatsAppModule/model contributor, Roles/RoleConfiguration/RoleRepository/RoleReader, PersonalWidgets del TestFeatures para mecánica privada, PlatformSettingsService/PlatformFeatureService/PlatformActionGuard, TenantFeatureFilter y guardas E7. No copiar tablas sintéticas a producción.

- [ ] Extender las guardas/DI neutrales de E8 por forma para Application.Modules.Assistant.Services/Interfaces. Configuración/uso/política de Plataforma llevan InstalledModuleRoute y permisos globales, sin TenantFeature de una organización; rutas Business/Personal llevan feature assistant y acceso correspondiente. Hoy ModuleControllersTests exige FeatureGate a todo controller fuera de su lista core: ampliar sus categorías neutrales, no eximir nombres de clase. Componer módulos en DependencyInjectionTests/ServiceProviderValidationTests y mantener controles de helper con UoW/controller con reader/sin autorización, máximo 8 dependencias y esquema global no declarado.
- [ ] Registro explícito por capa, una composición que se puede retirar; FeatureCatalog combina contribuidores por key única sin referencia a módulos. AssistantFeature declara assistant/Both/default false; el módulo implementa disponibilidad Consumer por personalEnabled, Business por TenantFeatures y Platform por instalación/emergencia/permiso. Gestión instalada de Plataforma no requiere chat configurado. Probar que política del Asistente no captura claves de otros módulos.
- [ ] La propuesta §4 sugería un esquema assistant, pero la regla canónica solo admite `tenant` para ITenantOwned y `platform`/`identity` para globales. **Decisión técnica del plan:** tablas `tenant.AssistantConversations/Messages/Attachments/Turns/Settings/UsageDaily` con ITenantOwned, filtro Tenant, RLS forzado, claves compuestas y tenant sellado; tablas `platform.PlatformAssistant*` para conversaciones/mensajes/adjuntos/turnos/uso del operador y configuración/catálogo/política globales. No tenant nullable mixto ni excepción de namespace. El contribuidor E8 declara exactamente los tipos globales del módulo; runtime/DesignTime/bootstrap/ArchitectureModel iguales.
- [ ] B2B y Personal: lectura **TenantId y OwnerUserId y lado**, incluso mismo usuario con accesos distintos. Platform global: OwnerUserId obligado en cada reader/repository, sin ruta administrativa de lectura de contenido. Métodos para worker/deletion son internos, con claves exactas/lease; no Readers genéricos que permitan omitir owner en HTTP. Mensajes/archivos tienen FK compuesta de conversación y heredan su tenant. No SET app.tenant_id arbitrario ni IgnoreQueryFilters para resolver un chat.
- [ ] Conversación/settings/model/adjunto/feedback versionados con xmin; cursor estable por fecha/Id. Texto de chat y títulos/filenames **no se auditan**: atributos NotAudited/entidad no auditable donde corresponda. Auditar solo metadatos/configuración sin apiKey, y SecurityEvent de política. Uso guarda tokens/costo, no texto ni argumentos/resultados. JSON tool metadata solo key/duración/estado.
- [ ] Modelo/catálogo global es dato de referencia aportado por módulo, no listas en F ni excepción genérica de referencias. Unicidad provider/providerModelId en UniqueKeys; actualizar validación/modelo de contribuidores sin tocar lista blanca con typeof Assistant en núcleo. Migración habilita EnableTenantRls e índices; mantener globales sin alcance Business.
- [ ] Permisos nuevos, es/en/roles de sistema; operador inicial recibe use/configure/read al crear su identidad según seed existente; operadores existentes se editan por permisos individuales, sin migración que los promueva solos.

**Tests:** nuevos `IT/Modules/Assistant/{AssistantPersistenceTests,AssistantFeatureTests,AssistantAuthorizationTests,AssistantConversationIsolationTests}.cs`, ampliar guardas citadas y ModuleIsolationTests. Dos tenants, dos personas, dos compañeros, Dueño y operador con permisos completos→404 chat ajeno; Consumer/Business/Platform cruzados; policies apagadas/expiry/cache y emergencia, config accesible sin clave, módulos independientes. **Hecho cuando:** persistencia/gates/privacidad pasan con rol runtime y núcleo sin referencias a Assistant/WhatsApp.

### T03 · Configuración, modelos y claves

**Archivos:** `A/Modules/Assistant/{Services/{AssistantSettingsService,AssistantSettingsWriter,AssistantModelService,AssistantModelWriter,AssistantPolicyService}.cs,Configuration/AssistantOptions.cs,Validation/*Validator.cs,Models/*Settings*.cs}`; `I/Modules/Assistant/{AnthropicChatClientFactory,AssistantKeyProtector,AssistantReferenceDataSeed}.cs`; `A/Modules/Assistant/Interfaces/IChatClientFactory.cs`; controllers/contratos de configuración/model/policy de T01; `B/docs/operations/configuracion.md`, `scripts/secretos/{secretos.plantilla.json,verificar.ps1}` (nombres, nunca valores).

**Copiar de:** A/Services/Platform/{PlatformSettingsService,PlatformSettingsAuditWriter}.cs, PlatformFeatureWriter/PlatformAuditWriter, DataProtection del Outbox, I/Persistence/Seed/ReferenceDataSeeder.cs y sus traducciones y contrato Currency. SDK oficial `AsIChatClient`; opciones/ValidateOnStart existentes. No inventar integración para una librería comunitaria.

- [ ] Clave de arranque `Assistant:Platform:ApiKey` por user-secrets/entorno, nunca repositorio/log. Configurable desde pantalla: guardar cifrada en BD, prevalece sobre secreto de arranque; keep no cambia, replace valida con petición mínima fuera de UoW y luego guarda con version, clear marca explícitamente sin clave (sin revivir el fallback al reiniciar). Sufijo4 solo en lectura; errores SDK sanitizados. Own-key independiente cifrada por tenant/purpose y no se descifra fuera del scope autorizado.
- [ ] Sin clave no falla toda la app: chat configured=false, atajos definidos/pending conservan disponibilidad según su contrato. SDK/Fake sólo se crean al necesitar turno. Sin registrar módulo, no workers/rutas/imports. Fake se rechaza al arrancar fuera de Testing o entorno E2E aislado explícito, nunca habilitado por body/header del usuario.
- [ ] Catálogo inicial desde [modelos](https://platform.claude.com/docs/en/about-claude/models/overview) y [precios oficiales](https://platform.claude.com/docs/en/about-claude/pricing) verificados al escribir: Fable5.1 (10/50), Opus5.5 (4/20), Sonnet5.5 (2/10), Haiku5.5 (0,10/0,50 con entrada hasta100k), USD por millón. Confirmar IDs/capacidades al sembrar, solo activos/disponibles para la API normal; no usar Batch/fast como precio normal. Catálogo es editable, seed idempotente **no pisa** cambios. Initial default Sonnet y esfuerzo low como tablero, permitidos Sonnet/Haiku; modelo activo ≠ modelo permitido con platform-key.
- [ ] Precios es/en y capacidades/tarifas de caché/tier como referencias versionadas para calcular estimaciones y reservas, no arrays en front. UI D26 mantiene sus columnas aprobadas. Conservar snapshot de tarifas por llamada: editar catálogo no reescribe costos viejos. La estimación puede ser conservadora cuando falta uso de caché; no mostrarla como factura de Anthropic.
- [ ] Platform-key: organización elige solo permitidos activos; own-key solo si allowOwnKey y cualquiera activo compatible. Inactivar/quitar permiso a un modelo no cae silenciosamente a otro más caro: no admitir turno hasta configuración válida. Quitar allowOwnKey deja own-key inutilizable, no usa ni factura platform-key por sorpresa; Config-Org muestra el modo permitido y puede guardar PlatformKey. Revalidar antes de cada turno y cambiar cachés después de commit.
- [ ] OwnKey exige al menos un receptor. Dueño es el rol existente `SystemRoles.TenantAdmin`, con cuenta/membresía activas y método Email verificado utilizable; no agregar flag Owner ni tomar el email del claim de Google como método verificado. Extender `A/Interfaces/Persistence/ITenantAdminReader.cs` y `I/Persistence/Readers/UserTenantAccessReader.cs` con lectura tipada de Dueños activos si falta, copiando sus consultas actuales. Sin receptor: hasAlertRecipient=false y error de contrato; revalidar antes de turnos OwnKey, sin fallback que facture Plataforma.
- [ ] Topes y retención por D10/D4; defaults del tablero: día 50, mes organización USD 100, retención de 30 días, personalEnabled=false. Own-key puede editar sus topes; modo plataforma solo lectura. Effort/ID sin free text; Provider Anthropic único v1. Política de tenant autorizada con PlatformActionGuard/SecurityEvent antes de Enter/UoW.

**Tests:** nuevos `AU/Modules/Assistant/AssistantSettingsTests.cs`, `IT/Modules/Assistant/{AssistantSettingsTests,AssistantModelCatalogTests,AssistantStartupTests}.cs`: key nunca en DTO/log/EF audit; clear/keep/replace concurrentes, datos cipher en BD, fallback reinicio, secretos/exportador OpenAPI offline, precios no pisados, catálogo/permiso/own-key, fake rechazado producción; OwnKey Phone-only rechazado con AlertRecipientRequired, un Dueño con Email habilita y Google-email sin método verificado no basta. **Hecho cuando:** pantallas pueden configurar sin recompilar, cada combinación tiene disponibilidad clara y costos anteriores conservan snapshot.

### T04 · Herramientas, consultas fijas y ayuda

**Archivos:** clases exactas de la tabla en `A/Modules/Assistant/Tools/{Business,Platform,Personal}/`, `IAssistantTool.cs`, `AssistantToolCatalog.cs`, `HelpArticleTool.cs`; `Services/{AssistantQuickService,AssistantQuickResponseBuilder,AssistantToolResolver,AssistantPromptBuilder}.cs`; `Resources/{Assistant,Assistant.en}.resx`; `A/Interfaces/Integrations/Help/IHelpArticleReader.cs`, `I/Help/RepositoryHelpArticleReader.cs`; `B/docs/ayuda/<area>/<tema>.{es,en}.md` y manifiesto `docs/ayuda/index.json`. Lecturas tipadas adicionales en servicios/readers de Usuarios/Roles si faltan filtros.

**Copiar de:** firmas reales y DTOs de la tabla, evaluadores de permisos del núcleo, CursorResult/PagedRequest, HomeService/IPendingItemSource/PartyPolicy. Artículos siguen textos del lienzo y features existentes, no conocimiento improvisado. No hacer HTTP loopback ni subir documentos completos de áreas.

- [ ] Implementar la lista exacta y esquema/permiso de cada herramienta; test de catálogo por lado y whitelist de métodos de lectura. Máximo 5 llamadas al modelo por turno y 20 filas por herramienta, DTO pequeño y sanitizado. Fijar `TextLimits.AssistantMessage=8000`, título=80, `MaxInputTokens=51200`, `MaxOutputTokens=2048` por llamada y título=64 tokens de salida. Son límites técnicos iniciales, no decisiones de producto pendientes. El presupuesto reserva esos máximos para todas las llamadas; cada ciclo revalida contexto y reserva antes de invocar. Contexto/adjuntos que excedan el límite devuelven ContextTooLarge, sin truncar silenciosamente preguntas ni archivos.
- [ ] Artículo solo visible si lado/pantalla/módulo/permisos lo permiten; metadatos topicKey/routeKey/permission, español e inglés obligatorios. Un markdown por tema; test cobertura de cada área con pantallas y paridad. D25 manda: no CRUD de ayuda ni vector DB/embeddings/RAG para v1.
- [ ] Ayuda explica el funcionamiento de WhatsApp E8 y métodos de ingreso sin pedir secretos ni OTP. La herramienta no llama a recuperación, registro, invitación, cambio de método ni transiciones. Datos de PDF/artículo jamás sustituyen system prompt. No leer código/repositorio arbitrario pedido por chat.
- [ ] Atajos: ejecutar herramienta directamente, renderizar respuesta localizada, persistir historial/mensajes/links, título determinista y binding privado de la operación; sin IChatClient, cuota ni costo. Replay usa DurableReference y reconstruye QuickResult autorizado, nunca serializa texto/datos en IdempotencyStore global. Si herramienta denegada, no encolar generación. P1 exige un puerto neutral sin consultar TestFeatures ni adelantar tablas de E11; sin fuente registra el resultado vacío bilingüe y ningún enlace ficticio.

- [ ] Crear puerto neutral `A/Interfaces/Integrations/Engagement/IConsumerInteractionSource.cs` y DTO de lectura propio acotado/paginado. El agregador puede tener cero contribuciones; cada fuente productiva revalida su gate/permiso y UserId derivado del servidor. Asistente consume el puerto sin importar otro módulo. Sin fuentes habilitadas, my_interactions/my_messages devuelven «Todavía no tenés mensajes con empresas.» / “You don't have any messages with businesses yet.”, sin URL, error, proveedor ni datos sintéticos. E11 registra su fuente después.

**Tests:** `AU/Modules/Assistant/{AssistantToolCatalogTests,AssistantToolsTests,AssistantQuickQueriesTests,AssistantHelpTests}.cs`; `IT/Modules/Assistant/AssistantToolAuthorizationTests.cs`, pruebas de aislamiento y lectura por empresa/rol. Revocar permiso durante turno, companyId ajeno, devolución enmascarada, contador Fake cero para 11 atajos y para el vacío personal, intentos de inyección en resultados/PDF y artículos. **Hecho cuando:** solo herramientas autorizadas devuelven datos, 11 consultas fijas tienen resultado real o vacío explícito de 0049, cada área tiene ayuda bilingüe.

### T05 · Conversaciones, turnos, SSE e idempotencia durable

**Archivos:** `A/Modules/Assistant/Services/{AssistantConversationService,AssistantConversationWriter,AssistantMessageService,AssistantTurnService,AssistantTurnRequester,AssistantTurnWriter}.cs`, modelos/validadores/interfaces/repositorios/readers de T01/T02; `I/Modules/Assistant/{AssistantTurnWorker,FakeChatClient,AssistantTurnStream}.cs`; `P/Modules/Assistant/{AssistantStreamResult.cs,Controllers/{PersonalAssistantController,OrganizationAssistantController,PlatformAssistantController}.cs}`; modo neutral en `P/Idempotency/{IdempotentAttribute,IdempotencyFilter,IIdempotentStreamResult,IIdempotentReferencedResult,IIdempotentReplayResolver}.cs`, stores existentes y contratos de replay; puertos `A/Interfaces/Integrations/Request/{IExecutionActorScope,IExecutionActorContext}.cs`, modelo `A/Models/Request/ExecutionActor.cs`, adaptador neutral `I/RequestContext/ExecutionActorContext.cs` y extensión `P/RequestContext/CurrentUser.cs`. No copiar un helper Runner con UoW.

**Copiar de:** Roles para UoW, outbox/lease de E8 para trabajo durable y dos réplicas; .NET10 SseItem/TypedResults desde controller; IdempotencyFilter actual para reserva/hash/scoping. El filtro actual captura **todo** en MemoryStream: eso retrasa SSE hasta el final y guardaría texto del chat en su tabla, por lo que no sirve sin adaptar.

- [ ] CRUD/cursor/guardar/renombrar/borrar con owner/tenant/lado/version, avisos legales y gate antes de idempotencia/replay. No permitir title auto pisar un nombre manual: compare-and-set por estado y version. Borrar cancela turnos antes de limpiar.
- [ ] Admitir turno en una sola UoW del método público: locks → relecturas → owner/legal/notice/gates/permisos/version/attachments → reserva diaria/costo → userMessage y assistantMessage Generating + turno/outbox de trabajo. Índice de un turno vivo por conversación y key/binding digest durables, sin guardar prompts en IdempotencyStore. Replays revalidan propiedad y estado; hash no incluye claves.
- [ ] Worker de Infrastructure coordina métodos públicos **separados** `ClaimAsync`, `CheckpointAsync`, `CompleteAsync`, cada uno con una UoW, y la llamada IChatClient fuera de toda transacción. No servicio que abre varias UoW ni helpers que reciben UoW. Scope por claim tenant conocido antes de UoW; operador global no entra a organización. Lease/SKIP LOCKED, timeout técnico, checkpoints parciales y stop durable; no Signal en memoria como fuente única.
- [ ] Aplicar el binding de actor del contrato: contexto AsyncLocal neutral con alcance anidado/restaurable, guardas para que un request HTTP no pueda sustituir su identidad por él y métodos usados solo por workers confiables. ITenantScope no aporta UserId/Access; el ICurrentUser actual depende de HttpContext y necesita esta adaptación para que Profile/Home funcionen en background. Resolver DI dentro del alcance, comprobar estado/membresía vigente y permisos en cada herramienta; suspensión, revocación o baja cancela generación antes de devolver datos. Nunca tomar OwnerUserId, access o tenant sugeridos por el modelo. Documentar el protocolo neutral y su test en ficha capas-y-flujo, sin imports de Assistant en núcleo.
- [ ] Api `AssistantStreamResult` lee stream del puerto, usa SSE nativo y flush sin buffer. Adaptar modos neutrales `[Idempotent(Mode=DurableStream)]` y `DurableReference`: ninguno captura/guarda el cuerpo; reservan identidad de operación y enlazan una referencia. Al terminal guardar **solo referencia/estado**; replay pide TurnResult/QuickResult/ConversationSummary/AttachmentSummary al servicio dueño después de autorizar. Introducir `IIdempotentReplayResolver` e `IIdempotentReferencedResult` neutrales, registrados por módulos; no referenciar Assistant desde filter. Para JSON, el resultado entrega referencia técnica al filtro antes de escribir cuerpo; SSE registra turno antes del primer delta. El modo JSON anterior queda idéntico para OTP/Meta/outbox y demás rutas. Ni SSE ni quick/upload pueden volcar prompts, respuestas, título o filename a tabla global.
- [ ] Cancelación/redelivery según contrato; finalizar con token interno acotado si el cliente se fue. Worker caído no repite llamada incierta; reaper Failed libera reserva no consumida, preserva gasto conocido/estimado conservador. Cambios de permisos/gate cancelan antes de nueva herramienta. Reintentar mensaje es nueva key, no reproduce automaticamente el anterior.
- [ ] FakeChatClient IChatClient guionado, determinista: tool call real a servicios, deltas/usage/errores/stop/título. Selección por entorno aislado; métricas contador de llamadas para demostrar replays/atajos. Nada de texto de prompt ni respuesta en LoggerMessage/OperationLog, telemetría generativa desactiva registro de contenido sensible/args/results y SDK logs de body.

**Tests:** `IT/Modules/Assistant/{AssistantConversationsTests,AssistantStreamingTests,AssistantIdempotencyTests,AssistantTurnRecoveryTests,AssistantWorkerActorTests}.cs`; ampliar `IT/Api/IdempotencyTests.cs` según ruta real y guards IdempotentActionsTests/TransactionBoundaryTests; `AU/Modules/Assistant/AssistantPromptTests.cs`. Primer delta antes de done y buffer desactivado; dos réplicas/key distinta/cuerpo distinto, aborto antes/después de admitido, mismo key terminal JSON, stop persistido, owner cambiado/permiso revocado, log capture sin texto. Quick/create/upload replay no duplica y no deja contenido/título/filename en IdempotencyStore. Forzar my_account/my_organizations/org_pending desde worker **sin HttpContext**, dos actores consecutivos y paralelos, cancelación/restauración sin contaminación de scopes y revocación entre llamadas. **Hecho cuando:** SSE inmediato y durable, herramientas ejecutadas con su actor vigente, reintento no duplica facturación y ninguna UoW engloba proveedor/stream.

### T06 · Topes, uso, títulos y alertas por correo

**Archivos:** `A/Modules/Assistant/Services/{AssistantLimitGuard,AssistantUsageService,AssistantUsageWriter,AssistantTitleIssuer,AssistantBudgetNotifier}.cs`; entidades/repositorios de uso/reserva/model snapshot/alert mark de T02; `I/Modules/Assistant/AssistantUsageAggregationWorker.cs`; `Resources/{Assistant,Assistant.en}.resx`; adaptador `I/Modules/Assistant/Messaging/AssistantBudgetEmailBuilder.cs`; configuración SMTP/soporte vigente. No modificar AccountNotice para alojar costos de IA.

**Copiar de:** cuota concurrente E8, Outbox/EmailAccountNoticeChannel/DisplayFormatter, claves de advisory lock, recursos de Notifications y ejemplos Mensajes de las cuatro alertas. Títulos y precio mínimo desde catálogo filtrado; no precio/modelo fijo en código.

- [ ] Cuota PlatformKey: entidad nueva `D/Assistant/AssistantPlatformKeyDailyQuota.cs`, tabla técnica global `platform.AssistantPlatformKeyDailyQuotas`, única por (UserId, DayUtc), reservados/consumidos sin chat. Declarar el tipo global exacto en el contribuidor EF, sin whitelist de namespace. Suma Consumer, Business de cualquier organización y Platform. OwnKey: `D/Assistant/AssistantOwnKeyDailyQuota.cs`, `tenant.AssistantOwnKeyDailyQuotas`, ITenantOwned/RLS, única por (TenantId, UserId, DayUtc). Configs/repositorios en `I/Modules/Assistant/Persistence/`; cuota y reservas en la misma UoW del turno. Clave financiadora e IDs se derivan del servidor, nunca del body. PlatformKey 50 por defecto configurable; OwnKey usa su tope organizacional.
- [ ] Reserva atómica antes del proveedor, locks ordenados y dos réplicas. Cambiar acceso, organización, chat, modelo o zona no reinicia cuota. Día [00:00Z, 00:00Z siguiente), dailyResetAtUtc y Retry-After hasta ese instante; front muestra renovación con hora local. Atajos no cuentan; título no cuenta como pregunta extra pero sí tokens/costo. Turno admitido que invocó proveedor cuenta aun detenido; fallo anterior libera reserva. Remaining null por encima de 10. Mes civil del presupuesto es una regla distinta del día UTC.
- [ ] Mes civil de organización en su zona efectiva (ITimeZoneService), guardar intervalo UTC y snapshot de zona por mes. Costo estimado decimal/MoneyUSD, reservas para entrada máxima/salida/5 vueltas y título antes de empezar; no sobrepasar presupuesto con dos usuarios concurrentes. Uso reconcilia cada llamada incluyendo ciclos de herramientas/cache, no solo último response. Conservar reserva conservadora de uso incierto, nunca convertir desconocido en cero. Reducir gasto disponible por reserva, alerta solo por consumo confirmado/estimado registrado, no por mera reserva. Personal y operadores tienen cuota diaria; v114 muestra — de presupuesto mensual, no inventar un tope org para ellos.
- [ ] Uso agregado global por organización clave propia/plataforma y por Personas/Operadores; mantener espejo **sin texto** por transacción de completado, para no entrar a todos los tenants desde consulta Platform. Uso Business por día/persona solo tenant activo, sin permitir lectura de mensajes. Costos siempre con snapshot de precio/modelo vigente cuando ocurrió.
- [ ] Después de primera respuesta completa, título corto por modelo activo más barato **elegible con esa clave**, máximo 80 caracteres, hasta 64 tokens de salida, sin herramientas. Guardar costo en uso; compare-and-set para no pisar renombrado. Si no hay presupuesto/provider, conservar título determinista/renombrado, no bloquear la respuesta ya entregada ni gastar fuera de reserva.
- [ ] Alertas 80/100 una vez por organización/mes/umbral, índice único y outbox en la UoW del consumo. Si cruza ambos, emitir ambos sin duplicar por replay/cambio de límite. OwnKey a **todos** los Dueños activos con Email verificado utilizable, deduplicado por destino; PlatformKey al soporte de Config-Plat. Releer receptores/gate; si quedaron cero, conservar alerta pendiente para reintento y bloquear nuevos turnos OwnKey con el error de contrato, sin marcarla enviada. No WhatsApp. Resources es/en y DisplayFormatter cultura/zona explícitas; sin chat, prompts, clave ni IDs secretos.

**Tests:** `AU/Modules/Assistant/{AssistantLimitsTests,AssistantCostTests,AssistantTitleTests,AssistantBudgetAlertsTests}.cs`, `IT/Modules/Assistant/{AssistantLimitConcurrencyTests,AssistantUsageTests,AssistantBudgetOutboxTests}.cs`: 50/51,40/10 visible, cuota compartida Consumer/Business/organizaciones, OwnKey aparte, rollover UTC y cambio de zona sin reinicio, mes civil organización, reserva/replay/cancel, 80/100 dos réplicas, titlecost/no extra consulta, tarifa cambiada sin recalcular pasado, mirror rollback y outbox cifrado. **Hecho cuando:** límites no se eluden, costo mostrado corresponde a la estimación persistida y alertas/títulos no duplican ni leen chats ajenos.

### T07 · IFileStore, adjuntos y exportación

**Archivos:** puerto nuevo del núcleo `A/Interfaces/Integrations/Files/IFileStore.cs` y modelos `A/Models/Files/{FileObject,StoredFile}.cs`; `I/Files/{LocalFileStore,FileStoreRegistration}.cs`; options `A/Configuration/Files/FileStoreOptions.cs`; `A/Modules/Assistant/Services/{AssistantAttachmentService,AssistantAttachmentGuard,AssistantAttachmentWriter,AssistantExportService}.cs`, interfaces/validadores/modelos; `I/Modules/Assistant/Export/{AssistantMarkdownExporter,AssistantPdfExporter,AssistantPdfFontResolver}.cs`, fonts TTF licenciadas y su licencia en `Export/Fonts/`; contratos/controllers download/upload/export de T01; configuración no secreta y guía de archivos.

**Copiar de:** puertos neutrales/outbox cifrado E8, mapeo manual de Api y propiedades de forma/formato; PDFsharp-MigraDoc Core (no GDI), fuentes abiertas con licencia incluida y FontResolver para Windows/Linux. No reutilizar localStorage/base64 de capturas del lienzo ni rutas web públicas como storage.

- [ ] Firma neutral fijada en T01: `PutAsync(FileObject,Stream,ct)→StoredFile`, `OpenReadAsync(storageKey,ct)→Stream?`, `DeleteAsync(storageKey,ct)`. FileObject tiene objectId técnico/mediaType/byteLength, sin tenant/owner impuestos al storage; la autorización/metadata pertenece al módulo consumidor. StorageKey opaca y generada por adaptador, nunca ruta/nombre original. LocalFileStore guarda bajo Files:RootPath **fuera del webroot**, valida containment de ruta absoluta, temp→rename atómico y delete idempotente. Configurar ruta dev; Production exige proveedor nube futuro E10, sin aparentar disco como nube.
- [ ] Api cuenta bytes mientras lee, máximo 10 MiB/archivo y límites totales request; MIME por firma/magic + extensión permitida, nombres limpios y longitud TextLimits. No confiar solo en Content-Type. Exactamente PNG/JPEG/WebP/PDF; rechazar otros/malformados; hasta 5 **al consumir mensaje**, no cinco por request si se permite subir de a uno. Archivo ajeno/otro chat/ya usado→error antes de IA.
- [ ] Upload: validar acceso/owner antes de leer, escribir temporal fuera de UoW, confirmar metadata y binding idempotente privado bajo lock/version una UoW, y compensar/purgar huérfanos si falla. Replay DurableReference no vuelve a escribir bytes ni copia filename al store global. Hash del multipart incluye archivo y campos con lectura acotada, sin guardar el body en BD. No llamada a storage dentro de UoW. Dejar staging con vencimiento técnico de 24 h para archivos no confirmados/pendientes nunca usados. Dos réplicas no se apropian de un pending ajeno.
- [ ] Asociar attachmentIds al mensaje de forma atómica. El adaptador convierte adjuntos al contenido multimodal del SDK vía IChatClient, sin subirlos a Files API persistente externa ni URLs públicas. Verificar PDF/imagen en integración SDK real; validación previa no garantiza que el modelo acepte un PDF cifrado o contexto excesivo: devuelve error propio sanitizado. No loguear filename/contenido; las previews de UI usan download autorizado y object URL revocable, nunca URL permanente compartida.
- [ ] Exportación síncrona privada de conversación propia: Markdown UTF-8 y PDF con título/fechas/messages/links permitidos y referencias de adjuntos por nombre, sin bytes originales ni secretos/system/tool data. Markdown se escapa donde corresponda; PDF trata markdown como texto/estructura limitada, sin ejecutar HTML ni descargar URLs remotas. Fuentes Unicode embebidas/licencia, paginado y acentos; links creados desde route whitelist. Reutilizar DisplayFormatter para cultura/zona. Sin llamar LLM ni descontar cuota/costo.

**Tests:** `IT/Modules/Assistant/{AssistantAttachmentsTests,AssistantExportTests}.cs`, `IT/Files/LocalFileStoreTests.cs`: path traversal y symlink fuera raíz, 10MiB exacto/+1, 5/6 archivos, firma distinta de extensión, propietario cruzado, rollback/temp/crash cleanup, descarga tras delete/baja404, Markdown/PDF con acentos/múltiples páginas/sin rawHTML/red remota. Abrir ambos exports en la puerta, no basta encabezado `%PDF`. **Hecho cuando:** archivo privado/limpiable, envío multimodal y ambas descargas reales sin filtrar datos fuera del chat.

### T08 · Legal, retención y baja

**Archivos:** `A/Modules/Assistant/Services/{AssistantNoticeService,AssistantRetentionService,AssistantConversationRemover,AssistantAccountDeletionParticipant}.cs`; `D/Assistant/AssistantConversationRetentionIndex.cs`, `AssistantFileDeletion.cs` y persistencia/índices del módulo; `I/Modules/Assistant/{AssistantRetentionWorker,AssistantFileDeletionWorker}.cs`; nuevas versiones legales en el mecanismo vigente `I/Persistence/Seed/` según archivos reales; resources legales existentes; inventario exportación E10 en `B/docs/features/asistente.md`.

**Copiar de:** `IAccountDeletionParticipant` y context/AccountDeletionProcessor, I/Modules/WhatsApp/Retention/WhatsAppMessageRetentionService.cs recibido de E8 T08, SKIP LOCKED/outbox y gate legal; **los participantes reciben UoW/scope del núcleo**, no entran a tenant ni guardan por su cuenta. AccountDeletionTenantReader ya enumera todas las organizaciones y el personal, incluso para membresías anteriores; usar esos pasos, no leer chats privados globalmente.

- [ ] Publicar versiones nuevas de Términos/Privacidad que nombren Anthropic/procesamiento de consultas/adjuntos y expliquen retención/exportación. No modificar v1 legal ya aceptada ni marcar aceptado por enviar OTP/usar chat. Gate legal vigente primero; luego aviso de IA Primera vez/Entendido por user/version, persistido sin convertirlo en permiso o cookie de identidad nueva.
- [ ] LastActivityAtUtc solo cambia con consulta/respuesta/edición relevante; leer historial no vuelve eternas conversaciones por polling. Guardar detiene expiración sin plazo; dejar de guardar vuelve a aplicar regla desde última actividad. Valor inicial30, configurable en Plat-Config. Worker revalida guardada/fecha bajo lock para que no borre una conversación recién guardada. Retención sin esperar 30 días, FakeTimeProvider.
- [ ] Índice global técnico del módulo permite al worker descubrir candidatos `{tenantId,conversationId,lastActivityAtUtc,isSaved}` sin texto/adjuntos; entrar en scope **antes** de UoW, recargar entidad privada/owner y borrar con RLS. Índice y fila privada se actualizan en la misma UoW; tipos globales exactos declarados por contribuidor. Para Platform usar su tabla global propia, nunca SET tenant falso. No IgnoreQueryFilters generales ni carga de todas las conversaciones a memoria.
- [ ] Eliminar conversación/baja: marcar cancelación de turnos, invalidar streams, quitar messages/tool metadata/feedback/attachments; encolar borrado físico del FileStore en ledger/outbox neutral **en la misma UoW**. Worker borra después del commit con reintentos/tombstones, sin acceso tras borrar metadata. No FileStore dentro de UoW ni una fila borrada que deja archivos inaccesibles sin trabajo de limpieza. Purga de staging tampoco toca archivo en uso.
- [ ] Participant limpia OwnerUserId exacto del context.TenantId en cada paso (Personal/Business, incluyendo conversaciones de exmiembro) y final global elimina aceptaciones/índices propios remanentes. Conservar solo uso/costo agregado necesario sin mensajes/títulos/nombres ni vínculo personal después de anonimización; documentar esa retención. Operadores no pueden darse de baja por ADR 0042; borrar sus chats a mano/por 30 d sigue válido.
- [ ] E10 aún no tiene puerto de exportación de datos personales: inventariar conversaciones/messages/feedback/attachments/notice y lecturas autorizadas que incluirá, sin crear un participante sin consumidor. D16 exportar conversación sí se implementa en T07 ahora. Archivos/costos no se suman a catálogo WhatsApp.

**Tests:** `IT/Modules/Assistant/{AssistantNoticeTests,AssistantRetentionTests,AssistantAccountDeletionTests,AssistantFileCleanupTests}.cs`, ampliar AccountDeletionParticipantsTests: 30 d, guardada 90 d no borrada, race guardar/purge, baja guardadas/antiguos tenants, doble participant/reinicio sin adjuntos huérfanos, outbox/workerrollback, IA/WhatsApp participants independientes y versiones legales no aceptadas implícitamente. **Hecho cuando:** eliminación impide leer y finalmente borra bytes; guardadas solo sobreviven a retención, nunca a baja/borrado explícito.

### T09 · Documentación, guardas y receta de quitar

**Archivos:** `B/docs/features/asistente.md`, `docs/guides/{agregar-una-herramienta-al-asistente,quitar-asistente}.md`, fichas modulos/modulos-habilitados/persistencia-ef/logs/ayuda/archivos afectadas, AGENTS de nuevas carpetas, árboles/arquitectura/operaciones; `AR/ModuleIsolationTests.cs`, `IT/Modules/Assistant/AssistantRemovalTests.cs` si la guarda usa fixture pertinente. Informe final en Parte 3.

**Copiar de:** docs/guides/quitar-whatsapp.md E8, recipe Roles/Personal, E8 namespace/model controls; conservar convenciones de permisos/capabilities sin referenciar concretos.

- [ ] Documentar puertos/gates/controles de instalación/owner/SSE/replay/archivos/topes/tarifas/fake y el catálogo exacto; agregar ayuda es/en de cada área asistida. Regla transversal nueva requiere guarda, no inventar lista blanca por clase. Mantener ModuleIsolationTests para núcleo y referencias entre módulos.
- [ ] Receta en **copia descartable**: apagar chat/contribución, purgar por servicios y confirmar borrado físico pendiente; preservar fuentes temporalmente para excluir modelo runtime/DesignTime/bootstrap, generar nueva migración Drop desde snapshot previo y revisar solo tablas Assistant. Quitar carpetas/contribución/composición/frontend/assets/paquetes exclusivos, dejando puertos neutrales IFileStore/idempotencia/catálogos. Conservar migraciones históricas/.Designer, no reescribir. Probar BD vacía y migrada. No borrar tablas/Phone/Outbox/correos/tenant de E8; no eliminar migraciones si contienen historia del módulo.
- [ ] Nuevas referencias de permisos/features/EF/routes salen de contribución removible o se retiran explícitamente con receta; el núcleo no importa Domain.Assistant. Quitar paquete AI de Application no deja un tipo IChatClient en puerto core, pues vive solo en módulo. IFileStore queda útil para futuros módulos y su almacenamiento no exige Assistant. Sin registro se arranca/build/tests/loginWhatsApp igual que E8.
- [ ] Documentar E9 injection/costo/rate y E10cloud/secretmanager/export/backups pendientes, sin diferir los límites, lectura autorizada ni seguridad de archivos de esta v1. No declarar IA de E11 implementada.

**Tests:** guardas/links pertinentes y prueba de fuego de T15. **Hecho cuando:** un agente nuevo ubica receta/herramientas/contratos y puede retirar Assistant sin tocar lógica de WhatsApp ni romper el núcleo.

### Texto listo para el chat del Back

```text
Ejecutá SOLO Parte 2a (T02–T09) de C:\Users\ezequ\source\repos\ArquitecturaBaseMutitenant\docs\plans\2026-10-08-etapa-8b-asistente.md. Tu repo es el back; no edites el front. E8 cerrada y T00/T01 commiteadas, con P1–P3 resueltas por ADR0049. Seguí Forma de trabajo rápida, tabla D1–D26 y contrato congelado. Implementá módulo privado y quitable, catálogo y claves, herramientas exactas de solo lectura con permiso interno, consultas fijas sin modelo, worker/turnos/SSE sin buffer/idempotencia durable, topes/uso/títulos/alertas email, IFileStore/adjuntos/exportes/legal/retención/baja. Cada método público que escribe una UoW; proveedor/storage fuera. Sin referencias entre módulos ni permisos de más. Fake solo Testing/E2E, nunca producción. Tests donde importan y commits main sin push por tus rutas. No cambies contrato unilateralmente: anotá necesidad en Cambios al contrato para pasarla al otro chat. Devolvé hashes y build/test completo verde para Integración, sin cerrar E8b ni omitir la prueba manual real.
```

## Parte 2b · Front

El contrato/tipos de T01 permiten trabajar con MSW tipado sin esperar implementación back. No editar tipos generados a mano ni importar un área desde otra para compartir el asistente.

### T10 · Módulo UI, assistant-ui, SSE y chat

**Archivos:** `F/src/modules/assistant/{registration.ts,api/{assistant,stream,attachments,export}.ts,runtime/{useAssistantRuntime,assistantTurnState}.ts,components/{AssistantThread,ConversationList,QuickQueries,ToolStatus,AssistantNotice,AttachmentComposer,AssistantMessageActions,NotConfigured,LimitReached}.tsx,pages/{AssistantHome,AssistantPhonePage}.tsx}`; `F/src/locales/{es,en}/assistant.json`; `F/src/shared/api/{httpClient,types}.ts` si necesita transporte genérico de streams/binarios; `F/package.json/package-lock.json` de T00.

**Copiar de:** httpClient/useIdempotentMutation/TanStack Query y errors por code, Account dialogs/hojas/Alert, tokens/íconos actuales; assistant-ui **ExternalStoreRuntime** oficial, piezas sin CSS de ejemplo. API sobre bearer en memoria, no Cloud de assistant-ui ni protocolo AI SDK.

- [ ] Runtime adapta mensajes/estado de Query a assistant-ui; streaming efímero comparte queryKey por access/tenant/user/conversation, IDs del back. Parser UTF-8 incremental soporta eventos divididos en chunks, comentarios/heartbeat, multiline SSE, terminal y JSON replay. No fetch en useEffect; stream es mutación/transporte del httpClient con AbortController. Sin tokens/key en localStorage; cache clear al cambiar acceso/tenant cancela stream y vacía historial/adjuntos/previews.
- [ ] Bienvenida/cards/Para resolver recibidos por núcleo, 11 atajos disponibles de State, historial Guardadas/Recientes, nueva conversación, guardar/desguardar/renombrar/borrar con version, export/copy/feedback/links. No botón de escribir negocio ni herramienta ejecutada en browser. Reintentar error crea nueva operación, volver a intentar red conserva misma key.
- [ ] Estados consultando/escribiendo/detenida/falló desde eventos y persistencia. Detener envía stop y aborta lectura sin esconder error de guardado; reabrir consulta estado durable. Remaining solo≤ 10, topes por código, daily/monthly no borra historial ni impide atajos. Sin configurar muestra acción solo si canConfigure; notice first-time Entendido antes de consulta con gate legal ya cumplido.
- [ ] Markdown sin HTML crudo/rehype-raw; enlaces de acciones solo route whitelist, URLs externos del markdown no disparan descargas ni scripts. Adjuntos botón/fileinput muestra máximo 5 archivos y 10 MiB temprano y errores API; input accept no sustituye verificación back. PDF no se ejecuta como HTML; object URL revocada al cambiar scope/borrar/desmontar.

**Tests:** runtime/stream focales (`assistantTurnState.test.ts`, `stream.test.ts`), chat `.test.tsx` para negocio/errores/notice/stop/replay/cache scopes/permiso/enlaces/adjuntos, axe compartido. No test individual de cada Tweak. **Hecho cuando:** chat consume exactamente HTTP/SSE real y todos los estados definidos pueden alcanzarse con MSW sin hardcodear datos.

### T11 · Inicios, Administración y segunda pestaña del teléfono

**Archivos:** `F/src/app/{routes,RouteViews}.tsx`, `F/src/layouts/{BusinessLayout,PersonalLayout,PlatformLayout,MorePage}.tsx`, `layouts/navigation/{business,platform,personal,types}.ts` según inventario real; `F/src/areas/{business,personal}/home/pages/*HomePage.tsx`; módulo `registration.ts`; `F/src/tenancy/access.ts`; navegación/locales/tests de rutas. Crear `F/src/areas/platform/home/pages/PlatformHomePage.tsx` como wrapper neutral del inicio.

**Copiar de:** Asistente-Org/Personal/Plat y sus M-*, layouts/wallet actuales y manifests de v114; `routes.tsx/createPlatformPageRoutes` hoy usa `/plataforma` para listado de organizaciones. No perder las guardas/autorización de ese listado al separarlo.

- [ ] Módulo aporta routes/navigation/home-content mediante registro neutral en composición F (por ejemplo `ModuleUiContribution`); shared/layouts/áreas no importan páginas concretas de Assistant. En composición única se importa el módulo y se pueden quitar su folder/registro, sin decenas de imports huérfanos. Igual criterio de quitar de E8.
- [ ] Desktop: inicio del lado muestra AssistantHome solo enabled+permiso; la bienvenida incluye tarjetas/pendientes existentes, nada se reimplementa en IA. Sin módulo/permiso conserva inicio previo. Consumer desktop conserva aviso método administrado de cuenta; operador usa solo su contexto Platform.
- [ ] Activo: Inicio Platform `/plataforma`; listado Organizaciones `/plataforma/organizaciones`, con permission tenants.read; detail/rutas/breadcrumbs/enlaces de correos/atajos/PlatformHomeRoute apuntan al nuevo listado. Sin Asistente conserva la entrada anterior `/plataforma` y navigation E8, con alias `/plataforma/organizaciones` al listado si hay enlaces guardados. No redirigir un operador sin assistant.use a una pantalla que le niegue acceso cuando antes veía Organizaciones.
- [ ] Administración › Asistente grupo con Configuración/Uso según sus permisos. Desktop no añade pestaña lateral de asistente además del inicio. Mobile ruta propia: `/asistente`, `/org/asistente`, `/plataforma/asistente`; billetera/Inicio sigue intacto; **Asistente segunda** pestaña, historial en hoja. Difusiones y Recuperaciones pasan a Más según v114 cuando estén presentes; no crear Difusiones E11 ni ocultar su entrada futura. Mantener máximo 4 secciones+Más y los demás permisos.
- [ ] Probar desactivado, no registrado y sin permisos en los tres lados: no grupo, no segunda pestaña ni API/chat; botones/posiciones previos de E8. Administración WhatsApp y sus capabilities/telefonía no dependen de Assistant.

**Tests:** manifests/routes-by-host/layouts/MorePage/BusinessHomePage/PersonalHomePage/PlatformHomeRoute y nuevas rutas: enabled/off/permission, desktop 1440 / móvil 390 / tablet 768, detalle/listado/breadcrumb en Platform, alias guardado, subdominio storefront sin Assistant privado, cancel/cache al cambiar acceso, coexistencia de cookies. **Hecho cuando:** navegación v114 correcta al habilitar y retorno completo a E8 al apagar/quitar, sin perder pantallas ni enlaces de WhatsApp.

### T12 · Configuración, catálogo de modelos y Módulos

**Archivos:** `F/src/modules/assistant/settings/{OrganizationAssistantSettingsPage,PlatformAssistantSettingsPage,ModelCatalog,ModelEditorDialog}.tsx`, API/settings queries; `F/src/areas/platform/tenants/tabs/OrganizationModulesTab.tsx` y `.test.tsx`; contribución neutral de fila/acciones por módulo; locales assistant/errors/navigation es/en.

**Copiar de:** Asistente-Config-Org/Plat-Asistente-Config/M-*, Organizacion(pestana Módulos)/M-Organizacion(Módulos, acciones Asistente); SettingsPage autosave/form/concurrencia, PlatformFeatures API/Reason confirmation. No hardcodear modelos/precios ni duplicar lógica de habilitación de WhatsApp.

- [ ] Config-Org: PlatformKey/OwnKey solo si autorizado, modelo dropdown filtrado, topes editables own-key/lectura platform-key; Cambiar clave password/write-only sin valor del secreto en query/cache/devtools. Guardar/descartar, error KeyRejected field, 409 recarga. Cambiar un dropdown no llama al proveedor desde browser. Sin hasAlertRecipient: aviso/error de campo mode y acción al flujo de Mi cuenta para agregar/verificar Email; no destinatario alternativo ni WhatsApp.
- [ ] Plat-Config: General/Catálogo, default/effort/modelos permitidos, diario/mensual/personas/retención y clave; catálogo paginado precios MoneyText, activo sí/no, agregar/editar con version. Móvil lista y hoja existente para formulario, sin pantalla de ayuda nueva. El costo de ejemplos del lienzo se reemplaza con dato API formateado, no literals del tablero.
- [ ] Módulos: fila Assistant por catálogo, prender/apagar/extendertrial con contrato existente; acción Permitir/impedir propia clave usa policy separado y reason/version. Cada guardado maneja versión del recurso propio; dos ediciones no pisan WhatsApp/otros módulos. Features false y policy false son defaults, no acciones sintéticas mientras catálogo carga.

**Tests:** settings/model/policy focales MSW: permisos, mode/keyAction/cuerpo, sin secreto en cache, choice restricciones, version conflict, toggle own-key/prender/off, trial y otras filas iguales. Axe/form focus compartido. **Hecho cuando:** reproduce 7 estados Config-Org/7 Plat y Módulos sin llamar SDK ni inventar dato de referencia.

### T13 · Uso, alertas visibles y pruebas del front

**Archivos:** `F/src/modules/assistant/usage/{OrganizationAssistantUsagePage,PlatformAssistantUsagePage,AssistantUsageSummary}.tsx`, `api/usage.ts`, sus tests/locales; enlaces de uso/config del módulo; documentación front/árbol y contribuciones de ayuda si front tiene área nueva.

**Copiar de:** Asistente-Uso-Org/Plat-Asistente-Uso y M-*, tablas/listas/Pagination/MonthField/MoneyText/DateText centrales. Mensajes correos se implementan en back T06; front no envía alertas ni cuenta tokens.

- [ ] Business mes/Por día/Por persona, cerca/tope/sin uso; Platform por organización+Personas/Operadores, costo de plataforma separado del ajeno. Paginado/orden/resumen del back, sin totales monetarios calculados en JS ni conversación detrás de una fila de uso.
- [ ] Link Ver uso en chat solo con configure o platform.read; respuesta de uso por herramienta no concede configuración. Alertas ya enlazan a pantalla por side/permiso; validar returnTo del login preserva acceso correcto, sin query secretos.
- [ ] Documentar integración modular y actualizar tests existentes afectados por navegación sin snapshot del nuevo estado por cada tablero. Build/lint/tests/contracts:check completos al terminar parte 2, con mocks solo en tests focales.

**Tests:** usage group/month/costos/null/no texto/permiso/off, estados foco y axe; regresiones PhoneField/account/loginLink/nav presentes en E8. **Hecho cuando:** uso muestra datos agregados reales de contrato, front completo en verde y listo para API real.

### Texto listo para el chat del Front

```text
Ejecutá SOLO Parte 2b (T10–T13) del plan C:\Users\ezequ\source\repos\ArquitecturaBaseMutitenant\docs\plans\2026-10-08-etapa-8b-asistente.md. Tu repo es C:\Users\ezequ\source\repos\ArquitecturaBaseMutitenantFront; no edites back. T01 y decisiones dependientes deben estar congeladas/commiteadas. Copiá todos los estados del Tweak asistente de Asistente-Org/Personal/Plat y sus M-*, Config/Uso, Módulos y navegación v114 commit 4340891b. Usá assistant-ui con tokens propios y ExternalStoreRuntime/Query, SSE/bearer en memoria, permisos/capacidades de API, adjuntos y exports privados. Asistente segunda pestaña móvil; mantener billetera, mover Recuperaciones/Difusiones existentes a Más y separar Inicio Platform /plataforma de listado /plataforma/organizaciones solo con módulo activo. Sin módulo todo como E8; no romper Phone/OTP/cuenta/loginLink/cookies. Tests con MSW donde importan, sin capturas/aprobación por pantalla. Commits main sin push por rutas propias, no cambiar contrato sin coordinación. Terminá build/lint/test/contracts:check completos y devolvé hashes/resultados para Integración; no cierres E8b.
```

## Parte 3 · Integración y puerta

### T14 · Conectar, FakeChatClient y regresiones E8

**Archivos:** `F/scripts/{test-e2e-real.test,test-e2e-real-case,test-e2e-real-ui}.mjs`, nuevo `test-e2e-real-assistant.mjs`, `F/scripts/recorrido-usuario.mjs` y nuevo recorrido asistente; `B/tools/RealE2ESetup/` y fixtures `IT/TestFeatures/` solo entorno E2E, FakeChatClient guiones de T05; `F/scripts/capturar-etapa-8b.mjs`, galería `F/docs/design/implementacion/etapa-8b/`; `B/docs/reviews/<fecha>-etapa-8b-asistente.md`.

**Copiar de:** E7/E8 E2E real, usuario persistente/cookies anteriores, pickup EML, capturar-etapa7/8 y guards de fixtures aisladas. FakeChatClient es **único reemplazo externo permitido** del modelo: front/API/OpenIddict/Postgres/RLS/outbox/FileStore/herramientas reales, sin MSW en E2E.

- [ ] Conectar HTTP/SSE (validar flush por Vite/proxy), binarios/uploads/feedback/policy/version; cualquier cambio atraviesa Cambios al contrato y sincronización B/F. Verificar cero 501 y contrato hashes iguales.
- [ ] E2E 1440/390: plataforma configura catálogo/clave fake/personas, habilita Business/allowOwnKey; inicia los tres accesos, aviso/legal/Entendido, fija+libre, tool/deltas/stop/fallo/replay,5/6 adjuntos y 10 MiB + 1, MD/PDF/feedback/guardar/renombrar/borrar, usuarios/tenants/owner cruzados404, permisos revocados,40/50/51/cuota mensual80/100/títulos/EML, retención y baja con relojfixture. Casos de tope no hacen50 requests reales ni bajan controles productivos: fixture de BD E2E exacta.
- [ ] P1 resuelto tiene resultado personal real; si fuente está ausente, comportamiento aprobado vacío y cero consulta a TestFeatures. P2 se prueba con Dueño Phone-only y Email; P3 con accesos/org distintas. Contador Fake demuestra atajos/replay/export/title número de llamadas correcto.
- [ ] Regresión E8: login/registroPhone personal+empresarial, métodos/principal/quitar sin código extra, recuperación, bot/enlace/preview/statePKCE, dos cookies, webhook/firma/dedupe/outbox y baja con ambos participantes. Fake no sustituye Meta: reenviar un código y bot reales en la prueba manual de T15, sin tocar destino webhook salvo guía E8.
- [ ] Galería **una vez** lado a lado tablero/app, lista de estados de Tableros contra decisiones a 1440/390 y revisión a 768, es y muestras en inglés. Screenshots sin claves/OTP/links reales/teléfonos sin máscara/texto privado de usuarios reales. Marcar diferencias explícitas de ADR; no ocultar faltantes con captura de un estado distinto.

**Tests que importan:** E2E real asistente y regresiones sin filtros; tests de parser y aislamiento corren junto a suites. **Hecho cuando:** producto real funciona con modelo fake guionado, galería completa y preparada prueba Anthropic manual.

### T15 · Única puerta final

Ejecutar sobre commits finales, repetir solo controles invalidados por correcciones posteriores y los checks de cierre pertinentes.

| Verificación | Comando/evidencia exigida |
|---|---|
| Back completo | En B `dotnet build ArquitecturaBaseMultitenant.slnx` 0 advertencias; `dotnet test` completo con Docker, sin fallos/omitidos relevantes. |
| Front completo | En F `npm run build`, `npm run lint`, `npm test`, `npm run contracts:check`, todos exit 0. |
| E2E | En F `npm run test:e2e:real` sin filtros, módulos/recorridos nuevos y E8/E7 en verde; real front/API/BD, solo FakeChatClient para modelo. |
| Documentación | En B `node scripts/arneses/arnes.mjs realinear-docs --detectar --repo back` y `--repo front`, todos contadores 0; si no, arnés conforme a skill/rutina, reconciliar/repetir. |
| Galería | Índice con tableros contra estados/lista D1–D26, app lado a lado desktop/móvil/tablet; revisada al final. |
| Recorrido usuario | Navegador con sesiones de antes según puerta [arreglos](2026-10-06-arreglos-antes-de-la-7.md#puerta-el-recorrido-de-un-usuario-nuevo), más E8/E8b sin limpiar cookies ni perder acceso. |
| Anthropic real | Prueba siguiente completa con clave cargada por usuario, sin mocks/modelo fake, IDs/tokens/costo registrados sin contenido privado. |
| Quitar Asistente | Copia descartable según T09, BD vacía/migrada, núcleo/build/tests/E8/WhatsApp siguen verdes; chat/grupo/pestaña nuevos desaparecen. No borrar en repos compartidos. |

Focales durante desarrollo: `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class "*AssistantStreamingTests"`, build proyecto afectado; en F `npm test -- src/modules/assistant/api/stream.test.ts`. Esperado clase/archivo completo en verde, no sustituto de suites finales.

### Prueba manual con clave real

1. Confirmar entorno propio, modelo/config/tarifas activos y tope pequeño configurable suficiente; usuario carga **Assistant:Platform:ApiKey** por user-secrets/scripts sin imprimir ni copiar a plan/log. Fake desactivado. Usar datos/adjuntos de prueba propios sin información real sensible; no generar evidencias con clave o cuerpos privados.
2. Entrar como operador con las dos cookies previas, configurar model/dropdown permitido y personas, habilitar organización. API/frontend/BD/FileStore reales con `aspire run`. Verificar que ausencia de clave muestra Sin configurar y Config aún permite corregirla; una clave inválida muestra error de campo sanitizado.
3. Business consulta fija Usuarios activos: resultado de servicio real **cero** llamada a Anthropic. Consulta libre de sistema: herramienta permitida, Consultando→deltas→Complete; link abre Usuarios filtrado. Pregunta sin herramienta se reconoce, no ejecuta cambios. Usuario sin users.read no obtiene ni atajo ni datos al pedirlo por texto.
4. Consumer mi cuenta tapada/mis organizaciones y ayuda WhatsApp; operador consultas globales autorizadas. Conversación de un tercero/otro tenant/otro acceso→404; Uso sólo agregados, ni Dueño ni operador pueden abrir historial ajeno.
5. Enviar imagen/PDF benignos reales, hasta límites, recibir respuesta multimodal; Detener guarda Stopped; abrir nuevamente conserva parcial. Replay con la misma key no genera nueva facturación. Título automático elegido del catálogo elegible, renombrar no es pisado. Exportar Markdown/PDF y **abrir** ambos, revisar acentos, fechas, páginas/enlaces y ausencia de tool/system/key.
6. Revisar tokens/estimación de costos/título en Uso y Aspire **sin texto ni argumentos** en trazas. Límite/alertas se verifican determinísticamente en E2E; en manual una consulta real actualiza gasto y no omite precio por caché. Validar consulta con own-key habilitada mediante clave de prueba del usuario si está disponible; si no, ese tramo externo queda pendiente, no se afirma probado.
7. Apagar Asistente y comprobar inicios/Módulos/Administración/barra originales de E8. Recorrer WhatsApp por el túnel de configuracion.md: código real delivered, bot/link consumer, operador con texto neutro sin enlace y cookies; no Fake en Meta. No cambiar una URL webhook de otro proceso sin coordinar; registrar/restaurar callback según E8 si hubo que tocarlo.
8. Borrar conversación y verificar descargas 404/bytes purgados; la retención y baja de todas/guardadas se apoyan en pruebas de reloj E2E y participante real. Registrar hashes/modelID/estado/facturación estimada/duración/resultados, sin prompt/adjunto/key. `aspire stop` solo entorno propio.

Si clave/modelo/Meta/galería/own-key real requerido no está disponible, puerta pendiente con causa concreta. No convertir Fake/local test en aprobación externa. **Hecho cuando de E8b:** T00–T14 implementadas, decisiones 0049 aplicadas, contrato idéntico, cero 501, seguridad/aislamiento y suites verdes, detectores en 0, galería/recorrido/prueba Anthropic/quitar aprobados. Solo entonces cerrar etapa en mecanismo vigente/informe.

### Texto listo para el chat de Integración y puerta

```text
Ejecutá SOLO Parte 3 (T14–T15) de C:\Users\ezequ\source\repos\ArquitecturaBaseMutitenant\docs\plans\2026-10-08-etapa-8b-asistente.md. Back/Front implementados contra T01 commiteado, E8 cerrada. Trabajá ambos repos main sin push y commits solo rutas propias, preservando procesos/cambios ajenos. Integrá contrato/SSE/uploads/exports, E2E real con FakeChatClient como único reemplazo del proveedor y herramientas/API/BD/FileStore reales. Corré suites completas/E2E sin filtros/detectores en 0; galería una vez y recorrido con sesiones previas. Hacé prueba manual con clave real de Anthropic, imágenes/PDF/stop/título/uso y regresión WhatsApp real vía túnel configuracion.md, sin secretos en evidencia. Probá quitar Asistente solo en copia descartable conservando WhatsApp/núcleo verdes y UI E8. Si contrato cambia, coordinar B/F mediante Cambios al contrato. Dejá informe corto hashes/resultados/decisiones/pendientes; no cierres con prueba externa/galería pendiente. Apagá solo tu Aspire y restaurá el callback de Meta si correspondió.
```

## Cambios al contrato

Vacío al escribir. Durante parte 2 anotar propuesta/motivo/rutas/cuerpos/eventos/códigos y decisión comunicada al otro chat/hashes B/F. Ningún cambio unilateral ni versión nueva de contrato improvisada en MSW.

## Decisiones aceptadas

La [ADR 0049](../decisions/0049-precisiones-whatsapp-y-asistente.md) registra P1–P3: «Mis mensajes con empresas» visible con puerto neutral y vacío explícito hasta conectar fuente productiva; alertas own-key por email a todos los Dueños activos con Email verificado utilizable, con al menos uno requerido para activar OwnKey; cuota PlatformKey global por UserId/día UTC compartida entre accesos y organizaciones, y OwnKey aparte por UserId/organización/día UTC. Atajos/títulos no consumen preguntas; títulos sí tienen costo.

## Para decidir

Ningún punto de producto pendiente. El vacío personal y la validación OwnKey son complementos visuales de las decisiones aceptadas y tareas previstas, sin abrir opciones nuevas.
