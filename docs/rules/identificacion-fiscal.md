# Identificación fiscal y documentos (CUIT, CUIL, DNI)

**Regla:** todo identificador fiscal o de persona es un `TaxId` con **país + tipo + número**, guardado **solo con dígitos** y validado con su dígito verificador. El formato con guiones es solo de presentación.

## Cómo se hace
- **Tipos iniciales** (Argentina): `CUIT` y `CUIL` (11 dígitos con verificador) y `DNI` (7 u 8 dígitos). Sumar otro país (RUT de Chile, RUC de Uruguay) es sumar un validador, sin cambiar la base.
- **Verificador de CUIT y CUIL:**
  - se multiplican los 10 primeros dígitos por `5 4 3 2 7 6 5 4 3 2` y se suman;
  - `v = 11 − (suma mod 11)`; si `v = 11`, el dígito es `0`; si `v = 10`, **inválido**.
  - Ejemplo: `20-12345678-6` es válido (suma 148, 148 mod 11 = 5, 11 − 5 = 6).
  - Prefijos: 20, 23, 24 y 27 son personas; 30, 33 y 34, empresas. El tipo de dueño (persona o empresa) se valida contra el prefijo cuando corresponde.
- `TaxId.Create(país, tipo, texto)` acepta el número con o sin guiones o espacios y devuelve `Result<TaxId>`. Error: `TaxIds.TaxId.Invalid`, atado al campo.
- **En la base:** `TaxCountry char(2)`, `TaxType varchar(8)`, `TaxNumber varchar(20)` con dígitos solos. Unicidad por tenant cuando el dato lo pide (dos empresas de la misma organización no comparten CUIT).
- **JSON de salida:** `{ "country": "AR", "type": "CUIT", "number": "20123456786" }`.
- **Mostrar:** `TaxIdText` en el front y `DisplayFormatter.TaxId` en el back → `20-12345678-6`.
- **Cargar:** `TaxIdField` (tipo + número), que valida mientras se escribe con `stdnum` (npm). Decide el back.

## Prohibido
- `string` suelto para un CUIT.
- Guardarlo con guiones, o sin validar el verificador.
- Suponer que todos son argentinos.
- Copiar un CUIT de ejemplo de internet: se usan los de `format-cases.json`, calculados.

## Copiá de
- `Domain/ValueObjects/TaxId.cs` y `Domain/ValueObjects/TaxIdValidators/ArgentineCuitValidator.cs` (E6).

## Lo verifica
- `TaxIdTests`: los casos válidos e inválidos de `format-cases.json`, que también corre el front con `stdnum`.
- `TaxIdPropertyTests` (arquitectura): ninguna propiedad `Cuit`, `Cuil` o `Dni` de tipo `string`.

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
