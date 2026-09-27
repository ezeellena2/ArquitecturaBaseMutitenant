# Validación

**Regla:** cada `*Request` que lo necesita tiene un validador. Se valida con el único `IRequestValidator`, **antes** del límite transaccional.

## Cómo se hace
- `Application/Validation/<Área>/<Acción>RequestValidator.cs`: `internal sealed class X : AbstractValidator<XRequest>`.
- Reglas comunes de `ValidationRules`: `Required()`, `MaxLength(n)`, `ValidEmail()`, `ValidTimeZone()`, `ValidCulture()`, `ValidCurrency()`, `ValidPermissions(scope)`.
- Los mensajes salen de `ValidationMessages` (resx), nunca de un literal.
- Un listado usa `PagedRequestValidator<T>` o `CursorRequestValidator<T>`, que ya validan página, tamaño, orden y búsqueda.
- El servicio recibe `IRequestValidator` y llama `if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;`.
- Los campos del error van en camelCase con puntos (`permissions.0`), que es como los ve el front.

## Prohibido
- Validar dentro del límite o en el controller.
- Inyectar un `IValidator<T>` o un validador por request en el constructor.
- `.WithMessage("texto")` literal.
- Reglas de negocio que necesitan la base (unicidad, existencia) en el validador: van en el servicio, como `Error`.

## Copiá de
- `Application/Validation/Roles/CreateRoleRequestValidator.cs` (E4)

## Lo verifica
- `RequestValidatorTests`, `PagedRequestValidatorTests`.
- `ValidationProblemTests`: forma del 400 con `errors`.
- `ServiceDependencyCountTests`: evita la vuelta a los validadores inyectados por request.

## Detalle
[backend.md §6, "Validación"](../architecture/backend.md#validación)
