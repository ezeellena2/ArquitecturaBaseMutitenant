# Revisión de código de las etapas 1 a 6

Fecha de inicio: **2026-10-06**. Cierre: **2026-10-07**. Guía: [revisión de código](../plans/2026-10-06-revision-de-codigo.md), sección «Para Codex».

## Cómo venimos

**Revisión terminada: diez hallazgos, dos Importantes y ocho Conviene.** Las capas del back están bien encaminadas: los controllers adaptan HTTP, los servicios dirigen los casos de uso y las reglas tienen lugares reconocibles. Roles sigue siendo una buena referencia. Identidad también tiene servicios y helpers con trabajo concreto; los arreglos A1–A7 están incorporados. No hay motivos para reescribir la arquitectura.

Lo que más pesa es usar catálogos incompletos en el front (R01), validar las empresas siempre como CUIT argentino aunque el back usa el país y el tipo guardado (R03), concentrar varios flujos en una misma pantalla (R04), proteger reintentos manipulando la caché interna de mutaciones desde Configuración de Plataforma (R06) y mantener dos reglas distintas para las acciones de los métodos de ingreso (R09). El código se entiende en general; esas pantallas y reglas repetidas son las partes que más cuesta seguir y conviene ordenar primero.

Este informe evalúa arquitectura, diseño y lectura. No es una auditoría exhaustiva de seguridad ni una certificación de ejecución. No se modificó código ni se ejecutaron builds que regeneren contratos en la copia compartida.

## Alcance y avance

| Área | Estado | Base de lectura |
|---|---|---|
| Roles y permisos | Revisada | Back `96167f2`; front `9c1c782`; archivos del área sin cambios locales |
| Organización: Usuarios, Empresas, Configuración, Página pública y Auditoría | Revisada | Back y front del mismo estado base; incluye el Inicio empresarial |
| Plataforma | Revisada | Servicios, dominio, readers/controllers y features administrativas; archivos estables, posteriores al cierre de E5 |
| Piezas comunes | Revisadas | `shared/` y scripts del front; Common y Persistence del back. Mi cuenta y el alcance/cierre de los recorridos de identidad del runner se contrastaron al final |
| Identidad, ingreso, registro y Mi cuenta | Revisada al final | Back `6ed4608`; front `fec2a86`, después de verificar los commits de A1–A7. Incluye invitaciones, recuperación y baja |

Se excluyen migraciones, archivos generados, `docs/design/`, WhatsApp y la Etapa 7. Las ADR, las fichas y las decisiones de los planes se usan como criterio; no se denuncian como defectos por ser distintas de otra arquitectura. Los cambios del otro chat quedaron fuera de nuestros commits. Se respetó el orden de la guía y cada área se cerró con un commit exclusivo de este informe.

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
- **Área:** Empresas; dependencia compartida con Registro-Empresa.
- **Dónde:** front `src/shared/referenceData/useCuitType.ts:7`, `src/areas/business/companies/components/CompanyDialog.tsx:32` y `:77`, `components/CompanyFields.tsx:33`; back `src/ArquitecturaBaseMultitenant.Application/Services/Companies/CompanyReferenceDataResolver.cs:22` y `:39`. El registro usa el mismo helper en `src/areas/public/auth/pages/BusinessSignupPage.tsx:88`, manda su tipo en `:128` y recorta once dígitos en `:320`.
- **Qué pasa:** el front selecciona siempre `countryCode === "AR"` y `validatorKey === "ar-cuit-mod11"`, valida con ese tipo y recorta la entrada a once dígitos. El back elige el tipo para empresas del país de la organización y conserva el tipo guardado si el número no cambió. Por ejemplo, un EIN estadounidense válido de nueve dígitos se rechaza en el formulario como CUIT. Si cambia el país de la organización, la validación del front tampoco toma el tipo fiscal de la empresa ya guardada. No es solo un nombre poco general: son dos reglas distintas sobre el mismo dato.
- **Propuesta:** ofrecer el tipo efectivo del país/cultura de la organización desde el catálogo y conservar el tipo de la empresa al editar, con el mismo criterio que el back. Derivar la máscara/largo/validador del tipo, mantener solo el campo número y ocultarlo cuando no hay tipo empresarial. Cambiar también rótulo y ayuda según ese tipo.
- **Decisiones:** en Empresas contradice D6 y decisión 10 de [consolidación](../plans/2026-10-03-consolidacion.md), que ya figuran pendientes para el front, y la ficha [identificación fiscal](../rules/identificacion-fiscal.md). Es ejecución pendiente de una decisión existente. Registro-Empresa ofrece CUIT opcional de once números por acuerdo explícito en [frontend.md, Auth](../../../ArquitecturaBaseMutitenantFront/docs/architecture/frontend.md#auth) y [Identidad](../features/identidad.md); no se cuenta ese acuerdo como otro defecto. Al refactorizar el helper para Empresas hay que conservar ese alcance o revisar expresamente el acuerdo de Registro, sin cambiarlo de manera implícita.
- **Esfuerzo:** mediano.

### Composición y lectura

### R04 · Algunas pantallas reúnen demasiados flujos en el componente

- **Importancia:** Conviene.
- **Área:** Usuarios, Configuración, Página pública y Registro-Empresa.
- **Dónde:** front `src/areas/business/users/pages/UserDetailPage.tsx:94` (guardado general, `saveCompanies` en `:204`, versiones de diálogos y `staleAfterDialog` en `:115`); `src/areas/business/settings/pages/SettingsPage.tsx:75`; `src/areas/business/public-page/pages/PublicPagePage.tsx:118` (publicación en `:154`, carga del logo en `:174`); `src/areas/public/auth/pages/BusinessSignupPage.tsx:75` (dos formularios y tres mutaciones en `:90`–`:128`, creación en `:223`, render de pasos en `:256`).
- **Qué pasa:** Usuario reúne estado/roles del borrador, cambios inmediatos de empresas, cuatro mutaciones, confirmaciones y recuperación tras un conflicto de diálogo. Configuración reúne la edición con todo el ciclo de dominios y TXT. Página pública reúne el formulario, disponibilidad del slug, lectura de archivos, publicación y vista previa. Registro-Empresa reúne correo/código, aceptación legal, retorno de Google, disponibilidad del slug, creación y siete estados, terminando en un condicional de varios niveles. Las piezas comunes están bien, pero queda mucho estado propio en la pantalla; para cambiar una acción hay que reconstruir qué versión, paso y error corresponden a cada flujo.
- **Propuesta:** extraer por responsabilidad dentro de cada feature: un hook de edición de acceso y uno de acciones de empresas en Usuario; un bloque de dominios con su diálogo TXT en Configuración; un hook del editor público y piezas para logo/publicación. En Registro-Empresa, un hook del recorrido y componentes por paso, con transiciones explícitas y reutilizando `AuthCodeStep`/`AuthStatus`. Mover conversiones puras a `lib/`. Copiar la composición de Empresas (`tabs/` y hooks propios), conservando las reglas de versión y los borradores. No agregar un motor genérico de formularios ni de recorridos.
- **Decisiones:** respeta los guardados inmediatos del tablero, los hooks `useEditForm`/`useEditPage`, la aceptación legal y la creación idempotente de Registro; separa la implementación sin cambiar esos acuerdos.
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

### Fechas civiles

### R07 · «Ayer» se calcula como 24 horas atrás

- **Importancia:** Conviene.
- **Área:** Formato común; tarjeta de Auditoría en el Inicio.
- **Dónde:** front `src/shared/format/formatters.ts:174`, resta fija en `:178`.
- **Qué pasa:** `formatCalendar` compara el día local del dato con el de `now - 24 horas`. Ayer es el día civil anterior y no siempre está a 24 horas: con horario de verano puede tener 23 o 25. Comprobación de lectura con `Intl`: en `America/New_York`, `2026-03-09T04:30:00Z` es el 9 de marzo a las 00:30; restar 24 horas da el 7 a las 23:30. Un cambio del 8, que fue ayer, se muestra como fecha completa. El helper ya distingue instantes y fechas civiles, pero esta rama vuelve a mezclarlos.
- **Propuesta:** obtener año/mes/día de hoy en la zona efectiva y restar un día de calendario, comparando esos componentes con los del dato. Mantener la resta de tiempo en `formatRelative`, donde sí se expresa tiempo transcurrido. Cubrir la rama con los cambios de horario de 23 y 25 horas en los tests existentes del formateador.
- **Decisiones:** preserva [formatos.md](../../../ArquitecturaBaseMutitenantFront/docs/architecture/formatos.md), que define «hoy/ayer» en la zona de la persona, y [fechas y zonas](../rules/fechas-y-zonas.md). No cambia la presentación del tablero.
- **Esfuerzo:** chico.

### Scripts y reutilización

### R08 · El motor de capturas quedó repartido entre entradas de distintas etapas

- **Importancia:** Conviene.
- **Área:** Scripts del front.
- **Dónde:** `scripts/capturar-etapa-3a.mjs:216`, `scripts/capturar-etapa-5.mjs:636`, `scripts/capturar-relevamiento.mjs:83` y `scripts/capturar-f7.mjs:124` (capturador de consolidación, no Etapa 7); `scripts/recorrido-lienzo/vista.mjs:1` importa utilidades desde E3a.
- **Qué pasa:** `capturar-etapa-3a` funciona a la vez como comando de esa etapa y como biblioteca general de servidor, mocks, acciones, fuentes, máscaras y rutas. A la vez, abrir y preparar un tablero vuelve a implementarse en E5, relevamiento y consolidación, con diferencias de espera, acciones y máscaras. Al corregir la preparación visual hay que decidir qué copia corresponde y qué consumidor hereda el cambio. Los grandes catálogos de estados no son el problema: son datos de prueba necesarios.
- **Propuesta:** sacar servidor, acciones, preparación de tablero/app y composición a un módulo común de capturas, con opciones explícitas para las diferencias reales de readiness y máscara. Dejar cada entrada con sus casos/fixtures y CLI; separar también los fixtures reutilizados de E6 de su comando. Conservar los manifiestos, estados aprobados y tests del pipeline; no crear otro lenguaje de escenarios.
- **Decisiones:** mantiene la reutilización de casos/respuestas acordada en consolidación y E5, la fuente única del lienzo y los capturadores reales. Cambia la ubicación de responsabilidades, no las referencias visuales ni el modo de probarlas.
- **Esfuerzo:** mediano.

### Identidad: reglas y configuración

### R09 · Las capacidades de Mi cuenta conservan la regla del respaldo por código

- **Importancia:** Conviene.
- **Área:** Identidad y Mi cuenta.
- **Dónde:** back `src/ArquitecturaBaseMultitenant.Application/Services/Identity/AccountLoginMethodsService.cs:32`, `:36` y `:42`; `LoginMethodAvailabilityPolicy.cs:14`, `:42` y `:63`; `LoginMethodEditor.cs:21`; `src/ArquitecturaBaseMultitenant.Application/Models/Identity/AccountLoginMethodsResponse.cs:17`.
- **Qué pasa:** el listado elige otro método que pueda recibir un código, calcula su destino enmascarado y exige ese respaldo para `CanRemove`/`CanMakePrimary`. La escritura ya usa la sesión iniciada: quitar verifica `CheckRemoval` y elegir principal verifica que el método esté disponible. El front consume esas capacidades, pero ya no consume `BackupDestination`. También quedaron sin callers `WasVerifiedBeforeSession` y la sobrecarga de `CanReceiveCode(LoginMethod)`. Para entender si una acción está permitida hay que reconciliar la regla del listado con la del editor y distinguir restos de reautenticación de la protección vigente del último método propio/activo. Por ejemplo, Google disponible sin canal de correo es un método válido para el editor, pero no un respaldo capaz de recibir código para el listado.
- **Propuesta:** derivar las capacidades de la misma política de disponibilidad y eliminación que aplica el editor, usando una decisión común sobre los datos mínimos del método. Retirar el destino y los cálculos exclusivos de la confirmación por código, junto con los helpers sin uso. Conservar `HasPersonalBackupAsync`, las guardas del último método, los avisos y la confirmación del diálogo. Verificar que capacidades y escritura expresen la misma regla en los tests existentes de métodos.
- **Decisiones:** aplica [ADR 0045](../decisions/0045-mi-cuenta-sin-codigo-de-confirmacion.md), ya implementada por A5; no propone restaurar códigos ni modificar la protección del último método. Es limpieza incompleta y duplicación de la política después de ese cambio.
- **Esfuerzo:** mediano.

### R10 · El primer código de ingreso ignora la espera que devuelve el servidor

- **Importancia:** Conviene.
- **Área:** Ingreso por correo, en las dos puertas.
- **Dónde:** front `src/areas/public/auth/components/EmailCodeForm.tsx:47`, `src/areas/public/auth/pages/LoginPage.tsx:129` y `:171`, `src/areas/public/auth/lib/loginCodeState.ts:4` y `:12`; back `src/ArquitecturaBaseMultitenant.Application/Configuration/Auth/LoginCodeOptions.cs:20` y `src/ArquitecturaBaseMultitenant.Application/Services/Auth/LoginCodeRequester.cs:63`.
- **Qué pasa:** el primer pedido descarta `resendAfterSeconds` y la pantalla arranca siempre 60 segundos. El reenvío sí usa el valor recibido; Registro y Recuperación también lo hacen desde el primer pedido. El back permite configurar la espera entre 0 y 3600 segundos. Con un valor distinto de 60, el primer paso puede esperar de más u ofrecer reenviar antes de lo permitido. Además, `CodeStep` transporta `sentAtMs`, que la pantalla no usa, y `secondsUntilResend` fija otros 60 segundos en un helper consumido solo por su test. Hay tres representaciones de una misma espera para seguir al leer el recorrido.
- **Propuesta:** transportar la espera de la respuesta en el estado del paso y usarla al iniciar el mismo countdown del reenvío. Retirar `sentAtMs` y el helper si no tienen otro uso. Añadir a los tests de ingreso un valor de respuesta distinto del default para comprobar el contrato, conservando aparte la espera de un error 429.
- **Decisiones:** [frontend.md, Auth](../../../ArquitecturaBaseMutitenantFront/docs/architecture/frontend.md#auth) acuerda 60 segundos como espera del ingreso. No se propone cambiar ese default: se propone que su fuente sea el contrato del back, como en los demás recorridos, y que la configuración existente no produzca dos esperas distintas.
- **Esfuerzo:** chico.

## Lo que está bien

- **Roles y permisos:** `RoleService` es una referencia razonable para copiar: valida, abre una UoW, aplica reglas y traduce los choques de índices; invalida la caché después del commit. `RoleAssignment.Check` expresa dónde vale cada asignación y `RoleAssignmentGuard` permite reutilizarla. Los catálogos de permisos del dominio son intencionales. El front separa API, columnas, errores y lógica pura del selector; usa los hooks comunes de edición, concurrencia e idempotencia. No encontré una razón para reescribir esta área ni para crear más servicios intermediarios.
- **Usuarios:** las escrituras vuelven al servicio de entrada y la emisión usa `InvitationIssuer`, sin duplicar tokens ni canales. Los helpers de asignaciones y protección de administradores tienen trabajo concreto. Estado y búsqueda se comparten entre filtros, filas y conteos; las expresiones SQL con especificación C# y test de paridad son una decisión explícita, no lógica de negocio puesta accidentalmente en un reader.
- **Empresas:** creación, edición y membresías tienen responsabilidades reconocibles; el back resuelve los datos fiscales desde referencias y usa `RoleAssignmentGuard`. La ficha del front separa pestañas, datos y diálogos, una buena referencia para R04.
- **Configuración:** el back distingue consulta DNS fuera de la transacción y cambio versionado dentro; nombre y preferencias se guardan juntos y la caché se limpia después del commit. El front reutiliza los selectores de referencia.
- **Página pública:** contenido, logo y publicación tienen reglas en el dominio; el servicio/editor mantiene el guardado y la traducción de concurrencia. La vista previa reutiliza `PublicPageCard` y los formatos/telefonía comunes. No corresponde adelantar el storefront de E7.
- **Auditoría e Inicio:** feed por cursor, traducción en Application y nombres cargados por lotes, sin una consulta por fila. `HomeService` recibe pendientes por `IPendingItemSource`; los helpers no abren transacciones propias. Los tamaños y colores de estas pantallas remiten al tablero y a tokens del tema: no se cuentan como números mágicos por el solo hecho de estar en clases de CSS.
- **Plataforma:** las acciones globales y las que entran a una organización están diferenciadas. `PlatformActionGuard` tiene una responsabilidad concreta: autorización y SecurityEvent confirmado antes del scope y de la UoW; no sobra como simple pasamanos. Los writers y guards mantienen el caso de uso legible sin abrir transacciones propias. Organización reutiliza las verificaciones de dominios; operadores tienen permisos individuales y protección del último gestor; Legales publica versiones inmutables con culturas del catálogo y número bajo lock. Listados y conteos comparten filtros y las fichas usan evidencia persistida para fechas/estados. Las pestañas del front están separadas y las versiones propias de cada recurso no se reemplazan con un GET de fondo. No encontré motivos para cambiar las capas ni agregar CQRS.
- **Comunes del back:** `UnitOfWork` expresa el único guardado y limpia el tracking tras rollback; `TenantContext`, los interceptores y los helpers RLS concentran la clasificación y el sello de pertenencia. Búsqueda, orden estable y cursor están centralizados en `QueryableExtensions`. Los trails auxiliares tienen tareas reconocibles y las excepciones técnicas se traducen en el límite correspondiente. `CultureProfiles` y `DisplayFormatter` derivan patrones y monedas del catálogo, con contexto explícito y métodos tipados. No propongo repositorio genérico ni una nueva capa de wrappers.
- **Comunes del front y scripts:** API/error por campo, paginado, cursor y edición versionada tienen una forma compartida y comentarios que explican sus límites. `DataTable` y los campos reutilizan formatos y selectores; los tonos tienen registro único. Los formatos puros y parsers están separados de React, con datos de referencia y reglas de precisión explícitas. Los scripts de contratos separan generación de comprobación; la falta del repo hermano en CI es una excepción decidida, no un fallo omitido por accidente. Los puertos locales, datos del tablero, esperas de Playwright y constantes de formato tienen un contexto técnico concreto: no se reportan como configuración faltante por ser números fijos.
- **Identidad, ingreso y registro:** los servicios de entrada conservan la UoW y los helpers separan emisión, verificación, registro y resolución de Google sin abrir transacciones propias. Las cookies se emiten después del commit. El adaptador OIDC conserva el protocolo en su límite, sin convertir sus claims y redirects en nuevos servicios de negocio. Las invitaciones separan prueba del token, preparación, aceptación bajo lock y recuperación de sesión; el alcance se abre antes de la transacción. El alta empresarial deja creación y replay idempotente en `BusinessOrganizationCreator`, con defaults del catálogo y provisión compartida. La recuperación pública expresa sus pasos y abandona respuestas anteriores mediante su revisión; no hace falta convertirla en un motor genérico.
- **Mi cuenta y baja:** A5 eliminó la confirmación por código en las escrituras y conserva la prueba propia del correo/Google, las guardas y los avisos. A7 concentra la derivación de `DisplayName` desde nombre y apellido en `ApplicationUser`; Google completa solo partes vacías y el perfil tiene su versión propia, separada de cambios de sesión/contacto. El front compone perfil y métodos con los hooks comunes. La baja separa solicitud, cancelación y limpieza por participantes con lease, con invalidación/restablecimiento después del commit. El runner real ya tiene alcance cancelable por caso y espera su limpieza antes de empezar el siguiente. No cuento la ubicación de `ApplicationUser` en Infrastructure ni esas piezas de protocolo como infracciones de capas: son límites expresos de la arquitectura acordada.

## Verificación del informe

Hallazgos contrastados con código, fichas y decisiones; referencias comprobadas por lectura. Los números de línea corresponden al estado leído y pueden desplazarse con cambios posteriores. Para R07 se reprodujo únicamente el cálculo de fecha con `Intl`, sin escribir archivos. Se verificó el diff del informe y el contenido de cada commit. No se ejecutaron suites, builds, generación de contratos ni recorridos que escriban datos durante esta revisión; el cierre de pruebas de A1–A7 pertenece al otro trabajo. Cada commit de esta revisión incluye únicamente este archivo, en `main`, sin push.
