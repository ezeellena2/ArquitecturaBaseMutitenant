# Identificación fiscal y documentos (CUIT, CUIL, DNI)

**Regla:** todo identificador fiscal o de persona es un `TaxId` con **país + tipo + número**, guardado **solo con dígitos** y validado con su dígito verificador. El formato con guiones es solo de presentación.

## Cómo se hace
- **Tipos iniciales** (Argentina): `CUIT` y `CUIL` (11 dígitos con verificador) y `DNI` (7 u 8 dígitos). La lista habilitada, etiquetas y máscaras salen de `ITaxIdTypeCatalog` (`tax-id-types.json` en E1, tabla `platform.TaxIdTypes` en E2); sumar otro país añade sus datos y, si hace falta, un validador para su algoritmo.
- **Verificador de CUIT y CUIL:**
  - se multiplican los 10 primeros dígitos por `5 4 3 2 7 6 5 4 3 2` y se suman;
  - `v = 11 − (suma mod 11)`; si `v = 11`, el dígito es `0`; si `v = 10`, **inválido**.
  - Ejemplo: `20-12345678-6` es válido (suma 148, 148 mod 11 = 5, 11 − 5 = 6).
  - Prefijos: 20, 23, 24 y 27 son personas; 30, 33 y 34, empresas. El tipo de dueño (persona o empresa) se valida contra el prefijo cuando corresponde.
- `TaxIdTypes.Code` es un código de catálogo único por país y tipo (`AR-CUIT`). `TaxIdField` emite en E1 `{ "type": "AR-CUIT", "number": "20123456786" }`, sin separadores; Application busca ese código y deriva `country` del catálogo si recibe el par del field. Si recibe el contrato completo, comprueba que `country` coincida con `TaxIdTypes.CountryCode`. En E6, `TaxId.Create(país, tipo de catálogo, texto)` recibe el país, la fila del catálogo (con su clave de tipo y clave de algoritmo) y el número; devuelve `Result<TaxId>` con dígitos solos. Application valida que el tipo esté habilitado y corresponda al uso. Error: `TaxIds.TaxId.Invalid`, atado al campo.
- **El tipo según el país (D6):** la ficha de Empresa pide solo el número. El tipo es el de empresas del país de la organización, que es la región de su «Idioma y región» (`TenantSettings.DefaultCulture`): el primer tipo habilitado de ese país que aplica a empresas (`Company` o `Both`), por `SortOrder` y después por código. En Argentina es la CUIT. Si el número no cambió, se conserva el tipo guardado, aunque la organización haya cambiado de país. Un país sin un tipo para empresas no admite un número (`TaxIds.TaxId.Invalid`). Lo resuelve `CompanyReferenceDataResolver`.
- `ValidTaxIdType()` en Application comprueba que el tipo esté habilitado en el catálogo para un dato nuevo; Domain aplica el algoritmo correspondiente. El front crea en E1 `TaxIdField` con `stdnum`; el value object y el contrato HTTP completo del back llegan en E6.
- **Front de Empresas (A10/R03):** `useCompanyTaxIdType` toma `organizationCountryCode` de `/api/me` (la cultura personal no define ese país) y resuelve con el orden de D6. Conserva el tipo guardado cuando el número no cambia, incluso deshabilitado. Sin un tipo empresarial del país oculta el campo y conserva el número guardado al editar otro dato. `TaxIdNumberField` deriva máscara y límite del catálogo; los mensajes comunes distinguen largo y algoritmo, con el nombre traducido del tipo. `33333333333` tiene once dígitos pero es inválido; `33333333339` es válido. Registro-Empresa mantiene su acuerdo de CUIT argentino opcional.
- **En la base:** `TaxCountry char(2)`, `TaxType varchar(16)`, `TaxNumber varchar(20)` con dígitos solos. Unicidad por tenant cuando el dato lo pide (dos empresas de la misma organización no comparten CUIT).
- **JSON de salida (E6):** `{ "country": "AR", "type": "AR-CUIT", "number": "20123456786" }`. El `country` coincide con `TaxIdTypes.CountryCode`; no cambia el significado de `type` entre entrada y salida.
- **Mostrar:** `TaxIdText` en el front → `20-12345678-6`. En el back, el mismo formato es el tipo `taxId` de los casos compartidos de `docs/contracts/format-cases.json`, que `DisplayFormatter` cumple (lo prueba `DisplayFormatterTests`).
- **Cargar:** `TaxIdField` (tipo + número, con opciones del catálogo), que valida mientras se escribe con `stdnum` (npm). Decide el back.

## Prohibido
- `string` suelto para un CUIT.
- Guardarlo con guiones, o sin validar el verificador.
- Suponer que todos son argentinos.
- Copiar un CUIT de ejemplo de internet: se usan los de `format-cases.json`, calculados.

## Copiá de
- `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxId.cs` y `src/ArquitecturaBaseMultitenant.Domain/ValueObjects/TaxIdValidators/ArgentineCuitValidator.cs` (E6).

## Lo verifica
- `HarnessTests` (E0): formato y referencias de la ficha.

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
