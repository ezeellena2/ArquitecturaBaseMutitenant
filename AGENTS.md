# ArquitecturaBaseMultitenant: reglas para agentes

Versión multitenant **B2B + B2C** de `../ArquitecturaBase`, como Mercado Libre y para cualquier tipo de negocio: una persona tiene una sola cuenta con dos accesos que no se mezclan, **como persona** (B2C) y **como empresa** (B2B); cada organización puede tener su página pública en un subdominio, y las personas interactúan con ella. La plantilla trae los accesos y la mecánica; no trae módulos de negocio. Usa .NET 10, Aspire 13.6 y PostgreSQL. El front vive en `../ArquitecturaBaseMutitenantFront`. Este archivo es el índice de reglas.

Las reglas que mandan están en este archivo y en las fichas de [`docs/rules/`](docs/rules/README.md). El panorama de la arquitectura está en dos documentos:
- [`docs/architecture/backend.md`](docs/architecture/backend.md): capas, patrones y carpetas;
- [`docs/architecture/multitenancy.md`](docs/architecture/multitenancy.md): organizaciones, empresas y aislamiento.

Cada área tiene su ficha funcional en `docs/features/`, como [Identidad e ingreso](docs/features/identidad.md). El área de referencia para copiar código es Roles ([Roles y permisos](docs/features/roles.md)). Leé la ficha del área antes de cambiar su conducta o sus límites.

Las decisiones están en [`docs/decisions/README.md`](docs/decisions/README.md) y el plan en [`docs/plans/2026-09-27-plan-de-desarrollo.md`](docs/plans/2026-09-27-plan-de-desarrollo.md).

## Antes de escribir código: el arnés

**No inventes.** Para cada tema hay una ficha en [`docs/rules/`](docs/rules/README.md), con la regla, cómo se hace, qué está prohibido, el archivo real que hay que copiar y el test que te va a frenar. Cada carpeta de código tiene además un `AGENTS.md` corto que dice qué va ahí y qué fichas leer. Cómo funciona: [`docs/architecture/arnes.md`](docs/architecture/arnes.md).

| Si vas a… | Leé |
|---|---|
| guardar algo en la base | [guardado](docs/rules/guardado.md) |
| devolver un error o validar | [result-y-errores](docs/rules/result-y-errores.md), [validacion](docs/rules/validacion.md) |
| usar una fecha u hora | [fechas-y-zonas](docs/rules/fechas-y-zonas.md) |
| usar una moneda, un país, una zona horaria, una cultura o un tipo de identificación fiscal | [datos-de-referencia](docs/rules/datos-de-referencia.md) (nunca una lista en el código) |
| usar un monto, decimal o porcentaje | [numeros-y-moneda](docs/rules/numeros-y-moneda.md) |
| hacer un listado | [paginado-y-busqueda](docs/rules/paginado-y-busqueda.md) |
| guardar o validar un teléfono | [telefonos](docs/rules/telefonos.md) |
| recibir un correo, un CUIT o DNI, o un texto libre | [emails](docs/rules/emails.md), [identificacion-fiscal](docs/rules/identificacion-fiscal.md), [textos-libres](docs/rules/textos-libres.md) |
| hacer una pantalla que edita algo | [concurrencia](docs/rules/concurrencia.md) |
| programar un trabajo de fondo (worker) | [concurrencia](docs/rules/concurrencia.md) (corre una vez entre copias), [multitenancy](docs/rules/multitenancy.md) |
| hacer un `POST` que crea o envía | [idempotencia](docs/rules/idempotencia.md) |
| agregar una funcionalidad vendible u opcional | [modulos-habilitados](docs/rules/modulos-habilitados.md) |
| tocar el registro, la cuenta o datos personales | [datos-personales](docs/rules/datos-personales.md) |
| mostrar un texto al usuario | [textos-y-traducciones](docs/rules/textos-y-traducciones.md) |
| crear una entidad o una tabla | [persistencia-ef](docs/rules/persistencia-ef.md), [multitenancy](docs/rules/multitenancy.md), [auditoria](docs/rules/auditoria.md), [migracion](docs/guides/migracion.md) (dos versiones conviven) |
| cachear un dato o cambiar uno cacheado | [cache](docs/rules/cache.md) (Redis con generaciones; nunca invalidar a mano) |
| limitar pedidos o declarar la cuota de una ruta | [cuotas](docs/rules/cuotas.md) (doce perfiles cerrados; nunca un limitador por proceso) |
| exponer una ruta | [api-http](docs/rules/api-http.md), [permisos](docs/rules/permisos.md) |
| loguear | [logs](docs/rules/logs.md) |
| tocar WhatsApp u otro módulo | [modulos](docs/rules/modulos.md); un mensaje nuevo por WhatsApp: [plantillas](docs/operations/whatsapp-plantillas.md); probarlo de punta a punta: [e2e-whatsapp](docs/guides/e2e-whatsapp.md) |
| configurar Google, Gmail o WhatsApp, o cargar secretos | [configuracion](docs/operations/configuracion.md) (nunca un secreto en el repo) |
| poner un tope, un plazo, un cupo o un valor por defecto de negocio | [reglas de negocio en pantalla](docs/operations/configuracion.md#reglas-de-negocio-en-pantalla-270) (nunca fijo en el código ni en `appsettings`) |
| escribir tests | [tests](docs/rules/tests.md) |
| crear un servicio, un helper, un puerto o un controller | [capas-y-flujo](docs/rules/capas-y-flujo.md) (la lista cerrada de sufijos de los helpers) |
| agregar un área completa | [agregar-un-area](docs/guides/agregar-un-area.md) |
| que la IA pueda crear, editar o eliminar algo | [agregar-una-accion-al-asistente](docs/guides/agregar-una-accion-al-asistente.md) ([ADR 0068](docs/decisions/0068-acciones-del-asistente.md)): la IA prepara, la persona confirma en la pantalla de siempre y guarda la ruta de siempre |
| entender cómo funciona un área o un caso | skill [`/explicar`](.agents/skills/explicar/SKILL.md) (lee la ficha y el código en el momento) |
| planificar o programar una etapa (back y front en paralelo, desde la E8) | [etapa-en-paralelo](docs/guides/etapa-en-paralelo.md), skill [`/plan-etapa`](.agents/skills/plan-etapa/SKILL.md) |
| programar una etapa entera con agentes, de punta a punta | [conducir-una-etapa](docs/guides/conducir-una-etapa.md) (el workflow `etapa`, primero con `modo: planificar`) |
| verificar o cerrar una etapa | skills [`/puerta`](.agents/skills/puerta/SKILL.md) y [`/recorrido`](.agents/skills/recorrido/SKILL.md) |
| revisar la arquitectura de un desarrollo terminado | [revisar-arquitectura](docs/guides/revisar-arquitectura.md) ([ADR 0059](docs/decisions/0059-revision-de-arquitectura.md)) |
| crear un documento o saber si uno quedó viejo | [documentacion](docs/rules/documentacion.md) |
| correr o crear un arnés ejecutable (realinear docs, programar una rutina) | [scripts/arneses/README.md](scripts/arneses/README.md), [agregar-un-arnes](docs/guides/agregar-un-arnes.md) |

El inventario de todos los estándares transversales (los definidos y los propuestos) está en [`docs/architecture/estandares.md`](docs/architecture/estandares.md). Si una regla no está escrita: **copiá cómo lo resuelve ArquitecturaBase** (`../ArquitecturaBase`, `../ArquitecturaBaseFront`); si tampoco está ahí, **decidí vos lo más simple y coherente con estos docs, anotalo en una línea del plan de la etapa y seguí**. Frená y preguntá **solo** si la decisión cambia el producto (qué ve o puede hacer un usuario, una pantalla del lienzo, el modelo de accesos) o contradice una regla escrita. Una duda técnica menor nunca frena una etapa. **Una excepción a una regla escrita** vale solo si es lo más robusto y correcto, acotada y escrita (enmienda en la ficha o nota en el plan), nunca por comodidad ni para cambiar una decisión de diseño ya confirmada (traspaso 15). Una convención transversal nueva se agrega a la ficha y se verifica con un test pertinente, existente o nuevo; una decisión local queda en la feature o el informe, con test de conducta si agrega lógica.

## Forma de trabajo
- Commits chicos, en español, con conventional commits. **Commitear cada cambio coherente y verificado**; los pasos internos de una tarea no requieren commits separados. No acumular trabajo terminado sin commitear.
- **Todo en `main`, también la puerta** (pedido del usuario, «fundamental», traspaso 10, 2026-11-07): sin ramas ni worktrees, ni siquiera temporales, y sin push salvo que lo pida. Varias sesiones trabajan a la vez en el mismo checkout: cada una commitea solo lo suyo (`git commit -- <rutas>`, o su fragmento en el índice), y nunca hace stash, reset, cambio de rama ni revierte lo de otra. Un commit se arma sobre el `HEAD` del momento: `git commit -- <rutas>` lo hace solo; un índice propio se arma con `git read-tree HEAD` justo antes, y `git update-ref` recibe el `HEAD` viejo, así falla si otro commiteó en el medio (en ArquitecturaBase un commit armado sobre una copia vieja deshizo 34 archivos). Si el checkout no compila por el trabajo a medias de otra sesión, se prueba en una copia de `HEAD` dentro de `.local/`, que se borra al terminar (allá las copias llenaron el disco con 387 GB). Con la app levantada, ninguna sesión edita el `src` del front: Vite le recarga la página al usuario y le borra lo que no guardó.
- **Con el usuario** (traspaso 10): nada de manejar su PC (computer use o escritorio remoto), y se le avisa de cualquier efecto visible en su pantalla. Ninguna clave real pasa por un agente: la pega él en la pantalla de configuración. En una prueba de punta a punta, se le dan los pasos clic por clic, con la ruta del menú, sin cargar datos por él. Para decidir: pocas opciones, la recomendada primero y una captura o un tablero antes. Las corridas largas, con la PC despierta: Docker no se recupera de una suspensión. Los videos que manda se miran por cuadros y con una transcripción local. Pierde el hilo de lo pendiente: se le recuerda desde la [hoja de ruta](docs/plans/2026-09-27-plan-de-desarrollo.md).
- **Sesiones y cupo** (traspaso 15). Tres o cuatro chats programando a la vez; no se abren sesiones en paralelo salvo que el usuario lo pida, y un chat quieto se archiva solo con su visto bueno. Una tarea de más de unos 150 pasos se parte en dos subagentes nuevos, y las correcciones de una revisión van a uno nuevo, no al que programó. **El cupo de la semana** se mira antes de empezar: con 85 % se escribe lo que falta, tarea por tarea, y con 95 % no se empieza nada nuevo; una revisión se planifica con su costo medido.
- **Un «terminado» se comprueba contra el código,** no contra los commits ni el reporte de un chat. Al listar lo que falta, se separa lo que el usuario decidió de lo que solo propone un plan.
- **Advertencias** (traspaso 15): la PC cargada parece un bug (tests que vencen, timeouts de Redis): antes de culpar al código se miran los procesos y las corridas de otras sesiones. Una corrida con un proveedor real (IA, Meta) pide el visto bueno del usuario y la máquina sin suites del back. Si la app de Claude se reinicia, las corridas en segundo plano mueren sin avisar y hay que retomarlas. **Con varias sesiones a la vez** (traspaso 18): una corrida pesada por vez (compilar la solución, una suite entera, la integración, el front entero, la corrida visual o un arnés), sin esperar una respuesta en la consola (`npx --yes`) y cortada si queda quieta; dos desarrollos que generan migraciones las regeneran una encima de la otra, en el orden en que entran, y corren el test que compara el modelo con el snapshot; sacar una clave de `appsettings` y reconstruir la app van juntos, igual que un campo nuevo del contrato y su lector en el front.
- TDD donde hay lógica: test dirigido rojo → verde y build del proyecto afectado. **Al terminar cada tarea, el build y los tests rápidos enteros, sin Docker y nunca filtrados por módulo** (traspaso 10: en ArquitecturaBase un filtro dejó pasar un test que rompió `main`). Si la tarea cambia una ruta, persistencia, autenticación o aislamiento, **al terminarla** (no antes de cada commit) se corren los tests de integración y de seguridad de esa área. La solución completa (`dotnet build` sin advertencias y `dotnet test` con Docker) se corre una vez, en la puerta, y en CI. Apagar Aspire con `aspire stop` si se levantó.
- Una pantalla nueva del front se dibuja antes de programarse, y el usuario aprueba el tablero en el lienzo. **Desde la Etapa 7 se programa directo** (pedido del usuario, 2026-10-07):
  - tests donde importan;
  - sin visto bueno por pantalla ni capturas en cada avance;
  - **una sola puerta al final:** suites completas, E2E real, documentación en 0, la galería lado a lado y el recorrido de usuario;
  - **una prueba integral con el usuario al cerrar el bloque E12–E25** (pedido del 2026-11-18), además de la puerta de cada etapa: cada chat termina todo lo que se le pidió, back y front; al final el usuario recorre todo de punta a punta, con los pasos clic por clic. Lo que encuentre se arregla en el momento solo si bloquea; lo demás va a una lista para después.

  Desde la Etapa 8, el back y el front van en paralelo, con el contrato primero ([etapa-en-paralelo](docs/guides/etapa-en-paralelo.md)).
- **Velocidad y cierre liviano** (pedido explícito del usuario, 2026-10-11). Las etapas que quedan tienen que salir mucho más rápido que la 8 y la 8b. Esas dos se programaron en dos días y tardaron otros tantos en cerrarse, por verificar y documentar de más. **El trabajo es programar**; la calidad la cuidan las reglas de abajo y sus tests, no las actas.
  - **Cada verificación pasa completa una sola vez por etapa.** Las suites de cada repo, el E2E real y el recorrido necesitan, cada uno, una corrida completa en verde con el código de la etapa. Lo que falla vuelve a su dueño, y con el arreglo se corre solo lo que ese arreglo toca: el test que falló, los de su área y, en el E2E, sus casos. No se repite todo al final ni se arma un corte nuevo por cada arreglo. La suite completa de un repo se vuelve a correr solo si el arreglo tocó algo que usa todo el repo: autenticación, persistencia base o la composición de la app.
  - **Planes cortos.** Un plan de etapa tiene unas 4.000 palabras como mucho: contrato, tareas con sus archivos y tests, y el texto para cada chat. No lleva historial.
  - **Sin actas.** El estado de un plan es una tabla de tareas (hecha o pendiente), y el resultado de la puerta es otra tabla corta. No se registra cada corrida ni se commitea documentación por cada paso. La documentación se toca cuando cambia una regla, un contrato o una decisión.
  - **Lo técnico lo decide quien programa, y lo avisa después.** Entran ahí los cambios de contrato que no cambian lo que una persona ve o puede hacer, los arreglos de scripts y de datos de prueba, y fusionar la propia documentación ya verificada. Al usuario se le pregunta solo lo de producto.
  - **Lo que no se hace** (en la puerta de la 8 hubo 177 corridas y 558 archivos de evidencia):
    - revisiones independientes por tarea o por arreglo, como subagentes revisores de especificación o de calidad, salvo la adversarial según el riesgo (abajo). La revisión de código va una vez, al cerrar la etapa;
    - repetir un test que ya pasó, ni corridas «de estabilidad»: un test que se pasó de tiempo se corre solo una vez más, aislado;
    - guardar evidencia (recibos, hashes, logs de diagnóstico): las salidas quedan en `.local/` y en el plan va solo la tabla final;
    - recapturar pantallas de etapas anteriores: la galería es solo de las pantallas nuevas o cambiadas en la etapa;
    - probar el retiro de un módulo en varias variantes: va una vez, en la etapa de ese módulo, con la base vacía (build, tests e ingreso);
    - worktrees, ramas o etiquetas, tampoco en la puerta: la puerta trabaja sobre copias del corte (skill `/puerta`).
  - **La revisión adversarial va según el riesgo** (pedido del 2026-11-18; cambia la 7A del 2026-11-07). Va completa, con su corrección, antes del commit, en la tarea que toca:
    - datos: migraciones, persistencia o cifrado;
    - permisos;
    - efectos fuera del sistema: Meta, correo, archivos o IA;
    - el aislamiento entre organizaciones: RLS, `ITenantScope`, trabajos de fondo, caché y cuotas por alcance.

    Un subagente nuevo recibe el código y los escenarios, sin la conclusión de quien programó, e intenta romperla. No hace falta en documentos, textos, tests ni arreglos de una línea.

## Reglas que no se negocian
1. **Capas:** Domain ← Application ← Infrastructure; Api compone. El recorrido es `Controllers → I<X>Service → <X>Service → I<X>Repository/I<X>Reader/Integrations → Infrastructure`. Sin handlers ni CQRS con `ICommand`, sin repositorio genérico, sin Minimal APIs de negocio, sin Scrutor ni AutoMapper.
2. **Una sola forma de guardar:** `IUnitOfWork.ExecuteInTransactionAsync(work, CommitPolicy, ct)`, una vez por cada método público que escribe. Se valida afuera; adentro van locks → lecturas → reglas → escrituras; después del commit, caché y logs.
3. **Result:** las reglas de negocio devuelven `Result`/`Result<T>` con errores de `<Entidad>Errors` y código `Area.Entidad.Motivo`. Las excepciones son solo para bugs. Toda respuesta de error es ProblemDetails con `code` y `traceId`.
4. **Accesos y tenant (B2C + B2B):** una cuenta, dos accesos (`access=consumer|business`) más `platform`. Una persona B2C nunca crea empresas. El tenant sale solo del claim `tenant_id`; el subdominio resuelve solo lo público. Todo dato se clasifica: privado (`ITenantOwned`, `tenant`), público (`IPublishedByBusiness`, `public_site`) o compartido (`IConsumerBusinessShared`, `engagement`), con su helper RLS. Nunca se setean a mano las columnas de tenant ni se ignoran filtros fuera de la lista blanca. Las rutas declaran `[Access(...)]` o `[PublicSite]` (solo el subdominio de una organización publicada); las anónimas del dominio principal (ingreso, registro, invitación, legales, contacto, directorio, webhooks) llevan solo `[AllowAnonymous]` y su controller va en la lista explícita de `AccessDeclarationTests`. Un recurso ajeno responde 404 ([multitenancy](docs/rules/multitenancy.md)).
5. **Plataforma:** entra a una organización solo con `ITenantScope.Enter(tenantId)`, después de autorizar y de registrar el `SecurityEvent`, y nunca con una transacción abierta. La cuenta de un operador es exclusiva de Plataforma, con permisos individuales y sin roles: no tiene perfiles de persona ni entra a una organización ([ADR 0042](docs/decisions/0042-operadores-de-plataforma.md)).
6. **Permisos, no roles:** `[HasPermission]`, `[HasCompanyPermission]` o `[HasPlatformPermission]`. Un permiso nuevo va en el catálogo, en `Permissions.resx` y `.en.resx`, y en el seed de los roles de sistema.
7. **Tiempo:** `TimeProvider` inyectado, `DateTime` UTC con sufijo `Utc`, `DateOnly` para fechas civiles, ISO con `Z`. `BannedSymbols.txt` rompe el build.
8. **Datos y formatos:** montos siempre como `Money` (monto + moneda ISO); `decimal` y nunca `double`; porcentajes como fracción; `null` para "sin dato"; redondeo `AwayFromZero` en el backend, salvo los cálculos de un formulario, que redondean al par ([numeros-y-moneda](docs/rules/numeros-y-moneda.md)). Lo que ve un usuario en correos y WhatsApp se formatea solo con `DisplayFormatter`, con cultura y zona explícitas ([backend.md §18](docs/architecture/backend.md#18-representación-y-formato-de-datos-unificado)).
9. **Textos:** todo lo que ve el usuario sale de resources, en es y en (`ResourceParityTests`). Español rioplatense con voseo. "Tenant" nunca en pantalla: se dice "Organización".
10. **Logs:** `[LoggerMessage]` siempre; cada método público de un servicio va envuelto en `OperationLog.RunAsync`. Nunca se registran códigos, tokens, enlaces, emails ni teléfonos completos.
11. **Build:** `TreatWarningsAsErrors`. Las versiones van solo en `Directory.Packages.props`. Los secretos van en user-secrets o variables de entorno.
12. **Varias copias y dos versiones:** la Api corre en varias copias que atienden a todas las organizaciones, y en cada despliegue conviven dos versiones. Se agrega antes de borrar, y un valor nuevo se lee un despliegue antes de escribirse ([ADR 0060](docs/decisions/0060-dos-versiones-conviven.md), [migración](docs/guides/migracion.md#convivencia-de-versiones)). Cada trabajo de fondo que borra o avisa corre una vez entre copias ([concurrencia](docs/rules/concurrencia.md)).

## Dónde va cada cosa

| Pieza | Carpeta |
|---|---|
| Controller / contrato HTTP | `Api/Controllers/<Área>/` · `Api/Contracts/<Área>/` |
| Adaptador de la petición | `Api/RequestContext/`, `Api/Tenancy/` |
| Interfaz de servicio | `Application/Interfaces/Services/` |
| Servicio y helpers (un sufijo de la lista cerrada de [capas-y-flujo](docs/rules/capas-y-flujo.md)) | `Application/Services/<Área>/` |
| Repositorio, reader, `IUnitOfWork`, `ITenantScope` | `Application/Interfaces/Persistence/` |
| Puerto externo | `Application/Interfaces/Integrations/<Tema>/` |
| Modelos / validadores / opciones | `Application/Models/<Área>/` · `Application/Validation/<Área>/` · `Application/Configuration/<Área>/` |
| Textos | `Application/Resources/*.resx` + `.en.resx` |
| Entidad y errores | `Domain/<Área>/` |
| Permisos | `Domain/Authorization/Permissions.cs` (organización y empresa), `PersonalPermissions.cs`, `PlatformPermissions.cs` |
| EF: repositorio / reader / configuración / migración | `Infrastructure/Persistence/{Repositories,Readers,Configurations/<Esquema>,Migrations}/` |
| Adaptador técnico o worker | `Infrastructure/<Tema>/` |
| Módulo quitable (WhatsApp, E8) | `Domain/WhatsApp/` y `Modules/WhatsApp/` en Application, Infrastructure y Api (E8); se enchufa por puertos del núcleo; el núcleo nunca referencia `*.Modules.*` |
| Receta paso a paso | `docs/guides/` (agregar un área, permiso nuevo, migración, prefijo de backend, quitar WhatsApp) |
| Registro DI | `DependencyInjection.cs` o `*Registration.cs` de la capa dueña |
| Tests | `tests/<Proyecto>/<Área>/`; los de aislamiento, en `Api.IntegrationTests/Tenancy/` |
| Decisión | `docs/decisions/NNNN-*.md` + índice |

## Comandos
- `dotnet build ArquitecturaBaseMultitenant.slnx`
- `dotnet test`, o `dotnet test --project tests/<P>/<P>.csproj -- --filter-class "<Clase>"`
- `aspire run` / `aspire stop`
- `node scripts/arneses/arnes.mjs realinear-docs --detectar` (0 tokens). Sin `--detectar`, el arnés completo trabaja en una copia de `HEAD` en `.local/arneses/copias/`, sin ramas ni worktrees, y `--fusionar` trae su commit con `--ff-only`. Tests: `node --test "scripts/arneses/**/*.test.mjs"`.
- `node scripts/arneses/arnes.mjs alinear-etapas --detectar` (0 tokens): dice qué etapa que falta programar no está completa o no está alineada.
- `node scripts/arneses/arnes.mjs estado-docs --detectar` (0 tokens): dice qué documentos quedaron viejos, terminaron o están fuera de formato.
- `node scripts/arneses/arnes.mjs conformidad --detectar --plan <plan>` (0 tokens): dice qué sumaron los commits de la etapa que su spec no nombra.
- Migración: ver [backend.md §9](docs/architecture/backend.md#9-persistencia).
