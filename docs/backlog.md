# Backlog de la revisión de Etapas 0 a 3c

**Estado:** en curso · lo que las revisiones dejaron para una etapa posterior.

## Observaciones de la puerta de la base de la 12

- **Un cuelgue que no se repitió.** En el recorrido del 2026-11-19, `/api/reference-data` y `/api/directory` quedaron colgados una vez en el E8-02: el país del teléfono se quedó en «Cargando…» y el idioma, vacío. No se repitió en cuatro corridas, con el corte de antes de los arreglos y con el de después. Lo que respondió no toca la base ni la caché; lo que se colgó sí. Si vuelve a pasar, mirar Redis y la conexión a la base en el entorno del recorrido. El informe está en `.local/puerta-base-12/2026-11-19/recorrido-fallo-original/`.
- **Los listados paginados no guardan caché.** En el front, `usePagedQuery` pasa `staleTime: undefined` y eso pisa los 30 s de la app: cada listado vuelve a pedirse siempre. Hay que corregirlo con un test que cuente los pedidos al volver a una lista.

## Observaciones de 3c para etapas posteriores

- `InvitationPage` sincroniza el fragmento de invitación a estado local después de retirarlo de la barra de direcciones; oxlint informa `react(set-state-in-effect)`. Refactorizar ese paso manteniendo que el token no llegue a la primera consulta mientras siga en la URL. El lint termina con código 0 y el E2E real cubre el flujo; no bloquea 3c.

## Observaciones de E4 para etapas posteriores

- **73.** En la puerta de documentación a ciegas, una respuesta ubicó una guía en `docs/guides/` sin precisar el archivo. Aclarar los punteros de `AGENTS.md` cuando se amplíen las recetas; la ubicación de la carpeta fue correcta y no afecta un flujo ni la seguridad.

## Observaciones de E6 para etapas posteriores

- La maqueta de Auditoría muestra el total de eventos del período en el subtítulo. El endpoint actual usa cursor y evita contar toda la tabla append-only; la pantalla conserva un subtítulo genérico en lugar de mostrar una cantidad parcial como si fuera el total. Definir una estrategia de recuento solo si se prioriza esa cifra.

## Observaciones de E5 para etapas posteriores

- El comentario «El backoffice comienza en E9» en `../ArquitecturaBaseMutitenantFront/src/layouts/navigation/platform.ts` quedó desactualizado: Plataforma se implementa en E5. Corregirlo cuando se actualice esa navegación; el comentario no rompe un flujo ni cambia la autorización.

## Observaciones de 3b para etapas posteriores

- Publicación administrativa de versiones legales: usar la administración prevista para plataforma cuando nazca su pantalla. La 3b permite leer/aceptar; el E2E publica solo en su propia base y el recorrido manual tiene SQL local explícito.
- Propagar `maxLength` desde las validaciones de texto al OpenAPI para que el formulario derive límites del contrato. El límite actual de nombre se valida en ambos lados y el backend sigue siendo autoridad.
- Probar las plantillas en la matriz de clientes de correo (Gmail, Outlook y otros). La 3b ya incluye el fallback sRGB equivalente al token de marca para clientes sin `oklch`; se verificó el HTML real en Chromium.
- Conciliar detalles de iconografía, interlineado y alineación de menús/toasts de Radix/Sonner con los prototipos, conservando foco y teclado. La evidencia de 3b está en el informe visual del front; no afecta sus flujos y se presenta para aprobación visual en la puerta 9.

El [panorama vigente](architecture/backend.md) describe la arquitectura; los pendientes de esta sección se conservan acá.

## Cuando una pantalla use la pieza

- **2.** DateField, DateTimeField y TimeField no se pueden completar desde el teclado de un teléfono
- **3.** MoneyField cambia la moneda sola cuando el importe queda vacío o inválido, o después de un reset
- **6.** Los instantes en query o ruta no pasan por la regla UTC: un valor sin offset entra y termina en un 500
- **24.** Controles de shared/ui con blancos táctiles menores a 44 px y con zoom de iOS al enfocar
- **25.** Enter en el buscador de los selectores envía el formulario
- **26.** Los seis campos con borrador ignoran los cambios del valor que vienen del padre (reset, «Ver lo nuevo»)
- **27.** NumberField y PercentField muestran un valor redondeado y envían otro
- **28.** PhoneField y TaxIdField quedan inválidos, y nunca vuelven a null, cuando la persona vacía un campo opcional

## Etapa 9 o descartar

- **4.** Las reglas no negociables 2 ("una vez por método") y 10 (OperationLog en cada método público) no tienen verificación
- **7.** La cadena real de interceptores con auditoría compartida nunca se prueba contra la base
- **8.** El diff de auditoría enmascara por nombre de propiedad, no por tipo
- **9.** Los grants de mt_app solo cubren las tablas de E2 y dan DML completo sobre los catálogos globales
- **11.** Runtime_role_validator_rejects_superuser es un falso verde
- **13.** Ningún test verifica que las claves de HybridCache salgan de CacheKeys
- **14.** El inventario de RLS solo mira los nombres de las políticas
- **15.** Los archivos modelo de 25 punteros nunca se verifican (solo se mira "Copiá de:" con dos puntos)
- **16.** NoManualFormattingTests sigue sin ver la interpolación sin formato (`$"{monto}"`) ni con alineación
- **17.** SensitiveToStringLoggingTests da verde con contratos que sí filtran datos en ToString()
- **18.** TenantScopeUsageTests habilita a TenantJobRunner, que cualquier clase de Infrastructure puede usar para entrar a cualquier organización
- **19.** VersionedContractTests asocia contratos y entidades por el nombre, y no ve un DELETE sin cuerpo
- **20.** react/jsx-no-literals con la configuración por defecto no frena textos en llaves ni en atributos
- **21.** Nadie verifica «sin any ni @ts-ignore», aunque la ficha dice que lo hacen tsc y oxlint
- **29.** Punto 17 incompleto: el contrato no tiene el caso 0.082/«8,2 %» con parseInput y el lector del front no lo lee
- **30.** El bloque de código de la ficha de guardado no compila (le falta TimeProvider a OperationLog.RunAsync)
- **31.** El script de importación pasa cada secreto como argumento de línea de comandos
- **32.** Respuesta pública que cambia según Accept-Language sin `Vary`, y el If-None-Match no admite listas ni ETag débiles
- **33.** El handler global loguea la excepción completa (mensaje incluido) que OperationLog evita loguear a propósito
- **34.** La metadata de un Error puede duplicar miembros RFC (status, title, type) en el JSON del ProblemDetails
- **35.** Una excepción o un 5xx después del commit libera la clave y el reintento duplica
- **36.** El replay pierde Location y los demás encabezados de la respuesta original
- **37.** El texto que entra por la query no se limpia: la búsqueda recibe invisibles y NFD sin normalizar
- **38.** MoneyJsonConverter acepta importes que ni el front ni numeric(19,4) pueden representar
- **39.** Al escribir, un DateTime Unspecified sale con Z y uno Local se convierte con la zona del servidor
- **40.** Money no tiene esquema OpenAPI: el conversor propio deja el esquema vacío y el front genera `unknown`
- **41.** Los tres secretos de PostgreSQL de E2 no están documentados ni los revisa verificar.ps1
- **42.** La API tipada de DisplayFormatter cubre 5 de los 23 formatos: fecha civil, duración, relativo, enum, id fiscal y correo siguen solo en el despachador interno
- **43.** PartyPolicy no puede verificar el acceso activo: la parte activa la declara el llamador
- **44.** Culture.Create rechaza separadores de miles que son espacios (NBSP/NNBSP): sumar fr-FR, pt-PT, ru-RU o pl-PL rompe el arranque
- **45.** PhoneNumber rechaza números E.164 válidos de 7 dígitos
- **46.** Una organización con error detiene el trabajo de todas las siguientes
- **47.** (Resto tras B2 de la E9) Ninguna guarda verifica que toda clave de caché salga de `CacheKeys`: `CacheArchitectureTests` vigila quién llena la caché, no cómo se arma la clave
- **48.** Una carrera con ReleaseAsync convierte un reintento legítimo en 500
- **49.** Búsqueda y cursor van como constantes SQL: un plan y una compilación de EF por cada término o cursor
- **51.** Enter borra TenantKind: dentro de un alcance técnico no se pueden crear datos públicos ni compartidos
- **52.** Enter no detecta una conexión ya abierta, y la GUC de sesión queda desfasada
- **53.** Ningún test asegura que cada zona habilitada del catálogo (IANA 2026d) exista en el runtime
- **54.** /health (readiness) no revisa la base aunque el doc y el comentario dicen que se sumaba en E2
- **55.** El chequeo de double/float solo mira las subclases de Entity y deja afuera 12 de las 14 entidades mapeadas
- **56.** ErrorCodeTests no detecta claves huérfanas de ArquitecturaBase ni revisa ApiErrorCodes
- **57.** Las guardas de guardado y de filtros no ven SQL crudo ni transacciones ambientales
- **58.** Documentación que contradice al código: f.money() no existe y el httpClient no reintenta solo
- **59.** Un 2xx con cuerpo no JSON produce un SyntaxError que se reintenta y se traga sin aviso
- **60.** ProblemDetails está escrito a mano aunque ya existe en generated/, y declara la nulabilidad distinta
- **61.** La guarda de formato manual no detecta toLocaleTimeString, Date.parse, Date() sin new ni toPrecision
- **62.** formatEmail valida el hostname de new URL pero devuelve el dominio crudo, con ruta, puerto o query
- **63.** Un catálogo inconsistente hace lanzar a FormatProvider fuera de toda ErrorBoundary y la app queda en blanco
- **64.** Hooks compartidos sin test, y useMediaQuery no se resincroniza al cambiar la consulta
- **65.** Los encabezados ordenables no bajan de línea y no muestran el sentido del orden
- **66.** FormField marca «obligatorio» solo a la vista
- **67.** La guarda de columnas reales va a fallar con el primer columns.tsx correcto
- **68.** El nombre accesible de CountrySelect reemplaza el rótulo del FormField, y MultiSelect no expone lo elegido
- **69.** EmailField con type="email" altera y rechaza correos que el contrato acepta, y no normaliza a NFC
- **70.** TaxIdField oculta y da por inválido un tipo fiscal guardado que después se deshabilitó
- **71.** El halo de foco de los controles no usa --foco-halo, como piden tema.md y accesibilidad.md
- **72.** renderWithProviders usa un QueryClient sin el manejo global de errores de producción

## Traspaso 15, el núcleo: para programar

Lo que trajo el traspaso 15 de ArquitecturaBase para el núcleo (su traspaso 14, `02-nucleo.md`) y pide código. Va en la [segunda parte de la base](plans/2026-11-19-etapa-base-de-la-12-segunda-parte.md), antes de la E12 (214), salvo lo que nombra otra etapa. Nada de allá pasó todavía su prueba integral, y un detalle puede cambiar con el traspaso 16.

- **Nadie da lo que no tiene** ([ADR 0061](decisions/0061-nadie-da-lo-que-no-tiene.md)). Un `*Guard` por alcance, usado dentro del límite: en la organización, por `RoleService` y los casos de uso de `OrganizationUsers` (editar, estado, quitar, invitar); en cada empresa, por `CompanyMembershipService`; en Plataforma, por `PlatformOperatorService`. Errores en `Errors.resx` y `.en.resx`, y tests con cuentas que tienen solo cada permiso.
- **Retención de los datos del ingreso** ([ADR 0062](decisions/0062-retencion-de-los-datos-del-ingreso.md)). Purga por lotes de `identity.LoginCodes`, `identity.LoginAudits` y OpenIddict en un scope global, y de las invitaciones cerradas con `TenantJobRunner`; sus índices; el test de que cada tabla con datos personales nombra quién la purga; y el texto de la auditoría de Plataforma que dice que los ingresos se guardan doce meses.
- **Topes de correo** ([ADR 0063](decisions/0063-topes-de-correo-de-la-plataforma.md)). Opciones `Email:*` validadas al arrancar, contadores en una tabla global, el 429 con `retryAfter`, la espera en horas y minutos en el front, el aviso en el log al 80 % y al llegar, y las claves en `docs/operations/configuracion.md`.
- **IPv6 por su /64** (N20) en `RateLimitIpHasher` y `WhatsAppQuotaIpHasher`, antes del HMAC ([cuotas](rules/cuotas.md)).
- **El bloqueo no se usa contra otro** (N3): que no frene a Google (`GoogleAccountGuard`, `ExternalLoginService`) y que `LoginCodeAttemptVerifier` sume solo contra un código activo y diga `LockedOut` después de un código correcto ([identidad](features/identidad.md)).
- **El correo no se pierde ni sale dos veces** (N5, N8 y O3): en `OutboxDispatcher`, clasificar las fallas de SMTP, volver a pendiente lo que falló antes del cuerpo, no repetir lo incierto, plazo propio dentro del apagado y el código que vence en la cola.
- **Estado de entrega de la invitación** (`Pending`, `Sent`, `Uncertain` y `Failed`), con un chequeo `email` en Degraded ante un 535 y contadores de enviados y descartados ([organizaciones](features/organizaciones.md)). Mostrarlo en Usuarios es un cambio de pantalla y se dibuja antes.
- **La resiliencia por defecto no reintenta un POST** (N18): `ServiceDefaults/Extensions.cs` usa `AddStandardResilienceHandler()` sin `DisableForUnsafeHttpMethods`. WhatsApp y Anthropic ya lo hacen por su cuenta.
- **El 500 se puede rastrear** (N10): la persona y la organización en el scope del log, y la consola en JSON fuera de desarrollo ([logs](rules/logs.md)). El exportador OTLP y la alerta de 5xx van con la E10.
- **Salir cierra las demás pestañas de ese lado** (N12), en el front, con `BroadcastChannel`: la pública o la de Plataforma, nunca las dos ([ADR 0044](decisions/0044-sesion-de-plataforma-aparte.md)).
- **Cada pantalla pide solo los conteos que muestra** (N30): revisar los `filter-counts` que pide el front (Roles, Usuarios, Empresas, Cuentas, Recuperaciones y Organizaciones).
- **Textos neutrales en el núcleo** (N22): que ningún texto del núcleo nombre un módulo.
- **Sin Formularios, el asistente de WhatsApp anda** (B5): al programar la E23 y la E25, pedir las capacidades `forms.*` solo si un dueño instalado las declara.
- **La plantilla sin datos de la instalación** (B1): pasar a user-secrets el `ClientId` de Google, el Gmail (`Email:Smtp:UserName` y `FromAddress`) y los ids de Meta (`WhatsApp:PhoneNumberId`, `AppId` y `EmbeddedSignupConfigId`) de `appsettings.Development.json`; invertir `StartupConfigurationTests.Development_settings_enable_Google_and_Gmail_without_committing_credentials`, y poner al día la §3 y la §7 de [configuración](operations/configuracion.md), que hoy los da como «no secretos, en el repo», y los pasos de `user-secrets` de la §2.
- **Borrar con el ETag del listado** (resto de N28): hoy no aplica, porque las claves son al azar. Va en el adaptador de Azure (E10) y en la E18 si alguna clave fuera determinística.

## Traspaso 15, diseño, fechas y números: para la segunda parte de la base

Lo que trajo el traspaso 15 para toda la app (decisiones 205 a 213 de la [hoja de ruta](plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md)) y pide código. Va en la **segunda parte de la base, antes de la E12**, junto con las tareas del núcleo de arriba, con una sola puerta (214). Las reglas ya están en las fichas del front, en [fechas y zonas](rules/fechas-y-zonas.md) y en el lienzo. Nada de allá pasó todavía su prueba integral: un detalle puede cambiar con el traspaso 16.

- **Fechas solo con números** (205). `cultures.json`: `DateTimePattern` «dd/MM/yyyy, HH:mm» y «MM/dd/yyyy, h:mm tt», y `LongDatePattern` sin uso, con la carga de los datos de referencia ([datos de referencia](rules/datos-de-referencia.md)); `docs/contracts/format-cases.json` y sus tests de los dos lados. `DisplayFormatter`: el mes como rango («del 01/10/2026 al 31/10/2026» en una frase, «01/10/2026 – 31/10/2026» como rótulo) en lugar de `FormatMonth` y `FormatMonthLabel`, y el correo del tope del asistente (`AssistantBudgetEmailRenderer`). En el front, `formatMonth` como rango, `MonthField` (cerrado, el rango; abierto, los meses con su número) y la banda de «Uso del asistente». El PDF, el Excel y el asistente toman el mismo patrón.
- **El porcentaje pegado** (208): `PercentPattern` de es-AR «{number}%», sus casos y `DisplayFormatter.FormatPercent`.
- **Un número ambiguo en la app** (207): `parseDecimal` (`shared/format/parsers.ts`) lee con la región de la cultura efectiva y marca «1.500» con `numberAmbiguous`, con su texto en es y en. Lo de WhatsApp va con la E14 (T9).
- **Avisos** (210): el `Toaster` (`shared/ui/sonner.tsx` e `index.css`) blanco, con el filo y el ícono de cada tipo y la ✕ adentro a la derecha; la franja oscura del teléfono, con el mismo filo. `toastError` se va a los 10 s, y un aviso con una acción, como «Copiar código», queda. La franja de error de un formulario (`FormError`), el cartel que bloquea una pantalla y la banda roja del teléfono, blancos con el filo. Con las capturas de cada tipo en la compu y en el teléfono.
- **Lo apagado y la (i)** (211): el tooltip que informa (`InfoTip`, `disabledReason` de `Button`, `CheckboxField`, `RadioGroupField` y `RowAction`) se abre también con un clic, un toque, Enter o Espacio, y queda hasta Escape o tocar afuera, sin hacer la acción; responde el control entero (la casilla con su texto, el renglón de una opción); en el teléfono reemplaza la franja oscura y la hoja chica. `TooltipContent` respeta los saltos de renglón.
- **El ⋮** (209): revisar con la regla los encabezados del núcleo. Ya cumplen los editores, los ajustes y los listados. En Usuario, «Quitar de la organización» a la vista se queda: aparece solo cuando la persona pidió borrar su cuenta, y es lo que espera una decisión, como aprobar. Lo que se programe después (catálogos y respuestas) nace con la regla.
- **Un dato por columna** (206): `DataTable` vuelve a dibujar un texto secundario en gris debajo del dato, que no se ordena ni se filtra, y se revisan con la regla las columnas del núcleo que juntan dos datos.
- **Roles como matriz** (212): `PermissionPicker` y `RoleEditorPage`, también en los roles de una empresa y en los permisos de un operador de Plataforma: la matriz desde 768 px y una banda por área más angosto, con `PermissionPicker.test.tsx` en las dos formas.
- **Las piezas** (213): el título de un grupo de opciones con su (i); un chip de solo lectura sin ✕; las teclas que mueven la pantalla, con el foco en el menú; el desplegable de 48 en el teléfono; las confirmaciones del núcleo en una frase, repasadas con la [lista de chequeo](../../ArquitecturaBaseMutitenantFront/docs/design/lienzo/README.md#lista-de-chequeo-de-diseño).

## Traspaso 15, la IA: para la segunda parte de la base

Lo que trajo el traspaso 15 para toda la IA (la revisión de la IA de ArquitecturaBase, su traspaso 14, `05-ia.md`) y cae sobre el chat del Inicio ya programado: va en la **segunda parte de la base, antes de la E12**, con una sola puerta (214). Las reglas están en la [ADR 0064](decisions/0064-reglas-de-toda-la-ia.md) y en la [ficha del asistente](features/asistente.md#reglas-de-toda-la-ia-traspaso-15-sin-programar); los tableros, en el lienzo v175. Lo del canal, la transcripción, los documentos y armar va con la E23 y la E25. Nada de allá pasó todavía su prueba integral: un detalle puede cambiar con el traspaso 16.

- **El cupo vuelve solo si no se intentó** (0064 §1): revisar que la recuperación y el vencimiento de un turno sin `BeginCall` devuelvan el cupo diario y la reserva del mes, y que lo intentado no; el tope de turnos pendientes, 4 por copia.
- **Sin cupo no se guarda el archivo** (§2): `AssistantAttachmentService` mira el cupo diario y el tope mensual antes de leer el cuerpo, y responde el 429 de siempre.
- **Las subidas** (§4): un lugar por persona, sin cola, y la cola de la copia con dos lugares por organización como mucho; el plazo de 120 s del cuerpo y el 429 con `retryAfter`; el mismo archivo dos veces con el mismo pedido.
- **Lo que se borra** (§5): la confirmación de borrar dice «Se borran también sus archivos.» (front); la purga con índices parciales, y abrir el Inicio y el claim cuentan solo lo pendiente.
- **Las versiones** (§6): lo sellado se escribe un despliegue después; un turno con algo desconocido queda pendiente; `unsupportedProvider` en la configuración, con la conexión «Pausada»; la conversación ilegible, «no disponible» y borrable; la pestaña que descarta lo desconocido.
- **Las fallas del proveedor** (§7): los seis códigos por el estado HTTP, sin leer el cuerpo, con sus textos en `Errors.resx` y `.en.resx`; la clave que no se descifra no gasta cupo; quién ve cada falla según quién paga, en `Asistente-Config-Org` y `Plat-Asistente-Config`.
- **El apagado** (§8): el plazo de vaciado, lo no reservado de vuelta a pendiente y `Assistant.Turn.Interrupted`, con su texto en el chat. Va con el apagado de la B6 de la E10.
- **La fecha** (§9): la hora del pedido y la zona del perfil al final del mensaje del Inicio.
- **El anillo y la traza** (§11): `platform.AiKeyRingControls` con su HMAC, la readiness que falla con otro anillo y la traza de quien pidió cada turno.
- **Los tests** de la «Verificación» de la 0064 que tocan al Inicio.
- **El diálogo de un modelo, en pasos** (decisión 254): en `PlatformAssistantSettingsPage`, agregar un modelo pide los datos técnicos en un segundo paso, y editarlo los muestra como lectura, con «Cambiar datos técnicos»; sale el plegado «Avanzado: datos técnicos». Tableros: `Plat-Asistente-Config` y `-23`, con sus teléfonos.

## Traspaso 15, WhatsApp y ARCA: para programar

Lo que trajo el traspaso 15 para WhatsApp como canal y para ARCA (tanda 5) y pide código. Lo de WhatsApp cae sobre lo ya programado de la E11: va con la **segunda parte de la base** (214) y, a más tardar, antes de la E14, que suma contenido cifrado y el envío de formularios. Nada de ArquitecturaBase pasó todavía su prueba integral, y un detalle puede cambiar con el traspaso 16.

- **El chat cifrado** ([ADR 0065](decisions/0065-contenido-del-chat-cifrado.md)). Pasan a `bytea` el `Text` de los mensajes de la organización y de Plataforma, el `LastMessagePreview` de cada chat y el `Text` de `engagement.WhatsAppLinkedMessages`. Un conversor de EF toma la organización del mismo contexto que fija `app.tenant_id`, con un subpropósito de Plataforma y uno propio de `engagement`; sin organización en `tenant.*`, falla. La migración vacía los chats de prueba, siempre que entre antes de la I1 de la E10. Tests: dos organizaciones no leen el contenido de la otra (un cifrado copiado a otra fila se lee vacío), lo que no se abre se lee vacío y la bandeja sigue buscando por nombre y número.
- **«Sin confirmar»** ([ADR 0066](decisions/0066-lo-que-no-se-sabe-si-salio.md), decisión 240). El outbox suma el estado como capacidad neutral, junto con la tarea del correo del núcleo (N5, N8 y O3), y el transporte de WhatsApp clasifica sus fallas: lo que no salió se reintenta a los 30 s, dos veces, con el chat esperando; lo que no se sabe no se repite. Además:
  - la revisión, con quién y cuándo, por el id del envío;
  - los ocho códigos de falla, con sus textos en `Errors.resx` y `.en.resx`;
  - la invitación «Sin confirmar» en Usuarios, con la tarea de su estado de entrega;
  - el apagado (H8) y la prioridad de lo que contesta sobre los avisos (H31).

  La revisión en la conversación y en Usuarios es un cambio de pantalla y se dibuja antes.
- **Los topes de códigos por WhatsApp** (H10): 10 por cuenta y por día para sumar o verificar un número desde Mi cuenta, como el de correo de la [ADR 0063](decisions/0063-topes-de-correo-de-la-plataforma.md). La IPv6 por su /64 ya está en las tareas del núcleo.
- **Dos versiones en WhatsApp** ([ADR 0060](decisions/0060-dos-versiones-conviven.md), con la B6 de la E10): un mensaje recibido de un tipo desconocido se guarda como «Otro», y una salida que esta versión no entiende vuelve a pendiente para otra copia, hasta 10 minutos. Lo de la respuesta de Flow (cinco fallas y un día) va con la E14.
- **Las métricas sin organización ni empresa** ([logs](rules/logs.md)): un test que recorra los instrumentos y rechace una etiqueta de organización, de empresa o de un dato de un mensaje, junto a `TenantTelemetryTests`.
- **Quitar WhatsApp y el respaldo** (H22 y H15): al sumar sus tablas en la E14 a la E25, la [guía de quitar](guides/quitar-whatsapp.md) las cuenta otra vez, y el [runbook](operations/runbook.md) suma, con el cifrado, que una base restaurada sin el certificado lee vacío el contenido de los chats.
- **La CUIT nunca once ceros** (ARCA, A3 de la tanda 5): `TaxId` rechaza `00000000000` en el dominio, y `TaxIdField` en el front, con su test de cada lado. Hoy el verificador lo da por válido (suma 0, dígito 0). La línea de [identificación fiscal](rules/identificacion-fiscal.md) se suma con el código.
- **Para decidir antes de producción: la retención de las respuestas de formularios.** ArquitecturaBase la dejó para decidir antes de producción, y acá no está en ninguna hoja.
