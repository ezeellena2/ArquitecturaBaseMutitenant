# ADR 0062 · Retención de los datos del ingreso

**Estado:** aceptada por el usuario el 2026-11-18 (pregunta 17 del traspaso 15), con los plazos de ArquitecturaBase. Toma la N16 y la N31 de allá (traspaso 14, `02-nucleo.md` §5), sin su rol que saltea el aislamiento. Todavía no está programada: la tarea está en el [backlog](../backlog.md#traspaso-15-el-núcleo-para-programar). Nada de ArquitecturaBase pasó todavía su prueba integral.

## Contexto

Hoy nada borra:
- los códigos de ingreso (`identity.LoginCodes`);
- los ingresos (`identity.LoginAudits`);
- las autorizaciones y los tokens de OpenIddict;
- las invitaciones cerradas de cada organización.

Cada código que pide cualquier persona, y cada token vencido, queda para siempre. El multitenant ya purga otras tablas: el texto de WhatsApp a los 90 días, los avisos leídos a los 90, la idempotencia, los contadores de cuotas y el Asistente.

En ArquitecturaBase el worker de retención corre sin una organización y con un rol que saltea RLS. Acá eso choca con la [0010](README.md) y con la prueba del E10 de que `mt_app` no tiene bypass.

## Decisión

1. **Plazos:**
   - los códigos de ingreso se borran a los 30 días;
   - los ingresos (`LoginAudits`), a los 12 meses, salvo el último de cada cuenta. Acá no guardan IP ni navegador ([identidad](../features/identidad.md));
   - las autorizaciones y los tokens de OpenIddict, a los 14 días de vencidos o revocados;
   - las invitaciones, a los 30 días de cerradas (aceptadas, revocadas o vencidas), salvo la última de cada destino en cada organización. Una pendiente nunca se borra.
2. **Sin saltear el aislamiento.** Lo global de `identity` (códigos, ingresos y OpenIddict) se purga en un scope global, como los demás workers de identidad. Las invitaciones, que son privadas de cada organización, se purgan con `TenantJobRunner`, una organización por vez, como `InvitationExpiryWorker`. No hay un rol que saltee RLS.
3. **Por lotes,** en transacciones cortas, con los índices que la purga necesite para no recorrer la tabla entera (los parciales de la N31). Sigue la regla de los workers periódicos entre copias de [backend.md](../architecture/backend.md). El log de una purga dice cuántas filas borró y, si falla, solo el tipo de la excepción ([logs](../rules/logs.md)).
4. **Cada tabla con datos personales nombra quién la purga,** o por qué se conserva. Un test lo exige, como `DataClassificationTests`.
5. **Lo que no se purga:**
   - la auditoría y los `SecurityEvent`, que son append-only ([0015](README.md));
   - las aceptaciones legales, que son la prueba (al eliminar la cuenta pierden solo la IP y el navegador);
   - la identidad anonimizada de una cuenta eliminada ([0035](0035-baja-de-cuenta.md)).
6. **La auditoría de Plataforma,** que muestra los ingresos, dice que se conservan doce meses.

## Consecuencias

- Un worker nuevo, o dos (uno global y otro por organización), heredado de `PeriodicWorker`.
- Antes de borrar invitaciones, quien programa revisa que la auditoría de la organización siga nombrando bien a quien no tiene nombre: hoy lo nombra por el correo de su invitación ([organizaciones](../features/organizaciones.md#presentación-de-auditoría-de-organización)). La proyección `identity.LoginInvitations` sigue a su invitación.
- La auditoría de Plataforma muestra como mucho un año de ingresos.

## Alternativas descartadas

- **Un rol de base de datos que saltee RLS,** como en ArquitecturaBase: rompe la 0010.
- **Guardar los ingresos más de un año:** el usuario eligió los plazos de ArquitecturaBase.
- **Dejarlo para antes de producción:** las tablas crecen desde el primer día, con datos de todas las organizaciones.
