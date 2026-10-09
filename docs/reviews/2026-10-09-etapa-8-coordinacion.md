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

Se solicitaron ambas revisiones desde WhatsApp Manager y se verificó el resultado visible: `pagina_publica_moderada/en` en revisión de categoría y `aviso_metodo_ingreso/en` en apelación. La nueva consulta Graph de **15:16 UTC** confirma **24 APPROVED / UTILITY**, `aviso_metodo_ingreso/en` IN_APPEAL / UTILITY y `pagina_publica_moderada/en` APPROVED / MARKETING. La solicitud de revisión no acredita aprobación. Evidencia local sin secretos: `.local/reviews/2026-10-09-meta-utility-revision-estados.json`, `2026-10-09-meta-categoria-en-revision.jpg` y `2026-10-09-meta-rechazo-en-revision.jpg`.

Las **cuatro variantes URL** de `invitacion_organizacion` y `recuperacion_aprobada` siguen sin crear. Necesitan el origen HTTPS público real del front, coherente con `ActionUrl` e issuer. El túnel de webhooks de la API, por sí solo, no hace accesible una URL de Vite local desde el celular. No aprobar botones con un origen de ejemplo o con `localtest.me`.

Paquete, respuestas y cotejo se conservaron sin secretos en `.local/reviews/2026-10-09-meta-utility-*.json`. Es evidencia local de la consulta, no una garantía sobre estados futuros. Todavía no hubo envíos a un celular real ni evidencia `delivered`.

## Soporte de integración

Back entregó `f2ec23a7`: [fixture privada de navegador](../guides/e2e-whatsapp.md) con API/Kestrel, OIDC y PostgreSQL reales. Build focal sin advertencias/errores; **15/15** pruebas de guardas/persistencia. Su smoke CLI verificó alta Phone/OTP, entrada HMAC persistida y deduplicada, procesamiento/enlace privado y OTP del operador.

El fixture bloquea transportes externos. Su control por stdin/stdout privado vincula cada orden a escenario, destino y cursor; permite obtener pruebas cifradas, consultar eventos o vencer el ticket específico de ese escenario. No agrega rutas de diagnóstico productivas. El Front implementa el runner permanente y las decisiones de recuperación desde la UI real de Plataforma.

El build completo del Back finalizó sin advertencias/errores. La primera suite completa Docker pasó **2262 tests, cero fallos y cero omitidos**, en 20m35s; el comando terminó con código 1 porque Microsoft.Testing.Platform intentó ejecutar también el tool E2E. Back corrigió esa configuración en `25983e9f`, declarando `IsTestingPlatformApplication=false`. Después centraliza dos códigos existentes en `WhatsAppErrors` y repite build, pruebas dirigidas y suite completa sobre las fuentes finales. La puerta requiere esa segunda ejecución sin error del comando. Front prepara su runner permanente y la galería; las ejecuciones se ordenan para no sobrescribir DLL en uso.

## Lo que falta para la puerta

- Completar las suites sobre los commits finales, E2E permanente de ambos repos, documentación en cero, comparación visual afectada y prueba efectiva de quitar el módulo en una copia descartable con bases migrada y vacía.
- Resolver aprobación/categoría de todas las variantes y el origen de los cuatro botones URL.
- Recibir el **celular destinatario real autorizado**, diferente del emisor y de una cuenta de operador para registro/enlace común.
- Levantar el entorno, verificar el túnel multitenant y cambiar temporalmente el callback compartido según [configuración](../operations/configuracion.md#4-lo-que-tenés-que-hacer-vos-una-sola-vez-fuera-del-código). Conservar la URL anterior para volver a ArquitecturaBase.
- Ejecutar el recorrido real con WhatsApp, OTP recibido y eventos `delivered`, sin registrar pruebas o números completos. El transporte controlado no sustituye esta evidencia.

## Decisiones tomadas

- Reutilizar los secretos/activos existentes sin reemplazar los que ya coinciden.
- Coordinar desde este chat los tres encajes aprobados, la regeneración del contrato y el orden de ejecuciones de ambos repos.
- Cambiar solamente el cierre textual de las cuatro variantes fallidas, con autorización expresa; se conserva la instrucción de soporte, variables y categoría. Email no cambia. Mensajes v115 recibe ese ajuste puntual autorizado, sin renumerar tableros ni modificar sus otros estados.
- Conservar el recorrido de recuperación actual, según confirmó el usuario: también atiende la pérdida de acceso a los métodos que reciben códigos. Esta continuación no incorpora contraseñas ni cambia ese flujo.
