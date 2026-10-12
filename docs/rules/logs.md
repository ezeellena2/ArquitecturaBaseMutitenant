# Logs

**Regla:** se loguea solo con `[LoggerMessage]`. Cada método público de un servicio va envuelto en `OperationLog.RunAsync`. Nunca se registra un secreto ni un dato personal completo.

## Cómo se hace
- **En un servicio:** `OperationLog.RunAsync(logger, timeProvider, "UpdateRole", async () => { … })`. Registra el inicio, la duración al terminar y el código de error si falla. La cancelación se propaga sin registrar el evento de error 103. Es una línea por método; `TimeProvider` se inyecta.
- **Un log propio:** `[LoggerMessage(Level = …, Message = "…")] private static partial void LogX(ILogger logger, …);` en una clase `partial`.
- Mensajes en inglés, con parámetros estructurados (`{RoleId}`), no concatenados.
- Correos y teléfonos, siempre con `PersonalDataMask` (`Application/Common/Text`): el correo con dos letras y el dominio (`lu•••@gmail.com`), y el teléfono con sus últimos cuatro dígitos (`•••• 6789`). Es el mismo formato de las pantallas, los avisos y la auditoría.
- El `TenantId` va en el scope del request (lo pone el middleware) y en la traza (`tenant.id`).
- **Excepciones inesperadas:** HTTP, `OperationLog` y mantenimiento usan `ExceptionLog.Write`: tipo y stack de cada excepción interna, nunca `Message`, `Data`, `ToString()` ni el objeto Exception. Los identificadores técnicos van en un scope. Los workers periódicos heredan de `PeriodicWorker`: la cancelación termina el servicio, cualquier otro fallo se registra y permite la siguiente vuelta; las señales de entrada conservan su espera propia.
- OpenIddict emite parámetros OIDC y claims personales en sus trazas internas. Su categoría se limita a `Warning` mediante `LoggerFilterOptions` después de cargar la configuración; una captura de pruebas en `Trace` sigue observando los logs de la aplicación sin exponer esos valores.

## Prohibido
- `logger.LogInformation(...)` directo.
- Loguear el request entero, códigos, tokens, enlaces de ingreso, contraseñas, emails o teléfonos completos.
- `Include Error Detail` en la cadena de conexión.
- Entregar una excepción al proveedor de logs o registrar su mensaje; copiar bucles periódicos con catches parciales que puedan detener el host.

## Copiá de
- `Application/Common/Logging/OperationLog.cs` (E1)

## Lo verifica
- `CA1848` (E0), `SensitiveToStringLoggingTests` (E1), `ControllerInputContractTests` (E1)

## Detalle
[backend.md §17](../architecture/backend.md#17-logging-openapi-health-rate-limiting-caché)
