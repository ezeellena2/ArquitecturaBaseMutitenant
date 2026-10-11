# ADR 0038 · WhatsApp de atención: plataforma, organizaciones y personas

Estado: aceptada el 2026-10-03, pantalla por pantalla con el dueño del producto. La fuente visual es la página «Organización · WhatsApp» del lienzo v69 (`../ArquitecturaBaseMutitenantFront/docs/design/lienzo`, tableros `WA-*` para la compu y `M-WA-*` para el teléfono), y el detalle de cada elección está en el tablero «Criterio · Decisiones por pantalla». El plan ejecutable es [la Etapa 11](../plans/2026-10-03-etapa-11-whatsapp.md).

## Contexto

El plan tenía el número de la plataforma (Etapa 8: códigos, invitaciones, avisos y bot de ingreso) y una Etapa 11 opcional, apenas esbozada, con «canales por organización». Hacía falta decidir quién tiene un número, para qué lo usa y qué hace una persona B2C. También qué pasa con los empleados que escriben al mismo número que los clientes, quién le paga a Meta y cómo se atiende entre varios.

## Decisión

### Quién tiene WhatsApp

- **La plataforma** tiene su número para los códigos, las invitaciones, los avisos de la cuenta y el bot de ingreso (Etapa 8). En la Etapa 11 ese número también **atiende consultas**, con el mismo módulo que usan las organizaciones (bot, equipos, bandeja y plantillas) y lo atienden los operadores con permiso. Se configura desde Plataforma › WhatsApp.
- **Cada organización** conecta **uno o más números**. Cada número dice a quién atiende: a toda la organización o a una empresa.
- **La persona B2C** no tiene número de negocio ni bot. Meta solo da la API a negocios. La persona escribe y recibe, y puede tener su WhatsApp verificado como forma de entrar (ADR 0033).
- No hay número compartido para las organizaciones: mezclaría marcas y respuestas, y una organización con spam bajaría la calidad del número que manda los códigos de todos.
- WhatsApp por organización es un **módulo que la plataforma prende o apaga por organización**, desde la pestaña «Módulos» de su ficha.

### Quién escribe y a dónde llega

- **Se reconoce por la cuenta:** si el número está verificado en la cuenta de un miembro activo, es **empleado** y su mensaje va a la bandeja **Internos**. Si no, es **cliente** y va a **Clientes**. Un miembro queda como empleado hasta que se desvincula. Lo que puede hacer por WhatsApp sale de sus roles. No hay listas de teléfonos aparte.
- «Cargar teléfonos de empleados», en Usuarios, es una ayuda para vincular de a muchos. Muestra qué pasa con cada número antes de confirmar: ya es miembro (se vincula) o se invita por WhatsApp. No dice si el número tiene cuenta ([ADR 0041](0041-no-revelar-cuentas.md), enmienda del 2026-10-03), y la cuenta se vincula al aceptar. Nadie hace nada interno sin cuenta.
- **El bot** tiene respuestas automáticas (bienvenida y fuera de horario) y un **menú**. El menú se manda como lista interactiva de WhatsApp, de hasta 10 opciones, y cada opción **pasa a un equipo** o **contesta un texto**. Por ahora no hay sub-menús. Hay un bot por número.
- **Los equipos** dicen qué chats le llegan a cada persona. Los roles dicen qué puede hacer. Cada equipo atiende una bandeja. Un chat se **toma**: responder un chat sin tomar lo toma, y si otro lo tomó antes, el mensaje no sale y se avisa. Un chat se **pasa** a un equipo o a una persona. Hay **notas internas** que el cliente no ve: se prenden con un botón al lado de donde se escribe.
- Si nadie toma un chat en el tiempo que eligió el Dueño, sale **un correo al equipo** que solo resume (cuántos chats y hace cuánto). Nunca copia mensajes ni teléfonos de clientes.
- Las acciones internas de cada negocio, como leer un PDF o cargar un pedido, no vienen en la plantilla. La plantilla trae **la mecánica**: reconocer al empleado y un registro de acciones con permiso, donde cada proyecto suma las suyas.

### Contactos, personas y promociones

- Un cliente que tiene cuenta puede **vincular la conversación a su cuenta** cuando el bot se lo ofrece. Si lo hace, la organización ve su nombre verificado y la persona ve sus charlas en Perfil personal › **Mensajes**, solo para leer, con «Seguir en WhatsApp». Lo puede desvincular. Es un dato compartido entre persona y negocio (`IConsumerBusinessShared`).
- Cada contacto guarda sus **etiquetas** y si **aceptó recibir promociones**: Acepta, No acepta, Sin respuesta o Se dio de baja. El bot lo pregunta. Se pueden **importar contactos** desde un archivo; la pantalla ofrece «Descargar ejemplo» (Nombre, Teléfono, Etiqueta, Acepta promociones). El consentimiento va **por contacto**, en la columna «Acepta promociones», con su origen guardado: quien no dice «Sí» queda «Sin respuesta» y no recibe difusiones hasta aceptar.
- **Las difusiones** van solo a quienes aceptaron, muestran el costo estimado antes de mandar, respetan «Baja» y tienen resultados por contacto.
- **Las plantillas** se crean desde la plataforma y Meta las aprueba. Una aprobada no se edita: se duplica. Pasadas 24 horas del último mensaje del cliente, solo se le puede escribir con una plantilla.

### Conexión y pago

- Cada número se conecta con el **alta embebida de Meta**, con la plataforma registrada como **Tech Provider**. Se puede conectar el número que ya está en la app WhatsApp Business (**coexistencia**).
- **Cada organización le paga directo a Meta** con su tarjeta, desde su cuenta de Facebook. Desde el 01/10/2026, Meta da 1.000 respuestas gratis por número y por mes, y sin medio de pago el número no puede ni responder. Por eso la pantalla de números muestra «Falta medio de pago» en rojo y los mensajes del mes.

### Permisos

- Organización: un área «WhatsApp» con cuatro permisos: **Atender clientes** (incluye ver contactos), **Atender internos**, **Mandar difusiones** y **Administrar WhatsApp** (números, bot, equipos y plantillas). Solo aparece si el módulo está prendido.
- Plataforma: `platform.whatsapp.manage` (Etapa 11) más uno para atender las consultas del número propio. El nombre exacto lo fija el plan de la etapa.

### El número de la plataforma (precisión del 2026-10-03)

Lo decidió el dueño del producto junto con los tableros `Plat-WA-*` y `M-Plat-WA-*` del lienzo v80 (front `cb40cdb`).

- **Qué hace el número:** códigos, invitaciones, avisos y consultas.
- **Qué no tiene:** contactos ni difusiones. Una difusión puede bajar la calidad del número en Meta y cortar los códigos de ingreso de todos.
- **Menú:**
  - «WhatsApp» va directo a la bandeja **Consultas**, sin panel y sin Clientes ni Internos. Desde una conversación, «Ver la cuenta» lleva a la ficha de la cuenta.
  - Administración › WhatsApp tiene Número, Bot, Equipos y Plantillas.
  - En el teléfono, la barra de abajo dice «Consultas», y «Más» trae las cuatro secciones.
- **Número:**
  - «Qué hace» muestra códigos y avisos como «Siempre», y las consultas con un interruptor.
  - Estados: conectado, falta medio de pago y sin número.
  - **Si falta el medio de pago, los códigos y avisos salen solo por correo.**
- **Bot:**
  - No tiene selector de número.
  - La opción del sistema «Entrar a mi cuenta» manda el enlace para entrar (Etapa 8). No se borra y existe solo en la plataforma.
- **Equipos:** son de operadores (por ejemplo, Soporte y Altas), sin elegir bandeja.
- **Plantillas:**
  - Dos pestañas: «Para consultas» y «Del sistema».
  - Las del sistema son las de [`whatsapp-plantillas.md`](../operations/whatsapp-plantillas.md): no se editan ni se borran, y el ⋮ solo deja ver el texto.
  - Sin plantillas de marketing.

### Precisión del 2026-10-11 (repaso antes de la Etapa 11)

Lo decidió el dueño del producto al repasar la etapa:
- **El número de la plataforma se conecta desde la pantalla.** Plataforma › WhatsApp › Número lo conecta con el alta embebida de Meta, igual que los números de las organizaciones. Sin un número conectado, WhatsApp queda apagado, y el ingreso y el registro muestran solo correo y Google. La configuración del servidor de la Etapa 8 (`WhatsApp:PhoneNumberId`) queda solo para el arranque; cómo se pasa del valor configurado al número conectado lo fija el plan.
- **«Cargar teléfonos de empleados» no dice si un número tiene cuenta.** La [0043](0043-las-pantallas-dicen-si-hay-cuenta.md) vale para los datos propios de quien usa la pantalla, y acá son teléfonos de otras personas. Sigue mostrando «Ya es miembro», «Se invita» o «Número inválido».
- **La invitación por WhatsApp en español se rehace como plantilla nueva al cerrar la Etapa 8.** Meta la clasificó como Marketing, y así llega aprobada como Utilidad a «Cargar teléfonos».

## Alternativas descartadas

- Un bot por persona B2C: Meta no da la API a personas, y no tiene un caso de uso.
- El número de la plataforma compartido por las organizaciones que no conectan el suyo: mezcla marcas, no se sabe de quién es cada respuesta y la calidad baja para todos.
- Una lista de teléfonos internos aparte de las cuentas: se mantiene dos veces y el acceso no se corta solo cuando alguien deja la organización.
- Que la plataforma le pague a Meta y les cobre a las organizaciones: exige ser Solution Partner y manejar facturación ajena. Se puede sumar si WhatsApp pasa a ser parte de un plan pago.
- Un canal de chat web propio para que la persona escriba desde la plataforma: es un segundo canal entero y parte la conversación en dos.
- Sub-menús en el bot, IA y avisos push al celular: quedan para después. La pestaña «Recorrido» del bot ya está pensada para mostrar ramas.

## Consecuencias

La Etapa 11 deja de ser «canales por organización» y pasa a ser el módulo de atención completo, el mismo para la plataforma y para las organizaciones. La plataforma usa solo una parte del módulo, como dice «El número de la plataforma» más abajo. Sigue siendo quitable (ADR 0007): el núcleo no conoce WhatsApp y todo entra por puertos. Lo que la Etapa 8 deja hecho (cliente de Cloud API, webhook, plantillas y teléfono como método de ingreso) se reusa. Los teléfonos (`M-WA-*`) quedaron dibujados como app el 2026-10-03: barra de abajo con «Más», título en la barra, listas de borde a borde, hojas desde abajo y «+» flotante (regla en «Criterio · Marco y pantallas»). Pendiente de diseño: las ventanas del alta de Meta, que se dibujan cuando el dueño lo pida.
