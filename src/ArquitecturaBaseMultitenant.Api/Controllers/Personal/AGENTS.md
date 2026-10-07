Rutas B2C del producto; esta carpeta sólo trae la guía. Leé [Personal](../../../../docs/features/personal.md).
Privado: `[Access(Consumer)]` sin permisos; `ITenantOwned` en `tenant`, tenant sólo del claim. Business sobre esa ruta da 403 `Tenancy.Access.Wrong`; un recurso ajeno da 404.
Controllers finos: contratos HTTP → `I<X>Service` → Result; leé `docs/rules/api-http.md`, `permisos.md`, `multitenancy.md`, `concurrencia.md` e `idempotencia.md`.
Todo listado pagina; edición/borrado reciben `version` y guardar devuelve `VersionResponse`; alta/envío lleva `[Idempotent]`.
Opcional: Features Consumer/Both + `[FeatureGate]`. Publicado: `IPublishedByBusiness`, `public_site`, `[PublicSite][AllowAnonymous]`; el directorio del dominio principal sólo `[AllowAnonymous]`.
Interacción: `IConsumerBusinessShared` en `engagement`, copia mínima `[NotAudited]`, transiciones por `PartyPolicy`. Alta `[PublicSite][Access(Consumer)]`; contraparte de IPublicSiteContext, no del body.
Bandeja Business real: `[Access(Business)]` + `[HasPermission]` del catálogo/resources/seed + gate; tests no agregan permisos productivos. La baja y la retención están en el puntero Services/Personal.
Copiá de: `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/Isolation/PersonalWidgetsController.cs`; verifican `PersonalIsolationTests` y `EngagementTests`.
