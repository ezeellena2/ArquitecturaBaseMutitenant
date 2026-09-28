# Persistencia: entidades, configuraciones, migraciones, repositorios y readers

**Regla:** EF Core vive solo en Infrastructure. Se escribe con repositorios (`I<X>Repository`) y se lee con readers (`I<X>Reader`), sin repositorio genérico. Cada entidad tiene su configuración.

## Cómo se hace
- **Entidad:**
  - hereda de `Entity` (Id Guid v7);
  - tiene constructor privado para EF, fábrica estática que valida con `ArgumentException` (un bug) y métodos que devuelven `Result` (reglas);
  - sus propiedades son `private set`.
- **Configuración:**
  - `internal sealed class XConfiguration : IEntityTypeConfiguration<X>` en `Configurations/<Esquema>/`;
  - enums como texto (`HasConversion<string>().HasMaxLength(n)`) y `decimal` con `HasPrecision`;
  - esquema según la clase de dato.
- **Nombres:**
  - en los repositorios, `Get…` devuelve la entidad seguida por EF o `null`, y además hay `Add` y `Remove`;
  - en los readers, `Find…` devuelve una proyección, `List…` una colección o página, y `Exists…`/`Count…` escalares.
- **Readers:** `AsNoTracking` y proyección directa a `*Row` o `*Response`.
- **Migración:** el comando de [`docs/guides/migracion.md`](../guides/migracion.md) (E2). Cada tabla nueva llama al helper RLS de su clase: `tenant` → `EnableTenantRls`, `public_site` → `EnablePublicRls`, `engagement` → `EnablePartiesRls` ([multitenancy](multitenancy.md)). Si la tabla es de `tenant`, además lleva un índice `(TenantId, campo, Id)` por cada campo de su `SortMap` ([paginado-y-busqueda](paginado-y-busqueda.md)). Se revisa el SQL generado antes de commitear.
- **Orden alfabético en español (collation):** la base se crea con collation **ICU `es-AR`**: `CREATE DATABASE … LOCALE_PROVIDER icu ICU_LOCALE 'es-AR' TEMPLATE template0`. Así "Álvarez" queda antes que "Zapata" y "Ñandú" después de "Nuñez". En Development la crea `DatabaseBootstrapExtensions`; en producción, el DBA con el mismo comando. `RuntimeRoleValidator` **aborta** el arranque si la base no es ICU `es-AR`.
- **Versión:** una entidad que se edita desde una pantalla implementa `IVersioned` ([concurrencia](concurrencia.md)).
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
- `EntityConfigurationTests`, `DataClassificationTests`, `DecimalPrecisionTests`, `TextLimitsTests`.
- `CollationTests`: orden real de "Álvarez", "Ñandú", "Nuñez" y "Zapata" contra la base de los tests (Testcontainers creada con ICU `es-AR`).
- `MigrationsTests`: falla si falta una migración.
- `RlsPolicyInventoryTests`, `SortIndexTests`, `TransactionBoundaryTests`.

## Detalle
[backend.md §9](../architecture/backend.md#9-persistencia)
