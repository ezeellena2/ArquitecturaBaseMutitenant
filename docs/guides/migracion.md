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

   Un `Down` que perdería datos se niega antes de cualquier DDL: `LOCK TABLE … IN ACCESS EXCLUSIVE MODE` y un `DO $$ … RAISE EXCEPTION …` si los hay, sin borrar ni transformar nada para pasar la guarda. En una tabla con RLS forzada, la guarda mira entre `NO FORCE ROW LEVEL SECURITY` y `FORCE ROW LEVEL SECURITY`, en la misma transacción: `mt_owner` corre la migración sin organización y no saltea la RLS, así que sin eso no vería ninguna fila (`InvitationWhatsAppConsent`). El SQL de toda migración va en una línea, sin `\r` (`MigrationSqlTests`). Su test va y vuelve: el Up, el Down sin datos y el Down que se niega con datos (`InvitationWhatsAppConsentMigrationTests`, `NotificationReadsMigrationTests`).
5. Revisá el SQL completo antes de commitear:

   ```text
   dotnet ef migrations script --idempotent --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api -- --environment Development --Logging:EventLog:LogLevel:Default None
   dotnet ef migrations has-pending-model-changes --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api -- --environment Development --Logging:EventLog:LogLevel:Default None
   ```

   El segundo comando debe indicar que el modelo coincide con la última migración. `MigrationsTests` comprueba lo mismo sin abrir conexión.
6. **Con `core.autocrlf`,** que Git para Windows trae prendido, el snapshot puede salir con `\r\n` escapado dentro de un CHECK. Volvelo a LF antes de commitear, y escribí cada CHECK en una sola línea: así no depende del fin de línea de quien genera la migración.
7. **Lo que EF no modela va en SQL dentro de la migración generada,** con un comentario en la migración y otro en la configuración de la entidad. Por ejemplo, una clave única o foránea diferida, para guardar en un solo paso dos filas nuevas que se apuntan entre sí (las de los catálogos de la E12). EF no la ve, así que la migración que quita el módulo la suelta a mano en su `Up` y la recrea, también diferida, en su `Down`, y la guía de quitar el módulo lo nombra ([modulos](../rules/modulos.md)).

## Convivencia de versiones

En cada despliegue, la migración corre antes que la imagen nueva, y la imagen vieja atiende contra el esquema nuevo hasta que la nueva pasa readiness. Al volver a la imagen anterior, la vieja lee lo que escribió la nueva. Lo que escribe la nueva lo tiene que poder leer y escribir la vieja. Se resuelve en dos despliegues: el primero deja que las dos versiones toleren el cambio, y el siguiente lo empieza a usar ([ADR 0060](../decisions/0060-dos-versiones-conviven.md)).

- **Un CHECK nuevo sobre una tabla que ya existía va en el despliegue siguiente.** Rechazaría lo que guarda la versión vieja, que no conoce la columna nueva. En la misma migración solo va un CHECK sobre una tabla nueva o que mencione únicamente columnas nuevas.
- **Una columna que pasa a aceptar nulos** se tolera primero: una versión que lee el nulo sin romper, y después la que lo escribe.
- **Un borrado no viaja con su reemplazo.** Se agrega lo nuevo en un despliegue y se borra lo viejo en el siguiente. Un renombre es siempre un agregado y un borrado. Para partir una migración que borra, se sacan de la generada el borrado del `Up` y su inverso del `Down`, con un comentario, y en el despliegue siguiente se escriben en otra generada vacía.
- **Un valor nuevo de un enum guardado como texto** se lee en un despliegue y se escribe en el siguiente, porque la versión vieja lanzaría al leerlo.
- **Un dato serializado que la app vuelve a abrir** (un JSON guardado, un comprobante sellado) ignora los miembros que no conoce.
- **Una excepción** se anota en la migración y en el control, con su motivo y con lo que pasa al volver a la imagen anterior.

Lo comprobará `MigrationCoexistenceTests` (E10) en cada migración posterior a la última aplicada: un CHECK nuevo sobre una tabla existente, una columna que pasa a aceptar nulos, un borrado de columna junto con un agregado en la misma tabla y un renombre. Hoy se revisa a mano, con esta lista.

## Aplicación y controles

En Development, `DatabaseBootstrapExtensions` prepara la base ICU `es-AR` y aplica las migraciones con `appdb-admin` antes del seed. En los demás ambientes, el despliegue aplica un migration bundle con el rol administrador antes de iniciar la Api. El runtime usa `mt_app` y nunca aplica migraciones.

La migración inicial crea los cinco esquemas, `unaccent`, `pg_trgm`, `public.f_unaccent`, las funciones de triggers, grants para `mt_app`, las once tablas de referencia, `platform.IdempotencyKeys` y `tenant.AuditEntries`. Las tablas `Widget`, `Poster` y `Deal` de aislamiento viven únicamente en el proyecto de tests: `IsolationSchema.ApplyAsync` las crea después de aplicar las migraciones reales.

No edites una migración ya aplicada. Si el modelo cambia, generá otra migración. Para revisar una migración nueva todavía no aplicada, corregí la fuente y regenerala antes de commitear.
