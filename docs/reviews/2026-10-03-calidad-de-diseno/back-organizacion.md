# Calidad de diseño · Back · organización, roles y empresas

> Informe de un revisor, solo lectura, sobre el commit `1202433 del back`. Los números de línea son de ese commit: buscá por símbolo. Antes de corregir un hallazgo, confirmalo en el código actual. El resumen, los bugs confirmados y el orden de trabajo están en [`../2026-10-03-calidad-de-diseno.md`](../2026-10-03-calidad-de-diseno.md).

Revisé el diseño del área Organización, Roles y Empresas en la copia congelada del commit 1202433. Fue solo lectura: no corrí build ni tests, así que lo marcado como "probable" sale de leer el código y conviene confirmarlo con un test.

**Rutas.** Son relativas a ``. Abrevio los proyectos así: `App` = `src\ArquitecturaBaseMultitenant.Application`, `Infra` = `src\ArquitecturaBaseMultitenant.Infrastructure`, `Api` = `src\ArquitecturaBaseMultitenant.Api`, `Domain` = `src\ArquitecturaBaseMultitenant.Domain` y `Tests` = `tests\`. El árbol de trabajo tiene cambios sin commitear en OrganizationSettings, así que ahí algunas líneas pueden estar corridas.

## 1. Puntajes

| # | Criterio | Puntaje | Justificación |
|---|---|---|---|
| 1 | Responsabilidad única y cohesión | 2/5 | Roles es cohesivo. Antes del commit d63e13c, `OrganizationUserService` tenía 26 dependencias y se partió en 13 archivos en un solo commit; varios de esos archivos solo reenvían llamadas para quedar en 8. |
| 2 | Inyección de dependencias y lifetimes | 4/5 | Todo es scoped, explícito y por constructor. Detalles menores: un registro muerto (`AddScoped<LastTenantAdminGuard>()` en `App\DependencyInjection.cs:119`), interfaces de guardas en `Interfaces/Services` y el usuario actual consultado en tres niveles distintos. |
| 3 | Capas y reglas en el lugar correcto | 3/5 | Las capas se respetan y hay tests que lo vigilan. Pero los readers de miembros y de auditoría deciden estados, enmascarado y qué nombre mostrar, y el Api valida correos armando `ValidationError` a mano. |
| 4 | Dominio con comportamiento | 3/5 | Role, RoleAssignment, Member, TenantDomain y PublicPage devuelven `Result`. Company y CompanyMembership tienen `SetStatus` sin reglas, y la sincronización de accesos vive en Application. |
| 5 | Duplicación | 2/5 | Hay búsqueda con SQL crudo, `EscapeLike` en 4 lugares, filtros de estado en SQL y en C#, la validación de alcance de roles en 5 lugares y el enmascarado en 3. Las copias ya divergieron. |
| 6 | Sobreingeniería | 3/5 | Los patrones base son intencionales. Sobra indirección: un coordinador y una clase de "efectos" que solo reenvían, un servicio de configuración vacío y tres clases para un solo aviso. |
| 7 | Clean code | 3/5 | Roles está prolijo. En el resto hay parámetros booleanos que esconden modos, métodos de 55 a 95 líneas y valores mágicos (períodos "0", "7", "30"; categorías; tipos de entidad; nombres de índices; "/cuenta"). |
| 8 | Manejo de errores | 3/5 | Result y ProblemDetails son consistentes. Pero el mismo caso devuelve códigos distintos según la pantalla, se remapea un error comparando `Error.Code`, y Roles no traduce la violación de unicidad. |
| 9 | Tests | 3/5 | Los tests de dominio y de los servicios de Roles y Empresas tienen nombres de conducta. Usuarios casi no tiene tests unitarios, los de integración son recorridos de 100 a 190 líneas, y faltan justo en búsqueda y en la matriz de permisos efectivos. |
| 10 | Consistencia con Roles | 2/5 | La lectura de Empresas y su edición siguen a Roles. Usuarios, Miembros de empresa, Configuración, Página pública y Auditoría se apartaron, y los AGENTS.md de cada carpeta mandan a copiar plantillas que ya se desviaron. |

## 2. Hallazgos

**H1 · Alta · Los helpers abren la transacción y el test de arquitectura se aflojó para permitirlo**
- **Evidencia:**
  - `Tests\ArquitecturaBaseMultitenant.ArchitectureTests\TransactionBoundaryTests.cs:24` tiene una lista blanca de 13 helpers, con el comentario "admite los seeds técnicos y los coordinadores internos de transacciones" (:58). Se agregó en el commit fdc678a, y el test se sigue llamando `Only_use_case_services_and_the_two_named_seeds_receive_unit_of_work` (:54).
  - Las reglas escritas dicen lo contrario: `docs\rules\guardado.md:28` prohíbe "Que un helper reciba `IUnitOfWork`" y `docs\architecture\backend.md:109` dice "Los helpers no guardan ni reciben `IUnitOfWork`".
  - Así se nota la fricción: `OrganizationUserStatusEditor.cs:23` abre la transacción y en :31 ofrece `ApplyAsync` "dentro de una transacción ya abierta", para que `OrganizationUserEditor.cs:30` lo reuse. Llamar a la variante equivocada anida transacciones, y eso lanza.
  - En Empresas, `CompanyService` abre la transacción en editar y cambiar estado, pero el alta la delega a `CompanyCreator.cs:21`.
  - `OrganizationSettingsService` ni siquiera valida: todo está en `OrganizationSettingsUpdater`.
- **Problema y refactor:** desde el servicio ya no se ve dónde empieza la transacción. Propuesta: volver al patrón de `RoleService`, donde el método público abre la transacción y los helpers trabajan dentro de ella, sin recibir `IUnitOfWork`. Para no pasar de 8 dependencias, partir el servicio por caso de uso, cada uno con su interfaz (por ejemplo, `IOrganizationInvitationService` para las rutas `api/users/invitations` e `IOrganizationMemberService`), y vaciar la lista blanca. Si se decide mantener el diseño actual, al menos hay que actualizar guardado.md, backend.md y el nombre del test.
- **Refutación:** ninguna regla lo exige; la regla escrita dice lo contrario.
- **Esfuerzo L.** Decidirlo ahora, antes de que la Etapa 7 copie el patrón, y hacer el refactor en el backlog cercano.

**H2 · Alta · Clases que solo reenvían para no pasar de 8 dependencias**
- **Evidencia:**
  - `OrganizationUserLifecycleCoordinator.cs:12-21`: sus tres métodos solo reenvían.
  - `OrganizationUserExitEffects.cs:10-15`: dos reenvíos.
  - `CompanyMembershipRoleManager.cs:50-58`: 4 de sus 6 métodos reenvían a repositorios.
  - `OrganizationSettingsService.cs:32`: `() => updater.UpdateAsync(request, cancellationToken)`.
  - Sin estas clases, `OrganizationUserService`, `OrganizationUserRemover` y `CompanyMembershipWriter` tendrían 10, 9 y 9 dependencias.
  - El StatusEditor llega directo al notificador (:17) y el Remover llega a través de ExitEffects: hay dos caminos al mismo colaborador.
- **Sobre los helpers que preguntaste:**
  - **Cohesivos, pero son casos de uso disfrazados de helper** (abren su propia transacción): StatusEditor, Remover, InvitationManager, CompanyCreator y CompanyMembershipWriter.
  - **Cohesivo pero con varios modos:** AssignmentManager, que mezcla validación previa, carga del estado actual, sincronización y `Clear`.
  - **Artificiales:** el coordinador, ExitEffects, la mitad de CompanyMembershipRoleManager, el par Settings/Updater y el ContextProvider (ver H14).
- **Refactor:** sacar las clases que reenvían e inyectar directo, y bajar dependencias partiendo por caso de uso como en H1. Configuración entra en 8 si se quita su registro de auditoría redundante (ver H6).
- **Refutación:** el tope de 8 es intencional, pero la regla pide helpers con un rol propio, no clases que solo reenvían.
- **Esfuerzo M.** En Usuarios ahora, coordinado con la otra conversación; en Empresas, al tocarlo.

**H3 · Alta · Los readers de miembros copian búsqueda y paginado por fuera de las piezas comunes, y las copias ya divergen**
- **Evidencia:**
  - El mismo SQL crudo aparece tres veces con tres escapes distintos:
    - `Infra\Persistence\Readers\OrganizationUserReader.cs:97-103` usa `ESCAPE '\'`.
    - `CompanyMembershipReader.cs:105-111` usa `ESCAPE '\\'` dentro de un raw string, o sea dos barras. PostgreSQL exige un solo carácter, así que probablemente la búsqueda con texto en miembros de empresa responde 500. Ningún test la cubre.
    - `OrganizationAuditReader.cs:187` usa `ESCAPE E'\\'`.
  - `EscapeLike` está en 4 archivos.
  - Usan `ILIKE` sin quitar acentos, así que "perez" no encuentra "Pérez" en Usuarios ni en Miembros, a diferencia de Roles y Empresas.
  - `CompanyMembershipReader.cs:41-45` cuenta y pagina con `Skip`/`Take` a mano, algo que `paginado-y-busqueda.md` prohíbe.
  - Los mapas de orden son propiedades de instancia (`OrganizationUserReader.cs:19`) y no campos estáticos como pide `backend.md:584`.
  - La subconsulta "correo verificado principal" se repite 3 veces por archivo.
- **Refactor:**
  - expresiones compartidas para el correo y el nombre visible de un miembro, usadas en orden y proyección;
  - buscar con `ApplySearch` y paginar con `ToPagedResultAsync`;
  - tests de búsqueda para `/api/users/paged` y `/members/paged`.
- **Refutación:** el SQL crudo puede venir de que el destino de la invitación es un value object con conversor, pero eso no justifica tres copias.
- **Esfuerzo M.** Ahora, el test de búsqueda y unificar el escape (S); el resto, al tocarlo.

**H4 · Alta · La regla "qué rol se puede asignar dónde" está en cinco lugares y devuelve códigos distintos**
- **Evidencia:**
  - **Dónde está la regla:**
    - `Domain\Authorization\RoleAssignment.cs:42-47`;
    - `OrganizationUserAssignmentManager.cs:29` y :44-46 (antes de la transacción) y :73 y :83-85 (adentro);
    - `CompanyMembershipRoleManager.cs:21-23` y :39-41;
    - `Infra\...\RoleReader.cs:102-113`, en el cálculo de permisos efectivos.
  - **Mismo caso, distinto código:** `CompanyMembershipRoleManager.cs:21` devuelve `RoleErrors.CompanyNotAllowed` y `OrganizationUserAssignmentManager.cs:44` devuelve `RoleAssignmentErrors.CompanyNotAllowed`.
  - **Doble consulta:** `CompanyMembershipRoleManager.cs:35-37` consulta dos veces por rol: una solo para obtener la versión y otra para cargar la entidad que pide `Assign`.
  - **Asignaciones inválidas en silencio:** `Role.Update` deja cambiar el alcance aunque el rol tenga asignaciones (`Role.cs:90-91`), y `RoleReader` filtra en silencio las asignaciones que quedan inválidas.
  - **Validación duplicada afuera y adentro:** `CompanyMembershipWriter.cs` repite el control "la empresa existe y está activa" 6 veces (:23-25/:33-35, :57-59/:67-69, :107-109/:114-116). Usuarios revalida el rol adentro de la transacción, pero no el estado de la empresa.
- **Refactor:**
  - una función pura en el dominio, `RoleAssignment.Check(alcance, empresaDelRol, empresa)`, que devuelva el error o nada;
  - un solo guard compartido por Usuarios y Empresas;
  - que `Assign` reciba un valor simple en lugar de la entidad;
  - que las reglas que consultan la base corran solo adentro de la transacción;
  - un test de matriz para los permisos efectivos.

  Es una decisión de producto si un rol con asignaciones puede cambiar de alcance.
- **Refutación:** revalidar adentro es defensa válida; tener dos códigos para el mismo caso no, porque el front traduce por código.
- **Esfuerzo M.** Al tocarlo; Usuarios, ahora.

**H5 · Media · El estado de un miembro se calcula en C# y en SQL, y las dos versiones ya difieren**
- **Evidencia:**
  - **Dos versiones de la regla:** `OrganizationUserStatePolicy.cs:10-26` frente a `OrganizationUserReader.cs:131-150`, y `CompanyMembershipService.cs:88-96` frente a `CompanyMembershipReader.cs:135-154`.
  - **Ya divergen:** un miembro invitado con su membresía de empresa inactiva es "Invitado" para el servicio (:90), pero entra en los filtros Invitado (:151) e Inactivo (:146). Los conteos lo cuentan dos veces. El caso es posible: `OrganizationUserAssignmentManager.cs:125-130` inactiva membresías de invitados.
  - **"Nombre o correo" en dos capas:** se resuelve en el servicio para usuarios (`OrganizationUserService.cs:119`) y en el reader para miembros de empresa (`CompanyMembershipReader.cs:75`).
  - **Filtro repetido:** `OrganizationUserReader.cs:81-85` repite un filtro que la consulta base ya aplica.
- **Refactor:**
  - una sola expresión de estado en SQL (un CASE) que sirva para filtrar, mostrar y contar con un `GroupBy` (hoy son 6 y 5 consultas de conteo);
  - la política en C# queda solo como especificación, con un test de paridad.
- **Esfuerzo M.** Al tocarlo.

**H6 · Media · El formato de los cambios auditados es implícito: dos escritores, dos lectores y el enmascarado en tres lugares**
- **Evidencia:**
  - **Dos formatos de escritura:** el interceptor escribe `{"Campo":{"old","new"}}` (`AuditTrailInterceptor.cs:179-181`). Las escrituras manuales escriben planos: `["domain"]` y `["status"]` en `OrganizationDomainRegistrar.cs:34-39`, y `["fields"]` en `PublicPageEditor.cs:71-72`.
  - **Lectura:** `OrganizationAuditService.cs:108-113` descarta lo que no viene como `old`/`new`, y `OrganizationAuditReader.cs:214-232` vuelve a interpretar el JSON por su cuenta.
  - **Enmascarado en tres lugares y ya distinto:** `SafeValue` en el interceptor (:188) y en el servicio (:129), y `MaskEmail` en el reader (:234). El del servicio también enmascara números fiscales; el del interceptor, no.
  - **Tipos de entidad como texto:** "TenantDomain", "TenantSettings" y "PublicPage" tienen que coincidir con `OrganizationAuditReader.cs:18-25` y con `ListOrganizationAuditRequestValidator.cs:11`.
  - **Probable doble entrada:** TenantSettings ya la audita el interceptor, y `OrganizationSettingsUpdater.cs:37` agrega una segunda entrada por cada guardado.
  - **Probable diff con campos sin cambios:** `MarkUpdated` marca la entidad completa como modificada (por ejemplo `RoleRepository.cs:47`), así que el diff incluiría todos los campos. Conviene confirmarlo con un test que edite un solo campo.
- **Refactor:**
  - un value object `AuditChanges` en Domain/Auditing, con Serialize/Parse;
  - `nameof(...)` en lugar de los textos;
  - un único enmascarador en Application/Common;
  - un enum `AuditCategory`;
  - que el interceptor omita los campos que no cambiaron.
- **Refutación:** backend.md §10 admite registros manuales (TenantDomain es global); el problema es que el formato no tiene contrato.
- **Esfuerzo M.** Backlog cercano.

**H7 · Media · El reader de auditoría decide qué mostrar y cómo enmascarar**
- **Evidencia:**
  - la taxonomía de categorías (:18-25);
  - el enmascarado (:234-238);
  - la regla de qué nombre mostrar como afectado (:136-143);
  - los mismos `switch` por tipo de entidad, dos veces en el mismo método (:64-72 con :118-125, y :100-108 con :129-135);
  - nombres de propiedades como texto ("MemberId", "RoleId");
  - `ResolveDisplayDataAsync` tiene unas 95 líneas.
- **Problema:** para sumar un tipo auditado hay que tocar 4 lugares en Infrastructure.
- **Refactor:** que el reader devuelva las entradas y diccionarios de nombres por id, y que una clase en Application decida a quién nombrar como afectado, el "dónde" y el enmascarado.
- **Refutación:** buscar los nombres en la base sí le corresponde al reader; lo que sobra es la política.
- **Esfuerzo M.** Al tocarlo.

**H8 · Media · Los AGENTS.md mandan a copiar plantillas que ya se desviaron**
- **Evidencia:**
  - `App\Services\PublicSite\AGENTS.md` dice "Copiá la escritura de … OrganizationSettingsService.cs", y ese servicio no escribe: delega en el Updater.
  - El AGENTS.md de OrganizationSettings apunta a CompanyService.
  - `Infra\Persistence\Readers\AGENTS.md` dice "Copiá de: ReferenceDataReader.cs", aunque `paginado-y-busqueda.md` dice RoleReader.
  - `Services\OrganizationUsers` no tiene AGENTS.md.
  - `docs\rules\capas-y-flujo.md:11` fija los sufijos Policy, Guard, Issuer, Verifier y Linker, pero el código usa además Manager, Editor, Remover, Coordinator, Effects, Cleaner, Notifier, ContextProvider, Resolver, Writer, Creator, Registrar, Updater y Assigner, y ningún test lo controla.
  - backend.md §4 describe `UserService`, `api/settings` y `api/public-site`, que no existen así en el código.
- **Problema:** esto explica el puntaje 10: cada área copió de la anterior y no de Roles.
- **Refactor:**
  - que todos los AGENTS.md apunten a Roles;
  - crear el AGENTS.md de OrganizationUsers;
  - actualizar backend.md §4 al árbol real;
  - cerrar la lista de sufijos y verificarla en `ApplicationServicesTests`.
- **Refutación:** es documentación, pero en este proyecto el arnés es lo que sostiene la consistencia del diseño.
- **Esfuerzo S.** Ahora.

**H9 · Media · El mismo error se maneja distinto según el área**
- **Evidencia:**
  - **404 en vez de 400:** `OrganizationUserService.cs:114` hace `if (memberId == Guid.Empty || version is null) return UserErrors.NotFound;`, así que una versión faltante da 404 con código `Users.User.NotFound`. En la misma clase, `GetAsync` (:57) usa `MemberErrors.NotFound`, y en Roles la versión faltante da 400.
  - **Remapeo frágil:** `OrganizationUserAdministratorGuard.cs:31-32` remapea comparando `Error.Code` contra `AccountDeletionErrors.LastAdmin.Code`.
  - **Roles sin traducir la unicidad:** `RoleConfiguration.cs:23` tiene un índice único por nombre, pero `RoleService` no traduce la violación de unicidad. Dos altas simultáneas con el mismo nombre dan 500.
  - **Nombres de índice como texto:** Empresas, Dominios y Página pública sí traducen la violación, pero con nombres de índice literales que dependen de la convención de EF (`CompanyService.cs:25`; "IX_Tenants_Slug" en `PublicPageEditor.cs:19` y en `TenantAdministrationService.cs:38`).
  - **Dos prefijos para la misma área:** `Tenancy.Company.*` y `Companies.Membership.*`.
- **Refactor:**
  - un `RemoveOrganizationUserRequest` con su validador, como `DeleteRoleRequest`;
  - que la guarda del último Dueño devuelva un resultado neutro y cada llamador elija su error;
  - constantes compartidas para los nombres de índice, usadas en `HasDatabaseName` y en el `catch`;
  - el `catch` también en `RoleService`;
  - un solo prefijo por área.
- **Esfuerzo S-M.** Lo de RoleService y el 404/400, ahora; el resto, al tocarlo.

**H10 · Media · Los controllers y contratos se apartan de RolesController y de reglas escritas**
- **Evidencia:**
  - **Ternarios prohibidos** (backend.md §5: "Nunca IsSuccess ? … : …"): `OrganizationDomainsController.cs:41`, `OrganizationUsersController.cs:58` y :75 (un 201 sin `Location`) y `PublicPageController.cs:28`.
  - **Mapeo de páginas a mano:** `CompanyMembersController.cs:33-37` y `OrganizationUsersController.cs:32-36`.
  - **Correo convertido en el Api:** se arma el `ValidationError` a mano en `OrganizationUsersHttpRequests.cs:44-50` y `PublicPageHttpRequests.cs:29-46`, aunque emails.md dice que lo convierte el servicio o el validador.
  - **Choques de nombres:** se resuelven con alias (`OrganizationUsersController.cs:13`) y con un nombre totalmente calificado (:41).
  - **Versión del DELETE:** viaja en el cuerpo en Roles (`RolesController.cs:82`) y en la query en Usuarios y Dominios (:132 y :63).
- **Refactor:**
  - sobrecargas `ToActionResult(this, map)` y `ToCreatedResult(..., map)`;
  - sufijo `*HttpResponse` para las respuestas HTTP;
  - una sola forma de enviar la versión en un DELETE;
  - decidir y documentar dónde se convierte el correo (Auth también lo hace en el Api).
- **Refutación:** los dos tipos de respuesta los fuerza la regla "los modelos usan Email"; lo criticable son los ternarios y los nombres.
- **Esfuerzo S-M.** Al tocarlo.

**H11 · Media · Tests largos y huecos justo donde está lo duplicado**
- **Evidencia:**
  - **Pocos unitarios donde más lógica hay:** en Usuarios, el único test unitario es `OrganizationUserStatePolicyTests.cs` (27 líneas); AssignmentManager, AdministratorGuard y los editores no tienen.
  - **Recorridos largos:** un solo test de unas 190 líneas para todo Empresas (`CompaniesTests.cs:21`) y uno de unas 140 para Roles (`RolesTests.cs:22`).
  - **Ruido y fragilidad:** la creación del cliente HTTP se repite 12 veces, y hay aserciones sobre el texto traducido "Administrador" (`OrganizationUsersTests.cs:179`, `CompaniesTests.cs:116`).
  - **Huecos:** no hay tests de búsqueda en Usuarios ni en Miembros (por eso pasó lo de H3), ni de matriz para los permisos efectivos (`ListRoleIdsForUserAsync`).
  - **Formato que nadie usa:** `OrganizationAuditTests` siembra un formato que los llamadores reales no usan (ver H6).
- **Refactor:**
  - un test por conducta;
  - un builder de escenario y DTOs tipados;
  - tests de matriz y de búsqueda;
  - unitarios del plan puro que propongo en H12.
- **Esfuerzo M.** Los tests de búsqueda y de matriz, ahora; el resto, al tocarlo.

**H12 · Media · Dominio anémico en empresas y membresías**
- **Evidencia:**
  - `CompanyMembership.cs:37` y `Company.cs:82-86` tienen `public void SetStatus(...)` sin reglas;
  - las reglas de activación están en `CompanyMembershipWriter.cs:122-131` y en `OrganizationUserStatusEditor.cs:33-46`;
  - el cálculo de qué asignaciones agregar y quitar está duplicado (`OrganizationUserAssignmentManager.cs:91-104` y `CompanyMembershipWriter.cs:93-98`);
  - `CompanyMembership.Create` (:30) devuelve un `Result` que nunca falla.
- **Contraste:** Member sí tiene transiciones con error de transición inválida, así que el patrón ya existe.
- **Refactor:**
  - `CompanyMembership.Activate` y `Deactivate` que devuelvan `Result`;
  - un `Member.Deactivate(actor)`;
  - un plan puro, `MemberAccessPlan`, que calcule altas, bajas y activaciones, testeable sin repositorios.
- **Refutación:** backend.md §4.1 apoya este cambio.
- **Esfuerzo M.** Al tocarlo.

**H13 · Media · Una guarda con parámetros booleanos, y la protección del último Administrador repetida**
- **Evidencia:**
  - `ProtectAsync(..., bool deactivating, bool removing, ...)` se llama con tres combinaciones (`OrganizationUserRemover.cs:29-30`, `OrganizationUserEditor.cs:34-35`, `OrganizationUserStatusEditor.cs:52-53`);
  - la lógica `!deactivating && !removing && desired is not null` está en `OrganizationUserAdministratorGuard.cs:24`;
  - los roles de sistema se buscan por nombre en cada llamada (:23, :35);
  - el cálculo "este cambio quita un Administrador de empresa" se repite en `CompanyMembershipWriter.cs:78-88` y :135-142.
- **Refactor:**
  - describir el cambio por el resultado (las asignaciones después del cambio; desactivar o quitar es el conjunto vacío);
  - un único guard para el último Administrador de empresa.
- **Esfuerzo M.** Al tocarlo.

**H14 · Baja · El "proveedor de contexto" de un aviso registra un evento de seguridad**
- **Evidencia:**
  - `OrganizationMembershipEndNoticeContextProvider.cs:34` agrega un evento de seguridad dentro de `PrepareAsync`;
  - hay tres clases para un solo aviso;
  - la ruta del front "/cuenta" está escrita a mano (:42);
  - si falta la organización devuelve `MemberErrors.NotFound` (:40).
- **Problema:** avisar audita como efecto oculto.
- **Refactor:** que el caso de uso registre el evento y el notificador absorba al proveedor de contexto, que le alcanza con 8 dependencias o menos.
- **Esfuerzo S.** Al tocarlo.

**H15 · Baja · Valores mágicos y validación dispareja**
- **Evidencia:**
  - los períodos "0", "7", "30" y "all" como texto (`ListOrganizationAuditRequestValidator.cs:12`, `OrganizationAuditService.cs:55-69`);
  - `AppliesTo is not ("Company" or "Both")` en `CompanyReferenceDataResolver.cs:17`;
  - 500 y 2048 escritos a mano en `PublicPage.cs:41` en lugar de `TextLimits`;
  - los tipos de logo repetidos en `UpdatePublicPageRequestValidator.cs:24` y `PublicPage.cs:110-117`;
  - `ValidationTexts.Required` usado como mensaje de reglas que no son de obligatoriedad;
  - Empresas no usa `OrganizationName()` ni `ValidTimeZone()`, y valida la zona en el servicio (`CompanyService.cs:66`, :91), mientras Configuración la valida con la regla común (`UpdateOrganizationSettingsRequestValidator.cs:18`).
- **Refactor:**
  - enums para períodos y categorías;
  - `TextLimits` en lugar de números sueltos;
  - las reglas comunes de `ValidationRules`;
  - mensajes específicos para cada regla.
- **Esfuerzo S.** Al tocarlo.

## 3. Lo que conviene conservar como patrón
- **RoleService:** valida antes, abre una transacción por método público, invalida la caché después del commit y usa `RoleErrors`, incluida la fábrica con metadata `HasUsers(userCount)`.
- **RoleReader y CompanyReader:** mapa de orden estático, `ApplySearch`, `ToPagedResultAsync`, desempate por Id y proyección solo de los ids de la página.
- **Dominio:** transiciones que devuelven `Result` y value objects sólidos (TenantSlug, OrganizationEmailDomain, TaxId).
- **OrganizationDomainVerifier:** consulta el DNS fuera de la transacción y después, adentro, toma el lock y recarga con versión. Es el patrón correcto para llamadas externas.
- **Repositorios y locks:** los repositorios exigen transacción y organización, y controlan la concurrencia con la versión esperada. Los locks se toman ordenados (`OrganizationUserAdministratorGuard.cs:42`), lo que evita deadlocks.
- **PermissionService:** cachea solo los permisos inmutables de cada rol y combina en vivo las asignaciones.
- **Controllers y tests de dominio:** los controllers son finos y declarativos. Los tests de dominio, y los de servicio de Roles y Empresas, tienen nombres de conducta y dobles escritos a mano.

## 4. Veredicto
- Roles es una buena referencia y la lectura y edición de Empresas la siguen; Usuarios, Miembros de empresa, Configuración, Página pública y Auditoría se apartaron.
- La causa principal es que, para cumplir el tope de 8 dependencias, se crearon clases que solo reenvían y se movió la transacción a los helpers, aflojando el test en lugar de partir los servicios por caso de uso. De ahí sale la duplicación, que ya divergió en búsqueda, estados, alcance de roles y auditoría.
- Prioridad: primero, los tests de búsqueda y de matriz y los AGENTS.md apuntando a Roles (S, ahora). Después, devolver la transacción al servicio y unificar las reglas duplicadas (M-L, antes de la Etapa 7). H1, H2 y H4 tocan Usuarios, que ya está editando la otra conversación: conviene coordinarlo con ella.
