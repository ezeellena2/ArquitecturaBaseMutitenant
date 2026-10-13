# El asistente: un LLM en el inicio de Plataforma, de la empresa y de la persona

Estado: **decisiones aceptadas**, recogidas en [ADR 0048](../decisions/0048-asistente.md) y [0049](../decisions/0049-precisiones-whatsapp-y-asistente.md). Ejecución en el [plan E8b](2026-10-08-etapa-8b-asistente.md), después de E8 cerrada. No implementado todavía. La tabla D1–D26 manda sobre las recomendaciones históricas de §2; D6 (Anthropic oficial) está resuelta por 0048. Los esquemas, contratos y árbol preliminares de abajo se interpretan con las adaptaciones al código existente del plan y la [revisión previa](../reviews/2026-10-08-alineamiento-etapas-8-y-8b.md).

Precisiones aceptadas el 2026-10-08 (0049): «Mis mensajes con empresas» permanece visible con puerto neutral y vacío explícito hasta tener fuente productiva, sin consultar TestFeatures ni adelantar E11. OwnKey requiere al menos un Dueño activo con método Email verificado utilizable; alertas email a todos los que lo tengan, con acción Mi cuenta si falta. PlatformKey comparte cuota global UserId/día UTC entre accesos/organizaciones; OwnKey usa UserId/organización/día UTC, con topes propios. Cambiar zona no reinicia contador; títulos suman costo pero no preguntas.

**El pedido del usuario:**
- un chat como Claude o Codex en el inicio de los tres lados, que responda solo cosas del sistema;
- la plataforma lo provee: la persona usa siempre el de la plataforma, y cada organización puede usar el de la plataforma o configurar el suyo;
- consultas fijas, topes, uso, historial y borrado;
- pensado como un módulo central, revisando cómo lo afectan las demás etapas.

## Decidido con el usuario

| Fecha | Decisión | Respuesta |
|---|---|---|
| 2026-10-07 | D1 · Lugar | **En la compu, el asistente es el inicio.** Lo de hoy («Para resolver», tarjetas) pasa a ser su bienvenida. En el teléfono, una pestaña aparte. |
| 2026-10-07 | D3 · Habilitar | **Módulo habilitable, apagado por defecto:** por organización desde Plataforma › Organización › Módulos, y para las personas desde Configuración de Plataforma. |
| 2026-10-07 | D2 · Clave propia | **Solo si la plataforma la habilita** por organización («puede usar su clave»). Por defecto, la de la plataforma. |
| 2026-10-07 | D5 · Privacidad | **Solo quien creó la conversación la lee.** El Dueño y la plataforma ven el uso, nunca el texto. |
| 2026-10-07 | D7 · Modelo | **Totalmente configurable en Plataforma,** con un desplegable de modelos. El catálogo de modelos y precios es dato de referencia (`ModelPrices`), no una lista en el código. Una organización con su clave elige del mismo desplegable. |
| 2026-10-07 | D4 · Retención | **Se borran solas a los 30 días sin actividad,** salvo las que la persona **guarda** (fijar o archivar): esas no vencen nunca. Se borran a mano, y todas, guardadas incluidas, con la baja de la cuenta. |
| 2026-10-07 | D9 · Consultas fijas | **Atajos sin modelo:** botones que llaman directo a una herramienta y muestran el resultado con un texto armado, gratis e instantáneos. Fijos por lado en la v1. |
| 2026-10-07 | D8 · Acciones | **Solo responde en la v1,** con enlaces a la pantalla donde se hace cada cosa. Las acciones con confirmación quedan para la v2. |
| 2026-10-07 | D11 · Quién lo usa | **Todos, por permiso:** `assistant.use` viene en todos los roles de sistema y el Dueño se lo puede quitar a un rol. Cada uno consulta solo lo que ya puede ver. |
| 2026-10-07 | D12 · Personas | **Sí en la v1,** con pocas herramientas (mi cuenta, mis organizaciones, mis interacciones). Se prende desde Configuración de Plataforma, con la clave de la plataforma. |
| 2026-10-07 | D13 · Extras | **Todo en la v1:** 👍/👎 por respuesta, enlaces a la pantalla ya filtrada, **exportar la conversación** y **adjuntar archivos o imágenes**. |
| 2026-10-07 | D14 · Adjuntos | **Imágenes y PDF** (PNG, JPEG, WebP y PDF), hasta **10 MB** y **5 por mensaje**. Se borran con su conversación. |
| 2026-10-07 | D15 · Archivos | **Un puerto de archivos del núcleo, `IFileStore`:** disco en desarrollo y nube en producción (S3 o Azure Blob, se elige en la E10). Lo reutiliza cualquier módulo que suba archivos. |
| 2026-10-07 | D16 · Exportar | **Markdown y PDF,** a elección de la persona. El PDF suma una librería en el back, que se elige en el plan. |
| 2026-10-07 | D17 · Teléfono | **Una pestaña «Asistente» en la barra de abajo,** con el chat a pantalla completa y el historial en una hoja. El inicio sigue siendo la billetera. |
| 2026-10-07 | D18 · Cuándo | **Justo después de la Etapa 8,** copiando su patrón de módulo quitable. |
| 2026-10-07 | D19 · Modelos con la clave de la plataforma | **La plataforma fija qué modelos se pueden elegir con su clave,** y la organización elige entre esos. Con su propia clave, elige cualquiera del catálogo. |
| 2026-10-07 | D20 · Contador | **Solo cerca del tope:** al quedar 10 mensajes, «Te quedan 10 consultas hoy». |
| 2026-10-07 | D21 · Ayuda | **También explica cómo usar la app.** Una herramienta lee artículos de ayuda que escribe la plataforma (uno por tema, en es y en) y responde con los pasos y el enlace a la pantalla. Los artículos son contenido configurable, no texto en el código. Cada área nueva suma los suyos. |
| 2026-10-07 | D22 · Aviso legal | **Aviso la primera vez** que alguien usa el asistente («Tus consultas las procesa un proveedor de IA; no subas datos sensibles», con «Entendido», que queda registrado) **y una versión nueva de Términos y Privacidad** que lo menciona. |
| 2026-10-07 | D23 · Alertas | **Correo al 80 % y al 100 % del tope mensual:** al Dueño si la organización usa su propia clave, y a la plataforma si usa la de la plataforma. Por el outbox, en es y en. |
| 2026-10-07 | D24 · Títulos | **Automáticos con IA:** después de la primera respuesta, el modelo más barato del catálogo le pone un título corto. Se puede renombrar, y el costo cuenta en el uso. |
| 2026-10-07 | D25 · Dónde vive la ayuda | **Archivos en el repo:** un `.md` por tema y por idioma (es y en), al lado del código de cada área (por ejemplo `Infrastructure/Modules/Assistant/Help/Articles/<area>/<tema>.es.md`). Los escribe quien programa el área y viajan con cada versión. Un test exige que cada área con pantallas tenga los suyos. No hay pantallas nuevas. |
| 2026-10-07 | D26 · Catálogo de modelos | **Una pantalla en Plataforma** (dentro de Configuración del asistente): una tabla de modelos con nombre, proveedor, ID, precio de entrada y de salida, y activo sí o no. Arranca con los modelos de Claude vigentes y la plataforma los mantiene sin programar. Diseño la suma al tablero de configuración. |
| 2026-10-07 | D10 · Topes | **Por persona y por organización:** 50 mensajes por persona por día y un tope mensual en dólares por organización, con un aviso claro al llegar. Los cambia la plataforma; una organización con su clave fija los suyos. |

---

## 1. La idea en una línea

Un **módulo quitable** («Asistente», con el mismo patrón que WhatsApp en la E8).
- Un modelo de lenguaje responde **solo con herramientas de solo lectura**, que llaman a los servicios que ya existen **en nombre de quien pregunta**: con su organización, sus permisos y su aislamiento.
- El modelo nunca ve un dato que esa persona no podría ver en pantalla, y nunca escribe nada.

```
Pantalla (assistant-ui)
   │  POST /…/assistant/conversations/{id}/messages
   ▼  (respuesta en streaming: SSE)
Controller ──► IAssistantService ──► AssistantTurnRequester (admite/reserva dentro de UoW)
                                      │  commit; worker ejecuta después, fuera de UoW
                                      │  IChatClient (Microsoft.Extensions.AI)
                                      │    └─ SDK oficial de Anthropic
                                      ├─ AssistantToolCatalog → herramientas (AIFunction)
                                      │     └─ IUserService, IRoleService, IPlatform… (los de siempre)
                                      ├─ AssistantLimitGuard (topes)
                                      └─ repositorios: conversaciones, mensajes, uso
```

---

## 2. Decisiones (con mi recomendación)

> Las respuestas del usuario están en «Decidido con el usuario», arriba, y **mandan sobre esta tabla**. Por ejemplo, la retención es de 30 días con conversaciones guardadas, y el modelo se elige con un desplegable.

| # | Decisión | Recomiendo | Por qué |
|---|---|---|---|
| D1 | ¿El chat **es** el inicio? | **En la compu, sí:** con el módulo prendido, el inicio es el asistente, y lo de hoy («Para resolver», tarjetas) pasa a ser la bienvenida del chat. **En el teléfono, no:** el inicio sigue siendo la billetera y el asistente es una pestaña de la barra de abajo. | En la compu entra todo. En el teléfono, la billetera ya está decidida (lienzo v77) y un chat no convive bien con ella. |
| D2 | ¿Quién trae su propia clave? | **La plataforma lo habilita por organización** (Plataforma › Organización › Módulos): «con la clave de la plataforma» o «puede usar la suya». Por defecto, la de la plataforma. | Vos controlás el costo y la calidad. Una empresa grande puede pagar la suya. |
| D3 | ¿Incluido o módulo habilitable? | **Módulo habilitable,** apagado por defecto. Por organización, desde Módulos; para las personas, un interruptor en Configuración de Plataforma. | Cuesta plata real por cada pregunta, y ya existe la pieza (`hasFeature`, [modulos-habilitados](../rules/modulos-habilitados.md)). |
| D4 | ¿Las conversaciones vencen? | **Sí: se borran solas a los 90 días sin actividad** (configurable en Plataforma). Además, borrado a mano y borrado total con la baja de la cuenta. | Privacidad y menos datos guardados. Nadie vuelve a una charla de hace tres meses. |
| D5 | ¿Quién ve una conversación? | **Solo quien la creó.** Ni el Dueño ni el operador leen el contenido; ven el **uso**, no el texto. | Es una charla privada, como un chat con Claude. |
| D6 | ¿Qué proveedor en la primera versión? | **Anthropic,** con el SDK oficial de C#, detrás de `IChatClient`. OpenAI u otro se suma después con un paquete, sin tocar el núcleo. | Una sola integración bien probada. |
| D7 | ¿Qué modelo por defecto? | **Claude Opus 5.5 con esfuerzo `low`** (lo recomendado para chat), configurable por la plataforma y por la organización con su clave: Sonnet 5.5 o Haiku 5.5 para gastar menos. La decisión de bajar de modelo es tuya: §8 tiene los números. | Calidad primero; el precio queda a la vista para elegir. |
| D8 | ¿Solo responde o también hace cosas? | **Solo responde en la v1.** En la v2, «acciones con confirmación»: propone una tarjeta («¿Invito a ana@…?») y la acción la ejecuta la pantalla, con la API de siempre. | Lo seguro primero. La v2 no necesita rehacer nada. |
| D9 | ¿Qué son las «consultas fijas»? | **Atajos que no gastan el modelo.** Son botones («¿Cuántos usuarios activos hay?») que llaman directo a una herramienta y muestran el resultado con un texto armado. Si la persona escribe, ahí sí responde el modelo. Fijos por lado en la v1, desde los recursos; configurables en la v2. | Lo más preguntado sale gratis e instantáneo. |
| D10 | ¿Topes? | Por persona: **50 mensajes por día.** Por organización: **un tope mensual en dólares** (los tokens por el precio). Al llegar, un aviso claro y no un error. Los fija la plataforma; la organización con su clave fija los suyos. | Sin tope, una sola persona puede gastar el mes. |

---

## 3. Qué se reutiliza en vez de hacerlo a mano

### Front: el chat

| Opción | Qué trae | Veredicto |
|---|---|---|
| **assistant-ui** (`@assistant-ui/react` + `@assistant-ui/react-markdown`, MIT) | Chat completo: hilo, lista de conversaciones, caja de texto, streaming, «Detener», copiar, sugerencias y markdown. Las piezas no traen estilo (estilo Radix), así que se pintan con nuestros tokens del tema. Se conecta a cualquier backend con `useExternalStoreRuntime`, y el estado lo llevamos nosotros con TanStack Query. | **Recomendado.** Es justo «un chat estándar» y encaja con nuestro front (Radix y Tailwind). |
| AI Elements (los componentes de chat sobre shadcn de Vercel, copiados al repo) | Las piezas visuales (conversación, mensaje, caja, sugerencias), pensadas para el `useChat` de AI SDK | Alternativa. Hay que escribir más estado a mano y se acopla al protocolo de AI SDK. |
| CopilotKit | Agente completo, con su runtime en Node | No: nuestro back es .NET. |
| chatscope | Mensajería genérica | No: no está hecho para LLM (streaming y herramientas). |

El markdown de las respuestas se muestra **sin HTML crudo**, el modo por defecto de react-markdown, así que no hay XSS.

### Back: el modelo y el streaming

| Pieza | Qué es | Para qué |
|---|---|---|
| **Microsoft.Extensions.AI** (`IChatClient`) | La abstracción estándar de .NET para modelos de lenguaje | Es el **puerto**: el núcleo no conoce al proveedor. Trae `UseFunctionInvocation()` (el ciclo de herramientas, con tope de vueltas) y `UseOpenTelemetry()` (cada llamada, con tokens y tiempos, en el panel de Aspire). |
| **SDK oficial de Anthropic para C#** (paquete `Anthropic`) | Cliente oficial de Claude | Tiene su integración con `IChatClient` y la invocación de funciones. Es el adaptador de Infrastructure. |
| **SSE de .NET 10** (`TypedResults.ServerSentEvents`) | Respuesta en streaming estándar | Manda el texto a medida que llega, sin WebSockets. Se devuelve desde un controller (`IResult`), sin Minimal APIs. |
| **Data Protection** de ASP.NET Core | Cifrado | Guarda cifrada la clave de la organización. Nunca se vuelve a mostrar completa. |
| **Rate limiter** de ASP.NET Core | Tope de pedidos | Un freno por persona contra ráfagas, además de los topes de negocio de D10. |

En T00 se confirman las versiones vigentes de los paquetes en `Directory.Packages.props`.

---

## 4. Datos: qué se guarda y dónde

Modelo preliminar de responsabilidades de la tabla siguiente. **No crear un esquema assistant:** el plan adapta estas entidades a `tenant.Assistant*` (Business y Personal, ITenantOwned/RLS y OwnerUserId) y `platform.PlatformAssistant*` (operador/configuración/referencias globales), con tipos separados y declaración global explícita del contribuidor E8. No tenant nullable mixto. Cuota PlatformKey técnica global y OwnKey tenant separadas según 0049; UsageDaily no sustituye esa unicidad. Ver plan T02/T06 y [multitenancy](../rules/multitenancy.md).

| Tabla | Campos clave | Clase y aislamiento |
|---|---|---|
| `Conversations` | Id, lado (Platform/Business/Personal), TenantId (si corresponde), OwnerUserId, Title, CreatedAtUtc, LastActivityAtUtc, xmin | **B2B y Personal:** `ITenantOwned` (la organización o el espacio personal), y además **solo el dueño**, controlado en el reader y el servicio. **Plataforma:** global, del operador. |
| `Messages` | Id, ConversationId, Role (User/Assistant), Text, ToolCalls (json: herramienta, duración y ok/error, **sin datos**), Status (Complete/Stopped/Failed), InputTokens, OutputTokens, Model, CreatedAtUtc | La de su conversación. |
| `Settings` | Alcance (Platform o TenantId), Mode (PlatformKey/OwnKey/Off), Provider, Model, Effort, EncryptedApiKey, topes, RetentionDays, xmin | La de Plataforma es global; la de una organización, `ITenantOwned`. |
| `UsageDaily` | Día, alcance, UserId, mensajes, tokens de entrada y salida, costo estimado | Para el uso y los topes. Sin texto. |
| `ModelPrices` | Proveedor, modelo, precio de entrada y de salida por millón | **Dato de referencia,** nunca una lista en el código ([datos-de-referencia](../rules/datos-de-referencia.md)). |

- **Nunca se loguea** el texto de una pregunta ni de una respuesta ([logs](../rules/logs.md)).
- **La baja de la cuenta** borra sus conversaciones.
- **La exportación de datos personales (E10)** las incluye.
- **Cambiar la configuración** del asistente deja su `SecurityEvent` o su entrada de auditoría, sin la clave.

---

## 5. El contrato (parte 1 del plan)

| Ruta | Acceso y permiso | Qué hace |
|---|---|---|
| `GET /api/{lado}/assistant` | el del lado + `assistant.use` | Estado: habilitado o no, configurado o no, topes y uso de hoy, y las consultas fijas |
| `GET /api/{lado}/assistant/conversations?cursor=` | ídem | Historial propio, por cursor ([paginado](../rules/paginado-y-busqueda.md)) |
| `POST /api/{lado}/assistant/conversations` | ídem, `[Idempotent]` | Crea una conversación |
| `GET /api/{lado}/assistant/conversations/{id}` | ídem; ajena → 404 | Los mensajes |
| `PUT /api/{lado}/assistant/conversations/{id}` | ídem, con `version` | Renombrar → `VersionResponse` |
| `DELETE /api/{lado}/assistant/conversations/{id}` | ídem | Borrar → 204 |
| `POST /api/{lado}/assistant/conversations/{id}/messages` | ídem, `[Idempotent]` | Pregunta y **responde por SSE**. Eventos: `delta` (texto), `tool` (herramienta y estado, por ejemplo «Consultando usuarios…»), `done` (id, tokens y uso), `error` (`code`). Si la persona corta, el mensaje queda `Stopped`. Un reintento con la misma clave devuelve el mensaje guardado, sin streaming. |
| `POST /api/{lado}/assistant/quick/{clave}` | ídem | Una consulta fija, sin modelo |
| `GET` y `PUT /api/organization/assistant/settings` | `assistant.configure` | Modo, modelo y clave (solo escribir, nunca se devuelve), topes |
| `GET` y `PUT /api/platform/assistant/settings` | `platform.assistant.configure` | Lo mismo para la plataforma, más las personas y la retención |
| `GET /api/organization/assistant/usage` · `GET /api/platform/assistant/usage` | `assistant.configure` · `platform.assistant.read` | Uso por día y por persona (organización) o por organización (plataforma) |

- `{lado}` es `me` (persona), `organization` (empresa) o `platform`, como el resto de la API.
- Permisos nuevos (catálogo, `Permissions.resx` y `.en.resx`, semilla):
  - `assistant.use`: todos los roles de sistema;
  - `assistant.configure`: el Dueño;
  - `platform.assistant.use`, `platform.assistant.configure` y `platform.assistant.read`.
- Errores: `Assistant.Module.Disabled`, `Assistant.Settings.NotConfigured`, `Assistant.Limit.DailyReached`, `Assistant.Limit.MonthlyReached`, `Assistant.Provider.Unavailable` (con reintento), `Assistant.Conversation.NotFound`, y la validación de siempre.

---

## 6. Las herramientas (lo que el asistente puede consultar)

Cada herramienta es una clase que implementa `IAssistantTool` y declara:
- nombre;
- descripción (en los recursos);
- esquema de parámetros;
- **lado**;
- **permiso requerido**.

El catálogo le da al modelo **solo las herramientas que esa persona puede usar**. Cada herramienta llama a un servicio existente y devuelve datos chicos, ya tapados como en pantalla.

| Lado | Herramientas de la v1 (cada una con su permiso) |
|---|---|
| Empresa | usuarios por estado; buscar una persona; roles y sus permisos; quién tiene un rol; empresas y sus miembros; invitaciones pendientes y vencidas; últimos cambios (auditoría); «Para resolver» |
| Plataforma | organizaciones por estado; buscar una organización; cuentas por estado; recuperaciones pendientes; últimos eventos de seguridad; uso del asistente |
| Persona | mi cuenta y mis métodos (tapados); mis organizaciones y perfiles; mis interacciones con empresas (E7) |

**Límites de cada respuesta:**
- hasta 5 vueltas de herramientas;
- salida máxima configurada;
- el prompt de sistema de cada lado, en los recursos (es y en, con voseo), dice: «respondé solo sobre este sistema; si no hay herramienta para algo, decilo»;
- lo que devuelven las herramientas se trata como datos, nunca como instrucciones;
- el prompt de sistema y las herramientas van primero y fijos, para aprovechar el **caché de prompts** (lo más caro del input queda cacheado).

**Agregar una herramienta** es una receta corta (`docs/guides/agregar-una-herramienta-al-asistente.md`):
1. la clase;
2. su permiso;
3. su texto;
4. su test de aislamiento.

Cada área nueva suma las suyas.

---

## 7. Pantallas (las dibuja Diseño antes de programar)

1. **El asistente en el inicio (compu),** para los tres lados:
   - historial a la izquierda: «Nueva conversación», renombrar y borrar;
   - «¿Qué querés consultar?» con la caja;
   - las **consultas fijas** como botones;
   - las tarjetas de «Para resolver» como bienvenida;
   - streaming con «Detener»;
   - «Consultando usuarios…» mientras corre una herramienta;
   - copiar y 👍/👎;
   - si no está configurado: «El asistente todavía no está configurado» y «Revisar configuración» (solo para quien puede);
   - al llegar al tope: el aviso.
2. **El teléfono:** la pestaña «Asistente» en la barra de abajo, con el chat a pantalla completa y el historial en una hoja.
3. **Configuración del asistente:**
   - en Plataforma: proveedor, modelo, esfuerzo, clave, topes, personas sí o no, retención;
   - en la organización: «Usar el de la plataforma» o «El nuestro» (si la plataforma lo habilitó), la clave, el modelo y los topes.
4. **Uso:**
   - en Plataforma, por organización y por mes, con el costo;
   - en la organización, por día y por persona, con los topes.
5. **Plataforma › Organización › Módulos:** «Asistente», con «puede usar su clave».

---

## 8. Cuánto cuesta (para decidir el modelo)

Una pregunta típica con herramientas: unos 3.000 tokens de entrada y 300 de salida. Con 2 o 3 vueltas de herramientas se multiplica la entrada, y el caché de prompts baja mucho ese costo.

| Modelo | Entrada / salida por millón | Una pregunta (aprox.) | 1.000 preguntas por día |
|---|---|---|---|
| Claude Opus 5.5 | USD 4 / 20 | ~USD 0,018 | ~USD 18 |
| Claude Sonnet 5.5 | USD 2 / 10 | ~USD 0,009 | ~USD 9 |
| Claude Haiku 5.5 | USD 0,10 / 0,50 | ~USD 0,0005 | ~USD 0,45 |

Las consultas fijas cuestan 0. Los precios van en la tabla `ModelPrices`, así el uso muestra el costo real.

---

## 9. Árbol de archivos

**Back** (módulo quitable, como WhatsApp):

```
src/ArquitecturaBaseMultitenant.Domain/Assistant/
  AssistantConversation.cs · AssistantMessage.cs · AssistantSettings.cs
  AssistantUsageDaily.cs · AssistantErrors.cs · AssistantSide.cs · AssistantMode.cs
src/ArquitecturaBaseMultitenant.Application/Modules/Assistant/
  Interfaces/ IAssistantService.cs · IAssistantConversationRepository.cs
              IAssistantConversationReader.cs · IAssistantSettingsRepository.cs
              IAssistantUsageRepository.cs · IAssistantChatClientFactory.cs
  Services/   AssistantService.cs · AssistantSettingsService.cs · AssistantUsageService.cs
              AssistantTurnRequester.cs · AssistantLimitGuard.cs · AssistantPromptBuilder.cs
              (los sufijos de helpers, de la lista cerrada de capas-y-flujo)
  Tools/      IAssistantTool.cs · AssistantToolCatalog.cs
              Business/ · Platform/ · Personal/   (una clase por herramienta)
  Models/ · Validation/ · Configuration/AssistantOptions.cs
  Resources/  Assistant.resx · Assistant.en.resx   (prompts, consultas fijas, textos)
src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Assistant/
  AnthropicChatClientFactory.cs   (SDK oficial → IChatClient + función + OpenTelemetry)
  FakeChatClient.cs               (Testing/E2E: respuestas y herramientas guionadas)
  AssistantKeyProtector.cs        (Data Protection)
  Persistence/ Configurations/ · Repositories/ · Readers/   (esquema assistant, RLS)
  AssistantRetentionWorker.cs     (borra lo vencido)
  AssistantRegistration.cs
src/ArquitecturaBaseMultitenant.Api/Modules/Assistant/
  Controllers/ PersonalAssistantController.cs · OrganizationAssistantController.cs
               PlatformAssistantController.cs · OrganizationAssistantSettingsController.cs
               PlatformAssistantSettingsController.cs
  Contracts/ · AssistantStreamResult.cs   (eventos SSE)
tests/…/Assistant/   (unitarios: guardas, prompts, catálogo · integración: aislamiento,
                      permisos, topes, SSE, idempotencia, retención, baja)
docs/features/asistente.md · docs/decisions/0048-asistente.md
docs/guides/agregar-una-herramienta-al-asistente.md
```

**Front:**

```
src/modules/assistant/
  api/        assistant.ts (consultas TanStack) · stream.ts (SSE sobre fetch, con AbortController)
  runtime/    useAssistantRuntime.ts   (assistant-ui ExternalStoreRuntime ↔ TanStack Query)
  components/ AssistantThread.tsx · ConversationList.tsx · QuickQueries.tsx
              ToolStatus.tsx · NotConfigured.tsx · LimitReached.tsx · UsageMeter.tsx
  pages/      AssistantHome.tsx (lo usan los tres inicios) · AssistantPhonePage.tsx
  settings/   PlatformAssistantSettingsPage.tsx · OrganizationAssistantSettingsPage.tsx
  usage/      PlatformAssistantUsagePage.tsx · OrganizationAssistantUsagePage.tsx
src/locales/{es,en}/assistant.json
```

---

## 10. El plan (según [etapa-en-paralelo](../guides/etapa-en-paralelo.md))

**Antes:**
- la ADR 0048, con §2 decidido;
- Diseño dibuja §7 y el usuario lo aprueba;
- la clave de Anthropic de la plataforma en user-secrets (`Assistant:Platform:ApiKey`).

| Parte | Tareas | Estimado (Codex) |
|---|---|---|
| **1. Contrato** | §5 en `openapi.json` y los eventos SSE en `docs/features/asistente.md` | 1 a 2 h |
| **2a. Back** | **B1:** módulo, entidades, esquema y RLS. **B2:** configuración, clave cifrada, fábrica de `IChatClient` y `FakeChatClient`. **B3:** catálogo y herramientas de la v1. **B4:** `AssistantTurnRequester` (prompt, herramientas, topes, uso, caché). **B5:** controllers, SSE, idempotencia y consultas fijas. **B6:** retención, baja y auditoría. | 1,5 días |
| **2b. Front** | **F1:** assistant-ui con nuestros tokens, runtime y SSE. **F2:** el inicio de los tres lados y el teléfono. **F3:** configuración. **F4:** uso. **F5:** Plataforma › Módulos. | 1 día |
| **3. Integración y puerta** | Conectar, el E2E real con `FakeChatClient` (determinista y sin costo) y **una prueba a mano con la clave real** | 0,5 día |

**Tests clave:**
- la conversación de A no la ve B, ni en la misma organización;
- una herramienta no devuelve nada fuera de los permisos de quien pregunta;
- un tenant no ve datos de otro;
- topes;
- streaming cortado → `Stopped`;
- reintento idempotente;
- la retención borra;
- la baja borra;
- nunca hay texto en los logs.

---

## 11. Cómo entra en las demás etapas

- **E8 (WhatsApp, módulo quitable):** va **antes**, porque deja el patrón de módulo, puertos y registro. El asistente lo copia.
- **El asistente, como etapa nueva, va justo después de la E8.** Así la E9 (endurecimiento) también lo cubre.
- **E5, E6 y E7:** solo se toca el **inicio** de cada lado (D1) y se suman Configuración, Uso y Módulos en Plataforma. Las herramientas reusan los servicios de lectura que ya existen.
- **E9 (endurecimiento):** pruebas de inyección de instrucciones, alertas de costo y topes por dirección.
- **E10 (despliegue):** la clave en el gestor de secretos, la exportación de datos personales con las conversaciones y los backups.
- **E11 (WhatsApp de atención):** el mismo asistente podría responder chats de WhatsApp de la organización, con las mismas herramientas. Es una sinergia para más adelante.

**Ideas para después:**
- las acciones con confirmación (D8);
- las consultas fijas configurables (D9);
- gráficos en las respuestas;
- enlaces a la pantalla filtrada («Ver los 3 inactivos en Usuarios»);
- un resumen diario;
- un servidor MCP con las mismas herramientas, para usarlas desde Claude o Codex.
