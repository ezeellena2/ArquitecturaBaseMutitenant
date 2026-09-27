# Teléfonos

**Regla:** un teléfono se guarda y viaja **siempre en E.164** (`+5491123456789`), como el value object `PhoneNumber`. Lo que escribe la persona lo interpreta **el backend** con `IPhoneNumberParser` (libphonenumber), a partir del país elegido. El front solo ayuda a escribirlo bien y lo muestra con `PhoneText`.

## Cómo se hace
- **Entrada (contrato HTTP):** `{ "phone": { "country": "AR", "number": "11 2345-6789" } }`, un `PhoneInputHttpRequest` con el país en ISO 3166-1 alfa-2. También se acepta un número que ya viene con `+`: en ese caso el país se ignora.
- **Servicio:** `phoneParser.Parse(input.Country, input.Number, PhoneUsage.X)` devuelve `Result<PhoneNumber>`. Según el uso:
  - `PhoneUsage.Any`: fijo o celular (datos de contacto, empresas);
  - `PhoneUsage.Mobile`: solo celular (ingreso, registro y vínculo con WhatsApp);
  - `PhoneUsage.WhatsApp`: celular **y** de un país de `WhatsApp:AllowedCountries`.
- **Errores**, atados al campo `phone` con `FieldErrors.On`: `Users.Phone.Invalid`, `Users.Phone.NotMobile` y `Users.Phone.CountryNotAllowed`.
- **Argentina:** si falta el 9 de un celular (o viene con 0 o 15), el parser lo completa. Se guarda siempre `+549…`. El número de prueba de Meta sin el 9 es una opción del módulo WhatsApp (`SendArgentineMobilesWithoutNine`), no un dato guardado.
- **Base de datos:** `varchar(16)` en E.164. El teléfono de una identidad es **único en todo el sistema** (índice sobre el E.164).
- **Buscar por teléfono:** se normaliza lo buscado con el mismo parser (o se compara por dígitos) y se busca por el E.164.
- **Salida (JSON):** el E.164 como texto. El país no se manda: sale del número.
- **Correo y WhatsApp:** `DisplayFormatter.Phone(phone, culture)` usa las mismas reglas que `PhoneText`.
- **Logs:** siempre `IPhoneNumberParser.Mask(phone)` (`+54 9 11 •••• 6789`).

## Prohibido
- Guardar el número con espacios, guiones, `0` o `15`, o sin el `+país`.
- Guardar el país aparte del número.
- Validar con una expresión regular propia.
- Emojis de banderas: en Windows no se ven.
- Loguear un número completo.

## Copiá de
- `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Phones/LibPhoneNumberParser.cs` y `Domain/ValueObjects/PhoneNumber.cs`: se copian y se les suma `PhoneUsage` (E3).

## Lo verifica
- `LibPhoneNumberParserTests`: el 9 argentino, números con 0 y 15, letras rechazadas, fijo contra celular, país no permitido.
- `PhoneNumberTests` (Domain) y `DisplayFormatterTests`, con los casos de teléfono de `docs/contracts/format-cases.json`, los mismos que corre el front.

## Detalle
El componente y el formato en pantalla: `../ArquitecturaBaseMutitenantFront/docs/rules/telefonos.md`.
