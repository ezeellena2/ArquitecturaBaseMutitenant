# Backlog de la revisión de Etapas 0 a 2

## Observaciones de 3b para etapas posteriores

- Publicación administrativa de versiones legales: usar la administración prevista para plataforma cuando nazca su pantalla. La 3b permite leer/aceptar; el E2E publica solo en su propia base y el recorrido manual tiene SQL local explícito.
- Propagar `maxLength` desde las validaciones de texto al OpenAPI para que el formulario derive límites del contrato. El límite actual de nombre se valida en ambos lados y el backend sigue siendo autoridad.
- Probar las plantillas en la matriz de clientes de correo (Gmail, Outlook y otros). La 3b ya incluye el fallback sRGB equivalente al token de marca para clientes sin `oklch`; se verificó el HTML real en Chromium.
- Conciliar detalles de iconografía, interlineado y alineación de menús/toasts de Radix/Sonner con los prototipos, conservando foco y teclado. La evidencia de 3b está en el informe visual del front; no afecta sus flujos y se presenta para aprobación visual en la puerta 9.

Detalle y alcance de cada punto: [revisión del 2026-09-29](reviews/2026-09-29-revision-etapas-0-2.md). Los puntos 1, 5, 10, 22 y 23 se corrigen antes de la Etapa 3b.

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
- **12.** SortIndexTests solo revisa el Widget de prueba, no los SortMap reales
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
- **47.** La única forma permitida de llenar HybridCache pierde el tenant
- **48.** Una carrera con ReleaseAsync convierte un reintento legítimo en 500
- **49.** Búsqueda y cursor van como constantes SQL: un plan y una compilación de EF por cada término o cursor
- **50.** El seed invalida un HybridCache propio, no el de la aplicación; el test de invalidación es un falso verde
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
