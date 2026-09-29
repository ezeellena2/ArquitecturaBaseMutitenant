# Nombres y textos libres

**Regla:** todo texto que entra a la API se **limpia solo**: sin espacios en los bordes, Unicode NFC y sin caracteres invisibles. Además, cada campo declara su **tipo de texto**, que fija su largo y si se colapsan los espacios. Los largos son constantes compartidas por la base, el validador y el front.

## Cómo se hace
- **Nivel 1, automático:** el resolver de `JsonConfiguration` aplica `NormalizedStringJsonConverter` a las propiedades `string` de los contratos HTTP de entrada (`*HttpRequest`) y ejecuta `TextNormalizer.Clean`:
  - trim;
  - normalización NFC;
  - saca los caracteres de control y los de ancho cero, salvo `\n` y `\t`;
  - conserva los espacios y saltos de línea internos: no colapsa globalmente los nombres ni las descripciones;
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
- **Nombres:** después de `Clean`, el helper tipado de nombre colapsa espacios internos solo para `PersonName`, `OrganizationName` y `ShortName`. `Description` y `LongText` conservan los internos y sus saltos de línea.
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
- `TextNormalizerTests` (E1): `Clean` iguala "José" con acento separado a "José", elimina ancho cero y transforma "Grupo  La Cosecha " en "Grupo  La Cosecha" (solo trim); el helper tipado de nombre produce "Grupo La Cosecha" sin alterar una descripción.
- `NormalizedInputTests` (E1): un POST de `TestFeatures/TestController` devuelve el cuerpo ya normalizado; no hay persistencia antes de E2.
- `TextLimitsTests` (E1): ningún `HasMaxLength` ni `MaxLength` con un número literal (arquitectura).

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas)
