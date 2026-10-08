# ADR 0049 · Precisiones de WhatsApp y del asistente antes de implementar

Estado: aceptada el 2026-10-08 por el dueño del producto: «hagamos esto que recomendás, ajustá la documentación». Precisa las [0033](0033-metodos-de-ingreso.md), [0043](0043-las-pantallas-dicen-si-hay-cuenta.md) y [0048](0048-asistente.md). No declara implementadas E8 ni E8b.

## Contexto

Los planes ejecutables de [E8](../plans/2026-10-07-etapa-8-whatsapp.md) y [E8b](../plans/2026-10-08-etapa-8b-asistente.md) encontraron seis huecos de producto. El usuario aceptó sus recomendaciones y pidió contrastar documentación, código existente y forma de trabajo antes de empezar, conservando los patrones del proyecto.

## Decisión

### WhatsApp de la plataforma (E8)

- El enlace de ingreso del bot entra como **persona**. Desde allí se cambia a empresa con el mecanismo existente. **No se emiten enlaces para cuentas de operador:** si escribe su número, recibe «Para entrar, usá la web.» / “To sign in, use the website.”, sin enlace ni mención a Plataforma o al operador. Entra por `/plataforma`, con su código y cookie exclusiva. Preview/redeem rechazan también un enlace previo cuya cuenta ahora es operadora con el mismo error de enlace inválido, sin sesión. Esta aclaración explícita del usuario reemplaza la recomendación anterior de entrar a Plataforma desde el bot.
- A un número sin cuenta el bot ofrece el **registro personal por WhatsApp en la web**. No crea identidad, no acepta términos por un «Hola» ni incluye el número en la URL. Si ConsumerSignup está cerrado, ofrece ingreso/recuperación e informa el registro cerrado.
- Los avisos `OrganizationApproved`, `OrganizationRejected` y `PublicPageModerated` también llegan a los métodos Phone por el módulo. Se agregan tres plantillas Utility, conservando contenido y privacidad del correo: `organizacion_aprobada`, `organizacion_rechazada`, `pagina_publica_moderada`. El catálogo E8 tiene **16 nombres**, Authentication + 15 Utility, con variantes es/en. Exportación de datos e invitación de operador siguen solo por correo.
- La 0043 se aplica también a teléfonos: número inexistente/ocupado produce su error de campo y acción real, sin código ficticio ni aviso al dueño. La 0045 elimina confirmación OTP en Mi cuenta; el número nuevo sí requiere su propia prueba de posesión.

### Asistente (E8b)

- «Mis mensajes con empresas» permanece en la bienvenida personal. Consulta un puerto neutral `IConsumerInteractionSource`; mientras no haya una fuente productiva devuelve **«Todavía no tenés mensajes con empresas»**, sin enlace a una bandeja inexistente. No lee TestFeatures ni adelanta E11; esta registra su fuente cuando exista.
- Las alertas own-key van por **email** a los Dueños activos con un método Email verificado y utilizable. Un correo de Google sin método Email verificado no basta. Antes de habilitar OwnKey debe existir al menos uno; de lo contrario Configuración lleva a agregar/verificar correo en Mi cuenta. Se usa la definición existente de Dueño (`SystemRoles.TenantAdmin`, membresía y cuenta activas), sin flag nuevo. Los avisos platform-key van al correo de soporte ya definido en Config-Plat.
- La cuota diaria con clave de plataforma es **global por UserId/día UTC**, compartida entre Consumer, Business y Platform. Con clave propia es **UserId + organización/día UTC**, con el límite de esa organización. El límite inicial es 50, configurable por quien corresponde. Cambiar acceso/organización/zona no reinicia la cuota financiada por Plataforma. Consultas fijas y títulos no suman preguntas; los títulos sí suman costo. «Hoy» y renovación se muestran en la zona local, con el instante de renovación de UTC. La cuenta exclusiva del operador conserva la 0042.

## Consecuencias

- Estas seis respuestas reemplazan «Para decidir» de los planes. Contrato, tareas, tests y puerta deben usarlas; no se vuelve a preguntar su aprobación.
- Hay complementos visuales pendientes sobre v114: respuesta del bot sin cuenta/registro cerrado y neutra al operador, avisos de organización por WhatsApp (moderación bloqueada/permitido volver a publicar), resultado personal vacío y error/acción de Config-Org sin Dueño con Email. Se dibujan con las piezas existentes antes de programar esas tareas. No se presenta v114 como si ya los incluyera.
- Cambios técnicos necesarios se distinguen del producto y de lo implementado: contribuidores EF, guardas para namespaces de módulos, streaming/replay privado, actor de worker e IFileStore se planifican; no se crean infraestructura o convenciones adicionales sin una necesidad concreta.
- Los artículos de ayuda y contratos describen solo capacidades efectivamente implementadas. La forma rápida tiene una sola puerta al final, conservando pruebas focales y tableros como fuente.

## Alternativas descartadas

- Recordar el acceso de un navegador dentro del enlace de WhatsApp, crear cuentas al recibir mensajes o cambiar el bot de ingreso por el asistente.
- Ocultar el atajo personal aprobado, usar datos sintéticos como mensajes reales o construir una bandeja E11 en E8b.
- Ignorar alertas own-key porque el Dueño solo tiene Phone, inventar avisos de costo por Meta o multiplicar cuotas al cambiar de acceso.
