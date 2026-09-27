# ArquitecturaBaseMultitenant: reglas para agentes

Versión multitenant **B2B + B2C** de `../ArquitecturaBase`, con el flujo de cuenta de Mercado Libre, aunque el producto no es un marketplace. Una persona tiene una sola cuenta: entra con su perfil personal (funcionalidades B2C) y con los perfiles de sus organizaciones (funcionalidades B2B). Usa .NET 10, Aspire 13.5 y PostgreSQL. El front vive en `../ArquitecturaBaseMutitenantFront`. Este archivo es el índice de reglas.

La arquitectura canónica está en dos documentos:
- [`docs/architecture/backend.md`](docs/architecture/backend.md): capas, patrones y carpetas;
- [`docs/architecture/multitenancy.md`](docs/architecture/multitenancy.md): organizaciones, empresas y aislamiento.

Las decisiones están en [`docs/decisions/README.md`](docs/decisions/README.md) y el plan en [`docs/plans/2026-09-27-plan-de-desarrollo.md`](docs/plans/2026-09-27-plan-de-desarrollo.md).

## Antes de escribir código: el arnés

**No inventes.** Para cada tema hay una ficha en [`docs/rules/`](docs/rules/README.md), con la regla, cómo se hace, qué está prohibido, el archivo real que hay que copiar y el test que te va a frenar. Cada carpeta de código tiene además un `AGENTS.md` corto que dice qué va ahí y qué fichas leer. Cómo funciona: [`docs/architecture/arnes.md`](docs/architecture/arnes.md).

| Si vas a… | Leé |
|---|---|
| guardar algo en la base | [guardado](docs/rules/guardado.md) |
| devolver un error o validar | [result-y-errores](docs/rules/result-y-errores.md), [validacion](docs/rules/validacion.md) |
| usar una fecha u hora | [fechas-y-zonas](docs/rules/fechas-y-zonas.md) |
| usar un monto, decimal o porcentaje | [numeros-y-moneda](docs/rules/numeros-y-moneda.md) |
| hacer un listado | [paginado-y-busqueda](docs/rules/paginado-y-busqueda.md) |
| mostrar un texto al usuario | [textos-y-traducciones](docs/rules/textos-y-traducciones.md) |
| crear una entidad o una tabla | [persistencia-ef](docs/rules/persistencia-ef.md), [multitenancy](docs/rules/multitenancy.md), [auditoria](docs/rules/auditoria.md) |
| exponer una ruta | [api-http](docs/rules/api-http.md), [permisos](docs/rules/permisos.md) |
| loguear | [logs](docs/rules/logs.md) |
| tocar WhatsApp u otro módulo | [modulos](docs/rules/modulos.md) |
| escribir tests | [tests](docs/rules/tests.md) |
| agregar un área completa | `docs/guides/agregar-un-area.md` (nace en la Etapa 4) |

Si una regla no está escrita, **preguntá antes de inventar**. Después se agrega la ficha y su test.

## Forma de trabajo
- Commits chicos, en español, con conventional commits. **Commitear al cerrar cada tarea**: el código que nunca se commitea se pierde.
- TDD donde hay lógica. Antes de dar algo por terminado: `dotnet build` sin advertencias y `dotnet test` en verde. Los tests de integración necesitan Docker. Apagar Aspire con `aspire stop`.
- Una pantalla nueva del front se dibuja antes de programarse.

## Reglas que no se negocian
1. **Capas:** Domain ← Application ← Infrastructure; Api compone. El recorrido es `Controllers → I<X>Service → <X>Service → I<X>Repository/I<X>Reader/Integrations → Infrastructure`. Sin handlers ni CQRS con `ICommand`, sin repositorio genérico, sin Minimal APIs de negocio, sin Scrutor ni AutoMapper.
2. **Una sola forma de guardar:** `IUnitOfWork.ExecuteInTransactionAsync(work, CommitPolicy, ct)`, una vez por cada método público que escribe. Se valida afuera; adentro van locks → lecturas → reglas → escrituras; después del commit, caché y logs.
3. **Result:** las reglas de negocio devuelven `Result`/`Result<T>` con errores de `<Entidad>Errors` y código `Area.Entidad.Motivo`. Las excepciones son solo para bugs. Toda respuesta de error es ProblemDetails con `code` y `traceId`.
4. **Tenant y perfiles (B2B + B2C):** una identidad por persona, con un perfil Personal y N perfiles Business. El tenant sale solo del claim `tenant_id` del perfil activo. Toda entidad de negocio es `ITenantOwned`, vive en el esquema `tenant` y su migración llama a `EnableTenantRls`. Los perfiles no comparten datos. Nunca se setea `TenantId` a mano ni se ignora el filtro `"Tenant"` fuera de la lista blanca. Las rutas declaran `[TenantKind(...)]`. Un recurso ajeno responde 404.
5. **Plataforma:** entra a una organización solo con `ITenantScope.Enter(tenantId)`, después de autorizar y de registrar el `SecurityEvent`, y nunca con una transacción abierta.
6. **Permisos, no roles:** `[HasPermission]`, `[HasCompanyPermission]` o `[HasPlatformPermission]`. Un permiso nuevo va en el catálogo, en `Permissions.resx` y `.en.resx`, y en el seed de los roles de sistema.
7. **Tiempo:** `TimeProvider` inyectado, `DateTime` UTC con sufijo `Utc`, `DateOnly` para fechas civiles, ISO con `Z`. `BannedSymbols.txt` rompe el build.
8. **Datos y formatos:** montos siempre como `Money` (monto + moneda ISO); `decimal` y nunca `double`; porcentajes como fracción; `null` para "sin dato"; redondeo `AwayFromZero` en el backend. Lo que ve un usuario en correos y WhatsApp se formatea solo con `DisplayFormatter`, con cultura y zona explícitas ([backend.md §18](docs/architecture/backend.md#18-representación-y-formato-de-datos-unificado)).
9. **Textos:** todo lo que ve el usuario sale de resources, en es y en (`ResourceParityTests`). Español rioplatense con voseo. "Tenant" nunca en pantalla: se dice "Organización".
10. **Logs:** `[LoggerMessage]` siempre; cada método público de un servicio va envuelto en `OperationLog.RunAsync`. Nunca se registran códigos, tokens, enlaces, emails ni teléfonos completos.
11. **Build:** `TreatWarningsAsErrors`. Las versiones van solo en `Directory.Packages.props`. Los secretos van en user-secrets o variables de entorno.

## Dónde va cada cosa

| Pieza | Carpeta |
|---|---|
| Controller / contrato HTTP | `Api/Controllers/<Área>/` · `Api/Contracts/<Área>/` |
| Adaptador de la petición | `Api/RequestContext/`, `Api/Tenancy/` |
| Interfaz de servicio | `Application/Interfaces/Services/` |
| Servicio y helpers (`*Policy`, `*Guard`, `*Issuer`, `*Verifier`, `*Linker`) | `Application/Services/<Área>/` |
| Repositorio, reader, `IUnitOfWork`, `ITenantScope` | `Application/Interfaces/Persistence/` |
| Puerto externo | `Application/Interfaces/Integrations/<Tema>/` |
| Modelos / validadores / opciones | `Application/Models/<Área>/` · `Application/Validation/<Área>/` · `Application/Configuration/<Área>/` |
| Textos | `Application/Resources/*.resx` + `.en.resx` |
| Entidad y errores | `Domain/<Área>/` |
| Permisos | `Domain/Authorization/Permissions.cs`, `PlatformPermissions.cs` |
| EF: repositorio / reader / configuración / migración | `Infrastructure/Persistence/{Repositories,Readers,Configurations/<Esquema>,Migrations}/` |
| Adaptador técnico o worker | `Infrastructure/<Tema>/` |
| Módulo quitable (WhatsApp) | `Domain/WhatsApp/` y `Modules/WhatsApp/` en Application, Infrastructure y Api; se enchufa por puertos del núcleo; el núcleo nunca referencia `*.Modules.*` |
| Receta paso a paso | `docs/guides/` (agregar un área, permiso nuevo, migración, prefijo de backend, quitar WhatsApp) |
| Registro DI | `DependencyInjection.cs` o `*Registration.cs` de la capa dueña |
| Tests | `tests/<Proyecto>/<Área>/`; los de aislamiento, en `Api.IntegrationTests/Tenancy/` |
| Decisión | `docs/decisions/NNNN-*.md` + índice |

## Comandos
- `dotnet build ArquitecturaBaseMultitenant.slnx`
- `dotnet test`, o `dotnet test --project tests/<P>/<P>.csproj -- --filter-class "<Clase>"`
- `aspire run` / `aspire stop`
- Migración: ver [backend.md §9](docs/architecture/backend.md#9-persistencia).
