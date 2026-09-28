# Fichas de reglas (backend)

Una ficha por tema, todas con el mismo formato: **Regla · Cómo se hace · Prohibido · Copiá de · Lo verifica · Detalle** ([arnes.md §2](../architecture/arnes.md#2-formato-de-una-ficha-docsrulestemamd)). Las nombran los `AGENTS.md` de cada carpeta y la tabla "si vas a tocar X, leé Y" del `AGENTS.md` raíz.

| Ficha | Tema |
|---|---|
| [capas-y-flujo](capas-y-flujo.md) | qué va en cada capa; controller → servicio → repositorio o lector |
| [guardado](guardado.md) | una sola forma de guardar (`ExecuteInTransactionAsync`) |
| [result-y-errores](result-y-errores.md) | `Result`, `<X>Errors`, códigos, ProblemDetails |
| [validacion](validacion.md) | `IRequestValidator`, `ValidationRules`, errores por campo |
| [multitenancy](multitenancy.md) | accesos B2C/B2B, `[Access]`, datos privados, públicos y compartidos, subdominios, RLS |
| [permisos](permisos.md) | permisos de organización, empresa, personal y plataforma |
| [fechas-y-zonas](fechas-y-zonas.md) | UTC, `DateOnly`, zona efectiva |
| [datos-de-referencia](datos-de-referencia.md) | monedas, países, zonas, culturas y tipos fiscales: JSON E1, tablas E2; nada de listas en código |
| [numeros-y-moneda](numeros-y-moneda.md) | `decimal`, `Money`, porcentajes, redondeo, formato |
| [telefonos](telefonos.md) | E.164, `IPhoneNumberParser`, `PhoneUsage`, el 9 argentino |
| [paginado-y-busqueda](paginado-y-busqueda.md) | por páginas, por cursor, orden, búsqueda, conteos |
| [textos-y-traducciones](textos-y-traducciones.md) | resources es/en, voseo, cultura |
| [persistencia-ef](persistencia-ef.md) | entidades, configuraciones, migraciones, readers |
| [auditoria](auditoria.md) | marcas, rastro de cambios, eventos de seguridad |
| [api-http](api-http.md) | contratos, status, OpenAPI, prefijos |
| [logs](logs.md) | `[LoggerMessage]`, `OperationLog`, qué nunca se loguea |
| [modulos](modulos.md) | módulos quitables (WhatsApp) |
| [tests](tests.md) | qué test va dónde, dobles, nombres |
| [concurrencia](concurrencia.md) | versión (`xmin`) y 409 si dos personas editan a la vez |
| [emails](emails.md) | value object `Email`: normalizado, validado y único |
| [textos-libres](textos-libres.md) | limpieza automática, `TextLimits` y tipos de texto |
| [identificacion-fiscal](identificacion-fiscal.md) | `TaxId`: CUIT, CUIL, DNI, con dígito verificador |
| [idempotencia](idempotencia.md) | `[Idempotent]` + `Idempotency-Key`: sin duplicados |
| [datos-personales](datos-personales.md) | términos y privacidad versionados, exportar y dar de baja |
| [modulos-habilitados](modulos-habilitados.md) | feature flags por organización; módulo ≠ permiso |

**Archivos y tests que todavía no existen:** un "Copiá de" o un test de "Lo verifica" que nace en una etapa posterior lleva la marca `(E#)` (por ejemplo `RoleService.cs`, Etapa 4). `HarnessTests` lo exige cuando esa etapa cierra ([arnes.md §2 y §5](../architecture/arnes.md#5-el-arnés-se-verifica-a-sí-mismo)).
