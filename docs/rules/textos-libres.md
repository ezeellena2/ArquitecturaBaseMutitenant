# Nombres y textos libres

**Regla:** todo texto que entra a la API se **limpia solo**: sin espacios en los bordes, Unicode NFC y sin caracteres invisibles. Además, cada campo declara su **tipo de texto**, que fija su largo y si se colapsan los espacios. Los largos son constantes compartidas por la base, el validador y el front.

## Cómo se hace
- **Nivel 1, automático:** `NormalizedStringJsonConverter` (registrado en `JsonConfiguration`) aplica `TextNormalizer.Clean` a **todo** `string` de un contrato:
  - trim;
  - normalización NFC;
  - saca los caracteres de control y los de ancho cero, salvo `\n` y `\t`;
  - un texto que queda vacío pasa a `null`.
  
  Excepción: una propiedad marcada `[RawText]` (tokens, códigos, contenido que se firma).
- **Nivel 2, por tipo**, con constantes en `Domain/Common/TextLimits.cs`:

  | Tipo | Regla de validación | Largo | Espacios internos |
  |---|---|---|---|
  | Nombre de persona | `PersonName()` | 100 | se colapsan |
  | Nombre de organización o empresa | `OrganizationName()` | 120 | se colapsan |
  | Nombre de rol o de un catálogo | `ShortName()` | 60 | se colapsan |
  | Descripción | `Description()` | 500 | se mantienen los saltos de línea |
  | Nota o comentario largo | `LongText()` | 4000 | se mantienen |

- **Configuración EF:** `HasMaxLength(TextLimits.PersonName)`, la misma constante.
- **Front:** los largos llegan por OpenAPI (`maxLength`) y los usan los `zod` generados; no se escriben a mano.
- **Unicidad de nombres:** el índice único compara el texto ya limpio, con la collation española (ver [persistencia-ef](persistencia-ef.md)).

## Prohibido
- `.Trim()` a mano en servicios o controllers: ya lo hizo el conversor.
- Largos escritos como número suelto (`HasMaxLength(100)`, `MaxLength(100)`).
- Guardar `""` para "sin dato".
- `[RawText]` en un campo que ve una persona.

## Copiá de
- `Application/Common/Text/TextNormalizer.cs` y `Api/Json/NormalizedStringJsonConverter.cs` (E1).

## Lo verifica
- `TextNormalizerTests`: "José" con el acento separado es igual a "José", "Grupo  La Cosecha " queda "Grupo La Cosecha", y caracteres de ancho cero.
- `NormalizedInputTests` (integración): un POST con esos textos guarda el valor limpio.
- `TextLimitsTests` (arquitectura): ningún `HasMaxLength` ni `MaxLength` con un número literal.

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
