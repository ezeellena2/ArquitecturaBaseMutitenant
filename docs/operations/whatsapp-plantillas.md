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

**Todavía falta:** el paquete definitivo es/en de los trece nombres originales (variables numeradas, muestras, footer y botones según sus DTO) se prepara en [E8 T03](../plans/2026-10-07-etapa-8-whatsapp.md#t03--plantillas-cloud-api-y-outbox), antes de programar cada envío. Los ejemplos españoles de Mensajes no sustituyen ese paquete ni aprobación/entrega Meta.

## Cómo crearlas y comprobarlas

1. En [Meta Business Suite](https://business.facebook.com/), WhatsApp Manager → Administrar plantillas, seleccionar la WABA correcta: desarrollo comparte la de ArquitecturaBase; producción tiene la propia. Ver [configuración §4](configuracion.md#4-lo-que-tenés-que-hacer-vos-una-sola-vez-fuera-del-código).
2. Crear el nombre snake_case de la tabla, Authentication solo para código (texto/botón Copiar código de Meta); Utility para los demás. Nunca Marketing.
3. Cargar es y en por separado, con cuerpo, orden de variables y muestras del paquete T03. Enlaces existentes mediante botón URL con base pública configurada y sufijo variable; nunca inventar ActionUrl donde no existe.
4. Enviar a revisión **antes de programar el envío**, para que la aprobación avance en paralelo. Comprobar que ambas variantes estén activas en la WABA usada. Dibujada, configurada, aprobada y delivered son estados distintos.
5. En E8 registrar `WhatsApp:Templates:<Clave>` no secreta y catálogo/orden del módulo, como LoginCode/Invitation de ArquitecturaBase. Son claves a crear en esa etapa, no configuración ya entregada en este repo.
6. Tests del catálogo verifican 16 nombres, ambas culturas, variante seleccionada y orden del payload. Una prueba del flujo real al celular de prueba debe recibir el mensaje y su evento delivered por el túnel, según la puerta E8.

## Reglas

- Nunca mensaje libre fuera de las 24 horas ni usar delivered para iniciar esa ventana.
- Envío siempre por outbox cifrado y llamadas externas fuera de UoW; nunca OTP o enlace en logs/WhatsAppMessage.
- No modificar una plantilla activa sin revisar el impacto de la nueva aprobación; para cambio incompatible usar nombre versionado y mantener ambas mientras se migra.
- Desarrollo y producción usan el mismo paquete funcional, pero sus aprobaciones son independientes.
- Mensaje nuevo de un producto: agregar inventario/etapa y preparar su plantilla antes del envío.
