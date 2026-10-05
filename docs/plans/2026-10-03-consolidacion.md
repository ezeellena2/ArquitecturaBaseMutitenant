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
- [x] El ADR 0039 está aplicado en `ServiceDependencyCountTests` (se le pidió a la Etapa 5). Si no lo está, aplicarlo primero: ya estaba, verificado el 2026-10-04. El test tiene sus casos de control (`InfrastructureOnly`, `EightBusinessDependencies` y `NineBusinessDependencies`), y la 0039 está en `capas-y-flujo.md`, `guardado.md` y `backend.md` §4.
  - el test con su caso de control;
  - `capas-y-flujo.md`, `guardado.md` y `backend.md` §4.
- [ ] Línea de base: `realinear-docs --detectar` en los dos repos. El 2026-10-03, el back dio 372 hallazgos automáticos, 193 de modelo y 217 preguntas. **Back, 2026-10-04 (antes de empezar):** 293 automáticos, 129 de modelo y 157 preguntas (579). Falta el front.
- [ ] Aplicar lo automático con `realinear-docs --sin-modelo` en los dos repos. **Back hecho** en `0fbdde4`: las 293 líneas de `arbol.md`, revisadas; las que decían algo falso o nada se corrigieron a mano en `17686e9`. Falta el front.
- [ ] Corregir los docs viejos (📄) y aplicar las enmiendas de la [revisión de decisiones](../reviews/2026-10-03-revision-de-decisiones.md). La lista está en su «Reparto» y el detalle con archivo y línea, en sus anexos. Incluye los docs que dicen que el CI ya compara los contratos (ADR 0016) y `backend.md:53` (ADR 0017). **Back hecho:** `arbol.md` (`17686e9`), los punteros (`a5c4512`), las features (`2acb1ee`), `backend.md` y `multitenancy.md` (`29684b6`), las fichas (`abe9f58`), el nombre de la página pública (`3ee854a`) y los planes (`c27c2d7` y `57b6866`). Las enmiendas de las ADR 0002, 0012, 0016, 0017, 0034, 0038 y 0042 ya estaban en el índice. Falta el front: `frontend.md`, `responsive.md`, `formularios.md`, su `arbol.md` y su `AGENTS.md:33` (ADR 0016).
- [x] Borrar las cuatro carpetas sobrantes de `tools/` (`LocalSetup`, `PlatformBootstrap`, `PlatformOperatorAdmin` y `Setup`), que solo tienen `bin` y `obj` y no están versionadas. Hecho el 2026-10-04, después de verificar que las cuatro tenían solo `bin` y `obj` y `git ls-files` vacío; no hay commit, porque no estaban versionadas.
- [ ] Resolver los hallazgos de modelo y las preguntas, comparando con el código:
  - `arbol.md` de los dos repos, llevado al árbol real;
  - los códigos de error, permisos y rutas, cada uno en su ficha de `docs/features`;
  - la fila de `capas-y-flujo.md` en AGENTS.md.
  - `arbol.md` y `backend.md` ubican el alta de empresa en `Services/Organizations/`, y el código está en `Services/Auth/`: los docs se llevan al código.
  - `Persistence/Interceptors/AGENTS.md` no nombra `AccessChangeTrail` ni `RoleAssignmentTrail`.
  - los `AGENTS.md` nombran los sufijos viejos: la raíz y `Services/AGENTS.md` listan cinco (`*Policy`, `*Guard`, `*Issuer`, `*Verifier`, `*Linker`). Pasan a mandar a la lista cerrada de 22 de `capas-y-flujo.md` (decisión 58). `Services/Auth/AGENTS.md` todavía dice `UserCultures`, que ahora es `UserCultureResolver`.

  Al usuario va solo lo que cambia el producto. **Back hecho:** los cinco puntos de arriba, con los commits del ítem anterior; nada cambia el producto. Además: `arnes.md` §3 suma 15 filas para las 24 carpetas que tenían puntero y no estaban en el mapa, y el detector de catálogos reconoce los permisos con dos puntos (`42bdfe9`; decisiones 80 y 81).
- [x] Sumar la consolidación al plan maestro como paso previo de la Etapa 7. Se hizo al aprobar el plan.
- [ ] **Hecho cuando** `--detectar` sale 0 en los dos repos. **Back: 0** (automáticos 0, de modelo 0 y preguntas 0, el 2026-10-04 después de `57b6866`), con cinco descartes anotados con su motivo en `ignorados.json` (`6606b5e`, decisión 82). Falta el front.

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
- [x] **El correo del perfil activo en tu menú** (el pie en la compu, «Más» en el teléfono):
  - en una organización, el correo que administra esa organización y, si no hay, el principal;
  - en el perfil personal, el principal.
  - Back: `/api/me` devuelve el correo del perfil activo, con su test. Front: el menú lo muestra.
  - **Hecho** en `668c564` (decisión 59): `email` de `/api/me` es el del perfil activo. Los tres casos, en `ProfileServiceTests.Me_email_is_the_one_of_the_active_profile`. El front ya muestra ese campo en el pie (`PrivateLayoutFrame`) y en «Más» (`useProfiles`): no cambia.
- [x] **Invitar un correo con una invitación de operador pendiente o vencida.** Hoy da «Ese correo ya tiene una cuenta…» (decisión 22). Pasa a un error propio, 409 con «Ese correo ya tiene una invitación. Reenviala desde la lista.», en es y en en, con su test. Las pantallas de Cuentas lo usan en la Etapa 5.
  - **Hecho** en `afb7dde` (decisión 60): 409 `Platform.Operator.InvitationExists`, con `PlatformOperatorTests.Inviting_an_email_with_a_pending_or_expired_invitation_asks_to_resend_it`.
- [x] **El correo de un operador invitado por alguien sin nombre:** se verifica que el asunto y el texto sean los de Mensajes › «Invitación como operador: sin nombre»: «El equipo de ArquitecturaBase te invitó como operador» y «Hola. El equipo de ArquitecturaBase te invitó a sumarte como operador de la plataforma.».
  - **No coincidían:** el asunto terminaba «… como operador de ArquitecturaBase» y el texto repetía la app. **Corregido** en `d419de1` y `9c54bd3` (decisión 61), con `EmailTemplateTests.Operator_invitation_matches_the_board_with_and_without_the_inviter_name`.
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
- [x] **Configuración, dominios y página pública:** hecho en `4d4f479` (la transacción) y `709b9e2` (el arreglo). La integración de Configuración, Dominios, Página pública, su moderación y Dominios de Plataforma da 8/8, y arquitectura 142/142 (decisiones 27 y 28).
  - `OrganizationSettingsService` valida y abre la transacción; `OrganizationSettingsUpdater` se absorbe o deja de recibir `IUnitOfWork`. Se absorbió y se borró;
  - `OrganizationDomainRegistrar` y `PublicPageEditor` dejan de recibir `IUnitOfWork`. `OrganizationDomainService` y `PublicPageService` abren la transacción;
  - `OrganizationDomainRemover` y `OrganizationDomainVerifier` ya se ordenaron en T10 de la Etapa 5: el verificador consulta el DNS fuera de la transacción, y toma el lock y recarga con versión adentro;
  - arreglar la Página pública (lo encontró el front): guardar con una versión vieja respondía 409 `PublicSite.PublicPage.VersionConflict`, y la regla pide `General.ConcurrencyConflict`, que es el que hace salir «Ver lo nuevo». Test `PublicPagesTests.Saving_with_a_stale_version_answers_the_common_concurrency_conflict`: rojo con `PublicSite.PublicPage.VersionConflict` y verde con el código común. El código propio y sus textos se borraron.
- [x] **Alta de empresa:** hecho en `2d4b31d`. La integración del registro de empresa da 4/4, con un test nuevo que llama al servicio sin la respuesta guardada por `Idempotency-Key`: `BusinessSignupTests.A_creation_that_reaches_the_service_checks_the_repetition_the_limit_and_the_slug` (repetición con otros datos, límite de organizaciones y slug ocupado; verde antes y después de mover el código). Decisiones 31 a 33 y 36.
  - `BusinessSignupService` abre la transacción en sus tres métodos;
  - `BusinessOrganizationCreator` deja de recibir `IUnitOfWork`; `BusinessSignupTransactionRunner` y `BusinessSignupCodeFlow` se absorbieron y se borraron;
  - quedan 3 clases propias (`BusinessSignupService`, `BusinessSignupPolicy` y `BusinessOrganizationCreator`) más `TenantProvisioner`, con un solo `OperationLog` por pedido. Se borraron además `BusinessSignupPolicyReader`, `BusinessSignupOrganizationProvisioner`, `BusinessSignupLegalAcceptor` y `BusinessSignupRequestResolver`;
  - `TenantProvisioner` se conserva, porque lo usa Plataforma, sin `OperationLog` propio.
- [x] **Ingreso:** se borra `SignupVerificationFlow`, y quien lo usaba inyecta directo. Hecho en `fe6fe7d`: `AccountService` inyecta `IPersonalSpaceProvisioner` y `SignupVerificationCore`, y queda con 5 dependencias de negocio. La integración de Registro, Ingreso y Legales da 18/18.
- [x] **Textos de la última Dueña y del último administrador de una empresa** (pedido junto con este bloque): hecho en `95d853f`. El del administrador de una empresa pasa al texto del tablero; el de la Dueña queda como está (decisión 29). Test: `CompaniesTests` compara el detalle, rojo con el texto viejo y verde con el nuevo.
- [x] **Invitaciones:** `InvitationAcceptanceCredentials` también solo reenvía; se absorbe. Hecho en `2f6d6ea`: `InvitationAcceptor` inyecta directo lo que reenviaba y queda con 8 dependencias de negocio. El borrado del archivo quedó en `bd0b4f3`, un commit de docs de otra conversación, porque estaba preparado en el índice; en ese commit la solución no compila.
- [x] **Plataforma:** los helpers de la Etapa 5 que reciben `IUnitOfWork` hacen el mismo cambio. Hecho en `a65e019`: ningún helper de Plataforma recibe `IUnitOfWork`; las 12 clases que lo reciben son servicios de entrada que abren su transacción. El único reenvío, `PlatformTenantProvisioning`, se borró, y `TenantAdministrationService` queda con 7 dependencias de negocio. La integración de Plataforma (alta, aprobación, administración, alcance, contadores y avisos) da 23/23 (decisión 34).
- [x] **Listas blancas:** en `TransactionBoundaryTests` quedan solo los dos seeds. Con el mismo criterio se revisa la lista de `TenantScopeUsageTests`. Hecho en `0f565d5`: `TransactionBoundaryTests` ya no tiene lista de helpers ni excepciones. `TenantScopeUsageTests` compara su lista con las clases que entran de verdad, así que una que ya no entra rompe el test; sale el runner del alta y entra `BusinessSignupService` (decisión 35). Arquitectura 142/142.
- [x] **Hecho cuando** la suite completa del back pasa y la lista blanca no tiene helpers. Cumplido en `0f565d5`: `dotnet build` de la solución con 0 advertencias, `dotnet test` completo con Docker **1568/1568**, y `TransactionBoundaryTests` sin helpers.

### 4. Back: una sola pieza por regla duplicada

- [x] **Alcance de un rol:** hecho en `e59149d` (decisiones 37 y 38).
  - `RoleAssignment.Check` en Domain: una función pura que devuelve el error o nada;
  - un solo guard para Usuarios y Empresas, con un solo código de error;
  - `Assign` recibe un valor simple, no la entidad.

  Tests: la matriz de dominio y la de permisos efectivos (`ListRoleIdsForUserAsync`). Hechos: `RoleAssignmentTests` 17/17 (la matriz de `Check` y la de `Assign`) y `RolePersistenceTests.Effective_roles_count_each_assignment_only_where_its_role_is_valid` (la organización, una empresa, la membresía inactiva, la empresa inactiva y dos asignaciones que dejaron de valer). Además, `CompaniesTests.Users_and_companies_refuse_a_role_outside_its_scope_with_the_same_code`: con el código anterior, Empresas respondía `Roles.Role.CompanyNotAllowed` donde Usuarios respondía `Roles.RoleAssignment.CompanyNotAllowed`. Se borró `CompanyMembershipRoleManager`.
- [x] **Estado de un miembro:** una sola expresión SQL para filtrar, mostrar y contar (con un `GroupBy`), en Usuarios y en Miembros de empresa. La política de C# queda como especificación, con un test de paridad. Hecho en `d55a19c`: `WithState` en cada reader y `MemberStateParityTests` 2/2 contra `OrganizationUserStatePolicy` y `CompanyMemberStatePolicy` (decisiones 39 y 40).
  - Esto arregla **B8**. Test: un invitado con su membresía inactiva aparece en un solo estado y se cuenta una vez. `CompaniesTests.An_invited_member_with_an_inactive_membership_has_one_state_and_counts_once`: rojo con el invitado también en el filtro Inactivo («Filter matched in collection»), verde con la expresión única.
- [x] **Búsqueda:** miembros e invitaciones usan `ApplySearch` y `ToPagedResultAsync`, sin SQL crudo ni `EscapeLike` propio. Tests de búsqueda con acentos en `/api/users/paged` y `/members/paged`. Hecho en `974b03c`, con `MemberSearch` (decisión 41). `OrganizationUsersTests.Searching_users_ignores_accents_and_finds_pending_invitations` y `CompaniesTests.Searching_company_members_ignores_accents_and_case`: rojos con «perez» sin encontrar a «Lucía Pérez», verdes con `ApplySearch`.
- [x] **Enmascarado:** `PersonalDataMask` en `Application/Common`, para correo y teléfono, con el formato que muestra el lienzo. La parte de correos ya existe desde T11 de la Etapa 5 (`Common/Text/PersonalDataMask.Email`, «lu•••@dominio»); falta el teléfono y migrar los demás usos.
  - Lo usan el interceptor de auditoría, la cuenta, los avisos y el reader de auditoría.
  - Se borra `IPhoneNumberParser.Mask`.

  Hecho en `c64b786` (decisiones 53 a 55). `PersonalDataMask` suma `Phone` («•••• 4521», los últimos cuatro dígitos) y `LoginMethod`, que elige por el tipo del método. Lo usan Plataforma (Cuentas y Recuperaciones), Mi cuenta (el destino de la reautenticación y el respaldo), los avisos de métodos, el interceptor, el servicio y el reader de auditoría; se borraron las cinco copias y `IPhoneNumberParser.Mask`. `PersonalDataMaskTests` 26/26 (correo, teléfono, lo ya enmascarado y casos borde): rojo sin `Phone` ni `LoginMethod` (no compilaba), verde con la pieza. `OrganizationAuditTests` controla el formato nuevo en el detalle.
- [x] **Auditoría de roles, como el tablero Auditoria-Org.** Cuando cambian los roles de una persona en una empresa, queda un solo cambio, «Roles en la empresa: antes → después». Hoy queda una entrada por cada asignación.
  - Test: asignar y quitar roles en una sola operación deja una entrada, con el antes y el después.
  - Hoy el detalle muestra filas «Empresa» y «un registro» con identificadores internos (lo vio la revisión de capturas del 2026-10-03). Regla: el detalle nunca muestra un identificador interno, y un test lo controla.

  Hecho en `4812ef8` (decisiones 46 y 47): `RoleAssignmentTrail` deja una entrada por persona y lugar, con los nombres antes y después, y `OrganizationAuditService` no muestra identificadores. `OrganizationAuditTests.Changing_the_roles_of_a_person_in_a_company_leaves_one_entry_with_the_roles_before_and_after`: rojo con tres entradas (la membresía vacía y una por asignación), verde con una, «Solo lectura, Ventas → Compras, Solo lectura». `The_audit_detail_never_shows_an_internal_identifier`: rojo con ids en el detalle. `Audit_returns_affected_person_and_company_for_role_changes` controla además la fila del tablero, «Roles en la empresa: Sin roles → Solo lectura».
- [x] **Índices únicos:** `UniqueKeys` con sus nombres, usados en `HasDatabaseName` y en cada `catch`. Test: cada clave existe en el modelo.
  - Suma la Página pública guardada dos veces a la vez por primera vez: hoy la segunda recibe 500 al chocar con «una página por organización». Pasa a 409 `General.ConcurrencyConflict`, como en `PlatformFeatureService`, con su test (decisión 28).
  - `RoleService` traduce la clave del nombre, y eso arregla **B10**. Test: dos altas simultáneas con el mismo nombre dan una creada y un 409.

  Hecho en `e2ecafc` (decisiones 43 a 45), con el índice trigram de `tenant.Invitations.Destination` que había quedado abierto en la decisión 41. `UniqueKeyRaceTests` retiene a los dos pedidos hasta que los dos pasaron el control previo: rojos con `UniqueConstraintViolationException` (500) en el nombre del rol y en la página, verdes con una creada y un 409. `DbContextModelTests.Every_unique_key_names_a_unique_index_of_the_model` (rojo con una sola clave) y `UniqueKeysTests` (rojo con seis nombres escritos a mano en Application).
- [x] **Registro:**
  - `AddApplication` se parte en un `*Registration.cs` por área;
  - se borra el registro duplicado de `LastTenantAdminGuard`;
  - las opciones de Application (`LoginCodeOptions`, `InvitationOptions` y `OutboxOptions`) pasan a `AddApplication`.

  Tests: cada clase de `Services` se registra una sola vez, y `ApiFactory` corre con `ValidateOnBuild`.

  Hecho en `fa51bb7` (decisiones 56 y 57): 15 registros, uno por área de `Services`, y las opciones en el registro de su área. `DependencyInjectionTests`: rojo con «LastTenantAdminGuard (2)» y sin las tres opciones, verde 4/4. `ServiceProviderValidationTests`: rojo con «No exception was thrown», verde.
- [x] **Sufijos:** la lista cerrada de sufijos de helpers, con los que quedaron después del bloque 3, va en `capas-y-flujo.md` y la verifica `ApplicationServicesTests`. Hecho en `df62c6e` (decisión 58): una tabla de 22 sufijos con lo que hace cada uno, y 23 helpers renombrados. `ApplicationServicesTests` lee la tabla: rojo con 23 helpers fuera de la lista, verde 5/5 (cada helper tiene un sufijo de la tabla y cada sufijo se usa).
- [x] **Personas asignadas a un rol** (decisión 42, la eligió el usuario): `CountUsersAsync` cuenta todas las asignaciones, activas, invitadas y desactivadas.
  - Mientras alguien tenga el rol, no se puede cambiar dónde vale.
  - Borrarlo responde un 409 claro, en lugar de chocar con la clave foránea.
  - Tests con un rol asignado solo a un invitado y solo a un desactivado: cambiar dónde vale y borrarlo.

  Hecho en `e8d0d94` (decisión 48). `RolesTests.A_role_held_only_by_an_invited_person_counts_as_assigned` y `A_role_held_only_by_a_deactivated_person_counts_as_assigned`: rojos con `userCount` 0, verdes con 1, 409 `Roles.Role.ScopeChangeHasUsers` al cambiar dónde vale y 409 `Roles.Role.HasUsers` al borrarlo.
- [x] **Repetir el alta de una empresa** (decisión 36): con el mismo `operationId` y sin la respuesta guardada (la clave venció o hubo un corte), respondía 409 en lugar de devolver la organización. La regla de idempotencia pide devolverla. Hecho en `d1e2fb3` (decisión 50): la repetición lee la empresa con su versión, con `ICompanyRepository.GetByIdAsync(id, ct)`. `BusinessSignupTests.Repeating_the_same_creation_without_the_saved_response_returns_the_organization`: rojo con `ConcurrencyConflictException` (409 `General.ConcurrencyConflict`), verde con la misma respuesta.
- [x] **Invitaciones** (decisión 35): la entrada al alcance de la organización pasa a `InvitationService`; se borra el reenvío de `InvitationAcceptanceResolver.Enter` y `TenantScopeUsageTests` queda sin esos dos helpers. Hecho en `6536523` (decisión 51). La conducta no cambia: la integración de Invitaciones da 34/34, Usuarios y el alta de Plataforma 16/16, `InvitationServiceTests` 16/16 y arquitectura 143/143.
- [x] **Asignar un rol mientras otro lo borra** (pendiente de la decisión 48): uno de los dos recibía 500. Hecho en `b5b0c9f` (decisión 49): la clave foránea se traduce como el índice único, con el código de cada lado. `ForeignKeyRaceTests`: rojos con `DbUpdateException` (23001 al borrar y 23503 al asignar), verdes con 409 `Roles.Role.HasUsers` y 404 `Roles.Role.NotFound`.
- [x] **Los índices de búsqueda que faltaban** (pendiente de la decisión 45): Roles (`Name` y `Description`), Empresas (`Name` y `TaxNumber`), `SecurityEvents.Reason`, `LoginMethods.ContactEmail` y `AccountRecoveryRequests` (`PreviousMethodValue` y `NewMethodValue`). Hecho en `fdca58f` (decisión 52), con la migración `SearchColumnTrigramIndexes`. `SearchIndexTests`, nuevo: rojo con las ocho columnas sin índice, verde con la migración.
- [x] **Hecho cuando** la suite completa del back pasa. Cumplido en `df62c6e`: `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1627/1627** (7m 45s).

### 4f. Back: la auditoría de la organización, como su tablero

Las diferencias con el tablero Auditoria-Org (lienzo v104) que no eran de la tanda anterior (decisiones 59 a 65).

- [x] **«Venció la invitación a …»:** el tablero la muestra, por el Sistema, y ningún proceso vencía las invitaciones. Hecho en `d7ecb12` (decisión 66): `InvitationExpiryWorker` la registra una sola vez, con la fecha en que venció. Tests `InvitationTests.Its_expiry_is_recorded_once_and_only_if_it_expired_while_pending` y `OrganizationAuditTests.An_expired_invitation_is_recorded_once_by_the_system_on_its_expiry_date`: rojos sin compilar (faltaban `RecordExpiry` y el worker), verdes.
- [x] **Resúmenes más finos:** «Cambió la zona de …» y «Cambió el idioma y la región predeterminados», los dos de un solo campo que dibuja el tablero. Hecho en `691c740` (decisión 67). Test `OrganizationAuditTests.Each_detail_row_shows_its_field_by_name_and_its_value_in_the_viewer_language`: rojo sin «Cambió el idioma y la región predeterminados»; suma un guardado de dos campos, que sigue con el texto general.
- [x] **«La plataforma aprobó la organización»:** sale del `SecurityEvent` de la aprobación, sin el operador, el motivo ni la IP. Hecho en `d64d50e` (decisión 68). Test `OrganizationAuditTests.The_platform_approval_appears_without_who_approved_it_or_why`: rojo sin ninguna entrada de la aprobación.
- [x] **Una sola entrada al quitar a alguien de una empresa y al revocar una invitación.** Hecho en `e5bae2b` (decisión 69). Tests `OrganizationAuditTests.Removing_a_person_from_a_company_leaves_one_entry_with_the_roles_they_had_there` (rojo con dos entradas) y `Revoking_an_invitation_leaves_one_entry` (rojo con cuatro).
- [x] **Una persona sin nombre ni invitación** aparecía como «Creó una membresía de empresa». Hecho en `a7ed218` (decisión 70): «Agregó a ac•••@…». Test `OrganizationAuditTests.Someone_without_a_name_or_an_invitation_appears_with_their_masked_email`, rojo con «Creó una membresía de empresa».
- [x] **El idioma en su propio idioma** («English (United States)»), como el selector de idioma y región. Hecho en `d02bf34` (decisión 71). Test `Each_detail_row_…`: rojo con «Inglés (Estados Unidos)».
- [x] **Palabras sin escribir ni dibujar:** el tablero no tiene otra palabra para «Invitación aceptada», «Invitación revocada» ni «Habilitó a». Quedan, y van a Diseño con las demás (decisión 72).
- [x] **Hecho cuando** la suite completa del back pasa. Cumplido en `d64d50e`: `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1643/1644** (11m 27s). La única que falla es `HarnessTests.Closed_stage_verification_tests_and_tools_exist`, por ocho tests nombrados en los docs del front sin su (E#) (`accesibilidad.md`, `errores.md`, `formularios.md` y `tests.md` del front): no es del back.

### 4g. Back: la auditoría de la organización, una entrada por acción y «Quién»

Lo que faltaba del tablero Auditoria-Org (lienzo v106) después de la tanda 4f (decisiones 73 a 77).

- [x] **Crear una empresa, una entrada:** dejaba «Creó la empresa …» y, aparte, «Agregó a» quien la creó. Hecho en `9b6c02f` (decisión 73): queda «Creó la empresa …» con solo «Nombre», como el tablero. Test `OrganizationAuditTests.Creating_a_company_leaves_one_entry_with_its_name_like_the_board`: rojo con dos entradas. `Someone_without_a_name_or_an_invitation_appears_with_their_masked_email` usaba ese «Agregó a»: ahora agrega a una empresa a otra cuenta sin nombre ni invitación.
- [x] **Quitar a alguien de la organización, una entrada:** dejaba «Quitó a …», sus roles de la organización y una entrada por empresa. Hecho en `20f9fa5` (decisiones 74 y 75): queda «Quitó a …» con los roles que tenía. Test `Removing_a_person_from_the_organization_leaves_one_entry_with_the_roles_they_had`: rojo con tres entradas, y después con las filas en otro orden.
- [x] **«Quién» con «Sistema» y «Plataforma»:** la lista ya los traía, sin identificador, pero el listado no filtraba por ellos. Hecho en `d52f518` (decisiones 76 y 77): el filtro `actorKind` y el orden del tablero; `openapi.json` regenerado. Test `Who_offers_the_system_and_the_platform_without_internal_ids_and_filters_by_them`: rojo por el orden («Plataforma» antes que «Sistema»); el filtro no existía.
- [x] **Hecho cuando** la suite completa del back pasa. Cumplido en `d52f518`: `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1647/1647** (9 minutos), `HarnessTests` incluido.

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
  - el último administrador de una empresa con un solo texto, el del tablero: «Tiene que quedar al menos un administrador en la empresa.». El back ya lo tiene (`95d853f`); la app tiene que usar ese;
  - la última Dueña según el caso, con un `errors.ts` en la ficha de Usuario para `Tenancy.Member.LastTenantAdmin`. El back no lo cambia, porque el mismo código sale por varios caminos (decisión 29):
    - si se saca el rol ella misma: «Tiene que quedar al menos un Dueño. Dale el rol a otra persona antes de quitártelo.»;
    - si se va ella misma de la organización: «…antes de salir de la organización.», como el tablero;
    - si se lo hace otra persona: el texto del back, «Tiene que quedar al menos un Dueño activo.»;
  - `scripts/capturar-f7.mjs` y su manifiesto simulan para la Página pública el 409 viejo (`PublicSite.PublicPage.VersionConflict`); pasan a `General.ConcurrencyConflict`, como en Configuración.
- [ ] **Hecho cuando:**
  - `npm run build` y `npm test` completos pasan;
  - las capturas no cambian, salvo lo que arreglan B3 y B4.

### 5b. Front: alineación visual con el relevamiento

El relevamiento del 2026-10-04 (front `fd25c5f`, [relevamiento.md](../../../ArquitecturaBaseMutitenantFront/docs/design/capturas/consolidacion/relevamiento/relevamiento.md)) comparó 111 estados de 17 pantallas con el lienzo v99, a 1440 y a 390: coinciden 35 de 222 capturas.
- El usuario eligió alinear dentro de la consolidación lo que sale de las piezas comunes y lo de cada pantalla, las piezas comunes primero, con la captura lado a lado en cada avance.
- Lo del lienzo va a Diseño.
- Lo de otras etapas queda para su momento: WhatsApp, «Recuperá tu cuenta», Plataforma, «Exportar mis datos» y la portada.

Cada tanda toma las diferencias «Nueva» de `relevamiento.md` que le tocan, las corrige en la pieza común cuando la hay y vuelve a capturar sus casos del manifiesto del relevamiento.

- [x] **R1 · Marco, tipografía y listas** (front `1f028f0` a `eb41575`, con el panel de v102). `npm test` 930/930, lint sin errores y build en verde. Las 222 capturas contra v102 están en `docs/design/capturas/consolidacion/r1/`.
  - Piezas: `menuRow.ts`, `Avatar`, `FilterBar`, `ListRow`, `FormField`, `List` y `ErrorPageFrame`, más los tokens `--lado-t2` y `--lado-guia`.
  - Lo que es del lienzo quedó en Diseño: el marco viejo de ocho tableros, el avatar del teléfono, el ☰ en Organización, el alto del buscador y el interlineado de los rótulos.
  - Lo que pedía:
  - los tamaños y pesos de letra del marco: el nombre de la organización, las migas y tu nombre;
  - el avatar, y el menú de perfiles;
  - el espacio entre opciones del menú lateral (v92);
  - en Personal, el menú y la flecha que el tablero no dibuja;
  - los filtros y el buscador de las listas, y el alto de las filas del teléfono;
  - los tamaños de texto que se desvían de la escala del lienzo, en el ingreso, el registro, la invitación, los términos, los errores, Mi cuenta y la Página pública.
- [x] **R2a · Avisos y errores** (front `efe3437` a `5f5b912`). `npm test` 943/943, lint sin errores y build en verde; 26 pares de capturas en `docs/design/capturas/consolidacion/r2a/`.
  - Piezas:
    - `LoadErrorPage`, la «Pantalla de error» común: la entrada que lee `/api/me`, seis fichas y `AppErrorPage`;
    - `FormError`, con `.aviso-mal` en la compu y v100 en el teléfono;
    - `Banner`, en `--alerta-tx`, que ahora usa también `ConcurrencyBanner`;
    - el aviso pasajero, blanco con sombra;
    - el texto de los diálogos (13,5 y 14,5);
    - `areas/business/users/errors.ts`.
  - Decisiones:
    - el círculo de la pantalla de error es gris con el ícono rojo, como lo dibujan los tableros;
    - la pantalla de error no muestra el detalle, y sale solo si no hay nada que mostrar;
    - `AppErrorPage` dice «Reintentar»;
    - el aviso de sesión ajena de la invitación va en la franja ámbar;
    - el caso «antes de deshabilitarte» no llega, porque el back responde antes `CannotDisableSelf`.
  - Lo del lienzo quedó en Diseño: `.ap-txt-l` en M-Avisos, el aviso viejo de Invitacion, el círculo de Ingreso y la franja `.msj`.
- [x] **R2b · Diálogos, hojas y campos** (front `48b6753` a `c23dd88`). `npm test` 952/952, lint sin errores y build en verde; 34 estados capturados en `docs/design/capturas/consolidacion/r2b/`.
  - Piezas:
    - `dialog.tsx`, con `size="sm"` (420), la cruz solo en la compu, el encabezado del teléfono, las hojas a todo el alto y `DialogBody`;
    - `OtpInput`, con 22 en el ingreso y en el teléfono, y 18 en los diálogos de la compu;
    - `checkbox` (la bloqueada, en gris) y `textarea` (84, sin tirador).
  - «Verificar» ya no se apaga: la franja dice «Escribí los 6 números del código.», como el tablero y Criterio · Botones.
  - Lo que pedía:
  - lo que fijó v100:
    - la franja de error del teléfono: 14,5 con interlineado 1,4, padding 12/14, radio 12 y el ícono arriba si ocupa dos líneas. En la compu sigue en 13, con las medidas de `.aviso-mal`;
    - en Empresa, «Roles de …» y «Agregar miembro» van en el diálogo chico (420) y «Editar empresa», en el de 560;
    - M-Error-Org con «No se pudo abrir», el modelo de la «Pantalla de error» en el teléfono;
    - «Sumar empresa»: «Empresa» obligatoria, con asterisco, y «Roles en la empresa» opcional, que aparece al elegir la empresa;
  - lo que encontró R1:
    - el texto de `Banner` en `--alerta-tx`, y el aviso de la invitación pasado a `Banner`;
    - el texto de los diálogos en 13,5;
    - la hoja de `SearchSelect` en el teléfono (de Paquete 0) usa `Dialog`: si cambian la cruz o el alto de las hojas, cambia también;
  - el aviso «Guardamos…», la franja de error, la «Pantalla de error», el cartel del 409 y los textos de «¿Salir sin guardar?»;
  - las hojas del teléfono (la cruz y el alto completo);
  - los casilleros del código y «Verificar», las casillas bloqueadas y el tirador de los cuadros de texto;
  - el diálogo de baja con su cruz y el «Motivo» en el teléfono.
- **Lo que fijó el lienzo v104** (front `7c60d17`, aprobado por el usuario). Los tableros pasaron a los criterios, y la app ya sigue casi todo. Queda para R2c y R3:
  - sin ☰ en las pantallas con sesión, también en Organización: queda la flecha redonda de 28 en el borde;
  - el avatar con degradé (`--marca` a `--marca-2`, 135°) en todos lados, también en el teléfono;
  - el buscador de las listas, de 36 × 360, con la lupa de 16 a 12 del borde, el texto a 36 y letra de 13;
  - «Dirección de la página» del registro, de 44 con radio 10;
  - los mensajes de ingreso y registro (`.msj`) con triángulo en los de atención y círculo en los de error, en `currentColor`;
  - Invitación «Aceptada»: el cartel sin botón;
  - «Sumar» de «Sumar empresa» nunca apagado: sin empresa, el campo queda en rojo con «Elegí una empresa.» y se limpia al elegir una. Lo mismo vale para «Agregar miembro»;
  - «Todos» a la derecha de «Roles en la empresa» en M-Usuario-Empresa;
  - los datos de las capturas con los CUIT del lienzo, que pasan el dígito verificador: 30-71234567-1, 30-71555444-1 y 30-12345678-1;
  - para las pantallas de Plataforma (Etapa 5): el marco del criterio en sus ocho tableros, y WhatsApp como grupo cerrado de Administración que lleva a Plat-WA-Numero.
- **Lo que fijó el lienzo v106** (front `3a35235`, aprobado por el usuario). Dibuja todos los estados de las etapas 1 a 6 que la app tenía y el lienzo no. Para la app, en R3:
  - M-Inicio-Miembro, en «billetera»: cabecera de color, «Hola, …», la organización y «Tus empresas» en filas;
  - crear y editar empresa en el teléfono, y la hoja de roles de un miembro, son hojas que suben desde abajo, no a pantalla completa. Reemplaza lo que R2b hizo con las hojas a todo el alto;
  - «Ese correo ya lo usa otra cuenta» no va (ADR 0041): agregar un correo de otra cuenta sigue a «Verificar» igual. Se verifica que la app no lo muestre;
  - el error genérico al guardar en M-Usuario va en la franja debajo de la barra, con el texto del back;
  - el relevamiento final captura también los estados nuevos de v106.
- **R3 se parte en cuatro tandas.** R3a con R3d, y R3b con R3c, pueden ir en paralelo porque tocan archivos distintos:
  - [x] **R3a · Marco y listas** (front `2b4aac1` a `c86396b`; 1002/1003 tests, lint y build; falla solo `organization-audit-contract.test.ts`, de R3c). Capturas en `r3a/`, 130 imágenes contra v106.
    - Piezas:
      - `Topbar`, sin ☰;
      - `Avatar`, con los tamaños del teléfono;
      - `FilterBar`, de 36 × 360 y la caja de 58;
      - `DataTable`, con `detail` y `StatusDetail`;
      - `Pagination` y `LoadMore`;
      - `Page`, con las pestañas;
      - la fecha corta en `formatters` y `DateText`;
      - `SectionFields`;
      - `toastErrorWithCode`.
    - El ⋮ de «Cómo entrás» ya coincidía: quedó fijado con su test.
    - Lo del lienzo quedó en Diseño: las pestañas, «Vence…», el pie del paginado, «Para resolver» y la flecha del menú.
    - Lo que pedía:
    - sin ☰ con sesión y el avatar con degradé en el teléfono (v104);
    - el buscador de 36 × 360;
    - la segunda línea de las filas (v92);
    - el ⋮ de «Cómo entrás» (v96);
    - el paginador «1 de 1», en 12, y «Cargar más», en 14;
    - las pestañas de Empresa;
    - la fecha corta y el subtítulo de M-Auditoria-Org (v99);
    - el relleno de `Section` (16);
    - el aviso con «Copiar código» con el botón debajo del texto, como el tablero Avisos;
    - los límites viejos de 10 y 15 s de esas suites, con su comentario o sin el límite.
  - [x] **R3b · Fichas y hojas** (front `1d8e0f1` a `4650372`; 1012/1013 tests, lint y build; falla solo `organization-audit-contract.test.ts`, de R3c). Capturas en `r3b/`.
    - Piezas: `MultiSelect`, con casilla y descripción; el campo abierto con borde y halo en los tres desplegables; `dialog`, sin la hoja a todo el alto; `app/PhoneBand.tsx`, la banda debajo de la barra (sin conexión, el 409 y el error de guardado); y `useEditPage`.
    - M-Inicio-Miembro en «billetera», las hojas de Empresa desde abajo y el nombre y el CUIT revisados al tocar Guardar.
    - Para B4: el «Para resolver» del miembro no puede decir «· Delta Norte S.A.» porque `PendingItem` no trae la empresa. Además, «Reenviar» lleva a `/org/usuarios?state=Expired`, que quizás no puede abrir quien tiene roles solo en su empresa. Va con la fila por empresa de Inicio-Miembro.
    - Las contradicciones del lienzo quedaron en Diseño: el asterisco de «Dominio», la hoja de roles de M-Empresa, «Guardar cambios» sin cambios, los errores de servidor en M-Usuario, el 409 de M-Pagina-Org y el CUIT con guiones.
    - Lo que pedía:
    - las hojas de M-Usuario (v99) y el desplegable de roles de la compu con casillas y descripción;
    - «Todos» a la derecha en M-Usuario-Empresa;
    - el ícono y el asterisco de Configuración, y el panel de la Página pública;
    - M-Inicio-Miembro en «billetera» (v106);
    - las hojas de Empresa desde abajo (v106);
    - la franja de error de M-Usuario (v106);
    - el nombre de la empresa con su mensaje al tocar Guardar y el CUIT revisado al guardar, como pide Criterio · Formularios;
    - «Verificar» del dominio en el teléfono (el rótulo del TXT y «Copiar»).
  - **R3c · Contratos y auditoría:**
    - `npm run contracts`: los `*HttpResponse`, `taxIdType`, los códigos nuevos, la ruta de roles de una empresa y la descripción de `/api/me`;
    - la pantalla de Auditoría con el contrato nuevo, `organization-audit-contract.test.ts` en verde y los datos de las capturas regenerados;
    - el correo del perfil activo en tu menú y «10 correos administrados» en Configuración;
    - las cuatro líneas de docs de v93.
    - **la vista previa de la Página pública** (lienzo v107, Tweak «Borrador: vista previa» de Pagina-Org y M-Pagina-Org):
      - en la compu, el diálogo «Vista previa» de 640, con la tarjeta de Pagina-Publica tal cual sobre su fondo (logo, nombre, descripción, los cuatro datos e «Ingresá para continuar» sin acción) y «Cerrar» primario;
      - en el teléfono, la hoja desde abajo con la tarjeta de M-Pagina-Publica y «Cerrar»;
      - abre desde «Vista previa» de la banda cuando la página no está publicada;
      - la tarjeta es una pieza común: la reusa la Etapa 7 para la página del subdominio (T14 de su plan);
    - la flecha para achicar el menú en el borde del último panel: Administración si está abierta, si no el menú (v107). Se verifica en Personal y en Organización.
  - **R3e · Piezas comunes que faltan** (las encontró R3d):
    - la franja de error en el teléfono, en 14,5 con 12/14 y radio 12, también en las páginas públicas, como dibujan M-Ingreso, M-Registro, M-Registro-Empresa y M-Invitacion (v104). `formularios.md` todavía dice 13 y se corrige;
    - `FormError` con tono de información, para «Tu sesión venció»;
    - `Button` con el estado «Guardando» de Criterio · Botones (aro de 14 y opacidad 0,85), usado en todos los botones que guardan;
    - `FormField` e `Input` en el teléfono: 44 de alto, radio 10 y letra 15, como los M-* (hoy 48, 12 y 16);
    - los íconos de las pantallas de estado que dibuja el tablero: el edificio, el candado y el reloj, en `shared/ui/icons.tsx`;
    - los textos del paso del código, en un solo lugar (hoy están en `auth:login.*` y en `common:code.*`);
    - 6 px entre el texto y el enlace en los renglones de enlaces.
    - se borra `SearchInput.tsx`, que no usa nadie;
    - los límites viejos de tiempo que quedan en `RoleEditorPage` (5), `RolesPage` (2), `UsersPage` (5) y `UserDetailCompanyAssignments` (1): se sacan o llevan su comentario.
  - [x] **R3d · Ingreso y registro** (front `60f2507` a `3cc7f64`; 993/994 tests, lint y build). Falla solo `organization-audit-contract.test.ts`, que es de R3c.
    - La pieza común está en `src/areas/public/auth/components/` (`AuthStatus`, `AuthCodeStep`, `AuthMessage` y `AuthLink`). La usan el ingreso, los dos registros, la invitación, `CallbackPage`, la baja pedida y `EmailCodeForm`.
    - ADR 0041: «Ese correo ya lo usa otra cuenta» no está en la app.
    - Capturas en `r3d/`.
    - Lo que pedía:
    - una sola pieza para la pantalla de estado y el paso del código, que usan las tres páginas;
    - los enlaces de texto en `--marca-tx`;
    - «Te quedan N intentos» con su segunda línea;
    - los `.msj` con triángulo o círculo (v104);
    - «Dirección de la página», de 44 con radio 10;
    - Invitación «Aceptada», con el cartel sin botón;
    - verificar que «Ese correo ya lo usa otra cuenta» no aparezca (ADR 0041).
- [ ] **R3 · Lo de cada pantalla:**
  - la segunda línea de las filas (v92) y el ⋮ de «Cómo entrás» (v96);
  - el paginador «1 de 1» y las pestañas de Empresa;
  - las hojas de Usuario de v99, el desplegable de roles con casillas y descripción, y «Sumar empresa»;
  - el ícono y el asterisco de Configuración, y el panel de la Página pública;
  - las decisiones de v99 del front: la fecha corta, el subtítulo de M-Auditoria-Org y el correo del perfil activo;
  - «10 correos administrados» y los contratos regenerados;
  - las cuatro líneas de docs de v93.
  - lo que encontró R1:
    - el relleno de arriba de las cajas de los formularios (`Section`): 16, como el criterio, y no 20;
    - los enlaces de texto del ingreso en `--marca-tx`, como la variante link de `Button`;
    - el paginador y «Cargar más» en 12 y 14, como los tableros;
    - **una sola pieza para el ingreso y el registro:** hoy hay tres copias de la pantalla de estado y del paso del código, una por página. Se arma una pieza común y las tres páginas la usan.
- [x] **R2c · Errores, botones y tests** (front `8e2c9c2` a `84409d4`; 963 tests en verde, lint y build). Hay una sola falla: `organization-audit-contract.test.ts`, que compara con el back y quedó vieja por el cambio de la auditoría (`20b5b12`); va en R3c. Capturas en `r2c/`.
  - `NotFoundPage` pasó a `shared/ui`: la usan la ruta que no existe, el módulo apagado y las fichas de Usuario, Empresa y Rol.
  - Ningún botón se apaga por un formulario incompleto: «Sumar», «Agregar miembro», «Crear empresa», «Guardar cambios» y «Siguiente» de «Agregar dominio».
  - `Select` recibe el rótulo, lo obligatorio y el error de `FormField`.
  - `resetBetweenTests` limpia los avisos entre pruebas.
  - El arnés visual usa `createQueryClient`.
  - Los tests lentos quedaron por debajo de 3 s; los dos que no bajan llevan 10 s, con su comentario.
  - Lo que pedía:
  - un 404 en una ficha muestra `NotFoundPage`, como pide `errores.md`; para eso sube a `shared/ui`, como `ForbiddenPage`;
  - el error de «Quitar de la organización» va en la franja, como el tablero, y no en el aviso de abajo;
  - los avisos de una prueba no quedan para la siguiente (se limpian entre pruebas; hoy pasa en `RoleEditorPage.test`);
  - el arnés visual (`src/test/visualApp.tsx`) usa `createQueryClient`, el de la app, para que las capturas muestren los avisos globales como los ve el usuario;
  - lo que encontró R2b:
    - el error de «Invitar usuario» va arriba de los botones, no arriba de los campos;
    - accesibilidad: con un `Select` adentro, `FormField` no le pasa al control que es obligatorio;
    - los anchos de diálogo fuera del criterio: Auditoría (640) y la vista previa de la Página pública (600) pasan a `size`;
    - los diálogos de Configuración pasan al cuerpo común (`DialogBody`);
    - «Agregar miembro» no apaga el botón hasta elegir a alguien: Criterio · Botones lo prohíbe y el tablero lo dibuja activo;
    - el texto «Elegí un usuario…» del tablero (la app dice «Elegí una persona…»).
- [ ] **Tests lentos con la máquina cargada** (va en R2c): en la corrida completa, los de `CompaniesPage`, `CompanyDetailPage`, `SettingsPage` y `UserDetailPage` se pasan de 5 s. Solos pasan. Se hacen más livianos, o, si no se puede, el límite se sube en ese test con un comentario que diga por qué. Una suite que falla al azar no sirve de red.
- [ ] **Hecho cuando** el relevamiento, vuelto a correr, no tiene diferencias «Nueva» de las etapas 1 a 6, salvo las que el usuario aceptó.

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

Bloque 3 del back, segunda parte (Configuración, dominios, página pública e Ingreso, 2026-10-04):

27. **Configuración, qué queda afuera de la transacción:** `PublicPageService` interpreta el slug, el logo y el teléfono de contacto antes de abrir la transacción, como `CompanyService` arma la empresa. Son errores de validación y no abren el límite. Por eso el servicio inyecta `IPhoneNumberParser`. Los índices únicos del dominio (`IX_TenantDomains_Domain`) y del slug (`IX_Tenants_Slug`) se traducen en el servicio, que es el que recibe la excepción del commit; sus nombres pasan a `UniqueKeys` en el bloque 4. Dependencias de negocio: `OrganizationSettingsService` 5, `OrganizationDomainService` 4, `PublicPageService` 5 y `PublicPageEditor` 5.
28. **Página pública, versión vieja:** sin página guardada, la única versión vigente es 0. Otra versión es vieja: lanza `ConcurrencyConflictException` y responde 409 `General.ConcurrencyConflict`, como los servicios de Plataforma que comparan la versión a mano. Con la página guardada, una versión vieja ya respondía el código común; el mismo test lo controla. **Queda abierto:** si dos personas guardan la página por primera vez al mismo tiempo, la segunda choca con el índice único de `PublicPages` (una página por organización) y recibe 500 en vez de 409. No es de este ítem y probarlo pide una carrera. Se arregla como en `PlatformFeatureService`, que traduce ese choque a `ConcurrencyConflictException`.
29. **Textos de la última Dueña y del último administrador de una empresa:**
    - `Companies.Membership.LastCompanyAdmin` pasa a «Tiene que quedar al menos un administrador en la empresa.» («The company must keep at least one administrator.»), el texto del tablero de Empresa (lienzo v97, diálogo de roles). Sirve para todos sus caminos: quitarle el rol o quitarla de la empresa en la ficha de Empresa, y cambiarle los roles, desactivarla o quitarla de la organización desde Usuarios. El front va a usar el mismo texto.
    - `Tenancy.Member.LastTenantAdmin` sigue diciendo «Tiene que quedar al menos un Dueño activo.». El texto del tablero de Usuario («…Dale el rol a otra persona antes de quitártelo.») le habla a quien se saca el rol a sí misma. El mismo código responde también cuando la única Dueña se quita a sí misma de la organización (ahí el tablero dice «…antes de salir de la organización.»), y cuando alguien con `users.manage` que no es Dueña le saca el rol, la desactiva o la quita (al desactivar, el tablero de Usuarios dice «Tiene que quedar al menos un Dueño activo.»). Desactivarse a una misma no llega a este código: antes responde `Tenancy.Member.CannotDisableSelf`. El front sabe si la persona es ella misma y puede elegir el texto de cada camino.
30. **Lista blanca:** en `TransactionBoundaryTests` quedan `BusinessOrganizationCreator`, `BusinessSignupCodeFlow` y `BusinessSignupTransactionRunner`, que son del alta de empresa.

Verificación (en `95d853f`): `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1567/1567** (8m 11s). Durante el trabajo: arquitectura 142/142 (`TransactionBoundaryTests` y `ServiceDependencyCountTests` incluidos), `ResourceParityTests` 6/6 y la integración de Configuración, Dominios, Página pública, Registro, Ingreso, Legales, Empresas y Usuarios en verde.

Bloque 3 del back, tercera parte (Alta de empresa, Plataforma y listas blancas, 2026-10-04):

31. **Alta de empresa, en qué clases queda:**
    - `BusinessSignupService` es la entrada y abre la transacción. Tiene 8 dependencias de negocio: el pedido de código, la verificación, los valores regionales, la sesión, el catálogo de identificaciones fiscales, el creador, el contexto y el alcance de la organización.
    - `BusinessSignupPolicy` (6) decide con la cuenta bloqueada: el estado de la cuenta, si el pedido repite un alta, el modo de alta, el límite de organizaciones propias y el slug libre. Es el nombre que ya usan `backend.md`, `arbol.md` y `multitenancy.md`.
    - `BusinessOrganizationCreator` (8) crea la organización con su primera empresa y la aceptación legal, o reproduce un alta ya hecha.
    - `TenantProvisioner` sigue con sus dos piezas, que no son solo del alta: `TenantSpaceProvisioner` (lo usan también el registro de personas y los tests) y `TenantProvisioningReplayVerifier`.
32. **Un solo `OperationLog` por pedido:** `TenantProvisioner` pierde su `OperationLog`, su reloj y su logger. También cambia en Plataforma: crear o aprobar una organización deja una sola línea, la del servicio. Ya no salen `CreateBusinessOrganization`, `ProvisionTenant` ni `ProvisionBusinessOwner`.
33. **Alta de empresa, qué queda afuera de la transacción:** el servicio interpreta el slug y el CUIT antes de entrar al alcance de la organización nueva y abrir la transacción, como en Configuración (decisión 27). Adentro, después del lock de la cuenta, van el estado de la cuenta, la repetición, el límite y el slug libre. El orden de los errores no cambia.
34. **Plataforma:** el grep de `IUnitOfWork` en `Services/Platform` da 12 clases, y todas son servicios de entrada. Lo que quedaba era un reenvío: `PlatformTenantProvisioning` pasaba cada llamada a `PlatformActionGuard`, `PlatformTenantCreator`, `PlatformTenantActivator` e `IPersonalSpaceProvisioner`, y se borró. Los demás helpers de Plataforma tienen una regla propia.
35. **`TenantScopeUsageTests`:** la lista tiene 10 clases y todas siguen entrando: los dos adaptadores (`TenantJobRunner` y `TenantSettingsLoader`), cinco servicios de entrada, `PlatformActionGuard` y dos helpers de Invitaciones. `PlatformActionGuard` se queda porque es la entrada de Plataforma que pide la regla: registra el `SecurityEvent` antes de entrar. **Queda abierto:** `InvitationPreviewFlow` e `InvitationAcceptanceResolver` entran desde un helper para leer la invitación antes de la transacción, e `InvitationAcceptanceResolver.Enter` solo reenvía. Con el criterio de este bloque, la entrada iría en `InvitationService`. No lo cambié porque Invitaciones ya estaba cerrado y no era de estos ítems. **Cerrado** en `6536523` (decisión 51).
36. **Alta de empresa, la repetición que llega al servicio (queda abierto):** si el mismo `operationId` llega al servicio sin la respuesta guardada por `Idempotency-Key` (vencida a las 24 horas, o un corte entre el commit y el guardado de la respuesta), repetir el pedido con los mismos datos responde 409 `General.ConcurrencyConflict` en vez de devolver la organización.
    - Causa: la repetición lee la primera empresa con `ICompanyRepository.GetByIdAsync(id, 0)`, que fija la versión esperada en 0. Cuando `RememberBusinessTenantAsync` guarda la cuenta con `UserManager`, se guarda también la empresa con `xmin = 0`, y choca.
    - Ya pasaba antes de este bloque: lo mostró el test nuevo antes de mover el código. Arreglarlo cambia lo que recibe quien repite el pedido, así que no lo toqué. El test nuevo cubre la repetición con otros datos, no la idéntica.
    - **Cerrado** en `d1e2fb3` (decisión 50).

Verificación (en `0f565d5`): `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1568/1568** (7m 56s). Durante el trabajo: arquitectura 142/142 (`TransactionBoundaryTests`, `TenantScopeUsageTests` y `ServiceDependencyCountTests` incluidos), unitarios de Application 451/451 y la integración del registro de empresa, Plataforma, Ingreso y códigos de ingreso en verde.

Bloque 4 del back, primera parte (alcance de un rol, estado de un miembro y búsqueda, 2026-10-04):

37. **Alcance de un rol, un código por caso:** «un solo código» se aplicó como el mismo código para el mismo caso, desde Usuarios y desde Empresas. La regla responde los tres de `RoleAssignmentErrors`, todos 400:
    - `Roles.RoleAssignment.CompanyNotAllowed`: un rol de toda la organización, en una empresa. Empresas respondía `Roles.Role.CompanyNotAllowed`.
    - `Roles.RoleAssignment.CompanyRequired`: un rol de empresa entre los roles de la organización. Usuarios respondía `Roles.Role.CompanyNotAllowed`.
    - `Roles.RoleAssignment.CompanyMismatch`: el rol propio de otra empresa. No cambia.

    `RoleErrors.CompanyNotAllowed` y `RoleErrors.CompanyRequired` quedan solo para definir un rol. Los textos ya estaban en los dos `.resx`, y el front no nombra estos códigos: muestra el `detail`.
38. **Alcance de un rol, dónde queda cada pieza:**
    - `RoleAssignment.Check(scope, roleCompanyId, companyId)` devuelve `Error?`. `Assign(memberId, roleId, scope, roleCompanyId, companyId)` la usa, así que no se puede crear una asignación inválida.
    - `RoleAssignmentGuard` (`Services/Roles`, 1 dependencia) lee cada rol con `IRoleReader` y aplica `Check`. Su `Assign` arma las asignaciones de los roles ya leídos, sin volver a cargarlos.
    - Usuarios valida antes de la transacción y vuelve a controlar adentro, porque el rol pudo borrarse o cambiar de alcance. Empresas controla una vez, adentro.
    - Ya no se carga la entidad `Role` para asignar. La versión que se le fijaba no controlaba nada, porque la entidad no se modificaba.
    - `OrganizationUserAssignmentManager` pasa de 6 a 5 dependencias de negocio y `CompanyMembershipWriter` sigue en 8.
    - Los permisos efectivos (`RoleReader.ListRoleIdsForUserAsync`) conservan su filtro en SQL como defensa, por el hallazgo de la decisión 42. La matriz de `RolePersistenceTests` lo compara con `Check`.
39. **Estado de un miembro, la expresión única:**
    - Cada reader lee una vez, en SQL, lo que decide el estado: el del miembro, el de la cuenta, el vencimiento de la invitación pendiente más nueva y, en empresas, la membresía. Lee también el nombre y el correo por los que se ordena.
    - Sobre eso aplica `WithState`, una expresión estática que EF traduce a un `CASE`. El filtro es `State == x`, la fila trae el estado (los servicios ya no lo calculan) y los conteos salen de un `GroupBy`, así que suman el total.
    - Las políticas quedan en `src` solo como especificación, como pide el plan: `OrganizationUserStatePolicy` y `CompanyMemberStatePolicy`, que sale de `CompanyMembershipService.GetState`. `MemberStateParityTests` compila `WithState` y lo compara con cada política en todas las combinaciones.
    - `OrganizationUserRow` y `CompanyMemberRow` cambian los estados sueltos por `State`. Las respuestas HTTP no cambian.
40. **B8, qué estado gana:** el de la política, que era el que ya mostraba la fila. Un invitado es «Invitado» aunque su membresía en la empresa esté inactiva. Cambian el filtro «Inactivo» de Miembros de empresa, que ya no lo incluye, y su conteo.
41. **Búsqueda de personas:**
    - Se busca lo mismo que antes: el nombre, cualquier correo verificado y el destino de una invitación pendiente. Ahora, sin acentos ni mayúsculas.
    - `ApplySearch` busca en columnas de una fila, y una persona tiene varios correos. Por eso cada parte filtra su tabla con `ApplySearch` y `MemberSearch` las combina con `Contains`. Lo comparten Usuarios y Miembros de empresa, y entra en la lista cerrada de `IdentityStorageAccessTests`. La ficha `paginado-y-busqueda.md` lo dice.
    - El destino de una invitación es un `Email` con conversor: se busca y se ordena con `EF.Property<string>`. Así se arregló también ordenar Usuarios por correo: `/api/users/paged?sort=email` respondía 500, porque el orden leía `Destination.Value`, que EF no traduce. El front no ordena por correo; el test de búsqueda de Usuarios lo cubre.
    - Miembros de empresa pagina con `ToPagedResultAsync`.
    - **Queda abierto:** `OrganizationAuditReader` sigue con SQL crudo y su `EscapeLike`, porque busca en el JSON de los cambios y no es de miembros ni invitaciones. `tenant.Invitations.Destination` no tiene el índice trigram que la ficha pide para cada columna buscable; tampoco lo tenía antes. **Cerrado:** el índice del destino es la decisión 45, y la búsqueda de la auditoría, `6791bdf` (decisión 65).
42. **Hallazgo sin corregir, D2 y los invitados:** `RoleReader.CountUsersAsync` cuenta solo miembros activos. Por el código, un rol asignado solo a invitados o a miembros inactivos puede cambiar de alcance, y esas asignaciones dejan de valer: los permisos efectivos las ignoran. Borrarlo chocaría con la clave foránea `Restrict` de `RoleAssignments`. Decidir si un invitado cuenta como «persona asignada» es de producto. **Lo decidió el usuario el 2026-10-04:** cuentan todas las asignaciones, también las de invitados y desactivados.

Verificación: `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1588/1588** (8m 51s). Durante el trabajo: arquitectura 142/142, unitarios de Application 451/451, dominio de roles 17/17 y la integración de Organización y Tenancy 142/142 (Usuarios, Empresas, Roles, permisos, Inicio y Auditoría incluidos).

Bloque 4 del back, segunda parte (índices únicos, auditoría de roles y personas asignadas a un rol, 2026-10-04):

43. **Índices únicos, cuáles van en `UniqueKeys`:** los seis que traduce un `catch`: el nombre de un rol, el slug de la organización, el dominio de correo, el módulo de una organización, la identificación fiscal de una empresa y la página pública. Los demás índices únicos no tienen un `catch` y conservan su nombre, como `IX_LoginMethods_UserId_Primary`. Cada configuración fija el nombre que el índice ya tenía, así que la base no renombra nada: la migración del ítem solo crea el índice trigram y el snapshot suma los `HasDatabaseName`. La regla quedó en `persistencia-ef.md`.
44. **B10 y la página pública, cómo se prueba la carrera:** `UniqueKeyRaceTests` usa un host de prueba que decora la pieza del control previo (`IRoleReader.ExistsByNameAsync` y `IPublicPageRepository.GetCurrentAsync`) y retiene a cada pedido hasta que llegan los dos. Así el choque con el índice ocurre siempre y no depende de la suerte. Los tests llaman al servicio, como `ConnectServiceConcurrencyTests`.
    - `RoleService` traduce el choque también al renombrar: dos cambios de nombre simultáneos tienen la misma carrera.
    - La página responde `ConcurrencyConflictException` (409 `General.ConcurrencyConflict`) y no un error propio: los dos guardaron con la versión 0, así que es una edición simultánea, y la pantalla ya sabe ofrecer «Ver lo nuevo».
45. **Índice trigram de las invitaciones:** `IX_Invitations_Search_Destination`, GIN sobre `public.f_unaccent(lower("Destination"))`, la misma expresión que arma `ApplySearch`. Va en la migración `InvitationDestinationSearchIndex`, solo con SQL, revisado con `migrations script`. **Queda abierto:** otras columnas buscables tampoco tienen su índice trigram: `tenant.Roles` (`Name` y `Description`), `tenant.Companies` (`Name` y `TaxNumber`), `platform.SecurityEvents.Reason`, `identity.LoginMethods.ContactEmail` y las de `platform.AccountRecoveryRequests`. No eran de este ítem. **Cerrado** en `fdca58f` (decisión 52).
46. **Auditoría de roles, una entrada por persona y lugar:**
    - `RoleAssignmentTrail` agrupa en el interceptor las asignaciones que cambian en un guardado: una entrada `RoleAssignment` por persona y lugar (la organización o una empresa), con la acción `Updated` y el miembro como `EntityId`. Si los roles quedaron iguales, no hay entrada.
    - El cambio es «Roles en la empresa» o «Roles de la organización», los textos que ya usa el front. Los nombres se guardan al escribir, así el historial se lee igual aunque después se renombre o se borre el rol. Los roles de sistema se traducen al leer. Sin roles se lee «Sin roles», como la fila «Sin roles → Solo lectura» del tablero.
    - Vale para todos los caminos sin que el servicio haga nada: Empresas, Usuarios, el alta de una organización o de una empresa, la invitación y la baja de una cuenta. Antes y después salen del ChangeTracker: todos esos caminos cargan primero los roles que la persona ya tiene en ese lugar, y la regla de `auditoria.md` lo pide.
    - Una modificación sin ningún valor distinto no deja entrada. Cambiar roles desde Empresas dejaba, además, «Cambió el acceso de…» vacío, porque la membresía se marca modificada para cambiar su versión.
47. **El detalle sin identificadores:** `OrganizationAuditService` no muestra un valor que es un identificador. Se van las filas con ids de las asignaciones, las membresías, los miembros, las invitaciones y los roles («Empresa», «un registro»). La persona y la empresa siguen en «Qué» y «Dónde». Las entradas viejas, una por asignación, quedan sin filas de detalle. **Queda abierto**, porque no era de este ítem:
    - el detalle muestra todavía filas «un registro» con fechas y valores técnicos (`Channel`, `IssuedAtUtc`, `ExpiresAtUtc`, `JoinedAtUtc`) y estados en inglés (`Active`);
    - agregar a alguien a una empresa con roles, o invitarlo, deja dos entradas: la membresía o la invitación, y sus roles. El tablero muestra una sola, «Agregó a Paula Méndez» con «Roles en la empresa» adentro;
    - los roles de una persona invitada salen como «Cambió una asignación de rol», sin nombre, porque todavía no tiene cuenta.
    - **Cerrado** en `20b5b12` (decisiones 62 a 64).
48. **Personas asignadas, una sola cuenta:** `CountUsersAsync` lee la cuenta de la proyección de la lista y la ficha: las personas distintas con una asignación del rol, sin mirar su estado. Quien fue quitado de la organización no cuenta, porque quitarlo borra sus asignaciones.
    - Cambia lo que se ve: la columna de personas de Roles y la de los roles de una empresa cuentan también a invitados y desactivados. El aviso previo del front al borrar (`userCount > 0`) coincide ahora con la respuesta del back.
    - Borrar un rol con asignaciones responde el código que ya existía, 409 `Roles.Role.HasUsers` con `userCount`: no hizo falta uno nuevo.
    - **Queda abierto:** si alguien asigna el rol en el mismo instante en que otro lo borra, uno de los dos choca con la clave foránea y recibe 500. Es una carrera como B10; se arreglaría traduciendo el 23503 como el 23505. **Cerrado** en `b5b0c9f` (decisión 49).

Verificación (en `e8d0d94`): `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1596/1596** (9m 31s). Durante el trabajo: arquitectura 143/143 (`UniqueKeysTests` y `HarnessTests` incluidos), unitarios de Application 451/451 (`ResourceParityTests` incluido), dominio 285/285 y la integración de Organización, Tenancy, Persistencia y Legales 300/300 con la auditoría nueva; después, Roles, Empresas, Usuarios, Invitaciones y `RolePersistenceTests` 36/36.

Bloque 4 del back, tercera parte (repetir el alta de una empresa, Invitaciones, la clave foránea de las asignaciones y los índices de búsqueda, 2026-10-04):

49. **Asignar un rol mientras otro lo borra:** los dos pasan el control previo y la clave foránea `FK_RoleAssignments_Roles_TenantId_RoleId` rechaza al que guarda segundo.
    - Borrar choca con 23001 (`restrict_violation`) y no con 23503, porque la clave es `ON DELETE RESTRICT`. Asignar un rol ya borrado choca con 23503. `ConstraintViolations`, que reemplaza a `UniqueViolations`, traduce los dos a `ForeignKeyViolationException`, con el nombre de la clave.
    - El nombre va en `ForeignKeys.RoleAssignmentRole` y la configuración lo fija con `HasConstraintName`. Es el que ya tenía: la base no cambia y el snapshot suma el `HasConstraintName`.
    - Cada lado responde lo mismo que si hubiera llegado después. El borrado, 409 `Roles.Role.HasUsers`, con el `userCount` releído después del rollback (como mínimo 1). La asignación, 404 `Roles.Role.NotFound`, el código que ya da `RoleAssignmentGuard` cuando el rol no está.
    - Lo traducen los cuatro métodos que asignan roles elegidos por alguien: invitar y editar en Usuarios, agregar y cambiar roles en Empresas. Las altas de una organización, de una empresa y de un operador asignan roles de sistema, que no se borran.
    - `ForeignKeyRaceTests` retiene a uno después de su control previo (`CountUsersAsync` o `FindByIdAsync`), deja terminar al otro y lo suelta. Así prueba los dos órdenes sin depender de la suerte. La asignación se prueba por Empresas; Usuarios e invitaciones tienen el mismo `catch`.
50. **Repetir el alta de una empresa:** la repetición lee la primera empresa con `ICompanyRepository.GetByIdAsync(id, ct)`, sin versión esperada, como `IMemberRepository` e `ITenantDomainRepository`. No es un reader porque `BusinessOrganizationCreator` ya tiene 8 dependencias de negocio. Cambia lo que recibe quien repite el pedido sin la respuesta guardada: la misma respuesta con la organización, en lugar de 409 `General.ConcurrencyConflict`, como pide `idempotencia.md`.
51. **Invitaciones, la entrada al alcance:** cada helper tiene dos pasos. `ReadToken` prueba el token afuera (el del pedido o el de la continuación) y devuelve `ProvenInvitationToken`, con `ToString` redactado. `InvitationService` entra a la organización del token, y adentro corren `PreviewAsync` o `PrepareAsync`, la transacción y la sesión, como antes. `InvitationService` pasa de 4 a 5 dependencias de negocio y la lista de `TenantScopeUsageTests` queda con 9 clases.
52. **Índices de búsqueda:** cada columna se confirmó en el código antes de crear su índice: `ApplySearch` en `RoleReader`, `CompanyReader`, `SecurityEventReader`, `PlatformAccountReader` y `AccountRecoveryReader`. Se llaman `IX_<Tabla>_Search_<Columna>`, como los anteriores.
    - `SearchIndexTests` no existía y se creó. Lee cada `ApplySearch` de los readers, toma los campos simples (`x => x.Columna` y `EF.Property`) con el `DbSet` de su sentencia y busca su índice en `pg_indexes`. Las búsquedas dentro de una subconsulta (el Dueño en `PlatformReader`) van en una lista a mano, y un campo simple sin `DbSet` en su sentencia tiene que estar en esa lista.
    - **Queda abierto:** `OrganizationAuditReader` busca con SQL crudo en el JSON de los cambios (decisión 41), sin índice trigram. El test no lo ve, porque no usa `ApplySearch`. **Cerrado** en `6791bdf` (decisión 65).

Verificación (en `fdca58f`): `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1601/1601** (7m 42s). Durante el trabajo: el registro de empresa 5/5, Invitaciones 34/34, Usuarios y el alta de Plataforma 16/16, y Roles, Empresas, Usuarios, Invitaciones, las dos carreras y la persistencia de roles y empresas 65/65. Además, `InvitationServiceTests` 16/16, `CompanyServiceTests` 10/10, unitarios de Application 451/451, arquitectura 143/143 (`TenantScopeUsageTests`, `UniqueKeysTests` y `HarnessTests` incluidos) y Persistencia 19/19 (`SearchIndexTests`, `MigrationsTests`, `DbContextModelTests`, `SearchTests` y `SortIndexTests`).

Bloque 4 del back, cuarta parte (enmascarado, registro y sufijos, 2026-10-04):

53. **El teléfono enmascarado, «•••• 4521»:** el lienzo lo muestra en el formato del país de quien mira, con el medio tapado: «011 15-••••-4521» en Cuentas, Cuenta-Plat y Recuperaciones, y «011 15-•••••-4521» en Cuenta-Metodo y Cuenta-Verificar. La característica y el «15» salen de los metadatos de libphonenumber, que viven en Infrastructure; `PersonalDataMask` está en Application y el plan borra `IPhoneNumberParser.Mask`. Por eso la pieza muestra solo los últimos cuatro dígitos, el criterio más simple que no revela el número. **Queda abierto**, porque es lo que se ve: para mostrar la característica como el tablero, cada servicio que muestra un teléfono tendría que formatearlo con el país de quien mira (`IPhoneNumberDisplayFormatter`) antes de taparlo.
54. **Un solo formato:** el correo queda «lu•••@dominio» (dos letras y el dominio), el de Plataforma y Mi cuenta. Lo ya enmascarado no muestra más: la auditoría vuelve a enmascarar al leer, y las entradas viejas («***@…», «***4521») se leen «•••@…» y «•••• 4521». Cambia lo que se ve:
    - Mi cuenta: el destino del código pasa de «l***@gmail.com» a «lu•••@gmail.com», como Cuenta-Metodo, Cuenta-Verificar y Cuenta-Baja; un teléfono, de «••••4521» a «•••• 4521».
    - Los avisos de métodos: de «l***@gmail.com» a «lu•••@gmail.com». El tablero Mensajes todavía muestra «l***@…» y «+54 9 11 •••• 4521».
    - Plataforma: el teléfono de Cuentas y Recuperaciones pasa de «+54 9 11 •••• 4521» a «•••• 4521». El front de Plataforma todavía no existe.
    - La auditoría de la organización: el correo de una invitación sin cuenta pasa de «***@gmail.com» a «va•••@gmail.com», y un teléfono del detalle, de «***4521» a «•••• 4521». El tablero Auditoria-Org muestra el correo completo («Invitó a valentina.ruiz@gmail.com»); `auditoria.md` pide enmascararlo, así que sigue enmascarado.
55. **`PersonalDataMask.LoginMethod`:** las cuatro copias que elegían entre correo, teléfono y Google usan un solo método. Google se muestra por su correo de contacto y, sin contacto, «Google». El número fiscal del detalle de auditoría sigue con «***1234»: no es correo ni teléfono.
56. **Registro por área:** cada área de `Services` tiene su `<Área>Registration.cs` con un `Add<Área>()` interno, y `AddApplication` los llama. Las opciones van en el registro de su área (Auth, Invitations y Messaging), con `ValidateOnStart`, e Infrastructure deja de registrarlas. El orden conserva el de «Para resolver»: Usuarios, Configuración y Página pública. `DisplayFormatter`, los validadores e `IRequestValidator` quedan en `AddApplication`, porque son de todas las áreas.
57. **`ValidateOnBuild` sin `ValidateScopes`:** hay pruebas que leen servicios scoped desde la raíz (`UserManager` e `IOpenIddictApplicationManager`). Las pruebas con una `WebApplicationFactory<Program>` propia, que no sale de `ApiFactory`, no lo heredan.
58. **Sufijos, la lista cerrada:** tiene 22 sufijos. Los cinco de siempre (`Policy`, `Guard`, `Issuer`, `Verifier` y `Linker`) no alcanzan para lo que quedó después del bloque 3: hay pasos que escriben, leen, arman o avisan. Un helper es una clase de `Services` que arma el contenedor y no termina en `Service`; así no entran `IssuedLoginCode` (un resultado), los records, las clases estáticas ni los registros.
    - Se renombraron 23 helpers con sufijos vagos (`Core`, `Flow`, `Manager`, `Coordinator`, `Consumer`, `Messenger`, `Cleaner`, `Changer`, `Assigner`, `Allocator` y `Acceptor`) o con un sustantivo (`Audit`, `Listing`, `Preferences`, `Defaults`, `Cultures`, `Availability`, `Companies`, `Areas` y `ContextProvider`). La conducta no cambia; la lista de renombres está en el commit.
    - `Scheduler`, `Canceller`, `Processor` y `Activator` tienen una sola clase cada uno: nombran pasos de la baja de una cuenta (ADR 0035) y de la activación de una organización que los demás sufijos no describen.
    - Los `AGENTS.md` no se tocaron, porque son las instrucciones de los agentes. La raíz y `Services/AGENTS.md` nombran los cinco de siempre, y `Services/Auth/AGENTS.md` todavía dice `UserCultures`, que ahora es `UserCultureResolver`.

Verificación (en `df62c6e`): `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1627/1627** (7m 45s). Durante el trabajo: `PersonalDataMaskTests` 26/26, unitarios de Application 473/473 (`DependencyInjectionTests` incluido), arquitectura 146/146 (`ApplicationServicesTests` y `HarnessTests` incluidos), la integración de auditoría, Plataforma, métodos y avisos 72/72, y la de Hosting, Outbox, ingreso, registro, Inicio e Invitaciones 55/55.

Última tanda del back, lo que ve el usuario (2026-10-04):

59. **El correo del perfil activo, en el mismo campo:** `/api/me` sigue con `email`, que ahora es el del perfil activo. En una organización, el correo verificado que ella administra (`ManagedByTenantId`), primero el principal; si no hay, el principal de la cuenta. En Personal y en Plataforma, el principal. No se sumó un campo: el front ya muestra `email` en el pie y en «Más». Un correo administrado sin verificar no cuenta. El contrato cambia solo en la descripción de `MeResponse`.
60. **La invitación de operador repetida:** 409 `Platform.Operator.InvitationExists` cuando el correo es de una cuenta con la invitación sin aceptar, vigente o vencida. Lo decide `PlatformOperatorProvisioner.IsPendingAsync`, que ya leía las cuentas: el servicio no suma dependencias. El correo de cualquier otra cuenta sigue con `Platform.Operator.AccountExists`. En inglés: «That email address already has an invitation. Resend it from the list.», como el de `AccountExists`.
61. **La invitación de operador sin nombre:** no coincidía. Sin nombre, el asunto era «El equipo de ArquitecturaBase te invitó como operador de ArquitecturaBase», y el texto terminaba «… de la plataforma ArquitecturaBase.».
    - Tiene sus dos textos, `OperatorInvited.UnnamedSubject` y `OperatorInvited.UnnamedBody`. En inglés: «The ArquitecturaBase team invited you as an operator» y «Hello. The ArquitecturaBase team invited you to join the platform as an operator.».
    - `PlatformOperatorTests` esperaba el asunto viejo al reenviar, porque quien reenvía no tiene nombre. Se corrigió en `9c54bd3`.
62. **Una entrada por alta, al guardar:** `AccessChangeTrail`, junto a `RoleAssignmentTrail`, arma la entrada en el interceptor y no al leer. Así el cursor no parte una entrada entre dos páginas.
    - Invitar: la entrada de la invitación suma `OrganizationRoles`, `CompanyIds` y `CompanyRoles`. El miembro, las membresías y los roles de esa persona en el mismo guardado no dejan entrada.
    - Agregar a una empresa: la membresía nueva suma `CompanyRoles`, y los roles de ese lugar no dejan entrada propia.
    - En un alta, el «antes» de los roles queda vacío y no «Sin roles», como el tablero.
    - Las empresas se guardan por id y se leen por nombre: «Dónde» y «Empresa» son las mismas. Una invitación a varias empresas las une con coma, y sus roles van juntos en «Roles en la empresa», como el correo de la invitación.
    - Quien todavía no tiene nombre se nombra por el correo de su última invitación, enmascarado: «Cambió los roles de va•••@gmail.com».
    - Quitar a alguien de una empresa y revocar una invitación siguen dejando más de una entrada: el tablero no los dibuja.
63. **El detalle, en el idioma de quien mira:** `OrganizationAuditEntryBuilder` arma cada entrada. Un campo se muestra solo si tiene `Field.*` en `Audit.resx` y un valor que se puede mostrar; si no, queda afuera. Así se van «un registro», las fechas técnicas y el canal.
    - Los estados, con las palabras de las pantallas (`Value.<Entidad>.Status.*`): «Activo», «Deshabilitado» e «Invitación pendiente» de Usuarios; «Activa» e «Inactiva» de Empresas; «Activo» e «Inactivo» de los miembros de una empresa. «Invitación aceptada» e «Invitación revocada» no estaban en ninguna pantalla. El estado no va en un alta ni en una baja.
    - La zona, el idioma y la moneda, con `DisplayFormatter`, que suma `FormatTimeZoneAsync`, `FormatCultureAsync`, `FormatCurrencyAsync` y `CreateForCurrentCultureAsync`: «Córdoba (GMT−3)», «Inglés (Estados Unidos)» y «peso argentino (ARS)», como las pantallas. El idioma va en el de quien mira; el tablero lo muestra en el propio («English (United States)»).
    - La zona de una empresa es «Zona de negocio», el rótulo de su ficha, y no se muestra al crearla: el tablero muestra solo «Nombre». Los permisos van por nombre («Permisos»), y el número fiscal, con «***1234» («Identificación fiscal»).
    - Un rol de sistema se lee en el idioma de quien mira: «Creó el rol Dueño».
64. **«Agregó a», «Deshabilitó a», «Habilitó a» y «Quitó a»:** el tablero dibuja «Agregó a Paula Méndez» y «Deshabilitó a Marcos Giménez». `AuditTexts.Summary` recibe una variante: la membresía nueva es «Agregó a», y el cambio de estado de un miembro, «Deshabilitó a», «Habilitó a» (la palabra de Usuarios) o «Quitó a» (el texto que ya existía).
65. **La búsqueda de la auditoría, con índice:** sigue en SQL, porque `Changes` es jsonb y `ApplySearch` no lo pasa a texto. Un índice GIN `jsonb_ops` no sirve para buscar texto adentro de los valores.
    - Usa la expresión de `ApplySearch` y dos índices GIN trigram: `IX_AuditEntries_Search_Changes`, sobre `f_unaccent(lower("Changes"::text))`, e `IX_AuditEntries_Search_EntityType`. Van en la migración `AuditEntrySearchIndexes`, solo con SQL, revisada con `migrations script`.
    - Quien actuó se busca con `ApplySearch` sobre `DisplayName`, que ya tenía su índice.
    - `SearchIndexTests` suma la lista `SqlSearches`: un reader que busca con `LIKE` en SQL tiene que estar ahí, usar la expresión de `ApplySearch` y no `ILIKE`, y tener el índice de cada columna. La excepción quedó en `paginado-y-busqueda.md`.
    - Cambia lo que se ve: la búsqueda ya no distingue acentos («invitacion» encuentra «Invitación»).

Verificación (en `9c54bd3`): `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1638/1638** (8m 05s). La primera corrida dio 1637/1638: `PlatformOperatorTests` esperaba el asunto viejo de la invitación sin nombre (decisión 61). Durante el trabajo, en rojo y después en verde: `ProfileServiceTests` 21/21, `PlatformOperatorTests` 7/7, `EmailTemplateTests` 25/25, `OrganizationAuditTests` 10/10 (4 en rojo) y `SearchIndexTests` 2/2. Además, `ResourceParityTests` 6/6, `ErrorCodeTests` 6/6, unitarios de Application 476/476, arquitectura 146/146, la integración de Organización, Persistencia, Invitaciones, Plataforma y avisos 79/79, y `MigrationsTests`, `DbContextModelTests` y `SearchTests` con la migración nueva.

Tanda 4f del back, la auditoría de la organización como su tablero (2026-10-04):

66. **El vencimiento de una invitación, una vez y con su fecha:** como `AccountRecoveryNoticeWorker`, una marca en la fila y un trabajo que la completa.
    - `Invitation.ExpiryRecordedAtUtc` (`[NotAudited]`) y `RecordExpiry`: una sola vez, si estaba pendiente al vencer, aunque después se la revocara o reenviara (reenviar revoca). «Vencida» no pasa a ser un estado guardado: sigue saliendo de la fecha, así que Usuarios, Inicio, la vista previa y el reenvío no cambian.
    - `InvitationExpiryWorker` corre cada hora y entra a cada organización activa con `TenantJobRunner`, que no tenía ningún worker; una organización que falla no frena a las demás. No corre en Testing, como los otros dos. Un lock por organización evita registrar dos veces el mismo vencimiento entre dos instancias.
    - La entrada la escribe `InvitationExpiryService` con `IAuditLog.Record`, que suma `occurredAtUtc` para la fecha real. Quien actúa es el sistema, porque el trabajo no tiene usuario. Lleva `Status: Pending → Expired` y las empresas de la persona invitada, para «Dónde». La fila «Empresa» queda solo al invitar: el tablero (a6) muestra solo «Estado».
    - El correo va enmascarado («Venció la invitación a ca•••@…»). El tablero lo dibuja completo, pero el criterio «Datos tapados» del lienzo y `auditoria.md` lo tapan en Auditoría.
    - Migración `InvitationExpiryRecorded`: una columna nula en `tenant.Invitations`, sin índice (se busca dentro de una organización). La primera corrida registra los vencimientos viejos, cada uno con su fecha.
    - Cambia lo que se ve: aparece «Venció la invitación a …», por «Sistema». Llega hasta una hora después de vencer, pero en su lugar por fecha.
67. **Un cambio de un solo campo dice cuál:** `AuditTexts.Summary` busca `AuditSummary.<Entidad>.<variante>`, después `.<acción>` y después `AuditSummary.<Entidad>`. La variante de un cambio de un solo campo es el nombre del campo, y un texto sin `{0}` vale sin el nombre de lo afectado (la configuración no lo tiene).
    - Solo tienen texto los dos que dibuja el tablero: `Company.TimeZoneId` («Cambió la zona de …») y `TenantSettings.DefaultCulture` («Cambió el idioma y la región predeterminados»). Los demás campos solos (la zona o la moneda de la organización, el nombre de una empresa, los permisos de un rol) siguen con el texto de la acción.
    - El tablero muestra «Zona de negocio: La de la organización → Córdoba (GMT−3)». En el back toda empresa tiene zona, así que el «antes» es siempre una ciudad. «La de la organización» es el texto del campo vacío en el front.
68. **Plataforma en la auditoría de la organización, desde sus `SecurityEvent`:** `auditoria.md` y `multitenancy.md` no lo decían. Se eligió leer y no escribir.
    - La aprobación ya deja un `SecurityEvent` con `TargetTenantId`. Escribir además un `AuditEntry` en la organización (como hace la moderación de la página) duplicaba el dato y dejaba afuera las aprobaciones de antes.
    - `OrganizationAuditReader` suma al cursor las acciones de su lista `PlatformActions` (hoy, aprobar, con el cambio de estado que significa). Va en SQL, como una unión de claves (fecha, Id) sobre la que corre el cursor; después lee las filas de la página. `mt_app` ya podía leer `platform.SecurityEvents`. De cada evento toma solo la fecha y el tipo.
    - No entran en la búsqueda (busca en los cambios y en quién actuó) ni en el filtro por persona, y van en el tema «Organización». La lista de «Quién» suma «Plataforma», sin identificador, cuando hay alguna.
    - La respuesta y la lista de «Quién» llevan identificador solo para una persona. Las entradas que dejó un operador adentro de la organización (al aprobarla o al moderar su página) mostraban su id; filtrar por un id que no es de una persona no devuelve nada.
    - «Espera aprobación» sale de Palabras, y «Activa», del tablero; en inglés, «Awaiting approval», como el front, y «Active».
    - Suspender, reactivar, cerrar, despublicar la página y verificar o quitar un dominio también dejan un `SecurityEvent` con `TargetTenantId`, pero el tablero no los dibuja: quedan para Diseño. Sumar uno es una fila en `PlatformActions` y su texto.
69. **Quitar a alguien de una empresa y revocar una invitación, una entrada:** `AccessChangeTrail` suma las salidas.
    - Quitar de una empresa (o volver a sumar) es la membresía que cambia de estado: se lleva los roles de ese lugar, con el «después» vacío, y guarda la persona y la empresa como claves técnicas. Antes salía «Cambió una membresía de empresa», sin nombre ni empresa, porque un cambio de estado no trae la persona, y aparte «Cambió los roles de …».
    - Revocar: la invitación que pasa de pendiente a revocada se lleva el miembro, las membresías y los roles de esa persona en el mismo guardado. Al reenviar no, porque la persona sigue invitada: quedan la revocada y la nueva. Quitar de la organización a alguien invitado (solo por la API: la pantalla ofrece «Revocar invitación») también queda como la invitación.
    - Los resúmenes no cambian, porque el tablero no los dibuja: «Cambió el acceso de …» y «Cambió la invitación de …» (decisión 72).
    - Quitar a alguien de la organización sigue dejando varias entradas: «Quitó a …», sus roles de la organización y una por empresa. No era de este ítem. **Cerrado** en `20f9fa5` (decisión 74).
70. **Sin nombre ni invitación, por su correo:** su correo verificado, primero el principal, enmascarado, como lo muestra Usuarios. Le pasa a quien registró la organización y todavía no puso su nombre: al crear una empresa, «Agregó a ac•••@…». Sin correo (una cuenta solo con teléfono) sigue el texto genérico. (Ya no al crear una empresa: decisión 73.)
71. **El idioma, en su propio idioma:** `DisplayFormatter.FormatCultureInItsOwnLanguageAsync` toma la traducción del catálogo en esa misma cultura, con su cadena de caída, como `nativeCultureName` del selector del front. La zona y la moneda siguen en el idioma de quien mira.
72. **Palabras para Diseño:** el tablero no tiene otra palabra para «Invitación aceptada», «Invitación revocada» ni «Habilitó a», así que quedan. Tampoco dibuja:
    - el «Qué» de revocar una invitación: queda «Cambió la invitación de …»; podría ser «Revocó la invitación a …», por «Revocar invitación»;
    - el de quitar a alguien de una empresa: queda «Cambió el acceso de …»; podría ser «Quitó a …», por «Quitar de la empresa»;
    - los textos nuevos en inglés: «The invitation to {0} expired», «Changed the time zone of {0}», «Changed the default language and region», «The platform approved the organization», «Invitation expired» y «Awaiting approval».

Verificación (en `d64d50e`): `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1643/1644** (11m 27s): falla solo `HarnessTests`, por los docs del front (arriba). Durante el trabajo, en rojo y después en verde, un ítem por vez: `OrganizationAuditTests` 15/15 (cinco tests nuevos y uno cambiado) e `InvitationTests` 8/8 (el nuevo no compilaba). Además, dominio 286/286, unitarios de Application 476/476 (`ResourceParityTests` y `DependencyInjectionTests` incluidos), arquitectura 145/146 (la misma `HarnessTests`), la integración de Auditoría, Empresas, Usuarios, Invitaciones y la baja de cuentas 68/68, la de Inicio, `SearchIndexTests`, el alta de Plataforma y `AuditTrailTests` 25/25, y `MigrationsTests` 4/4 con la migración nueva.

Tanda 4g del back, la auditoría de la organización: una entrada por acción y «Quién» (2026-10-04):

73. **Crear una empresa, una sola entrada:** `AccessChangeTrail` toma las empresas nuevas del guardado. La membresía y los roles de quien la crea, en ella, no dejan entrada propia ni se muestran: el tablero (a9) muestra solo «Nombre».
    - Vale para toda empresa nueva: también la primera de una organización, cuando se guarda junto con la membresía de su Dueño.
    - Cambia lo que se ve: al crear una empresa ya no sale «Agregó a …». Quien la creó sigue en «Quién».
74. **Quitar a alguien de la organización, una sola entrada:** el miembro que pasa a quitado se lleva sus membresías (que quedan inactivas) y sus roles del mismo guardado.
    - Queda «Quitó a …», el texto que ya existía, con los «Roles de la organización» y los «Roles en la empresa» que tenía y el «después» vacío, como al quitarlo de una empresa (decisión 69). Los roles de todas sus empresas van juntos, como en la invitación. «Dónde» es «Toda la organización».
    - El estado no se muestra: Usuarios no tiene una palabra para quien fue quitado, y el resumen ya lo dice.
    - Si tenía la invitación pendiente, la entrada sigue siendo la de la invitación (decisión 69).
    - La baja de una cuenta (`MembershipDeletionParticipant`) también quita al miembro: su «Quitó a …» ahora lleva los roles. Sigue dejando, además, la entrada `Custom` que ya dejaba (abajo, en lo abierto). **Cerrado** en `616c2cc` (decisión 78).
    - El tablero no dibuja esta entrada: el detalle va a Diseño.
75. **El orden de las filas del detalle:** `Changes` es jsonb, y PostgreSQL guarda las claves ordenadas por largo. Por eso «Roles de la organización» salía después de «Roles en la empresa», al revés del diálogo de Usuarios y de lo que pide el comentario de `CompleteInvitation`. `OrganizationAuditEntryBuilder` pone primero los roles de la organización y deja el resto en el orden guardado. Cambia también la invitación con roles de la organización: «Correo», «Roles de la organización», «Empresa» y «Roles en la empresa».
76. **«Quién» por tipo, con `actorKind`:** es un texto, como `period` y `category` de la misma ruta. Sus valores son los del `actorKind` que ya devuelven la lista y las entradas: `User`, `System` y `PlatformOperator`.
    - Se combina con los demás filtros, también con `actorId`. `PlatformOperator` incluye las acciones de Plataforma (sus `SecurityEvent`); `User` y `System` las dejan afuera.
    - `/audit/actors` ignora `actorKind` y `actorId`, como ya ignoraba `actorId`, para que la lista no se achique a lo elegido.
    - Un valor desconocido responde 400 `Validation.Failed` con `errors.actorKind`. `User` sin persona no está en el tablero, pero es el valor que la lista devuelve para una persona.
77. **El orden de «Quién»:** las personas (por nombre), «Sistema» y «Plataforma», como el desplegable del tablero. Antes salía «Plataforma» antes que «Sistema», por el orden del enum.

Queda abierto:
- **Cerrado en `616c2cc` (decisión 78). La baja de una cuenta deja dos entradas:** «Quitó a …» y la `Custom` que agrega `MembershipDeletionParticipant` con `IAuditLog.Record` (motivo y cuenta). `auditoria.md` dice que un guardado deja una sola entrada por entidad y que no se suma otra a mano. No era de esta tanda.
- **El front:** «Quién» todavía muestra solo personas (`AuditPage` descarta las opciones sin `actorId`). Para «Sistema» y «Plataforma» tiene que mandar `actorKind` y regenerar sus tipos con `npm run contracts`.

Verificación (en `d52f518`): `dotnet build` de la solución con 0 advertencias y `dotnet test` completo con Docker **1647/1647** (9 minutos); esta vez `HarnessTests` también pasó. Durante el trabajo, en rojo y después en verde, un ítem por vez: `OrganizationAuditTests` 16/16, 17/17 y 18/18 (tres tests nuevos, uno rehecho y la preparación de la aprobación pasada a un helper). Además, la integración de Empresas, Usuarios, `AuditTrailTests`, `AuditTrailRlsTests` y la baja de cuentas (31/31 y 45/45, con auditoría), `OpenApiTests`, `ExplicitRouteInventoryTests` y `SearchIndexTests` (33/33, con auditoría) y `ControllerInputContractTests` 13/13.

Bloque 0 del back: los docs al día con el código (2026-10-04):

78. **La baja de una cuenta, una sola entrada:** `MembershipDeletionParticipant` ya no suma la entrada `Custom` (motivo y cuenta). El motivo queda en el diff de «Quitó a …» (`RemovalReason`), y la cuenta es el miembro de la entrada. Test `AccountDeletionParticipantsTests.Current_participants_clean_only_the_account_data_and_can_run_twice`: rojo con dos entradas, verde con una. Lo dicen `auditoria.md` e `identidad.md`.
79. **`arbol.md`, desde el árbol real:**
    - las líneas que existen conservan su texto y su orden;
    - salen 189 líneas viejas: nombres que nunca existieron y 36 archivos que se mudaron, que conservan su descripción en su carpeta real;
    - lo nuevo toma la descripción de su `<summary>` o una escrita a mano; las migraciones no se listan, como antes;
    - lo de la E5 se llevó al real aunque `HarnessStage` no la cerró, porque su back está hecho;
    - las secciones «Piezas…» repetían el árbol con nombres viejos: quedan solo las piezas de la E7 (con las rutas de su plan) y de la E10.
80. **Punteros fuera del mapa:** 24 carpetas tenían `AGENTS.md` y no estaban en el mapa de `arnes.md` §3. Se sumaron 15 filas: una carpeta padre, como `Infrastructure/Persistence/` o `tests/*.Api.IntegrationTests/TestFeatures/`, cubre a sus hijas. `Configuration/Auth`, `Configuration/Messaging` y `TestFeatures` recibieron su puntero, porque el mapa los exige. `Domain.UnitTests/Invitations` apunta a `identidad.md`, como una carpeta de área.
81. **El detector de catálogos** solo reconocía permisos de un punto, así que `company.*` y `platform.*` nunca contaban como documentados. Ahora acepta uno o más puntos y también los verifica contra el catálogo (test con `company.members.reed`). El de símbolos suma siete nombres del framework (`HttpContext`, `DbContext`…).
82. **Cinco descartes con motivo** en `ignorados.json`:
    - la carpeta reservada `Readers/Platform`, la única lista blanca de `QueryFilterBypassTests`, hoy vacía;
    - `IIdentityService` e `IQuery` (dos veces), nombrados a propósito: uno es el tipo de ArquitecturaBase que se reemplazó y el otro, algo prohibido;
    - los códigos `ReferenceData.*`, que no son de una feature: los usan los value objects de E1, pero ninguna ruta los devuelve.
83. **Lo futuro lleva su etapa entre paréntesis:** `(E7)`, `(E8)`, `(E10)`. Es lo que reconocen el detector y `HarnessTests`; «E7» suelto o «Etapa 10» no cuentan.

Verificación: `dotnet build` de la solución con 0 advertencias; `HarnessTests` 10/10; `node --test` de los arneses 44/44; la integración de la baja y la auditoría (`AccountDeletion*`, `OrganizationAuditTests`, `AuditTrailTests` y `AuditTrailRlsTests`) 50/50; `realinear-docs --detectar` en 0 para el back; y `dotnet test` completo con Docker **1647/1647 (10 minutos)**.
