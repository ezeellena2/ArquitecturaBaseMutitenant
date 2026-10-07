# Etapa 7 · Personal, sitio público e interacción

Inicio: 2026-10-07. Plan: [público y personal](../plans/2026-10-04-etapa-7-publico-y-personal.md). Estado: implementación commiteada y puerta automática aprobada; cierre pendiente del recorrido manual.

## Alcance y base

Back en `main`, base `2d5f87f`; front en `main`, base `fec2a86`. Ambos arneses declaran cerradas las etapas 0 a 6. La consolidación y las piezas comunes de edición, permisos y navegación ya existen. El lienzo vigente es v113 y sus tableros de esta etapa están aprobados.

La copia se comparte con los arreglos A8–A11 y R01/R03. Sus cambios en ingreso, sesiones, invitaciones, Empresas, datos fiscales y runner quedan fuera de los commits de esta etapa. Se integra el ingreso después de los commits A8/A9. No se detiene el entorno de la otra conversación.

## Forma de trabajo aplicada

Tareas en orden, commits por rutas propias, sin push. Tests de negocio, permisos, aislamiento y nuevos recorridos reales junto al código; una sola puerta completa al final, sin aprobación ni capturas por avance. La galería se arma al cerrar la implementación. No se inicia la Etapa 8.

## Decisiones tomadas

9. La sección inicial «Forma de trabajo de esta etapa» y el encargo del 2026-10-07 reemplazan los pasos antiguos de rojo previo, evidencia por tarea, visto bueno y capturas en cada avance. La base compartida puede tener cambios ajenos: se preservan y no se exige limpiar ni apagar el trabajo de otro chat. Las verificaciones completas y los detectores se ejecutan en la puerta final.
10. Las ADR 0043–0047 son el contrato vigente del ingreso y la cuenta: errores explícitos de cuenta, cookies públicas y de Plataforma separadas, acciones de Mi cuenta sin reautenticación adicional, nombre derivado en el back y aceptación de invitaciones por código. La etapa reutiliza esos comportamientos y no restaura las reglas anteriores del plan.
11. El contexto público conserva `BusinessTenantId` y `Slug` anulables en un objeto por pedido. Solo las rutas `[PublicSite]` consultan el reader y solo aceptan una etiqueta válida sobre el host y puerto del issuer. La consulta exige publicación, ausencia de bloqueo, organización empresarial activa y slug no reservado. El filtro HTTP usa ese contexto y el error existente; nunca modifica el tenant de la sesión ni agrega una caché antes de la Etapa 9.
12. Las versiones de URLs y ETags usan `VersionWireFormatter`, con una excepción nominal en la guarda de formato, igual que los serializadores HTTP existentes. El tipo solo acepta `uint` y produce dígitos invariantes sin separadores; sus tests cubren culturas distintas y el límite del tipo. `DisplayFormatter` conserva la presentación de datos para personas. Esto corrige los dos llamados locales de T02 que detectó la guarda durante las verificaciones de A9.
13. El directorio proyecta `DirectoryEntryRow` en `Models/PublicSite`, con los cuatro campos del contrato. Listado y logo usan las mismas condiciones explícitas de publicación y organización activa, además de EF y RLS: un dueño tampoco obtiene su borrador por la ruta pública. El logo lleva una URL con versión y revalidación; el listado no carga sus bytes. Las pruebas usan nombres únicos por caso para contar filas sin borrar datos de otras pruebas de la fábrica compartida.

14. Las pruebas de interacción componen `ApiFactory` en una fábrica propia y agregan el modelo sintético al `ApplicationDbContext` real. Conservan la unidad de trabajo, los interceptores y RLS; no incorporan un módulo de negocio a la API productiva. Las tablas sintéticas se amplían en `IsolationSchema`, sin migraciones productivas. La copia del nombre es anulable por la ADR 0046 y queda fuera de la auditoría.
15. Las rutas de transición del dato compartido aceptan Consumer y Business para que `PartyPolicy` compruebe qué parte puede actuar y devuelva `Tenancy.Party.NotAllowed`. La lectura excluye antes a terceros con 404. Los listados sintéticos también respetan el contrato paginado. La bandeja de prueba no agrega permisos al catálogo productivo; la receta exige el permiso correspondiente en una bandeja Business real.
16. El pedido público comprueba el módulo en la organización del subdominio, como dispone la decisión 2 del plan. El contexto privado de la persona conserva su identificador. El fingerprint de idempotencia incorpora el identificador público resuelto: repetir dentro del mismo sitio conserva el replay, mientras que reutilizar la clave en otra organización devuelve el error existente de clave reutilizada. Las pruebas también comprueban que retirar la publicación o apagar el módulo impida recuperar una respuesta anterior.
17. La receta personal usa `Widget` y una fábrica sintética propia para probar CRUD, aislamiento entre personas, separación Consumer/Business, idempotencia, concurrencia y paginado con la persistencia real. Su ruta Business de prueba no lleva un permiso artificial: permite comprobar el error de acceso; los permisos Business reales conservan sus guardas existentes.
18. En el front, la página pública va en la feature `areas/storefront/site/`, con sus carpetas `api`, `pages` y `components` cuando hagan falta. Las rutas directas `storefront/pages` y `storefront/api` del plan serían dos features distintas para la regla de imports existente. Se conserva esa regla y se ajustan los nombres del árbol; los hosts, las rutas HTTP y las pantallas aprobadas no cambian. Los módulos personales mantienen el patrón `areas/personal/<módulo>` de la ficha del front.
19. La ampliación de OpenIddict usa `SubdomainApplicationManager<TApplication>` como genérico abierto: primero conserva las URI fijas y amplía solo `web`. También amplía la búsqueda de cliente por la URI de salida, necesaria cuando falta `client_id`. Consumer con `prompt=none` usa la lectura de cuenta activa y responde `login_required` si no hay espacio personal; Business conserva la selección que recuerda la organización. La CSP usa los componentes de `Uri` para conservar esquema y puerto sin formatear números a mano.
20. La retención de la copia usa una política de Application y un ledger técnico global con la clave estable `engagement.<tabla>`. La resolución de tipos y las consultas genéricas quedan en Infrastructure. Se conserva la fila de retención finalizada para evitar reprogramarla; cambiar o quitar la política no altera un plazo ya fijado. El reemplazo omite solo el filtro `SoftDelete`, conserva `Parties` y RLS y marca el ledger en la misma transacción, con lock y `SKIP LOCKED`. La copia borrada lleva un marcador distinto de un nombre originalmente `null`.
21. Durante las ventanas de la otra conversación, la preparación queda fuera de los proyectos compilados. Las copias del front se guardan en `.local` del back: Vitest incluye archivos ocultos y podría ejecutar copias `.test.ts(x)` alojadas en `.local` del front. El portado compara las preimágenes y aplica solo los hunks propios cuando un archivo compartido cambió.
22. La salida conserva el tratamiento de `id_token_hint` de OpenIddict 7.7.1: es una pista opcional, se valida y se ignora cuando no es válida. La ampliación de subdominios conserva el comportamiento de una URI fija y sigue rechazando destinos no publicados. El test compara ambos retornos y prueba el destino desconocido; no cambia la validación productiva para imponer una aserción distinta del framework ([fuente de OpenIddict](https://github.com/openiddict/openiddict-core/blob/7.7.1/src/OpenIddict.Server/OpenIddictServerHandlers.cs)).
23. AppHost pasa un único issuer `https://localtest.me:5174/` a la API, Vite y el preparador del E2E. `E2EPublicOrigin` lee esa configuración en vez de conservar el `localhost` fijo anterior, así los enlaces de invitación y los callbacks siguen el mismo origen. La configuración operativa registra el callback de Google correspondiente; no se modifican secretos ni se cargan datos de ejemplo en Development.
24. El modelo sintético agrega explícitamente las entidades de OpenIddict con `ModelBuilder.UseOpenIddict<Guid>()` antes de personalizar el contexto real. Reemplazar `IModelCustomizer` también reemplaza la integración del servidor OIDC; conservarla permite que la fixture use el ingreso, el seed, los stores y las sesiones reales de la API.

25. El apagado de emergencia explícito `FeatureManagement:<módulo>=false` prevalece sobre cualquier `EnabledFor` heredado. El adaptador común lee ese valor antes de consultar FeatureManagement, de modo que rutas, workers y `/api/me` aplican el mismo apagado. Las pruebas HTTP conservan a propósito el filtro heredado y una habilitación vigente para verificar el 404 y la ausencia del módulo en la sesión.

26. La migración real `20261007173301_ConsumerDataRetention` crea solo el ledger global técnico, con claves a las dos organizaciones, índice de pendientes y unicidad de la fila compartida. Agrega el grant de `mt_app` explícito, como las migraciones de identidad existentes: los permisos de la migración inicial no se heredan por tablas futuras. Se revisó el SQL y EF no detecta cambios de modelo pendientes.
27. La programación de retención considera también las filas `Added` del contexto. Repetir el participante dos veces antes del primer commit conserva una sola fecha, además de los reintentos posteriores con ledger persistido. El test verifica la copia antes del vencimiento y su reemplazo en el instante exacto.
28. El reloj de la fixture de retención parte del `TimeProvider` de la base compartida para conservar la vigencia de sus documentos legales. Su precisión se ajusta al microsegundo de PostgreSQL; los tests siguen avanzando hasta un tick antes del vencimiento y hasta el vencimiento exacto.

29. Los cuatro punteros de las carpetas Personal reservadas por T08 se conservan mediante la lista nominal existente de `realinear-docs/ignorados.json`. Se documenta el motivo en el mapa del arnés: la plantilla trae la receta y no incorpora código de negocio para llenar esas carpetas. Los demás punteros siguen bajo el detector y `HarnessTests`; se agregan al mapa Tenancy HTTP y los tests de formato y partes que ya existen.

30. `authConfig` fija `authorization_endpoint` y `end_session_endpoint` en el issuer principal. La metadata consultada desde un subdominio puede devolver esos endpoints con el host de la petición; fijar solo `authority` no los corrige. El canje, userinfo, revocación y callbacks conservan el origen propio, y la salida vuelve a la página que la inició. Las pruebas focales y el E2E verifican esa separación.
31. El registro iniciado desde una publicación conserva la autorización OIDC validada original, incluido PKCE, en vez de iniciar otra autorización al terminar. Código y Google reutilizan ese retorno. Para cambiar a Business desde el sitio público se selecciona primero la organización y después se vuelve al dominio principal; el flujo no acepta una URL arbitraria de otro host.
32. La navegación Personal usa el catálogo real filtrado por módulos habilitados: hasta cuatro accesos y «Más», con todas las secciones visibles allí. No agrega módulos productivos ficticios para llenar el inicio. El layout memoiza las secciones a partir de la navegación visible, el acceso y su inicio.
33. Las capturas usan una entrada aislada de `src/test`, que no forma parte del build productivo. Sus datos y sesión simulada pertenecen solo al arnés visual; el E2E y el recorrido manual usan la API real. El puerto visual es 5192, porque se conservó el servidor ajeno que estaba en 5191.
34. `Button asChild` compone el enlace de «No disponible»: además de conservar su semántica de enlace, aplica la combinación de clases común y elimina el conflicto entre el borde transparente base y el borde secundario. La comparación visual detectó ese conflicto y se repitieron las dos capturas afectadas.
35. La guarda de botones crea el TypeChecker solo cuando la expresión `disabled` necesita resolución de símbolos. Conserva los mismos casos y reglas; reduce el costo del escaneo que había agotado el tiempo durante la suite completa. La prueba de búsqueda literal de Plataforma usa su prefijo único, porque las fixtures nuevas del directorio comparten la base de integración y también incluyen `%`.
36. El preparador E2E publica dos organizaciones empresariales propias del recorrido: una activa y otra suspendida. Usa la unidad de trabajo y la provisión reales, exclusivamente en la base aislada del E2E; no carga ejemplos en Development ni modifica los perfiles de los recorridos anteriores. El test espera el regreso efectivo al subdominio después del código y busca con el rol accesible `searchbox` del directorio.
37. La auditoría ciega de AGENTS respondió las diez preguntas y corrigió el puntero Personal del back a `src/areas/personal/<módulo>/`. Un segundo agente, sin contexto de implementación, siguió la receta: siete pruebas HTTP con Docker y 27 pruebas focales de front pasaron. Se descartaron sus seis archivos temporales después de verificar sus hashes; el módulo no quedó incorporado al producto.

38. El helper `openPlatformOrganization` busca por nombre desde la pantalla antes de abrir la ficha, igual que el helper de Cuentas. Las organizaciones creadas por los otros recorridos y las publicaciones nuevas pueden llevar el destino fuera de la primera página. Se conserva el paginado productivo y se elimina ese supuesto del E2E; no se borra la preparación ni se aumenta el tamaño de página para ocultarlo.

## Cierre

La implementación T01–T19 está commiteada y la puerta automática T20 pasó. El E2E completo sin filtros terminó 36/36. `ClosedStages` conserva 0–6 hasta completar el recorrido manual pendiente. No se inició la Etapa 8.

### Comandos y evidencias

Las evidencias completas de esta sesión están en `.local/etapa7/gate/` del back; las capturas y la comparación están versionadas en el front. Las primeras ejecuciones fallidas se conservan y no se cuentan como aceptación.

| Comando / verificación | Resultado actual | Evidencia |
|---|---|---|
| `dotnet build ArquitecturaBaseMultitenant.slnx` | EXIT 0, 0 advertencias, 0 errores; 8,19 s | `build-back-final.log` |
| `dotnet test --no-build --no-restore` con Docker | EXIT 0, 1989/1989, 0 fallos y 0 omitidos, 17 min 55,268 s. Primera ejecución: 1987/1989; se corrigieron la guarda del callback y el aislamiento de la búsqueda de Plataforma | `test-back-final.log`; anteriores en `test-back.log` |
| Plataforma y directorio después de corregir la fixture | EXIT 0, 18/18, 0 omitidos | `platform-directory-verified.log` |
| `npm run build` | EXIT 0 | `build-front-final.log` |
| `npm run lint` | EXIT 0, 0 errores, 751 advertencias heredadas: lienzo archivado v35 y seis avisos en componentes/formularios existentes. Repetido después de corregir el helper del E2E | `lint-front-verified.log`; anterior en `lint-front-final.log` |
| `npm test` | EXIT 0, 246 archivos y 2195 pruebas, 156,99 s | `test-front-final.log` |
| `npm run contracts:check` | EXIT 0; schema TypeScript coincide con OpenAPI | `contracts-front-final.log` |
| `npm run test:e2e:real` sin filtros | EXIT 0, 36/36, 0 fallos, cancelados y omitidos; 29 min 57,257 s. E7 a 1440 y 390, más recorridos anteriores, con front/API/PostgreSQL/pickup reales. Primera corrida: 33/36, EXIT 1, 28 min 7,732 s: fallaron 5b, Empresas dependiente de su preparación y el padre. Helper corregido; focal 5b/6 EXIT 0, 3/3, 246,886 s, antes de repetir el completo | `e2e-full-verified.log`; anteriores en `e2e-full-final.log` y `e2e-platform-search-focal.log` |
| E2E focal E7 | Escritorio completo verde; móvil final EXIT 0, 3/3 incluyendo padre y regresión móvil B2B, sin cancelados/omitidos | `e2e-stage7-search.log`, `e2e-stage7-mobile-final.log` |
| `realinear-docs --detectar --repo back` | EXIT 0, auto 0 / modelo 0 / humano 0, 0 tokens; repetido con el código final | `docs-back-final.log`, `docs-back-final-report.md` (20261007-1725); anterior 1618 |
| `realinear-docs --detectar --repo front` | EXIT 0, auto 0 / modelo 0 / humano 0, 0 tokens; repetido con el código final | `docs-front-final.log`, `docs-front-final-report.md` (20261007-1725); anterior 1620 |
| Guardas finales de documentación front | EXIT 0, 9/9 | `harness-final.log` |
| Receta B2C ciega | Back: build sin advertencias y 7/7 HTTP con Docker. Front: 27/27 focales. Se descartaron todos los archivos temporales | `recipe-back.log`, `recipe-front.log` |
| Galería T18 y `--verify` | EXIT 0, 21 comparaciones y 63 PNG; 21/21 revisadas visualmente | galería y `comparacion.md` del front |
| Revisión independiente final T01–T19 | Aprobación estática sin hallazgos accionables en aislamiento, autorización, retorno OIDC, baja/retención y regresiones. Solo lectura; no sustituye las ejecuciones de la puerta ni la revisión visual | `.local/etapa7/final-code-review.md` |
| Limpieza del entorno propio | `aspire ps` EXIT 0, `[]`: el E2E apagó su AppHost. Vite de capturas 5192 apagado después de comprobar PID, ruta y puerto propios | `aspire-final.log`, `runtime-cleanup.log` |

### Commits propios

Los commits de A8–A10/R03 y las actualizaciones ajenas del lienzo y las guías se preservaron y no se incluyen en esta lista.

| Tarea | Back | Front |
|---|---|---|
| T00 · Alcance | `4b03395` | — |
| T01 · Contexto público | `61a5cfc` | — |
| T02 · Página y logo | `eb35f86`, `6ce3460` | — |
| T03 · Directorio | `7143789` | — |
| T04 · OIDC y CSP de subdominios | `8f972dd` | — |
| T05 · Origen local y hosting | `df24b0b` | — |
| T06 · Interacción y módulos | `b28f925` | — |
| T07 · Baja y retención | `5c80738` | — |
| T08 · Receta Personal | `442f7e2` | — |
| T09 · Documentación del back | `b609e94` | — |
| T10 · Rutas por host | — | `7670fb49` |
| T11 · Marco web y menú | — | `139dc4f6` |
| T12 · Landing | — | `6a683f65` |
| T13 · Directorio | — | `da1f6206` |
| T14 · Página pública | — | `96342df7` |
| T15 · Ingreso y sesión | — | `a820b0e9`, `ee70e29b` |
| T16 · Navegación Personal | — | `b17fcfb7` |
| T17 · Documentación del front | — | `1f8fe115` |
| T18 · Galería | — | `407d5b2d`, corrección visual `c8cb7967` |
| T19 · E2E | `fd6114d` | `5b7ea863`, `369af3c9` |
| T20 · Guardas y documentación ciega | `c1a5123`, `fe235d8`, `e02f33e` | `8e2ba709` |

### Galería y recorrido manual

[Galería final](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/etapa-7/galeria.html) y [comparación con el lienzo](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/etapa-7/comparacion.md), con las diferencias concretas y su motivo. Se mostraron al usuario antes del cierre. No hay cortes ni estados ausentes. Persisten diferencias de composición heredada en la Landing, tamaño del vacío móvil del directorio y foco del menú común; hosts locales e iniciales Sigma conservan el comportamiento canónico.

El recorrido manual de esta sesión todavía no se realizó. Se eligió Chrome, perfil persistente Eze, sin limpiar cookies ni almacenamiento ni tocar sus pestañas personales. El navegador bloqueó el sitio con `ERR_CERT_COMMON_NAME_INVALID` y conserva la pestaña «Privacy error». Está pendiente la intervención humana solicitada para habilitar el dominio principal y el subdominio de prueba. El [SKILL.md de computer-use](C:/Users/ezequ/.codex/plugins/cache/openai-bundled/computer-use/26.930.61225/skills/computer-use/SKILL.md) remite a su [política de confirmaciones](C:/Users/ezequ/.codex/plugins/cache/openai-bundled/computer-use/26.930.61225/docs/confirmations.md), que exige `Hand-Off Required` para «“site not secure” HTTPS interstitial bypass». Por esa instrucción explícita, Codex no atraviesa el aviso. Los E2E aislados no reemplazan el recorrido manual solicitado ni se presentan como tal.

Al intentar volver a vincular esa pestaña, la revisión automática rechazó el acceso a `https://localtest.me:5174/`: consideró que acceder mientras persiste el intersticial podría eludir la protección y que el usuario todavía no realizó el desbloqueo manual. No fue un rechazo manual del usuario. Se reiteró el pedido de intervención humana con ese motivo; no se intenta atravesar el bloqueo por otra superficie.

Pendiente: completar el recorrido de la «Puerta» de arreglos y el de la 7 en ese perfil, después de la intervención humana sobre el certificado, y registrar sus evidencias. Para continuarlo se vuelve a levantar el entorno propio; la corrida automática y el capturador ya están apagados. Solo entonces se evalúa agregar 7 a `ClosedStages`.
