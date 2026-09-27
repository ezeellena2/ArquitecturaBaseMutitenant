# Paginado, orden y búsqueda

**Regla:** todo listado pagina en el backend. La regla es **por páginas**, con 10 filas por defecto. Las tablas que solo crecen (auditoría, eventos, mensajes) paginan **por cursor**.

## Cómo se hace
- **Pedido:** `record ListXRequest : PagedRequest` con `SortableFields = ["name", "createdAtUtc"]`. `pageSize` admite 10 (por defecto), 20, 50 o 100.
- **Validador:** `PagedRequestValidator<ListXRequest>`.
- **Reader:**
  ```csharp
  private static readonly SortMap<Role> Sort = new() { ["name"] = r => r.Name, ["createdAtUtc"] = r => r.CreatedAtUtc };
  return await query.ApplyXFilters(request).ApplySearch(request.Search, r => r.Name)
      .ApplySort(request.Sort, Sort, defaultSort: "name").Select(r => new RoleRow(...))
      .ToPagedResultAsync(request, ct);
  ```
- **Respuesta:** `PagedResult<XRow>`. Una página fuera de rango devuelve `items` vacío con el `totalCount` real, sin error.
- **Filtros y conteos:** una sola función `ApplyXFilters`, que usan el listado y `GET …/filter-counts`.
- **Búsqueda:** siempre `ApplySearch`. No distingue acentos ni mayúsculas y escapa `%` y `_`. Cada columna buscable lleva un índice GIN trigram sobre `f_unaccent(lower(col))`.
- **Índices:** cada campo del `SortMap` tiene `(TenantId, campo, Id)`.
- **Orden de textos:** lo da la collation ICU `es-AR` de la base ([persistencia-ef](persistencia-ef.md)), sin `ToLower` ni `COLLATE` a mano.
- **Cursor:** `CursorRequest`, `CursorResult<T>` y `ToCursorResultAsync`. Orden fijo, del más nuevo al más viejo; sin total.
- **Contrato HTTP:** `XQuery` con `page`, `pageSize`, `sort`, `search` y los filtros, mapeado a mano.

## Prohibido
- Devolver listas sin paginar.
- Ordenar sin desempate (lo agrega `ApplySort`).
- `Contains` o `ToLower` a mano para buscar.
- `Skip`/`Take` fuera de `ToPagedResultAsync`.
- Contar sobre una tabla de solo crecimiento.

## Copiá de
- `Infrastructure/Persistence/Readers/RoleReader.cs` y `Application/Models/Roles/ListRolesRequest.cs` (E4)

## Lo verifica
- `PaginationTests`, `CursorPaginationTests`, `SearchTests`.
- `SortIndexTests`: índice por cada campo de un `SortMap`.
- `PagedRequestValidatorTests`.

## Detalle
[backend.md §9, "Paginado, orden y búsqueda"](../architecture/backend.md#paginado-orden-y-búsqueda)
