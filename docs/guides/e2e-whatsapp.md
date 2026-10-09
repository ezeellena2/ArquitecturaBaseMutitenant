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

La salida de Node admite `CHECK <resultado sanitizado>` y `FIXTURE <JSON>`. Back reenvía CHECK al log y contesta cada FIXTURE con una línea JSON por stdin de Node. Sólo puede haber una orden pendiente. El runner consume y cierra stdin al finalizar; nunca imprime una respuesta, cuerpo HTTP, OTP, teléfono, ticket o URL con fragmento. stderr no se publica desde el host. No existe una ruta HTTP de control o lectura de pruebas.

Cada orden lleva `kind`, `scenario` y `phone`. El identificador de escenario es ASCII alfanumérico con guion/underscore, de 1–80 caracteres y único en esa corrida. Los destinos son sintéticos de la fixture: `+54911345678XX`, 14 caracteres. Iniciá `begin` **antes** de la acción UI para capturar el cursor UTC; el mismo escenario no se reinicia ni cambia de destinatario. Para un paso posterior del mismo teléfono, creá otro escenario antes de solicitar el nuevo código. Esperá la respuesta HTTP que confirma el commit antes de leer outbox.

| kind | Efecto y respuesta |
|---|---|
| `begin` | Ata escenario/destino y cursor UTC. `{ "ok": true }`. |
| `code` | Último OTP `codigo_ingreso` del outbox cifrado, destino y cursor de ese escenario. `{ "proof": "…" }`. |
| `operatorCode` | OTP Email del único operador seed `e8-operator@example.test`; requiere escenario con Phone `+5491134567899`. Lectura del párrafo OTP renderizado, sin despachar a SMTP. `{ "proof": "…" }`. |
| `inbound` | Envía un POST real firmado al webhook, texto «Hola», ID estable por escenario (repetir verifica deduplicación). `{ "status": 200 }` después de persistir. |
| `bot` | Procesa por el puerto actual la cola durable y devuelve el último enlace/CTA del destinatario desde outbox cifrado. `{ "proof": "…" }`. |
| `events` | Consulta mensajes persistidos por contacto y recepción posterior al cursor. `{ "events": [{ "kind": "Text", "direction": "Inbound", "status": "Processed", "processed": true }] }`, sin cuerpos ni pruebas. |
| `expireTicket` | Recibe además `ticket` por IPC privado. Vence sólo el hash exacto del ticket emitido después del cursor para el dueño del Phone del escenario, no consumido. Una coincidencia exacta o falla; no cambia reloj global, cookies ni otros tickets. `{ "ok": true }`. |

Ejemplo de orden sin secreto: `FIXTURE {"kind":"begin","scenario":"signup-1440","phone":"+5491134567880"}`. Después del alta por UI y su commit, enviar `code` con el mismo escenario/destino. Para el bot, `begin` → `inbound` → `events` → `bot`, y abrir la URL recibida en el navegador real. Para recuperaciones, generar y verificar el pedido por UI, ingresar como el operador exclusivo y aprobar/rechazar por UI; la fixture no resuelve la recuperación.

El operador seed tiene permisos de Plataforma y Phone verificado, sin perfil Personal. Ingreso por Email usa `operatorCode`; por WhatsApp usa `code`. Las guardas del protocolo, aislamiento de escenarios y rechazo fuera de Testing los verifica `WhatsAppBrowserFixtureTests` (E8). El runner real permanente y su resultado pertenecen al Front/Integración; no considerar una fixture compilada como un E2E aprobado.
