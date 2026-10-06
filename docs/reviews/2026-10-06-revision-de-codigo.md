# Revisión de código de las etapas 1 a 6

Fecha de inicio: **2026-10-06**. Guía: [revisión de código](../plans/2026-10-06-revision-de-codigo.md), sección «Para Codex».

## Cómo venimos

**Revisión en curso.** Roles, Organización y Plataforma conservan una separación razonable: los controllers adaptan HTTP, los servicios dirigen los casos de uso y el dominio conserva las reglas. Lo que más pesa hasta ahora es usar catálogos incompletos en el front, validar siempre CUIT argentino aunque el back admite el tipo fiscal de cada país y concentrar varios flujos de edición en una misma pantalla. En Configuración de Plataforma, la protección de reintentos además exige conocer la caché de mutaciones desde el componente. El código se entiende, pero esas pantallas exigen seguir mucho estado para cambiar una sola acción.

Este informe evalúa arquitectura, diseño y lectura. No es una auditoría exhaustiva de seguridad ni una certificación de ejecución. No se modificó código ni se ejecutaron builds que regeneren contratos en la copia compartida.

## Alcance y avance

| Área | Estado | Base de lectura |
|---|---|---|
| Roles y permisos | Revisada | Back `96167f2`; front `9c1c782`; archivos del área sin cambios locales |
| Organización: Usuarios, Empresas, Configuración, Página pública y Auditoría | Revisada | Back y front del mismo estado base; incluye el Inicio empresarial |
| Plataforma | Revisada | Servicios, dominio, readers/controllers y features administrativas; archivos estables, posteriores al cierre de E5 |
| Piezas comunes | Pendiente | `shared/` y `scripts/` del front; Common y Persistence del back |
| Identidad, ingreso, registro y Mi cuenta | En espera | Se lee al final, una vez commiteados A1 a A7 |

Se excluyen migraciones, archivos generados, `docs/design/`, WhatsApp y la Etapa 7. Las ADR, las fichas y las decisiones de los planes se usan como criterio; no se denuncian como defectos por ser distintas de otra arquitectura. Los cambios locales del otro chat quedan fuera de nuestros commits.

## Hallazgos

### Catálogos y consultas

### R01 · Una página de 100 filas se usa como catálogo completo

- **Importancia:** Importante.
- **Área:** Roles, Usuarios y Empresas.
- **Dónde:** cinco consultas en tres features: front `src/areas/business/roles/api/roles.ts:44`, `src/areas/business/users/api/users.ts:78` y `:82`, `src/areas/business/companies/api/companies.ts:97` y `:106`. Roles consume ese catálogo en `pages/RolesPage.tsx:62` y `pages/RoleEditorPage.tsx:147`; `components/RoleScopeField.tsx:26` busca el nombre en el mismo resultado. Empresas usa esos roles también para su pestaña propia (`tabs/useCompanyRoles.ts:9`).
- **Qué pasa:** se pide solo `page=1&pageSize=100`, y la pantalla toma `items` como todas las empresas, roles o personas candidatas. Roles y Usuarios luego filtran empresas activas en el cliente. Desde la empresa 101 faltan opciones y nombres; con empresas inactivas entre las primeras 100 pueden faltar activas incluso antes. También dejan de ofrecerse candidatos y roles fuera de la primera página, y la pestaña Roles de una empresa queda incompleta. El límite del paginado queda convertido en una regla de producto implícita. Los métodos se llaman «opciones» o «asignables», pero su contrato no advierte que devuelven catálogos incompletos. Además, Roles y Usuarios repiten la consulta de empresas con claves de caché distintas.
- **Propuesta:** definir una consulta compartida de opciones, con búsqueda y paginado según el uso, y resolver por id la opción ya guardada. Para catálogos que deban cargarse enteros, recorrer las páginas explícitamente y devolver una colección completa. Llevar el filtro de estado al servidor. No resolverlo aumentando el número fijo.
- **Decisiones:** mantiene ADR 0036 y las reglas de paginado/estructura; no hay un máximo documentado de 100 empresas por organización. Es deuda que también aparecía en la revisión del 2026-10-03 y sigue presente.
- **Esfuerzo:** mediano.

### R02 · Los conteos por empresa multiplican los pedidos

- **Importancia:** Conviene.
- **Área:** Roles y Usuarios.
- **Dónde:** front `src/areas/business/roles/pages/RolesPage.tsx:63`, `:70` y `:75`; `src/areas/business/users/pages/UsersPage.tsx:78`; back `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/RoleReader.cs:34`.
- **Qué pasa:** en Roles, cada empresa activa genera un `fetchRoles` para leer solamente `totalCount`; otros tres listados calculan los alcances y, con búsqueda, otros tres el resumen global. Cada consulta también trae filas, permisos y personas asignadas que se descartan. Usuarios sí usa `filter-counts`, pero hace igualmente una llamada por empresa, más «Todas» y «Sin empresa». El tráfico crece con el número de empresas y los componentes mezclan la vista con la coordinación de todos esos pedidos. La caché reúne las claves idénticas sin búsqueda, pero no elimina el pedido por empresa.
- **Propuesta:** un endpoint de conteos de Roles que use el mismo filtro que el listado y devuelva en conjunto los conteos por alcance/empresa; ampliar la consulta de conteos de Usuarios para devolver sus opciones en conjunto. Dejar la construcción de las consultas en hooks de cada feature.
- **Decisiones:** preserva los conteos del tablero; aplica backend.md §9 («Filtros y conteos con una sola función»). No cambia el paginado acordado.
- **Esfuerzo:** mediano.

### Datos de referencia

### R03 · Empresas sigue fijando Argentina y once dígitos en el front

- **Importancia:** Importante.
- **Área:** Empresas; el registro empresarial se contrasta cuando se revise Identidad.
- **Dónde:** front `src/shared/referenceData/useCuitType.ts:7`, `src/areas/business/companies/components/CompanyDialog.tsx:32` y `:77`, `components/CompanyFields.tsx:33`; back `src/ArquitecturaBaseMultitenant.Application/Services/Companies/CompanyReferenceDataResolver.cs:22` y `:39`.
- **Qué pasa:** el front selecciona siempre `countryCode === "AR"` y `validatorKey === "ar-cuit-mod11"`, valida con ese tipo y recorta la entrada a once dígitos. El back elige el tipo para empresas del país de la organización y conserva el tipo guardado si el número no cambió. Por ejemplo, un EIN estadounidense válido de nueve dígitos se rechaza en el formulario como CUIT. Si cambia el país de la organización, la validación del front tampoco toma el tipo fiscal de la empresa ya guardada. No es solo un nombre poco general: son dos reglas distintas sobre el mismo dato.
- **Propuesta:** ofrecer el tipo efectivo del país/cultura de la organización desde el catálogo y conservar el tipo de la empresa al editar, con el mismo criterio que el back. Derivar la máscara/largo/validador del tipo, mantener solo el campo número y ocultarlo cuando no hay tipo empresarial. Cambiar también rótulo y ayuda según ese tipo.
- **Decisiones:** contradice D6 y decisión 10 de [consolidación](../plans/2026-10-03-consolidacion.md), que ya figuran pendientes para el front, y la ficha [identificación fiscal](../rules/identificacion-fiscal.md). Es ejecución pendiente de una decisión existente, no una propuesta de producto nueva.
- **Esfuerzo:** mediano.

### Composición y lectura

### R04 · Algunas fichas reúnen demasiados flujos en el componente de pantalla

- **Importancia:** Conviene.
- **Área:** Usuarios, Configuración y Página pública.
- **Dónde:** front `src/areas/business/users/pages/UserDetailPage.tsx:94` (guardado general, `saveCompanies` en `:204`, versiones de diálogos y `staleAfterDialog` en `:115`); `src/areas/business/settings/pages/SettingsPage.tsx:75`; `src/areas/business/public-page/pages/PublicPagePage.tsx:118` (publicación en `:154`, carga del logo en `:174`).
- **Qué pasa:** Usuario reúne estado/roles del borrador, cambios inmediatos de empresas, cuatro mutaciones, confirmaciones y recuperación tras un conflicto de diálogo. Configuración reúne la edición con todo el ciclo de dominios y TXT. Página pública reúne el formulario, disponibilidad del slug, lectura de archivos, publicación y vista previa. Las piezas comunes de edición están bien, pero queda mucho estado propio dentro del JSX de la ficha; para cambiar una acción hay que reconstruir qué versión y qué error corresponden a cada flujo.
- **Propuesta:** extraer por responsabilidad dentro de cada feature: un hook de edición de acceso y uno de acciones de empresas en Usuario; un bloque de dominios con su diálogo TXT en Configuración; un hook del editor público y piezas para logo/publicación. Mover conversiones puras a `lib/`. Copiar la composición de Empresas (`tabs/` y hooks propios), conservando las reglas de versión y los borradores. No agregar un motor genérico de formularios.
- **Decisiones:** respeta los guardados inmediatos del tablero y los hooks `useEditForm`/`useEditPage`; separa la implementación sin cambiar esos acuerdos.
- **Esfuerzo:** mediano.

### R05 · Auditoría calcula dos veces a quién y a qué empresa refiere cada cambio

- **Importancia:** Conviene.
- **Área:** Auditoría de la organización.
- **Dónde:** back `src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/OrganizationAuditReader.cs:166`; los mismos switches de miembro en `:176` y `:264`, y empresa en `:245` y `:275`; `ReadGuid` en `:373`.
- **Qué pasa:** `ResolveDisplayDataAsync` primero interpreta entidad/JSON para juntar ids y cargar nombres, y vuelve a interpretar las mismas referencias para armar cada fila. Cada llamada a `ReadGuid` parsea de nuevo todo el JSON. El método también contiene todas las alternativas para personas sin nombre, invitaciones, roles y empresas. Agregar una clase de cambio obliga a modificar dos mapas que deben coincidir; el recorrido de una entrada queda repartido entre recolección y presentación.
- **Propuesta:** interpretar una vez cada entrada en una referencia local tipada (miembro, rol, empresa, empresas invitadas), cargar los nombres por lotes y construir las filas desde esas mismas referencias. Separar en métodos locales la resolución de nombres y sus alternativas. Mantener SQL, RLS y la política de qué muestra la auditoría en las capas actuales.
- **Decisiones:** conserva auditoría.md y las decisiones de consolidación sobre «Qué», «Dónde», nombres y enmascarado; el problema es la implementación repetida, no esa conducta.
- **Esfuerzo:** mediano.

### R06 · Configuración de Plataforma administra desde la pantalla los callbacks internos del diálogo

- **Importancia:** Conviene.
- **Área:** Configuración de Plataforma y su diálogo común.
- **Dónde:** front `src/areas/platform/settings/pages/PlatformSettingsPage.tsx:182`; `protect` en `:188`, `bind` en `:200` y captura de eventos en `:226`; `src/shared/platform/ReasonDialog.tsx:75`.
- **Qué pasa:** el diálogo común conserva su mutación y sus callbacks, pero no ofrece un contrato para expresar cuándo dejó de valer una confirmación. La pantalla lo resuelve buscando la mutación por identidad de variables, guardando callbacks en un `WeakMap`, reemplazando sus opciones, observando `observerOptionsUpdated` y reaplicando la protección en una microtarea. También observa el DOM del textarea para detectar revisiones del motivo. Es infraestructura de ciclo de vida mezclada con un formulario de cinco campos. Para cambiar el guardado hay que entender cómo se actualizan las opciones de TanStack, la identidad del envío, el formulario y el router a la vez.
- **Propuesta:** acordar un contrato explícito y opcional de vigencia del envío en `ReasonDialog`, capturado al submit y conservado con sus callbacks originales. La pieza que crea la mutación debe proteger su ejecución y feedback sin que el caller encuentre y modifique esa mutación desde la caché. Separar después el hook de edición de Configuración de su JSX. Mantener RHF/foco, versión propia, descarte, guardas de borrador/motivo/permisos/navegación y Retry nativo; verificar la refactorización con los tests de vida existentes.
- **Decisiones:** la implementación actual cumple las decisiones 51 y 53 del [plan de pantallas de E5](../plans/2026-10-05-etapa-5-pantallas.md), que impidieron cambiar shared durante esa etapa. No se denuncia una falla de reintentos: se propone revisar ese límite de implementación para que la responsabilidad quede explícita y se entienda mejor. Las decisiones 44 (nombre de organización no único) y 46 (comparación de vigencia legal con el reloj del navegador) se conservaron y no se cuentan como hallazgos.
- **Esfuerzo:** mediano.

## Lo que está bien

- **Roles y permisos:** `RoleService` es una referencia razonable para copiar: valida, abre una UoW, aplica reglas y traduce los choques de índices; invalida la caché después del commit. `RoleAssignment.Check` expresa dónde vale cada asignación y `RoleAssignmentGuard` permite reutilizarla. Los catálogos de permisos del dominio son intencionales. El front separa API, columnas, errores y lógica pura del selector; usa los hooks comunes de edición, concurrencia e idempotencia. No encontré una razón para reescribir esta área ni para crear más servicios intermediarios.
- **Usuarios:** las escrituras vuelven al servicio de entrada y la emisión usa `InvitationIssuer`, sin duplicar tokens ni canales. Los helpers de asignaciones y protección de administradores tienen trabajo concreto. Estado y búsqueda se comparten entre filtros, filas y conteos; las expresiones SQL con especificación C# y test de paridad son una decisión explícita, no lógica de negocio puesta accidentalmente en un reader.
- **Empresas:** creación, edición y membresías tienen responsabilidades reconocibles; el back resuelve los datos fiscales desde referencias y usa `RoleAssignmentGuard`. La ficha del front separa pestañas, datos y diálogos, una buena referencia para R04.
- **Configuración:** el back distingue consulta DNS fuera de la transacción y cambio versionado dentro; nombre y preferencias se guardan juntos y la caché se limpia después del commit. El front reutiliza los selectores de referencia.
- **Página pública:** contenido, logo y publicación tienen reglas en el dominio; el servicio/editor mantiene el guardado y la traducción de concurrencia. La vista previa reutiliza `PublicPageCard` y los formatos/telefonía comunes. No corresponde adelantar el storefront de E7.
- **Auditoría e Inicio:** feed por cursor, traducción en Application y nombres cargados por lotes, sin una consulta por fila. `HomeService` recibe pendientes por `IPendingItemSource`; los helpers no abren transacciones propias. Los tamaños y colores de estas pantallas remiten al tablero y a tokens del tema: no se cuentan como números mágicos por el solo hecho de estar en clases de CSS.
- **Plataforma:** las acciones globales y las que entran a una organización están diferenciadas. `PlatformActionGuard` tiene una responsabilidad concreta: autorización y SecurityEvent confirmado antes del scope y de la UoW; no sobra como simple pasamanos. Los writers y guards mantienen el caso de uso legible sin abrir transacciones propias. Organización reutiliza las verificaciones de dominios; operadores tienen permisos individuales y protección del último gestor; Legales publica versiones inmutables con culturas del catálogo y número bajo lock. Listados y conteos comparten filtros y las fichas usan evidencia persistida para fechas/estados. Las pestañas del front están separadas y las versiones propias de cada recurso no se reemplazan con un GET de fondo. No encontré motivos para cambiar las capas ni agregar CQRS.

## Verificación del informe

Hallazgos contrastados con código, fichas y decisiones; referencias comprobadas por lectura. Los números de línea corresponden al estado leído y pueden desplazarse con los arreglos simultáneos. Cada commit de esta revisión incluye únicamente este archivo.
