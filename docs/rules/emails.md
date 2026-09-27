# Correos electrónicos

**Regla:** toda dirección de correo pasa por el value object `Email`, que la **normaliza** y la **valida** una sola vez. Se guarda normalizada, y la comparación y la unicidad usan ese valor.

## Cómo se hace
- `Email.Create(texto)` devuelve `Result<Email>`:
  1. saca los espacios de los bordes y normaliza a Unicode NFC;
  2. pasa **todo a minúsculas**;
  3. convierte el dominio con acentos a su forma ASCII (IDN: `ñandú.com.ar` → `xn--and-6ma2c.com.ar`) para compararlo y enviarlo, y guarda la forma legible para mostrar;
  4. valida: una sola `@`, parte local de 1 a 64 caracteres, dominio con punto y sin espacios, total de hasta 254.
- **Error:** `Users.Email.Invalid`, atado al campo.
- **En la base:** `varchar(254)`, ya normalizado, con índice único en la identidad (único en todo el sistema) y en las invitaciones pendientes por tenant.
- **Contratos:** el `*HttpRequest` recibe `string`. El servicio o el validador lo convierte con `Email.Create`. Los modelos de Application usan `Email`, no `string`.
- **EF:** un conversor de valor por convención para todo `Email`.
- **Front:** `z.string().trim().toLowerCase().email()` como guía, con los mismos casos. Decide el back.

## Prohibido
- `string` suelto para un correo en una entidad o un modelo.
- Expresiones regulares propias.
- Trucos de un proveedor, como sacar los puntos o lo que va después del `+` en Gmail: mezclarían personas distintas.
- Comparar sin normalizar.
- Mostrar el correo en un log (se enmascara: `j***@gmail.com`).

## Copiá de
- `Domain/ValueObjects/Email.cs` (E1), que parte del de `../ArquitecturaBase` y le suma la normalización.

## Lo verifica
- `EmailTests`: la tabla de casos de `format-cases.json` (`"  Juan@Gmail.COM "` → `juan@gmail.com`, IDN, límites).
- `EmailPropertyTests` (arquitectura): ninguna entidad ni modelo tiene una propiedad `string` llamada `*Email`.
- `SignupTests`: dos registros que solo difieren en mayúsculas dan `Auth.Signup.EmailTaken`.

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
