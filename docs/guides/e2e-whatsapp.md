# Navegador real de WhatsApp en Testing

El soporte versionado reproduce la sonda provisional E8 con API/Kestrel, OIDC, outbox cifrado y PostgreSQL reales. El runner del Front maneja Vite y Playwright, los recorridos en 1440/390 y la aprobación/rechazo de recuperaciones por la UI de Plataforma. Esto verifica el encaje Front/Back; el envío y `delivered` de Meta se verifican en el recorrido operativo separado de T14.

## Comando e aislamiento

Con Docker disponible y ambos checkouts, establecé `MT_E2E_ISOLATED=1` en el proceso y ejecutá:

```text
dotnet run --project tools/ArquitecturaBaseMultitenant.RealE2ESetup -- --whatsapp-browser <ruta-absoluta-del-runner-Front.mjs>
```

El modo reutiliza `ApiFactory` desde `TestFeatures/WhatsAppBrowser`, crea/migra su PostgreSQL Testcontainers, inicia HTTPS en un puerto propio y ejecuta Node con cwd en el Front. No usa Aspire, user-secrets, conexiones de Development ni callback Meta. El entorno de la API es Testing; reemplaza los sender y no tiene despachador externo. El proceso elimina certificados/clave privados temporales, detiene su Kestrel y descarta su contenedor al terminar. El timeout es 30 minutos.

Node recibe `API_HTTPS` y `services__api__https__0` con la dirección API, `VITE_AUTH_ISSUER=https://localtest.me:5176/`, `MT_E2E_ISOLATED=1` y los paths temporales `E8_CERT`/`E8_KEY`. Vite sirve HTTPS en `localtest.me:5176`; callbacks registrados: `/auth/callback`, `/silent-renew.html` y logout `/`. El certificado es de prueba: el navegador puede usar `ignoreHTTPSErrors` exclusivamente en este entorno.

## Control privado por líneas

El perfil UI de la fixture fija explícitamente `DailyAuthCodeLimit=1000`, `ProtectedAuthCodeFraction=0.2` y `DailyNewAuthCodeIpLimit=1000`: margen conservador de 20 OTP por cada uno de los 38 grupos (760 nuevos frente a 800 disponibles). Toda la corrida comparte su ledger; no se reinicia por escenario ni se cambia la IP para evadirlo. Ya usa cooldown 0 y ventana 100 para recorrer UI. Este perfil Testing verifica encaje y sesión, no los defaults de cuota: `WhatsAppQuotaEnqueueTests` y `WhatsAppCodeQuotaTests` prueban por separado 100 total, 80 nuevos, 20/IP y protección mínima 20. No altera la configuración productiva ni acredita Meta.

La salida de Node admite `CHECK <resultado sanitizado>` y `FIXTURE <JSON>`. Back reenvía CHECK al log y contesta cada FIXTURE con una línea JSON por stdin de Node. Sólo puede haber una orden pendiente. El runner consume y cierra stdin al finalizar; nunca imprime una respuesta, cuerpo HTTP, OTP, teléfono, ticket o URL con fragmento. stderr no se publica desde el host. No existe una ruta HTTP de control o lectura de pruebas.

Cada orden lleva `kind`, `scenario` y `phone`. El identificador de escenario es ASCII alfanumérico con guion/underscore, de 1–80 caracteres y único en esa corrida. Los destinos son sintéticos de la fixture: `+54911345678XX`, 14 caracteres. Iniciá `begin` **antes** de la acción UI para capturar el cursor UTC; el mismo escenario no se reinicia ni cambia de destinatario. Para un paso posterior del mismo teléfono, creá otro escenario antes de solicitar el nuevo código. Esperá la respuesta HTTP que confirma el commit antes de leer outbox.

| kind | Efecto y respuesta |
|---|---|
| `begin` | Ata escenario/destino y cursor UTC. `{ "ok": true }`. |
| `code` | Último OTP `codigo_ingreso` del outbox cifrado, destino y cursor de ese escenario. `{ "proof": "…" }`. |
| `operatorCode` | OTP Email del único operador seed `e8-operator@example.test`; requiere escenario con Phone `+5491134567899`. Lectura del párrafo OTP renderizado, sin despachar a SMTP. `{ "proof": "…" }`. |
| `methodEmailCode` | Recibe además `email` y `methodId` UUID del POST real de Agregar correo. Sólo `backup-email-1440`/`backup-email-390`, con `e8-backup-1440@example.test`/`e8-backup-390@example.test` respectivamente y Phone original principal global verificado. Devuelve el OTP Email real cifrado por stdin: `{ "proof": "…" }`, sin despachar. |
| `recoveryNotice` | Recibe además `recoveryId` UUID obtenido por la UI de Plataforma. Exige pedido Pending creado después del cursor cuyo Phone **nuevo** coincide exactamente. Invoca `IAccountRecoveryNoticeService.NotifyAccountsAsync` real y devuelve sólo `{ "marked": true, "queuedNotices": 1, "beforeVersion": 123, "afterVersion": 123 }` (valores ilustrativos). No decide el pedido. |
| `inbound` | Envía un POST real firmado al webhook, texto «Hola», ID estable por escenario (repetir verifica deduplicación). `{ "status": 200 }` después de persistir. |
| `bot` | Procesa por el puerto actual la cola durable y devuelve el último enlace/CTA del destinatario desde outbox cifrado. `{ "proof": "…" }`. |
| `events` | Consulta mensajes persistidos por contacto y recepción posterior al cursor. `{ "events": [{ "kind": "Text", "direction": "Inbound", "status": "Processed", "processed": true }] }`, sin cuerpos ni pruebas. |
| `expireTicket` | Recibe además `ticket` por IPC privado. Vence sólo el hash exacto del ticket emitido después del cursor para el dueño del Phone del escenario, no consumido. Una coincidencia exacta o falla; no cambia reloj global, cookies ni otros tickets. `{ "ok": true }`. |

Ejemplo de orden sin secreto: `FIXTURE {"kind":"begin","scenario":"signup-1440","phone":"+5491134567880"}`. Después del alta por UI y su commit, enviar `code` con el mismo escenario/destino. Para el bot, `begin` → `inbound` → `events` → `bot`, y abrir la URL recibida en el navegador real. Para recuperaciones, generar y verificar el pedido por UI, ingresar como el operador exclusivo y aprobar/rechazar por UI; la fixture no resuelve la recuperación.

Para el aviso diferido, `begin` precede el pedido UI y se ata al Phone nuevo recovered/rejected. Con el diálogo Pending abierto y su versión ya leída, `recoveryNotice` usa el ID de ese GET: la marca y el outbox pertenecen al servicio real, con su propia UoW; la fixture lee después del commit sin tracking. `queuedNotices` cuenta las filas nuevas cifradas de la cuenta objetivo, no el contador devuelto por el servicio. Primera llamada: marca presente y avisos > 0; repetición: marca presente y cero avisos nuevos. La versión del pedido se conserva para aprobar/rechazar por HTTP/UI. El servicio procesa una tanda global de hasta 50 pedidos en la base aislada; no se presenta como servicio limitado al escenario. Testing ya excluye su worker periódico.

Para el respaldo Email, iniciá `begin` antes de Agregar correo. El escenario captura el dueño verificado principal del Phone y los métodos existentes; rechaza un método histórico, ajeno, ya verificado o un Phone que cambió de dueño/principal. `methodEmailCode` requiere el método Email pendiente exacto, código VerifyDestination de ese dueño y outbox posterior al cursor. El runner usa el OTP por stdin para verificar por UI (204) y conserva Phone principal. Esta lectura no usa `PickupCodeReader`, pues ese helper despacha outbox; los sender prohibidos de la fixture permanecen activos.

El operador seed tiene permisos de Plataforma y Phone verificado, sin perfil Personal. Ingreso por Email usa `operatorCode`; por WhatsApp usa `code`. Las guardas del protocolo, aislamiento de escenarios y rechazo fuera de Testing los verifican `WhatsAppBrowserFixtureTests`, `WhatsAppBrowserControlTests` y `WhatsAppBrowserPrivateCommandsTests` (E8). El runner real permanente y su resultado pertenecen al Front/Integración; no considerar una fixture compilada como un E2E aprobado.

## Recorrido combinado E7 + E8 en Aspire

Con `MT_E2E_ISOLATED=1`, AppHost ejecuta `RealE2ESetup --recorrido-host` como recurso `api`. Ese proceso hospeda la API real en **Testing** y prepara E7 con los servicios de esa misma API. Reutiliza exclusivamente el PostgreSQL efímero de Aspire mediante las tres conexiones explícitas bootstrap/owner/runtime; `ApiFactory.ForIsolatedDatabase` no crea ni elimina un contenedor. El modo normal de AppHost conserva la API Development. Front sigue en `https://localtest.me:5174/`, con issuer y callbacks registrados en ese mismo origen, correo `.eml` pickup y el protocolo de sesión antigua existentes.

Los parámetros PostgreSQL de la rama aislada son aleatorios y propios de la corrida. El host privado carga sólo `appsettings.json` y su configuración sintética explícita: no carga JSON Development, user-secrets ni variables de proveedores. Comparte el anillo Data Protection persistido en su base para emitir y validar la cookie antigua real. Conserva el preparador de Plataforma, invitaciones, legales, vencimiento de código y sitio público E7. El operador seed sigue siendo `e2e-operator@example.test`; sus códigos Email se leen del pickup existente.

Google usa ClientId y ClientSecret sintéticos explícitos sólo en el host privado. Los pasos E7 de registro Persona/Empresa construyen el desafío real con antiforgery, state y callback `https://localtest.me:5174/signin-google`, y abren `accounts.google.com`. Esto conserva la navegación del guion; no acredita autenticación Google, un cliente válido en el proveedor ni callback completado. El recorrido operativo de ingreso Google conserva su comprobación manual separada.

Testing excluye `OutboxDispatcher` y `AccountRecoveryNoticeWorker`. Una bomba controlada reclama sólo el canal Email y entrega con el sender real `PickupDirectory`; nunca reclama WhatsApp. El sender WhatsApp privado falla si se lo invoca. Los controles no producen receipts Meta. `recoveryNotice` conserva las guardas, la UoW del servicio real y la versión original del pedido: el worker no se adelanta y la orden local es el único disparador del aviso. El diálogo Pending debe leer la versión antes del aviso; la primera orden marca y encola > 0, la repetida encola 0 y ambas conservan xmin. Luego se aprueba una vez por UI con la versión original; F5 va después.

El runner usa su `env.command('whatsapp', request)` sobre `mt-e2e-*.whatsapp.json`, derivado del readiness `.ready`. El preparador publica la misma respuesta lower camelcase de `WhatsAppBrowserControl` en `.whatsapp.json.done` mediante `.tmp` y rename atómico. La orden JSON no supera **2048 bytes**, sólo hay una orden pendiente, y las rutas son absolutas dentro del directorio temporal propio creado por el runner (`mt-e2e-*`, `arquitecturabase-recorrido-*` o `arquitecturabase-e2e-*`). Rechaza enlaces de filesystem y comandos fuera de Testing aislado. El caller borra solicitud y respuesta después de consumirlas; OTP, ticket, cookie y enlace permanecen exclusivamente en ese IPC privado, sin stdout ni endpoint HTTP de control. El host externo tampoco incorpora `TestControllerApplicationPart` ni sus probes `test/*`.

`begin`, `code`, `inbound`, `bot`, `events` y `recoveryNotice` aceptan los escenarios `walkthrough-*` que cumplan el formato ASCII 1–80 y destinos sintéticos `+54911345678XX`; no se reinician cursores ni ledgers. El respaldo Email usa los `.eml` existentes y no necesita `methodEmailCode`. El perfil UI conserva total 1000, fracción protegida 0.2, nuevos/IP 1000, cooldown 0, ventana 100 y rate limits de ingreso/verificación 1000, todos explícitos sólo en Testing.

Para quitar módulos de una **copia**, `E7Preparation` y `TestFeatures/Recorrido` contienen el núcleo de preparación, correo y configuración; `RecorridoWhatsAppModule`, `WhatsAppFileBridge` y la llamada de composición en `RecorridoHost` son los bindings privados WhatsApp que se retiran. No quitar a ciegas la referencia herramienta → integración: también sostiene el host privado del núcleo E7. Esta separación no acredita por sí sola la remoción física ni el recorrido de Meta.

`WhatsAppFileBridgeTests` verifica guardas de IPC, formato, límite y concurrencia. `RecorridoUnifiedHostTests` usa PostgreSQL real y la conexión externa a la misma base para signup/verify, Email pickup, webhook firmado/deduplicación, bot durable, cookie antigua, probes ausentes y aviso explícito con versión/repetición/aprobación HTTP. Es verificación focal; la puerta completa y el recorrido de usuario siguen siendo la comprobación final del Front/Integración.
