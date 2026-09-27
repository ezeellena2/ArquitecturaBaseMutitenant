# Auditoría

**Regla:** quién y cuándo se completa solo. Cada cambio a una entidad `IAuditable` deja un `AuditEntry` **en la misma transacción**. Las acciones de plataforma dejan además un `SecurityEvent` con motivo.

## Cómo se hace
- La entidad implementa `IAuditable` (y `ISoftDeletable` si se borra lógicamente). No hay que hacer nada más: `AuditableEntityInterceptor` y `AuditTrailInterceptor` completan las marcas y el diff.
- Datos sensibles (tokens, hashes): `[NotAudited]` en la propiedad.
- Evento que no es un cambio de entidad (una exportación, reenviar una invitación): `IAuditLog.Record(action, entityType, entityId, data)` **dentro** del límite.
- Acción de un operador sobre una organización: `PlatformActionGuard` exige el motivo y registra el `SecurityEvent` **antes** de `Enter`.
- Las acciones se traducen con `AuditAction.<código>` en `Audit.resx`.

## Prohibido
- Setear a mano `CreatedBy` o `ModifiedAtUtc`.
- Escribir en `AuditEntries` fuera de los interceptores o de `IAuditLog`.
- Actualizar o borrar un `AuditEntry`: el trigger lo impide.
- Auditar datos personales completos (teléfono, email) en el diff: van enmascarados.

## Copiá de
- `Infrastructure/Persistence/Interceptors/AuditTrailInterceptor.cs` (E2)

## Lo verifica
- `AuditingTests`, `AuditTrailTests`.
- Trigger `prevent_update_delete`.
- `TenantColumnsImmutabilityTests`.

## Detalle
[backend.md §10](../architecture/backend.md#10-auditoría)
