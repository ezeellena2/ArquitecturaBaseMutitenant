# E8 — entorno unificado privado del recorrido E7 + E8

Fecha: 2026-10-10. Corte inicial: `fd573a2e0980b7be79a19ba2519770b774fd97c4` (M1). Implementación entregada a Root para publicar su propio scope, sin stage, commit ni push desde el implementador.

## Problema y resultado

El recorrido E7 usaba API Development, preparador separado sin módulos y correo pickup; la fixture WhatsApp independiente usaba otra base, otro puerto y prohibía también Email. El nuevo modo aislado de AppHost ejecuta `RealE2ESetup --recorrido-host` como recurso `api`: una API real Testing, la misma base PostgreSQL efímera de Aspire, preparación E7 en sus servicios, Front 5174 y correo pickup. El modo normal conserva su composición Development.

`ApiFactory.ForIsolatedDatabase` recibe explícitamente bootstrap, owner y runtime de la misma instancia/base. No crea ni elimina un contenedor; la ruta Testcontainers normal continúa disponible. El host externo excluye `TestControllerApplicationPart` y sus probes HTTP. La preparación E7 conserva Plataforma, legales, invitaciones, sitio público, vencimiento de código y sesión antigua. Las claves Data Protection permanecen en la misma base.

`WhatsAppFileBridge` deriva `.whatsapp.json` del readiness y delega en `WhatsAppBrowserControl`, conservando su escenario, cursor, destino, ownership y versiones. Valida Testing aislado, workspace absoluto propio, archivo regular, máximo 2048 bytes y una orden pendiente. La respuesta lower camelcase se publica con `.tmp` → rename a `.done`. OTP, cookie y enlaces se consumen exclusivamente por IPC local; no hay endpoint de control.

`RecorridoEmailPump` reclama sólo Email y usa el sender real PickupDirectory. El sender WhatsApp privado está prohibido. Testing excluye dispatcher externo y worker Notice; `recoveryNotice` invoca explícitamente el servicio real con su UoW. Primera orden: marca y avisos nuevos > 0; repetida: 0 nuevos; ambas conservan la versión Pending original, que permite la aprobación HTTP única. La aprobación UI con el diálogo y el F5 posterior pertenecen al recorrido final.

## Decisiones tomadas

- Separar preparación/configuración/correo del núcleo en `E7Preparation` y `TestFeatures/Recorrido`; los bindings WhatsApp están en `RecorridoWhatsAppModule`, `WhatsAppFileBridge` y la llamada explícita en `RecorridoHost`. La copia sin módulos debe preservar el preparador E7 y su referencia al soporte de integración que lo hospeda.
- Usar parámetros aleatorios propios de PostgreSQL aislado y configuración sintética explícita, sin user-secrets, JSON Development ni variables de proveedores en la API privada. Perfil UI: total 1000, protección 0.2, nuevos/IP 1000, cooldown 0, ventana 100 y rate limits 1000; una sola IP y ledger continuo.
- Fijar NumberId, AppSecret y VerifyToken mediante `UseSetting` temprano además de la configuración final. El primer focal mostró que WAF configura opciones después de que Program calcula la disponibilidad del webhook al registrar DI.
- Mantener issuer/callbacks explícitos `https://localtest.me:5174/`. La discovery real y OIDC se comprobaron, además de las opciones.
- Conservar los dos desafíos Google del guion E7 con ClientId/Secret sintéticos explícitos Testing, configuración temprana y final. El POST real usa antiforgery/state y callback del origen Front; sólo construye la salida a `accounts.google.com`. No acredita autenticación Google, un cliente válido en el proveedor ni callback completado.
- El ejecutable privado usa certificado HTTPS efímero y el cliente IPC fija su huella. Se retiró sólo su healthcheck Aspire, que necesitaría confiar ese certificado; el runner ya espera el readiness después de preparar E7. El healthcheck y TLS generales del producto no cambian.

## Validación focal

Resultados finales de clases disjuntas, sin sumar repeticiones:

| Comprobación | Resultado | Evidencia local |
|---|---:|---|
| RED dirigido del puente ausente | 6 ejecutados, 1 fallo de conducta, 5 correctos, 0 omitidos; exit 2 | `unified-bridge-red-real.log` |
| RED dirigido de desafíos Google E7 ausentes | 4 ejecutados, 2 fallos 404 de conducta, 2 correctos, 0 omitidos; exit 2 | `unified-google-red.log` |
| WhatsAppFileBridgeTests final | 7/7, 0 fallos/omitidos; exit 0 | `unified-bridge-final.log` |
| RecorridoUnifiedHostTests final, PostgreSQL real | 4/4, 0 fallos/omitidos; exit 0 | `unified-host-google-final.log` |
| WhatsAppBrowserPrivateCommandsTests existente | 5/5, 0 fallos/omitidos; exit 0 | `unified-existing-private-final.log` |
| Build herramienta, serial `-m:1 -nr:false -p:UseSharedCompilation=false` | 0 advertencias/errores; exit 0 | `unified-tool-google-final-build.log` |
| Build AppHost, mismos flags | 0 advertencias/errores; exit 0 | `unified-apphost-google-final-build.log` |
| Build integración, mismos flags | 0 advertencias/errores; exit 0 | `unified-it-google-final-build.log` |
| Sonda propia Aspire, sin navegador | `PROBE phase=complete exit=0` | `unified-aspire-probe.log` y script original |
| Apagado y procesos propios | stop exitoso, Aspire `[]`, `--recorrido-host` 0 procesos | `unified-aspire-stop.log`, `unified-aspire-after.json`, `unified-host-processes-after.json` |

Los cuatro casos nuevos prueban la conexión externa a la misma PG, signup/verify WhatsApp real, respaldo Email por pickup y verificación real, webhook firmado/deduplicado, bot durable y outbox WhatsApp intacto sin intentos de envío, sender prohibido, legacy-cookie reconocida y descartada por la puerta pública real, probes HTTP ausentes, Notice explícito/repetible con xmin original seguido de una aprobación HTTP y los desafíos Google consumer/business reales con antiforgery y retorno de Front. La sonda Aspire prueba además el ejecutable real, preparación E7/Plataforma/legacy, Front Vite 5174, discovery issuer exacta, ambos desafíos Google por el proxy Front, signup/verify, pickup y bot/IPC.

Los intentos de compilación y el intento de test bloqueado por named pipe del sandbox se registran como incidencias de infraestructura, sin contarlos como RED. El primer focal de host fue 2 ejecutados, 1 fallo webhook404 y 1 correcto; se corrigió la disponibilidad temprana y se repitió en verde. Una invocación de build iniciada antes de recoger la finalización anterior fue interrumpida inmediatamente; queda identificada y excluida de validación. Los tres builds finales se ejecutaron en serie. El log de `aspire run` omite el token de dashboard efímero emitido por el CLI; no contiene pruebas OTP ni cookies.

## Integridad y alcance

Contrato SHA256: `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`. Archivo ajeno Role SHA256: `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`. Ambos conservados.

Se compararon los blobs Git actuales y de HEAD de los **147 archivos** de `Infrastructure/Persistence/Migrations` (incluido snapshot): 147 iguales, 0 cambios. Los SHA256 físicos y ambos blobs están en `unified-schema-hashes.json`, SHA256 `da253cce2966ee074d43d83181508517d3c6115c2a7b0b85b579004ed5310fc3`. No se modificaron Domain, lógica productiva, contrato HTTP ni Front.

Inventario exacto de fuentes, SHA256, comandos, logs, salidas, exits y conteos: `.local/reviews/e8b-contract/e8-unified-walk-bridge-files.json`. Los procesos propios están apagados y las fuentes quedan inmóviles para Root. Quedan para la puerta única final las suites completas, recorrido Playwright con sesiones Persona/Plataforma, galería/capturas, detector de documentación y recorrido operativo Meta. Esta entrega no cierra E8 ni acredita Meta o remoción física de módulos.
