# Migraciones de la base

Las migraciones productivas viven solo en `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/`. Cada cambio de modelo se entrega con su migración y con el SQL revisado. Los datos globales de referencia van en `platform`; las tablas de negocio se clasifican antes de crearlas según [multitenancy](../architecture/multitenancy.md#4-tres-clases-de-datos).

## Crear una migración

1. Definí la entidad y su `IEntityTypeConfiguration` en el esquema correcto. Una entidad `ITenantOwned` usa `tenant`, una `IPublishedByBusiness` usa `public_site` y una `IConsumerBusinessShared` usa `engagement`. Las tablas globales explícitas van en `platform` o `identity`.
2. Compilá el backend. Cargá `ConnectionStrings:appdb` desde user-secrets o una variable de entorno local para que el proyecto de inicio pueda construir el contexto; `migrations add` no se conecta a la base. Usá la versión de `dotnet-ef` correspondiente a `Microsoft.EntityFrameworkCore.Design` en `Directory.Packages.props`.
3. Desde la raíz del repo, generá la migración:

   ```text
   dotnet ef migrations add <Nombre> --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output-dir Persistence/Migrations -- --environment Development --Logging:EventLog:LogLevel:Default None
   ```

   EF genera `<ts>_<Nombre>.cs`, `<ts>_<Nombre>.Designer.cs` y actualiza `ApplicationDbContextModelSnapshot.cs`. Los tres archivos van en el mismo commit. El argumento de EventLog evita que las advertencias de EF impidan usar la herramienta en Windows sin permisos sobre el registro de eventos.
4. Revisá `Up` y `Down`: tablas, columnas, FKs, índices, nombres y esquemas. Cada tabla nueva de `tenant`, `public_site` o `engagement` llama, después de `CreateTable`, a `migrationBuilder.EnableTenantRls`, `EnablePublicRls` o `EnablePartiesRls`, respectivamente. `AuditEntries` además llama a `PreventUpdateDelete`. Para cada campo de un `SortMap` privado, creá el índice `(TenantId, campo, Id)`; para cada columna buscable, el índice GIN trigram sobre `f_unaccent(lower(col))`.
5. Revisá el SQL completo antes de commitear:

   ```text
   dotnet ef migrations script --idempotent --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api -- --environment Development --Logging:EventLog:LogLevel:Default None
   dotnet ef migrations has-pending-model-changes --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api -- --environment Development --Logging:EventLog:LogLevel:Default None
   ```

   El segundo comando debe indicar que el modelo coincide con la última migración. `MigrationsTests` comprueba lo mismo sin abrir conexión.

## Aplicación y controles

En Development, `DatabaseBootstrapExtensions` prepara la base ICU `es-AR` y aplica las migraciones con `appdb-admin` antes del seed. En los demás ambientes, el despliegue aplica un migration bundle con el rol administrador antes de iniciar la Api. El runtime usa `mt_app` y nunca aplica migraciones.

La migración inicial crea los cinco esquemas, `unaccent`, `pg_trgm`, `public.f_unaccent`, las funciones de triggers, grants para `mt_app`, las once tablas de referencia, `platform.IdempotencyKeys` y `tenant.AuditEntries`. Las tablas `Widget`, `Poster` y `Deal` de aislamiento viven únicamente en el proyecto de tests: `IsolationSchema.ApplyAsync` las crea después de aplicar las migraciones reales.

No edites una migración ya aplicada. Si el modelo cambia, generá otra migración. Para revisar una migración nueva todavía no aplicada, corregí la fuente y regenerala antes de commitear.
