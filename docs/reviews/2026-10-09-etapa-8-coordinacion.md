# Etapa 8 · Continuación coordinada

Continuación autorizada por el usuario desde este chat, después de la [revisión del pase](2026-10-09-etapa-8-revision-del-pase.md). Trabajo en `main`, sin push y sin incluir cambios ajenos. **E8 sigue abierta**: este informe no certifica la puerta T14. Back y Front mantienen sus implementaciones en sus respectivos chats.

**Estado actualizado al 2026-10-10:** el circuito real de identidad está completo y su entorno quedó apagado/restaurado. El runner ampliado pasó **34/34 grupos en 1440/390** y el menú vacío quedó corregido con «Sin dato». La prueba dirigida del aviso diferido de recuperación pasó **6/6** en una copia aislada del Back, conservando la guarda de versión; la causa del 409 histórico sigue sin atribución. Los arneses Back y Front terminaron verificados con **auto 0 / modelo 0 / humano 0**, se fusionaron con autorización y se reverificaron sin cambios sobre los cortes commiteados de ambos main. La detección de las fuentes de trabajo todavía en curso marca dos automáticos, cero de modelo y cero humanos: no se confunde ese estado con los snapshots verificados. Meta mantiene 32 variantes creadas, 28 utilizables por categoría y cuatro excepciones en revisión, detalladas en [la guía operativa](../operations/whatsapp-plantillas.md). El usuario eligió esperar la revisión, sin contactar soporte. No se atribuyen a E8 las implementaciones E8b todavía en curso.

**Actualización del E2E y menú:** runner ampliado **34/34, exit 0 y verified=true**; menú `845e6fe7` con 38/38 tests, build en verde y SPEC/QUALITY aprobadas, pase Front `de184460`. Las pruebas, snapshots y límites de restauración se detallan en «Correctivos finales de Front». Las ramas del arnés se incorporaron y el test del 409 quedó en `630cbbee`; las cuatro excepciones Meta siguen pendientes.

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

En ese primer corte las **cuatro variantes URL** de `invitacion_organizacion` y `recuperacion_aprobada` estaban sin crear, a la espera del origen HTTPS público real del front, coherente con `ActionUrl` e issuer. Después se crearon con el origen del túnel y autorización humana, como registra el recorrido posterior. El túnel de webhooks de la API, por sí solo, no hace accesible una URL de Vite local desde el celular. No aprobar botones con un origen de ejemplo o con `localtest.me`.

Paquete, respuestas y cotejo se conservaron sin secretos en `.local/reviews/2026-10-09-meta-utility-*.json`. Es evidencia local de la consulta, no una garantía sobre estados futuros. Este corte inicial precede los envíos reales y su evidencia `delivered`, registrados más abajo.

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

## Historial documental del arnés

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

La skill `arnes` prohíbe abrir los archivos que tocó o rehacer su trabajo. En ese corte se consultó una corrección manual del árbol; posteriormente se eligió resolverlo mediante otra corrida completa. No se aplicó esa excepción al árbol ni se borraron o modificaron estos archivos de código para ocultar los hallazgos.

La detección de **23:29 de Argentina**, `.local/arneses/realinear-docs/20261009-2329.md`, informó **auto 14, modelo 35, humano 0**, 0 tokens y «Para vos: Nada». Las fuentes avanzaron con E8b desde el corte 15:15. Se prepara otra corrida completa del arnés sobre snapshots commiteados estables de ambos chats; este resultado no acredita arreglos ni documentación en cero.

### Corridas completas del 10 de octubre

Se usó `--motor codex` con la CLI instalada de la app, `0.162.0-alpha.17.2`, mediante el override temporal documentado `ARNES_CODEX_BIN`; no se cambió la configuración global ni el modelo del manifiesto. Las corridas fueron en segundo plano y se leyeron sus informes al terminar.

| Corte | Estado y verificación | Auto / modelo / humano antes → después | Consumo |
|---|---|---|---|
| Front `arnes/realinear-docs/20261010-0006` | Commiteado; `npx vitest run src/test/harness.test.ts` aprobado | 5 / 5 / 0 → **0 / 0 / 0** | Entrada 95084, caché 68864, salida 894, razonamiento 62; costo n/d |
| Back `arnes/realinear-docs/20261010-0005` | Falló la verificación; conservar como evidencia, **no fusionar** | 11 / 30 / 0 → 0 / 0 / 1 | Entrada 129141, caché 90112, salida 2511, razonamiento 67; costo n/d |
| Back `arnes/realinear-docs/20261010-0024` | Commiteado; tests Node del arnés y `HarnessTests` aprobados | 11 / 29 / 0 → **0 / 0 / 0** | Entrada 128726, caché 89728, salida 2633, razonamiento 181; costo n/d |

El intento Back fallido dejó un único «Para vos» técnico: reemplazar `OrganizationAssistantSettings` por el DTO publicado `OrganizationAssistantSettingsResponse`. El usuario autorizó esa excepción puntual a la skill; se aplicó exclusivamente en `docs/features/asistente.md`, commit `c2dcbf23`. No se corrigió el árbol manualmente.

Los tests del primer intento también detectaron un puntero `CLAUDE.md` faltante en Infrastructure/Modules/Assistant y la falta del Front hermano en la ubicación del worktree. El owner Back agregó el puntero canónico en `cae9d52f`, con dos guardas verdes. Se preparó un junction de lectura hacia el Front real para la verificación entre repos; no se eliminaron ni relajaron expectativas. La nueva corrida Back tomó ese commit y terminó con «Para vos: Nada», al igual que Front.

Informes: `.local/arneses/realinear-docs/20261010-0006-front.md`, `20261010-0005-back.md` y `20261010-0024-back.md`. El usuario autorizó «Sí, fusionar ambas ramas». Se incorporaron por fast-forward: Back **`62879d7b`** y Front **`1cd1aa2a`**, preservando las fuentes en curso de ambos chats. No se hizo push.

La detección posterior sobre los directorios de trabajo, `.local/arneses/realinear-docs/20261010-0029.md`, terminó con **auto 2 / modelo 0 / humano 0**, «Para vos: Nada» y exit 2, que significa hallazgos pendientes. Para separar ese estado de los cortes incorporados se corrió el arnés `--sin-modelo`, que detecta dentro del worktree de lo commiteado: `20261010-0031-back.md` y `20261010-0031-front.md` confirman **0 / 0 / 0, sin cambios y verificación aprobada en ambos**, 0 tokens. No se fusionó ninguna rama adicional ni se atribuyeron los dos automáticos a una causa no inspeccionada. Los próximos commits de E8b deben volver a comprobar su documentación; el cero de este corte no certifica fuentes posteriores.

## Lo que falta para la puerta

- Mantener la documentación en cero sobre el corte final: los snapshots commiteados incorporados están en cero; el directorio de trabajo con fuentes posteriores en curso conserva dos automáticos. Las suites finales, el E2E permanente, la comparación visual afectada y el retiro efectivo en una copia con bases migrada y vacía tienen los resultados indicados arriba.
- Resolver aprobación/categoría de las cuatro variantes que todavía no son Utility aprobadas. Los cuatro botones URL ya están creados con el origen del túnel.
- Completar las entregas reales que dependen de la categoría pendiente de Meta. Bot neutro e ingreso exclusivo del operador, alta empresarial por Phone con ConsumerSignup cerrado, registro personal abierto/cerrado, ingreso posterior a recuperación aprobada, Mi cuenta, destinos ocupados, suspensión, recuperación con ambos métodos anteriores, enlaces usados/invalidados/vencidos y Bot → Personal → Perfiles → Organización tienen evidencia posterior. El correctivo visual de Cuentas está comprobado en el túnel; el menú Personal cerró su pase dirigido. El transporte controlado no sustituye la entrega por Meta.
- Incorporar el pase documental del 409 y la corrección de la premisa de su ficha, conservando la exigencia de versión. La prueba dirigida ya pasó 6/6 y está commiteada; no atribuye la causa del intento histórico. La ampliación del E2E permanente pasó 34/34 y el menú cerró su pase documental/revisiones; se conserva separado el snapshot y su transporte controlado.

### Correctivos finales de Front

El correctivo del runner `f70fb7e97e61267796cd5248ae9e7f7461dc0541` pasó SPEC y QUALITY independientes y **37/37** focales. La primera ejecución real falló antes del OTP en `page.goto` (30 s): la copia Back dentro de `.local` del Front tenía un enlace circular al propio Front. Se conservó el diagnóstico y se trasladó la fixture al root de visualizaciones autorizado, fuera de ese árbol; se retiró sólo el enlace propio. No se cambiaron respuestas, reloj, cooldown ni rutas productivas.

La repetición completa terminó con **34/34 grupos, exit 0, verified=true, 381360 ms**, sobre fixture Back `git archive 250c498` compilada con cero advertencias/errores. Los diecisiete grupos por tamaño incluyen configuración Cerrado/Abierta por UI y GET pasivo/F5, alta e ingreso empresarial Phone/OIDC/F5 y restauración por UI/GET/F5, además del recorrido anterior. El coordinador cotejó `receipt.json` del Front y su `CHECK FINAL`, y comprobó **5176 sin listener**. Evidencia: `.local/e8-consumer-closed-back-20261009/receipt.json` y sus logs/capturas del Front. La fixture normalizó contactos nulos a contactos sintéticos antes del recorrido: la prueba restaura políticas, límite y contactos restaurables; **no acredita volver los contactos a null**. API, base y cookies son reales; Meta y `delivered` siguen fuera de ese transporte controlado.

El menú fue corregido en `845e6fe7cfb05a6bef3cc2cf281ed640c0ac1d5a`: usa «Sin dato»/«No data» existentes cuando no hay nombre ni correo, en texto y nombres accesibles de Personal/Business, Más, hoja pública móvil y Sidebar. Conserva el nombre real para Avatar; no introduce teléfono visible, textos nuevos ni HTTP adicional. **38/38** tests dirigidos de cinco archivos, build completo y lint propios en verde. SPEC y QUALITY aprobaron, con 38/38 independientes de SPEC; el pase documental quedó en `de184460`. El coordinador cotejó su [informe](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/etapa-8/informe.md) y alcance de los commits. El ajuste del contrato del asistente es otro commit, `0d3468ca`, una inserción `activeTurnId: null` en su fixture, con 34/34 runtime y build en verde, revisado por separado; no se mezcla con el correctivo E8. El build aislado reproducible de ese HEAD espera que Back commitee sus dos archivos de tipos regenerados; no se incluyen ni modifican desde este chat.

### Nueva revisión operativa de Meta

La consulta Graph de **2026-10-10 02:30:20 UTC** (09/10 23:30 de Argentina) confirma dos Authentication aprobadas, 26 Utility aprobadas y las mismas cuatro excepciones. La UI de soporte muestra **tres revisiones de categoría Pending** (`invitacion_organizacion`, es/en; `pagina_publica_moderada/en`) y **una apelación** (`aviso_metodo_ingreso/en`, última revisión 9 de octubre). El detalle de invitación atribuye la reclasificación a contenido Marketing; el del aviso de métodos atribuye el rechazo a contenido Authentication. Este último aviso no contiene OTP: notifica un cambio ya realizado. El diálogo indica que las revisiones suelen tardar 24 horas; no ofrece una decisión ni garantiza ese plazo. Evidencia: `.local/reviews/2026-10-09-meta-aviso-apelacion-pendiente.jpg`.

Se corrigió la [guía operativa](../operations/whatsapp-plantillas.md), que todavía decía que no se habían creado las cuatro variantes con URL y que el catálogo seguía por implementar. La corrección conserva cuerpos, variables, categoría requerida y textos del lienzo. La acción inmediata recomendada es esperar la revisión ya solicitada y volver a consultar sus estados antes de habilitar el envío. Ante una decisión desfavorable, revisar el motivo y preparar un ajuste concreto de texto/ejemplos para aprobación humana, sin dar por segura su aceptación ni cambiar a Marketing.

Ayuda ofrece «Contactar con el servicio de asistencia». La revisión automática rechazó abrir esa opción porque puede iniciar una escalación externa y la autorización actual sólo cubre inspección; no se creó un caso ni se envió información. Quedó preparado `.local/reviews/2026-10-09-meta-soporte-borrador.md`, con las cuatro variantes y su justificación funcional, sin secretos, destinatarios, códigos ni enlaces privados. Contactarlo y enviar el borrador requiere autorización expresa; no se duplican apelaciones pendientes.

El usuario eligió **«Esperar la revisión (recomendado)»**. El borrador queda sin enviar y no se abre soporte. Se continúa con los cierres técnicos E8; una respuesta de Meta debe volver a comprobarse antes del envío real pendiente.

La limpieza de la prueba real está hecha: teléfono libre, cuentas conservadas por sus correos, registro **Abierto/Abierta**, sesiones temporales cerradas, callback compartido anterior y encabezados por defecto restaurados; Aspire aislado detenido y 5176 libre. Los datos temporales quedan únicamente en el volumen aislado.

## Decisiones tomadas

- Reutilizar los secretos/activos existentes sin reemplazar los que ya coinciden.
- Coordinar desde este chat los tres encajes aprobados, la regeneración del contrato y el orden de ejecuciones de ambos repos.
- Cambiar solamente el cierre textual de las cuatro variantes fallidas, con autorización expresa; se conserva la instrucción de soporte, variables y categoría. Email no cambia. Mensajes v115 recibe ese ajuste puntual autorizado, sin renumerar tableros ni modificar sus otros estados.
- Conservar el recorrido de recuperación actual, según confirmó el usuario: también atiende la pérdida de acceso a los métodos que reciben códigos. Esta continuación no incorpora contraseñas ni cambia ese flujo.

## Prueba Meta real en paralelo con E8b (09/10, noche)

El usuario pidió ejecutar aquí el recorrido real E8 con WhatsApp Web mientras los dos chats avanzan E8b. Es una excepción explícita al orden de ejecución: **E8 conserva abierta su puerta operativa**. Back prepara T00/T01 E8b y el contrato primero; Front terminó su preparación en lectura y espera el freeze B/F. La implementación paralela de Parte 2 se habilita después de esos commits.

La prueba usa un worktree managed propio, `C:/Users/ezequ/.codex/worktrees/etapa-8-meta-real/ArquitecturaBaseMutitenant`, sobre Back `16662d87`; el Front es un `git archive` separado de `1192f3d4`. No comparte `bin/obj`, `node_modules`, PostgreSQL ni volumen con main. El AppHost temporal usa PostgreSQL 5544, volumen `etapa-8-meta-real-pgdata`, API HTTPS 63323 y Front 5176. Los builds API y AppHost terminaron en 0 advertencias/errores; `npm ci` y build Front terminaron con exit 0. npm informó engines de dependencias de tests incompatibles con el Node local 24.14.0; esta copia no reejecuta Vitest ni sustituye su evidencia previa.

El número emisor y las credenciales Meta son los existentes; no se copiaron a archivos. El operador local es sintético, sin Phone y con Email PickupDirectory. Esto permite preparar la revisión de recuperación y **no acredita SMTP ni Google reales**. Los documentos legales sembrados dicen explícitamente que son demostración, sin términos/política productivos.

Se reutilizó el túnel multitenant existente `tunnel-e9a04def.brs`, sin hosts antes de iniciar. Aspire requiere el ID `tunnel-e9a04def`, sin sufijo regional: el primer intento falló y se conservó; el segundo inicio pasó. El origen emitido y confirmado por Aspire es `https://n1w0n29x-63323.brs.devtunnels.ms/`. Se sirve el build Front en `wwwroot` de la API mediante su hosting SPA existente, con issuer, redirects y postlogout de ese origen. Es configuración exclusiva de la copia.

Verificación HTTP pública: `/alive` 200 Healthy, `/ingreso` 200 HTML, discovery OIDC 200 JSON y GET de verificación WhatsApp 200 con desafío exacto. El túnel reescribía Host/Origin y discovery devolvía endpoints localhost; `devtunnel port update ... --host-header unchanged --origin-header unchanged` corrigió authorize/token/logout al dominio público, sin cambiar certificados ni reglas de identidad. Conservar estos encabezados es una opción del [CLI oficial](https://learn.microsoft.com/azure/developer/dev-tunnels/cli-commands).

Meta aceptó `POST WABA/subscribed_apps` con override temporal hacia `/webhooks/whatsapp` de ese origen, y el GET posterior lo confirmó para la app existente. La segunda app suscrita no cambió. El callback por defecto del App Dashboard sigue siendo el de ArquitecturaBase. Antes de liberar la prueba se debe quitar el override mediante POST sin esos campos para volver al callback previo, como indica la [colección oficial de Meta](https://www.postman.com/meta/whatsapp-business-platform/documentation/du6gzjv/embedded-signup). También se restauran los encabezados por defecto del puerto y se detiene exclusivamente este AppHost.

WhatsApp Web está conectado y tiene el chat del bot. El usuario autorizó expresamente usar la cuenta conectada terminada en 0069 para las cuentas temporales y códigos/avisos de esta prueba. El primer envío de registro se detalla abajo. No guardar el número completo, códigos ni enlaces de autenticación en informes o capturas.

Chrome bloqueó inicialmente la navegación pública con `ERR_BLOCKED_BY_CLIENT`. El usuario confirmó que ya carga, y se verificó después el formulario real WhatsApp en `/login` (la primera URL `/ingreso` no era una ruta del proyecto). No se desactivaron protecciones ni se continuó por la alternativa local que mostró un error de certificado. Evidencia inicial y del formulario público, sin datos privados: `.local/reviews/2026-10-09-e8-tunel-chrome-bloqueado.jpg` y `2026-10-09-e8-login-whatsapp-publico.jpg`. Esto prepara el recorrido; todavía no acredita OTP ni ingreso.

El usuario autorizó crear las cuatro variantes con botón URL y luego cerrar el cuerpo de invitación con «Podés revisarla con el botón.» / «You can review it using the button.» tras el rechazo 100/2388299. **Las cuatro están creadas**; GET de 23:38 UTC confirma 32 variantes E8 y cuerpos/botones URL iguales al paquete aprobado (4/4). Authentication aprobada: 2; Utility aprobada en esa consulta: 24. Las nuevas estaban pendientes y Meta reclasificó ambas invitaciones a Marketing. Se pidió su revisión; la UI confirmó ambas filas Pending y el total En revisión de 1 a 3. Las excepciones anteriores de página pública/en y método de ingreso/en continúan. Creación/revisión no acredita Utility habilitada ni entrega/delivered.

Guía y ejemplo del lienzo quedaron ajustados; Front commit `addd0bd2`. Se alinearon también seis valores de `WhatsAppTexts.resx/en`: invitación y los cierres de baja suspendida/aviso de recuperación aprobados antes que faltaban en resources. Cotejo guía/resources 6/6; paridad 7/7; catálogo WhatsApp 3/3; Application build 0 advertencias/errores; script del lienzo parseado. Las pruebas usan artefactos privados en la copia; ningún build Root sobrescribió DLL de main. El runtime se reinició con esos textos, AppHost build 0/0 y salud pública/OIDC correctos. Sin cambios de lógica, variables, eventos o contrato HTTP. Evidencias locales: `2026-10-09-meta-inventario-e8-completo.json`, `2026-10-09-meta-utility-url-creacion.json` y `2026-10-09-meta-invitaciones-categoria-en-revision.jpg`.

E8b T00 ya está commiteada: Back `fe36e7ce`, Front `429fcfe`. Ambos chats recibieron GO condicionado: Back puede seguir T02–T09 después de T01 aprobado/commiteado y freeze B/F; Front verifica ese freeze antes de empezar T10–T13. No se requiere otra confirmación para ese trabajo autorizado. No declarar E8 completa.

### Primer tramo real: bot y código de registro

Con el destinatario autorizado, un «Hola» nuevo recibió por WhatsApp la respuesta sin cuenta y el botón «Crear mi cuenta». La web real mostró «No hay una cuenta con ese número». Antes de registrar había un mensaje Inbound/Text `Processed`, un Outbound/Interactive `Delivered` y cero Authentication en la base aislada. La respuesta del bot acredita entrada real por Meta y su devolución; no es una fixture ni un mensaje histórico del chat.

La revisión automática rechazó inicialmente marcar la casilla de Términos/Privacidad por faltar autorización explícita para el consentimiento. Los dos documentos servidos por la API dicen «DOCUMENTO DE DEMOSTRACIÓN» y que no incluyen términos/política legales. El usuario respondió «Sí, aceptar los textos de demostración para la prueba»; recién después se marcaron y se envió el registro. Esto no autoriza acuerdos productivos ni externos.

La web pasó a «Revisá tu WhatsApp», con destinatario enmascarado y seis casillas vacías. WhatsApp Web recibió el mensaje de Authentication pero **oculta el código de un solo uso y exige verlo en el dispositivo principal**. El usuario leyó el código en el celular, lo ingresó directamente y confirmó que se creó la cuenta; se verificó el shell Personal/Inicio. No se extrae el código de la base, logs ni estado interno para suplir esa lectura. La consulta de metadatos confirma un Outbound/Authentication `Delivered`, con `EncryptedText` nulo; dos eventos Delivery `Sent` y dos `Delivered` para el bot y ese código. Evidencia sin datos privados: `.local/reviews/2026-10-09-e8-registro-personal-confirmado.jpg`.

El GET de Meta del 09/10 a las 23:58:21 UTC confirmó las 32 variantes: ambas `recuperacion_aprobada` ahora están `APPROVED`/`UTILITY`. Quedan cuatro excepciones: `invitacion_organizacion` es/en `APPROVED`/`MARKETING` con revisión de categoría solicitada, `aviso_metodo_ingreso` en `IN_APPEAL`/`UTILITY`, y `pagina_publica_moderada` en `APPROVED`/`MARKETING`. No se envía una invitación como Marketing para sustituir la categoría Utility acordada. Evidencia: `.local/reviews/2026-10-09-meta-inventario-e8-real.json`.

### Mi cuenta y enlace de cuenta vinculada

La revisión automática pidió autorización explícita para agregar una credencial secundaria. El usuario autorizó agregar/verificar `e8-meta-persona-a@example.test` en esta cuenta temporal; se hizo desde Mi cuenta y se verificó con el párrafo OTP del correo en el pickup aislado. Esto acredita la conducta del correo local, no entrega SMTP. El aviso de método agregado se entregó por WhatsApp: un Outbound/Template `Delivered`, sin contenido persistido.

Un segundo «Hola» recibió el botón «Entrar» con URL del túnel. Abrirlo mostró «Entrar a ArquitecturaBase», el número enmascarado y «Continuar». La base conservaba un enlace y cero consumos: abrir preview no lo consumió. «Continuar» entró por OIDC a Personal; la base pasó a un consumo. F5 conservó Personal y reabrir el enlace usado mostró «Este enlace ya no sirve». El token vive sólo en memoria del montaje, y el front retira el fragmento de la URL: recargar ese preview sin fragmento descarta la prueba local, sin consumir ni invalidar el enlace en la base. No atribuir ese caso a un vencimiento del servidor.

Evidencias: `.local/reviews/2026-10-09-e8-bot-preview-real.jpg`, `2026-10-09-e8-bot-ingreso-personal.jpg` y `2026-10-09-e8-bot-enlace-usado.jpg`. Se salió por «Salir», se volvió desde la portada a «Ingresar» y se pidió un código WhatsApp para la cuenta existente. La pantalla explica «Llega al WhatsApp de tu celular. En WhatsApp Web no se muestra.»; el usuario completó el nuevo código en el celular y se verificó Personal. La base confirma dos Authentication `Delivered` acumulados (registro e ingreso), ambos sin contenido cifrado persistido. `2026-10-09-e8-codigo-ingreso-entregado.jpg` muestra el formulario previo sin código. No cerrar la etapa por estos tramos parciales.

### Principal, retiro y número ocupado

El usuario autorizó hacer principal el correo sintético de A y quitar su teléfono conservando ese correo. Ambas acciones pasaron por sus confirmaciones normales sin casillas de código. Antes de quitar había dos enlaces, uno consumido y ninguno invalidado; después hubo uno invalidado. Abrir el enlace pendiente desde el botón real de WhatsApp mostró «Este enlace ya no sirve». Authentication siguió en dos `Delivered`. El aviso de principal llegó por Meta, y el aviso de retiro al correo restante, comprobado en el pickup propio. Evidencias: `2026-10-09-e8-cuenta-telefono-quitado.jpg` y `2026-10-09-e8-bot-enlace-invalidado.jpg` en `.local/reviews/`.

Se creó la cuenta propia temporal B por `e8-meta-persona-b@example.test`, usando el registro real, los mismos documentos demo autorizados y OTP del pickup. Desde Mi cuenta agregó el teléfono libre autorizado; el usuario lo verificó desde su celular. Ese tercer Authentication quedó `Delivered`. A conserva sólo su correo; B tiene el Phone verificado y un correo principal, lo que permite probar conflicto entre cuentas sin otra persona ni otro número.

| Pantalla real | Caso | Resultado visible |
|---|---|---|
| Registro | Phone verificado de B | «Ya tenés una cuenta con ese número.» |
| Recuperar | Método anterior Email de A; nuevo Phone de B | «Ese número ya es de otra cuenta.» |
| Mi cuenta de A | Agregar Phone verificado de B | «Ese número ya está asociado a otra cuenta.» |

Los tres rechazos no avanzaron a OTP. El cotejo de 10/10 a las 00:27 y 00:28:40 UTC conservó exactamente **3 Authentication, 3 Interactive y 2 Template `Delivered`**, con cero contenido cifrado en esos mensajes outbound: no emitieron código ni aviso al dueño. Metadatos agregados, sin direcciones, números, wamid ni tokens: `.local/reviews/2026-10-09-e8-meta-metadatos.jsonl`. Recortes sólo del error visible: `2026-10-09-e8-registro-numero-ocupado.jpg`, `2026-10-09-e8-recuperar-numero-ocupado.jpg` y `2026-10-09-e8-cuenta-numero-ocupado.jpg`.

### Recuperación real: pendiente y rechazo

Se ingresó a B por su correo local y se quitó el teléfono desde Mi cuenta, conservando el correo verificado y sin OTP adicional. Con el teléfono libre se inició Recuperar para el correo anterior de A y ese WhatsApp como método nuevo. El usuario verificó el cuarto código real desde el celular; la web envió el pedido y mostró «Recibimos tu pedido», sin iniciar sesión. La consulta agregada confirmó `Pending|1`, cuatro Authentication y tres Template `Delivered`. Intentar ingresar con el teléfono pendiente mostró «No hay una cuenta con ese número», sin emitir otro código. La verificación de posesión no concede acceso por sí sola.

El operador sintético entró por `/plataforma`, con código de su pickup y aceptación de los mismos documentos de demostración autorizados. La bandeja mostró un pedido pendiente con ambos métodos enmascarados. Se rechazó desde «Revisar» con el motivo «Prueba integral E8: rechazo controlado.»; la bandeja pasó a un pedido y cero pendientes. «Ver detalle» recuperó estado, operador y motivo guardados. La base confirmó `Rejected|1` y cuatro Template `Delivered`; WhatsApp Web mostró el aviso de rechazo, sin incluir el motivo interno. No se agregó el teléfono a la cuenta.

Evidencia local sin código ni número completo: `2026-10-09-e8-recuperacion-pendiente-sin-ingreso.jpg`, `2026-10-09-e8-recuperacion-rechazada.jpg` y `2026-10-09-e8-recuperacion-rechazo-detalle.jpg` en `.local/reviews/`. Se preparó un segundo pedido para comprobar aprobación y su botón Utility; todavía no acredita esa rama ni cierra la puerta.

### Recuperación real: aprobación y botón

El usuario verificó el quinto código real y la web registró un segundo pedido pendiente. La aprobación quedó preparada con el motivo «Prueba integral E8: aprobación controlada de cuenta temporal.»; antes de ejecutarla se pidió la autorización puntual para agregar el método de acceso. El usuario respondió «Sí, aprobar la recuperación temporal». Plataforma lo aprobó; después de F5 muestra dos pedidos, cero pendientes, uno Aprobado y otro Rechazado. La consulta de estados confirma lo mismo. La pantalla pública seguía en «Recibimos tu pedido»: aprobar no creó una sesión.

WhatsApp Web mostró «Aprobamos tu pedido: ya podés entrar a ArquitecturaBase con este número, que quedó como tu método principal.» y el botón **Ingresar**. Su botón real abrió `/login` en el origen HTTPS del túnel. El sexto Template quedó `Delivered`; el correo conservado recibió el aviso de método agregado, comprobado en el pickup propio. También se comprobaron allí los dos avisos legítimos de pedido a la cuenta anterior; no son avisos por un número ocupado. Se solicitó un nuevo código desde el ingreso abierto por el botón para terminar la prueba de acceso posterior.

Evidencias: `2026-10-09-e8-recuperacion-aprobar-propuesta.jpg` y `2026-10-09-e8-recuperaciones-decididas.jpg` en `.local/reviews/`. GET Meta de **10/10 a las 00:45:58 UTC** conserva 32 variantes: dos Authentication aprobadas, 26 Utility aprobadas y las cuatro excepciones de categoría/apelación indicadas antes (`2026-10-09-meta-inventario-e8-actualizado.json`). El usuario completó el sexto código de WhatsApp; se verificó Personal y, en Mi cuenta, Phone principal y Email conservado. Meta confirma seis Authentication `Delivered`. El operador mantuvo su sesión Plataforma después de recargar, mientras la otra pestaña usaba Personal. La recuperación con nuevo Phone está acreditada; no cierra toda la puerta.

### Suspensión y hallazgo visual

Se suspendió A desde Plataforma con motivo controlado. Su sesión Personal dejó de entrar después de recargar; la del operador siguió operativa. Un «Hola» real recibió «No podemos darte un enlace para entrar. Usá la web para revisar tu cuenta.», sin enlace ni botón: Outbound/Text `Delivered`, con el conteo de enlaces todavía en dos. Ingreso Phone emitió el séptimo Authentication `Delivered`, como exige la conducta vigente: el estado se explica sólo después de probar posesión. El usuario verificó el código y la web mostró «Tu cuenta está suspendida», sin sesión. Se volvió al ingreso y se reactivó la cuenta por la UI normal con motivo, para continuar las pruebas. Evidencia recortada sólo al aviso, sin OTP: `2026-10-09-e8-suspendida-codigo-valido.jpg`.

**Hallazgo abierto de Front:** una cuenta sin nombre y con Phone principal tiene `primaryEmail=null`; `accountName` devuelve vacío. La tabla muestra «Sin dato» mediante el componente compartido, pero la ficha tiene H1 vacío y no identifica breadcrumb/acciones; los diálogos y toasts de suspensión/reactivación también quedan sin identificación. No afecta la decisión ni los métodos. Se encargó al chat Front un correctivo aislado con el fallback vigente y un test del caso, sin inventar nombre ni mostrar el teléfono completo. Se pidió revisar también el rótulo vacío del menú Personal contra sus convenciones. Evidencia: `2026-10-09-e8-plataforma-phone-sin-titulo.jpg`. No dar por corregido hasta recibir el commit y comprobarlo.

### WhatsApp como método anterior y vencimiento natural

El usuario autorizó un tercer pedido temporal: WhatsApp de A como método anterior y `e8-meta-recuperacion-c@example.test` como nuevo contacto, con OTP exclusivamente en el pickup local, seguido de rechazo. Se verificó y registró por la UI pública. El aviso de pedido llegó al WhatsApp anterior por Meta; el séptimo Template quedó `Delivered`. El primer rechazo desde Plataforma devolvió 409 y «Otra persona cambió esto. Revisá la versión más reciente antes de guardar.»; se conserva ese intento. Se canceló, recargó y volvió a leer el pedido antes de rechazar con la versión vigente. El worker de aviso es una posible explicación del cambio concurrente, todavía sin atribución confirmada.

Después de F5, Plataforma muestra **tres pedidos, cero pendientes: dos Rechazados y uno Aprobado**. El pickup del correo nuevo contiene «No pudimos recuperar tu cuenta». Este pedido rechazado no concede acceso por el correo nuevo; A conserva sus métodos anteriores. Evidencia: `.local/reviews/2026-10-09-e8-recuperacion-telefono-anterior-rechazada.jpg`.

El enlace emitido tras reactivar A se dejó sin tocar durante los diez minutos completos. Antes de cerrar la sesión o invalidar otros enlaces, los metadatos de **10/10 a las 01:08:47 UTC** mostraban tres enlaces, uno consumido y uno invalidado; el tercero había vencido sin consumo ni invalidación. Su botón real de WhatsApp abrió «Este enlace ya no sirve». No se adelantó el reloj ni se alteró la base. Evidencia: `.local/reviews/2026-10-09-e8-bot-enlace-vencido-real.jpg`.

### Bot, Perfiles y organización; sesiones separadas

Con autorización expresa se creó en la base aislada «Prueba integral E8 Meta», dirección `prueba-e8-meta`, y la primera empresa vacía «Empresa E8 Meta», sin CUIT. Se usó la identidad de A por su correo local, los documentos de demostración ya autorizados y el registro empresarial normal. A quedó Dueño; no se creó otra identidad para la organización.

Se salió del acceso común y un nuevo «Hola» generó otro enlace. Abrirlo mostró el preview enmascarado; Continuar entró a **Personal**, aun existiendo el perfil empresarial. Perfiles mostró la organización con rol Dueño; elegirla abrió Business y F5 conservó el acceso. La consulta de **01:17:27 UTC** confirma cuatro enlaces y dos consumos; Authentication conserva siete `Delivered`, Interactive cinco, Template siete y Text uno. Evidencias: `.local/reviews/2026-10-09-e8-bot-organizacion-inicia-personal.jpg` y `2026-10-09-e8-bot-perfiles-organizacion.jpg`.

Se comprobaron ambas salidas desde los menús reales: salir del acceso común conserva la sesión Plataforma después de F5; salir de Plataforma conserva Business después de F5. La cuenta exclusiva del operador sigue separada de los perfiles de A.

### Coordinación E8b: contrato publicado y ajuste de configuración

T01 está publicado en Back `250c498dea0e9b8d5ec770ad78b9c8399e2c2bf3`, Front `61f15c1ccf4575255c76f9e6cd62ada972d4a7a5` e informe `6998439b`. El coordinador cotejó estructuralmente el JSON contra E8: **120 rutas anteriores idénticas, cero retiradas y 40 nuevas**. SHA LF OpenAPI `fb765706ea855dfb8b11752f9243dc2672b5e91db9af6c84fd8ce1bc58e0530b`; tipos Front `e9c098388a8f87991af93146977b3cad3643c563936a66227f24b3758cb2c2c0`. Back sigue T02–T09 y Front T10–T11, con el correctivo E8 separado.

Front detectó un hueco de T12/D19: quien configura la organización necesita modelos elegibles y precios para ambos modos, pero el contrato inicial sólo devuelve `modelId`; el chat requiere otro permiso y el catálogo de Plataforma no corresponde a ese acceso. Se encargó a Back el ajuste aditivo CC01, con regeneración de OpenAPI/tipos, pruebas de permisos y nuevo pase. **T12 espera esa publicación**; no se suplen modelos o precios con listas en el Front. Los servicios 501 de T01 son contratos pendientes de implementación de E8b, no una regresión de E8.

### Correctivo de Cuentas comprobado en el túnel

Front entregó `8f37a744594af132f7f0142dcf1b1e1e71c93f0b`: ocho archivos de Cuentas de Plataforma, **128/128 focales**, build y lint focal verdes, revisiones SPEC/QUALITY aprobadas. Se aplicó exclusivamente ese diff a la copia E8, sin tipos, dependencias, transporte ni navegación de E8b. El build de esa copia terminó con exit 0 y su `dist` se publicó en el `wwwroot` aislado; los índices coinciden. Log: `.local/reviews/2026-10-09-e8-correctivo-front-build.log`.

Todavía con Phone principal y sin nombre, la ficha real mostró H1/breadcrumb «Sin dato», «Acciones de Sin dato» y «¿Suspender la cuenta de Sin dato?». Los dos métodos siguieron enmascarados. Se canceló esa confirmación sin otra suspensión; los toasts están cubiertos por las pruebas focales del pase. Evidencias: `2026-10-09-e8-plataforma-phone-titulo-corregido.jpg` y `2026-10-09-e8-plataforma-phone-confirmacion-corregida.jpg` en `.local/reviews/`. El título de Cuentas queda corregido; el menú Personal con nombre vacío se remitió como hallazgo separado para contrastarlo con el fallback compartido.

### Registro cerrado y restauración

El usuario autorizó el circuito reversible final: volver a hacer principal el correo de A y quitar su WhatsApp conservando identidad/organización; cerrar temporalmente el registro personal con alta empresarial abierta y restaurarlo; verificar el teléfono en el operador sintético, probar su bot/ingreso y después quitarlo conservando su correo. La revisión automática había rechazado repetir el cambio principal por interpretar insuficiente la autorización anterior; no se ejecutó hasta esta respuesta expresa.

La configuración inicial aislada tenía ambos contactos vacíos. El primer intento de guardar devolvió errores de correo válido y no persistió cambios. El usuario autorizó completar ventas y soporte con `e8-meta-operator@example.test`, entregado exclusivamente al pickup local. Se guardó ConsumerSignup Cerrado y BusinessSignup Abierta con motivo y se comprobó tras F5. A quedó con su correo principal y su organización conservada, sin Phone; principal/quitar no pidieron OTP.

Un «Hola» del número libre recibió el aviso de que no tiene cuenta y por ahora no se pueden crear cuentas, con **Ingresar** y **Recuperar mi cuenta**. Son quick replies: ambas entraron por el webhook como Interactive y devolvieron «Continuá en la web» con su botón URL. Los botones reales abrieron `/login` (sólo `returnUrl`) y `/recuperar` (sin query); ninguno incluyó teléfono, código, nonce ni fragmento privado. Desde el ingreso se intentó el registro WhatsApp normal, con los documentos demo autorizados: mostró «Por ahora no se pueden crear cuentas», sin paso de código. Los metadatos de **01:32:05 UTC** conservan **7 Authentication `Delivered`**, con 8 Interactive y 8 Template `Delivered`; no hubo envío adicional por ese rechazo.

Después se restauró **Abierto/Abierta**, con motivo, y F5 lo confirmó. Los contactos sintéticos quedan sólo en la base aislada, según la autorización específica. Evidencias: `.local/reviews/2026-10-09-e8-registro-cerrado-ingreso-web.jpg`, `2026-10-09-e8-registro-cerrado-sin-codigo.jpg`, `2026-10-09-e8-a-correo-organizacion-conservados.jpg` y `2026-10-09-e8-registro-configuracion-restaurada.jpg`. La continuidad empresarial con OTP Phone mientras ConsumerSignup está cerrado tiene integración Back en `WhatsAppIdentityTests.Business_phone_signup_is_independent_and_otp_cannot_register_consumer` (cuatro variantes, identidad nueva/existente y alta abierta/con aprobación). El runner de navegador comprueba alta empresarial Phone con registro abierto, **todavía no esta combinación cerrada**; se encargó extender ese recorrido. También falta su ejecución por Meta.

Desde Mi cuenta del operador se agregó el teléfono autorizado y se solicitó el código. Meta confirma el **octavo Authentication `Delivered` a las 01:35:14 UTC**, sin contenido persistido. El diálogo «Verificar tu WhatsApp» está preparado con destino enmascarado y casillas vacías; el usuario debe leerlo en su celular. Este estado pendiente todavía no acredita bot neutro ni ingreso Phone del operador. Evidencia: `.local/reviews/2026-10-09-e8-operador-verificacion-pendiente.jpg`.

### Bot del operador y código de ingreso

El usuario completó la verificación del teléfono del operador temporal. Un nuevo «Hola» recibió exactamente **«Para entrar, usá la web.»**, sin enlace, botón ni mención de Plataforma. La consulta de **01:39:20 UTC** confirma un segundo Outbound/Text `Delivered` y conserva cuatro enlaces: el bot no emitió otro. Captura sólo de ese mensaje: `.local/reviews/2026-10-09-e8-bot-operador-neutro.jpg`.

Se salió por el menú normal, abrió `/plataforma` y solicitó ingreso por WhatsApp en su puerta. El noveno Authentication está `Delivered` a las **01:41:40 UTC**, sin contenido persistido; el usuario debe ingresarlo desde el celular. Todavía falta comprobar destino Plataforma, F5 y retirar el teléfono del operador conservando su correo. Captura previa, con casillas vacías y destino enmascarado: `.local/reviews/2026-10-09-e8-operador-ingreso-codigo-pendiente.jpg`.

GET Meta de **01:42:29 UTC** sigue mostrando dos Authentication aprobadas, 26 Utility aprobadas y cuatro excepciones sin cambios. La entrega real de invitación continúa pendiente de revisión de categoría; no se envió como Marketing.

Front señaló otro hueco E8b: al reabrir una conversación en generación, el GET no devuelve el turno activo durable que exige Stop. El coordinador cotejó `AssistantConversationResponse`, Summary y Message y remitió a Back un delta aditivo mínimo con pruebas y publicación B/F. El stream en vivo puede continuar; **Detener al reabrir queda pendiente del pase**, sin fabricar IDs ni ocultar la limitación. No se cambió unilateralmente el contrato.

### Operador: ingreso confirmado y teléfono retirado

El usuario confirmó que verificó el código y llegó a Plataforma. El coordinador comprobó el shell y recargó: conserva Plataforma; el menú ofrece únicamente **Mi cuenta** y **Salir**, sin perfiles comunes. Desde Mi cuenta se ejecutó el retiro del WhatsApp expresamente autorizado, conservando `e8-meta-operator@example.test` como principal. No pidió otro código. Después de una nueva recarga, «Cómo entrás» muestra sólo ese correo y Google sin vincular; el teléfono quedó libre. A y B conservan sus correos, y A conserva la organización creada.

Evidencias seguras: `.local/reviews/2026-10-09-e8-operador-plataforma-confirmado.jpg` y `2026-10-09-e8-operador-correo-conservado.jpg`. Los metadatos de **02:03:55 UTC** muestran **10 Authentication, 8 Interactive, 8 Template y 2 Text `Delivered`**, sin contenido outbound persistido. El conteo de Authentication aumentó en uno durante el tramo asistido de ingreso; no se atribuye ese envío adicional a una acción que el coordinador no observó. La confirmación humana y la sesión persistente sí acreditan el ingreso.

El circuito final autorizado del operador está completo y limpio. Se preparó una consulta concreta para la prueba real de alta empresarial por Phone con ConsumerSignup cerrado: nueva cuenta y organización temporales, conservación por correo local, retiro del teléfono y restauración de la configuración. Esa ampliación espera autorización; mientras tanto la configuración sigue **Abierto/Abierta** y no se creó esa tercera cuenta.

### Alta empresarial con registro personal cerrado: completa

El usuario respondió «Sí, autorizar el circuito completo»: cuenta temporal por WhatsApp, documentos demo, «Prueba empresarial E8 cerrado» (`prueba-e8-cerrado`) y «Empresa E8 cerrado» sin CUIT, conservación por `e8-meta-empresa-c@example.test` del pickup, correo principal, retiro del teléfono y restauración **Abierto/Abierta**. Se guardó **Cerrado/Abierta** desde Configuración con motivo y F5 lo confirmó.

Desde la portada pública se siguió «Registrá tu empresa», eligió WhatsApp y continuó con el teléfono autorizado y el consentimiento demo autorizado. El formulario avanzó a «Revisá tu WhatsApp», aunque el registro personal está cerrado. Los metadatos de **02:07:24 UTC** confirman **11 Authentication `Delivered`**, uno más que antes del envío empresarial, sin contenido persistido. El usuario verificó el código y la web mostró «Tu organización»; se creó la organización y su primera empresa por el formulario normal, sin CUIT. «Entrar» abrió Business; F5 conservó acceso, y el menú mostró el rol Dueño y el perfil Personal de la misma cuenta.

Desde Mi cuenta se agregó y verificó el correo autorizado con el pickup propio, se hizo principal y se quitó el teléfono, sin otros códigos por WhatsApp. Después de recargar conserva sólo el correo y su organización. Se restauró **Abierto/Abierta** desde Plataforma con motivo y F5 lo confirmó. Los metadatos finales de **02:11:51 UTC** muestran tres cuentas comunes y un operador, **11 Authentication, 8 Interactive, 10 Template y 2 Text `Delivered`**, sin contenido outbound persistido. La continuidad empresarial con registro personal cerrado queda acreditada por Meta y UI real, además de las cuatro variantes de integración Back; el correctivo de cobertura del runner permanece separado hasta su pase.

Evidencias sin código ni teléfono completo: `.local/reviews/2026-10-09-e8-empresa-registro-cerrado.jpg`, `2026-10-09-e8-empresa-cerrado-codigo-pendiente.jpg`, `2026-10-09-e8-empresa-cerrado-creada.jpg`, `2026-10-09-e8-empresa-cerrado-correo-conservado.jpg` y `2026-10-09-e8-empresa-cerrado-config-restaurada.jpg`.

### Limpieza y estado de cierre del recorrido

Se salió de ambas sesiones por sus menús reales y cerraron sólo las pestañas propias del circuito terminado. El teléfono autorizado quedó libre; las tres cuentas comunes y el operador conservan correo local. Ambas organizaciones quedan en la base aislada; no se eliminó ningún dato ni se modificó una cuenta productiva.

El POST sin campos de override y su GET posterior de **02:13:52 UTC** confirmaron las dos apps originales suscritas con `overrideCallback=null`; se vuelve al callback por defecto anterior de ArquitecturaBase. Evidencia: `.local/reviews/2026-10-09-meta-callback-restaurado.json`. Se detuvo exclusivamente el AppHost del worktree E8 mediante `aspire stop --apphost ...`, y se comprobó que **5176 no tiene listener**; Front recibió GO para su E2E propio.

El CLI `1.0.2094` devolvió exit 0 al actualizar encabezados, pero su lectura posterior mostró Origin todavía `unchanged`. No se tomó ese exit como prueba de restauración. El [contrato oficial TunnelOptions](https://github.com/microsoft/dev-tunnels/blob/main/cs/src/Contracts/TunnelOptions.cs) distingue el valor del encabezado de `isOriginHeaderUnchanged`, que prevalece. Se usó el PUT de puerto del [SDK oficial](https://github.com/microsoft/dev-tunnels/blob/main/ts/src/management/tunnelManagementHttpClient.ts), con credencial de alcance `manage:ports` sólo en memoria y el ID canónico. GET de **02:22:20 UTC** confirmó Host/Origin nulos, ambos flags `unchanged=false`, HTTPS y ACL propios preservados. Evidencia: `.local/reviews/2026-10-09-e8-tunel-headers-restaurados.json`. No se guardaron ni imprimieron tokens.

La fotografía final de Meta de **02:23:03 UTC** conserva las 32 variantes: dos Authentication aprobadas, 26 Utility aprobadas y las mismas cuatro excepciones de categoría/apelación. **El recorrido de identidad autorizado está completo; la puerta E8 sigue abierta** por las entregas bloqueadas por Meta, documentación en cero y los correctivos pendientes detallados arriba.

### Coordinación E8b: proyección de uso pendiente

Front comunicó con autorización humana el hueco T13 y el coordinador cotejó los DTO vigentes: `AssistantUsagePageResponse` no entrega fracción/estado del presupuesto; `AssistantUsageRowResponse` contiene consultas mensuales y un límite diario opcional, pero no consultas de hoy. El lienzo distingue «Hoy» de «Consultas este mes» y exige la barra/estados de presupuesto. Las reglas de formatos impiden calcular montos o porcentajes en Front.

En ese corte se encargó a Back evaluar **CC03**, una proyección mínima tipada, con semántica de mes histórico, fecha/ámbito de cuota, PlatformKey compartida, OwnKey y null cuando no aplica. Los tres deltas se publicaron después, como acredita el corte siguiente. No se incorporaron cálculos ni IDs inventados para completar los estados.

### CC01, CC02 y CC03 publicados: E8 preservada

Back publicó `ce1494d7f620e0e152a12fde2c8d2eeea5b791e6` y Front regeneró los tipos en `3caae3b0841127a1f252b613247f78fb5173eb07`, con revisiones SPEC/QUALITY aprobadas. CC01 aporta opciones de modelos/precios al permiso de configuración; CC02 publica `activeTurnId` durable y nullable para recuperar Detener; CC03 publica presupuesto y cuota diaria tipados. Sus implementaciones de E8b siguen en los chats respectivos, sin cerrar esa etapa.

El coordinador comparó estructuralmente el contrato publicado con el corte `250c498`: **120 rutas de E8 idénticas, cero retiradas o cambiadas**. Los cuatro esquemas anteriores modificados pertenecen al asistente. Los hashes LF se cotejaron con el pase Back:

- OpenAPI: `ac450a7ca6ccfda23e5bd16f2f2fec0c20e3bec9176cfd785be9960f3919c9f4`.
- Schema Front: `523dcdbf361e37a624e90746d396d886574425d3df8c3e88c0aadbdc310175e4`.
- Alias Front: `67f6aa7c2593e5317f23ae34f653e1ff7a72fbeb9e603d14fb19488b4a0818f7`.

Front verificó ese corte con build TypeScript/Vite y `contracts:check`, ambos exit 0. El fixture CC02 `0d3468ca` aporta `activeTurnId: null`, con 34 pruebas y build aprobados. Los hashes iniciales de E8 conservados arriba corresponden a su contrato histórico completo; no se confunden con el documento OpenAPI ampliado de E8b.

### Prueba dirigida del aviso diferido y el 409

Integración reservó únicamente `PlatformRecoveryTests.cs` y preparó una copia `git archive` del Back `62879d7b`, con binarios, fixture Testing y PostgreSQL Testcontainers propios. No levantó Aspire ni Meta, no usó secretos de Development y no compiló las fuentes en curso de T03. La clase completa pasó **6/6, cero fallos y omitidos, exit 0**, en 46,158 s, con compilación incluida.

Los dos casos nuevos de `A_deferred_account_notice_invalidates_the_read_version_without_applying_a_stale_decision` comprueban aprobar y rechazar: leer el pedido Pending, ejecutar el worker real, verificar `AccountNotifiedAtUtc` y el cambio de `xmin`, enviar la versión vieja y recibir **409 `General.ConcurrencyConflict`** sin cambiar métodos, estado, eventos ni outbox. Recargar y decidir con la versión vigente tiene éxito, registra la decisión y emite un solo aviso al método nuevo; no concede una sesión.

Las cinco fuentes del recorrido siguen idénticas al baseline. Se copió al archivo principal solamente el test verificado, después de confirmar que nadie lo había cambiado; commit **`630cbbee1b507fcf665fd999baa81b032330091e`**, sin código de producto, contrato ni guardas modificadas. Recibo: `.local/reviews/2026-10-10-e8-recuperacion-409-recibo.json`. No demuestra la causa del 409 histórico, cuyas versiones antes/después no se conservaron. Back completa su [pase de auditoría](2026-10-10-recuperacion-version-aviso.md) y la corrección técnica de la ficha de concurrencia; la exigencia de versión de recuperación permanece vigente.
