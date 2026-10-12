# Auditoría

**Regla:** quién y cuándo se completa solo. Cada cambio a una entidad `IAuditable` de tenant deja un `AuditEntry` **en la misma transacción**. Las entidades globales, como `PlatformSettings`, generan un `SecurityEvent` con motivo **en esa transacción**, en lugar de un `AuditEntry` de tenant. Las demás acciones de plataforma también dejan un `SecurityEvent` con motivo.

## Cómo se hace
- La entidad implementa `IAuditable` (y `ISoftDeletable` si se borra lógicamente). No hay que hacer nada más: `AuditableEntityInterceptor` y `AuditTrailInterceptor` completan las marcas y el diff.
- Un guardado deja una sola entrada por entidad: no se suma a mano otra con `IAuditLog` para el mismo cambio. El diff de una modificación lleva solo las propiedades cuyo valor cambió, aunque `MarkUpdated` marque la entidad entera, y no repite las marcas de quién y cuándo, que ya están en la entrada. Una modificación sin ningún valor distinto (un `MarkUpdated` que solo cambia la versión) no deja entrada.
- Un cambio privado deja una entrada en su tenant; uno público, en la organización; uno compartido, una por cada parte distinta. `AuditTrailInterceptor` deriva las contrapartes del ChangeTracker y fija `app.audit_counterpart_tenant_ids` **local a la transacción** antes del único `SaveChanges`. Una política RLS adicional permite solo esos INSERT en `tenant.AuditEntries`; las lecturas siguen limitadas al tenant activo. Esta lista nunca sale directamente de parámetros HTTP: el caso de uso valida la contraparte antes de crear el dato compartido.
- Un cambio de `PlatformSettings` exige que el caso de uso agregue un `SecurityEvent` de tipo `PlatformSettingsChanged`, con motivo, en el mismo `IUnitOfWork`; el interceptor rechaza el guardado si falta. El seed inicial registra un evento con actor `System`.
- Un módulo puede aportar globales auditables con `IGlobalSecurityAudited` ([ADR 0050](../decisions/0050-auditoria-global-de-modulos.md)): declara el tipo y destino del evento requerido por implementación explícita de la interfaz, sin columnas ni referencias del núcleo al módulo. El interceptor exige evento/destino/motivo en la misma transacción y conserva el sello de auditoría, sin fabricar un tenant. La clasificación global sigue exigiendo cada tipo exacto del contribuidor; la marca es incompatible con clases privadas, públicas o compartidas. T02 lo verifica con ausencia de evento y con configuraciones globales reales.
- Datos sensibles (tokens, hashes): `[NotAudited]` en la propiedad.
- Evento que no es un cambio de entidad (una exportación, reenviar una invitación): `IAuditLog.Record(action, entityType, entityId, data)` **dentro** del límite. Un hecho que pasó antes de registrarse lleva su fecha real (`occurredAtUtc`); los demás, el ahora del reloj.
- Acción de un operador sobre una organización: `PlatformActionGuard` exige el motivo y registra el `SecurityEvent` **antes** de `Enter`.
- El `SecurityEvent` guarda `TargetTenantId` o `TargetAccountId` según el destino y un resumen `Changes` JSON de claves técnicas con `before`/`after`. `SecurityEventChanges` admite booleanos, cantidades, enums y fechas UTC; omite valores iguales y no acepta nombres, contactos ni texto libre. El motivo interno se conserva aparte y no entra en ese resumen ni en correos.
- `MaskedIpAddress` conserva IPv4 sin el último octeto e IPv6 sin los últimos 80 bits; IPv4 mapeada se normaliza. Una IP ausente o inválida queda null. `AuditTrailInterceptor` completa la IP de los eventos nuevos con `IRequestInfo`, sin alterar su diff ni los eventos ya guardados. Las filas históricas sin evidencia mantienen diff vacío y campos desconocidos null.
- Las acciones se traducen con `AuditAction.<código>` en `Audit.resx`.
- **El «Qué» de una entrada** sale de `AuditSummary.<Entidad>.<variante>`, después `.<acción>` y después `AuditSummary.<Entidad>` (`AuditTexts.Summary`). Un cambio de un solo campo usa como variante el nombre del campo: tiene su texto solo si el tablero lo dibuja («Cambió la zona de …», «Cambió el idioma y la región predeterminados»); si no, vale el de la acción. Un texto sin `{0}` no necesita el nombre de lo afectado.

## Prohibido
- Setear a mano `CreatedBy` o `ModifiedAtUtc`.
- Escribir en `AuditEntries` fuera de los interceptores o de `IAuditLog`.
- Actualizar o borrar un `AuditEntry`: el trigger lo impide.
- Auditar datos personales completos (teléfono, email) en el diff: van enmascarados con `PersonalDataMask`.

## Copiá de
- `Infrastructure/Persistence/Interceptors/AuditTrailInterceptor.cs` (E2)

## Lo verifica
- `HarnessTests` (E0): formato y referencias de la ficha.

## Detalle
[backend.md §10](../architecture/backend.md#10-auditoría)
