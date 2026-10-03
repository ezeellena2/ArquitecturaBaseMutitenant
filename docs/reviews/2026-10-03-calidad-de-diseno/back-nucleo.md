# Calidad de diseño · Back · núcleo compartido y arquitectura general

> Informe de un revisor, solo lectura, sobre el commit `1202433 del back`. Los números de línea son de ese commit: buscá por símbolo. Antes de corregir un hallazgo, confirmalo en el código actual. El resumen, los bugs confirmados y el orden de trabajo están en [`../2026-10-03-calidad-de-diseno.md`](../2026-10-03-calidad-de-diseno.md).

# Revisión de diseño · Back · núcleo compartido y arquitectura general

Raíz: `la raíz del repo`, commit 1202433. La copia congelada está en una copia congelada. Todos los números de línea son de ese commit y los verifiqué uno por uno.

Abreviaturas: `Dom/`, `App/`, `Infra/` y `Api/` son `src/ArquitecturaBaseMultitenant.{Domain,Application,Infrastructure,Api}/`. `ArchT/` es `tests/ArquitecturaBaseMultitenant.ArchitectureTests/` e `IntT/` es `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/`.

Fue solo lectura: no corrí build ni tests. No revisé Plataforma; solo la cito como evidencia de volumen. Para comparar con la base leí `../ArquitecturaBase`.

## 1. Puntajes

| # | Criterio | Nota | Por qué |
|---|---|---|---|
| 1 | Responsabilidad única | 3 | Las piezas chicas del núcleo son ejemplares: `UnitOfWork`, `TenantContext`, `CacheKeys` y `HybridCacheExtensions`. En cambio, `DisplayFormatter`, `AddApplication`, `AuditTrailInterceptor` y `TenantResolutionMiddleware` mezclan entre 3 y 5 responsabilidades cada uno. |
| 2 | Inyección de dependencias | 3 | Los lifetimes están bien: las cachés singleton usan `IServiceScopeFactory` y no hay dependencias cautivas. Fallan el registro monolítico, un registro muerto, las opciones de Application registradas en Infrastructure y un grafo que cambia según el modo de host. |
| 3 | Capas | 4 | Los tests verifican la dirección de dependencias y Domain usa solo la BCL. Hay fugas puntuales: Application conoce nombres de índices de EF y carga un adaptador JSON que solo usan los tests, y la política de acceso vive en un middleware de la Api. |
| 4 | Abstracciones del núcleo | 4 | `Result`/`Error`, `IUnitOfWork` (un solo método), `ITenantScope` y `CacheKeys` son las justas; no hay genéricos ni frameworks propios de más. Sobran algunos puertos de un método (los locks) y `TenantJobRunner`, que solo usan los tests. Falta una abstracción para enmascarar datos personales. |
| 5 | Duplicación | 3 | Hay repetición mecánica en interceptores, escritura de ProblemDetails (×4), lectura de claims (×3), enmascarado (×5), los cinco pipelines de `ReferenceDataService`, las cinco reglas `Valid*` y los tres lectores de IL. |
| 6 | Arnés | 3 | Las guardas de aislamiento, capas, guardado y caché aportan mucho y tienen caso de control. Pero dos guardas empujan a malas prácticas medibles (el tope de 8 y la de formato), y otra congela rutas de archivo. |
| 7 | Clean code | 3 | Fuera de los puntos calientes, los nombres son precisos y los métodos cortos. Hay valores mágicos repetidos («GetDocument.Insider» ×7, «Testing» ×8, «appdb» ×4, roles de base ×12), llaves en estilo mixto y condiciones booleanas densas. |
| 8 | Errores y ProblemDetails | 4 | Hay un solo mapper, `code` y `traceId` siempre, y `Result` sin excepciones de negocio. Pero la escritura de ProblemDetails se repite en 4 lugares y el cursor inválido viaja como una segunda excepción de negocio. |
| 9 | Tests | 3 | Los nombres son frases, los dobles están hechos a mano, se usa `FakeTimeProvider` y cada detector tiene su caso de control. En contra: la integración corre en una sola colección secuencial y levanta 19 contenedores extra, y el contrato de formatos prueba un camino que producción no usa. |
| 10 | Consistencia con ArquitecturaBase | 3 | `Result`, UoW, `OperationLog`, el mapper y los interceptores son copia fiel. Se aparta en cuatro cosas: la base registra las opciones en `AddApplication`, tiene una constante para la conexión, no tiene ningún chequeo de modo de host, y su tope de dependencias admite excepciones con motivo. |

## 2. Hallazgos (los más importantes primero)

### 1. [ALTA] El tope de 8 dependencias genera clases pasamanos y ya contradice la regla del guardado

**Evidencia**
- `ArchT/ServiceDependencyCountTests.cs:13`: `MaximumDependencies = 8`, y cuenta `ILogger` y `TimeProvider` (`:33`).
- Las reglas del propio proyecto ocupan 3 o 4 de esos 8 lugares: `ILogger` y `TimeProvider` (por `OperationLog`), `IRequestValidator` y, en los que escriben, `IUnitOfWork`.
- `App/Services/OrganizationUsers/OrganizationUserLifecycleCoordinator.cs:7-21` solo delega tres métodos en tres helpers. Existe para que `OrganizationUserService.cs:18-26` quede en 8 exactos; sin él tendría 10. Pasa lo mismo con `Auth/SignupVerificationFlow.cs:7-15` y con `OrganizationUserExitEffects.cs:6-15`.
- De las 136 clases de `App/Services` con constructor primario, la moda es 8 dependencias (28 clases) y otras 23 tienen 7: se apilan contra el tope.
- El plan lo dice explícito en `docs/plans/2026-09-29-etapa-3a-ingreso.md:81`: «separando emisión y verificación para el máximo de ocho dependencias». La partición se decide por el tope, no por responsabilidad.

**Efectos colaterales**
- Unas 70 de las 139 clases de `Services` usan sufijos fuera de los cinco documentados (`*Coordinator`, `*Flow`, `*Core`, `*Runner`, `*Effects`…).
- Como el límite transaccional bajó a los helpers, `ArchT/TransactionBoundaryTests.cs:24-40` mantiene una lista de 14 helpers que reciben `IUnitOfWork`, más un caso especial en `:196-198`. Eso contradice `backend.md:109` («Los helpers no guardan ni reciben IUnitOfWork») y `:467`.
- En la base, el mismo tope ya condicionó una decisión de corrección: en `../ArquitecturaBase/docs/features/identidad.md:11` no se tomó un lock porque habría sido la novena dependencia.

**Contraprueba.** El tope es regla escrita, pero no está entre lo declarado intencional y el pedido lo pone en duda. El daño es medible (pasamanos y una lista blanca que contradice la regla); no es una cuestión de gusto.

**Refactor**
- Contar solo dependencias de negocio: excluir `ILogger<T>`, `TimeProvider`, `IRequestValidator`, `IUnitOfWork` e `IOptions<T>`, con un tope de 6 a 8.
- Recuperar el diccionario de excepciones con motivo que tiene la base.
- Agregar un detector de pasamanos (una clase cuyos métodos son todos una sola llamada a un campo) y reabsorber los tres casos.
- Reemplazar la lista de `TransactionBoundaryTests` por un atributo, por ejemplo `[UseCaseBoundary]`.
- Actualizar `backend.md:109` con los sufijos reales.

**Esfuerzo:** M. **Cuándo:** ahora, antes de cerrar Plataforma, que ya suma 17 registros (12 helpers).

### 2. [MEDIA] `AddApplication`: 143 líneas sin orden por área, un registro muerto y las opciones en la capa equivocada

**Evidencia**
- `App/DependencyInjection.cs:25-169` tiene unos 135 registros intercalados (Companies → OrganizationUsers → Platform → LoginCode → Signup → Google → Legal → Invitations → LoginMethods → Reauth → Google otra vez).
- `:110` registra `AddScoped<ILastTenantAdminGuard, LastTenantAdminGuard>()` y `:119` registra `AddScoped<LastTenantAdminGuard>()`. Nadie inyecta la clase concreta: el segundo es un registro muerto, y si alguien la pidiera tendría otra instancia por scope.
- Las opciones de Application (`LoginCodeOptions`, `InvitationOptions` y `OutboxOptions`) se registran en `Infra/DependencyInjection.cs:48-61` y en `Infra/Messaging/MessagingRegistration.cs:16-19`. Eso va contra `backend.md:528` y contra la base, que las registra en `AddApplication`.
- Ningún lado pide validar el grafo de DI: en el entorno Testing no corre `ValidateOnBuild`. Solo lo validan de rebote los tests de OpenAPI que levantan Development.

**Contraprueba.** El registro explícito, uno por uno, es regla y no lo cuestiono. Partirlo en un `*Registration.cs` por área está permitido por `AGENTS.md`.

**Refactor**
- Un `Services/<Área>/<Área>Registration.cs` por área (`AddRoles`, `AddCompanies`, `AddOrganizationUsers`, `AddAuth`, `AddInvitations`, `AddLegal`, `AddPlatform`…).
- `AddApplication` queda en opciones, unas 15 llamadas y los validadores.
- Mover las tres opciones a `AddApplication`.
- Un test que arme `new ServiceCollection().AddApplication()` y verifique que cada clase de `Services` se registra una sola vez.
- `ValidateOnBuild = true` explícito en `ApiFactory`.

**Esfuerzo:** M. **Cuándo:** ahora.

### 3. [MEDIA] El modo de host está desparramado y el grafo de producción no es el que se prueba

**Evidencia**
- «GetDocument.Insider» aparece 7 veces en 5 archivos: `Api/Program.cs:30`, `Infra/DependencyInjection.cs:72` y `:81`, `Infra/Identity/IdentityRegistration.cs:79`, `Infra/Identity/OpenIddict/OpenIddictRegistration.cs:31` y `:105`, y `Infra/Messaging/Email/EmailRegistration.cs:29`.
- Testing se chequea en 8 lugares. Siete usan el literal: `Program.cs:50`, `Infra/DependencyInjection.cs:97`, `IdentityRegistration.cs:87`, `EmailRegistration.cs:18`, `EmailOptionsValidator.cs:12`, `MessagingRegistration.cs:23` y `PlatformSeeder.cs:79`. El octavo usa la constante `TestingEnvironment`, que nadie más usa.
- `Infra/DependencyInjection.cs:92-98` registra `IIdempotencyStore` y dos workers solo si existe la conexión «appdb».

**Consecuencias**
- `OutboxDispatcher` y `AccountDeletionWorker` nunca se registran en los tests.
- Sin «appdb», `IdempotencyFilter` falla recién cuando se resuelve.
- Como Testing saltea `SeedDatabaseAsync`, las referencias se siembran por un segundo camino: un contenedor de DI armado a mano en `Infra/Persistence/PersistenceRegistration.cs:21-38`, que llama `DatabaseBootstrapExtensions.cs:83`. En Development ese camino corre además del `DatabaseSeeder`.
- La base no tiene ninguno de estos chequeos y usa una constante `DatabaseConnectionName`; acá «appdb» está escrito 4 veces.

**Contraprueba.** El chequeo de GetDocument.Insider es el recurso documentado para exportar OpenAPI al compilar. El problema es la dispersión y el acople al nombre del entorno de pruebas, no el chequeo.

**Refactor**
- Detectar el modo una sola vez en `Program` y pasarlo a `AddInfrastructure`.
- Apagar workers y seed con opciones (`BackgroundJobs:Enabled`, `Seed:OnStartup`) que `ApiFactory` pone en false, en lugar de mirar el nombre del entorno.
- Registrar siempre `IIdempotencyStore`.
- Una constante única para «appdb».

**Esfuerzo:** M. **Cuándo:** ahora, antes de sumar más workers.

### 4. [MEDIA] Application conoce los nombres de índices que genera EF

**Evidencia**
- `App/Services/Companies/CompanyService.cs:25`: `"IX_Companies_TenantId_TaxCountry_TaxType_TaxNumber"`.
- `OrganizationSettings/OrganizationDomainRegistrar.cs:17`: `"IX_TenantDomains_Domain"`.
- `PublicSite/PublicPageEditor.cs:19` y `when (exception.ConstraintName == "IX_Tenants_Slug")` en `Auth/BusinessSignupTransactionRunner.cs:43` y en `Platform/TenantAdministrationService.cs:38`: el mismo literal tres veces.
- `Platform/PlatformFeatureService.cs:32`.

**Problema.** Es una fuga de capa: esos nombres salen de la convención de EF en Infrastructure. Y es un acople silencioso: si se renombra una columna o un índice, el `catch` deja de coincidir y el 409 o el error de campo se vuelve un 500, sin error de compilación.

**Contraprueba.** `backend.md:462` pide que el servicio convierta la excepción en su Error, no que compare nombres de la base. La base no compara nombres.

**Refactor**
- Claves semánticas en `App/Common/Exceptions` (`UniqueKeys.TenantSlug`, `UniqueKeys.CompanyTaxId`…), usadas por las configuraciones de EF con `HasDatabaseName(UniqueKeys.X)`.
- O bien un mapa índice → clave en `Infra/Persistence/UniqueViolations.cs`.
- En los dos casos, un test que verifique que cada clave existe en el modelo.

**Esfuerzo:** S. **Cuándo:** ahora.

### 5. [MEDIA] La guarda de formato manual empuja a atajos y deja un agujero

**Evidencia**
- `ArchT/NoManualFormattingTests.cs:117-121` prohíbe todo `string.Format` y `AppendFormat` sin mirar los argumentos. Las excepciones son una lista de clases (`:26-32`).
- `Api/ErrorHandling/RetryAfterHeaderFormatter.cs:8` es una clase de una línea que existe para alojar `seconds.ToString(CultureInfo.InvariantCulture)` y estar en esa lista.
- `App/Configuration/Auth/BrowserSessionKeys.cs:15` hace `JsonSerializer.Serialize(instantUtc).Trim('"')` para escribir una fecha sin llamar a `ToString`.
- `App/Common/Formatting/DisplayFormatter.cs:22-23` define `FormatResource(..., params object[] args) => string.Format(profile.Culture, ...)`, y lo usa `App/Resources/NotificationTexts.cs:29-30` con `params object[]`. Un `decimal` o un `DateTime` que se pase ahí sale con el formato de .NET, no con los patrones del catálogo, y la guarda no lo ve porque la llamada vive dentro del tipo permitido.

**Contraprueba.** «Nadie formatea a mano» (§18) es intencional. Lo que falla es la guarda, que confunde el formato de transporte invariante con la presentación a una persona.

**Refactor**
- Cambiar la firma a `NotificationTexts.Format(string key, CultureProfile profile, params string[] args)` y sacar `FormatResource` de `DisplayFormatter`.
- Acotar la guarda a números y fechas formateados con un proveedor que depende de la cultura, y permitir `InvariantCulture`.
- Borrar `RetryAfterHeaderFormatter` y el truco de JSON.

**Esfuerzo:** S. **Cuándo:** ahora.

### 6. [MEDIA] `DisplayFormatter`: el contrato compartido con el front prueba un camino que producción no puede llamar

**Evidencia**
- `DisplayFormatter.cs:133-168` define `internal async Task<string> FormatAsync(string type, JsonElement input, ...)`.
- De los 23 tipos de `format-cases.json`, solo 5 pasan por la API pública: dateTime, decimal, percent, money y phone. Los otros 18 se resuelven en métodos privados que reciben `JsonElement` (`:170-380`) o en el propio switch.
- Producción usa `CreateAsync`, `FormatDate` y `FormatTime` (`Infra/Messaging/Email/EmailTemplateRenderer.cs:99-101`, `:124-127` y `:140-142`).
- El caso «date» del contrato pasa por `FormatDate(JsonElement)` (`:142`, `:170`), no por el `FormatDate(DateTime)` que usan los correos, y no hay método tipado para `DateOnly`.
- Application referencia System.Text.Json solo para esto.

**Problema.** Son dos responsabilidades (adaptar JSON y formatear) en 400 líneas. El día que un correo necesite «hace 5 minutos» o un CUIT, se escribe otro método, y la lógica se duplica o se aparta del contrato con el front.

**Contraprueba.** `backend.md:796` permite el despachador interno para las pruebas. Lo que no se cumple es la otra mitad de §18, «presenta todos los tipos»: la mayoría existe solo del lado del despachador.

**Refactor**
- API tipada para todos los tipos: `FormatDate(DateOnly)`, `FormatRelative`, `FormatCompact`, `FormatFileSize`, `FormatDuration(TimeSpan)`, `FormatTimeZoneAsync`, `FormatCultureAsync`, `FormatTaxIdAsync(TaxId)`, `FormatEmail(Email)`, `FormatEnum` y `FormatBoolean`.
- Mover el adaptador `JsonElement` → API tipada al proyecto de tests.

**Esfuerzo:** M. **Cuándo:** al tocarlo, antes del próximo correo que use otro tipo.

### 7. [MEDIA] `TenantResolutionMiddleware`: la política de acceso vive en la Api, con rutas escritas a mano

**Evidencia**
- `Api/Tenancy/TenantResolutionMiddleware.cs:18-148` es un `InvokeAsync` de 130 líneas con 12 salidas tempranas. Lee los claims, valida los estados de cuenta, organización y membresía, completa el error leyendo `ITenantReader` (`:115`) y decide excepciones por ruta: `:51` (`"/connect"`), `:87-88` (`new PathString("/api/me")`) y `:163-167` (`"/connect"`, `"/account"`, `"/api/auth"`).
- El claim «sub» se vuelve a leer en `Api/RequestContext/CurrentUser.cs:20` y en `InvitationFlowContext.cs:36`.
- El claim «access» se lee como literal en `CurrentUser.cs:25`, con un switch propio, aunque existe `TenantClaimTypes.Access`; `AccessFilter.cs:27` lo vuelve a interpretar.

**Contraprueba.** El middleware es parte del pipeline obligatorio (§16). Pero el repo ya tiene el patrón correcto: `Api/Legal/LegalAcceptanceMiddleware.cs` delega en `ILegalAcceptanceService`.

**Refactor**
- Un único `AccessClaims.TryRead(ClaimsPrincipal)`.
- Un `ITenantAccessService` en Application que devuelva `Result` con los errores de estado y su metadata, testeable sin HTTP.
- Metadata en los endpoints (`[AllowInactiveMembership]`, `[AccountScope]`) en lugar de prefijos de ruta.

**Esfuerzo:** M. **Cuándo:** al tocarlo, con los tests de Tenancy como red.

### 8. [MEDIA] `AuditTrailInterceptor` hace cinco cosas y conoce entidades concretas

**Evidencia**
- `Infra/Persistence/Interceptors/AuditTrailInterceptor.cs:58`: `entry.Entity is not (PlatformSettings or TenantFeature)`.
- `:94-117` exige `SecurityEvent` de tipos concretos (`TenantFeaturesChanged`, `PlatformSettingsChanged`).
- `:188-209` enmascara por nombre de propiedad (`propertyName.Contains("Email")`, «Phone»).
- Además verifica que el historial solo agregue (`:119-126`), arma el diff (`:167-186`) y escribe SQL de contrapartes (`:211-248`).

**Problema.** Cada entidad global auditable nueva obliga a editar este interceptor genérico. Enmascarar por nombre es frágil: un «ContactMail» o un CUIT pasan en claro, y un booleano «EmailVerified» se guarda como «***».

**Contraprueba.** El interceptor y la regla «PlatformSettings exige SecurityEvent» son intencionales (`backend.md:607`). Lo que cuestiono es dónde vive la regla, no la regla.

**Refactor**
- Extraer un `AuditDiffBuilder` puro: el diff y el enmascarado por atributo (`[MaskedInAudit(...)]`, junto a `[NotAudited]`).
- Una marca `IRequiresSecurityEvent`, implementada por `PlatformSettings` y `TenantFeature`, para que la verificación sea genérica.

**Esfuerzo:** M. **Cuándo:** al tocarlo, con la próxima entidad global auditable.

### 9. [MEDIA] Nada obliga a usar `ValidationRules`: hay reglas con el mensaje por defecto, en inglés

**Evidencia**
- `App/Validation/PublicSite/UpdatePublicPageRequestValidator.cs:16`: `RuleFor(request => request.Name).NotEmpty().MaximumLength(TextLimits.OrganizationName);`, sin `WithMessage` y sin `LanguageManager` configurado en ningún lado. Ya existe `ValidationRules.OrganizationName()` (`App/Common/Validation/ValidationRules.cs:24-27`).
- `:15` y `:27` usan `ValidationTexts.PageInvalid` («La página debe estar entre {From} y {To}.», el texto del paginado) para el slug y para el conflicto del logo.

**Contraprueba.** La regla 9 de `AGENTS.md` exige que los textos salgan de resources, y ninguna guarda lo verifica en los validadores.

**Refactor.** Un test que instancie cada `IValidator` del ensamblado y recorra `CreateDescriptor()` exigiendo un mensaje propio en cada componente.

**Esfuerzo:** S. **Cuándo:** ahora.

### 10. [MEDIA] Enmascarado de datos personales: cinco implementaciones con salidas distintas

**Evidencia**
- `AuditTrailInterceptor.cs:195-206` produce «***@dominio» y «***1234».
- `App/Services/Identity/LoginMethodNotifier.cs:28-34` produce «a***@dominio» y «••••1234», y está copiado en `AccountLoginMethodsService.cs:48-53`.
- `Infra/Persistence/Readers/OrganizationAuditReader.cs:234` tiene su propia versión.
- `IPhoneNumberParser.Mask` (`Infra/Phones/LibPhoneNumberParser.cs:42`) solo lo usan los tests (`LibPhoneNumberParserTests.cs:56`), y además mete presentación en un parser.

**Contraprueba.** No es gusto. La regla 10 (nunca correos ni teléfonos completos) depende de que cada copia esté bien, y la misma persona ve su correo enmascarado de forma distinta en la cuenta, en la auditoría y en los correos.

**Refactor.** Un único `PersonalDataMask` en `App/Common` que usen todos; sacar `Mask` del parser.

**Esfuerzo:** S. **Cuándo:** junto con el hallazgo 8.

### 11. [MEDIA] Tiempos de la suite de integración

**Evidencia**
- `IntT/Support/ApiTestGroup.cs` reúne 106 clases en una sola colección, que xUnit corre en serie. El proyecto tiene 488 tests.
- 16 tests crean su propio `new ApiFactory()`, y cada uno levanta un contenedor nuevo con roles, base ICU, migraciones y seed. Por ejemplo, `Platform/PlatformSettingsTests.cs:24`, `:66` y `:88`, y `Legal/AccountDeletionProcessingTests.cs:40` y `:107`.
- Otros 3 levantan Postgres a mano: `Identity/DevelopmentSeedTests.cs:29`, `ProductionSeedTests.cs:36` y `ConcurrentDevelopmentSeedTests.cs:27`.
- La imagen «postgres:18.3» está escrita 4 veces.
- Hay 488 tests de integración contra 189 unitarios de Application.

**Contraprueba.** La base real es necesaria para RLS, y la colección única evita interferencias. Es una mejora de costo, no un error de diseño.

**Refactor**
- Una constante para la imagen.
- Para los casos aislados, clonar una base plantilla ya migrada (`CREATE DATABASE … TEMPLATE`) en el mismo contenedor, en vez de levantar otro.
- Colecciones paralelas para lo que no escribe.
- Bajar a tests unitarios las reglas que no prueban aislamiento.

**Esfuerzo:** M. **Cuándo:** backlog.

### 12. [BAJA] Claves de caché: el nombre del recurso se escribe dos veces y conviven dos patrones

**Evidencia**
- `Infra/Persistence/Readers/TenantSettingsReader.cs:27` llena `CacheKeys.Tenant(tenantId, "settings")`, y `Infra/Caching/TenantSettingsCache.cs:10` invalida esa misma cadena escrita de nuevo.
- Lo mismo pasa entre `PlatformSettingsReader.cs:24` y `PlatformSettingsCache.cs:9`.
- `TenantStatusCache.cs:21`/`:28` y `TenantFeatureCache.cs:20`/`:24` repiten el literal dentro de la misma clase.
- `ReferenceDataCache.cs:8-12`, en cambio, sí centraliza sus claves.
- Un error de tipeo deja la caché vieja sin que falle nada: `CacheKeyScopeTests` solo verifica el prefijo.

**Refactor.** Métodos con nombre en `CacheKeys` (por ejemplo `TenantSettings(id)`), o adoptar en todos el patrón de `ReferenceDataCache`.

**Esfuerzo:** S. **Cuándo:** al tocarlo.

### 13. [BAJA] Repetición mecánica en interceptores y en la escritura de errores, y una segunda excepción de negocio

**Evidencia**
- Los cuatro `SaveChangesInterceptor` repiten el par `SavingChanges`/`SavingChangesAsync` con el mismo `ThrowIfNull`: `AuditableEntityInterceptor.cs:12-27`, `TenantStampInterceptor.cs:13-28`, `SoftDeleteInterceptor.cs:11-26` y `AuditTrailInterceptor.cs:24-47`.
- Tres middlewares repiten `FromError` → `StatusCode` → `WriteAsync`: `TenantResolutionMiddleware.cs:150-161`, `LegalAcceptanceMiddleware.cs:30-34` y `FeatureGateMiddleware.cs:36-43`.
- El 429 está armado en línea en `Api/DependencyInjection.cs:59-79`, aunque existe `RateLimitingExtensions`.
- El cursor inválido viaja como excepción: `Infra/Persistence/Extensions/QueryableExtensions.cs:139-141` la lanza y `Api/ErrorHandling/GlobalExceptionHandler.cs:19-25` la convierte en 400. Pasa porque `CursorCodec` vive en Infrastructure. Va contra `backend.md:496`, que dice que la de concurrencia es la única excepción de ese tipo, y `CursorRequestValidator.cs:7` promete una validación que no hace.

**Refactor**
- Una base `SavingInterceptor` con `OnSaving(DbContext)`.
- Una extensión `HttpContext.WriteProblemAsync(Error)`.
- Mover el 429 a `RateLimitingExtensions`.
- Mover `CursorCodec` (solo usa la BCL) a `App/Common/Pagination`, para que el validador devuelva un `ValidationError` sobre `after`.

**Esfuerzo:** S. **Cuándo:** al tocarlo.

### 14. [BAJA] Valores mágicos y estilo mixto

**Evidencia**
- «mt_owner» y «mt_app» están escritos 12 veces en `Infra/Persistence/DatabaseBootstrapExtensions.cs:27-130` y en `Rls/RuntimeRoleValidator.cs:17` y `:38`.
- `Dom/ValueObjects/TaxId.cs:35` usa `typeCode.Length > 16`, que repite `TextLimits.TaxIdType`.
- `TaxId.cs:49-54` tiene un switch con `"ar-cuit-mod11"` y `"ar-dni-length"`: sumar un país obliga a editar el value object.
- `Infra/BackgroundJobs/AccountDeletionWorker.cs:14` (`count < 100`) y `:41` (`FromHours(1)`).
- `Api/Idempotency/IdempotencyFilter.cs:26` y `:59` escriben los nombres de los headers en línea.
- `AdvisoryLockKeys.cs:10`, `:18`, `:25` y `:42` usan prefijos de lock sin alcance («seed:database», «google:subject:», «global:tenant-domain:», «login-code:»), frente a «t:…:lock:».
- Con `csharp_prefer_braces = true:suggestion` conviven los `if` sin llaves con los bloques.

**Refactor.** Constantes con nombre, un registro de validadores fiscales por clave, prefijos de lock con alcance como en `CacheKeys`, y las llaves como warning.

**Esfuerzo:** S. **Cuándo:** al tocarlo.

### 15. [BAJA] Ruido en el arnés: tests que congelan rutas o se prueban a sí mismos

**Evidencia**
- `ArchT/Stage3aInventoryTests.cs:6-41` exige que existan 32 rutas de archivo. Mover `TenantResolutionMiddleware.cs` o partir `ConnectService` rompe un test que no prueba ninguna conducta.
- `:50-65` comprueba que los valores de `InlineData` están en la constante del mismo test, y `:67-69` que `ClosedStages` contiene 3. Son tests de constantes.
- El IL se lee con tres implementaciones: `ArchitectureIl` (dentro de `TransactionBoundaryTests.cs:253-369`), `Support/CallSites.cs` y el recorrido propio de `NoManualFormattingTests.cs:75-94`.
- Las excepciones de las guardas son nombres completos en strings: 26 en `TransactionBoundaryTests` y 14 en `TenantScopeUsageTests`.
- `HarnessTests.cs` tiene 838 líneas.

**Refactor**
- Borrar `Stage3aInventoryTests`: la puerta ya cerró y `HarnessTests` cubre los «Lo verifica».
- Un solo lector de IL, en `Support/CallSites`.
- Pasar las excepciones a atributos en el código (`[EntersTenantScope("motivo")]`), que sobreviven a un renombre.
- Partir `HarnessTests` por tema.

**Esfuerzo:** S a M. **Cuándo:** backlog.

## 3. Lo que está bien hecho y conviene conservar como patrón

- **`UnitOfWork`:** es el único límite, prohíbe anidar, hace rollback con `CancellationToken.None` y `ChangeTracker.Clear()`, y traduce el error 23505 y los conflictos de concurrencia. Además, `CommitPolicyExtensions.Commits` se comparte con `FakeUnitOfWork`, así el doble cumple el mismo contrato.
- **`TenantContext`:** alcances que se cierran en orden inverso, y rechazo de `Enter` con una transacción abierta.
- **Filtros por clase de dato:** `ModelBuilderExtensions` los aplica con nombre, y `TenantIsolationModelValidator` hace fallar el arranque si una entidad queda sin clasificar.
- **Caché:** `HybridCacheExtensions.GetOrCreateInOwnScopeAsync`, con las cachés singleton sobre `IServiceScopeFactory`. No hay dependencias cautivas, no se cachea nada sin confirmar, y el comentario que explica el costo es un modelo de documentación. `CacheKeys` obliga a usar prefijo.
- **`QueryableExtensions`:** el orden siempre desempata por Id, la búsqueda escapa comodines y usa unaccent, y el cursor compara por fila.
- **Errores:** `Result`, `Error` y `ValidationError` son chicos y validan sus invariantes en el constructor. `FieldErrors.On` ata un error a un campo, y `ProblemDetailsMapper` no deja que la metadata pise las claves reservadas.
- **`OperationLog`:** una línea por operación y solo códigos, sin datos personales.
- **Value objects:** fábricas que devuelven `Result` (`Email` con IDN, `PhoneNumber`, `CultureCode`, `Money` con `AwayFromZero`).
- **Guardas con caso de control:** cada guarda tiene un test `Detector_*` para que no pase vacía.
- **`LegalAcceptanceMiddleware`:** delega en un servicio de Application. Es el modelo a seguir para `TenantResolutionMiddleware`.
- **`SpaExtensions`:** usa un middleware en vez de `MapFallback` y deja escrito por qué.
- **Tests unitarios:** `ServiceFixture` con dobles a mano y nombres de test que son frases.

## 4. Veredicto

El núcleo es sólido: guardado, aislamiento, caché y errores están bien resueltos y protegidos por guardas con caso de control. El costo de diseño se concentra en el arnés (el tope de 8 genera pasamanos y contradice la propia regla de la UoW), en la composición (`AddApplication` monolítico y el modo de host desparramado) y en cuatro clases que hacen demasiado. Conviene arreglar el tope, partir el registro por área y centralizar el modo de host antes de cerrar Plataforma, para que esos problemas no se multipliquen.
