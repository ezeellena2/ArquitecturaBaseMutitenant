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
- **Retención de los datos del ingreso** ([ADR 0062](../decisions/0062-retencion-de-los-datos-del-ingreso.md), sin programar): códigos a los 30 días; ingresos a los 12 meses, salvo el último de cada cuenta; autorizaciones y tokens de OpenIddict a los 14 días de vencidos o revocados; invitaciones cerradas a los 30 días, salvo la última de cada destino en cada organización. Se purga por lotes, sin un rol que saltee RLS: lo global de `identity` en su scope y lo de una organización con `TenantJobRunner`.
- **Cada tabla con datos personales nombra quién la purga,** o por qué se conserva (la auditoría, la prueba legal, la identidad anonimizada). Un test lo exige (ADR 0062).
- **Los chats de WhatsApp de una organización** se guardan mientras exista el contacto y se borran con él (D4 de [WhatsApp](../features/whatsapp.md), reconfirmada el 2026-11-18 frente a los 90 días de ArquitecturaBase). Su contenido va cifrado ([ADR 0065](../decisions/0065-contenido-del-chat-cifrado.md), sin programar). La ubicación y el texto de un envío de formulario se vacían antes, al cerrar la captura o al vencer el envío (E14). La copia vinculada sigue la política de [Personal](../features/personal.md).
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
- `IdentityStorageAccessTests` (E2), `SensitiveToStringLoggingTests` (E0).

## Detalle
[Identidad](../features/identidad.md), [Personal](../features/personal.md), [ADR 0035](../decisions/0035-baja-de-cuenta.md) y [ADR 0062](../decisions/0062-retencion-de-los-datos-del-ingreso.md).
