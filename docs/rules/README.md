# Fichas de reglas (backend)

Una ficha por tema, todas con el mismo formato: **Regla · Cómo se hace · Prohibido · Copiá de · Lo verifica · Detalle** ([arnes.md §2](../architecture/arnes.md#2-formato-de-una-ficha-docsrulestemamd)). Las nombran los `AGENTS.md` de cada carpeta y la tabla "si vas a tocar X, leé Y" del `AGENTS.md` raíz.

| Ficha | Tema |
|---|---|
| [capas-y-flujo](capas-y-flujo.md) | qué va en cada capa; controller → servicio → repositorio o lector |
| [guardado](guardado.md) | una sola forma de guardar (`ExecuteInTransactionAsync`) |
| [result-y-errores](result-y-errores.md) | `Result`, `<X>Errors`, códigos, ProblemDetails |
| [validacion](validacion.md) | `IRequestValidator`, `ValidationRules`, errores por campo |
| [multitenancy](multitenancy.md) | `ITenantOwned`, `[TenantKind]`, `ITenantScope`, RLS |
| [permisos](permisos.md) | permisos de organización, empresa, personal y plataforma |
| [fechas-y-zonas](fechas-y-zonas.md) | UTC, `DateOnly`, zona efectiva |
| [numeros-y-moneda](numeros-y-moneda.md) | `decimal`, `Money`, porcentajes, redondeo, formato |
| [paginado-y-busqueda](paginado-y-busqueda.md) | por páginas, por cursor, orden, búsqueda, conteos |
| [textos-y-traducciones](textos-y-traducciones.md) | resources es/en, voseo, cultura |
| [persistencia-ef](persistencia-ef.md) | entidades, configuraciones, migraciones, readers |
| [auditoria](auditoria.md) | marcas, rastro de cambios, eventos de seguridad |
| [api-http](api-http.md) | contratos, status, OpenAPI, prefijos |
| [logs](logs.md) | `[LoggerMessage]`, `OperationLog`, qué nunca se loguea |
| [modulos](modulos.md) | módulos quitables (WhatsApp) |
| [tests](tests.md) | qué test va dónde, dobles, nombres |

**Archivos que todavía no existen:** un "Copiá de" puede nombrar un archivo que nace en una etapa posterior (por ejemplo `RoleService.cs`, Etapa 4). Esos enlaces llevan la marca `(E#)` y `HarnessTests` los empieza a exigir cuando la etapa cierra.
