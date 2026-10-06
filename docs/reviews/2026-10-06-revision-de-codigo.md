# Revisión de código de las etapas 1 a 6

Fecha de inicio: **2026-10-06**. Guía: [revisión de código](../plans/2026-10-06-revision-de-codigo.md), sección «Para Codex».

## Cómo venimos

**Revisión en curso.** Roles está bien separado y se entiende: los controllers adaptan HTTP, el servicio dirige el caso de uso y el dominio conserva las reglas. La deuda encontrada hasta ahora está en cómo el front obtiene catálogos y conteos. Conviene corregir esas dos referencias antes de copiarlas a otra área.

Este informe evalúa arquitectura, diseño y lectura. No es una auditoría exhaustiva de seguridad ni una certificación de ejecución. No se modificó código ni se ejecutaron builds que regeneren contratos en la copia compartida.

## Alcance y avance

| Área | Estado | Base de lectura |
|---|---|---|
| Roles y permisos | Revisada | Back `96167f2`; front `9c1c782`; archivos del área sin cambios locales |
| Organización: Usuarios, Empresas, Configuración, Página pública y Auditoría | Pendiente | Se incorpora en el siguiente commit del informe |
| Plataforma | Pendiente | Se incorpora después de Organización |
| Piezas comunes | Pendiente | `shared/` y `scripts/` del front; Common y Persistence del back |
| Identidad, ingreso, registro y Mi cuenta | En espera | Se lee al final, una vez commiteados A1 a A7 |

Se excluyen migraciones, archivos generados, `docs/design/`, WhatsApp y la Etapa 7. Las ADR, las fichas y las decisiones de los planes se usan como criterio; no se denuncian como defectos por ser distintas de otra arquitectura. Los cambios locales del otro chat quedan fuera de nuestros commits.

## Hallazgos

### Catálogos y consultas

### R01 · Una página de 100 filas se usa como catálogo completo

- **Importancia:** Importante.
- **Área:** Roles; se verifican las otras apariciones al revisar Organización.
- **Dónde:** front `src/areas/business/roles/api/roles.ts:44`; consumidores en `pages/RolesPage.tsx:62` y `pages/RoleEditorPage.tsx:147`; `components/RoleScopeField.tsx:26` busca el nombre en ese mismo resultado.
- **Qué pasa:** `fetchCompanyOptions` pide solo `page=1&pageSize=100`, y la pantalla toma `items` como todas las empresas. Luego filtra las activas en el cliente. Desde la empresa 101 faltan opciones y nombres; con empresas inactivas entre las primeras 100 pueden faltar activas incluso antes. El límite del paginado queda convertido en una regla de producto implícita. El método se llama «opciones», pero su contrato no advierte que devuelve un catálogo incompleto.
- **Propuesta:** definir una consulta compartida de opciones, con búsqueda y paginado según el uso, y resolver por id la opción ya guardada. Para catálogos que deban cargarse enteros, recorrer las páginas explícitamente y devolver una colección completa. Llevar el filtro de estado al servidor. No resolverlo aumentando el número fijo.
- **Decisiones:** mantiene ADR 0036 y las reglas de paginado/estructura; no hay un máximo documentado de 100 empresas por organización. Es deuda que también aparecía en la revisión del 2026-10-03 y sigue presente.
- **Esfuerzo:** mediano.

### R02 · Los conteos de Roles multiplican los pedidos de listados

- **Importancia:** Conviene.
- **Área:** Roles y permisos.
- **Dónde:** front `src/areas/business/roles/pages/RolesPage.tsx:63`, `:70` y `:75`; back `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/RoleReader.cs:34`.
- **Qué pasa:** cada empresa activa genera un `fetchRoles` para leer solamente `totalCount`; otros tres listados calculan los alcances y, con búsqueda, otros tres el resumen global. Cada consulta también trae filas, permisos y personas asignadas que se descartan. El tráfico crece con el número de empresas y el componente mezcla la vista con la coordinación de todos esos pedidos. La caché reúne las claves idénticas sin búsqueda, pero no elimina el pedido por empresa.
- **Propuesta:** un endpoint de conteos de Roles que use el mismo filtro que el listado y devuelva en conjunto los conteos por alcance/empresa. Dejar la construcción de las consultas en un hook de la feature. La pantalla compone los resultados, como hacen las áreas con `filter-counts`.
- **Decisiones:** preserva los conteos del tablero; aplica backend.md §9 («Filtros y conteos con una sola función»). No cambia el paginado acordado.
- **Esfuerzo:** mediano.

## Lo que está bien

- **Roles y permisos:** `RoleService` es una referencia razonable para copiar: valida, abre una UoW, aplica reglas y traduce los choques de índices; invalida la caché después del commit. `RoleAssignment.Check` expresa dónde vale cada asignación y `RoleAssignmentGuard` permite reutilizarla. Los catálogos de permisos del dominio son intencionales. El front separa API, columnas, errores y lógica pura del selector; usa los hooks comunes de edición, concurrencia e idempotencia. No encontré una razón para reescribir esta área ni para crear más servicios intermediarios.

## Verificación del informe

Hallazgos contrastados con código, fichas y decisiones; referencias comprobadas por lectura. Los números de línea corresponden al estado leído y pueden desplazarse con los arreglos simultáneos. Cada commit de esta revisión incluye únicamente este archivo.
