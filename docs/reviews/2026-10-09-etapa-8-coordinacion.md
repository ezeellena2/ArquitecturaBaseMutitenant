# Etapa 8 · Continuación coordinada

Continuación autorizada por el usuario desde este chat, después de la [revisión del pase](2026-10-09-etapa-8-revision-del-pase.md). Trabajo en `main`, sin push y sin incluir cambios ajenos. **E8 sigue abierta**: este informe no certifica la puerta T14. Back y Front mantienen sus implementaciones en sus respectivos chats.

## Encajes resueltos

| Encaje | Entrega |
|---|---|
| Bot → Registro selecciona WhatsApp | Back `5358f696`, Front `8b428edf`: `/registro?channel=whatsapp`, sin identidad ni secretos. Respeta capacidad tardía y elección manual. |
| Baja pendiente desde Enlace | Front `9ac6b8bb`: cancelar con ticket en memoria y comenzar autorización OIDC Consumer nueva. No vuelve a canjear el enlace ni cancela por segunda vez después del éxito. |
| Metadata temporal del contrato | Back `5358f696` retira descripciones/501/`General.Contract.NotImplemented` de operaciones ya implementadas. Puertos neutrales, rutas, cuerpos y códigos funcionales conservados. Front regenera tipos en `5dfde077`. |
| Referencias de tests de teléfono | Front `8daca855`: cinco referencias recuperan su etapa correspondiente, conservando la guarda documental. |

Hashes SHA-256 normalizados a LF, comprobados por el coordinador:

- OpenAPI: `cf80064358a63daa3be361dc4a5308d0ee52e27a31824953061eb1d44b77727d`.
- Schema Front: `70ff361a5c4079f19ffed7e702592eca680f76b9e88ddb3e7647815caf3fac93`.

El freeze inicial T01 permanece como evidencia histórica. Los acuerdos posteriores constan en «Cambios al contrato» de su [informe](2026-10-08-etapa-8-contrato.md).

## Entorno reutilizado de ArquitecturaBase

Se cotejaron localmente seis secretos necesarios y los identificadores/configuración no secreta de WhatsApp: ya coincidían, por lo que no se sobrescribieron. No se copiaron claves del asistente, caché ni dashboard. Los valores secretos no se incluyen en archivos, informes, mensajes a otros chats o memoria.

`e9a75323` conecta el DevTunnel que faltaba en AppHost, copiando la composición de ArquitecturaBase: región BrazilSouth, ID opcional y acceso anónimo únicamente al endpoint HTTPS de la API. El entorno E2E aislado nunca se publica. El paquete ya estaba instalado; no se añadió ni actualizó ningún paquete. Build de AppHost: **0 advertencias / 0 errores**.

Se habilitó `DevTunnel:Enabled` únicamente en user-secrets del AppHost multitenant. La CLI está autenticada. La lectura de Meta confirmó que el número aportado por el usuario es el **emisor del bot**, no el destinatario de la prueba. La app de Meta sigue suscrita y su callback conserva el túnel de ArquitecturaBase; todavía no se cambió ni se inició Aspire para este recorrido.

## Plantillas Meta

La consulta inicial confirmó `codigo_ingreso` aprobado, Authentication, es/en; los quince nombres Utility E8 no existían. Las plantillas ajenas, incluida `invitacion_acceso` Marketing de ArquitecturaBase, se conservaron.

El usuario autorizó crear **26 variantes sin URL**, trece nombres es/en del [paquete T03](../operations/whatsapp-plantillas.md). Primera ejecución: 22 creadas y cuatro sin crear por `100/2388299`, Leading or Trailing Params Not Allowed. El usuario aprobó cerrar baja suspendida y aviso de recuperación con «para pedir ayuda.» / «for help.», actualizar la guía y los dos ejemplos WhatsApp de Mensajes y reintentar esas cuatro. Segundo envío: **4/4 creadas**.

Consulta de verificación del 2026-10-09, antes de las 14:52 UTC:

| Resultado | Variantes |
|---|---:|
| APPROVED y UTILITY | 13 |
| PENDING | 11 |
| REJECTED / INCORRECT_CATEGORY, `aviso_metodo_ingreso/en` | 1 |
| APPROVED pero MARKETING, `pagina_publica_moderada/en` | 1 |
| Cuerpos que coinciden con la guía | 26/26 |

Las solicitudes usaron Utility y `allow_category_change=false`. Aun así, Meta reclasificó una variante: no alcanza con comprobar APPROVED. Las dos con problemas de categoría no se consideran habilitadas para E8. No se cambia el catálogo a Marketing para acomodar el rechazo.

Se solicitaron ambas revisiones desde WhatsApp Manager y se verificó el resultado visible: `pagina_publica_moderada/en` en revisión de categoría y `aviso_metodo_ingreso/en` en apelación. La consulta Graph de **15:16 UTC**, repetida a las **17:16 UTC** sin cambios, confirma **24 APPROVED / UTILITY**, `aviso_metodo_ingreso/en` IN_APPEAL / UTILITY y `pagina_publica_moderada/en` APPROVED / MARKETING. La solicitud de revisión no acredita aprobación. Evidencia local sin secretos: `.local/reviews/2026-10-09-meta-utility-revision-estados.json`, `2026-10-09-meta-categoria-en-revision.jpg` y `2026-10-09-meta-rechazo-en-revision.jpg`.

Las **cuatro variantes URL** de `invitacion_organizacion` y `recuperacion_aprobada` siguen sin crear. Necesitan el origen HTTPS público real del front, coherente con `ActionUrl` e issuer. El túnel de webhooks de la API, por sí solo, no hace accesible una URL de Vite local desde el celular. No aprobar botones con un origen de ejemplo o con `localtest.me`.

Paquete, respuestas y cotejo se conservaron sin secretos en `.local/reviews/2026-10-09-meta-utility-*.json`. Es evidencia local de la consulta, no una garantía sobre estados futuros. Todavía no hubo envíos a un celular real ni evidencia `delivered`.

## Soporte de integración

Back entregó `f2ec23a7`: [fixture privada de navegador](../guides/e2e-whatsapp.md) con API/Kestrel, OIDC y PostgreSQL reales. Build focal sin advertencias/errores; **15/15** pruebas de guardas/persistencia. Su smoke CLI verificó alta Phone/OTP, entrada HMAC persistida y deduplicada, procesamiento/enlace privado y OTP del operador.

El fixture bloquea transportes externos. Su control por stdin/stdout privado vincula cada orden a escenario, destino y cursor; permite obtener pruebas cifradas, consultar eventos o vencer el ticket específico de ese escenario. No agrega rutas de diagnóstico productivas. El Front implementa el runner permanente y las decisiones de recuperación desde la UI real de Plataforma.

El build completo del Back finalizó sin advertencias/errores. La primera suite completa Docker pasó **2262 tests, cero fallos y cero omitidos**, en 20m35s; el comando terminó con código 1 porque Microsoft.Testing.Platform intentó ejecutar también el tool E2E. Back corrigió esa configuración en `25983e9f`, declarando `IsTestingPlatformApplication=false`. Después centraliza dos códigos existentes en `WhatsAppErrors` y repite build, pruebas dirigidas y suite completa sobre las fuentes finales. La puerta requiere esa segunda ejecución sin error del comando. Front prepara su runner permanente y la galería; las ejecuciones se ordenan para no sobrescribir DLL en uso.

Back commiteó la centralización en `8e0bb63c1638b7e5a5e52d89c28de91562b24cf6`: los mismos códigos, descripciones y respuestas pasan al catálogo de Domain, sin ignorar hallazgos ni borrar documentación. Build final: **0 advertencias / 0 errores**, exit 0, 41,92 s. Pruebas dirigidas: **143/143 WhatsApp y 8/8 ErrorCodeTests**. OpenAPI conserva el SHA indicado arriba. La segunda suite completa terminó con **2262/2262, cero fallos y omitidos, exit 0**, 16m46s; el tool ya no se descubre como aplicación de tests. El [pase Back](2026-10-09-etapa-8-pase-back.md), commiteado con el plan en `605be253381f0faf563765fd8ce4d5b65230e916`, y el log `.local/reviews/2026-10-09-pase-back-test-final.log` conservan la evidencia. El coordinador cotejó el resumen del log y el alcance del commit de código.

Front entregó su preparación estática en `cab63db64ee92fa90cc850282631ed5eac49847e`, sin push. Runner final `579fc2c34b920cef7e2ac8cd5ebb6d26b330893e`: **30 grupos, 15 por tamaño**, todavía sin ejecutar contra la API real. Incluye aprobación/rechazo de recuperación por la UI de Plataforma, seleccionando el pedido por ambos métodos y conservando 390 px en móvil. Build, lint y contracts:check terminaron con código 0; suite completa **2350/2350**, 255 archivos; focales Node **12/12**. La galería quedó en **108/108 casos, 324 PNG**, con el ícono del retorno a WhatsApp y sus cuatro comparaciones actualizados. Su [informe](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/etapa-8/informe.md) distingue estas pruebas del E2E real pendiente.

Con los binarios liberados, se dio el GO a Front para ejecutar el recorrido general y los 30 grupos WhatsApp con la fixture real aislada. Se encargó a Back el retiro efectivo en una copia descartable con bases migrada y vacía; sus builds no podían modificar las DLL de main usadas por Front. El coordinador inspeccionó también los cuatro pares recapturados de enlace inválido y ticket de baja vencido: ícono presente en escritorio/móvil, textos legibles y contenido principal sin cortes. Esta revisión visual no sustituye el recorrido integrado ni la entrega por Meta.

### Resultado del E2E integrado

Front completó el recorrido general: **36/36 pruebas, exit 0**, sin fallos, canceladas ni omitidas, 1800419,6797 ms. Son 35 subcasos y su prueba padre, ejecutados completos sin `MT_E2E_ONLY_CASE`. Log: `.local/reviews/e8-front-20261009/e2e-general.log` del Front. El entorno aislado usó claves temporales propias; al terminar detuvo Aspire y confirmó cero sesiones antes de iniciar WhatsApp.

El runner WhatsApp final `4955e9a04dfe3663c0577dadbe5fc1ea700915c7` pasó **30/30 grupos reales, exit 0**, quince en 1440 y quince en 390, 371194 ms. Usó el código Back `8e0bb63c`, con HEAD documental `734fe909`. Verificó OTP, cuenta, ambos accesos, webhook HMAC y deduplicación persistida, bot/enlace y OIDC con state/PKCE, replay, destinos ocupados, aprobación/rechazo de recuperación desde Plataforma persistidos después de F5 y cancelación de baja por enlace con ticket vigente/vencido. Log: `.local/reviews/e8-front-20261009/e2e-whatsapp-f5.log` del Front. Los hashes del contrato permanecen iguales.

Los intentos fallidos se conservan en el informe Front. Sus correcciones pertenecen al soporte de pruebas: espera de la sesión privada de Plataforma, aceptación legal por la UI del operador de prueba en un contexto separado, respeto del cooldown real y correlación de la respuesta de F5 con consultas iniciadas después de navegar. No se cambió producto, autorización, reloj ni respuestas esperadas. Las pruebas dirigidas del soporte pasaron **35/35 Node**, con revisiones independientes aprobadas. La suite funcional completa conserva **2350/2350** y la galería **108/108**.

El coordinador cotejó el informe y los resúmenes de ambos logs, además de la liberación explícita de navegador, Vite, Kestrel y PostgreSQL propios. No queda Aspire de esta ejecución. Front cerró su pase documental en `4634789695bd31ac5f755c80b8d65f18c84283b7`. Se dio entonces el GO al Back para el ajuste mínimo de autocontención de sus fixtures. La API y las cookies fueron reales; el transporte externo permaneció bloqueado: este resultado no acredita envío ni `delivered` por Meta.

### Autocontención de tests y retiro del módulo

La copia sin WhatsApp mostró que los tests que levantan Development podían depender del secreto Google de esta máquina y de un operador local. La suite Main anterior pasó, pero no acredita independencia de esos secretos. Después de la liberación del Front, Back corrigió las fixtures en `e265907d052c6a639e5bfc9051187a8489cd97a9`: Google apagado por defecto sólo en `ApiFactory` (los casos Google aportan sus valores explícitos) y helper de operador sintético en los casos Development de `SpaHostingTests`. No se relajan validadores productivos ni cambia el contrato.

La verificación final se ejecutó con `APPDATA` privado, sin copiar secretos ni retirar `UserSecretsId` productivos: **build completo exit 0, cero advertencias y errores**, 44.61s; **109/109 dirigidos** de Google, registro, arranque, SPA y OpenAPI; **suite completa Docker 2262/2262, cero fallos y omitidos, exit 0**, 16m38.270s. El coordinador cotejó el log y manifiesto, verificó los ocho hashes de evidencias y la ausencia de `Microsoft/UserSecrets` después de terminar, y recalculó ambos hashes LF del contrato: coinciden con los indicados arriba. El [pase Back](2026-10-09-etapa-8-pase-back.md) conserva los resultados previos y registra el final en `37ab6a8e9c79f45b65f06b373d26fd48318eddbe`. Las fuentes, binarios y recursos propios quedaron liberados.

El retiro efectivo se ejecutó únicamente en `.local/module-removal/effective-605be253/source`, con bases y contenedor propios. Su [informe](2026-10-09-etapa-8-retiro-whatsapp.md) conserva las adaptaciones de fixtures e inventarios al quitar el módulo, los intentos fallidos y la comparación de datos, esquema y contrato del núcleo. La cuarta suite completa pasó **2084/2084, cero fallos y omitidos, exit 0**, 14m54.120s. La reversión pasó **12/12 permisos DML individuales** y, después de volver a retirar el módulo, los esquemas de ambas bases coinciden: SHA-256 `04ab3d7e1bda60d9c7e17f30e261f1fc9a41ac1b9fd5d2c3e393486b72034022`. Permanecen iguales los 183 DTO comunes; sólo desaparece el DTO del webhook. El coordinador cotejó el log, las doce filas de permisos, ambos hashes finales y la ausencia del contenedor propio después de la limpieza. Las fuentes finales quedan archivadas sin secretos. Esta prueba técnica de retiro no certifica el recorrido operativo por Meta ni cierra T14 completo.

## Pendientes documentales del arnés

La centralización del Back eliminó sus dos hallazgos de códigos. Quedan cuatro entradas en el árbol del Back y cinco en el del Front; son archivos existentes que los motores no incorporaron. Los informes son `.local/arneses/realinear-docs/20261009-1216.md` del Back y `.local/arneses/realinear-docs/20261009-1209-front.md` / `20261009-1220-front.md` del Front. Los ajustes mecánicos sí quedaron verificados y commiteados. **Los detectores todavía no están en cero.**

La detección de ambos repos, `.local/arneses/realinear-docs/20261009-1510.md`, conservó **9 modelo, 0 humano** y marcó **2 automáticos** posteriores del Front. El arnés los preparó sin modelo ni fusión en `arnes/realinear-docs/20261009-1512`: **auto 2→0, modelo 5→5, humano 0**, verificación `npx vitest run src/test/harness.test.ts` aprobada y 0 tokens. Informe `.local/arneses/realinear-docs/20261009-1512-front.md`. La revisión automática rechazó inicialmente `--fusionar` por faltar una petición humana explícita de fusión, exigida por la skill. Después de revisar la rama concreta, el usuario respondió «Sí, fusionar esa rama». Se incorporó mediante fast-forward en Front **`1192f3d4`**, sólo dos líneas del árbol, preservando los cambios ajenos y sin rehacer manualmente el trabajo del arnés. La detección posterior de ambos repos, `.local/arneses/realinear-docs/20261009-1515.md`, confirma **auto 0, modelo 9, humano 0**, 0 tokens. La excepción manual de las nueve entradas de ambos árboles es una consulta distinta y sigue pendiente.

| Repositorio | Entrada existente pendiente |
|---|---|
| Back | `docs/features/contrato-http.md` |
| Back | `src/ArquitecturaBaseMultitenant.Application/Validation/Identity/AddLoginMethodRequestValidator.cs` |
| Back | `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/WhatsAppErrorProbeController.cs` |
| Back | `tests/ArquitecturaBaseMultitenant.ArchitectureTests/ModuleControlProbes.cs` |
| Front | `scripts/capturar-etapa-8.mjs` |
| Front | `scripts/generar-galeria-etapa-8.mjs` |
| Front | `scripts/test-e2e-real-whatsapp.mjs` |
| Front | `src/test/etapa8VisualApp.tsx` |
| Front | `src/test/etapa8VisualFixtures.ts` |

La skill `arnes` prohíbe abrir los archivos que tocó o rehacer su trabajo. La consulta para permitir la corrección manual del árbol sigue pendiente; no supone una decisión nueva de producto ni autoriza borrar o modificar estos archivos de código.

## Lo que falta para la puerta

- Completar documentación en cero. Las suites finales, el E2E permanente, la comparación visual afectada y el retiro efectivo en una copia con bases migrada y vacía tienen los resultados indicados arriba.
- Resolver aprobación/categoría de todas las variantes y el origen de los cuatro botones URL.
- Recibir el **celular destinatario real autorizado**, diferente del emisor y de una cuenta de operador para registro/enlace común.
- Levantar el entorno, verificar el túnel multitenant y cambiar temporalmente el callback compartido según [configuración](../operations/configuracion.md#4-lo-que-tenés-que-hacer-vos-una-sola-vez-fuera-del-código). Conservar la URL anterior para volver a ArquitecturaBase.
- Ejecutar el recorrido real con WhatsApp, OTP recibido y eventos `delivered`, sin registrar pruebas o números completos. El transporte controlado no sustituye esta evidencia.

## Decisiones tomadas

- Reutilizar los secretos/activos existentes sin reemplazar los que ya coinciden.
- Coordinar desde este chat los tres encajes aprobados, la regeneración del contrato y el orden de ejecuciones de ambos repos.
- Cambiar solamente el cierre textual de las cuatro variantes fallidas, con autorización expresa; se conserva la instrucción de soporte, variables y categoría. Email no cambia. Mensajes v115 recibe ese ajuste puntual autorizado, sin renumerar tableros ni modificar sus otros estados.
- Conservar el recorrido de recuperación actual, según confirmó el usuario: también atiende la pérdida de acceso a los métodos que reciben códigos. Esta continuación no incorpora contraseñas ni cambia ese flujo.
