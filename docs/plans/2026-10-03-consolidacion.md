# Consolidación previa a la Etapa 7 — plan ejecutable

> Plan de implementación. Seguir el orden, completar las casillas con evidencia y commitear cada cambio coherente y verificado con rutas explícitas.

**Estado:** aprobado por el usuario el 2026-10-03.

**Objetivo:** que la Etapa 7 copie referencias sanas. Para eso:
- se corrigen los bugs de la revisión de calidad;
- se aplica el ADR 0039 a lo que ya existe;
- se unifica cada regla duplicada;
- se crean las piezas comunes del front.

No hay pantallas nuevas. Los teléfonos de v78 necesitan el marco de app (Paquete 4), así que van con el front de la Etapa 5.

**Fuentes:**
- La [revisión de calidad de diseño](../reviews/2026-10-03-calidad-de-diseno.md) y sus anexos, con archivo y línea de cada hallazgo. Las líneas son del commit `1202433` del back y `961f786` del front, así que hay que buscar por símbolo.
- El [ADR 0039](../decisions/0039-tope-de-dependencias-de-negocio.md) y la decisión D2 de la revisión.
- El lienzo v78 (`../../../ArquitecturaBaseMutitenantFront/docs/design/lienzo/`), para lo que se ve: la columna Estado de Empresa, los mensajes y el formato del enmascarado.
- AGENTS.md, `backend.md`, `frontend.md`, `arbol.md` de los dos repos y las fichas de `docs/rules/`.

**Cuándo empieza:** cuando estén commiteados, con `git status` limpio en los dos repos:
- la tanda de alineación (paquetes 1 y 3 e Inicios);
- el back pendiente de la Etapa 5 (T10 a T18);
- la alineación del front que «Paquete 0» hace en paralelo desde el 2026-10-03, con aprobación del usuario: Paquete 2, punto 1.11, Paquete 4 (con los teléfonos de v78) y Paquete 5.

Las pantallas de la Etapa 5 (T19 a T25) van después de esta consolidación, para usar sus piezas comunes.

**Quién:** Claude programa y verifica. Usa dos agentes en paralelo solo donde los archivos no se cruzan: el back en los bloques 3 y 4, y el front en los bloques 5 y 6. Cada entrega se revisa antes del commit.

**Restricciones:**
- **Repositorio:** se trabaja en `main`, sin push. Commits chicos en español, con rutas explícitas.
- **Conducta:** es un refactor y la conducta no cambia, salvo en los bugs y en D2. Si un test se rompe solo por estructura (clases, rutas de archivo), se cambia para que pruebe conducta.
- **Sin sobreingeniería:**
  - ninguna abstracción nueva sin dos usos reales;
  - ningún detector ni lector de IL nuevo;
  - antes borrar que envolver.
- **Hallazgos:** antes de corregir uno, se confirma en el código actual. Si no se sostiene, se anota en el informe y se sigue.
- **Tests:**
  - dirigidos mientras se trabaja;
  - la suite completa del back al cerrar los bloques 3 y 4;
  - la del front al cerrar los bloques 5 y 6;
  - las dos en la puerta.
- **Lienzo:** no se toca. Lo que cambie lo que ve o puede hacer un usuario se pregunta.

**Fuera de alcance:** lo de «Primer paso de la Etapa 8», «Al tocarlo» y «Backlog» de la revisión.

## Seguimiento

### 0. Línea de base y documentación

- [ ] `main` limpio y último commit en los dos repos; Aspire apagado.
- [ ] El ADR 0039 está aplicado en `ServiceDependencyCountTests` (se le pidió a la Etapa 5). Si no lo está, aplicarlo primero:
  - el test con su caso de control;
  - `capas-y-flujo.md`, `guardado.md` y `backend.md` §4.
- [ ] Línea de base: `realinear-docs --detectar` en los dos repos. El 2026-10-03, el back dio 372 hallazgos automáticos, 193 de modelo y 217 preguntas.
- [ ] Aplicar lo automático con `realinear-docs --sin-modelo` en los dos repos.
- [ ] Corregir los docs viejos (📄) y aplicar las enmiendas de la [revisión de decisiones](../reviews/2026-10-03-revision-de-decisiones.md). La lista está en su «Reparto» y el detalle con archivo y línea, en sus anexos. Incluye los docs que dicen que el CI ya compara los contratos (ADR 0016) y `backend.md:53` (ADR 0017).
- [ ] Borrar las cuatro carpetas sobrantes de `tools/` (`LocalSetup`, `PlatformBootstrap`, `PlatformOperatorAdmin` y `Setup`), que solo tienen `bin` y `obj` y no están versionadas.
- [ ] Resolver los hallazgos de modelo y las preguntas, comparando con el código:
  - `arbol.md` de los dos repos, llevado al árbol real;
  - los códigos de error, permisos y rutas, cada uno en su ficha de `docs/features`;
  - la fila de `capas-y-flujo.md` en AGENTS.md.

  Al usuario va solo lo que cambia el producto.
- [x] Sumar la consolidación al plan maestro como paso previo de la Etapa 7. Se hizo al aprobar el plan.
- [ ] **Hecho cuando** `--detectar` sale 0 en los dos repos.

### 1. Bugs que se arreglan por su cuenta (test rojo → arreglo → verde)

- [x] **B1 · Buscar con texto entre los miembros de una empresa da 500.** Hecho en `cfbf1bd`; test `CompaniesTests.Searching_company_members_by_text_finds_members_and_pending_invitations` (rojo con «invalid escape string»).
  - Test: integración de `GET /api/companies/{id}/members/paged?search=`, con y sin invitaciones pendientes.
  - Arreglo: un solo carácter en el `ESCAPE` de `CompanyMembershipReader`. El bloque 4 después lo reemplaza por `ApplySearch`.
- [x] **B2 · El alta de un rol se traba con 422 después de un error.** Hecho en el front, `1c2151f` (`RoleEditorPage.test`).
  - Test de `RoleEditorPage`: el nombre está repetido (409), se corrige y el segundo «Guardar» sale con otra clave y crea el rol.
  - Arreglo: `useIdempotentMutation`.
- [x] **B3 · A los miembros de Empresa les falta la columna «Estado».** Ya estaba arreglado; el test de encabezados y el fin de la columna de relleno están en el front, `acc4613`.
  - Test de encabezados: Miembro, Roles en la empresa, Estado y Acciones, como el tablero Empresa.
  - Arreglo: la factory de columnas recibe la de acciones, y se borra la columna de relleno de Empresas.
- [x] **B5 · Página pública muestra mensajes de validación equivocados.** Hecho en `100d713`; test `PublicPageValidationTests` (Application.UnitTests).
  - Test del validador: cada regla tiene su texto; el slug y el logo ya no dicen «La página debe estar entre…», y el nombre usa `ValidationRules.OrganizationName()`.
  - Arreglo: textos propios en `Validation.resx` y `.en.resx`.
- [ ] **B7 + D2 · Un rol con personas asignadas no puede cambiar de alcance.** Back hecho en `600405a`: 409 `Roles.Role.ScopeChangeHasUsers` con `userCount`, textos es/en y `roles.md`; tests `RoleTests.The_scope_changes_when_it_applies_somewhere_else_but_not_when_only_the_name_changes`, `RoleServiceWriteTests.A_role_with_assigned_users_cannot_change_where_it_applies` y `…without_assigned_users_changes_where_it_applies`, `RolesTests.A_role_with_assigned_people_cannot_change_where_it_applies_but_one_without_people_can`. **Falta el front.**
  - Tests de dominio o servicio, y de integración: si el rol tiene asignaciones, cambiar el alcance da un error propio con el conteo; si no tiene, el alcance cambia.
  - Front: el error aparece al guardar, igual que al borrar.
  - Textos en es y en; actualizar `docs/features/roles.md`.
- [x] **B9 · Configuración audita dos veces cada guardado.** Hecho en `9b6cb5c`; tests `OrganizationSettingsTests.A_save_leaves_a_single_audit_entry_whose_diff_has_only_the_edited_field` y `AuditTrailTests.An_entity_marked_as_updated_records_only_the_properties_that_changed`. Ver la decisión 1.
  - Test: un guardado deja una sola entrada, y si se edita un campo, el diff tiene solo ese campo.
  - Arreglo: sacar la entrada manual que sobra y hacer que el interceptor omita las propiedades que no cambiaron.

B4, B6, B8, B10, B11 y B12 se arreglan con su pieza, en los bloques 4 y 5.

### 1b. Pendientes del cierre de los paquetes 1 y 3

- [ ] **1.2 · Configuración › dominio:** mostrar cuántos correos administra el dominio. Quedó esperando los archivos de dominios de Plataforma, ya commiteados en `4bb5c35`. Back con su test y front como el tablero. Back hecho en `7cb8f75`: `managedEmailCount` en cada dominio (también en Plataforma); test `OrganizationDomainsTests.Owner_verifies_removes_and_keeps_existing_login_method_associations`. **Falta el front.**
- [x] **1.3 · Teléfono de la página pública:** revisar la columna `varchar(16)` contra `telefonos.md` y migrarla si hace falta, y pasar `PublicPage.ContactPhone` al value object `PhoneNumber`. Hecho en `fe74254`, con la migración `PublicPageContactPhoneE164` (varchar(100) → varchar(16)); test `DbContextModelTests.Public_page_contact_phone_is_stored_as_e164_like_every_phone_column`. Ver la decisión 3.
- [x] **Recuperar sin delatar por el tiempo** (ADR 0041; lo vio el back de la Etapa 5, decisiones 22 a 26 de su plan). Hecho en `e433c2c` y `533215d`, con la migración `AccountRecoveryDeferredNotice`; test `AccountRecoveryTests.Registering_a_request_writes_the_same_with_or_without_an_account_and_the_account_hears_afterwards`. Ver la decisión 4.
  - hoy el aviso a la cuenta suma tiempo solo cuando la cuenta existe;
  - el pedido tiene que hacer el mismo trabajo en los dos casos: el aviso se decide y se encola fuera de la respuesta, o las dos ramas escriben lo mismo;
  - test: con una cuenta que existe y con una que no, el pedido hace las mismas escrituras.
- [x] **Aprobar una recuperación avisa a todos los métodos** (ADR 0033): el método que se suma al aprobar dispara el aviso de método agregado en todos los métodos de la cuenta, como cualquier método nuevo. Hoy solo se avisa al correo nuevo. Va con su test. Hecho en `8d4e1e5`; test `PlatformRecoveryTests.Approving_makes_the_new_email_the_primary_method_and_the_person_enters_with_it`.
- [x] **Aviso «Alguien pidió recuperar tu cuenta» con los textos de v89** (agregado del 2026-10-04, lienzo v89 aprobado): saludo, fecha y hora del pedido, quién revisa, la nota con el correo de soporte y sin botón ni el método nuevo; sale la nota alternativa `RecoveryRequested.Note`. Hecho en `f89686c`; test `EmailTemplateTests.Recovery_request_notice_uses_the_approved_copy_without_button_or_new_method`. La plantilla de WhatsApp es de la Etapa 8.
- [x] **D5 · Única Dueña y baja** ([ADR 0035](../decisions/0035-baja-de-cuenta.md), decisión del usuario del 2026-10-03). Hecho en `3e66117`; tests `AccountDeletionTests.The_sole_owner_of_a_suspended_organization_cannot_request_deletion_either` y `UserTenantAccessReaderTests.Tenant_admin_reader_counts_active_owners_in_every_organization_that_is_not_closed`. Ver la decisión 9.
  - bloquean la baja todas las organizaciones no cerradas, incluidas las suspendidas;
  - la baja que pide la persona deja de mirar solo las activas (`UserTenantAccessReader`) y usa la misma regla que la baja por Plataforma;
  - test con una organización suspendida;
  - actualizar `roles.md` e `identidad.md`.
- [ ] **D6 · Tipo de identificación fiscal según el país** (decisión del usuario del 2026-10-03). Back hecho en `c1c15cd`: el contrato deja `taxIdType`; tests `CompanyServiceTests.The_tax_id_type_is_the_default_for_the_organization_country`, `…In_a_country_without_a_company_tax_id_type…`, `…Editing_keeps_the_saved_type…` y `CompaniesTests.The_company_tax_id_takes_its_type_from_the_organization_country` (Argentina y Estados Unidos). Ver la decisión 10. **Falta el front.**
  - la ficha de Empresa sigue pidiendo solo el número;
  - el tipo sale del tipo por defecto del país de la organización, según los datos de referencia: CUIT en Argentina;
  - `identificacion-fiscal.md` lo dice;
  - tests con Argentina y con otro país.
- [x] **D7 · Auditoría y ex miembros** (decisión del usuario del 2026-10-03). Hecho en `f24df16`; test `OrganizationAuditTests.Someone_who_is_no_longer_a_member_appears_only_with_their_name`.
  - si quien actuó ya no es miembro de la organización, se muestra solo su nombre, sin el correo (`OrganizationAuditReader`);
  - test con un ex miembro.
- [ ] **D4 · Mi cuenta: agregar un correo sin revelar si es de otra cuenta** ([ADR 0041](../decisions/0041-no-revelar-cuentas.md)). Back hecho en `8cb4830` y `b05caca`; tests `AccountMethodsHttpTests.Adding_an_email_of_another_account_answers_like_a_free_one_and_only_its_owner_hears` y `…An_account_can_still_add_the_email_of_its_own_google_sign_in`. Ver la decisión 8: los pasos siguientes (verificar, reenviar y la lista) los cerró D4b (`6b50777`). **Falta el front.**
  - Si el correo es de otra cuenta, la respuesta y la pantalla son las mismas que con uno libre. No se manda código, y a su dueño le llega el aviso «Método de ingreso: ya es de una cuenta» (lienzo v83, Mensajes):
    - asunto y título: «Ya tenés una cuenta con este correo»;
    - texto: «Alguien quiso agregar este correo para entrar a otra cuenta de ArquitecturaBase. Este correo ya es de una cuenta, así que no lo agregamos y no mandamos ningún código. / Si fuiste vos, entrá con este correo: ya tenés una cuenta.»;
    - nota: «Si no fuiste vos, ignorá este mensaje.».
  - Sale de esta respuesta el error `Identity.LoginMethod.AlreadyUsed` («Este correo ya está asociado a una cuenta.», en `Errors.resx`). El de Google (`Identity.Google.AlreadyUsed`) se conserva, porque vincular Google queda fuera de la 0041.
  - Tests: el correo libre y el de otra cuenta dan la misma respuesta; en el segundo, el aviso se encola y no se emite ningún código.
- [x] **D4b · La ADR 0041 hasta el final en Mi cuenta** (tanda 2 del back). Con el `methodId` que devuelve D4, hoy verificar y reenviar dan 404 y la lista no muestra el correo como pendiente: eso delata la cuenta. Hecho en `6b50777`, con la migración `LoginMethodPendingPerAccount`; tests `AccountMethodsHttpTests.The_email_of_another_account_stays_pending_and_each_step_answers_like_a_free_one`, `…The_pending_email_of_another_account_is_removed_and_expires_like_a_free_one` y `LoginCodeTests.An_unsent_or_unusable_destination_code_fails_like_a_wrong_code_and_spends_attempts`; `LoginPersistenceTests.A_verified_method_value_is_unique_across_accounts_and_a_pending_one_within_its_account` prueba el índice nuevo (`2fcf0e4`). Ver la decisión 14. El front no cambia de contrato; su parte de D4 sigue pendiente.
  - El correo de otra cuenta aparece en la lista como pendiente, igual que uno libre.
  - Verificarlo responde como un código incorrecto (cuenta el intento).
  - Reenviar responde igual que con uno libre.
  - El pendiente se puede quitar y vence como cualquier otro.
  - El pendiente no choca con el índice único de `LoginMethods`; por ejemplo, el índice único vale solo para los métodos verificados.
  - Tests de la misma respuesta en cada paso.
- [x] **Un solo aviso al método nuevo de una recuperación** (tanda 2 del back): hoy recibe «recuperación aprobada» y además «agregaste un correo». El aviso de método agregado va a los demás métodos, no al nuevo. Hecho en `35fd948`; test `PlatformRecoveryTests.Approving_makes_the_new_email_the_primary_method_and_the_person_enters_with_it` (rojo: el correo nuevo recibía dos avisos). Reemplaza la decisión 6.
- [ ] **D6, front:** si el país de la organización no tiene un tipo fiscal para empresas, el campo no aparece (decisión 10 del back).
- [x] **D3 · Nombres de rol repetidos, como el tablero Roles** (decisión del usuario del 2026-10-03). Hecho en `2956276`, con la migración `RoleNamePerCompany`; tests `RolesTests.A_role_name_repeats_across_companies_but_not_within_a_company_or_the_whole_organization`, `DbContextModelTests.E4_roles_are_private_versioned_and_assignments_have_tenant_scoped_keys` y `RolePersistenceTests`.
  - un rol de una empresa puede llamarse igual que el de otra empresa;
  - los roles de toda la organización siguen siendo únicos entre sí.

  El índice único pasa a organización + empresa + nombre (`NULLS NOT DISTINCT` para los de toda la organización) y su nombre entra en `UniqueKeys`. Hay que actualizar `docs/features/roles.md`.

  Tests:
  - dos empresas con «Solo lectura» se crean;
  - el mismo nombre dos veces en una empresa da 409;
  - el mismo nombre dos veces en toda la organización da 409.

### 1c. Decisiones del lienzo v99 (2026-10-04)

El usuario las resolvió con Diseño (front `706cd3c`). Todas están en Decisiones por pantalla.

- [ ] **Fecha corta en las listas del teléfono.** Sin el año solo en las listas del teléfono y solo para el año en curso (`27/09` y `27/09 10:42`; en en-US, `09/27`). La de otro año va completa, y en la compu siempre lleva el año.
  - Front: la ficha `formatos.md` suma «Fecha corta», `useFormat` la da y las listas del teléfono la usan. Tests en es-AR y en-US, con el año en curso y con otro año.
- [ ] **Subtítulo «Administración» en el teléfono.** El 403 y el 404 ya lo hacen así en la app. Falta M-Auditoria-Org, que muestra «Últimos 30 días» (el filtro) en lugar de «Administración».
- [ ] **El correo del perfil activo en tu menú** (el pie en la compu, «Más» en el teléfono):
  - en una organización, el correo que administra esa organización y, si no hay, el principal;
  - en el perfil personal, el principal.
  - Back: `/api/me` devuelve el correo del perfil activo, con su test. Front: el menú lo muestra.
- [ ] **Invitar un correo con una invitación de operador pendiente o vencida.** Hoy da «Ese correo ya tiene una cuenta…» (decisión 22). Pasa a un error propio, 409 con «Ese correo ya tiene una invitación. Reenviala desde la lista.», en es y en en, con su test. Las pantallas de Cuentas lo usan en la Etapa 5.
- [ ] **El correo de un operador invitado por alguien sin nombre:** se verifica que el asunto y el texto sean los de Mensajes › «Invitación como operador: sin nombre»: «El equipo de ArquitecturaBase te invitó como operador» y «Hola. El equipo de ArquitecturaBase te invitó a sumarte como operador de la plataforma.».
- Sin trabajo, porque la app ya es así:
  - M-Usuario: «Estado» y «Roles de la organización» abren hojas de opciones;
  - M-Pagina-Org: el país y el número del teléfono van en dos campos.

### 2. Arnés y referencias

- [ ] Los «Copiá de» apuntan a referencias sanas:
  - **back:** `RoleService`, `RoleReader` y `CompanyReader`. Va en los `AGENTS.md` de `Services/PublicSite`, `Services/OrganizationSettings` y `Persistence/Readers`, y en `paginado-y-busqueda.md`;
  - **front:** `personal/account` para formularios y diálogos, y `UsersPage` para listados, hasta que el bloque 6 deje Roles como referencia.
- [ ] Crear el `AGENTS.md` de `Services/OrganizationUsers`.
- [x] Agregar un test que exija un mensaje propio en cada regla de validación: recorre `CreateDescriptor()` de cada validador del ensamblado. Hecho en `74ee2fc`: `ValidationMessageTests`, con caso de control; encontró el filtro de estado de Empresas.
- [x] Borrar `Stage3aInventoryTests`. Hecho en `3dafde9`.
- [ ] **Capturas de la 3b contra el lienzo vigente:** el script todavía compara contra la copia v35 e inyecta estados internos. Hay que pasarlo al lienzo vigente y sumar el test del Paquete 6 de la alineación: falla si una etapa nueva apunta a otra copia del lienzo.
- [x] **Salida del Api** (enmienda de la ADR 0002): lo que devuelve un controller es un `*Response` de Application o un `*HttpResponse` de `Api/Contracts`. Renombrar los tres `*Response` del Api que no siguen el sufijo, corregir `Api/Contracts/AGENTS.md` y sumar un test de salida junto a `ControllerInputContractTests`. Hecho en `f47dcd0`, con `ControllerOutputContractTests`; ver la decisión 12. Falta regenerar los contratos del front.
- [ ] **Accesibilidad** (ADR 0029):
  - el «Copiá de» de `accesibilidad.md` apunta a `UsersPage.test.tsx`, que corre axe;
  - un chequeo en el arnés del front exige que todo `pages/*Page.test.tsx` corra axe.
- [x] `OrganizationUsersTests.InvitationEmailAsync`: guardar los ids del outbox antes de invitar, como hace `InvitationsTests`, en lugar del try/catch que saltea los mensajes que no se pueden descifrar. Hecho en `2cd3893`.

### 3. Back: la transacción vuelve al servicio, sin clases que solo reenvían

Cada servicio de entrada abre la transacción en su método público, y los helpers trabajan adentro sin recibir `IUnitOfWork`. Si un servicio pasa de 8 dependencias de negocio, se parte por caso de uso; por ejemplo, en Usuarios, las invitaciones aparte de los miembros.

- [x] **Usuarios:** hecho en `16dee4e` (el arreglo) y `3739c24` (la transacción). `OrganizationUserService` abre la transacción en sus seis métodos que escriben y queda con 7 dependencias de negocio, así que no se parte (decisión 24).
  - `OrganizationUserEditor`, `OrganizationUserStatusEditor`, `OrganizationUserRemover` y `OrganizationUserInvitationManager` dejan de recibir `IUnitOfWork`;
  - se borran `OrganizationUserLifecycleCoordinator` y `OrganizationUserExitEffects`;
  - corregir `OrganizationUserAdministratorGuard`: usa `CheckCanLeaveAsync`, que mira todas las organizaciones de la cuenta. Así, quitar el rol de Dueña en una organización falla si la cuenta es la única Dueña de otra. Tiene que mirar solo la organización actual, y va con su test (lo encontró la tanda 1). Test `OrganizationUsersTests.Demoting_an_owner_checks_only_this_organization`: rojo con 409 `Tenancy.Member.LastTenantAdmin` al desactivar a una Dueña de acá que es la única Dueña de otra organización; verde con `ILastTenantAdminGuard.CheckAnotherOwnerAsync` (decisión 23).
- [x] **Empresas:** hecho en `a76545e`. `CompanyService` y `CompanyMembershipService` abren la transacción; `CompanyServiceTests` 10/10 (decisión 25).
  - `CompanyCreator` y `CompanyMembershipWriter` dejan de recibir `IUnitOfWork`;
  - `CompanyMembershipRoleManager` pierde los métodos que solo reenvían;
  - el control «la empresa existe y está activa» queda una sola vez, adentro de la transacción.
- [ ] **Configuración, dominios y página pública:**
  - `OrganizationSettingsService` valida y abre la transacción; `OrganizationSettingsUpdater` se absorbe o deja de recibir `IUnitOfWork`;
  - `OrganizationDomainRegistrar` y `PublicPageEditor` dejan de recibir `IUnitOfWork`;
  - `OrganizationDomainRemover` y `OrganizationDomainVerifier` ya se ordenaron en T10 de la Etapa 5: el verificador consulta el DNS fuera de la transacción, y toma el lock y recarga con versión adentro.
- [ ] **Alta de empresa:**
  - `BusinessSignupService` abre la transacción;
  - `BusinessOrganizationCreator`, `BusinessSignupTransactionRunner` y `BusinessSignupCodeFlow` dejan de recibir `IUnitOfWork`;
  - pasa de 10 clases a 3 o 4, con un solo `OperationLog` por pedido;
  - `TenantProvisioner` se conserva, porque lo usa Plataforma.
- [ ] **Ingreso:** se borra `SignupVerificationFlow`, y quien lo usaba inyecta directo.
- [x] **Invitaciones:** `InvitationAcceptanceCredentials` también solo reenvía; se absorbe. Hecho en `2f6d6ea`: `InvitationAcceptor` inyecta directo lo que reenviaba y queda con 8 dependencias de negocio. El borrado del archivo quedó en `bd0b4f3`, un commit de docs de otra conversación, porque estaba preparado en el índice; en ese commit la solución no compila.
- [ ] **Plataforma:** los helpers de la Etapa 5 que reciben `IUnitOfWork` hacen el mismo cambio.
- [ ] **Listas blancas:** en `TransactionBoundaryTests` quedan solo los dos seeds. Con el mismo criterio se revisa la lista de `TenantScopeUsageTests`.
- [ ] **Hecho cuando** la suite completa del back pasa y la lista blanca no tiene helpers.

### 4. Back: una sola pieza por regla duplicada

- [ ] **Alcance de un rol:**
  - `RoleAssignment.Check` en Domain: una función pura que devuelve el error o nada;
  - un solo guard para Usuarios y Empresas, con un solo código de error;
  - `Assign` recibe un valor simple, no la entidad.

  Tests: la matriz de dominio y la de permisos efectivos (`ListRoleIdsForUserAsync`).
- [ ] **Estado de un miembro:** una sola expresión SQL para filtrar, mostrar y contar (con un `GroupBy`), en Usuarios y en Miembros de empresa. La política de C# queda como especificación, con un test de paridad.
  - Esto arregla **B8**. Test: un invitado con su membresía inactiva aparece en un solo estado y se cuenta una vez.
- [ ] **Búsqueda:** miembros e invitaciones usan `ApplySearch` y `ToPagedResultAsync`, sin SQL crudo ni `EscapeLike` propio. Tests de búsqueda con acentos en `/api/users/paged` y `/members/paged`.
- [ ] **Enmascarado:** `PersonalDataMask` en `Application/Common`, para correo y teléfono, con el formato que muestra el lienzo. La parte de correos ya existe desde T11 de la Etapa 5 (`Common/Text/PersonalDataMask.Email`, «lu•••@dominio»); falta el teléfono y migrar los demás usos.
  - Lo usan el interceptor de auditoría, la cuenta, los avisos y el reader de auditoría.
  - Se borra `IPhoneNumberParser.Mask`.
- [ ] **Auditoría de roles, como el tablero Auditoria-Org.** Cuando cambian los roles de una persona en una empresa, queda un solo cambio, «Roles en la empresa: antes → después». Hoy queda una entrada por cada asignación.
  - Test: asignar y quitar roles en una sola operación deja una entrada, con el antes y el después.
  - Hoy el detalle muestra filas «Empresa» y «un registro» con identificadores internos (lo vio la revisión de capturas del 2026-10-03). Regla: el detalle nunca muestra un identificador interno, y un test lo controla.
- [ ] **Índices únicos:** `UniqueKeys` con sus nombres, usados en `HasDatabaseName` y en cada `catch`. Test: cada clave existe en el modelo.
  - `RoleService` traduce la clave del nombre, y eso arregla **B10**. Test: dos altas simultáneas con el mismo nombre dan una creada y un 409.
- [ ] **Registro:**
  - `AddApplication` se parte en un `*Registration.cs` por área;
  - se borra el registro duplicado de `LastTenantAdminGuard`;
  - las opciones de Application (`LoginCodeOptions`, `InvitationOptions` y `OutboxOptions`) pasan a `AddApplication`.

  Tests: cada clase de `Services` se registra una sola vez, y `ApiFactory` corre con `ValidateOnBuild`.
- [ ] **Sufijos:** la lista cerrada de sufijos de helpers, con los que quedaron después del bloque 3, va en `capas-y-flujo.md` y la verifica `ApplicationServicesTests`.
- [ ] **Hecho cuando** la suite completa del back pasa.

### 5. Front: piezas comunes

- [x] **F1 · El 403 bloquea la vista solo si viene de la consulta principal.** Front `fcde757`.
  - `httpClient` solo transporta, y `QueryCache.onError` publica cuando la consulta lo declara en `meta`.
  - `Legal.AcceptanceRequired` sigue su camino.
  - Test: un 403 en una consulta secundaria no reemplaza la pantalla.
- [x] **F2 · Un mensaje de error común** (front `4d836d5`, con B12): `shared/api/errorMessage.ts` y un `MutationCache` para red, 5xx (con el traceId) y 429. Se borran los cuatro `failureMessage` y los `error.message` crudos.
  - Arregla **B12**. Test: si se corta la red al guardar, aparece «No pudimos conectarnos» con «Reintentar».
- [x] **F3 · `usePagedQuery`** (front `28cd128`, con B6) en las cuatro listas paginadas. Auditoría usa cursor y queda afuera, como pide su ficha: filtros, debounce, consulta, corrección de página y las props de `Pagination`.
  - Arregla **B6**. Test: al borrar el único ítem de la última página, vuelve a la anterior.
- [x] **F4 · `tenancy/access.ts`** (front `1b5272a`) concentra el tipo `Access` del contrato, el inicio y el ingreso de cada acceso, la organización activa y los estados no disponibles. Se suma `useSignOut`.
  - Fuera de ahí no queda ningún literal `"/org"` ni `"/login/empresa"`.
  - Tests de las funciones puras.
- [x] **F5 · `usePermissions`,** con `can`, `canInCompany` y códigos tipados, y un manifiesto de rutas único para `routes.tsx` y el menú (front `5ee936c` y `5fcf5c3`).
  - Tests: `usePermissions.test.ts`, `RequirePermission.test.tsx`, `visible.test.ts` y, en `routes.test.tsx`, «la ruta %s pide el permiso de su manifiesto…».
  - `hasFeature` vive en `usePermissions` y reemplaza a `useFeature`, `<Feature>` y `FeatureRoute`, que nunca existieron. `RequirePermission` responde como un 404 con el módulo apagado, y como «No tenés permiso» sin el permiso. `visibleNavigation` filtra el menú lateral y «Más».
  - `canInCompany` usa `companies[companyId] ?? organization`: con el mapa del back (decisión 13), una empresa que no figura cae en los permisos de la organización, y eso es lo correcto.
- [ ] **B4 · Permisos por empresa de punta a punta.** Hoy un Administrador de empresa no puede usar las pantallas de su empresa (confirmado leyendo el código):
  - `ProfileSnapshotBuilder` manda vacío `effectivePermissions.companies` en `/api/me`;
  - `permissions` trae solo los permisos de la organización;
  - la ficha de Empresa pide `/api/roles/paged`, que exige `roles.read` de organización, y el 403 bloquea toda la pantalla.

  Arreglo:
  - llenar el mapa por empresa en `/api/me`;
  - una ruta de roles asignables de la empresa, bajo `[HasCompanyPermission]`;
  - `canInCompany` en el front.

  Tests:
  - integración con un usuario que solo tiene `CompanyAdmin`: entra a la ficha de su empresa y ve miembros, roles y filtros;
  - E2E real del mismo recorrido.

  Si los Inicios ya llenaron el mapa, solo se verifica. Con el mapa llega también la fila por empresa de Inicio-Miembro: su «Para resolver», con la columna Empresa.

  Back hecho en `2197699` (tanda 2): `/api/me` llena `effectivePermissions.companies` y `GET /api/companies/{companyId}/roles/paged` responde bajo `[HasCompanyPermission(company.members.read)]`; test `CompaniesTests.A_company_admin_without_organization_roles_gets_its_company_permissions_and_assignable_roles` (rojo: el mapa vacío). Ver la decisión 13. **Falta:** `canInCompany` y la ficha en el front, el E2E real y la fila por empresa de «Para resolver» de Inicio-Miembro (back y front), que no entró en esta tanda.
- [x] **F6 · Una sola cultura efectiva** (front `f7f9737`): `resolveCulture` y `useEffectiveCulture`, que usan i18n, formatos, datos de referencia y `Accept-Language`. Se borró `useLanguagePreference`.
  - Tests: `i18n.test.tsx` (`resolveCulture`), `safeStorage.test.ts` y `useFormat.account.test.tsx`.
  - El catálogo se pide dos veces solo si la cultura de la cuenta difiere de la elegida en el navegador; evitarlo obligaría a esperar `/api/me`. Queda así.
- [x] **F7 · `useEditPage`** (guard, «Cambios sin guardar» y 409) en Configuración, Página pública y la ficha de Usuario (front `a6ced5c` a `bcfcc48`). El usuario vio los tableros antes de programar y eligió aplicar a las tres los estados de Configuración (tablero Avisos y criterios).
  - Arregla **B11** (`a2da32b`). Test: `SettingsPage.test.tsx`, editar y tocar el menú muestra «¿Salir sin guardar?».
  - Sin `errors.ts`: ninguna de las tres pantallas traduce un código propio, y la ficha de estructura no deja crear archivos que la feature no necesita.
  - Los avisos que salían dos veces (`25ea5c3`, `5609326`): `/api/me` muestra un solo aviso, y la ficha de Empresa ya no tiene su `ConcurrencyBanner`. El error de cada diálogo, el 409 incluido, va en la franja arriba de los botones.
  - Después de un 409 (`General.ConcurrencyConflict`) en un diálogo, cerrarlo vuelve a leer la ficha si no tiene cambios sin guardar (`5609326`, `3f87201`).
  - Diálogos (`9957b92`, `75bedd7` y `e531b9c`):
    - el fondo, sin difuminar (lienzo v97);
    - la franja de `CompaniesPage`, arriba de los botones;
    - «Sumar empresa», de 420 px.
  - La letra de la franja en la app del teléfono queda en 14,5, como los avisos de M-Avisos (`1dbb394`); falta que Diseño la confirme.
  - Verificación: `npm test` 920/920, lint sin errores y build en verde. Capturas lado a lado en `docs/design/capturas/consolidacion/f7/` (fases 2 y 3), con su manifiesto para la vista en vivo.
- [ ] **F8 · Avisos y diálogos al detalle** (lo que mostraron las capturas de F7):
  - una pieza común de «Pantalla de error», como la de Criterio-Avisos (centrada, con el ícono, «No pudimos abrir esta pantalla» y «Reintentar»), para `LegalAcceptanceGate` y para las fichas que no cargan. Hoy la ficha de Usuario ni siquiera tiene «Reintentar»;
  - la franja (`FormError`) con las medidas del tablero (`.aviso-mal`: padding 10/12, radio 8, ícono 16 y su rojo);
  - el diálogo de roles de Empresa: el encabezado sin bajada (Criterio-Diálogos) y el campo vacío con «Sin roles»;
  - «Sumar empresa» como su tablero, y si el tablero choca con un criterio, manda el criterio;
  - las hojas a todo el alto del teléfono (Empresa y Empresas), con el título arriba y no flotando en el medio;
  - el último administrador de una empresa con un solo texto, el del tablero: «Tiene que quedar al menos un administrador en la empresa.» Hoy hay tres versiones: la del tablero, la de la app y la del back.
- [ ] **Hecho cuando:**
  - `npm run build` y `npm test` completos pasan;
  - las capturas no cambian, salvo lo que arreglan B3 y B4.

### 6. Front: Roles como referencia de verdad

- [ ] **`RoleEditorPage`:**
  - RHF con zod y `errors.ts`;
  - `lib/` con tests, para el selector de permisos y los roles de sistema;
  - los componentes que pide `arbol.md`: `PermissionPicker` y `RoleScopeField`;
  - tests con axe y a 390 px, y «sin coincidencias» en `RolesPage`;
  - lo que mostraron las capturas, para que quede como el tablero:
    - «Crear rol» apagado mientras el formulario esté vacío;
    - «Vale en» sin asterisco, y el editor ya no crea roles con `?scope=AnyCompany` (enmienda de las ADR 0012 y 0034);
    - «Vale en toda la organización» y «Vale en cada empresa», en minúscula (lienzo v96);
    - axe en Roles, Editar rol, Empresas, Ficha de empresa, Registrá tu empresa y Aceptar términos;
    - «Descripción» a todo el ancho;
    - «1 de 2» a la derecha, en su banda;
    - al rehacer el editor, conservar los botones de v87: «Descartar cambios» al editar (vuelve a lo guardado, sin salir) y «Cancelar» al crear. Los cambia «Paquete 0» en el Paquete 4.
- [ ] **`CompanyDetailPage`,** partida según `arbol.md`:
  - `CompanyDialog` (compartido con Empresas), `AddMemberDialog`, `MemberRolesDialog`, `tabs/CompanyMembersTab` y `tabs/CompanyRolesTab`;
  - cada pieza es dueña de su borrador, su mutación y su error;
  - los diálogos se montan solo cuando están abiertos y devuelven el foco al cerrar.
- [ ] **Estados** con `StatusBadge` y `statusTones` en Usuarios, Empresas y Miembros. Cada feature registra sus tonos, tipados por los enums del contrato, con el color del lienzo para cada estado.
- [ ] Los «Copiá de» del front vuelven a apuntar a Roles.
- [ ] **Hecho cuando:**
  - `npm test` completo pasa;
  - las capturas de Roles, Rol, Empresas y Empresa coinciden con sus tableros.

### 7. Puerta

- [ ] `dotnet build` sin advertencias y `dotnet test` completo; `npm run build` y `npm test` completos.
- [ ] E2E real de Playwright, con front y API reales y sin mocks:
  - ingreso;
  - alta de un rol con nombre repetido y su corrección;
  - un Administrador de empresa en la ficha de su empresa, con búsqueda y estado de los miembros;
  - salir de Configuración sin guardar.
- [ ] Capturas de **todas** las pantallas que ya existen de las etapas 1 a 6 contra el lienzo vigente, no solo de las tocadas, en la compu y en el teléfono. Lo pidió el usuario el 2026-10-04: todo hasta la 6 alineado. Cada diferencia se corrige o se le muestra al usuario para que decida.
- [ ] `realinear-docs --detectar` sale 0 en los dos repos.
- [ ] Informe en `docs/reviews/<fecha>-consolidacion.md`:
  - lo hecho;
  - lo descartado, con los hallazgos que no se sostuvieron;
  - las «Decisiones tomadas».

## Decisiones tomadas

Primera tanda del back (bloques 1, 1b y 2, 2026-10-04):

1. **B9:** el diff de una modificación lleva solo las propiedades cuyo valor cambió (con el comparador de EF) y no repite las marcas de quién y cuándo (`CreatedAtUtc`, `CreatedBy`, `ModifiedAtUtc`, `ModifiedBy`), que ya están en la entrada; sin eso, el diff nunca tendría «solo ese campo». El nombre de la organización vive en `platform.Tenants`, que es global y no lo audita el interceptor: la entrada manual que se sacó lo guardaba sin antes y después, y el detalle no lo mostraba. Un cambio solo de nombre deja la entrada de Configuración sin filas.
2. **1.2:** `managedEmailCount` cuenta los métodos de correo verificados de ese dominio con `ManagedByTenantId` de la organización, en la misma consulta de `TenantDomainReader`, que entra en la lista cerrada de `IdentityStorageAccessTests`. Plataforma recibe el mismo dato.
3. **1.3:** `PhoneNumber` se mapea con `PhoneNumberConvention`, como `Email`: sin una convención, EF tomaba el value object como entidad.
4. **Recuperar sin delatar por el tiempo:** el aviso a la cuenta lo encola `AccountRecoveryNoticeWorker` cada 30 segundos con `AccountRecoveryNoticeService`, que toma los pedidos con `AccountNotifiedAtUtc` vacío (`FOR UPDATE SKIP LOCKED`, índice parcial) y los marca, con cuenta o sin ella. Es un servicio aparte para que su categoría de logs quede en Warning, como el outbox, sin silenciar los de «Recuperar mi cuenta». La migración marca como avisados los pedidos anteriores. Marcar cambia el `xmin` del pedido: si un operador lo abrió antes, aprobar da 409 y hay que recargar.
5. **Textos de v89:** si la configuración de la plataforma todavía no tiene correo de soporte (recién instalada), el aviso sale sin nota en vez de fallar.
6. **Aprobar una recuperación:** «Agregaste un correo a tu cuenta» llega a todos los métodos verificados, el nuevo incluido, así que el correo nuevo recibe ese aviso y «Recuperaste tu cuenta». Sale por `PlatformRecoveryNotifier`, para que el servicio de decisión no pase de 8 dependencias.
7. **D3:** `UniqueKeys` nace en `Application/Interfaces/Persistence` con `RoleName`; traducir la violación de ese índice en `RoleService` es B10, del bloque 4. Con el índice del plan, un rol de una empresa puede llamarse igual que uno que vale en cada empresa, y los dos aparecen en esa empresa; el bloque 6 deja de crear roles de «Cada empresa» desde el editor.
8. **D4:** un correo verificado de otra cuenta (un método de correo o el contacto de Google) responde 202 con un `methodId` nuevo y la misma espera; el código se guarda para los límites y no se manda; el aviso al dueño lo encola `AccountNoticeIssuer.EnqueueToOwnerAsync`, que ahora usa también Recuperar. Un correo ya verificado en la misma cuenta responde igual y el aviso le llega a ella (antes era el error; un texto propio para ese caso sería una decisión de producto). El contacto del propio Google se sigue pudiendo sumar como correo (`b05caca`). Volver a agregar un correo propio sin verificar es pedir otro código. **Queda abierto, porque cambia lo que ve la persona:** los pasos siguientes todavía delatan, porque ese `methodId` no existe (verificar y reenviar dan 404) y la lista no muestra el correo pendiente. Cerrarlo pide decidir cómo se guarda un pendiente que no puede chocar con el índice único de `LoginMethods`.
9. **D5:** la regla queda en una sola pieza, `LastTenantAdminGuard` (organizaciones no cerradas y otra Dueña con cuenta y membresía activas); se borran `PlatformSoleOwnerGuard`, `ListActiveTenantIdsForOwnerAsync`, `CountActiveOwnersAsync` y `AccountDeletionPolicy.CheckLastTenantAdmin`.
10. **D6:** el país de la organización es la región de su «Idioma y región» (`TenantSettings.DefaultCulture`), como ya lo usa `PersonalSpaceProvisioner`. El tipo por defecto es el primer tipo habilitado del país que aplica a empresas, por `SortOrder` y después por código. Si el número no cambió, se conserva el tipo guardado. Un país sin tipo para empresas no admite número (`TaxIds.TaxId.Invalid`).
11. **D7:** es miembro quien tiene la membresía activa o deshabilitada; con la membresía quitada (o solo invitada), se muestra solo el nombre.
12. **Salida del Api:** además de los tres renombres, `GoogleSignupAntiforgeryResponse` pasa a `*HttpResponse` porque es de protocolo. Del lado de Application el test acepta cualquier modelo de `Application.Models`: cinco salidas no terminan en `Response` (`LegalDocumentRow`, `PendingDeletionState` y los tres `Platform*FilterCounts`) y renombrarlas cambiaría esquemas que ya usa el front.

Segunda tanda del back (bloques 1b y 5, 2026-10-04):

13. **B4:** el mapa por empresa tiene las mismas empresas que «Tus empresas» del inicio (miembro, membresía y empresa activos), con los permisos de `GetCompanyPermissionsAsync`, que ya suman los de la organización, como `[HasCompanyPermission]`; una empresa sin permisos no aparece. Lo calcula `IPermissionService.GetPermissionsByCompanyAsync`, para que `ProfileSnapshotBuilder` no pase de 8 dependencias. La ruta es `GET /api/companies/{companyId}/roles/paged` (`CompanyRolesController`, etiqueta «Miembros de empresa»), paginada y con búsqueda como manda `paginado-y-busqueda.md`; devuelve `RoleResponse` con el filtro «Vale en» de `RoleReader` (los de cada empresa y los propios) y 404 si la empresa no existe.
14. **D4b:** el índice único `(Type, Value)` de `LoginMethods` vale solo para los verificados y un pendiente es único en su cuenta (`(UserId, Type, Value)`); `FindMethodAsync` devuelve el verificado primero. Un correo de otra cuenta deja un pendiente real; un correo ya verificado en la misma cuenta no se repite (su `methodId` sigue sin existir, y es el correo propio, así que no delata a nadie). Reenviar un pendiente de otra cuenta emite otro código sin mandarlo y le vuelve a avisar al dueño, como cada pedido de «Recuperar mi cuenta». Si el destino pasa a ser de otra cuenta con un código ya mandado, verificarlo falla como un código incorrecto (`usable`), en vez de chocar con el índice. Agregar un correo libre sigue retirando el pendiente de otra cuenta, como antes. Los tests de D4b se escribieron antes del código, pero su rojo no quedó registrado (el coordinador recortó la tanda por consumo).
15. **Recuperación:** reemplaza la decisión 6: «Agregaste un correo a tu cuenta» va a los demás métodos verificados; el correo nuevo recibe solo «Recuperaste tu cuenta». Sin otro método verificado, queda el `LoginMethodChanged` y no sale ese aviso.
16. **T16b, dónde vive la invitación:** el vencimiento está en la cuenta (`AspNetUsers.OperatorInvitationExpiresAtUtc`, migración `OperatorInvitationExpiry`), sin tabla aparte, porque Cuentas lo muestra como un estado de la cuenta. Pendiente es una cuenta Active con vencimiento; aceptar lo vacía; revocar deja la identidad Deleted y anónima y conserva el vencimiento como marca, para que no figure en Cuentas. «La borra» no es un DELETE: el rol `mt_app` no puede borrar identidades y los ingresos (`LoginAudits`) solo admiten INSERT.
17. **T16b, «a Plataforma»:** el primer ingreso con código acepta la invitación solo si el `returnUrl` elige `access=platform`. Por Personal, Empresa o `/invitacion` responde 403 `Tenancy.Access.PlatformOnly`, sin sesión, y la invitación sigue pendiente. Google no la acepta ni se vincula (`Auth.ExternalLogin.Failed`). Pendiente, no tiene permisos (`PlatformPermissionService`) ni cuenta como administrador de operadores (`ListActiveManagerIdsAsync`).
18. **T16b, vencida no delata:** el pedido de código responde 202 como siempre y no sale el código, como sin cuenta. Un código pedido antes de vencer falla como `Auth.LoginCode.Invalid`.
19. **T16b, solo dos acciones:** con la invitación pendiente, suspender, cerrar sesiones, iniciar la baja y cambiar o quitar permisos responden 409 `Platform.Operator.InvitationPending`. Reenviar o revocar algo que no es una invitación pendiente responde 409 `Platform.Operator.NoPendingInvitation`, y 404 si la cuenta no existe o ya se revocó. Las rutas siguen el comentario de v90: `POST /api/platform/operators/{id}/invitation/resend` con Idempotency-Key, que devuelve el vencimiento nuevo, y `DELETE /api/platform/operators/{id}/invitation` sin cuerpo ni Idempotency-Key, como «Revocar invitación» de Usuarios.
20. **T16b, eventos y plazo:** reenviar y revocar dejan `PlatformOperatorInvitationResent` (diff de `invitation.expiresAtUtc`) y `PlatformOperatorInvitationRevoked` (diff de permisos) con un motivo fijo traducido, porque el lienzo no pide motivo. El plazo es `Authentication:Invitations:LifetimeDays` (7), el mismo de las organizaciones, y se guarda en microsegundos para que la respuesta coincida con Cuentas. En el correo reenviado, quien invita es quien reenvía, como en Usuarios.
21. **T16b, correo:** los permisos van por nombre, unidos con «, » y «y» (`List.And`), como el ejemplo de v90. Sin nombre de quien invita (el operador inicial no tiene), el correo dice «El equipo de {app}»: es un texto nuevo, que v90 no muestra. Si se revoca con el aviso todavía en el outbox, el correo sale igual; el botón ya no sirve, porque la cuenta no existe.
22. **T16b, invitar de nuevo un correo con invitación pendiente o vencida** sigue respondiendo 409 `Platform.Operator.AccountExists` («Ese correo ya tiene una cuenta…»), como en T16; lo esperado es reenviarla. Un texto propio sería una decisión de producto.

Verificación de la tanda 2: `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1560/1560** (7m 39s). T16b no entró en esta tanda: queda para un agente nuevo, con la ADR 0042 y la tarea del plan de la Etapa 5 sin cambios. Después la hizo un agente aparte (decisiones 16 a 22; evidencia en el plan de la Etapa 5).

Segunda tanda del front (bloque 5, 2026-10-04): F5 y F6, más las marcas (E#) de «Lo verifica» en las fichas del front (`33ee1d5`). `npm test` 900/900, lint sin errores y build en verde. Quedan para agentes nuevos:
- F7 con B11, y los avisos que salen dos veces (`/api/me` y la ficha de Empresa);
- las filas de v92 (una segunda línea gris con `detail` en la columna) y el espacio entre opciones del menú lateral (hoy 39 px de una a otra; el tablero, 34);
- los contratos regenerados: los `*HttpResponse`, `taxIdType` fuera de `companyPayload` y la ruta nueva de roles de una empresa;
- las cuatro líneas de docs de v93 (los inicios, los avisos en el teléfono y el sitio público sin sesión);
- en Mi cuenta, el ⋮ de «Cómo entrás» solo en las filas con acciones (lienzo v96): no lo lleva el principal si es el único método personal; lo llevan el correo de la empresa («Hacer principal» y «Quitar») y el teléfono sin verificar («Quitar»);
- B4 en el front, con `canInCompany`: la ruta de la ficha de Empresa tiene que pedir un permiso de empresa, y `RequirePermission` hoy sabe solo de permisos de organización. Se resuelve en el manifiesto: la entrada declara su permiso de empresa y `RequirePermission` lo mira con el `companyId` de la ruta;
- `/org/roles/new`: `frontend.md` §3 dice `/nuevo`, y el código se alinea al doc en el bloque 6.

**Hallazgos sin corregir:**
- `bd0b4f3` (docs del plan) arrastró un `git rm` que otro agente tenía preparado en el índice, así que en ese commit la solución no compila; vuelve a compilar desde `2f6d6ea`. No se reescribe la historia. Desde ahí, cada commit lleva su pathspec (`git commit -- <rutas>`).
- ~~`OrganizationUserAdministratorGuard` protege a la última Dueña con `ILastTenantAdminGuard.CheckCanLeaveAsync`, que mira todas las organizaciones de la cuenta: quitar el rol de Dueña en una organización falla si esa cuenta es la única Dueña de otra. Viene de antes; con D5 también cuentan las suspendidas. Va con Usuarios en el bloque 3.~~ Resuelto en `16dee4e` (decisión 23).
- ~~`HarnessTests.Closed_stage_verification_tests_and_tools_exist` falla por docs del front sin `(E#)`.~~ Resuelto con las marcas del front (`33ee1d5`): la suite completa de la tanda 2 lo pasó.

Bloque 3 del back, primera parte (Usuarios, Empresas e Invitaciones, 2026-10-04):

23. **La última Dueña, por organización:** quitarle a una Dueña el rol, la membresía o el acceso en Usuarios mira solo esa organización. Lo hace `ILastTenantAdminGuard.CheckAnotherOwnerAsync(tenantId, userId)`: toma el lock de las asignaciones de esa organización, cuenta las otras Dueñas activas y responde `Tenancy.Member.LastTenantAdmin`. La organización sale de la membresía (`Member.TenantId`), que ya se cargó con el filtro de la organización actual. La regla sigue en una sola pieza (D5): la baja de la cuenta sigue mirando todas con `CheckCanLeaveAsync`. `docs/features/roles.md` lo dice.
24. **Usuarios no se parte:** con el conteo de la ADR 0039, `OrganizationUserService` queda con 7 dependencias de negocio, así que no se parte en invitaciones y miembros (la revisión lo proponía con el conteo viejo). El editor, el de estado, el que quita y el de invitaciones siguen como helpers, sin `IUnitOfWork`. Invitar recibe del servicio el `inviterId` que ya controló; el mismo control repetido adentro se borró. El que quita inyecta directo `OrganizationUserAccessCleaner` y `OrganizationMembershipEndNotifier`, como ya lo hacía el de estado.
25. **Empresas, el orden de los errores:** el control «la empresa existe y está activa» queda en `CompanyMembershipWriter.LockActiveCompanyAsync`, después del lock del último Administrador. La validación de roles (`ValidateRoleIdsAsync`) pasó adentro, después de ese control, para que los errores salgan en el mismo orden que antes: empresa, roles y miembro. Un pedido que falla por la empresa o los roles ahora abre la transacción y la descarta; antes cortaba afuera. Sin los reenvíos de `CompanyMembershipRoleManager`, el writer inyecta `IRoleRepository` e `IRoleAssignmentRepository` y queda con 8 dependencias de negocio.
26. **Lista blanca:** en `TransactionBoundaryTests` quedan `BusinessOrganizationCreator`, `BusinessSignupCodeFlow`, `BusinessSignupTransactionRunner`, `OrganizationDomainRegistrar`, `OrganizationSettingsUpdater` y `PublicPageEditor`, que son de los otros ítems del bloque 3.

Verificación: `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1566/1566** (8m 40s). Durante el trabajo: arquitectura 142/142 (`TransactionBoundaryTests` y `ServiceDependencyCountTests` incluidos) y la integración de Usuarios, Empresas, Invitaciones, Roles, Inicio y Auditoría en verde.
