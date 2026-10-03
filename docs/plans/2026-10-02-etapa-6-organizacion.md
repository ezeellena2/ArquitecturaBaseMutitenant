# Etapa 6: área B2B (organización) — plan ejecutable

> Plan de implementación para esta etapa. Seguir el orden, completar las casillas con evidencia y commitear cada cambio coherente y verificado con rutas explícitas.

**Objetivo:** completar el acceso B2B desde el alta de una empresa hasta su administración diaria: usuarios e invitaciones, empresas y membresías, configuración y dominio de correo, página pública, auditoría y navegación.

**Arquitectura:** respetar `docs/architecture/backend.md`, `docs/architecture/multitenancy.md`, `docs/features/identidad.md`, `docs/features/roles.md`, `docs/guides/agregar-un-area.md` y las fichas de `docs/rules/`. La fuente visual vigente es `../../../ArquitecturaBaseMutitenantFront/docs/design/lienzo/` v64. El 2026-10-03 el usuario indicó copiar v64 para `Usuarios`, `Usuario`, `Roles`, `Rol`, `Empresas`, `Empresa`, `Configuracion`, `Pagina-Org`, `Auditoria-Org`, `Error-Org` y `Avisos`, y confirmó que sus móviles v64 están aprobados para esta etapa. `Registro-Empresa` sigue validado; Roles/Rol de escritorio fue aprobado en E4. Los dos tableros de Inicio quedan «para repensar» según el README v64.

**Restricciones de etapa:** trabajar en `main`, sin push; no crear cuentas ni organizaciones de ejemplo en Development; pruebas con sus propias bases; conservar los cambios de invitaciones `51f604f`, `feb67ec` y `7c83835`; no crear guardas de arquitectura ni tests nuevos del arnés salvo la verificación explícita del salto de etapas no contiguas; no marcar E5 cerrada al cerrar E6.

## Seguimiento

### 0. Línea de base y plan

- [x] Confirmar `main`, árbol limpio y último commit en ambos repos.
- [x] Confirmar Aspire apagado; no repetir la puerta de E4.
- [x] Leer las fichas, decisiones, informes, plan maestro y aprobaciones; revisar el README y `canvas.json` vigentes de v64.
- [x] Revisar este plan con un revisor de documentos y resolver las observaciones de secuencia, E2E canónico, formatos, arnés y puerta manual antes del código.
- [x] Registrar E6 antes de E5 en los arneses existentes con la lista explícita `{0,1,2,3,4,6}`; E5 sigue abierta. `HarnessTests` y `harness.test.ts` pasan sin arnés paralelo.

### 1. Alta B2B y provisionamiento reutilizable

- [x] Escribir tests para límite `MaxOwnedOrganizations` y validar los campos de `BusinessSignup`; la prueba de alta y repetición con PostgreSQL pasó en la suite completa.
- [x] Implementar `TenantProvisioner` reutilizable por alta B2B y futura plataforma, conservando las transacciones/UoW existentes y roles de sistema; la repetición de la misma operación no duplica tenant, owner, empresa ni settings. Commit `813ff0c`.
- [x] En el alta inicial, asignar al Dueño como miembro activo de la primera empresa y otorgarle `CompanyAdmin`; el replay confirma la membresía y la asignación sin duplicarlas.
- [x] Agregar catálogo persistido de slugs reservados con los nombres validados por el tablero: `www`, `app`, `api`, `auth`, `admin`, `empresas` y `plataforma`; el lector distingue disponibles, reservados y tomados. Commits `bd91f9e` y `fb12872`.
- [x] Exponer `GET /api/public-site/slug-availability` con estados disponible/reservado/tomado y formato validado.
- [x] Traducir la carrera de alta contra el índice único de `Tenants` en `Tenancy.TenantSlug.Taken`.
- [x] Crear la primera empresa con CUIT opcional y zona inicial; validar el CUIT mediante `TaxId`/catálogo y guardarlo normalizado.
- [x] Implementar el alta por correo y por Google, con aceptación vigente y sin crear acceso Personal. Aplicar modo de alta y máximo de organizaciones propias; devolver el estado de aprobación.
- [x] Agregar ruta HTTP con antiforgery y cookie de identidad, declarar la ruta anónima existente, textos es/en, actualizar OpenAPI y probar operación/replay y ausencia de Personal. El contrato y cliente front están sincronizados y verificados.
- [x] Extender y ejecutar `test:e2e:real`: alta B2B por correo, sesión, `/org`, F5, cambio a Personal, invitaciones y aceptación sin Personal; el proveedor OAuth de Google no forma parte del entorno local E2E. El E2E real usa PostgreSQL y pickup aislados.
- [x] Verificar builds y pruebas dirigidas; los cambios de modelo/API/recorridos se commitearon en unidades coherentes con rutas explícitas.

### 2. Empresas y roles por empresa

- [x] Crear `Company`, configuración EF/migración e índices tenant-owned; la persistencia PostgreSQL pasó en integración.
- [x] Crear `TaxId` y `ArgentineCuitValidator` según `docs/rules/identificacion-fiscal.md`; agregar `TaxIdTests` y pruebas de propiedades para dígito, normalización, valor inválido y datos históricos nulos. Commits `234fc72`.
- [x] Implementar servicio y API de listado paginado, conteos por estado, ficha, alta, edición concurrente (`version` y 409) y cambio de estado activo/inactivo; validar zona horaria y normalizar CUIT desde catálogos. Pruebas unitarias dirigidas para listado, conteos, alta, primer Administrador y concurrencia de estado. Commit `7fbc59a`.
- [x] Ejecutar casos API con PostgreSQL para estado/versiones, permisos de empresa activa y listados/conteos; la suite completa confirmó aislamiento, TaxId y concurrencia.
- [x] Implementar membresías de empresa y asignación de roles mediante `RoleAssignment.CompanyId`, reutilizando catálogos y `RoleService`; agregar listado, conteos, alta, edición de roles y estado versionado bajo permisos de empresa. La prueba HTTP real cubre permisos antes y después, una y dos personas Administradoras, y el rechazo de quitar a la última.
- [x] Proteger quitar o deshabilitar al último `CompanyAdmin` activo con lock transaccional; los tests de guardia y concurrencia PostgreSQL pasan en `CompaniesTests`.
- [x] Conectar el catálogo de empresas y roles a los flujos de Invitaciones, manteniendo los estados individuales de cada invitación.
- [x] Actualizar inventario de rutas, OpenAPI y permisos/textos existentes; controllers, contratos y recorridos focales pasaron con PostgreSQL y el cliente front quedó sincronizado.

### 3. Usuarios e invitaciones de la organización

- [x] Implementar lectura paginada de miembros con filtros, conteos y ficha; incluir estados activo, invitado, vencido, deshabilitado, baja pedida y correo administrado. Las pruebas HTTP PostgreSQL cubren 1/50 registros y límites de página.
- [x] Implementar edición concurrente con `version`; habilitar, deshabilitar y quitar según ficha, registrando auditoría sin exponer correos/tokens en logs. Member tiene `xmin` versionado y auditoría de creación/modificación.
- [x] Implementar `LastTenantAdminGuard` sobre la lectura de Dueños activos de E4; una cuenta con baja pedida no cuenta como Dueño. PostgreSQL confirma el error `Legal.AccountDeletion.LastAdmin` y la carrera concurrente.
- [x] Extender `InvitationIssuer` y su solicitud para incluir empresa/roles cuando corresponda; crear `POST /api/users/invitations`, reenviar y revocar bajo la ruta del área. El reenvío conserva el mismo Member y crea una invitación histórica independiente; al revocar una invitación sin cuenta se retira el Member huérfano y sus asignaciones sin borrar la Invitation revocada.
- [x] Al terminar o quitar una membresía, la disponibilidad de correos administrados se deriva de `UserTenantAccess`; se registra `OrganizationMembershipEnded` y se encola `ReviewLoginMethods` solo a otros métodos verificados disponibles. La prueba existente `LoginCodeTests.Inactive_managed_member_cannot_use_the_email_to_sign_into_personal_access` verifica que la baja invalida un código previamente emitido y retiene el nuevo envío; `OrganizationDomainsTests.Owner_verifies_removes_and_keeps_existing_login_method_associations` verifica que quitar el dominio mantiene el vínculo actual.
- [x] Preservar los cambios ya cerrados de invitación: ingresar para aceptar no crea Personal y el estado es por invitación. El E2E canónico existente no se modificó en este bloque.
- [x] Agregar el controller, declaración explícita de rutas, OpenAPI y pruebas de política/integración; el contrato y cliente front están sincronizados y verificados.
- [x] Ejecutar pruebas HTTP focales de Usuarios, aislamiento y concurrencia; `LoginCodeTests`, `OrganizationDomainsTests` y la suite PostgreSQL completa pasan.

### 4. Configuración y dominio de correo

- [x] Extender settings por organización con nombre, cultura, zona horaria y moneda desde catálogos; el formulario usa `TenantSettings.Version` y actualiza nombre/preferencias en una sola UoW. API, traducciones y prueba PostgreSQL de lectura/guardado/409 implementadas; build focal sin advertencias.
- [x] Ejecutar `OrganizationSettingsTests` con PostgreSQL para verificar lectura/guardado de configuración y 409 por versión obsoleta; el aislamiento y la invalidación de caché quedan para la puerta final.
- [x] Crear `platform.TenantDomains`, dominio global único y normalizado, token TXT de un solo propósito, estados `Pending | Verified | Removed`, persistencia con `xmin`, reader y auditoría.
- [x] Crear `OrganizationDomainService`, puerto `IDnsTxtLookup` y adaptador DoH; integrar TXT incorrecto y correcto, dominio ocupado, reintentos y retiro. Nunca imprimir el token.
- [x] Al verificarse un dominio, asignar `ManagedByTenantId` a métodos existentes; al crear/verificar un LoginMethod, consultar dominio verificado y asignarlo sin abrir otro acceso.
- [x] Probar con PostgreSQL real la asociación de correos existentes y nuevos al verificar, la retención de vínculos al retirar, que nuevas asociaciones se detengan al retirarlo, el TXT inválido y la respuesta/listado.
- [x] Probar con PostgreSQL las políticas de ingreso de correo administrado tras desactivar la membresía, el aislamiento (otra organización no lista ni retira un dominio ajeno), el DNS no disponible/TXT ausente, la retención del vínculo al retirar el dominio y la carrera por dominio único. Contratos OpenAPI/front y textos bilingües actualizados; build focal. Evidencia: `dotnet test ... --filter "FullyQualifiedName~OrganizationDomainsTests|FullyQualifiedName~LoginCodeTests"` pasó 12/12.

### 5. Página pública y moderación

- [x] Crear `PublicPage` y configuración bajo clasificación `public_site`; nombre, descripción, logo, contacto, slug de tenant, estado borrador/publicado y campos de bloqueo (`PublishBlockedAtUtc`, `PublishBlockedReason`, `PublishBlockedByUserId`).
- [x] Implementar logo como carga validada de imagen con tipo/tamaño acotados y lectura protegida por permiso; no almacenar rutas externas arbitrarias. Registrar en decisiones la solución de almacenamiento elegida y sus límites.
- [x] Implementar lectura/edición/publicación/despublicación del área bajo `publicpage.manage`; slug reutiliza `ReservedSlugs` y verificación de disponibilidad. Una página bloqueada queda en borrador y no se puede republicar; respetar `PublicSite.PublicPage.PublishBlocked`.
- [x] Añadir pruebas de firma/tamaño de logo, transición de publicación, moderación bloqueada, versiones, slug reservado/ocupado, aislamiento por organización y rutas; OpenAPI, contrato front y suite PostgreSQL pasan.

### 6. Auditoría, inicio y navegación B2B

- [x] Implementar consulta por cursor y traducida de auditoría de organización, con período, tema, persona y búsqueda; el día usa la zona de la organización. Prueba PostgreSQL: 50 entradas, cursor sin duplicados, filtro de tema/búsqueda, aislamiento entre organizaciones y correo/teléfono enmascarados. Contratos e inventario explícito de las dos rutas verificados.
- [x] Completar `AdminPanel` únicamente con las áreas y permisos disponibles en E6; no muestra navegación a módulos futuros.
- [ ] Los tableros Inicio-Org e Inicio-Miembro continúan «por refinar» en v64 y no se implementan con estados inventados. Se conserva el inicio existente y el flujo B2B se verifica en E2E; queda pendiente una definición aprobada si se quiere cambiar su presentación.
- [x] Auditoría agrega rutas Business con `audit.read`, recursos de auditoría es/en con paridad probada y formato UTC para que el front use `shared/ui/format`. El test de integración de Auditoría cubre 50 elementos; los tests de flujo/permiso de Usuarios, Empresas y su paginado focal se ejecutaron en las tareas de esas áreas.

### 7. Frontend por grupos aprobados

- [x] Implementar `/registro/empresa` desde `Registro-Empresa` y `M-Registro-Empresa`: correo/Google, términos, slug y estados de disponibilidad, primera empresa/CUIT opcional, límite, espera de aprobación y alta cerrada. E2E real cubre correo; Google requiere el proveedor OAuth local. WhatsApp queda para E8.
- [x] El 2026-10-03 el usuario aprobó copiar v64 de las once pantallas de Organización indicadas, incluidos móviles y diálogos. No se redibujan tableros existentes; los dos inicios siguen «para repensar».
- [x] Completar la copia de escritorio v64 para Usuarios, Usuario, Roles, Rol, Empresas, Empresa, Configuración, Página pública, Auditoría, Error-Org y Avisos, incluidos estados y diálogos. Capturas y diferencias están registradas en `docs/design/capturas/etapa-6/`.
- [x] Resolver las empresas de Usuario como lista de lectura con ⋮; editar roles y sumar empresa abren diálogos que guardan al momento. Pasan las pruebas focales de esos diálogos y el flujo real de Empresas.
- [x] Auditoría devuelve nombre afectado y empresa («Dónde»); el test de contrato cruza la respuesta del back con el contrato generado del front y `contracts:check` pasa.
- [x] Configuración usa «Dominio de correo» y explica que vínculos existentes se mantienen; Página pública usa «Nombre de la página».
- [x] Error-Org y Avisos siguen v64 sin sumar explicaciones fuera del tablero.
- [x] El usuario confirmó el 2026-10-03 que los tableros móviles v64 de las pantallas aprobadas están aprobados y deben copiarse; incluirlos en la implementación y comparación visual.
- [x] Los inicios de Organización y Miembro siguen «por refinar» en README v64; no se agregaron estados visuales nuevos y se mantuvo el flujo de acceso cubierto por E2E.
- [x] Verificar con PostgreSQL de prueba listados de 1 y 50 filas para Usuarios y Empresas, y 50 entradas para Auditoría; las pruebas contrastan conteos, cursores/paginado y aislamiento. La comparación visual está en `docs/design/capturas/etapa-6/`.

### 8. Recorridos reales, documentación y puertas

- [x] Completar el E2E real sin mocks con PostgreSQL y pickup aislados. Pasó 13/13 (0 fallos/omitidos, 14m45s): alta B2B por correo, sesión/F5/cambio a Personal; invitaciones con/sin cuenta desde `.eml`, empresa y rol por invitación, edición de empresa/configuración, borrador y publicación de página, auditoría con nombre/empresa, e invariantes de invitaciones. Google OAuth no se ejecuta porque el entorno local no lo configura.
- [x] Agregar `docs/features/organizaciones.md`, actualizar Identidad/Roles e informes de conducta; las guías existentes se referencian sin duplicar la receta de agregar área.
- [x] Actualizar informe de etapa y backlog para diferencias visuales/no bloqueantes; mantener en este plan los pendientes de intervención manual y aprobación de Inicio.
- [x] Ejecutar puerta back: build de solución con cero advertencias y 0 errores; suite completa, PostgreSQL/Testcontainers, inventario de rutas/aislamiento y generadores aplicables pasan. No se agregó guardia nueva del arnés.
- [x] Ejecutar puerta front: build, tests (720/720), lint (exit 0; avisos concentrados en lienzo v35 y reglas existentes), `contracts:check` y generadores pasan.
- [x] Ejecutar `HarnessTests` y `harness.test.ts` existentes; corregir referencias obsoletas de “Copiá de”; confirmar `{0,1,2,3,4,6}` y E5 abierta.
- [x] Ejecutar `npm run test:e2e:real` con PostgreSQL exclusivo y `Email:Delivery=PickupDirectory`: 13/13 casos, incluyendo el recorrido canónico y los recorridos E6, sin datos personales ni secretos en el resumen.
- [x] Comparar los tableros v64 y estados afectados en 1440×900 y 390×844; 46 pares (92 PNG) de lienzo/app verificados y diferencias con motivo en `docs/design/capturas/etapa-6/comparacion.md`.
- [x] Revisar cada estado implementado contra los tableros v64 aprobados y los móviles; registrar cinco diferencias y su motivo. No se inventaron diseños para estados sin tablero aprobado.
- [x] Subir `HarnessStage` de 4 a 6 en back/front usando etapas cerradas explícitas; E5 permanece pendiente.
- [ ] Recorrido manual Gmail (pendiente a pedido del usuario): registrar organización, F5, cambiar a Personal, invitar a una persona con cuenta y otra sin cuenta y aceptar ambos enlaces. E2E automatizado pasó; esta prueba manual no se declara aprobada hasta que se complete.
- [x] Aspire está apagado (`aspire ps` vacío); ambos repos quedan en `main`, limpios tras los commits de cierre y sin push.

## Decisiones tomadas

- El lienzo v64 es la fuente visual; el texto del plan maestro que cita lienzo v35/67 tableros está desactualizado y no se usará como catálogo visual.
- El salto `4 → 6` no cierra E5: el arnés debe representar etapas cerradas como conjunto explícito en las comprobaciones existentes, no con una comparación ordinal que marque etapas intermedias.
- La pantalla de alta empresarial soporta solo los canales que publica el backend. El botón de WhatsApp no se conecta antes de E8; se usará correo y Google.
- La aprobación explícita de v64 del 2026-10-03 habilita la implementación de todo el grupo Organización, incluidos móviles y diálogos. La confirmación del usuario prevalece sobre las marcas antiguas de `canvas.json`; no se redibujan tableros existentes.
- Las empresas no se borran físicamente desde E6: se puede cambiar su estado a Inactiva con `version`, de acuerdo con el diálogo validado, que conserva datos y miembros.
- Al crear una empresa, el miembro activo que la creó queda como miembro de la empresa y recibe el rol de sistema `CompanyAdmin`, para que la empresa nazca con un Administrador.
- El permiso por empresa combina los roles `Organization` (que valen para todas las empresas) y los roles asignados a esa empresa. Los roles company-scoped requieren membresía activa; una empresa inactiva entrega cero permisos efectivos.
- Los cambios de membresía que pueden afectar `CompanyAdmin` adquieren el lock por empresa dentro de la misma UoW y vuelven a leer el estado de la empresa bajo transacción. La reactivación de una membresía vinculada a cuenta exige identidad activa; una invitación sin cuenta puede prepararse con roles.
- Los nombres técnicos de roles de sistema se traducen en respuestas (`TenantAdmin` → Dueño/Owner, `CompanyAdmin` → Administrador/Administrator); los nombres elegidos por la organización se conservan.
- El logo se guarda en `public_site.PublicPages.LogoBytes` como `bytea`, limitado a PNG/JPEG/WebP, firma de archivo válida y 512 KiB.
- `Member.Version` usa la columna de sistema PostgreSQL `xmin` que ya estaba mapeada; la migración solo agrega `CreatedAtUtc`, la rellena con `JoinedAtUtc` (o el instante de migración si falta) y aplica `NOT NULL`. No se crea ni se elimina manualmente `xmin`.
- Al retirar una invitación sin cuenta pendiente, se conserva su fila y estado `Revoked`, y se marca `Member` como `Removed` junto con la limpieza de roles/membresías de empresa. Así el reenvío mantiene un solo miembro, y una invitación nueva posterior no deja asignaciones huérfanas. Las invitaciones para una cuenta existente conservan su Member para permitir re-invitarla.
- La finalización o deshabilitación de una membresía no modifica los métodos de ingreso personales. Los métodos `ManagedByTenantId` de esa organización dejan de ser disponibles por el índice `UserTenantAccesses`; `ReviewLoginMethods` se encola a otros métodos verificados, excluyendo además los métodos originados por una invitación de esa organización y métodos administrados por otra organización cuya membresía no esté activa.
- La edición de nombre, cultura, zona y moneda usa una versión única de `TenantSettings`; se fuerza su `UPDATE` aunque solo cambie el nombre global de `Tenant`, así `xmin` protege todo el guardado atómico sin una versión manual adicional ni una transacción parcial.
- Quitar un dominio verificado impide nuevas asociaciones automáticas, pero conserva `ManagedByTenantId` en los métodos existentes para que un correo de un exmiembro no vuelva a habilitar Personal. El usuario aprobó cambiar el diálogo de retiro porque su texto anterior prometía quitar esa protección.
- Los dominios se verifican consultando TXT mediante Cloudflare DNS-over-HTTPS, encapsulado detrás de `IDnsTxtLookup`; las pruebas reemplazan únicamente el puerto por un lector determinista.
- La migración nueva usa la convención existente `IVersioned`/`xmin`; verifiqué su SQL con `dotnet ef migrations script` y Npgsql omite las operaciones a la columna de sistema.
- La página pública persiste su imagen en `public_site.PublicPages.LogoBytes` como `bytea`: solo PNG/JPEG/WebP, firma de archivo comprobada y máximo de 512 KiB. La fila completa queda bajo RLS `IPublishedByBusiness`: la organización puede leer su borrador y una página publicada es legible públicamente para el futuro flujo E7; no se aceptan URLs externas de logo.
- El editor devuelve y exige `version` (xmin). Si la página todavía no existe, solo acepta `version=0` para crearla; el `GET` inicial muestra los datos de la organización como borrador hasta el primer guardado.
- La auditoría se pagina por cursor sobre `OccurredAtUtc` e `Id` en orden descendente; no cuenta toda una tabla append-only. El período “Hoy” usa la zona de la organización, y los campos de correo, teléfono y CUIT se enmascaran al proyectar el detalle. La búsqueda usa SQL parametrizado sobre el JSONB almacenado; nunca devuelve el texto crudo de cambios.
- El usuario decidió dejar pendiente por ahora el recorrido manual de Gmail. Los dos destinatarios y sus enlaces no se probaron manualmente, y el E2E con pickup no se presenta como sustituto de esa acción.
