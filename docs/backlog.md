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

## Queda afuera de la segunda parte de la base

Lo que trajeron los traspasos 15 a 18 y la [segunda parte de la base](plans/formularios/base-de-la-12-segunda-parte.md) mandó a otra etapa. Lo demás de esos traspasos ya está programado y vive en sus fichas. Nada de ArquitecturaBase pasó todavía su prueba integral.

- **El 500 se puede rastrear** (N10): el exportador OTLP y la alerta de 5xx van con la E10. La persona y la organización en el scope del log y la consola en JSON fuera de desarrollo ya están ([logs](rules/logs.md)).
- **El apagado del asistente** ([ADR 0064](decisions/0064-reglas-de-toda-la-ia.md) §8): el plazo de vaciado, lo no reservado de vuelta a pendiente y `Assistant.Turn.Interrupted`, con su texto en el chat. Va con el apagado de la B6 de la E10.
- **Dos versiones en WhatsApp** ([ADR 0060](decisions/0060-dos-versiones-conviven.md), con la B6 de la E10): un mensaje recibido de un tipo desconocido se guarda como «Otro», y una salida que esta versión no entiende vuelve a pendiente para otra copia, hasta 10 minutos ([WhatsApp](features/whatsapp.md)).
- **El número ambiguo por WhatsApp** (207, T9) y **la respuesta de Flow** (cinco fallas y un día) van con la E14.
- **La IA fuera del Inicio** ([ADR 0064](decisions/0064-reglas-de-toda-la-ia.md)): el canal, la transcripción, los documentos, armar, `ProposalTooLarge`, los topes de lectura y los hechos del negocio van con la E23 y la E25.
- **Sin Formularios, el asistente de WhatsApp anda** (B5): al programar la E23 y la E25, pedir las capacidades `forms.*` solo si un dueño instalado las declara.
- **Borrar con el ETag del listado** (resto de N28): hoy no aplica, porque las claves son al azar. Va en el adaptador de Azure (E10) y en la E18 si alguna clave fuera determinística.
- **Quitar WhatsApp** (H22): al sumar sus tablas en la E14 a la E25, la [guía de quitar](guides/quitar-whatsapp.md) las cuenta otra vez.
- **Para decidir antes de producción: la retención de las respuestas de formularios.** ArquitecturaBase la dejó para decidir antes de producción, y acá no está en ninguna hoja.

## Lo que vio la prueba integral de ArquitecturaBase

Las observaciones de su prueba integral, del 19 al 23/11 (traspaso 16, `05-prueba-integral.md`, y traspaso 18, `03-prueba-integral.md`, en `docs/plans/` de ArquitecturaBase), anotadas como riesgos para cuando se programe cada etapa: nada de esto es una decisión. El plan de cada etapa las nombra en su lista de control.

- **E12 y E13, catálogos:** el tope de un campo de texto es el mismo en la pantalla y en el servidor, y al llegar se ve (O13); la plantilla y la exportación nombran los campos igual (O14); la comparación de una revisión lista los cambios de las opciones antes de «Usar la revisión actual» (O15).
- **E14, el Flow:** la ayuda de un número pide la coma en español, con el patrón que nombra primero la coma (`[,.]`) y sin una coma antes de `]` o `}` en ningún texto fijo (O29, 207); un número ambiguo vuelve con su pregunta marcada y nombrada en el mensaje (O30); «1.500,5», con separador de miles, lo frena Meta con su propio mensaje en inglés, coherente con la ayuda «sin separador de miles» (O42); una respuesta de Flow que se pierde no la ve nadie (la persona ve «Respuesta enviada» y la sesión queda pendiente), así que del webhook se guarda lo necesario para diagnosticarla (O49).
- **Las listas dependientes en el Flow** (94, después del bloque): el tope de 200 de Meta es por valor del campo y para la lista de valores, nunca para el total del catálogo (O31).
- **E16 y E20, las listas según otra respuesta:** guardar un filtro sin elegir la respuesta se frena en la pantalla y marca solo el combo que falta (O16).
- **E18:** el visor de fotos muestra la fecha de cada una (O21).
- **E19:** la vista previa del formulario aplica las condiciones (O17).
- **E20:** el cálculo en vivo lee los números con `parseDecimal` y la cultura de la cuenta, y no muestra resultado mientras la validación marque uno como ambiguo; «Enviada» usa la misma lectura (O19). A 1024 px, «Solo en la app» entra en su tarjeta (O18).
- **E21:** el panel de revisar y la pantalla completa cuentan y recorren lo mismo («1 de 2 por aprobar») (O22); «Enviada» dice el estado que quedó después de las reglas, también «Por resolver» (O23).
- **E23:** una sugerencia hacia un formulario que publicó otra versión lo nombra y dice qué hacer (O20); una sugerencia armada antes de tomar la conversación y mandada después la rechaza el servidor con un 409 claro, que la pantalla no muestra como un resultado incierto (O32); un audio que el proveedor devuelve vacío no deja a la persona sin respuesta (allá queda sin derivar); y ante un pedido que junta un formulario y otra pregunta, el motor propone solo el formulario (154).
- **E25:** después de importar en un catálogo que propuso la IA, la cabecera muestra su estado sin recargar (O24); Meta no devuelve en la respuesta el archivo precargado en un Flow, así que la copia guardada cuenta antes del mínimo de la pregunta (O38); el resumen no dice dos veces el código y la opción que salen de la misma columna (O34); «Este comprobante ya se cargó» no se dice si el primero solo se propuso (O37 y O46); el banco de origen que se ve en el logo queda sin leer (O45); y el monto de un comprobante de débito llega negativo, y un negativo resta en el tablero (O47, importante, sin decidir). Lo que no tiene la forma de su validación ya lo decide la 277.
- **Toda la app:** después de cerrar un diálogo, el primer clic en otra fila o en el menú funciona (O2); al llegar al tope de caracteres, el campo lo dice (O26); un dato que falta dice «—», nunca «?» (O9); con la Api caída, recargar no deja «Cargando…» para siempre: a los segundos dice qué pasa y ofrece «Reintentar» (O40); en desarrollo, StrictMode lee dos veces y puede chocar con una cuota (O33), así que un «Hiciste demasiadas solicitudes» ahí se mira antes de culpar al código. En el ingreso, después de un código equivocado, escribir encima reemplaza lo de las casillas (O5, a confirmar con el teclado). A 320 px, el nombre largo de un área de un rol baja de renglón sin tapar su (i) (O11).
- **No aplican acá:** O4 (allá tampoco un administrador corrige el correo de otra cuenta; si se decide, llega en el traspaso 18), O6 («Arquitectura Base» se lee bien en el asunto), O8 y O12 (eran del panel de carpetas, que se fue), O25 (el front no valida con esquemas estrictos: los tipos salen del OpenAPI y `contracts:check` los compara), O27 (ya está en [configuración](operations/configuracion.md), §4) y O28 (la [ADR 0043](decisions/0043-las-pantallas-dicen-si-hay-cuenta.md) ya lo resuelve así).

## Para después de la etapa base-de-la-12-segunda-parte

- **Borrar `LongDatePattern` de `Cultures`, del generador y de la respuesta de reference-data.** Queda sin uso desde la B10, y según la 0060 se borra en un despliegue posterior. Dónde: `scripts/datos-de-referencia/cultures.source.json`, `generar.mjs`, `Domain/ReferenceData/Culture.cs`, `CultureConfiguration.cs`, `ReferenceDataHttpResponse.cs` (spec, sección Datos) (B10).
- **`InvitationAcceptanceGuard` (aceptar una invitación con la sesión abierta), `InvitationFlowContext.SignInAsync` y `GoogleMethodLinker` (vincular Google desde Mi cuenta) todavía rechazan una cuenta bloqueada.** Después de la B4, quien entró con Google estando bloqueado no puede aceptar una invitación ni vincular Google hasta que termine el bloqueo. La spec nombra solo `GoogleAccountGuard` y `ExternalLoginService`; si el bloqueo por códigos frena también estos flujos con sesión es una decisión de producto que no está escrita. Dónde: `Application/Services/Invitations/InvitationAcceptanceGuard.cs:55`, `Api/RequestContext/InvitationFlowContext.cs:89`, `Application/Services/Identity/GoogleMethodLinker.cs:23` (B4).
- **El contexto del worker (`AssistantTurnWriter.ClaimAsync` / `ListMessagesAsync`) todavía hace `Enum.Parse` del rol y el estado: si una conversación ilegible recibe un turno nuevo, el claim lanza en vez de saltear lo que no conoce.** La 0064 §6 dice que la pestaña descarta lo que no conoce. Dónde: `Application/Modules/Assistant/Services/AssistantTurnWriter.cs` (ClaimAsync) y `AssistantTurnRequester.AdmitAsync`; spec, paso 15 (B14).
- **Que `importar-desde-arquitecturabase.ps1` también copie a user-secrets el ClientId de Google y la cuenta de Gmail (UserName y FromAddress).** Hoy copia solo los seis secretos; ahorraría pasos en I1. Dónde: `scripts/secretos/importar-desde-arquitecturabase.ps1` y `docs/operations/configuracion.md` §2 (B9).
- **En la §3 de configuracion.md, la fila de `Authentication:Issuer` dice `https://localhost:5174/`, pero `appsettings.Development.json` usa `https://localtest.me:5174/`.** Dónde: `docs/operations/configuracion.md` §3 (B9).
- **Que `--clase` de `verificar.mjs` acepte el nombre corto de la clase, o avise si no corrió ningún test, en vez de «0 de 0 pasaron, 1 con error».** Dónde: `scripts/arneses/verificar.mjs` (planDe y el resumen de tests) (B14).
- **Ordenar la zona horaria de las empresas por la ciudad que se ve, no por su id IANA.** Hay que unir los datos de referencia en la cultura de quien pide. Dónde: `Infrastructure/Persistence/Readers/CompanyReader.cs` (SortMap, `timeZoneId`); pantalla Empresas (B17).
- **Que la columna «Atendió» de las conversaciones de la ficha de un contacto pueda ordenar por el nombre de quien atiende.** Hoy no puede: el módulo WhatsApp no lee identidad (IdentityStorageAccessTests). Haría falta un puerto del núcleo que ordene por el nombre de una cuenta, o guardar el nombre en el chat; las dos son decisiones de arquitectura. Dónde: `Infrastructure/Modules/WhatsApp/Persistence/WhatsAppContactReader.cs` (ConversationSort) y la tabla de conversaciones de WA-Contacto (B17).
- **Que la retención de 90 días de `identity.WhatsAppMessages` (la copia global de la entrada) tome el plazo de los chats de la plataforma o quede como técnica con su motivo en el inventario.** La B18 pide que cada plazo de negocio vaya a la configuración de su dueño, y la spec no dice cuál corresponde. Dónde: `Application/Modules/WhatsApp/Services/WhatsAppMessageRetentionService.cs` y `docs/operations/configuracion.md` (inventario 270) (B18).
- **Sumar «Ordenar por» en Plantillas y Equipos de WhatsApp, o confirmar que alcanza con el encabezado.** Esas pantallas no tienen FilterBar y su tablero no dibuja una barra. Dónde: `src/modules/whatsapp/templates/pages/TemplatesPage.tsx` y `src/modules/whatsapp/teams/pages/TeamsPage.tsx` (F12).
- **Ver si el aviso de un equipo («notice») ordena por un número o por un texto.** Hoy ordena como texto; si el back ordena por minutos, le corresponde `sortAs: "number"`. Dónde: `src/modules/whatsapp/teams/columns.tsx` y `WhatsAppTeamReader.cs` (SortMap) (F12).
- **Ordenar por columnas el uso del asistente (por día, por persona y el de Plataforma por organización).** `/api/organization/assistant/usage` pagina sin `sort`. Dónde: `src/modules/assistant/usage/AssistantUsageRows.tsx` y su reader en el back (F12).
- **Ordenar por columnas el catálogo de modelos del asistente.** `/api/platform/assistant/models` pagina sin `sort`. Dónde: `src/modules/assistant/settings/ModelCatalog.tsx` y su reader en el back (F12).
- **Ordenar por columnas los números de WhatsApp.** `/api/organization/whatsapp/numbers` pagina sin `sort`. Dónde: `src/modules/whatsapp/numbers/pages/NumbersPage.tsx` y su reader en el back (F12).
- **Ordenar los dominios de una organización por Estado y por TXT.** El SortMap de `TenantDomainReader` solo tiene `domain`. Dónde: `Infrastructure/Persistence/Readers/TenantDomainReader.cs` y `src/areas/platform/tenants/tabs/OrganizationDomainTab.tsx` (F12).
- **Darle al emisor de la app autenticadora de los operadores una clave propia, o tomarlo de «Nombre y correos», antes de borrar `Email:AppName`.** El emisor de TOTP todavía lo lee; si se borra la clave, cambia el nombre que ven los autenticadores nuevos. Dónde: `Infrastructure/Security/PlatformFactorSecrets.cs:54` y el inventario de `docs/operations/configuracion.md` (B18).
- **Actualizar la confirmación «¿Guardar la configuración?» de Config-Plat para que nombre los plazos y el nombre y los correos.** Hoy dice solo cómo entran las personas y organizaciones y los límites de uso. Dónde: `src/locales/es/platformSettings.json` (confirmDescription) (F13).
- **Un texto propio para el tope de «Conversaciones del Inicio» en Asistente › Configuración de Plataforma, que nombre el máximo.** Con más de 30 días el campo dice «Ingresá un número entero mayor que cero.». Dónde: `src/modules/assistant/settings/settingsForm.ts` (integración).
- **Que el artículo de ayuda «Administrar WhatsApp» le llegue también a quien entra a Configuración de WhatsApp solo con `settings.read`.** El artículo pide `whatsapp.manage`. Dónde: `Infrastructure/Modules/Assistant/Help/Articles/index.json` (`whatsapp.admin`) (integración).
- **Sumar `dailyResetTimeZoneId` a los estados del asistente de la galería vieja de la 8b.** Si se vuelve a capturar, el cupo agotado se formatea en la zona por defecto. Dónde: `scripts/capturas/etapa-8b.json` (front) (integración).
- **Poner al día los selectores de la suite «E2E completo» después de la base 2.** El E2E real de la puerta se corrigió en `2ba6f0c9`: el ⋮ de una fila se busca en su pestaña, Legales se lee en su tabla y los roles de una invitación se eligen en el Picker (`option`, no `menuitemcheckbox`). Varios `e2e-completo-*.mjs` todavía usan los selectores viejos. No es parte de la puerta. Dónde: `scripts/e2e-completo-negocio.mjs` y los demás `e2e-completo-*.mjs` del front (puerta).
- **Con la clave propia, que la falla del proveedor en el hilo del asistente le sume a quien puede configurar un enlace «Revisar configuración»,** como el de «Sin configurar»: por ejemplo, el Dueño ve «El proveedor rechazó la clave. Reemplazala.» y el enlace lo lleva a la configuración del asistente. Es de producto: se le pregunta al usuario y se dibuja antes, porque los tableros `Asistente-Org`, `-Personal` y `-Plat` tienen una sola falla en el hilo. Dónde: `src/modules/assistant/components/AssistantThread.tsx` y `errors.ts` (puerta, 0064 §7).
- **La lista del catálogo de modelos de Plataforma redondea una tarifa de 0,125 a «US$ 0,13», y el editor dice «US$ 0,125».** La lista usa la columna de moneda común (`type: "money"`), que formatea con los decimales de la moneda. Dónde: `src/modules/assistant/settings/ModelCatalog.tsx` (front) (puerta).
- **En `M-WA-Config-Base2`, la (i) del plazo de los chats se abre hacia arriba y tapa el título; el tablero la abre hacia abajo.** Dónde: `src/modules/whatsapp/settings/pages/WhatsAppSettingsPage.tsx` (front) (puerta).

## Para después de la etapa costuras-de-modulos

- **El E2E real y el recorrido de siempre, sin casos nuevos, corren al cierre del bloque E12–E25, en la prueba integral** (decisión del usuario del 2026-11-27, «puerta mínima por etapa»). En el corte de la etapa se cortaban en el Asistente (attachments.five-six y E8B-20-2, por `ContextTooLarge`), que arregló 77155ea9; el E2E de la puerta de acciones ya lo pasó. Dónde: `scripts/test-e2e-real-assistant.mjs` y `scripts/recorrido-usuario.mjs` (front) (puerta).
- **`WhatsAppModulePolicy` (trabajos sin sesión: la cola de entrada y el aviso de espera) no mira el apagado de emergencia de configuración (`FeatureManagement:whatsapp=false`), que `FeatureService` sí mira. Con el apagado puesto, esos trabajos siguen corriendo si hay una fila vigente.** La regla dice que el apagado de emergencia manda sobre todo, y es uno de los escenarios que tiene que probar el revisor. Ya pasaba antes de B2 y la spec no lo pide. Dónde: `src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/Services/WhatsAppModulePolicy.cs`; `docs/rules/modulos-habilitados.md` (Configuración / apagado de emergencia) (B2).
- **Decidir si, con WhatsApp apagado para una organización, el webhook tiene que seguir creando en ella el contacto de un remitente nuevo, o si alcanza con guardar la entrada pendiente.** Hoy, con el módulo apagado, la organización sigue juntando datos personales de remitentes nuevos (teléfono y fecha de la última entrada). Lo que pasa con los datos de un módulo apagado (plazo y borrado) le toca a la etapa del cobro. Dónde: `src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/Services/WhatsAppWebhookWriter.cs` (crea el `WhatsAppContact` antes de que el procesador mire el módulo); `docs/plans/formularios/costuras-de-modulos.md`, «fuera de alcance» (B7).

## Para después de la etapa acciones-del-asistente

- **El E2E real, el recorrido y la galería de la etapa corren al cierre del bloque E12–E25, en la prueba integral** (decisión del usuario del 2026-11-27, «puerta mínima por etapa»). El E2E («Invitar usuario» con «Ya se guardó») pasó en el corte de la puerta; el recorrido se cortaba en ACC-08 a 1440, porque el paso cerraba el navegador antes de la captura, y el script ya está arreglado (2084389b), sin correr: faltan ACC-08, ACC-09 y todo a 390. Dónde: `scripts/test-e2e-real-acciones.mjs` y `scripts/recorrido-usuario-acciones.mjs` (front) (puerta).
- **A los tableros de las acciones del asistente les falta la tarjeta mientras lee y la de error con «Reintentar»:** la galería las capturó sin tablero con el que compararlas. Dónde: el lienzo, página de las acciones (v179) (puerta).
- **`read_action` no lleva página ni el valor del campo del que depende (`DependsOn`): las opciones de un Choice vienen solo en la primera página de 20, sin poder pedir, por ejemplo, los roles de la empresa elegida.** La ADR 0068 §7 dice «20 por página y con búsqueda», y `companyRoles` depende de `company`. Dónde: `docs/plans/formularios/acciones-del-asistente.md` §Contrato; `AssistantActionTools.ReadAsync` (B4).
- **Los campos de una fila de un Items llegan al modelo con su nombre como rótulo: no hay clave `Action.<id>@<versión>.Field.<item>.<campo>`.** Ninguna acción de hoy usa Items. Dónde: `AssistantActionTools.Describe`; `Application/Resources/ActionTexts.cs` (B4).
- **Contarle al modelo cómo se escriben las operaciones de un Choices al editar (`{op, value}`) en la descripción de `propose_action` o `read_action`.** Hoy está solo en `docs/features/asistente.md`. Dónde: `AssistantActionTools.cs` y `Modules/Assistant/Resources/Assistant.resx` (B6).
- **`IAssistantActionSource.ListChoicesAsync` no recibe la cultura: los nombres que `read_action` da al modelo salen en la cultura del hilo del worker.** Igual resuelve, porque la fuente acepta el nombre neutro y el código. Dónde: `AssistantActionTools.cs` (ReadAsync) e `IAssistantActionSource.cs` (B7).
- **Que la cobertura falle si una acción con confirmation `dialog` tiene un adaptador sin Dialog, o al revés.** Dónde: `src/shared/assistant-actions/coverage.test.ts` del front (F5).
- **Que el motivo de «Revisá» diga los nombres parecidos, como el tablero («Hay dos roles parecidos: Ventas y Ventas mayoristas.»).** Pide que la duda lleve parámetros: cambio de contrato en las fuentes y la tarjeta. Dónde: tableros Asistente-Org-Acciones y Usuarios-Invitar-Propuesta; `OrganizationUserActionSource`, `RoleActionSource`, `AssistantActionCard` (integración).
- **En M-Confirmar-Propuesta el tablero pone «Dar de baja» a la izquierda; la confirmación de siempre lo pone a la derecha. La lista de Roles del teléfono es la de tarjetas (263), no la del tablero.** Son piezas compartidas anteriores; cambiarlas toca todas las confirmaciones y listas (integración).
