# WhatsApp

Módulo quitable del número de la plataforma, Etapa 8. El alcance y las tareas están en el [plan ejecutable](../plans/2026-10-07-etapa-8-whatsapp.md); T02–T05 aportan composición, persistencia, adaptadores/outbox cifrado y Phone en identidad/recuperación. T06–T08 completan webhook, bot/enlaces y privacidad. El asistente corresponde a E8b y la atención de organizaciones, a E11.

## Contrato publicado en T01

`WhatsAppWebhookController` es el controller del módulo y delega en `IWhatsAppWebhookService`; `WhatsAppApiModule` y `WhatsAppModule` registran cada capa. Esta ficha documenta el área HTTP `whatsapp`, incluso mientras sus rutas permanecen apagadas.

`GET/POST /webhooks/whatsapp` son rutas técnicas globales, anónimas y condicionadas por `InstalledModuleRoute` y un contribuidor disponible, sin FeatureGate de organización. El contrato [OpenAPI](../contracts/openapi.json) las exporta sin secretos. T02 reemplaza la disponibilidad falsa de T01 por opciones reales: sin número de plataforma disponible devuelve 404 común. Mientras T06 está pendiente, el webhook disponible conserva el stub 501, sin sesión ni procesamiento. T06 aporta firma, entrada persistente, ACK y workers. Cada capa registra sus propios servicios y Program tiene una sentencia de composición removible.

El POST declara máximo 5 MiB, firma `X-Hub-Signature-256` sobre bytes originales y deduplicación por IDs Meta/tipo de evento, sin Idempotency-Key. Un mensaje entrante nunca crea sesión. Los enlaces de cuenta común entran como Persona después de Continuar; el núcleo rechaza operadores bajo lock y el bot debe contestar solo «Para entrar, usá la web.», sin enlace ni revelar su tipo. Bot y enlaces reales se completan en T07; el contrato y la prueba de exclusión ya están en T01.

El núcleo conoce capacidades de canales, Phone y puertos neutrales; no compara una clave WhatsApp ni lee sus opciones. La invitación conserva identidad/proof Email y puede entregarse a un Phone verificado de esa misma cuenta. La [ficha de identidad](identidad.md) y el [informe de Contrato](../reviews/2026-10-08-etapa-8-contrato.md) distinguen la conducta existente de los pendientes 501.

## Operación y puerta pendientes

El outbox cifra destino/payload, reclama con lease en una transacción, envía a Cloud API fuera de ella y confirma por token en otra; `Sent` significa aceptación, no delivered. Las plantillas y adaptadores de códigos, invitaciones y avisos usan perfil explícito, resources es/en y DisplayFormatter. Retención, observer, limpieza y prueba de retirada se completan con T08/Integración. El catálogo tiene 16 nombres (1 Authentication, 15 Utility), 32 variantes es/en; el lienzo no implica aprobación en Meta. Usar [plantillas](../operations/whatsapp-plantillas.md) y [configuración/túnel](../operations/configuracion.md) antes de la puerta real. T01 no instaló paquetes ni usó claves reales ni levantó Aspire.

## Verificación

`Etapa8ContractTests`, `OpenApiContractTests`, `AccessDeclarationTests`, `ModuleControllersTests`, `LoginLinkIssuerTests`, `LoginMethodsCapabilitiesTests` e `InvitationDeliveryDestinationTests` verifican la parte publicada. Las suites y el recorrido Meta completo del plan siguen pendientes; E8 no está cerrada.
