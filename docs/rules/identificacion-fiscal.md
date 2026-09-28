# Identificación fiscal y documentos (CUIT, CUIL, DNI)

**Regla:** todo identificador fiscal o de persona es un `TaxId` con **país + tipo + número**, guardado **solo con dígitos** y validado con su dígito verificador. El formato con guiones es solo de presentación.

## Cómo se hace
- **Tipos iniciales** (Argentina): `CUIT` y `CUIL` (11 dígitos con verificador) y `DNI` (7 u 8 dígitos). La lista habilitada, etiquetas y máscaras salen de `ITaxIdTypeCatalog` (`tax-id-types.json` en E1, tabla `platform.TaxIdTypes` en E2); sumar otro país añade sus datos y, si hace falta, un validador para su algoritmo.
- **Verificador de CUIT y CUIL:**
  - se multiplican los 10 primeros dígitos por `5 4 3 2 7 6 5 4 3 2` y se suman;
  - `v = 11 − (suma mod 11)`; si `v = 11`, el dígito es `0`; si `v = 10`, **inválido**.
  - Ejemplo: `20-12345678-6` es válido (suma 148, 148 mod 11 = 5, 11 − 5 = 6).
  - Prefijos: 20, 23, 24 y 27 son personas; 30, 33 y 34, empresas. El tipo de dueño (persona o empresa) se valida contra el prefijo cuando corresponde.
- `TaxIdTypes.Code` es un código de catálogo único por país y tipo (`AR-CUIT`). `TaxIdField` emite en E1 `{ "type": "AR-CUIT", "number": "20123456786" }`, sin separadores; Application busca ese código y deriva `country` del catálogo si recibe el par del field. Si recibe el contrato completo, comprueba que `country` coincida con `TaxIdTypes.CountryCode`. En E6, `TaxId.Create(país, tipo, texto)` recibe el país, ese mismo código de tipo y el número, y devuelve `Result<TaxId>`. Error: `TaxIds.TaxId.Invalid`, atado al campo.
- `ValidTaxIdType()` en Application comprueba que el tipo esté habilitado en el catálogo para un dato nuevo; Domain aplica el algoritmo correspondiente. El front crea en E1 `TaxIdField` con `stdnum`; el value object y el contrato HTTP completo del back llegan en E6.
- **En la base:** `TaxCountry char(2)`, `TaxType varchar(16)`, `TaxNumber varchar(20)` con dígitos solos. Unicidad por tenant cuando el dato lo pide (dos empresas de la misma organización no comparten CUIT).
- **JSON de salida (E6):** `{ "country": "AR", "type": "AR-CUIT", "number": "20123456786" }`. El `country` coincide con `TaxIdTypes.CountryCode`; no cambia el significado de `type` entre entrada y salida.
- **Mostrar:** `TaxIdText` en el front y `DisplayFormatter.TaxId` en el back → `20-12345678-6`.
- **Cargar:** `TaxIdField` (tipo + número, con opciones del catálogo), que valida mientras se escribe con `stdnum` (npm). Decide el back.

## Prohibido
- `string` suelto para un CUIT.
- Guardarlo con guiones, o sin validar el verificador.
- Suponer que todos son argentinos.
- Copiar un CUIT de ejemplo de internet: se usan los de `format-cases.json`, calculados.

## Copiá de
- `Domain/ValueObjects/TaxId.cs` y `Domain/ValueObjects/TaxIdValidators/ArgentineCuitValidator.cs` (E6).

## Lo verifica
- `TaxIdTests` (E6): los casos válidos e inválidos de `format-cases.json`, que también corre el front con `stdnum`.
- `TaxIdPropertyTests` (E6): ninguna propiedad `Cuit`, `Cuil` o `Dni` de tipo `string` (test de arquitectura).

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
