# Módulos quitables (WhatsApp)

**Regla:** un módulo vive en `Modules/<Módulo>/` en cada capa (más `Domain/<Módulo>`) y se enchufa al núcleo **solo por puertos**. El núcleo nunca referencia `*.Modules.*`.

## Cómo se hace
- **Puertos del núcleo:** `ILoginCodeChannel`, `IInvitationChannel` e `IPhoneLinkObserver`. El núcleo trae `"email"` y el módulo registra `"whatsapp"`.
- **Registro:** un `AddWhatsAppModule()` por capa, llamado desde `Program.cs`. El módulo aplica sus propias configuraciones EF y sus resources.
- **Apagado:** sin `WhatsApp:PhoneNumberId` se registran las implementaciones de `Disabled/`, que responden "no disponible" sin lanzar.
- **Reglas del producto:** un mensaje entrante nunca abre una sesión, solo produce un enlace de un solo uso. Los números van enmascarados y el texto se borra a los 90 días.
- **Envío:** siempre por el outbox (canal `"whatsapp"`), nunca en línea con el request.

## Prohibido
- `using …Modules.WhatsApp` en el núcleo.
- `if (channel == "whatsapp")` en el núcleo.
- Registrar servicios de Application desde Infrastructure.
- Llamar a la Cloud API desde un servicio del núcleo.

## Copiá de
- `Application/Modules/WhatsApp/WhatsAppModule.cs` (E8) · la guía [`quitar-whatsapp.md`](../guides/quitar-whatsapp.md) (E8)

## Lo verifica
- `ModuleIsolationTests`.
- La prueba de fuego de la Etapa 8: quitar el módulo y que el núcleo siga en verde.
- `WhatsAppWebhookTests` (firma, idempotencia, 413, 401).

## Detalle
[backend.md §15](../architecture/backend.md#15-whatsapp-un-módulo-quitable) · ADR 0007
