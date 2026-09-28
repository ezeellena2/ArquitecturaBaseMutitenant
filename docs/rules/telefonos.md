# Teléfonos

**Regla:** un teléfono se guarda y viaja **siempre en E.164** (`+5491123456789`), como el value object `PhoneNumber`. Lo que escribe la persona lo interpreta **el backend** con `IPhoneNumberParser` (libphonenumber), a partir del país elegido. El front solo ayuda a escribirlo bien y lo muestra con `PhoneText`.

## Cómo se hace
- **Entrada (contrato HTTP):** `{ "phone": { "country": "AR", "number": "11 2345-6789" } }`, un `PhoneInputHttpRequest` con el país en ISO 3166-1 alfa-2. También se acepta un número que ya viene con `+`: en ese caso el país se ignora.
- **Servicio:** `phoneParser.Parse(input.Country, input.Number, PhoneUsage.X)` devuelve `Result<PhoneNumber>`. Según el uso:
  - `PhoneUsage.Any`: fijo o celular (datos de contacto, empresas);
  - `PhoneUsage.Mobile`: solo celular (ingreso y registro con teléfono, teléfono de la cuenta).
  
  `PhoneUsage` queda en `Any | Mobile` y **no nombra WhatsApp**. Qué países acepta WhatsApp lo controla el módulo en sus flujos (código, vínculo y registro por WhatsApp), leyendo `WhatsApp:AllowedCountries` solo dentro de `Modules/WhatsApp`, con su propio error sobre el campo `phone`, y solo para un número **nuevo**: si se achica la lista, un número existente no se invalida ([modulos](modulos.md)).
- **Errores**, atados al campo `phone` con `FieldErrors.On`: `Users.Phone.Invalid` y `Users.Phone.NotMobile`.
- **Argentina:** si falta el 9 de un celular (o viene con 0 o 15), el parser lo completa. Se guarda siempre `+549…`. El número de prueba de Meta sin el 9 es una opción del módulo WhatsApp (`SendArgentineMobilesWithoutNine`), no un dato guardado.
- **Base de datos:** siempre el E.164; una columna que guarda solo teléfonos es `varchar(16)`. Los teléfonos de una cuenta viven únicamente en `identity.LoginMethods` (`Type=Phone`, `Value` en E.164) y son **únicos en todo el sistema** por el índice `(Type, Value)`; esa columna `Value` es compartida con los correos, así que su largo no es el del teléfono. `AspNetUsers.PhoneNumber` es solo una copia del método principal, **sin índice único**, y la mantiene el servicio de métodos de ingreso ([multitenancy.md §12](../architecture/multitenancy.md#12-caché-locks-unicidad)).
- **Buscar por teléfono:** se normaliza lo buscado con el mismo parser (o se compara por dígitos) y se busca por el E.164.
- **Salida (JSON):** el E.164 como texto. El país no se manda: sale del número.
- **País de entrada:** `ValidCountry()` de Application consulta `ICountryCatalog`; `GET /api/reference-data` abastece el selector, sin una lista de países en el código.
- **Correo y WhatsApp:** `DisplayFormatter.Phone(phone, culture, timeZone)` usa las mismas reglas que `PhoneText`: nacional si el país coincide con la cultura, internacional si no. `format-cases.json` fija el texto exacto si las librerías difieren.
- **Logs:** siempre `IPhoneNumberParser.Mask(phone)` (`+54 9 11 •••• 6789`).

## Prohibido
- Guardar el número con espacios, guiones, `0` o `15`, o sin el `+país`.
- Guardar el país aparte del número.
- Validar con una expresión regular propia.
- Emojis de banderas: en Windows no se ven.
- Loguear un número completo.
- Un `PhoneUsage` o un error del núcleo que nombre WhatsApp o sus países.
- Una lista de países o prefijos telefónicos escrita a mano; salen de los datos de referencia ([ADR 0036](../architecture/datos-de-referencia.md)).

## Copiá de
- `Domain/ValueObjects/PhoneNumber.cs` (E1), que se copia de `../ArquitecturaBase` (solo verifica el E.164; interpretar lo que escribe la persona es trabajo del parser) · `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Phones/LibPhoneNumberParser.cs`, que se copia y se le suma `PhoneUsage` (E3).

## Lo verifica
- `LibPhoneNumberParserTests` (E3): el 9 argentino, números con 0 y 15, letras rechazadas, fijo contra celular. El país no permitido para WhatsApp lo prueban los tests del módulo (E8).
- `PhoneNumberTests` (E1) de Domain y `DisplayFormatterTests` (E1), con los casos de teléfono de `docs/contracts/format-cases.json`, los mismos que corre el front.

## Detalle
El componente y el formato en pantalla: [ficha del front](../../../ArquitecturaBaseMutitenantFront/docs/rules/telefonos.md).
