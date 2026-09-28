# Result y errores

**Regla:** las reglas de negocio devuelven `Result` o `Result<T>` con un `Error`. Nunca lanzan. Las excepciones son solo para bugs y fallas de infraestructura.

## Cómo se hace
- Declarar el error en `Domain/<Área>/<Entidad>Errors.cs`: una constante `…Code` y un `static readonly Error`, o una fábrica si lleva metadata.
- Código **`Area.Entidad.Motivo`** (tres partes o más), estable (`Roles.Role.HasUsers`). Es la clave en `Errors.resx` **y** `Errors.en.resx`.
- **Claves reservadas**, la única excepción al formato (lista cerrada, `ReservedKeys` de `ErrorCodeTests`): `Title.<ErrorType>`, `Validation.Failed`, las de `ApiErrorCodes` (`General.Unexpected`, `General.ConcurrencyConflict`, `Request.Invalid`, `Request.InProgress`, `Request.IdempotencyKeyRequired`, `Request.IdempotencyKeyReused` y `Http.*`) y `Legal.AcceptanceRequired`. Un código nuevo nunca entra en esa lista.
- `Description` en inglés: es el respaldo si falta la traducción.
- Devolverlo en forma implícita: `return RoleErrors.NotFound;`.
- El controller responde con `ToActionResult(this)`, `ToCreatedResult(...)` o `ToAcceptedResult(this)`, y el mapper arma el ProblemDetails.
- Tipo → status: Validation 400 · Unauthorized 401 · Forbidden 403 · NotFound 404 · Conflict 409 · TooManyRequests 429 (con `retryAfter`) · Failure 500.
- Un error `TooManyRequests` lleva los segundos que faltan en la metadata `retryAfter`: el front los usa para la cuenta regresiva del botón.
- **Un recurso de otro tenant devuelve 404**, nunca 403.
- Para atar un error de negocio a un campo del formulario: `FieldErrors.On(error, "name")`.

```csharp
public const string HasUsersCode = "Roles.Role.HasUsers";
public static Error HasUsers(int userCount) => Error.Conflict(HasUsersCode, "The role has users.",
    new Dictionary<string, object?> { ["userCount"] = userCount });
```

## Prohibido
- `throw` por una regla de negocio.
- `IsSuccess ? Ok(...) : BadRequest(...)` a mano en un controller.
- Textos del usuario en `Description`.
- Códigos sin su traducción en los dos `.resx`.
- Un código nuevo de dos partes, o sumarlo a `ReservedKeys`.
- Exponer el mensaje de una excepción: `GlobalExceptionHandler` responde 500 `General.Unexpected` con `traceId`.

## Copiá de
- `Domain/Authorization/RoleErrors.cs` (E4) · `Api/ErrorHandling/ControllerResultExtensions.cs` (E1)

## Lo verifica
- `ErrorCodeTests`: toda clave de `Errors.resx` sigue el formato `Area.Entidad.Motivo`, salvo las de `ReservedKeys`, que además tienen que seguir existiendo en el resx; y cada código declarado tiene su clave.
- `ResourceParityTests`: la misma clave en es y en.
- `ErrorHandlingTests`, `FrameworkErrorsTests`: status y forma del ProblemDetails.

## Detalle
[backend.md §6](../architecture/backend.md#6-result-pattern-y-manejo-de-errores)
