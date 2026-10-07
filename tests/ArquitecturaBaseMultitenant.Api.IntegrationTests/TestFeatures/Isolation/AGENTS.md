Entidades de prueba para las tres clases de datos; nunca código ni migraciones productivas.
`EngagementFactory` agrega este modelo al contexto real y `IsolationSchema` crea las tablas después de migrar.
Leé: [multitenancy](../../../../docs/rules/multitenancy.md) · [tests](../../../../docs/rules/tests.md).
Copiá de `Widget.cs` para privado, `PostersController.cs` para público y `DealsController.cs`/`DealService.cs` para compartido.
El alta Consumer toma la organización de `IPublicSiteContext`, el nombre de la cuenta y el espacio del interceptor.
Las transiciones pasan por `PartyPolicy`; el gate del alta consulta la organización pública sin cambiar el acceso.
