# Validación

**Regla:** cada `*Request` que lo necesita tiene un validador. Se valida con el único `IRequestValidator`, **antes** del límite transaccional.

## Cómo se hace
- `Application/Validation/<Área>/<Acción>RequestValidator.cs`: `internal sealed class X : AbstractValidator<XRequest>`.
- Reglas comunes de `ValidationRules`:
  - textos: `Required()`, `PersonName()`, `OrganizationName()`, `ShortName()`, `Description()`, `LongText()` (los largos salen de `TextLimits`; [textos-libres](textos-libres.md));
  - datos de contacto y fiscales: `ValidEmail()` ([emails](emails.md)), `ValidTaxId()` ([identificacion-fiscal](identificacion-fiscal.md));
  - preferencias: `ValidTimeZone()`, `ValidCulture()`, `ValidCurrency()`;
  - permisos: `ValidPermissions(scope)`.
- El teléfono no tiene regla en `ValidationRules`: lo interpreta el servicio con `IPhoneNumberParser.Parse(country, number, usage)`, y el error (`Users.Phone.*`) se ata al campo `phone` con `FieldErrors.On` ([telefonos](telefonos.md)).
- El texto ya llega limpio (trim, NFC, sin caracteres invisibles) gracias al conversor global: no se vuelve a limpiar.
- Los mensajes salen de `ValidationTexts` (`Validation.resx`), nunca de un literal.
- Un listado usa `PagedRequestValidator<T>` o `CursorRequestValidator<T>`, que ya validan página, tamaño, orden y búsqueda.
- El servicio recibe `IRequestValidator` y llama `if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;`.
- Los campos del error van en camelCase con puntos (`permissions.0`), que es como los ve el front.

## Prohibido
- Validar dentro del límite o en el controller.
- Inyectar un `IValidator<T>` o un validador por request en el constructor.
- `.WithMessage("texto")` literal.
- Reglas de negocio que necesitan la base (unicidad, existencia) en el validador: van en el servicio, como `Error`.
- Validar un teléfono en el validador: lo decide el servicio con `IPhoneNumberParser`.

## Copiá de
- `Application/Validation/Roles/CreateRoleRequestValidator.cs` (E4)

## Lo verifica
- `RequestValidatorTests` (E1), `PagedRequestValidatorTests` (E1).
- `ValidationProblemTests` (E1): forma del 400 con `errors`.
- `ServiceDependencyCountTests` (E1): evita la vuelta a los validadores inyectados por request.

## Detalle
[backend.md §6, "Validación"](../architecture/backend.md#validación)
