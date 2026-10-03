# Auditoría

**Regla:** quién y cuándo se completa solo. Cada cambio a una entidad `IAuditable` de tenant deja un `AuditEntry` **en la misma transacción**. Las entidades globales, como `PlatformSettings`, generan un `SecurityEvent` con motivo **en esa transacción**, en lugar de un `AuditEntry` de tenant. Las demás acciones de plataforma también dejan un `SecurityEvent` con motivo.

## Cómo se hace
- La entidad implementa `IAuditable` (y `ISoftDeletable` si se borra lógicamente). No hay que hacer nada más: `AuditableEntityInterceptor` y `AuditTrailInterceptor` completan las marcas y el diff.
- Un cambio privado deja una entrada en su tenant; uno público, en la organización; uno compartido, una por cada parte distinta. `AuditTrailInterceptor` deriva las contrapartes del ChangeTracker y fija `app.audit_counterpart_tenant_ids` **local a la transacción** antes del único `SaveChanges`. Una política RLS adicional permite solo esos INSERT en `tenant.AuditEntries`; las lecturas siguen limitadas al tenant activo. Esta lista nunca sale directamente de parámetros HTTP: el caso de uso valida la contraparte antes de crear el dato compartido.
- Un cambio de `PlatformSettings` exige que el caso de uso agregue un `SecurityEvent` de tipo `PlatformSettingsChanged`, con motivo, en el mismo `IUnitOfWork`; el interceptor rechaza el guardado si falta. El seed inicial registra un evento con actor `System`.
- El seed inicial que crea o habilita al operador registra `PlatformOperatorGranted`, actor `System` y motivo `Initial platform owner seed` en la misma UoW. Un método de correo existente debe estar verificado y no gestionado por una organización.
- Datos sensibles (tokens, hashes): `[NotAudited]` en la propiedad.
- Evento que no es un cambio de entidad (una exportación, reenviar una invitación): `IAuditLog.Record(action, entityType, entityId, data)` **dentro** del límite.
- Acción de un operador sobre una organización: `PlatformActionGuard` exige el motivo y registra el `SecurityEvent` **antes** de `Enter`.
- El `SecurityEvent` guarda `TargetTenantId` o `TargetAccountId` según el destino y un resumen `Changes` JSON de claves técnicas con `before`/`after`. `SecurityEventChanges` admite booleanos, cantidades, enums y fechas UTC; omite valores iguales y no acepta nombres, contactos ni texto libre. El motivo interno se conserva aparte y no entra en ese resumen ni en correos.
- `MaskedIpAddress` conserva IPv4 sin el último octeto e IPv6 sin los últimos 80 bits; IPv4 mapeada se normaliza. Una IP ausente o inválida queda null. `AuditTrailInterceptor` completa la IP de los eventos nuevos con `IRequestInfo`, sin alterar su diff ni los eventos ya guardados. Las filas históricas sin evidencia mantienen diff vacío y campos desconocidos null.
- En Plataforma, el tipo «Ingresos» se consulta desde `identity.LoginAudits`, con método/resultado y códigos de error técnicos, sin dirección completa ni código OTP. No se inventan ingresos como SecurityEvents.
- Las acciones se traducen con `AuditAction.<código>` en `Audit.resx`.

## Prohibido
- Setear a mano `CreatedBy` o `ModifiedAtUtc`.
- Escribir en `AuditEntries` fuera de los interceptores o de `IAuditLog`.
- Actualizar o borrar un `AuditEntry`: el trigger lo impide.
- Auditar datos personales completos (teléfono, email) en el diff: van enmascarados.

## Copiá de
- `Infrastructure/Persistence/Interceptors/AuditTrailInterceptor.cs` (E2)

## Lo verifica
- `AuditingTests` (E2), `AuditTrailTests` (E2).
- Trigger `prevent_update_delete` (E2).
- `TenantColumnsImmutabilityTests` (E2).

## Detalle
[backend.md §10](../architecture/backend.md#10-auditoría)
