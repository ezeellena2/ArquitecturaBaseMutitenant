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
