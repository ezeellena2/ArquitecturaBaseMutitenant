# Persistencia: entidades, configuraciones, migraciones, repositorios y readers

**Regla:** EF Core vive solo en Infrastructure. Se escribe con repositorios (`I<X>Repository`) y se lee con readers (`I<X>Reader`), sin repositorio genérico. Cada entidad tiene su configuración.

## Cómo se hace
- Las configuraciones aportadas por un módulo pueden declarar una FK a `ApplicationUser` únicamente si implementan `IEntityTypeConfiguration<T>`, pertenecen al namespace declarado por su contribuidor y `T` figura explícitamente como entidad global `identity`. Esto no habilita repositorios, readers ni servicios a acceder a Identity; lo comprueba `IdentityAccessTests` (E2).
- **Entidad:**
  - hereda de `Entity` (Id Guid v7);
  - tiene constructor privado para EF, fábrica estática que valida con `ArgumentException` (un bug) y métodos que devuelven `Result` (reglas);
  - sus propiedades son `private set`.
- **Excepción de referencias globales:** las once entidades de `Domain/ReferenceData` usan las claves naturales `Code`/`Id` y las claves compuestas de traducciones y `TimeZoneCountries`, como fija [`datos-de-referencia.md` §2](../architecture/datos-de-referencia.md#2-las-tablas-esquema-platform-globales-sin-rls). No heredan de `Entity` ni reciben Guid artificial; viven en `platform` sin RLS.
- **Configuración:**
  - `internal sealed class XConfiguration : IEntityTypeConfiguration<X>` en `Configurations/<Esquema>/`;
  - enums como texto (`HasConversion<string>().HasMaxLength(n)`) y `decimal` con `HasPrecision`;
  - esquema según la clase de dato.
  - un módulo aporta sus configuraciones mediante `Infrastructure.Persistence.IPersistenceModelContributor`, con su namespace de configuración y un diccionario **explícito** tipo → esquema global. No se exime un namespace completo de clasificación. El núcleo aplica sus configuraciones y cada contribuidor aplica exclusivamente las suyas, exactamente una vez.
  - runtime registra el contribuidor desde el módulo; DesignTime, bootstrap y fábricas sin DI lo descubren neutralmente con `PersistenceModelContributors.Discover()`, sin opciones, secretos ni red. `new ApplicationDbContext(..., modelContributors: [])` construye el núcleo sin módulos; la clave de caché EF distingue ambos conjuntos. Apagar un canal no quita tablas.
- **Nombres:**
  - en los repositorios, `Get…` devuelve la entidad seguida por EF o `null`, y además hay `Add` y `Remove`;
  - en los readers, `Find…` devuelve una proyección, `List…` una colección o página, y `Exists…`/`Count…` escalares.
- **Readers:** `AsNoTracking` y proyección directa a `*Row` o `*Response`.
- **Migración:** el comando de [`docs/guides/migracion.md`](../guides/migracion.md) (E2). Cada tabla nueva llama al helper RLS de su clase: `tenant` → `EnableTenantRls`, `public_site` → `EnablePublicRls`, `engagement` → `EnablePartiesRls` ([multitenancy](multitenancy.md)). Si la tabla es de `tenant`, además lleva un índice `(TenantId, campo, Id)` por cada campo de su `SortMap` ([paginado-y-busqueda](paginado-y-busqueda.md)). Se revisa el SQL generado antes de commitear.
- **Orden alfabético en español (collation):** la base se crea con collation **ICU `es-AR`**: `CREATE DATABASE … LOCALE_PROVIDER icu ICU_LOCALE 'es-AR' TEMPLATE template0`. Así "Álvarez" queda antes que "Zapata" y "Ñandú" después de "Nuñez". En Development la crea `DatabaseBootstrapExtensions`; en producción, el DBA con el mismo comando. `RuntimeRoleValidator` **aborta** el arranque si la base no es ICU `es-AR`.
- **Versión:** una entidad que se edita desde una pantalla implementa `IVersioned` ([concurrencia](concurrencia.md)).
- **Índice único que traduce un caso de uso:** el control previo del servicio («¿ya existe?») no alcanza ante dos pedidos simultáneos; la barrera es el índice. Su nombre va en `UniqueKeys` (`Application/Interfaces/Persistence`), la configuración lo fija con `HasDatabaseName(UniqueKeys.X)` y el servicio lo compara en `catch (UniqueConstraintViolationException e) when (e.ConstraintName == UniqueKeys.X)`, alrededor de la transacción. Un dato repetido devuelve su `Error` (`RoleErrors.AlreadyExists`, `TenantSlugErrors.Taken`); dos primeros guardados del mismo registro son una edición simultánea y lanzan `ConcurrencyConflictException` (`PlatformFeatureService`, `PublicPageService`). Nunca un nombre de índice escrito a mano en Application.
- **Clave foránea que traduce un caso de uso:** igual que el índice único. Si un pedido guarda una fila que apunta a otra mientras otro pedido la borra, la base rechaza al que guarda segundo: 23503 al guardar la fila que apunta, 23001 al borrar con `ON DELETE RESTRICT`. `UnitOfWork` traduce los dos a `ForeignKeyViolationException`; el nombre va en `ForeignKeys` (`Application/Interfaces/Persistence`), la configuración lo fija con `HasConstraintName(ForeignKeys.X)` y el servicio lo compara en `catch (ForeignKeyViolationException e) when (e.ConstraintName == ForeignKeys.X)` y devuelve el `Error` de cada lado: asignar un rol borrado es `RoleErrors.NotFound`; borrar un rol recién asignado, `RoleErrors.HasUsers`.
- **Largos:** `HasMaxLength(TextLimits.X)`, nunca un número suelto ([textos-libres](textos-libres.md)). Los correos, CUIT y teléfonos son value objects ([emails](emails.md), [identificacion-fiscal](identificacion-fiscal.md), [telefonos](telefonos.md)).
- **Soft delete:** `ISoftDeletable` y `Remove()`. El interceptor lo convierte en una marca. Para ver lo borrado: `IgnoreQueryFilters(["SoftDelete"])`.

## Prohibido
- `DbContext` fuera de Infrastructure.
- `SaveChanges` en repositorios (ver [guardado](guardado.md)).
- Setear a mano o cambiar campos de auditoría, de soft delete o las columnas de tenant (`TenantId`, `BusinessTenantId`, `ConsumerTenantId`): los sella el interceptor ([multitenancy](multitenancy.md)).
- `ExecuteUpdate`/`ExecuteDelete` sobre entidades `IAuditable` o `ISoftDeletable`.
- Editar una migración ya aplicada.
- Eventos de dominio (ADR 0003).

## Copiá de
- `Infrastructure/Persistence/Configurations/Tenant/RoleConfiguration.cs`, `Repositories/RoleRepository.cs` y `Readers/RoleReader.cs` (E4)

## Lo verifica
- `EntityConfigurationTests` (E2), `DataClassificationTests` (E2), `DecimalPrecisionTests` (E1), `TextLimitsTests` (E1).
- `WhatsAppPersistenceTests` (E8): modelos runtime/DesignTime/bootstrap iguales, modelo sin módulos, persistencia tras reinicio y declaraciones globales faltantes/duplicadas/con esquema incorrecto. `EntityConfigurationTests` incluye las configuraciones aportadas y exige exactamente una por entidad.
- `CollationTests` (E2): orden real de "Álvarez", "Ñandú", "Nuñez" y "Zapata" contra la base de los tests (Testcontainers creada con ICU `es-AR`).
- `MigrationsTests` (E2): falla si falta una migración.
- `RlsPolicyInventoryTests` (E2), `SortIndexTests` (E2), `TransactionBoundaryTests` (E2).
- `DbContextModelTests` (E6): cada clave de `UniqueKeys` es un índice único del modelo, y cada una de `ForeignKeys`, una clave foránea. `UniqueKeysTests` (E6): Application no escribe nombres de índice ni de clave foránea. `UniqueKeyRaceTests` (E6): dos pedidos que pasan juntos el control previo, y uno guarda mientras el otro recibe 409. `ForeignKeyRaceTests` (E6): asignar un rol mientras otro lo borra, en los dos órdenes.

## Detalle
[backend.md §9](../architecture/backend.md#9-persistencia)
