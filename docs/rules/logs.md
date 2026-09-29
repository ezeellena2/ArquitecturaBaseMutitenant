# Logs

**Regla:** se loguea solo con `[LoggerMessage]`. Cada método público de un servicio va envuelto en `OperationLog.RunAsync`. Nunca se registra un secreto ni un dato personal completo.

## Cómo se hace
- **En un servicio:** `OperationLog.RunAsync(logger, timeProvider, "UpdateRole", async () => { … })`. Registra el inicio, la duración al terminar y el código de error si falla. La cancelación se propaga sin registrar el evento de error 103. Es una línea por método; `TimeProvider` se inyecta.
- **Un log propio:** `[LoggerMessage(Level = …, Message = "…")] private static partial void LogX(ILogger logger, …);` en una clase `partial`.
- Mensajes en inglés, con parámetros estructurados (`{RoleId}`), no concatenados.
- Teléfonos enmascarados con `IPhoneNumberParser.Mask` (`+54 9 11 •••• 6789`); correos enmascarados con la primera letra y el dominio (`j***@gmail.com`), como dice [emails](emails.md).
- El `TenantId` va en el scope del request (lo pone el middleware) y en la traza (`tenant.id`).

## Prohibido
- `logger.LogInformation(...)` directo.
- Loguear el request entero, códigos, tokens, enlaces de ingreso, contraseñas, emails o teléfonos completos.
- `Include Error Detail` en la cadena de conexión.

## Copiá de
- `Application/Common/Logging/OperationLog.cs` (E1)

## Lo verifica
- `CA1848` (E0) como warning con `TreatWarningsAsErrors`: un `logger.LogX` directo rompe el build.
- `SensitiveToStringLoggingTests` (E1): instancia los contratos HTTP de entrada con un valor centinela y comprueba que `ToString()` no lo revele. `ControllerInputContractTests` (E1) comprueba que los parámetros de entrada sean contratos de Api.
- `OperationLogTests` (E1): una cancelación conserva la excepción y no produce el evento 103; una excepción real sí lo produce sin revelar su mensaje.

## Detalle
[backend.md §17](../architecture/backend.md#17-logging-openapi-health-rate-limiting-caché)
