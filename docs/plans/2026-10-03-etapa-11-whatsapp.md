# Etapa 11: WhatsApp de atención — plan ejecutable

> **Repaso del 2026-10-11.** Este plan se reescribe en formato corto después de la 8b (`AGENTS.md`, «Cierre liviano»). Mientras tanto, rigen estos puntos:
> - **Decisiones nuevas:** están en la precisión del 2026-10-11 de la [ADR 0038](../decisions/0038-whatsapp-de-atencion.md). El número de la plataforma se conecta desde la pantalla; «Cargar teléfonos» no revela cuentas; la invitación en español se rehace al cerrar la 8.
> - **Correcciones al reescribirlo:**
>   - la fuente visual es el lienzo v119, no la v69 ni la v84;
>   - la [0043](../decisions/0043-las-pantallas-dicen-si-hay-cuenta.md) reemplazó a la 0041;
>   - el bot de la plataforma manda el enlace para entrar o registrarse, y las plantillas del sistema son las 16 de la Etapa 8 ([0049](../decisions/0049-precisiones-whatsapp-y-asistente.md));
>   - la 11 conecta la fuente productiva de «Mis mensajes con empresas» del asistente personal (`IConsumerInteractionSource`, 0049);
>   - las tablas globales de la plataforma siguen la [0050](../decisions/0050-auditoria-global-de-modulos.md);
>   - antes de programar hay que verificar con Meta los precios (las 1.000 respuestas gratis por mes) y los requisitos de Tech Provider.
> - **Meta** (según su documentación, consultada el 2026-10-11):
>   - **Tech Provider se tramita por producto, al lanzarlo.** Cada producto necesita su app con su marca, que es el nombre que ve la organización en la ventana de Meta; su empresa verificada, y la revisión de Meta de `whatsapp_business_messaging` y `whatsapp_business_management`. La plantilla no lo tramita.
>   - **La 11 se desarrolla y se prueba con el acceso estándar.** Hace falta una app en modo desarrollo; el alta la hace alguien con rol en la app o una cuenta sandbox de Meta, que dura 30 días. «Servicios Ya» está publicada, y una app publicada solo ofrece en el alta los permisos aprobados por revisión. El plan elige qué app de desarrollo se usa.
>   - **Se usa Embedded Signup v4:** la v2 se retira el 15/10/2026.
>   - **Los dominios del alta van con HTTPS,** cargados en «Allowed domains» y «Valid OAuth redirect URIs» de la app, incluidos los de desarrollo. Hay que suscribirse al webhook `account_update`.
>   - **Hay un límite de organizaciones nuevas:** sin la empresa verificada ni la revisión aprobada, son 10 cada 7 días; con las dos, 200.

> Plan de implementación para esta etapa. Seguir el orden, completar las casillas con evidencia y commitear cada cambio coherente y verificado con rutas explícitas.

**Objetivo:** que la plataforma y cada organización atiendan por WhatsApp con el mismo módulo: números conectados con el alta de Meta, bot con menú, equipos y bandejas de Clientes e Internos, contactos con consentimiento, plantillas, difusiones y la sección «Mensajes» de la persona.

**Decisión de producto:** [ADR 0038](../decisions/0038-whatsapp-de-atencion.md). **Fuente visual:** página «Organización · WhatsApp» del lienzo v69 (`../ArquitecturaBaseMutitenantFront/docs/design/lienzo`, tableros `WA-*` y, para el teléfono, `M-WA-*`, más el área WhatsApp de `Rol`, «Cargar teléfonos» en `Usuarios`, «Mensajes» en el perfil personal, el correo «Chats esperando» de `Mensajes` y el menú de Plataforma). Cuando el lienzo y este texto no coinciden, manda el lienzo.

**Arquitectura:** respetar `docs/architecture/backend.md` (§15, módulo quitable), `docs/architecture/multitenancy.md`, ADR 0007, 0014, 0022, 0033 y 0038, y las fichas de `docs/rules/`, en especial [modulos](../rules/modulos.md), [modulos-habilitados](../rules/modulos-habilitados.md), [datos-personales](../rules/datos-personales.md), [idempotencia](../rules/idempotencia.md), [paginado-y-busqueda](../rules/paginado-y-busqueda.md), [telefonos](../rules/telefonos.md), [numeros-y-moneda](../rules/numeros-y-moneda.md) y [plantillas](../operations/whatsapp-plantillas.md). Todo vive en `Modules/WhatsApp`, y el núcleo no lo referencia.

**Requisitos fuera del código:** la plataforma registrada en Meta como **Tech Provider**, con su app aprobada para el alta embebida. Sin eso, el alta se prueba solo con la cuenta de la plataforma.

**Depende de:** la Etapa 8 (cliente de Cloud API, webhook con firma, plantillas, teléfono como método de ingreso y bot de ingreso).

## Pendiente de diseño (no se programa hasta que esté dibujado y aprobado)

Nada. El alta de Meta quedó aprobada en el lienzo v84 (sección 2). Los Inicios se aprobaron en v77 y ya están programados (ADR 0040); a la Etapa 11 le toca solo la fuente de «Para resolver» de WhatsApp.

## Seguimiento

### 0. Línea de base

- [ ] Confirmar la Etapa 8 cerrada, `main` limpio en los dos repos y Aspire apagado.
- [ ] Leer ADR 0038, el tablero de decisiones y los tableros `WA-*` y `M-WA-*` de v69; revisar este plan con un revisor de documentos antes del código.

### 1. Modelo y módulo por organización

- [ ] Módulo «WhatsApp» en el catálogo de módulos por organización. La pestaña «Módulos» de la ficha de organización lo prende y lo apaga. Apagado, las rutas del módulo responden 404 y el menú no lo muestra.
- [ ] `WhatsAppChannels`: número, dueño (plataforma u organización), a quién atiende (toda la organización o una empresa), ids de Meta, estado (conectado, falta medio de pago, desconectado) y fecha. `PhoneNumberId` único global, como ya está previsto.
- [ ] Equipos (nombre, bandeja Clientes o Internos, personas, minutos para el aviso por correo), contactos (nombre, número, etiquetas, consentimiento de promociones con su origen, vínculo con una cuenta), conversaciones (bandeja, equipo, quién la tomó, estado de la ventana de 24 h), mensajes, notas internas, plantillas, difusiones y destinatarios.
- [ ] Clasificar cada tabla: lo de la organización es `ITenantOwned`; el vínculo de un contacto con una persona es `IConsumerBusinessShared`; lo de la plataforma es su propio esquema. Agregar los tests de aislamiento.
- [ ] Permisos de organización: `whatsapp.customers` (Atender clientes), `whatsapp.internal` (Atender internos), `whatsapp.broadcasts` (Mandar difusiones) y `whatsapp.manage` (Administrar WhatsApp), en el catálogo, en `Permissions.resx` y `.en.resx` y en el seed. Plataforma: `platform.whatsapp.manage` y un permiso para atender las consultas del número propio.

### 2. Conectar números (alta embebida de Meta)

- [ ] **Pantallas del alta** (lienzo v84, front `a085cfd`).
  - **Tableros:**
    - WA-Alta-Meta: la ventana de Meta, en 8 pasos, en inglés como sus capturas;
    - WA-Alta-Coexistencia: el número que ya está en la app WhatsApp Business;
    - WA-Alta y M-WA-Alta: lo nuestro alrededor de la ventana;
    - Plat-WA-Alta y M-Plat-WA-Alta: el número de la plataforma.
  - **Decisiones del usuario:**
    - «Atiende a» va antes de abrir Meta, así que «Cancelar» no deja nada a medias. Sin empresas, la ventana se abre directo.
    - Al volver hay tres resultados:
      - «Número conectado»;
      - «No se conectó el número» (evento `CANCEL`);
      - el error de Meta con su código (evento `ERROR`, el `error_id`).
    - Cerrar la ventana en la última pantalla cuenta como terminado.
    - No hay estado «nombre en revisión»: Meta revisa el nombre visible recién cuando sube el límite de mensajes, no al conectar.
    - La plataforma no tiene «Atiende a», y su error manda al soporte de Meta.
    - En el teléfono, la ventana de Meta no se dibuja, porque Meta no publica cómo se ve en el celular.
  - **Aviso técnico:** si el teléfono es una PWA instalada, hay que probar que la ventana de Meta le devuelva el resultado.
- [ ] Recibir lo que devuelve el alta (id de la cuenta de WhatsApp Business y del número), cambiar el código por el token del negocio, suscribirse a los webhooks de esa cuenta y registrar el número. Nunca registrar tokens en logs. Soportar coexistencia.
- [ ] Consultar si la cuenta tiene medio de pago: sin medio de pago, el estado es «Falta medio de pago», con «Comprobar». Llevar los mensajes del mes por número.
- [ ] «Cambiar a quién atiende» y «Desconectar», con su auditoría.
- [ ] **Plataforma › WhatsApp** (lienzo v80, front `cb40cdb`, tableros `Plat-WA-*` y `M-Plat-WA-*`; [ADR 0038](../decisions/0038-whatsapp-de-atencion.md), «El número de la plataforma»). El número propio atiende solo consultas, además de códigos, invitaciones y avisos.
  - **Consultas:** bandeja sin Clientes ni Internos, con «Ver la cuenta», que lleva a Cuenta-Plat.
  - **Número:**
    - «Qué hace»: códigos y avisos como «Siempre», y consultas con un interruptor;
    - estados: conectado, falta medio de pago y sin número;
    - si falta el medio de pago, los códigos y avisos salen solo por correo.
  - **Bot:** con la opción del sistema «Entrar a mi cuenta», que no se borra.
  - **Equipos:** de operadores.
  - **Plantillas:** «Para consultas» y «Del sistema», estas últimas solo de lectura.
  - Sin contactos, sin difusiones y sin marketing.

### 3. Entrada de mensajes y bandejas

- [ ] Reconocer quién escribe: un número verificado en la cuenta de un miembro activo de la organización dueña del número va a Internos; cualquier otro, a Clientes.
- [ ] Bot por número: bienvenida, fuera de horario y menú como lista interactiva de WhatsApp (hasta 10 opciones), donde cada opción pasa a un equipo o contesta un texto. Los empleados no ven el menú y van al equipo de Internos configurado.
- [ ] Tomar (al responder también se toma, con control de carrera: si otro lo tomó antes, error y aviso), pasar a equipo o persona, y notas internas (un botón al lado de donde se escribe las prende).
- [ ] Ventana de 24 h: fuera de ella solo se manda una plantilla.
- [ ] Aviso por correo cuando un chat queda sin tomar más del tiempo del equipo: por outbox (ADR 0014), solo con el resumen.
- [ ] Registro de acciones internas por módulo: cada acción declara su permiso. La plantilla no trae acciones de negocio.

### 4. Contactos, consentimiento y personas

- [ ] Etiquetas y consentimiento de promociones: el bot pregunta y «Baja» lo corta solo. Importar contactos (CSV o Excel) con «Descargar ejemplo» (Nombre, Teléfono, Etiqueta, Acepta promociones); el consentimiento va por contacto, en esa columna, con su origen guardado. Sin «Sí», el contacto queda «Sin respuesta» y no entra en difusiones.
- [ ] Vincular con la cuenta: el bot ofrece el enlace; con la aceptación de la persona, la organización ve su nombre verificado. La persona puede desvincular. Sumarlo a «Exportar mis datos» y a la baja de la cuenta.
- [ ] «Cargar teléfonos de empleados»: revisar cada número (ya es miembro, se invita o inválido), sin revelar si tiene cuenta (ADR 0041), y confirmar los vínculos y las invitaciones, que salen por WhatsApp. La cuenta se vincula al aceptar. `[Idempotent]`.
- [ ] Fuente de «Para resolver» del módulo (ADR 0040): un `IPendingItemSource` de WhatsApp para «Falta medio de pago».

### 5. Plantillas y difusiones

- [ ] Crear plantillas por la API de Meta (nombre, tipo Utilidad o Marketing, texto con datos variables), traer las existentes y su estado (en revisión, aprobada, rechazada con motivo). Una aprobada se duplica, no se edita.
- [ ] Elegir plantilla en un chat: datos completados, vista previa y costo estimado (`Money`).
- [ ] Difusiones: destinatarios (los que aceptaron o una etiqueta), programar, costo estimado, envío por outbox respetando los límites de Meta, resultados por contacto (entregado, leído, respondió, baja, no llegó con motivo) y «Duplicar».

### 6. Front, copiando el lienzo v69

- [ ] Chats (Clientes e Internos, filtros, tomar, pasar, notas, 24 h, elegir plantilla), Contactos (importar), Ficha del contacto, Difusiones, Nueva difusión y Resultados.
- [ ] Administración › WhatsApp: Números (falta medio de pago), Bot con «Configuración» y «Recorrido», Equipos y Plantillas.
- [ ] Teléfono de Plataforma (`M-Plat-WA-*`): en la barra de abajo, «Consultas» en lugar de «Chats», sin Contactos ni Difusiones; «Más» (M-Mas-Plat) suma WhatsApp con Número, Bot, Equipos y Plantillas.
- [ ] Teléfono como app (`M-WA-*`): barra de abajo con Inicio, Chats, Contactos, Difusiones y «Más»; dentro de un chat y en las pantallas con acción fija abajo, sin barra. Diálogos como hojas desde abajo y «+» flotante para crear.
- [ ] Área WhatsApp en Rol, «Cargar teléfonos» en Usuarios, «Mensajes» en el perfil personal, el menú de Plataforma («WhatsApp» y la configuración aparte) y el correo «Chats esperando».

### 7. Puerta

- [ ] La general (build sin advertencias y tests con Docker), los tests de aislamiento del módulo y `ModuleIsolationTests`.
- [ ] Recorrido real con el webhook de Meta apuntando al túnel: conectar un número de prueba, escribir como cliente y como empleado, tomar, pasar, mandar una plantilla y una difusión chica.
- [ ] La prueba de fuego de la Etapa 8, otra vez: quitar el módulo en una copia descartable y que el núcleo quede en verde.
