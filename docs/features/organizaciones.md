# Organizaciones y administración B2B

Esta ficha describe el acceso empresarial de la plantilla multitenant. Una identidad es global; cada organización tiene su propio espacio Business y puede tener empresas y miembros. El acceso privado se determina por `tenant_id`, nunca por el host. Las reglas de aislamiento están en [multitenancy.md](../architecture/multitenancy.md); la autorización se apoya en [roles.md](roles.md).

## Alta de una organización

`POST /api/auth/business-signup` crea una organización desde `/registro/empresa`, después de verificar el correo o autenticar una cuenta existente con Google y aceptar los documentos vigentes. `GET /api/public-site/slug-availability` consulta si el slug está disponible, reservado o tomado. El alta aplica `BusinessSignup` y `MaxOwnedOrganizations`, crea la primera empresa (CUIT opcional) y asigna al creador como miembro activo, Dueño (`TenantAdmin`) de la organización y Administrador (`CompanyAdmin`) de la empresa inicial.

`TenantProvisioner` prepara el tenant y sus roles de sistema de forma idempotente y se comparte con la provisión de plataforma. El alta empresarial no crea un espacio Personal: una persona lo obtiene solo al entrar por el lado Consumer. El estado de alta puede dejar la organización pendiente de aprobación; un registro cerrado o el límite de organizaciones propias se presenta como estado de producto, no como error técnico.

## Usuarios e invitaciones

`GET /api/users/paged` y `GET /api/users/filter-counts` devuelven miembros de la organización con filtros, página y conteos. `GET /api/users/{memberId}` devuelve la ficha. `PUT /api/users/{memberId}`, `PUT /api/users/{memberId}/status` y `DELETE /api/users/{memberId}` editan roles de organización, estado y baja de membresía con control de versión donde corresponde. Las rutas de invitación son `POST /api/users/invitations`, `POST /api/users/invitations/{invitationId}/resend` y `DELETE /api/users/invitations/{invitationId}`.

La emisión y el encolado se delegan al `InvitationIssuer` compartido con la plataforma; el área de usuarios no duplica la creación de tokens ni la entrega por canal. Cada invitación conserva su propio estado e historial. Aceptar una invitación vincula o crea la identidad y concede el acceso Business invitante; aceptar no crea Personal. La baja o salida no puede quitar al último Dueño activo (`Legal.AccountDeletion.LastAdmin`). Los correos administrados bloquean el acceso Personal cuando no queda una membresía activa.

## Empresas y membresías

`/api/companies` contiene listado paginado y conteos, ficha, alta, edición versionada y cambio de estado (`GET .../paged`, `GET .../filter-counts`, `GET .../{id}`, `POST`, `PUT .../{id}`, `PUT .../{id}/status`). El CUIT, si existe, se valida y normaliza con `TaxId` y los catálogos habilitados; la zona horaria también sale del catálogo. El CUIT es único dentro de la organización. Una empresa se desactiva conservando datos y miembros.

`/api/companies/{companyId}/members` administra miembros y sus roles en esa empresa. `CompanyAdmin` es un rol de sistema por empresa. Los cambios de roles y estado usan versión y serializan las operaciones que podrían dejar a la empresa sin Administrador. No se puede quitar ni deshabilitar al último Administrador activo. La membresía de empresa no cambia la membresía de organización ni el acceso a otras empresas.

## Configuración y dominios de correo

`/api/organization/settings` lee y actualiza el nombre, la cultura, la zona horaria y la moneda de la organización en un solo cambio versionado; una versión vieja responde 409.

`/api/organization/settings/domains` lista y registra un dominio, solicita su verificación TXT, y lo retira con versión. Cada dominio es globalmente único. La verificación consulta DNS TXT y vincula métodos de ingreso existentes y futuros al `ManagedByTenantId` de la organización. Un método administrado solo se puede usar mientras haya membresía activa. Retirar un dominio impide asociaciones automáticas nuevas, pero conserva el vínculo de correos ya administrados para que una persona que salió no recupere el acceso Personal por ese correo.

## Página pública y auditoría

`/api/organization/public-page` lee y guarda el borrador versionado; `POST .../publish` y `POST .../unpublish` publican o retiran la página. El contenido y el logo pertenecen a `public_site`. El logo se valida por firma y tamaño, se guarda como bytes y no admite URL arbitraria. Moderación bloqueada impide volver a publicar hasta que se resuelva.

`/api/organization/audit` devuelve eventos traducidos con filtros y cursor. El día se interpreta en la zona de la organización; los datos de contacto y fiscales se enmascaran. La descripción nombra a la persona afectada y la columna `Dónde` identifica la empresa cuando corresponde. La consulta no cuenta toda la tabla append-only.

## Acceso, datos y verificación

Las rutas privadas declaran acceso Business y el permiso de organización o empresa que corresponde. Un tenant ajeno responde 404; las entidades privadas aplican RLS y el tenant siempre proviene del claim validado. Los cambios que editan recursos con versión devuelven 409 si otra operación guardó primero.

Los flujos de API, aislamiento, concurrencia e invitaciones se verifican en `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization` con PostgreSQL aislado. El E2E real crea sus datos en su propia base y usa `PickupDirectory`; no se siembran cuentas ni organizaciones de ejemplo en Development. El recorrido manual Gmail se rige por la puerta de Etapa 6 del [plan maestro](../plans/2026-09-27-plan-de-desarrollo.md#etapa-6-área-b2b-organización).
