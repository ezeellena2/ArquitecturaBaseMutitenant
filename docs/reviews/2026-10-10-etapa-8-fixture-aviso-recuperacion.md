# E8 — control privado del aviso de recuperación y correo de respaldo

Soporte exclusivo de Testing para el runner del navegador. No agrega rutas HTTP, contrato Swagger, migraciones, cambios productivos ni transporte Meta/SMTP. La fixture conserva sus sender prohibidos y Testing ya excluye el worker periódico de recuperación.

## Resultado

- `recoveryNotice`: requiere escenario existente, Phone nuevo exacto y `recoveryId` no vacío de un pedido Pending propio posterior al cursor. Antes de cualquier efecto verifica esos límites. Llama al servicio real fuera de una UoW de fixture; relee pedido/marca sin tracking después del commit y cuenta exclusivamente filas nuevas cifradas del outbox de la cuenta objetivo. Devuelve `marked`, `queuedNotices`, `beforeVersion`, `afterVersion`; no devuelve contactos ni payloads. Repetir conserva la marca y no genera otro aviso.
- `methodEmailCode`: exige `backup-email-1440`/`backup-email-390`, Email sintético exacto de ese ancho y `methodId` no vacío del POST real. Ata el Phone principal global verificado y su dueño al inicio; requiere método Email pendiente del mismo dueño, código VerifyDestination y outbox cifrado posteriores al cursor. Sólo devuelve el OTP por stdin privado; nunca usa el helper que despacha correos.
- Los nuevos datos privados sobrescriben `ToString` para evitar exponer pruebas o destinos. Se conserva el protocolo anterior de códigos, bot, tickets y cursores.

## Decisiones tomadas

`LoginMethod` no tiene `CreatedAtUtc`. Para rechazar métodos históricos, el inicio del escenario Email registra sus IDs existentes y el dueño del Phone; la lectura exige un ID nuevo y prueba/outbox posteriores al cursor. Se usa el modelo existente, sin inventar una entidad o fecha productiva. El aviso procesa la tanda global real de hasta 50 pedidos en la base aislada; el guard de fixture no transforma ese servicio en uno limitado a un escenario.

## Verificación

El RED de la clase exacta de guardas ejecutó 15 casos: 13 correctos y 2 fallidos por rechazar los comandos todavía ausentes. Se conservan por separado el intento inicial sin casos (build) y el selector de método sin coincidencias (0 casos), que no constituyen RED.

La primera corrida de integración ejecutó 23 casos: 22 correctos y 1 fallido en la preparación del operador (retorno de ingreso inválido). Las dos corridas intermedias también dieron 22/23: faltaba aceptar los legales vigentes del operador seed por HTTP y el selector del test suponía un campo que la respuesta pública enmascara. La preparación usa ahora ingreso/OIDC, GET de pendientes legales y aceptación HTTP reales; la búsqueda del pedido usa el contrato existente. Son fallas separadas de preparación del test, sin ampliar producto ni sumar sus contadores.

La corrida final exacta de las tres clases ejecutó **23 correctos, 0 fallidos, 0 omitidos**, exit 0, contra PostgreSQL Testcontainers. Los builds explícitos de integración y API con analizadores fueron seriales, sin restore ni compilador compartido: **0 advertencias, 0 errores**. Comandos, stdout, exits y contadores de cada intento están separados en `.local/reviews/e8b-contract/e8-browser-notice-email-ipc-files.json`.

Los tests cubren registro Phone y OIDC reales, POST de Agregar correo → OTP privado → verificación HTTP 204 en ambos anchos, rechazo de históricos/ajenos/verificados/cambio de principal sin envío, guardas de recuperación sin efectos, aviso/marca/outbox cifrado/xmin, repetición y aprobación HTTP con la misma versión leída por Plataforma. Un envelope posterior de otra cuenta al mismo Email no sustituye el OTP propio: el reader filtra también `UserId` antes de descifrar, con negativo significativo en ambos anchos.

OpenAPI conserva SHA-256 `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`. El `Role.cs` ajeno conserva SHA-256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`.

## Límites

Esta verificación focal no acredita el E2E Playwright integral, galería, recorrido de usuario, entrega Meta ni cierre de E8. Front/Integración ejecutan esos recorridos sobre el par publicado. No se levantó Aspire, no se cambiaron secretos y no se realizó push.
