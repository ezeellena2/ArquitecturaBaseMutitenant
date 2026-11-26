# Módulos habilitados por organización (feature flags)

**Regla:** una funcionalidad **vendible u opcional** es un **módulo** (feature). Su controller lleva `[FeatureGate]`, y la plataforma decide qué módulos tiene cada organización (y cuáles están disponibles para el acceso B2C). **Módulo ≠ permiso:** primero se pregunta si la organización tiene el módulo, y después si la persona tiene permiso.
| | Módulo | Permiso |
|---|---|---|
| Pregunta | ¿**esta organización** tiene "Reportes"? | ¿**esta persona** puede ver reportes? |
| Lo decide | la plataforma (plan, prueba, habilitación) | el administrador de la organización (roles) |
| Si no | **404** (el módulo "no existe") | **403** |

## Cómo se hace
- **Librería:** `Microsoft.FeatureManagement.AspNetCore`.
- **Catálogo:** cada módulo declara su clave, de qué lado vive (`FeatureScope`: `Consumer`, `Business` o `Both`, si usa datos compartidos) y si arranca prendido en un `IFeatureDefinitionContributor` registrado en Application, como `AssistantFeatureDefinitionContributor`. `IFeatureCatalog` junta esas definiciones con `Domain/Features/Features.cs` (`Features.All`, vacío en la plantilla).
- **Por tenant:** `platform.TenantFeatures` (`TenantId`, `FeatureKey`, `Source`, `ExpiresAtUtc?`, `ExpiresOn?`), con **una fila por origen** (único por organización, clave y origen; [ADR 0069](../decisions/0069-modulos-y-suscripciones.md)). Los cinco orígenes: `Manual` y `Trial`, que edita Plataforma; `Approved` (un pedido aprobado) y `Subscription` (un pago), que escribe la etapa del cobro; y `Plan`, solo para leer filas viejas. `IsPlatformManaged()` dice cuáles son de Plataforma (`Plan`, `Manual` y `Trial`).
- **La regla, en un solo lugar:** `TenantFeatureRow.IsModuleEnabled(rows, key, defaultEnabled, nowUtc)`. Sin filas del módulo vale su valor de entrada; con filas, está prendido si **alguna** está vigente, así que una prueba vencida no apaga una habilitación manual. La usan el filtro (y con él `FeatureService`, `[FeatureGate]` y `/api/me`) y la política de WhatsApp para los trabajos sin sesión (`WhatsAppModulePolicy`); nadie busca una sola fila con `SingleOrDefault`.
- **Topes (0069):** un tope con máximo de la plataforma se declara en el aporte de su módulo (`IModuleLimitContributor`, como `AssistantModuleLimitContributor` o `WhatsAppModuleLimitContributor`): la clave `<módulo>.<campo>`, su clase (`Count`, un entero, o `Amount`, un `Money`), el valor de entrada, el mínimo y de dónde lee su máximo. Se lee solo por `ModuleLimitResolver.ResolveAsync(key, organizationId, ct)`, que da el tope (`ModuleLimitCeiling`); lo que elige la organización se valida contra ese tope, y en cada uso vale `Effective(lo elegido)`, el menor entre lo elegido (o el de entrada) y el tope, sin tocar lo guardado. El getter de cada máximo lo leen solo su aporte y la pantalla de Plataforma de su módulo, que lo muestra para editarlo, y ningún módulo nombra la configuración ni las claves de otro. Un tope nuevo suma su getter a la lista cerrada de `ModuleLimitArchitectureTests`.
- El contrato de Plataforma usa la fecha civil `expiresOn`; el writer la convierte al fin exclusivo del día en `TenantSettings.DefaultTimeZoneId`, con `GetDayRangeUtc`, dentro del alcance y la UoW autorizados. Se conserva la civil para su lectura global sin consultar Settings privados; una habilitación histórica sin civil mantiene null. Los gates siguen evaluando UTC.
- **Filtro:** `TenantFeatureFilter` (el filtro `"Tenant"` de la librería) lee los módulos del tenant activo con caché `t:{tenantId}:features`, que se invalida al cambiarlos.
- FeatureManagement y el filtro son scoped porque dependen del contexto firmado. La caché contiene filas y fechas, nunca el resultado de vigencia; una prueba vence en el instante `ExpiresAtUtc` aunque la fila siga cacheada. La comprobación HTTP previa corre después de autenticar y resolver el acceso y antes de los permisos; el handler de la librería también devuelve ProblemDetails 404.
- **Configuración** (`appsettings`): cada módulo se declara con `EnabledFor: [{ "Name": "Tenant" }]`. Ponerlo en `false` es el **apagado de emergencia** para todos.
- **Controller de un módulo:** `[FeatureGate("clave")]` (como `[FeatureGate("assistant")]` en `BusinessAssistantController`) + `[Access]` y el permiso real Business/Platform. Consumer no lleva permiso explícito. Apagado, responde404 ProblemDetails (`DisabledFeatureHandler`). Una interacción creada desde `[PublicSite][Access(Consumer)]` comprueba el módulo de la organización publicada, sin cambiar el tenant del token.
- La edición privada de la página usa `PublicPageController`; la lectura anónima y el directorio usan `PublicSiteController` y `DirectoryController`. Son núcleo y no se agregan features sintéticas al catálogo productivo.
- **Apagar no toca los roles:** los permisos del módulo siguen guardados y vuelven a valer al prenderlo otra vez, con cualquier origen. Para que alguien deje de tenerlos, se le quitan del rol.
- **Apagar no borra datos ([0069](../decisions/0069-modulos-y-suscripciones.md)):** con el módulo apagado sus rutas dan 404 y sus trabajos de fondo no lo atienden, pero sus filas siguen en la base y al prenderlo se ven igual. El plazo de lo que sigue guardado es la purga de siempre, que corre igual con el módulo apagado; apagar no borra ni adelanta nada.
- **Workers y correos del módulo:** preguntan `IFeatureService.IsEnabledAsync(Features.X)` (un puerto de Application, sobre `IFeatureManager`) antes de hacer algo por un tenant.
- **`GET /api/me`** devuelve `features: ["reportes", …]` del acceso activo. Un módulo de datos compartidos está prendido para una persona si lo está para la organización con la que interactúa.
- **Plataforma:** la ficha de una organización tiene la sección "Módulos", para prenderlos, apagarlos y fijar la fecha de fin de una prueba, con motivo y `SecurityEvent`.
- `GET /api/platform/tenants/{id}/features` devuelve el catálogo Business y, de cada módulo, solo la fila de Plataforma (`IsPlatformManaged()`), o sus valores iniciales con versión 0 cuando no existe; el `PUT` edita esa misma fila y nunca una de otro origen. Si hubiera varias de Plataforma, que hoy solo arma un test, las dos toman la más reciente: el reader ordena por clave y, dentro de cada clave, por fecha de cambio de la más nueva a la más vieja. `PUT /{featureKey}` exige enabled, source, version y reason; Trial exige vencimiento. La habilitación y el evento se confirman juntos; una edición vieja o una carrera de alta devuelve 409. La plantilla mantiene el catálogo vacío hasta incorporar módulos reales.
- **El núcleo no es un módulo:** usuarios, roles, empresas, configuración, auditoría y la página pública (con el directorio) están siempre prendidos. Que una organización publique su página o no es su estado (`Draft`/`Published`), no un módulo.
- **Puertos de composición y disponibilidad por acceso:** Cada módulo aporta `IFeatureDefinitionContributor`; el catálogo agregado rechaza claves duplicadas. `IFeatureAvailabilityPolicy.Key` cubre sólo su clave: `true`/`false` decide disponibilidad completa del acceso, `null` delega a `TenantFeatures`. `FeatureService` y el filtro scoped `TenantFeatureFilter` usan esa misma semántica, porque el middleware y `[FeatureGate]` son dos puertas. El apagado de emergencia de configuración prevalece. Consumer y Business requieren un espacio de un alcance compatible; Platform sin espacio sólo se habilita por una decisión explícita de su policy. Sin policy, Platform queda apagado. Las policies duplicadas se rechazan.

Los defaults de FeatureManagement se generan desde el catálogo completo después de que Api compone módulos, sin importar nombres de módulos desde el núcleo. `IModuleCapabilityContributor` añade capacidades neutrales a `/api/me`; `ProfileModulesBuilder` agrupa features y capacidades y ocupa una dependencia de `ProfileSnapshotBuilder`, manteniendo su límite de ocho.

## Prohibido
- `if (tenant.Plan == "Pro")` en el código.
- Chequear el módulo solo en el front.
- Usar un permiso para simular un plan, o un módulo para simular un permiso.
- Un módulo sin su entrada en el catálogo.
- Dejar flags viejos: un módulo que ya está prendido para todos se saca del catálogo con su ADR.

## Copiá de
- `Infrastructure/Features/TenantFeatureFilter.cs` (E5) · la guía [`agregar-un-area.md`](../guides/agregar-un-area.md) (E4), paso "¿es un módulo?" (E5).

## Lo verifica
- `ModuleControllersTests` (E5) · `TenantFeatureRowTests` y `TenantFeatureMultipleSourcesTests` (varias filas por módulo) · `WhatsAppModuleOffKeepsDataTests` y `AssistantModuleOffKeepsDataTests` (apagar no borra)
- `ModuleLimitArchitectureTests` (E12, ADR 0069; topes: cada máximo de la plataforma declarado y leído solo por su aporte y su pantalla de Plataforma; ningún módulo nombra la configuración de otro; control negativo) · `ModuleLimitResolverTests` (E12: el tope y `Effective`)

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas) · front: `docs/rules/accesos-y-permisos.md`

La metadata `InstalledModuleRouteCategory` distingue webhook técnico anónimo de chat/gestión globales de Plataforma. Los últimos requieren `Access.Platform` exclusivo y `HasPlatformPermission` en el controller o en cada acción; no admiten `AllowAnonymous`. El webhook técnico exige anonimato y ausencia de access/permisos. `ModuleControllersTests` verifica estas categorías con negativos, sin exenciones por nombres de clase.
