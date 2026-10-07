Casos de uso B2C del producto; esta carpeta sólo trae la guía. Leé [Personal](../../../../docs/features/personal.md).
Privado: `ITenantOwned` en `tenant`; lo sella el interceptor desde `tenant_id`, nunca el host. Consumer no pide roles ni permisos personales explícitos.
Módulo opcional: clave en `Domain/Features/Features.cs`, alcance Consumer/Both y `[FeatureGate]`; apagado da 404.
Publicado: `IPublishedByBusiness` en `public_site`, leído con `[PublicSite]`; compartido: `IConsumerBusinessShared` en `engagement`, copia mínima para la empresa y campos personales `[NotAudited]`.
Las transiciones pasan por `PartyPolicy`; una bandeja Business real exige `[HasPermission]` y su catálogo/resources/seed. No inventes permisos para tests.
Baja: método Domain de `IConsumerBusinessShared` reemplaza la copia; retención por tipo, motivo y plazo con `IRetainedOnConsumerDeletion` en Application/Interfaces/Integrations/Legal, nunca en Domain.
Front: `areas/personal/modules/<módulo>/`, enlace `navigation/personal.ts`; publicación en `areas/storefront/<módulo>/`. Cada listado pagina; cada escritura usa una UoW y cada método OperationLog.
Copiá de: `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/TestFeatures/Isolation/PersonalWidgetService.cs`; verifican `PersonalIsolationTests` y `EngagementTests`.
