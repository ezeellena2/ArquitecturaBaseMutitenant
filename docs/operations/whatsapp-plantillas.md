# Plantillas de WhatsApp: cuándo y cómo crearlas

## Alcance vigente

La E8 usa **16 nombres: una Authentication y quince Utility**, en español e inglés. Esto es inventario funcional, no una afirmación de que estén creadas o aprobadas en Meta. `codigo_ingreso` existe en la configuración de ArquitecturaBase; comprobar su estado e idiomas en la WABA de prueba antes de reutilizarlo. [ADR 0043](../decisions/0043-las-pantallas-dicen-si-hay-cuenta.md), [0045](../decisions/0045-mi-cuenta-sin-codigo-de-confirmacion.md) y [0049](../decisions/0049-precisiones-whatsapp-y-asistente.md) mandan sobre ejemplos viejos del lienzo.

Meta exige plantilla aprobada para iniciar una conversación. Un mensaje libre solo se envía dentro de las 24 horas posteriores a una entrada real de la persona. El bot de ingreso responde en esa ventana: no necesita plantilla para el enlace, el registro ni la respuesta neutra al operador.

## Inventario de E8

| Nombre Meta | Categoría | Uso y variables funcionales |
|---|---|---|
| `codigo_ingreso` | Authentication | Código para ingresar, registro y probar método nuevo (también recuperación). **No** confirma principal/quitar/baja en Mi cuenta. |
| `invitacion_organizacion` | Utility | Quien invita, organización y vencimiento; botón del enlace Email de invitación existente. Reemplaza invitacion_acceso. |
| `aviso_metodo_ingreso` | Utility | Cambio localizado, método enmascarado, fecha; LoginMethodChanged. |
| `revisa_metodos_ingreso` | Utility | Organización; ReviewLoginMethods. |
| `baja_cuenta_pedida` | Utility | Fecha de eliminación; DeletionRequested por la persona. |
| `baja_cuenta_cancelada` | Utility | Sin variables; DeletionCancelled. |
| `cuenta_eliminada` | Utility | Sin variables; AccountDeleted. |
| `baja_cuenta_plataforma` | Utility | Fecha de eliminación; DeletionRequested por Plataforma, cuenta activa y cancelable. |
| `baja_cuenta_plataforma_suspendida` | Utility | Fecha de eliminación y soporte; DeletionRequested por Plataforma, suspendida y no cancelable. |
| `recuperacion_recibida` | Utility | RecoveryReceived al método nuevo, pedido efectivamente registrado. |
| `recuperacion_aprobada` | Utility | RecoveryApproved al método nuevo; botón del ActionUrl vigente. |
| `recuperacion_rechazada` | Utility | RecoveryRejected al método nuevo; no revela datos de la cuenta. |
| `recuperacion_aviso_cuenta` | Utility | Soporte; RecoveryRequested a los métodos activos de la cuenta anterior; nunca muestra el método nuevo. |
| `organizacion_aprobada` | Utility | Organización; OrganizationApproved. |
| `organizacion_rechazada` | Utility | Organización; OrganizationRejected. |
| `pagina_publica_moderada` | Utility | Organización y estado localizado; PublicPageModerated bloqueada/permitido volver a publicar. |

El núcleo emite el `AccountNotice` cerrado existente por `IAccountNoticeChannel`. El adaptador Email sigue en el núcleo y WhatsApp atiende Phone dentro de su módulo, con outbox cifrado. DeletionRequested se mapea a tres plantillas según RequestedByPlatform/CanCancel. No inventar una variante de AccountNotice por cada nombre Meta. OrganizationApproved/Rejected y PublicPageModerated **no tienen ActionUrl**: sus plantillas no añaden botones ni enlaces.

**Derogadas:** `metodo_ingreso_en_uso` y `recuperacion_metodo_en_uso`. No crearlas ni emitirlas: 0043 informa número ocupado en la pantalla sin código ni aviso al dueño. RecoveryRequested sigue vigente para un pedido registrado. Exportación de datos e invitación de operador van solo por correo. Los cuatro correos de costo del asistente también van solo por email; no aumentan este inventario.

## Textos de los tres avisos incorporados por 0049

Paquete de carga definido para estos tres nombres. Categoría Utility, cuerpo sin encabezado ni footer y **sin botones**, es/en. Las muestras son ficticias; el adaptador pasa variables localizadas y formateadas desde resources, sin teléfonos/correos completos.

| Nombre / idioma | Cuerpo | Variables y muestra |
|---|---|---|
| organizacion_aprobada / es | Aprobamos {{1}} y terminamos el alta. Ya podés entrar como Dueño de tu organización. Ingresá con los métodos de tu cuenta y elegí tu organización. | {{1}}: Taller del Sur |
| organizacion_aprobada / en | We approved {{1}} and completed its setup. You can now sign in as the owner of your organization. Use your account's sign-in methods and select your organization. | {{1}}: South Workshop |
| organizacion_rechazada / es | No aprobamos la solicitud de alta de {{1}}. Tu cuenta y tu espacio personal siguen funcionando. | {{1}}: Taller del Sur |
| organizacion_rechazada / en | We did not approve the application to create {{1}}. Your account and personal space continue to work. | {{1}}: South Workshop |
| pagina_publica_moderada / es | La página pública de {{1}}: {{2}}. Tu organización y sus accesos siguen funcionando. | {{1}}: Taller del Sur; {{2}}: la plataforma la despublicó; quedó en borrador y no se puede publicar hasta que la plataforma lo permita |
| pagina_publica_moderada / en | The public page of {{1}}: {{2}}. Your organization and its access continue to work. | {{1}}: South Workshop; {{2}}: the platform unpublished it; it is a draft and cannot be published until the platform allows it |

Para IsBlocked=false, {{2}} es «la plataforma permitió volver a publicarla; podés revisarla y publicarla desde tu organización» / “the platform allowed publishing it again; you can review and publish it from your organization”. Esto permite la acción; **no republica automáticamente**. Dos estados, una plantilla por idioma; no dos nombres Meta. Usar el mismo contenido funcional que el correo: no sumar una decisión de negocio, motivo interno ni acción inexistente. Diseño incorpora los ejemplos WhatsApp de bloqueo y permiso para volver a publicar en Mensajes antes de implementar T03.

## Paquete de carga es/en de T03

Los trece nombres originales se completan con este paquete, cotejado con Mensajes v115 y los DTO del núcleo. Los tres avisos de organización de la tabla anterior completan los **16 nombres y 32 variantes**. Ninguna fila implica aprobación de Meta. En Utility no hay encabezado ni footer; el texto preventivo forma parte del cuerpo. El nombre del producto del lienzo se mantiene fijo: no agrega una variable ni cambia el orden congelado. Las muestras son ficticias y los destinos no se incluyen en las plantillas.

| Nombre / idioma | Cuerpo definitivo | Variables y muestras | Botón |
|---|---|---|---|
| codigo_ingreso / es | Tu código de verificación es *{{1}}*. Por tu seguridad, no lo compartas. | {{1}}: 483920 | Copiar código |
| codigo_ingreso / en | *{{1}}* is your verification code. For your security, do not share this code. | {{1}}: 483920 | Copy code |
| invitacion_organizacion / es | Hola. {{1}} te invitó a sumarte a {{2}} en ArquitecturaBase. La invitación vence el {{3}}. Podés revisarla con el botón. | {{1}}: Lucía Fernández; {{2}}: Grupo Delta; {{3}}: 04/10/2026 | Ver la invitación |
| invitacion_organizacion / en | Hello. {{1}} invited you to join {{2}} in ArquitecturaBase. The invitation expires on {{3}}. You can review it using the button. | {{1}}: Lucy Smith; {{2}}: Delta Group; {{3}}: 10/04/2026 | View invitation |
| aviso_metodo_ingreso / es | Cambiaron los métodos de ingreso de tu cuenta de ArquitecturaBase: {{1}} {{2}} el {{3}}. Si no fuiste vos, entrá a tu cuenta y revisá tus métodos de ingreso. Si no podés entrar, usá «Recuperá tu cuenta». | {{1}}: agregaste; {{2}}: •••• 4521; {{3}}: 27/09/2026 a las 14:35 | Ninguno |
| aviso_metodo_ingreso / en | Your ArquitecturaBase account's sign-in methods changed: you {{1}} {{2}} on {{3}}. If this was not you, sign in and review your sign-in methods. If you cannot sign in, use “Recover your account”. | {{1}}: added; {{2}}: •••• 4521; {{3}}: 09/27/2026 at 2:35 PM | None |
| revisa_metodos_ingreso / es | Ya no sos miembro de {{1}}. Tu cuenta de ArquitecturaBase sigue activa. Si entrabas con un correo de esa organización, puede que ya no te sirva: revisá tus métodos de ingreso desde Mi cuenta. | {{1}}: Grupo Delta | Ninguno |
| revisa_metodos_ingreso / en | You are no longer a member of {{1}}. Your ArquitecturaBase account is still active. If you used that organization's email to sign in, it may no longer work: review your sign-in methods in My account. | {{1}}: Delta Group | None |
| baja_cuenta_pedida / es | Pediste la baja de tu cuenta de ArquitecturaBase. Se elimina el {{1}}. Si no fuiste vos o te arrepentiste, ingresá antes de esa fecha para cancelarla. | {{1}}: 27/10/2026 | Ninguno |
| baja_cuenta_pedida / en | You requested deletion of your ArquitecturaBase account. It will be deleted on {{1}}. If this was not you or you changed your mind, sign in before that date to cancel it. | {{1}}: 10/27/2026 | None |
| baja_cuenta_plataforma / es | La plataforma inició la baja de tu cuenta de ArquitecturaBase y cerramos todas tus sesiones. Se elimina el {{1}}. Si creés que es un error, ingresá antes de esa fecha para cancelarla. | {{1}}: 27/10/2026 | Ninguno |
| baja_cuenta_plataforma / en | The platform started deletion of your ArquitecturaBase account and closed all your sessions. It will be deleted on {{1}}. If you believe this is a mistake, sign in before that date to cancel it. | {{1}}: 10/27/2026 | None |
| baja_cuenta_plataforma_suspendida / es | La plataforma inició la baja de tu cuenta de ArquitecturaBase. Se elimina el {{1}}. Tu cuenta está suspendida, así que no podés cancelar la baja. Si creés que es un error, escribí a {{2}} para pedir ayuda. | {{1}}: 27/10/2026; {{2}}: soporte@ejemplo.com | Ninguno |
| baja_cuenta_plataforma_suspendida / en | The platform started deletion of your ArquitecturaBase account. It will be deleted on {{1}}. Your account is suspended, so you cannot cancel deletion. If you believe this is a mistake, contact {{2}} for help. | {{1}}: 10/27/2026; {{2}}: support@example.com | None |
| baja_cuenta_cancelada / es | Cancelaste la baja de tu cuenta de ArquitecturaBase. Tu cuenta sigue activa. Si no fuiste vos, entrá a tu cuenta y revisá tus métodos de ingreso. Si no podés entrar, usá «Recuperá tu cuenta». | Sin variables | Ninguno |
| baja_cuenta_cancelada / en | You canceled deletion of your ArquitecturaBase account. Your account is still active. If this was not you, sign in and review your sign-in methods. If you cannot sign in, use “Recover your account”. | No variables | None |
| cuenta_eliminada / es | Tu cuenta de ArquitecturaBase fue eliminada, con sus métodos de ingreso y tu espacio personal. Este número ya no está asociado a ninguna cuenta. | Sin variables | Ninguno |
| cuenta_eliminada / en | Your ArquitecturaBase account was deleted, including its sign-in methods and your personal space. This number is no longer associated with any account. | No variables | None |
| recuperacion_recibida / es | Recibimos tu pedido para recuperar tu cuenta de ArquitecturaBase. Lo revisa una persona de la plataforma y te avisamos por acá. Si no pediste esto, ignorá este mensaje. | Sin variables | Ninguno |
| recuperacion_recibida / en | We received your request to recover your ArquitecturaBase account. A person from the platform will review it and we will notify you here. If you did not request this, ignore this message. | No variables | None |
| recuperacion_aprobada / es | Aprobamos tu pedido: ya podés entrar a ArquitecturaBase con este número, que quedó como tu método principal. | Sin variables de cuerpo | Ingresar |
| recuperacion_aprobada / en | We approved your request: you can now sign in to ArquitecturaBase with this number, which is your primary method. | No body variables | Sign in |
| recuperacion_rechazada / es | Revisamos tu pedido para recuperar tu cuenta de ArquitecturaBase y no pudimos confirmar que sea tuya. Podés hacer un pedido nuevo desde la pantalla de ingreso. | Sin variables | Ninguno |
| recuperacion_rechazada / en | We reviewed your request to recover your ArquitecturaBase account and could not confirm that it belongs to you. You can submit a new request from the sign-in page. | No variables | None |
| recuperacion_aviso_cuenta / es | Alguien pidió recuperar tu cuenta de ArquitecturaBase. Una persona de la plataforma revisa el pedido antes de cambiar algo. Si fuiste vos, no tenés que hacer nada. Si no fuiste vos, escribí a {{1}} para pedir ayuda. | {{1}}: soporte@ejemplo.com | Ninguno |
| recuperacion_aviso_cuenta / en | Someone requested recovery of your ArquitecturaBase account. A person from the platform will review the request before changing anything. If this was you, you do not need to do anything. If it was not you, contact {{1}} for help. | {{1}}: support@example.com | None |

Authentication utiliza los componentes oficiales de Meta: recomendación de seguridad activada, footer de vencimiento **10 minutos** y botón OTP de tipo Copiar código. La consulta real del 2026-10-08 confirmó `codigo_ingreso` **APPROVED / AUTHENTICATION**, es/en, con esos componentes; el texto generado por Meta puede diferir del orden del ejemplo visual y no se modifica la plantilla activa. Su único parámetro se envía igual en body y botón; el código nunca se copia a la tabla de mensajes. La vigencia del código configurada debe corresponder al footer aprobado; un cambio de vigencia exige revisar esa plantilla.

Los únicos botones URL de Utility son los dos de la tabla: invitación y recuperación aprobada, índice 0. Al cargar se fija el origen público HTTPS de la instalación y `{{1}}` como sufijo dinámico de URL; por ejemplo `https://app.example.com/{{1}}`. Muestras de sufijo: `invitacion/ejemplo-de-carga` y `login`. En ejecución se usa **el ActionUrl del DTO**, separando su origen autorizado del resto del enlace; no se fabrica otra identidad, token ni ruta. La base aprobada tiene que corresponder al origen público configurado. Los tres avisos de organización carecen de ActionUrl y de botón.

Las variables de fecha y hora salen de `DisplayFormatter` con cultura y zona explícitas; el cambio de método es localizado y el método está enmascarado. Para baja suspendida y aviso de recuperación, si soporte no está configurado la variable usa la instrucción localizada de contacto con soporte de la plataforma, sin inventar una dirección. No usar el método nuevo en el aviso de recuperación a la cuenta anterior.

**Ajuste operativo aprobado el 2026-10-09:** Meta rechazó crear baja suspendida y aviso de recuperación en es/en porque sus cuerpos terminaban en un parámetro, aun seguido de punto (`100/2388299`, Leading or Trailing Params Not Allowed). El cierre «para pedir ayuda.» / «for help.» de la tabla y los dos ejemplos WhatsApp de Mensajes fue aprobado por el usuario; conserva variables, acción y categoría. No cambia los correos ni autoriza convertir una plantilla a Marketing.

**Estado operativo (Graph del 2026-10-11, 13:14 UTC; revisión UI histórica del 10/10 a las 17:38 UTC):** las **32 variantes están creadas** con autorización del usuario: dos Authentication aprobadas (`codigo_ingreso`, es/en), **29 APPROVED / UTILITY** y una excepción. `aviso_metodo_ingreso/en` pasó de IN_APPEAL a APPROVED / UTILITY; `pagina_publica_moderada/en` e `invitacion_organizacion/en` conservan APPROVED / UTILITY. Estas aprobaciones no acreditan nuevas entregas. Los botones de `invitacion_organizacion` y `recuperacion_aprobada`, es/en, ya tienen el origen público HTTPS del túnel de prueba. Recuperación aprobada tiene ambas variantes APPROVED / UTILITY; su aviso con botón y el ingreso posterior se comprobaron en el circuito real.

| Variante pendiente | Estado / categoría | Revisión / pendiente |
|---|---|---|
| `invitacion_organizacion/es` | APPROVED / MARKETING | Revisión de categoría terminada: Rejected, en «Sin cambios». La plantilla sigue aprobada como Marketing. |

Las revisiones de categoría y la apelación se solicitaron desde WhatsApp Manager el 09/10. La UI posterior confirmó el rechazo de la revisión de categoría española; la nueva consulta Graph confirma la aprobación Utility del aviso inglés y deja sólo la invitación española sin habilitar para E8. El usuario autorizó que la invitación española quede pendiente documentado sin bloquear corte ni puerta: el circuito real usa invitación inglesa y avisos Utility aprobados, conservando OTP Authentication. El contacto con soporte quedó pendiente porque el usuario decidió no aceptar las condiciones beta/IA/privacidad del formulario; no se abrió un caso ni se envió el borrador completo. Al presentar el cierre de E8 se propondrá español más literal del inglés aprobado para una plantilla nueva; no se crea ni se cambia el catálogo o los ejemplos hasta su autorización posterior. No duplicar apelaciones ni reenviar revisiones pendientes. Meta reclasificó variantes aun solicitando Utility con `allow_category_change=false`: comprobar estado **y** categoría, no sólo APPROVED. No usar Marketing como solución. `invitacion_acceso` es Marketing y pertenece a ArquitecturaBase: no se modifica ni se reutiliza en E8. El origen aprobado de los botones corresponde a este entorno de prueba; producción requiere su propio origen y aprobación. Los estados pueden cambiar: volver a consultarlos antes de cada recorrido Meta. El [informe de coordinación](../features/whatsapp.md) distingue las entregas reales acreditadas de las pendientes. La suite de payloads y el transporte controlado no certifican aprobación ni `delivered`.

## Cómo crearlas y comprobarlas

**C0 (12/10/2026):** se crearon con autorización las variantes `invitacion_organizacion_literal/es` y `/en`, solicitando Utility y sin cambio de categoría permitido. Español: «Hola. {{1}} te invitó a unirte a {{2}} en ArquitecturaBase. La invitación vence el {{3}}. Podés revisarla con el botón.», botón «Ver la invitación». Inglés y componentes URL copiados de `invitacion_organizacion/en` aprobado. La consulta posterior devolvió **APPROVED / MARKETING en ambas**. No se usan ni se cambia `WhatsApp:Templates:Invitation` hasta verificar APPROVED / UTILITY en las dos; la creación sola no completa C0. Las muestras son ficticias y no se enviaron invitaciones a personas.

1. En [Meta Business Suite](https://business.facebook.com/), WhatsApp Manager → Administrar plantillas, seleccionar la WABA correcta: desarrollo comparte la de ArquitecturaBase; producción tiene la propia. Ver [configuración §4](configuracion.md#4-lo-que-tenés-que-hacer-vos-una-sola-vez-fuera-del-código).
2. Crear el nombre snake_case de la tabla, Authentication solo para código (texto/botón Copiar código de Meta); Utility para los demás. Nunca Marketing.
3. Cargar es y en por separado, con cuerpo, orden de variables y muestras del paquete T03. Enlaces existentes mediante botón URL con base pública configurada y sufijo variable; nunca inventar ActionUrl donde no existe.
4. Enviar a revisión **antes de programar el envío**, para que la aprobación avance en paralelo. Comprobar que ambas variantes estén activas en la WABA usada. Dibujada, configurada, aprobada y delivered son estados distintos.
5. El módulo E8 ya define las 16 claves y sus nombres predeterminados en `WhatsAppTemplateOptions` (`WhatsAppOptions.cs`), y el orden de parámetros en `WhatsAppTemplateCatalog.cs`, bajo `Infrastructure/Modules/WhatsApp/`. Una instalación puede sobrescribir el nombre con `WhatsApp:Templates:<Clave>` no secreta. Esto configura el envío; no crea ni aprueba la plantilla en Meta.
6. Tests del catálogo verifican 16 nombres, ambas culturas, variante seleccionada y orden del payload. Una prueba del flujo real al celular de prueba debe recibir el mensaje y su evento delivered por el túnel, según la puerta E8.

## Reglas

- Nunca mensaje libre fuera de las 24 horas ni usar delivered para iniciar esa ventana.
- Envío siempre por outbox cifrado y llamadas externas fuera de UoW; nunca OTP o enlace en logs/WhatsAppMessage.
- No modificar una plantilla activa sin revisar el impacto de la nueva aprobación; para cambio incompatible usar nombre versionado y mantener ambas mientras se migra.
- Desarrollo y producción usan el mismo paquete funcional, pero sus aprobaciones son independientes.
- Mensaje nuevo de un producto: agregar inventario/etapa y preparar su plantilla antes del envío.
