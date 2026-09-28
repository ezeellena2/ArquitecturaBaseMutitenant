# Módulos quitables (WhatsApp)

**Regla:** un módulo vive en `Modules/<Módulo>/` en cada capa (más `Domain/<Módulo>`) y se enchufa al núcleo **solo por puertos**. El núcleo nunca referencia `*.Modules.*`.

## Cómo se hace
- **Puertos del núcleo:** `ILoginCodeChannel`, `IInvitationChannel`, `IAccountNoticeChannel` e `IPhoneLinkObserver`. El núcleo trae `"email"` y el módulo registra `"whatsapp"`.
- **Avisos de la cuenta:** `IAccountNoticeChannel` recibe un `AccountNotice` cerrado (`LoginMethodChanged`, `ReviewLoginMethods`, `DeletionRequested`, `DeletionCancelled`, `AccountDeleted`, `RecoveryReceived`, `RecoveryApproved` y `RecoveryRejected`) y cada implementación atiende un tipo de método: la del núcleo, `Email`; la del módulo (`WhatsAppAccountNoticeChannel`), `Phone`. Sin el módulo, los teléfonos no reciben aviso y no es un error. "Exportación lista" va solo por correo.
- **Países de WhatsApp:** los controla el módulo en sus adaptadores y flujos (código por WhatsApp, vínculo y registro por WhatsApp), leyendo `WhatsApp:AllowedCountries` solo dentro de `Modules/WhatsApp`, con su propio error sobre el campo `phone`, y solo para un número **nuevo**: achicar la lista no invalida un número existente. `PhoneUsage` del núcleo no nombra WhatsApp ([telefonos](telefonos.md)). El módulo aporta sus países a `GET /api/auth/methods` (`channels: [{ key: "whatsapp", countries: [...] }]`).
- **Registro:** un `AddWhatsAppModule()` por capa, llamado desde `Program.cs`. El módulo aplica sus propias configuraciones EF y sus resources.
- **Apagado:** sin `WhatsApp:PhoneNumberId` se registran las implementaciones de `Disabled/`, que responden "no disponible" sin lanzar.
- **Reglas del producto:** un mensaje entrante nunca abre una sesión, solo produce un enlace de un solo uso. Los números van enmascarados y el texto se borra a los 90 días.
- **Envío:** siempre por el outbox (canal `"whatsapp"`), cifrado, nunca en línea con el request.
- **Plantillas:** un mensaje que la plataforma inicia (fuera de las 24 horas posteriores a que la persona escribió) usa una plantilla aprobada por Meta. Se crea en Meta al empezar la etapa que la usa, antes de programar el envío, y se registra en `WhatsApp:Templates:<Nombre>` y en el catálogo de plantillas del módulo, con el orden de sus variables. Cuáles son y cómo se crean: [whatsapp-plantillas.md](../operations/whatsapp-plantillas.md).

## Prohibido
- `using …Modules.WhatsApp` en el núcleo.
- `if (channel == "whatsapp")` en el núcleo, o leer `WhatsApp:*` (como `AllowedCountries`) fuera de `Modules/WhatsApp`.
- Registrar servicios de Application desde Infrastructure.
- Llamar a la Cloud API desde un servicio del núcleo.
- Un mensaje libre fuera de la ventana de 24 horas.
- Editar una plantilla aprobada: se crea otra con sufijo (`_v2`) y se cambia la configuración.

## Copiá de
- `Application/Modules/WhatsApp/WhatsAppModule.cs` (E8) · la guía [`quitar-whatsapp.md`](../guides/quitar-whatsapp.md) (E8)

## Lo verifica
- `ModuleIsolationTests`.
- La prueba de fuego de la Etapa 8: quitar el módulo y que el núcleo siga en verde.
- `WhatsAppWebhookTests` (firma, idempotencia, 413, 401).
- El test del catálogo de plantillas: cada plantilla configurada y el orden de sus variables.

## Detalle
[backend.md §15](../architecture/backend.md#15-whatsapp-un-módulo-quitable) · ADR 0007
