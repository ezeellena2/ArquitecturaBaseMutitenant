# Paginado, orden y búsqueda

**Regla:** todo listado pagina en el backend. La regla es **por páginas**, con 10 filas por defecto. Las tablas que solo crecen (auditoría, eventos, mensajes) paginan **por cursor**.

## Cómo se hace
- **Pedido:** `record ListXRequest : PagedRequest` con `SortableFields = ["name", "createdAtUtc"]`. `PagedRequest.AllowedPageSizes` es la colección inmutable única de tamaños: 10 (por defecto), 20, 50 o 100.
- **Validador y contrato:** `PagedRequestValidator<ListXRequest>` valida contra esa colección; OpenAPI publica el mismo enum numérico en `PagedRequest.pageSize` y en el parámetro `pageSize` de cada ruta paginada. Una lectura que trae menos filas por vez (las opciones de una pregunta, E14) le pasa al validador su tope, un subconjunto de esos tamaños, y el error lo dice («entre 1 y 50»).
- **Reader:**
  ```csharp
  private static readonly SortMap<Role> Sort = new() { ["name"] = r => r.Name, ["createdAtUtc"] = r => r.CreatedAtUtc };
  return await query.ApplyXFilters(request).ApplySearch(request.Search, r => r.Name)
      .ApplySort(SortDescriptor.Parse(request.Sort), Sort,
          new SortDescriptor("name", Descending: false), r => r.Id)
      .Select(r => new RoleRow(...))
      .ToPagedResultAsync(request, ct);
  ```
- **Respuesta:** `PagedResult<XRow>`. Una página fuera de rango devuelve `items` vacío con el `totalCount` real, sin error.
- **Filtros y conteos:** una sola función `ApplyXFilters`, que usan el listado y `GET …/filter-counts`.
- **Búsqueda:** siempre `ApplySearch`. No distingue acentos ni mayúsculas y escapa `%` y `_`. Cada columna buscable lleva un índice GIN trigram sobre `f_unaccent(lower(col))`, en una migración con SQL (`IX_<Tabla>_Search_<Columna>`).
  - Para buscar entre varios valores de una fila (todos los correos verificados de una persona), cada tabla filtra con `ApplySearch` y las partes se combinan con `Contains`: `MemberSearch` lo hace para Usuarios y Miembros de empresa. Una columna con conversor (un `Email`) se busca con `EF.Property<string>`.
  - **Lo que una lista muestra tapado no se busca:** a quién sumar a una empresa (`GET /api/companies/{companyId}/candidates/paged`) muestra el correo con `PersonalDataMask` y se busca solo por nombre; buscar por correo lo destaparía de a una letra.
  - **Única excepción, la auditoría de la organización:** `OrganizationAuditReader` busca con SQL en el texto de los cambios, porque `Changes` es jsonb y `ApplySearch` no lo pasa a texto. Usa la misma expresión (`public.f_unaccent(lower("Changes"::text))`, y la del tipo) y tiene sus índices (`IX_AuditEntries_Search_Changes` y `IX_AuditEntries_Search_EntityType`). A quien actuó lo busca por nombre con `ApplySearch`. Una búsqueda nueva en SQL entra en la lista de `SearchIndexTests` con su expresión e índice.
- **Índices:** cada campo de un `SortMap` tiene su índice: `(TenantId, campo, Id)` en una tabla privada y uno que termina en `(campo, Id)` en una global. El `SortMap` es un campo estático del lector; si ordena una proyección, `SortIndexTests` declara la columna de cada campo, o ninguna si sale de la cuenta global (nombre o correo), de una cuenta en SQL (el estado de una persona, las personas o los permisos de un rol, los miembros de una empresa, los integrantes de un equipo, los entregados de una difusión) o de una lista acotada a un padre que ordena sus pocas filas (los destinatarios de una difusión).
- **Orden de textos:** lo da la collation ICU `es-AR` de la base ([persistencia-ef](persistencia-ef.md)), sin `ToLower` ni `COLLATE` a mano.
- **Qué ordena** (decisión 264 de la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md)): toda columna con un dato que muestra la pantalla está en el `SortMap`; no entran las acciones, un ícono ni varios valores. Lo vacío va al final en los dos sentidos (en un orden descendente o con varias claves, `ApplySort` ordena primero por «es nulo»), y un estado o un tipo ordena por el orden de su circuito: como acá los enums se guardan como texto, con un `CASE` en su orden de declaración, nunca por su texto. Un campo puede ordenar por varias claves (el número de una respuesta: su prefijo y su número), y una columna dinámica suma su id (`sortFieldId`). Las tablas cuyo orden es un dato (las opciones de un catálogo) y las colas («Por aprobar») no ordenan por columnas. `ApplySort` lo hace solo: un campo que devuelve un enum ordena por su circuito, uno con un tipo anónimo (`row => new { row.Prefix, row.Number }`) por cada clave y queda vacío si le falta alguna, y con el mismo valor sigue el orden de siempre de la lista (`defaultSort`) y después el desempate. El nombre de cada campo es el `id` de su columna en el front.
- **Cursor:** `CursorRequest`, `CursorResult<T>` y `ToCursorResultAsync`. Orden fijo por instante UTC e Id, del más nuevo al más viejo; sin total. `CursorCodec` codifica ambos en base64url; un cursor inválido devuelve 400 `Validation.Failed` con `errors.after`.
- **Contrato HTTP:** `<X>Query` con `page`, `pageSize`, `sort`, `search` y los filtros, mapeado a mano.
- **Compatibilidad de arrays anteriores:** un recurso creciente incorpora una ruta paginada aditiva. Su adaptador anterior devuelve sólo la primera página de 10 desde SQL, lleva `BoundedLegacyCollection` con la sucesora y publica `deprecated`, `maxItems: 10` y `x-successor` en OpenAPI. No habilita una lista completa ni una excepción por área; las únicas listas completas son catálogos cerrados. Las opciones de auditoría paginan el conjunto de actores u organizaciones distintos, mientras el historial conserva su cursor sin total.

## Prohibido
- Devolver listas sin paginar.
- Ordenar sin desempate (lo agrega `ApplySort`).
- `Contains` o `ToLower` a mano para buscar.
- `Skip`/`Take` fuera de `ToPagedResultAsync`.
- Contar sobre una tabla de solo crecimiento.

## Copiá de
- `Infrastructure/Persistence/Readers/RoleReader.cs` y `Application/Models/Roles/ListRolesRequest.cs` (E4).
- `Infrastructure/Persistence/Readers/CompanyReader.cs` (E6): un listado con sus conteos por filtro.

## Lo verifica
- `HarnessTests` (E0): formato y referencias de la ficha.
- `SortIndexTests` (E2): cada campo de cada `SortMap` de los lectores tiene su índice en la base, y el de una tabla corre en PostgreSQL en los dos sentidos.
- `WhatsAppBroadcastTests` (E11, 264): cada tabla de WhatsApp ordena por cada columna en los dos sentidos, el consentimiento y el estado por su circuito, y entregados vacíos al final.
- `ListSortTests` (E12, 264): con datos reales, los roles por sus personas y los permisos que muestra su fila, las empresas por sus miembros activos y las organizaciones de Plataforma por el Dueño que muestra la fila y sus contadores, en los dos sentidos, lo vacío al final y el nombre como desempate.
- `SortMapTests` (E2, 264): lo vacío al final en los dos sentidos, el circuito de un estado, un campo con varias claves y el orden de siempre antes del desempate, en memoria y en el SQL que arma EF.
- `GrowingCollectionContractTests` (E0), correctivo C9: detecta arrays e interfaces de colección HTTP también dentro de respuestas anidadas, permite sólo catálogos cerrados o adaptadores legacy acotados con sucesora paginada y verifica los límites publicados; cada relación anidada permitida se declara por propiedad. `HomeResponse.Pending` es el adaptador vacío (`maxItems: 0`); los pendientes HTTP van en los grupos acotados.

## Detalle
[backend.md §9, "Paginado, orden y búsqueda"](../architecture/backend.md#paginado-orden-y-búsqueda)
