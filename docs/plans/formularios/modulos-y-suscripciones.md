# Módulos y suscripciones (spec)

**Estado:** pendiente · spec de la etapa «Módulos y suscripciones», que va después de la E25 y programa la [ADR 0069](../../decisions/0069-modulos-y-suscripciones.md), aprobada por el usuario el 2026-11-23. Da por cerradas las «Costuras de módulos» (0069 §4 y §8), cuya spec va aparte ([costuras de módulos](costuras-de-modulos.md)). Sus trece tableros están aprobados desde el 2026-11-24, en su lienzo aparte (v4), que abre la tarjeta «Contrata módulos» del índice del lienzo principal (v183). Se contrasta con el código real con `/plan-etapa` cuando cierre la E25.

**Partes.** Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| Contrato y Datos | [datos y contrato](modulos-y-suscripciones-parte-datos-y-contrato.md) |
| Pasos y Qué verificar al arrancar | [pasos y verificar](modulos-y-suscripciones-parte-pasos-y-verificar.md) |

## Qué leer ✓

1. **Lo anterior:**
   - la spec de las Costuras de módulos: el permiso de uso con una fila por origen, el resolvedor de topes y lo que cada módulo declara de sus datos;
   - de la [segunda parte de la base](base-de-la-12-segunda-parte.md), la Configuración de Plataforma (`Config-Plat`, B18 y F13), donde se suman dos zonas;
   - de la [E18](18-fotos-y-documentos.md) y la [E25](25-documentos.md), cómo un módulo declara un tope y lo lee por el resolvedor;
   - de [Acciones del asistente](acciones-del-asistente.md), la fuente de un área y la batería genérica.
2. **Decisiones:** las ADR [0069](../../decisions/0069-modulos-y-suscripciones.md) (manda), [0028](../../decisions/README.md) (el módulo no es un permiso), [0060](../../decisions/0060-dos-versiones-conviven.md), [0061](../../decisions/0061-nadie-da-lo-que-no-tiene.md), [0062](../../decisions/0062-retencion-de-los-datos-del-ingreso.md) (cada tabla nombra quién la purga), [0066](../../decisions/0066-lo-que-no-se-sabe-si-salio.md) («Sin confirmar») y [0068](../../decisions/0068-acciones-del-asistente.md); de la [hoja de ruta de formularios](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), la 270 (reglas en pantalla), la 261 a la 264 (filas, piezas, columnas y orden) y la 269.
3. **Fichas:**

| Ficha | Por qué |
| --- | --- |
| [modulos-habilitados](../../rules/modulos-habilitados.md) | el gate responde 404 y ningún módulo pregunta por precios ni planes |
| [multitenancy](../../rules/multitenancy.md) | las tablas globales con `TenantId`, el tenant del claim, `PlatformActionGuard` y el 404 de lo ajeno |
| [permisos](../../rules/permisos.md) | los permisos nuevos, sus textos y el seed |
| [idempotencia](../../rules/idempotencia.md) | pedir, probar, suscribirse, exportar, precios, aprobar y rechazar |
| [concurrencia](../../rules/concurrencia.md) | la versión y el 409, y cada trabajo de fondo una vez entre copias |
| [datos-personales](../../rules/datos-personales.md) | qué tabla guarda qué y quién lo purga; los comprobantes fiscales nunca se borran |
| [cache](../../rules/cache.md) | la etiqueta de módulos avanza con el guardado; nadie invalida a mano |
| [modulos](../../rules/modulos.md) | la CSP con `IContentSecurityPolicySource`, el webhook y las plantillas |
| [api-http](../../rules/api-http.md) | contratos, `cardToken` con su `ToString()` y los ejemplos de una corrida real |
| [cuotas](../../rules/cuotas.md) | el webhook cuenta en una cuota; los doce perfiles son cerrados |
| [auditoria](../../rules/auditoria.md) | `SecurityEvent` con motivo en Plataforma e `IAuditLog` en la organización |
| [numeros-y-moneda](../../rules/numeros-y-moneda.md) y [fechas-y-zonas](../../rules/fechas-y-zonas.md) | `Money` en ARS, fechas civiles y `DisplayFormatter` en los avisos |
| [configuración, regla 270](../../operations/configuracion.md#reglas-de-negocio-en-pantalla-270) y [plantillas de WhatsApp](../../operations/whatsapp-plantillas.md) | los plazos en pantalla y las plantillas nuevas |

4. **Código para copiar:**
   - el permiso de uso de Plataforma, de `Api/Controllers/Platform/PlatformFeaturesController.cs`, `Application/Services/Platform/PlatformFeatureService.cs` y `PlatformFeatureWriter.cs`;
   - un pedido que revisa Plataforma, de `Domain/Platform/AccountRecoveryRequest.cs`, `PlatformRecoveryService.cs` y `PlatformRecoveriesController.cs`;
   - una versión que no se edita, de `Domain/Legal/LegalDocument.cs` y `PlatformLegalService.cs`;
   - las claves cifradas en pantalla, de `Domain/Assistant/AssistantPlatformSettings.cs`, `AssistantSettingsService.cs` e `IAssistantKeyProtector.cs`;
   - el webhook y su bandeja, de `Api/Modules/WhatsApp/Controllers/WhatsAppWebhookController.cs`, `Domain/WhatsApp/WhatsAppMessage.cs` (el lease) e `Infrastructure/Modules/WhatsApp/Inbound/WhatsAppInboundProcessor.cs`;
   - la reserva por fila, de `Infrastructure/Messaging/OutboxDispatchStore.cs` (`FOR UPDATE SKIP LOCKED`), y los trabajos periódicos, de `InvitationExpiryWorker.cs` y `TenantJobRunner.cs`;
   - los avisos, de `Application/Models/Notifications/AccountNotice.cs` y `PlatformTenantNotifier.cs`; «Para resolver», de `OrganizationDomainPendingItems.cs`;
   - la CSP, de `Api/Modules/WhatsApp/Hosting/MetaContentSecurityPolicySource.cs`;
   - en el front, `src/areas/business/settings/pages/SettingsPage.tsx`, `src/areas/platform/recoveries/*`, `src/areas/platform/legal/pages/*`, `src/areas/platform/tenants/tabs/OrganizationModulesTab.tsx`, `src/areas/platform/settings/components/RateLimitsSection.tsx`, `src/modules/assistant/settings/PlatformAssistantSettingsPage.tsx` y `src/modules/whatsapp/numbers/signup/embeddedSignup.ts` (cargar un SDK de afuera).
5. **Referencia:** la documentación de Mercado Pago (Suscripciones, Webhooks y MercadoPago.js, en `mercadopago.com.ar/developers`) y el Contexto de la 0069, que resume la investigación.

## Qué se construye

**La regla** (0069 §1 y §2): hay un solo permiso de uso por organización y módulo, con una fila por origen (`Manual`, `Trial`, `Approved` y `Subscription`), y el módulo está prendido si alguna fila está vigente. La facturación lo alimenta desde afuera. `[FeatureGate]`, el filtro, los trabajos de fondo y `/api/me` no cambian, y ningún módulo pregunta por precios ni suscripciones.

### En la organización: «Módulos»

Es una página nueva, Organización › Módulos (`/org/modulos`), con el permiso nuevo `modules.manage`. Muestra cada módulo que Plataforma ofrece, con su nombre y su descripción (de los textos del módulo), su estado, su precio por mes y, si hay, el precio que viene, con su fecha.

**Los estados:** «Apagado», «En prueba hasta el {fecha}», «Pedido enviado», «Prendido por Plataforma» (con su vencimiento, si tiene), «Suscripto» (con el próximo cobro), «Cobro rechazado» («Cambiá la tarjeta antes del {fecha}»), «Baja pedida» («Funciona hasta el {fecha}») y «Suspendido» («Tus datos se guardan hasta el {fecha}»).

**Lo que se puede hacer** (lo dice el servidor en cada módulo):
- **Pedir:** un diálogo de confirmación, con un pedido pendiente por módulo, que se puede cancelar mientras espera. Plataforma lo aprueba (prende `Approved`, sin cobrar y con un vencimiento opcional) o lo rechaza con un motivo, que la organización ve.
- **Probar gratis {n} días:** una confirmación. Va una sola vez por organización y módulo (el registro de pruebas) y sin tarjeta, y prende `Trial` por los días de la configuración. Si termina sin suscripción, llega el aviso y el módulo se apaga.
- **Suscribirme:** una página con el precio, el primer cobro (hoy, o el fin de la prueba si está en prueba), lo incluido (los topes del plan), el formulario de tarjeta de MercadoPago.js y el consentimiento sin tildar: «Acepto que se cobre {monto} por mes con esta tarjeta hasta que dé de baja el módulo. La baja rige al fin del período pagado, sin reembolso». La tarjeta la toma MercadoPago.js, y al servidor llega solo el token de un uso.
  - Mientras Mercado Pago no confirma, la pantalla dice «Estamos confirmando el pago» (0066).
  - Si rechaza la tarjeta, la pantalla da el motivo, de una lista cerrada, y pide otra.
- **Cambiar tarjeta:** con la suscripción viva. En «Cobro rechazado» es lo primero que se ofrece.
- **Dar de baja:** una confirmación que dice hasta cuándo funciona, que no hay reembolso y cuántos días se guardan los datos. La baja se hace en Mercado Pago en el acto, así no sale otro cobro, y el módulo sigue hasta el fin del período. No se deshace: al terminar el período se puede volver a suscribir.
- **Exportar datos:** solo con el módulo apagado y los datos guardados. Arma el archivo en segundo plano, avisa al terminar y se descarga desde la pantalla. Hay una exportación por módulo: la nueva reemplaza a la anterior, y se borra con los datos.

**Para resolver:** un cobro rechazado y una suspensión le aparecen a quien tiene `modules.manage` (`IPendingItemSource`).

**Cada acción** deja su entrada en la auditoría de la organización con `IAuditLog`, porque las tablas son globales.

### En Plataforma

- **Catálogo** (`/plataforma/modulos`): cada módulo Business del catálogo de features, con si se ofrece, si se puede pedir, probar y suscribirse, el precio vigente, el que viene y cuántas suscripciones vivas tiene. Cambiar esas marcas pide motivo.
- **Precios:** en `Money` ARS por mes. Un precio nunca se edita: «Nuevo precio» pide el monto, desde qué día rige (como pronto, los días de aviso), los topes incluidos y el motivo.
  - **Los topes incluidos** son el valor del plan: solo las claves que declara el módulo, entre su mínimo y el máximo de la plataforma.
  - **Al guardar,** avisa a las organizaciones con una suscripción viva. Desde ese día, cada suscripción cobra el precio nuevo en su próximo cobro, sin prorrateo.
- **Pedidos** (`/plataforma/modulos/pedidos`): los pendientes primero, con filtros por estado. Aprobar pide motivo y acepta un vencimiento; rechazar pide motivo.
- **Suscripciones** (`/plataforma/modulos/suscripciones`): organización, módulo, estado, precio y próximo cobro. La ficha trae los cobros: monto, estado, período, id de Mercado Pago y «Factura pendiente» hasta ARCA. Un cobro repetido del mismo período aparece marcado, para devolverlo a mano en Mercado Pago.
- **La ficha de una organización, pestaña «Módulos»** (se redibuja): cada módulo con sus filas por origen y su estado, y la zona «Topes». Cada tope muestra el máximo de la plataforma, el del plan, el de esta organización (con motivo y vencimiento) y el que rige. Prender y apagar a mano sigue igual (`Manual`).
- **Configuración** (`Config-Plat`, `platform.settings.manage`), con dos zonas nuevas:
  - «Módulos y cobros», con los plazos de la tabla de abajo;
  - «Mercado Pago», con la clave pública, el token de acceso y el secreto del webhook (con «Reemplazar» y «Quitar», y solo sus últimos cuatro caracteres a la vista), el modo «Prueba» o «Real», y la URL del webhook para pegar en Mercado Pago.

### Los plazos (270)

Se guardan en la configuración de Plataforma, con motivo, versión y 409, y se leen de la base en cada uso. Un cambio rige para lo que empieza después: lo que ya se le prometió a una organización se cumple.

| Valor | De entrada | Mínimo | Máximo |
| --- | --- | --- | --- |
| Días de prueba gratis | 14 | 1 | 90 |
| Días de gracia después de un cobro rechazado | 10 | 1 | 30 |
| Días que se guardan los datos de un módulo apagado | 90 | 30 | 365 |
| Días de aviso antes de borrar esos datos | 7 | 1 | 30 |
| Días de aviso de un precio nuevo | 30 | 1 | 90 |

### Los estados (0069 §6)

Cada cambio de una suscripción escribe su fila `Subscription` en la misma transacción. Las columnas `Enabled` y `ExpiresAtUtc` siempre dicen lo mismo que el estado, así la versión anterior, que lee solo esas dos, ve lo mismo (0060).

| La suscripción pasa a | La fila `Subscription` queda |
| --- | --- |
| `Pending`: creada, sin confirmar | como estaba: todavía no prende |
| `Trialing`: autorizada, con el primer cobro al fin de la prueba | `Active` |
| `Active`: cobro aprobado | `Active`; con la baja pedida, vence al fin del período |
| `PastDue`: cobro rechazado | `Grace` hasta el primer rechazo más los días de gracia |
| `Paused` | `Suspended` |
| `Canceled` | `Suspended`: en el acto si cancela Mercado Pago, o al fin del período si se dio de baja |

- **Los trabajos de fondo** pasan `Grace` a `Suspended` cuando vence la gracia, y `Suspended` a `Ended` cuando se borran los datos.
- **Un cobro aprobado** vuelve a `Active` desde `PastDue` o desde la suspensión por gracia.
- **Un pedido aprobado** escribe `Approved`, y una prueba, `Trial`.
- **`Canceled` es terminal:** volver a suscribirse crea otra suscripción, que toma la misma fila.

### Apagar, exportar y borrar (0069 §5)

- **Un módulo está apagado** cuando ninguna fila está vigente.
- **Mientras sus datos se guardan:** desde que el trabajo de fondo lo ve apagado (a lo sumo una vuelta después) y durante los días de la configuración, el módulo responde 404, la organización lo exporta y, si se prende de nuevo, no se pierde nada.
- **Antes de borrar,** sale el aviso con los días de aviso. El borrado corre después de los dos plazos, por lotes y una vez entre copias, y deja las filas en `Ended`.
- **Si se prende antes,** el borrado se cancela.
- **Nunca se borran** los comprobantes fiscales de ARCA ([datos-personales](../../rules/datos-personales.md)).

**Al cerrar una organización,** sus suscripciones se dan de baja en Mercado Pago en el acto, sin reembolso. Suspenderla no las toca.

### Mercado Pago (0069 §3)

- **El proveedor** queda detrás del puerto `IPaymentProvider`, y Mercado Pago es su primer adaptador. Sus claves se cargan en la pantalla y se guardan cifradas.
- **La suscripción:** cada precio tiene su plan de Mercado Pago (`preapproval_plan`), y la suscripción es un `preapproval` de ese plan con el token de la tarjeta, `authorized`, y la fecha del primer cobro.
- **El webhook:** `POST /webhooks/mercadopago`, anónimo, valida la firma con el SDK, guarda el aviso en la bandeja con clave única por proveedor y evento, y responde 200 enseguida. Un trabajo de fondo, una vez entre copias, le pide el recurso a la API y concilia, aunque el aviso llegue repetido o desordenado.
- **La conciliación periódica** revisa las suscripciones vivas, las `Pending` sin respuesta (las busca por la referencia externa), los planes de los precios nuevos, las bajas que falta mandar, los precios que empiezan y las suscripciones de las organizaciones cerradas.
- **El cobro con dinero real,** apagado: el modo «Real» no se guarda mientras la plataforma no pueda emitir facturas de ARCA (`Billing.Connection.LiveRequiresInvoicing`). En Production, «Suscribirme» aparece solo en modo «Real», y en los demás ambientes, también en «Prueba».

**La factura:** cada cobro aprobado queda con «Factura pendiente», y la emite la etapa de ARCA que emite comprobantes. **La hoja de ruta de ARCA tiene que sumar «la plataforma les factura a las organizaciones»**, porque hoy cubre solo que las organizaciones les facturen a sus clientes.

### Los avisos

Van a cada persona activa con `modules.manage` en la organización y con el correo confirmado, en su idioma, con montos y fechas por `DisplayFormatter` y en la zona de la organización. Salen por la cola en la misma transacción que el cambio, como avisos de `AccountNotice`. Por WhatsApp salen además, solo si su plantilla está configurada; si no, solo por correo ([plantillas](../../operations/whatsapp-plantillas.md)). Las seis plantillas son Utility, en es y en, y se mandan a Meta al empezar la etapa.

| Aviso | Cuándo | Plantilla |
| --- | --- | --- |
| `ModuleRequestDecided` | Plataforma aprueba o rechaza un pedido; lleva el motivo | `modulo_pedido_resuelto` |
| `ModuleTrialEnded` | la prueba termina sin suscripción | `modulo_prueba_terminada` |
| `ModuleChargeRejected` | un cobro rechazado abre la gracia; lleva su fin | `modulo_cobro_rechazado` |
| `ModuleSuspended` | vence la gracia, termina el período dado de baja o cancela Mercado Pago; lleva el día del borrado | `modulo_suspendido` |
| `ModuleDataPurgeScheduled` | faltan los días de aviso para borrar | `modulo_datos_por_borrar` |
| `ModulePriceChanging` | Plataforma agrega un precio; lleva el monto y desde cuándo | `modulo_precio_nuevo` |
| `ModuleSubscriptionCanceled` | la organización da de baja; lleva el fin | solo correo |
| `ModuleExportReady` | la exportación está lista; solo a quien la pidió | solo correo |

## Pantallas ✓

**Aprobados** por el usuario el 2026-11-24, en su lienzo aparte (v4, página «Módulos y suscripciones», con su índice `Indice-modulos`), porque el principal no tiene lugar: lo abre la tarjeta «Contrata módulos» del índice del lienzo v183, y la guía los recorre en el capítulo del mismo nombre. Cada uno lleva su teléfono a 390 px (`M-`), salvo `Mensajes-Modulos`, y estos estados: cargando, vacío, error con «Reintentar», sin permiso, un módulo suspendido y, en Plataforma, una organización suspendida o cerrada. Valen las de toda la app (160 a 164, 173 a 175, 180, 261 a 264 y 269): las acciones de una fila van en su ⋮. Los tableros fijan además que «Suscribirme» también va en el ⋮, que el detalle de un estado (hasta cuándo, el próximo cobro o el motivo de un rechazo) va en su (i), y que el pedido de una organización cerrada muestra Aprobar y Rechazar apagados, con su motivo. Los precios que muestran son de ejemplo.

| Tablero | Qué muestra |
| --- | --- |
| `MOD-Modulos` | la página de la organización con cada estado de un módulo; vacía («Todavía no hay módulos para sumar»), cargando, error, sin permiso; arriba, el aviso de un cobro rechazado, y un módulo suspendido con «Exportar datos» |
| `MOD-Suscribir` | el precio, el primer cobro, lo incluido, el formulario de MercadoPago.js y el consentimiento; «Estamos confirmando el pago», la tarjeta rechazada con su motivo, el precio que cambió y Mercado Pago no disponible; y «Cambiar tarjeta» |
| `MOD-Pedir` | el diálogo de pedir, «Pedido enviado» con «Cancelar pedido», el rechazo con su motivo y el diálogo con «Propuesta del asistente» |
| `MOD-Confirmar` | las confirmaciones de probar (los días, una sola vez) y de dar de baja (hasta cuándo, sin reembolso y los días de los datos) |
| `MOD-Exportar` | el diálogo de exportar: preparando, lista para descargar, falló y los datos ya borrados |
| `Plat-MOD-Catalogo` | el catálogo con sus marcas, el precio vigente, el que viene y las suscripciones vivas |
| `Plat-MOD-Modulo` | la ficha de un módulo: las marcas con motivo y los precios (vigente, el que viene y los anteriores) |
| `Plat-MOD-Precio` | «Nuevo precio»: monto, desde, topes incluidos y motivo, con el aviso de que avisa a las organizaciones y no se deshace |
| `Plat-MOD-Pedidos` | la bandeja de pedidos con filtros y el diálogo de aprobar (vencimiento opcional) o rechazar, con motivo |
| `Plat-MOD-Suscripciones` | la lista y la ficha con sus cobros, el cobro repetido y «Factura pendiente» |
| `Plat-Org-Modulos` | la pestaña «Módulos» de la ficha de una organización, redibujada: las filas por origen con su estado y la zona «Topes» con el valor de la organización |
| `Config-Plat-Modulos` | las zonas «Módulos y cobros» (los cinco plazos, cada uno con su (i), su mínimo y su máximo) y «Mercado Pago» (claves, modo y URL del webhook) |
| `Mensajes-Modulos` | los ocho correos de la tabla de avisos |

## Acciones del asistente ✓

**Una sola acción:**

| Acción | Qué prepara | Dónde se confirma | Ruta que guarda |
| --- | --- | --- | --- |
| `modules.request@1` · Pedir un módulo | el módulo (`Choice` de los que esa organización puede pedir) | el diálogo de pedir (`Dialog`), sobre «Módulos» | `POST api/organization/modules/{key}/requests` |

- **Requisito:** `modules.manage`, el de la ruta. No sale del sistema.
- **La fuente** es `Application/Services/Billing/ModuleActionSource.cs`. Lista con el reader del catálogo solo lo ofrecido que se puede pedir, sin un pedido pendiente y apagado.
- **Cuándo se ofrece:** queda registrada y sin ofrecer hasta calificarla (0068).

**Lo que no es una acción:**
- **probar:** es un botón sin datos que la IA pueda preparar, y se usa una sola vez;
- **suscribirse y cambiar la tarjeta:** piden la tarjeta en MercadoPago.js, y el consentimiento nunca se propone (0068 §4);
- **dar de baja:** cambia el contrato de pago en Mercado Pago, como pagar, así que queda a mano;
- **exportar:** no crea, edita ni elimina un registro, sino que arma un archivo;
- **las pantallas de Plataforma:** Plataforma todavía no tiene acciones.

## Decisiones tomadas

- **Las de la ADR 0069** (§1 a §8), aprobadas por el usuario el 2026-11-23.
- **Las de esta spec,** técnicas o menores. El usuario aprobó el 2026-11-23 las seis de producto: los dos plazos, el pedido sin mensaje, que la baja no se deshace, cerrar y suspender, el motivo del rechazo y que la IA solo pida:
  - **los permisos:**
    - `modules.manage` para ver y operar «Módulos». De entrada lo tiene solo el rol de sistema TenantAdmin (el Dueño);
    - `platform.modules.read` y `platform.modules.manage`, este último con la prueba reciente;
    - el tope de una organización, con `platform.tenants.manage`, como prender a mano;
    - los plazos y las claves, con `platform.settings.manage`;
  - **dos plazos más en pantalla** (270): el aviso antes de borrar (7 días) y el aviso de un precio nuevo (30 días);
  - **dónde viven los datos:** pedidos, pruebas, suscripciones, cobros, bandeja y topes van en `platform`, como `TenantFeatures`, y la exportación va en `tenant`;
  - **el cobro es del núcleo,** no un módulo quitable. Sin la conexión de Mercado Pago, «Suscribirme» no aparece y el webhook responde 404;
  - **el pedido no lleva mensaje,** así no guarda texto libre;
  - **la baja no se deshace,** y hay **una sola exportación por módulo**;
  - **cerrar una organización** da de baja sus suscripciones; **suspenderla,** no;
  - **el motivo de un rechazo** lo ve la organización;
  - **un cobro repetido** se devuelve a mano en Mercado Pago;
  - **del asistente,** solo pedir.

## Fuera de esta etapa

- **Lo que la 0069 deja para más adelante:** los paquetes de varios módulos, el prorrateo, cobrar el excedente de la IA, otros medios de pago y otro proveedor.
- **La factura de ARCA de cada cobro:** va con la etapa de ARCA que emite comprobantes.
- **Retirar un precio que todavía no rige, deshacer una baja y devolver un cobro desde la pantalla.**
- **Que la organización vea su historial de cobros:** Mercado Pago le manda el comprobante de cada uno.
- **Un plazo de datos distinto por módulo,** salvo que lo traiga la costura.

## Cómo se cierra ✓

La puerta de la etapa, con la lista de control de `/puerta`, suma lo propio:
- **la verificación de la 0069:**
  - la bandeja procesa una sola vez un aviso repetido y uno desordenado;
  - la firma inválida se rechaza;
  - cada estado de la suscripción escribe su fila;
  - la gracia, la suspensión y el borrado corren una vez entre copias, con dos hosts;
  - una organización no ve los pedidos, las suscripciones ni las exportaciones de otra;
- **la revisión adversarial** de cada paso marcado en [Pasos](modulos-y-suscripciones-parte-pasos-y-verificar.md), antes de su commit;
- **el E2E,** con un proveedor falso: pedir y aprobar, probar, suscribirse, el cobro rechazado con su gracia, cambiar la tarjeta, dar de baja, y exportar con el módulo apagado;
- **la prueba con Mercado Pago,** en su entorno de prueba y con el visto bueno del usuario, con usuarios de prueba de Mercado Pago, sin una tarjeta real y con las claves que pega él en `Config-Plat-Modulos`. Recorre suscribirse, un cobro aprobado, uno rechazado, cambiar la tarjeta y dar de baja. Se le dan los pasos clic por clic, con la ruta del menú;
- **las seis plantillas,** aprobadas en Meta, con un envío al celular de prueba;
- **la galería** de los tableros nuevos, lado a lado, y **el recorrido**.

## Requisitos previos

- La E25 y las Costuras de módulos, cerradas.
- Los tableros de «Pantallas», aprobados el 2026-11-24.
- Una cuenta de Mercado Pago de la plataforma con su entorno de prueba, que abre el usuario, y una URL pública para el webhook (el túnel, en desarrollo).
- Las seis plantillas, mandadas a Meta al empezar.
