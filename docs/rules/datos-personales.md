# Términos, privacidad y datos personales

**Regla:** El uso exige aceptar términos y privacidad vigentes, con versión y fecha registradas. LoginMethods es la fuente única de contactos de ingreso. La baja conserva auditoría y elimina o anonimiza datos según su política.

## Cómo se hace
- LegalDocuments/LegalDocumentContents conservan cada versión publicada por idioma; no se editan.
- LegalAcceptances guarda cuenta, documento, versión, fecha UTC, IP y user agent; al eliminar la cuenta conserva la prueba y retira IP/agente.
- Registro guarda consentimiento explícito vigente en la misma UoW que crea identidad; Google usa formulario POST con antiforgery.
- LegalAcceptanceMiddleware bloquea con 403 Legal.AcceptanceRequired la identidad autenticada que debe aceptar una versión nueva.
- Los [recorridos de registro, invitaciones, métodos y baja](../features/identidad.md) definen sus contratos y excepciones.
- Un módulo con datos personales registra IAccountDeletionParticipant; trabaja dentro del alcance y UoW del llamador, sin Enter ni SaveChanges propios.
- Cada paso de baja es idempotente; la identidad se anonimiza, los contactos se liberan y la auditoría se conserva.
- La copia compartida y su retención legal se definen en [Personal](../features/personal.md); nunca se atribuye historia al dueño actual de un contacto.
- La exportación personal prevista usa solo datos de la cuenta, su espacio y su lado compartido; [WhatsApp](../features/whatsapp.md) mantiene su inventario.
- Rechazos por destino ocupado/inexistente no emiten códigos ficticios ni avisos al dueño; el resultado privado del operador exige probar su identidad.
- **Retención de los datos del ingreso** ([ADR 0062](../decisions/0062-retencion-de-los-datos-del-ingreso.md)): códigos a los 30 días; ingresos a los 12 meses, salvo el último exitoso de cada cuenta («Último ingreso»); autorizaciones y tokens de OpenIddict a los 14 días de vencidos o revocados; invitaciones cerradas a los 30 días, salvo la última de cada destino de cada persona invitada, y nunca una pendiente. Se purga por lotes de 500, en transacciones cortas y por condición (sin auditoría por fila), sin un rol que saltee RLS: `IdentityRetentionWorker` en el scope global y `InvitationRetentionWorker` con `TenantJobRunner`. Una purga recorre todas las organizaciones, también las suspendidas y las cerradas (`RunBusinessesAsync`): la retención no depende de que estén activas. El log de cada purga dice cuántas filas borró, nunca cuáles. Los plazos salen de `IdentityRetentionPeriods.Default` hasta que la decisión 272 los pase a la configuración de Plataforma.
- **Cada tabla del núcleo con datos personales nombra quién la purga,** o por qué se conserva (la auditoría, la prueba legal, la identidad anonimizada), en `PersonalDataPurgeTests`. Las de un módulo las suma el módulo, con el mismo criterio (ADR 0062).
- **Los chats de WhatsApp de una organización** se guardan mientras exista el contacto y se borran con él (D4 de [WhatsApp](../features/whatsapp.md), reconfirmada el 2026-11-18 frente a los 90 días de ArquitecturaBase). Su contenido va cifrado con la clave de la instalación atada a la organización ([ADR 0065](../decisions/0065-contenido-del-chat-cifrado.md)): una copia de la base sin el certificado no lo lee. La ubicación y el texto de un envío de formulario se vacían antes, al cerrar la captura o al vencer el envío (E14). La copia vinculada sigue la política de [Personal](../features/personal.md).
- **Los comprobantes fiscales** (ARCA, decisión 246, sin programar): el comprobante, su pedido, su PDF y la numeración se conservan durante el plazo fiscal que fija el negocio. No los borran desconectar ARCA, quitar el módulo ni un pedido de borrado de datos personales; la purga al vencer el plazo queda para después.

## Prohibido
- Aceptación booleana sin versión/fecha; editar legales publicados; borrar auditoría o identidad física.
- Saltear la gracia de baja; exportar datos empresariales como personales.
- Contactos de ingreso fuera de LoginMethods o escribir las copias de AspNetUsers a mano.
- Activar métodos sin probar posesión; vincular Google sin OAuth/sesión; dejar la cuenta sin respaldo propio o activo.
- Una tabla nueva con datos personales sin purga declarada, o una purga con un rol que saltee RLS.

## Copiá de
- `Domain/Legal/LegalDocument.cs` y `LegalAcceptance.cs` (E3).
- `Domain/Authentication/LoginMethod.cs` y `Application/Services/Identity/LoginMethodEditor.cs` (E3).

## Lo verifica
- `IdentityStorageAccessTests` (E2), `SensitiveToStringLoggingTests` (E0), `PersonalDataPurgeTests` (E12, ADR 0062).
- `IdentityRetentionTests` (E12) e `InvitationRetentionTests` (E12), de integración: cada plazo, la última que queda, dos organizaciones sin cruce y la auditoría que sigue nombrando a quien no tiene nombre; `InvitationRetentionAdversarialTests` (E12), una organización suspendida o cerrada también se purga, e `IdentityRetentionAdversarialTests` (E12), el log dice cuántos tokens y autorizaciones podó.

## Detalle
[Identidad](../features/identidad.md), [Personal](../features/personal.md), [ADR 0035](../decisions/0035-baja-de-cuenta.md) y [ADR 0062](../decisions/0062-retencion-de-los-datos-del-ingreso.md).
