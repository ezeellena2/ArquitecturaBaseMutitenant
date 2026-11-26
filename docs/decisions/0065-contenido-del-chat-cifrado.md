# ADR 0065 · El contenido del chat de WhatsApp se guarda cifrado

**Estado:** aceptada por el usuario el 2026-11-18 (preguntas 55 a 58 del traspaso 15, tanda 5). Toma la ADR 0030 de ArquitecturaBase (H16 de su revisión de WhatsApp) y la adapta a muchas organizaciones. Precisa la [0038](0038-whatsapp-de-atencion.md) y deja la retención como está (D4). Programada en la B8 de la [base de la 12, segunda parte](../plans/formularios/base-de-la-12-segunda-parte.md) (2026-11-24). Nada de ArquitecturaBase pasó todavía su prueba integral.

## Contexto

Hoy el texto de los chats se guarda en claro: `Text` de `tenant.WhatsAppChatMessages`, `LastMessagePreview` de cada chat, sus gemelas de `platform` y `Text` de la copia vinculada (`engagement.WhatsAppLinkedMessages`). Quien llega a una copia de la base, como un respaldo o un usuario de reportes, lee los chats de todas las organizaciones. Con la D4 el texto no vence, así que esa copia tiene toda la historia.

Ya se cifran la entrada global (`EncryptedText`), la salida del outbox (destino y contenido), el token y el PIN de cada número, la clave propia de IA y la respuesta de un Flow de la E14 (`ProtectedFlowResponse`). El anillo de claves es uno, compartido entre copias y protegido con un certificado ([configuración](../operations/configuracion.md)). En ArquitecturaBase, además, el contenido vence a los 90 días.

## Decisión

1. **Se cifra todo el contenido del chat** con Data Protection, en todas las tablas del módulo:
   - el texto de cada mensaje, con las notas internas, lo que dijo el bot y los valores de una plantilla en la conversación;
   - la vista previa del último mensaje de cada chat;
   - lo mismo en Plataforma y en la copia vinculada de «Mensajes»;
   - lo que sumen la E14 a la E25 a las tablas de WhatsApp nace cifrado: la ubicación y el texto de un envío, lo que transcribe, redacta o explica el asistente, las filas de una planilla y lo leído de un documento mientras lo guarda el chat.

   No alcanza a las tablas de Formularios: las respuestas y `FormDocuments` (E25) son datos de la organización, con RLS, como las respuestas, y la bandeja de documentos busca en SQL en lo leído y en sus dígitos.
2. **Quedan en claro** (pregunta 57):
   - el nombre y el número del contacto, porque la bandeja y Contactos buscan por ellos;
   - quién escribió (`AuthorName`), las etiquetas y el consentimiento, que son de la organización;
   - los identificadores técnicos, los estados, las fechas y los datos del negocio, como el nombre del formulario o el número de una respuesta.
3. **La clave es la de la instalación, con el propósito atado a cada organización** (pregunta 55):
   - en `tenant.*`, `ArquitecturaBaseMultitenant.WhatsApp.ChatContent.v1` con la organización como subpropósito, como `AssistantKeyProtector` y `WhatsAppTokenProtector`;
   - en `platform.*`, el mismo propósito con un subpropósito de Plataforma;
   - en la copia vinculada (`engagement.*`), un propósito propio del esquema, sin la organización: la persona la lee desde su espacio personal, sin la organización en el alcance. La protege la RLS de las partes, y contra una copia de la base alcanza el anillo.

   Una clave por organización se suma si un cliente la exige.
4. **Se cifra en un conversor de EF**, como allá, así entidades, repositorios y lectores siguen con texto y una lectura nueva no puede olvidarse de descifrar.
   - El conversor toma la organización del mismo contexto que fija `app.tenant_id` para la RLS: la petición, `ITenantScope.Enter` o `TenantJobRunner`. Nunca de la fila, del pedido ni del Host.
   - Leer o guardar contenido de `tenant.*` sin organización es un bug y falla.
   - EF arma el modelo una vez por proceso, así que el protector se resuelve en cada conversión, no al arrancar.
   - Si el conversor no puede seguir al alcance de forma segura, el contenido se cifra en los repositorios y lectores del módulo con un tipo propio, que no es `string`, así una lectura que se olvida no compila. Quien programa lo anota acá.
   - **Programado (B8):** el conversor sigue al alcance y no hizo falta el tipo propio. `WhatsAppChatContentConverter` toma la base que está leyendo o guardando (`PersistenceOperation`): cada consulta la fija desde su compilador (`OperationQueryCompiler`, también en cada paso de una lista que se recorre después) y cada guardado, desde `ApplicationDbContext`. De esa base sale la organización, la misma que lee `TenantConnectionInterceptor`, así que dos bases a la vez en el mismo flujo no se mezclan. Lo prueban `Tenancy/WhatsAppChatContentIsolationTests` en la petición, en `ITenantScope.Enter`, en `TenantJobRunner` y con dos bases a la vez.
5. **El contenido no se busca ni se compara en SQL;** solo se pregunta si es nulo. La bandeja busca por nombre y número, que quedan en claro. Lo frena `WhatsAppChatContentTests` en los operadores con paréntesis y en la sintaxis de consulta (`where`, `orderby`, `join … on … equals` y `group … by`), porque sobre el cifrado una búsqueda o un orden no fallan: no encuentran nada o salen cambiados.
6. **Lo que el anillo ya no abre se lee como vacío,** sin romper la carga de su fila: claves perdidas, una base restaurada sin el certificado o un cifrado copiado a la fila de otra organización.
7. **Los chats que ya están guardados** (pregunta 56): hoy son de prueba, en las bases de desarrollo, y la migración los vacía, siempre que el cifrado entre antes de la prueba con un número real de la E10. Si la E10 sale antes, se convierten, también la copia vinculada y los chats de Plataforma.
8. **La retención no cambia** (pregunta 58, el usuario mantuvo la D4): el texto de los chats de una organización se guarda mientras exista el contacto y se borra con él, y en Plataforma, mientras exista la conversación. De ArquitecturaBase se trae solo H13: la ubicación y el texto de un envío de formulario se vacían al cerrar la captura o al vencer el envío (E14). La copia vinculada sigue la política de [Personal](../features/personal.md).

## Consecuencias

- Un respaldo sin el certificado no se lee. Si se pierden las claves, se pierde el contenido de todos los chats, no el de 90 días como allá: con la D4, el ensayo de la restauración con el certificado ([runbook](../operations/runbook.md)) importa más.
- Dos versiones conviven ([0060](0060-dos-versiones-conviven.md)): la columna cifrada se agrega, se escribe un despliegue después de que todas las copias la leen y la de texto se borra al final. Mientras no haya datos reales, puede ir en un solo paso.
- Cifrar y descifrar cuesta algo por fila; la bandeja lee páginas acotadas.
- Se prueba que dos organizaciones no leen el contenido de la otra: un cifrado copiado a la fila de otra organización se lee vacío.

## Alternativas descartadas

- **Una clave por organización** (pregunta 55): borrar una organización borraría su clave, pero suma un anillo y una rotación por organización.
- **Cifrar también el nombre** (pregunta 57): rompe la búsqueda de la bandeja y la de Contactos.
- **Vencer el contenido a los 90 días** (pregunta 58), como ArquitecturaBase: el usuario prefirió que el operador vea toda la historia del contacto.
- **Cifrar en PostgreSQL (pgcrypto):** la clave viajaría en las consultas.

Detalle: [WhatsApp](../features/whatsapp.md#vínculos-y-privacidad) y [datos personales](../rules/datos-personales.md).
