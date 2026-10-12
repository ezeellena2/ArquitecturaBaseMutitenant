# Correos electrónicos

**Regla:** toda dirección de correo pasa por el value object `Email`, que la **normaliza** y la **valida** una sola vez. Se guarda normalizada, y la comparación y la unicidad usan ese valor.

## Cómo se hace
- `Email.Create(texto)` devuelve `Result<Email>`:
  1. saca los espacios de los bordes y normaliza a Unicode NFC;
  2. pasa **todo a minúsculas**;
  3. convierte el dominio con acentos a su forma ASCII (IDN: `ñandú.com.ar` → `xn--and-6ma2c.com.ar`) para compararlo y enviarlo, y guarda la forma legible para mostrar;
  4. valida: una sola `@`, parte local dot-atom de 1 a 64 **bytes UTF-8** (sin separadores, controles ni caracteres de formato), dominio con punto y sin espacios, total canónico de hasta 254 **bytes UTF-8**.
- **Error:** `Users.Email.Invalid`, atado al campo.
- **En la base:** `varchar(254)`, ya normalizado. La unicidad en todo el sistema la da el índice único `(Type, Value)` de los métodos verificados de `identity.LoginMethods` (uno pendiente es único solo en su cuenta, D4b), la única fuente de los correos de una cuenta (ADR 0033, [multitenancy.md §12](../architecture/multitenancy.md#12-caché-locks-unicidad)). Las invitaciones pendientes tienen su propio índice único por tenant.
- **Métodos de ingreso:** `LoginMethod.Value` es un texto genérico por tipo. Cuando `Type = Email`, se llena solo con el valor de `Email.Create`, nunca con el texto que llegó en la petición.
- **Pedido de código:** `LoginCodeRequester` busca la cuenta verificada bajo el lock del destino. Si no existe, devuelve `Auth.LoginCode.AccountNotFound` en el campo `email`, sin emitir, renderizar ni encolar (ADR 0043). En una cuenta existente conserva la emisión y las guardas de estado; la condición de operador no se informa antes de probar su identidad.
- **Copia de Identity:** `ApplicationUser.Email` y `ApplicationUser.NormalizedEmail` (en `Infrastructure/Identity`, heredadas de `IdentityUser`) son solo copias del correo principal, sin índice único. Las mantiene el servicio de métodos de ingreso, y nunca se leen para buscar ni para verificar si un correo ya existe.
- **Contratos:** el `*HttpRequest` recibe `string`. El servicio o el validador lo convierte con `Email.Create`. Los modelos de Application usan `Email`, no `string`.
- **EF:** un conversor de valor por convención para todo `Email`.
- **Front:** `z.string().trim().toLowerCase().email()` como guía, con los mismos casos. Decide el back.

## Prohibido
- `string` suelto para un correo en una entidad o un modelo (las únicas excepciones son `LoginMethod.Value` y las copias heredadas `ApplicationUser.Email` y `ApplicationUser.NormalizedEmail`, descriptas arriba).
- Expresiones regulares propias.
- Trucos de un proveedor, como sacar los puntos o lo que va después del `+` en Gmail: mezclarían personas distintas.
- Comparar sin normalizar.
- Mostrar el correo en un log (se enmascara con `PersonalDataMask.Email`: `lu•••@gmail.com`).

## Copiá de
- `Domain/ValueObjects/Email.cs` (E1), con la normalización.

## Lo verifica
- `EmailPropertyTests` (E1)

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
