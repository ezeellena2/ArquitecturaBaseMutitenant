# Módulos quitables (WhatsApp)

**Regla:** un módulo vive en `Modules/<Módulo>/` en cada capa (más `Domain/<Módulo>`) y se enchufa al núcleo **solo por puertos**. El núcleo nunca referencia `*.Modules.*`.

## Cómo se hace
- **Puertos del núcleo:** `ILoginCodeChannel`, `IInvitationChannel`, `IAccountNoticeChannel`, `IPendingItemSource` (lo que el módulo suma a «Para resolver», [ADR 0040](../decisions/0040-para-resolver.md)) e `IPhoneLinkObserver` (E8). El núcleo trae `"email"` y el módulo registra `"whatsapp"`. El canal declara `AuditMethod` para los éxitos y fallos de código; el núcleo lo resuelve por el canal del destino validado, sin decidirlo por una clave específica ni perderlo si cambia la disponibilidad. La implementación predeterminada conserva `Code` para adaptadores heredados; WhatsApp declara `WhatsAppCode`. Lo verifican `LoginMethodsCapabilitiesTests` con claves sintéticas y `WhatsAppIdentityTests` por HTTP.
- **Avisos de la cuenta:** `IAccountNoticeChannel` recibe el catálogo cerrado real `Application/Models/Notifications/AccountNotice.cs`. Incluye cambios de método, membresía, baja, recuperación y los tres avisos de organización/página pública. La del núcleo atiende `Email`; `WhatsAppAccountNoticeChannel` atiende `Phone` desde E8, con las 16 plantillas de operaciones (ADR 0049). `RecoveryRequested` informa un pedido efectivamente registrado; agregar/recuperar con destino ocupado **no** emite aviso ni código (0043). Sin módulo no se envía a Phone. Exportación e invitación de operador siguen solo correo; las alertas del Asistente usan su propio mensaje/outbox email, sin ampliar AccountNotice con costos.
- **Países de WhatsApp:** los controla el módulo en sus adaptadores y flujos (código por WhatsApp, vínculo y registro por WhatsApp), leyendo `WhatsApp:AllowedCountries` solo dentro de `Modules/WhatsApp`, con su propio error sobre el campo `phone`. Nuevos y pendientes respetan la lista vigente al pedir, reenviar y verificar; achicarla no invalida un Phone ya verificado. `PhoneUsage` del núcleo no nombra WhatsApp ([telefonos](telefonos.md)). El módulo aporta sus países a `GET /api/auth/methods` (`channels: [{ key: "whatsapp", countries: [...] }]`).
- **Registro:** un `AddWhatsAppModule()` por capa, llamado desde `Program.cs`. El módulo aplica sus propias configuraciones EF y sus resources.
- **Tests quitables:** referencias CLR, bindings e inventarios exclusivos viven en el árbol de tests del módulo; los archivos partial aportan sólo sus entradas explícitas. Las guardas y casos mixtos conservan su parte núcleo, controles sintéticos e inventarios exactos de participantes/resources instalados, incluso sin módulos. Retiro físico y roles históricos: [WhatsApp](../guides/quitar-whatsapp.md) y [Asistente](../guides/quitar-asistente.md).
- **Contribución EF (E8):** `IPersistenceModelContributor` aplica solo las configuraciones de su namespace y declara explícitamente cada tipo global con su esquema. Runtime, DesignTime y bootstrap usan el mismo modelo, sin secretos ni red para descubrir la contribución. El DbContext del núcleo no referencia clases del módulo. Apagar PhoneNumberId conserva el esquema. Las guardas reconocen `Application.Modules.<M>.Services`/`Interfaces` por forma de namespace y contrato, con controles negativos; conservan interfaz I*Service, sufijos cerrados, tope de dependencias y UoW solo en servicios. E8b reutiliza E8; no se registra Application desde Infrastructure ni un módulo desde otro.
- **Apagado (E8):** sin `WhatsApp:PhoneNumberId`, `DisabledWhatsAppAvailability` informa "no disponible"; no se registra el emisor ni se publican capacidades del canal. El modelo EF instalado conserva sus tablas. Una ruta técnica global usa `InstalledModuleRoute` y un contribuidor registrado/disponible; no lleva FeatureGate por organización. El webhook apagado responde 404 común aunque OpenAPI lo exporte. Envío e ingreso requieren disponibilidad; el mantenimiento de privacidad de datos existentes continúa mientras el módulo esté instalado.
- **Reglas del producto:** un mensaje entrante nunca abre una sesión. Cuenta común vinculada recibe enlace de un solo uso a Persona; operador recibe mensaje neutro **sin enlace**, sin mencionar Plataforma; desconocido recibe registro personal web o la variante registro cerrado (0049). El operador usa `/plataforma` y su código. Los números van enmascarados y el texto se borra a los 90 días. El bot de E8 no usa el asistente de E8b.
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
- **Una funcionalidad dentro de un módulo quitable** (feature por organización con `IFeatureDefinitionContributor`, `[FeatureGate]`, permisos en los catálogos con la migración de roles, rutas de Business y de Plataforma): el Asistente. La composición, en `Application/Modules/Assistant/AssistantModule.cs`; la feature, en `Application/Modules/Assistant/Services/AssistantFeatureDefinitionContributor.cs`; las rutas, en `Api/Modules/Assistant/Controllers/BusinessAssistantController.cs` y `Api/Modules/Assistant/Controllers/PlatformAssistantController.cs`; los permisos, en `Domain/Authorization/Permissions.cs` y `Domain/Authorization/PlatformPermissions.cs`, con las migraciones `Infrastructure/Persistence/Migrations/20261010203618_SystemRoleAssistantPermissions.cs` (roles de sistema) y `Infrastructure/Persistence/Migrations/20261010214712_PlatformOperatorAssistantPermissions.cs` (operadores). El retiro, en [`quitar-asistente.md`](../guides/quitar-asistente.md).
- **Webhook y outbox:** WhatsApp, con `Application/Modules/WhatsApp/WhatsAppModule.cs`, `Api/Modules/WhatsApp/Controllers/WhatsAppWebhookController.cs` y `Infrastructure/Modules/WhatsApp/WhatsAppChannelSender.cs` (E8) · la guía [`quitar-whatsapp.md`](../guides/quitar-whatsapp.md) (E8)

## Lo verifica
- `ModuleIsolationTests` (E8).
- `ModuleControllersTests`, `WhatsAppStartupTests` y `Etapa8ContractTests` (E8): ruta técnica con contribuidor, apagado/retirada, configuración y ausencia de sesión en webhook.
- La prueba de fuego de la Etapa 8: quitar el módulo y que el núcleo siga en verde.
- `WhatsAppWebhookTests` (E8): firma, idempotencia, 413, 401.
- `WhatsAppTemplateTests` (E8): cada plantilla configurada y el orden de sus variables.
- `WhatsAppBotTests`, `WhatsAppCloudClientTests` y `WhatsAppOutboxTests` (E8): operador neutro, propietario original, reply IDs y ventana verificada también antes del envío.
- `WhatsAppPhoneLinkObserverTests`, `WhatsAppAccountLifecycleTests` y `WhatsAppRetentionTests` (E8): vínculo sin apropiación, retiro/rollback, baja por propietario original y retención fija por recepción, con canal apagado.
- `WhatsAppPersistenceTests` (E8): igualdad de las vías reales; composición WA + contribuidor técnico de test con inventario neutral explícito en tres contextos, configuración una vez y capacidades únicas. El fake no amplía el descubrimiento productivo de assemblies.

## Detalle
[backend.md §15](../architecture/backend.md#15-whatsapp-un-módulo-quitable) · ADR 0007
