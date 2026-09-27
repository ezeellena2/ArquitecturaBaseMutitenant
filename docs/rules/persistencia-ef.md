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
- **Migración:** el comando de [`docs/guides/migracion.md`](../guides/migracion.md) (E2). Si la tabla es de `tenant`, `EnableTenantRls` más los índices del `SortMap`. Se revisa el SQL generado antes de commitear.
- **Soft delete:** `ISoftDeletable` y `Remove()`. El interceptor lo convierte en una marca. Para ver lo borrado: `IgnoreQueryFilters(["SoftDelete"])`.

## Prohibido
- `DbContext` fuera de Infrastructure.
- `SaveChanges` en repositorios (ver [guardado](guardado.md)).
- Setear a mano campos de auditoría, de soft delete o `TenantId`.
- `ExecuteUpdate`/`ExecuteDelete` sobre entidades `IAuditable` o `ISoftDeletable`.
- Editar una migración ya aplicada.
- Eventos de dominio (ADR 0003).

## Copiá de
- `Infrastructure/Persistence/Configurations/Tenant/RoleConfiguration.cs`, `Repositories/RoleRepository.cs` y `Readers/RoleReader.cs` (E4)

## Lo verifica
- `EntityConfigurationTests`, `DataClassificationTests`, `DecimalPrecisionTests`.
- `MigrationsTests`: falla si falta una migración.
- `RlsPolicyInventoryTests`, `SortIndexTests`, `TransactionBoundaryTests`.

## Detalle
[backend.md §9](../architecture/backend.md#9-persistencia)
