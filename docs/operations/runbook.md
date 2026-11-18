# Manual de operación

Estado: preparación **E10**, sin despliegue aplicado. El [plan de E10](../plans/2026-10-16-etapa-10-despliegue.md) define tareas, pruebas y decisiones P1–P7. B5 completa los comandos del proveedor y los identificadores no secretos de la instalación elegida; este manual fija el procedimiento común. La configuración y las claves existentes están en [configuracion.md](configuracion.md).

## Instalación y primer despliegue

Registrar proveedor/región, ambiente, dos SHAs de repos, digest de imagen, versión del bundle, dominio principal, responsables de DNS/correo y ubicación de backups. Los valores de credenciales quedan en el gestor de secretos, nunca acá ni en salidas de CI. Validar la elección de PostgreSQL18 con ICU, Redis administrado TLS y el almacenamiento privado antes de crear recursos facturables.

El DBA del proveedor crea `mt_owner` y `mt_app` con LOGIN, NOSUPERUSER, NOBYPASSRLS, NOCREATEDB, NOCREATEROLE y NOREPLICATION, con credenciales independientes. `mt_app` no es miembro de `mt_owner` ni de roles privilegiados. El administrador crea la base vacía con el equivalente exacto de:

```sql
CREATE DATABASE appdb OWNER mt_owner
  LOCALE_PROVIDER icu ICU_LOCALE 'es-AR' TEMPLATE template0;
```

No convertir ni recrear una base con datos para cambiar collation. Verificar proveedor/locale/owner contra `RuntimeRoleValidator`; una base incompatible no recibe tráfico. El SQL/grants salen de `DatabaseBootstrapExtensions` y de las migraciones vigentes, sin ejecutar en Production el bootstrap que recrea bases de Development. La API no conserva las credenciales del administrador ni de mt_owner.

Preparar backup, ejecutar el migration bundle como `mt_owner` en un job serializado y después arrancar la imagen como `mt_app`. Los permisos de esquemas, tablas, funciones, triggers y RLS salen de las migraciones. El seed idempotente de Production conserva el operador existente, sus factores de E9 y las configuraciones guardadas. No crea personas/organizaciones de muestra.

La imagen trae API+SPA en `wwwroot`, issuer principal idéntico en frontend/backend, y los archivos privados fuera del web root. El ingreso termina TLS con certificado para principal y wildcard; transmite Host y esquema reales desde proxies explícitamente confiables. No se habilita TrustAll como arreglo de un bucle HTTPS; fuera de Development y Testing, la Api no arranca sin `KnownProxies` o `KnownNetworks` (E10), porque con un proxy mal declarado todas las organizaciones comparten la cuota de una sola dirección. Las organizaciones privadas se resuelven por claims, nunca por DNS.

Probar `/alive`, `/health`, ingreso del operador con TOTP, Google, una persona y una organización, subdominio publicado y no publicado, recepción de SMTP y upload/download privado. La imagen y el bundle usados son los artefactos verificados por la puerta. Promover el mismo digest; no reconstruir para promover. Volver a la imagen anterior sólo con esquema compatible; las migraciones no se revierten automáticamente. En cada despliegue conviven las dos versiones un rato, y al volver atrás la vieja lee lo que escribió la nueva: por eso una migración agrega antes de borrar y un valor nuevo se escribe un despliegue después del que lo lee ([ADR 0060](../decisions/0060-dos-versiones-conviven.md)).

## Secretos y certificados

Usar [scripts/secretos](../../scripts/secretos/) para nombres y carga local desde archivo fuera del repo. El verificador de E10 debe cubrir Production sin imprimir valores. En el host se inyectan nombres existentes: `ConnectionStrings:appdb`, `ConnectionStrings:cache`, `Authentication:Issuer`, `Authentication:Google:ClientSecret`, `Authentication:LoginCode:HashKey`, `Email:Smtp:*`, `DataProtection:Certificate:{Base64,Password}` y `Authentication:Certificates:{Signing,Encryption}:{Base64,Password}`; además de los del módulo instalado y la identidad del almacenamiento elegido. El job de migración recibe su conexión mt_owner separadamente por `Migrations__ConnectionString` (E10), leída por la factory; no se inyecta en la API ni se imprime en argumentos. La conexión Redis exige TLS, autenticación y validación de certificado.

Las réplicas comparten `platform.DataProtectionKeys` y sus certificados de descifrado. No se genera un PFX distinto por contenedor. Conservar separados HTTPS, firma/cifrado OIDC y protección del anillo. B4 incorpora listas `Previous` con `{Base64,Password}` bajo `Authentication:Certificates:Signing`, `Authentication:Certificates:Encryption` y `DataProtection:Certificate` (E10). Rotar publicando la nueva clave/certificado como actual y mantener los anteriores sólo para lectura/validación durante la vigencia de los datos; probar tokens, cookies y payloads entre réplicas antes de retirar uno. Perder el PFX de Data Protection puede volver ilegibles los datos protegidos aunque se conserve la base. Una rotación HMAC respeta las cuotas diarias y los códigos pendientes documentados en configuración; no vacía límites a mitad del día.

El CI obtiene acceso cloud por OIDC, con trust de repo/ambiente/audience exactos. El token lector de contratos sólo alcanza el repo contraparte; no habilita deploy ni push. No se exponen secretos a código de PR. Verificar nombres/certificados públicos, no volcar configuración ni user-secrets completos.

## Backup y restore

Respaldar PostgreSQL con retención y restauración del proveedor elegido, objetos privados y configuración no secreta; custodiar las claves/certificados necesarios de forma separada. Un backup de base no contiene automáticamente los archivos del bucket ni reemplaza el PFX. Redis es reconstruible según ADR0052, no la copia maestra de permisos o datos. El certificado TLS del sitio tampoco sustituye a los certificados OIDC/Data Protection.

Ensayar restore en un ambiente aislado: recrear roles y base ICU `es-AR`, restaurar bajo mt_owner, aplicar grants y comprobar runtime mt_app/RLS; recuperar anillo y certificados de lectura; reponer objetos referidos por la base; invalidar el namespace de caché restaurado y verificar aislamiento. No activar workers ni SMTP/WhatsApp contra destinatarios reales durante el ensayo.

**Una restauración real** también arranca, la primera vez, con los trabajos de fondo apagados por configuración, y se prenden después de revisar la base: una Api levantada sobre una copia restaurada vuelve a mandar lo que en ese momento estaba pendiente (correos, mensajes de WhatsApp, difusiones, avisos). Los interruptores son de la instalación, no de cada organización. Hoy tienen uno el procesador de entrada, las difusiones y la sincronización de plantillas de WhatsApp (`WhatsApp:ProcessInboundInBackground`) y los turnos del Asistente (`TurnWorkerEnabled`); el outbox y los avisos suman el suyo en la E10. Comprobar login, lectura de payload cifrado y archivo privado antes de cambiar tráfico. Los resultados son la fila de restore de la puerta, no una colección de evidencias versionadas.

Una restauración no debe reactivar exportaciones vencidas, cuentas ya dadas de baja ni enlaces revocados. Antes de habilitar acceso, conciliar las bajas/purgas posteriores al punto restaurado y la expiración por reloj actual. Conservar auditoría y las retenciones legales de las fichas; no usar backups para reconstruir historia personal ya eliminada dentro de la aplicación.

## Suspensiones, trabajos y purga

Suspender/reactivar cuentas u organizaciones desde Plataforma, con permiso individual, motivo y prueba reciente de E9. Para una organización, ITenantScope sólo después de autorización y SecurityEvent, sin transacción previa. Confirmar revocación de accesos y la invalidación Redis en otra réplica; no cambiar flags ni tenant_id a mano en SQL.

Outbox, exportaciones y baja usan trabajos durables con lease; no se solucionan duplicados ejecutando dos efectos manualmente. Reclamo/confirmación van en la UoW, transporte y borrado de archivos afuera. Investigar por traceId e identificadores técnicos, sin correos/teléfonos, claves, enlaces o contenido en logs. Durante fallas de Redis, aplicar el fallback de E9: readiness queda en Degraded, que responde 200, y las copias siguen atendiendo con PostgreSQL ([ADR 0058](../decisions/0058-sin-redis-la-copia-sigue-en-el-trafico.md)); liveness no lo incluye. No desactivar autenticación ni RLS para recuperar el servicio.

La exportación personal prepara JSON de cuenta, Personal y su lado compartido; envía sólo correo y exige la misma cuenta para descargar. Vence a las 48 h desde que queda lista, incluso con correo demorado. Pedido de baja revoca las exportaciones; los participantes dejan eliminación durable, el worker borra objetos y confirma. Un borrado cloud fallido conserva su trabajo para reintentar. El vencimiento/baja bloquea descargar aunque el objeto siga pendiente de purga física. Los huérfanos del almacenamiento se verifican contra referencias antes de borrarlos. No alterar la gracia de baja, los plazos de módulos o la auditoría para resolver falta de espacio.

## WhatsApp en producción (E10)

Desde la 10, producción tiene WhatsApp (P8 del [plan de la 10](../plans/2026-10-16-etapa-10-despliegue.md)): el número real de la plataforma y el alta embebida para que cada organización conecte el suyo. No se cambia código de WhatsApp: son configuración, secretos y verificación.

- **Dos apps de Meta.** La actual, «Plantilla Multitenant» (AppId 1482265200631609), queda para desarrollo, con el túnel y la WABA de prueba. Producción usa una app nueva del portafolio de la plataforma. El motivo: una app tiene una sola URL de webhook. El override por WABA o por número cubre `messages`, `calls` e `history`, pero `account_update` (que avisa que terminó un alta embebida) y el estado de las plantillas van siempre a la URL de la app ([override](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/override/)).
- **La app de producción** tiene:
  - el webhook HTTPS en el dominio principal, con los campos de la E11 y su `VerifyToken`;
  - en Facebook Login for Business, prendidos Client OAuth, Web OAuth, Enforce HTTPS, Embedded Browser OAuth, Strict Mode y el ingreso con el SDK de JavaScript;
  - el dominio principal exacto, sin comodín, en Allowed domains y en Valid OAuth redirect URIs. El alta embebida se abre solo en el dominio principal, porque el tenant sale del claim ([implementación](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/implementation/));
  - una configuración del alta con un acceso que no vence, el valor por defecto de Meta para la comunicación entre servidores. Así deja de existir el vencimiento a los 60 días de la configuración de desarrollo.
- **El número de la plataforma:**
  - es real, con código de área, recibe SMS o llamadas y no está en WhatsApp; si está, se borra antes;
  - lleva un PIN de 6 dígitos y su nombre visible, y su WABA tiene medio de pago;
  - antes de abrir, las 16 plantillas del sistema del [inventario](whatsapp-plantillas.md) se crean y se aprueban en esa WABA.
- **Los límites:**
  - hasta verificar la empresa, 10 organizaciones nuevas cada 7 días, 250 destinatarios por día fuera de la ventana de 24 horas y 2 números;
  - con la verificación de la empresa y App Review, 200 organizaciones por semana, 2.000 destinatarios y 20 números;
  - App Review pide acceso avanzado a `whatsapp_business_messaging` y `whatsapp_business_management`, con dos videos: un mensaje enviado y recibido, y la creación de una plantilla;
  - la verificación de la empresa tarda unos días y App Review, cerca de 24 horas ([Tech Provider](https://developers.facebook.com/documentation/business-messaging/whatsapp/solution-providers/get-started-for-tech-providers)).
- **Los secretos** van por nombre en el gestor del ambiente: los `WhatsApp:*` de [configuracion](configuracion.md), con los valores de la app y de la WABA de producción. Nunca van en el repo ni en la imagen.

## Al lanzar un producto

1. **Países habilitados.** Revisar [habilitados.json](../../scripts/datos-de-referencia/habilitados.json) y `WhatsApp:AllowedCountries` juntos: **la plantilla sólo trae Argentina (`AR`)**. Habilitar los países del producto mediante el generador/catálogos y sus tests; no agregar listas paralelas en código. Revisar formatos, monedas y tipos fiscales del catálogo correspondiente, sin inferir que habilitar un país habilita automáticamente WhatsApp.
2. **Tech Provider propio en Meta.** Desde la Etapa 10 (P8), la plataforma hace esto en su primer despliegue: la app de producción, la verificación de la empresa, App Review y los dominios del alta embebida. Ver [WhatsApp en producción](#whatsapp-en-producción-e10). Otro producto con su propia marca repite el trámite con su app.
   - **Permisos:** el alta embebida usa `whatsapp_business_management`, `whatsapp_business_messaging` y los que exija la configuración vigente.
   - **Qué verificar:** que el acceso avanzado esté aprobado, la publicación, las políticas y URLs del producto, el webhook HTTPS y los dominios OAuth.
   - **Qué no alcanza:** la app de desarrollo de la plantilla y sus cuentas de prueba no acreditan el lanzamiento de otro producto.
   - **Qué se registra:** solo el AppId, el config_id y los identificadores públicos. El AppSecret y los tokens van al gestor de secretos.
3. **Invitación por WhatsApp en español.** Se envía como **Marketing**, según la precisión de E11. Revisar plantilla aprobada, idioma, categoría, consentimiento y tarifa vigente de Meta; no presupuestarla ni enviarla como Utilidad. La versión en inglés y los avisos del sistema conservan su categoría aprobada; no se cambia el catálogo de plantillas para forzar la clasificación.
4. **Acceso del alta embebida.** En producción, la configuración del alta usa un acceso que no vence (P8 de la Etapa 10). El acceso de usuario del sistema de la configuración de desarrollo vence a los **60 días**. Hay que renovarlo **antes de que venza**, con responsable y fecha registrados fuera de los secretos. Al renovar/reconectar, verificar permisos, suscripciones y un envío/webhook real. No dar por existente una renovación automática: el código de E11 no la implementa. Un acceso sin vencimiento también puede revocarse; se verifica su validez y se conserva el procedimiento de reconexión.
5. **Correo de producción.** La plantilla manda desde el Gmail del desarrollo (unos 500 correos por día, compartidos con ArquitecturaBase). Un producto real pasa a un servicio transaccional (Resend o Amazon SES) con su propio dominio verificado (SPF, DKIM y DMARC). Es solo configuración: el transporte SMTP no cambia.
6. **Antivirus del almacenamiento.** Antes de recibir archivos de clientes reales, hay que activar el análisis de malware del almacenamiento (por ejemplo, Defender for Storage) y decidir qué pasa con un archivo marcado. Hoy se reciben los adjuntos del asistente, y desde la E18 también fotos y documentos. ArquitecturaBase lo pide como requisito antes de producción.

El Coordinador completa en B5 los comandos del proveedor, calendario de renovación, ubicación de backups y responsables aprobados, sin convertir esta lista en funcionalidades nuevas del producto.
