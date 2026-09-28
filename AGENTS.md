# ArquitecturaBaseMultitenant: reglas para agentes

Versión multitenant **B2B + B2C** de `../ArquitecturaBase`, como Mercado Libre y para cualquier tipo de negocio: una persona tiene una sola cuenta con dos accesos que no se mezclan, **como persona** (B2C) y **como empresa** (B2B); cada organización puede tener su página pública en un subdominio, y las personas interactúan con ella. La plantilla trae los accesos y la mecánica; no trae módulos de negocio. Usa .NET 10, Aspire 13.5 y PostgreSQL. El front vive en `../ArquitecturaBaseMutitenantFront`. Este archivo es el índice de reglas.

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
| guardar o validar un teléfono | [telefonos](docs/rules/telefonos.md) |
| recibir un correo, un CUIT o DNI, o un texto libre | [emails](docs/rules/emails.md), [identificacion-fiscal](docs/rules/identificacion-fiscal.md), [textos-libres](docs/rules/textos-libres.md) |
| hacer una pantalla que edita algo | [concurrencia](docs/rules/concurrencia.md) |
| hacer un `POST` que crea o envía | [idempotencia](docs/rules/idempotencia.md) |
| agregar una funcionalidad vendible u opcional | [modulos-habilitados](docs/rules/modulos-habilitados.md) |
| tocar el registro, la cuenta o datos personales | [datos-personales](docs/rules/datos-personales.md) |
| mostrar un texto al usuario | [textos-y-traducciones](docs/rules/textos-y-traducciones.md) |
| crear una entidad o una tabla | [persistencia-ef](docs/rules/persistencia-ef.md), [multitenancy](docs/rules/multitenancy.md), [auditoria](docs/rules/auditoria.md) |
| exponer una ruta | [api-http](docs/rules/api-http.md), [permisos](docs/rules/permisos.md) |
| loguear | [logs](docs/rules/logs.md) |
| tocar WhatsApp u otro módulo | [modulos](docs/rules/modulos.md); un mensaje nuevo por WhatsApp: [plantillas](docs/operations/whatsapp-plantillas.md) |
| configurar Google, Gmail o WhatsApp, o cargar secretos | [configuracion](docs/operations/configuracion.md) (nunca un secreto en el repo) |
| escribir tests | [tests](docs/rules/tests.md) |
| agregar un área completa | `docs/guides/agregar-un-area.md` (nace en la Etapa 4) |

El inventario de todos los estándares transversales (los definidos y los propuestos) está en [`docs/architecture/estandares.md`](docs/architecture/estandares.md). Si una regla no está escrita: **copiá cómo lo resuelve ArquitecturaBase** (`../ArquitecturaBase`, `../ArquitecturaBaseFront`); si tampoco está ahí, **decidí vos lo más simple y coherente con estos docs, anotalo en la sección «Decisiones tomadas» del informe de la etapa y seguí**. Frená y preguntá **solo** si la decisión cambia el producto (qué ve o puede hacer un usuario, una pantalla del lienzo, el modelo de accesos) o contradice una regla escrita. Una duda técnica menor nunca frena una etapa. Después se agrega la ficha y su test.

## Forma de trabajo
- Commits chicos, en español, con conventional commits. **Commitear al cerrar cada tarea**: el código que nunca se commitea se pierde.
- TDD donde hay lógica. Antes de dar algo por terminado: `dotnet build` sin advertencias y `dotnet test` en verde. Los tests de integración necesitan Docker. Apagar Aspire con `aspire stop`.
- Una pantalla nueva del front se dibuja antes de programarse.

## Reglas que no se negocian
1. **Capas:** Domain ← Application ← Infrastructure; Api compone. El recorrido es `Controllers → I<X>Service → <X>Service → I<X>Repository/I<X>Reader/Integrations → Infrastructure`. Sin handlers ni CQRS con `ICommand`, sin repositorio genérico, sin Minimal APIs de negocio, sin Scrutor ni AutoMapper.
2. **Una sola forma de guardar:** `IUnitOfWork.ExecuteInTransactionAsync(work, CommitPolicy, ct)`, una vez por cada método público que escribe. Se valida afuera; adentro van locks → lecturas → reglas → escrituras; después del commit, caché y logs.
3. **Result:** las reglas de negocio devuelven `Result`/`Result<T>` con errores de `<Entidad>Errors` y código `Area.Entidad.Motivo`. Las excepciones son solo para bugs. Toda respuesta de error es ProblemDetails con `code` y `traceId`.
4. **Accesos y tenant (B2C + B2B):** una cuenta, dos accesos (`access=consumer|business`) más `platform`. Una persona B2C nunca crea empresas. El tenant sale solo del claim `tenant_id`; el subdominio resuelve solo lo público. Todo dato se clasifica: privado (`ITenantOwned`, `tenant`), público (`IPublishedByBusiness`, `public_site`) o compartido (`IConsumerBusinessShared`, `engagement`), con su helper RLS. Nunca se setean a mano las columnas de tenant ni se ignoran filtros fuera de la lista blanca. Las rutas declaran `[Access(...)]` o `[PublicSite]` (solo el subdominio de una organización publicada); las anónimas del dominio principal (ingreso, registro, invitación, legales, directorio, webhooks) llevan solo `[AllowAnonymous]` y su controller va en la lista explícita de `AccessDeclarationTests`. Un recurso ajeno responde 404 ([multitenancy](docs/rules/multitenancy.md)).
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
| Permisos | `Domain/Authorization/Permissions.cs` (organización y empresa), `PersonalPermissions.cs`, `PlatformPermissions.cs` |
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
