# Plataforma

Administración global operada con `access=platform`, separada de la administración Business de cada organización. El desarrollo y su puerta están en el [plan de E5](../plans/2026-10-03-etapa-5-plataforma.md); E5 permanece abierta hasta completar esa puerta.

## Operadores y permisos

Una cuenta operadora tiene una asignación global versionada de `PlatformRole`, independiente de los roles privados de organización. El catálogo de permisos sigue siendo `PlatformPermissions`; no se agregan claves para representar roles.

| Rol | Permisos |
|---|---|
| Owner | Todas las claves de `PlatformPermissions.All` |
| Support | `platform.tenants.read`, `platform.accounts.read`, `platform.recoveries.manage`, `platform.audit.read` |

Las colecciones de permisos son inmutables. `platform.PlatformRoleAssignments` tiene una asignación única por UserId, FK a la identidad global y versión xmin. `PlatformPermissionService` consulta el rol persistido sin cachear permisos revocables: exige cuenta Active y el indicador IsPlatformOperator; un indicador sin asignación nunca concede permisos. Las rutas y la protección del último Owner se completan en T16.

La migración de E5 convierte los operadores anteriores a Owner sin crear cuentas ni espacios personales. El seed técnico preserva una asignación existente (incluido Support) y solo agrega Owner al primer operador o a un indicador legado sin asignación. Se ejecuta dentro de la transacción/lock global del seed.

## Configuración global

`GET/PUT /api/platform/settings` requiere `platform.settings.manage`. Expone los modos de registro de personas y organizaciones y el límite de organizaciones propias (mínimo uno). La gracia de baja permanece en 30 días. La edición exige motivo y versión xmin; un conflicto devuelve 409 sin guardar ni agregar un evento. El cambio y su SecurityEvent con el operador se confirman juntos; la caché global se invalida después del commit. Support y los accesos Consumer/Business no pueden consultar ni cambiar esta configuración.

## Organizaciones: consulta y estado

`/api/platform/tenants/paged`, `/filter-counts` y `/{id}` requieren `platform.tenants.read` y muestran exclusivamente Business. La búsqueda compartida no distingue acentos/case y escapa comodines; incluye nombre, slug y contacto del Dueño. Cada dimensión de conteos mantiene los otros filtros. La ficha de Personal se devuelve como 404.

El registro global conserva origen, fecha y cuenta que registró y versión xmin. Los conteos viven en `platform.TenantCounters`, una fila técnica 1:1 por espacio, creada por trigger. La fecha histórica se recupera de las incorporaciones disponibles; si no existe evidencia queda null. Los totales de empresas y miembros no retirados se mantienen mediante deltas atómicos en triggers de sus escrituras bajo RLS, sin consultar esas filas privadas desde Plataforma. Esos deltas no actualizan Tenants: sumar un miembro o una empresa no invalida una versión leída para cambiar el estado. Los cambios de estado con versión vieja siguen dando 409. La migración conserva los agregados existentes antes de retirar las columnas anteriores. El contacto se proyecta de la identidad global del Dueño actual (o del registrante autoservicio cuando aún no hay asignación).

Rechazar (PendingApproval→Closed), suspender (Active→Suspended), reactivar (Suspended→Active) y cerrar (PendingApproval/Suspended→Closed) requieren `platform.tenants.manage`, motivo y versión. Los cambios guardan SecurityEvent con actor y destino. Solo el rechazo encola un aviso; suspender, reactivar, cerrar, aprobar y reintentar no tienen aviso previsto. Las bajas de acceso revocan únicamente los tokens de esa organización; después del commit se invalida su estado en caché. La identidad y los accesos Personal conservan sus sesiones.

Los avisos de rechazo y moderación se dirigen exclusivamente a Dueños con membresía e identidad activas, desde el índice global de accesos. Se utiliza su principal verificado si tiene canal y no está administrado por otra organización; en caso contrario, los métodos disponibles del resolver de Organización, excluyendo también los administrados por otra organización. No se envían al registrante retirado ni se incluye el motivo interno del operador. El cambio y el outbox se confirman juntos.

`POST /api/platform/tenants` crea una organización activa con primera empresa e invitación al Dueño; recibe nombre, primera empresa, correo, cultura y zona. Genera el slug desde el nombre; ante colisión/reserva/nombre inválido usa una dirección `org-{operationId}`. El motivo de creación es la descripción traducida de esa acción. No crea identidad ni Personal: el Member queda Invited y al aceptar recibe TenantAdmin y CompanyAdmin sobre la primera empresa.

`POST /{id}/approve` y `/retry-provisioning` reutilizan TenantProvisioner. Completan roles/settings faltantes o comprueban lo existente. Aprobar recibe motivo y versión; reintentar recibe solo `version` y registra el motivo fijo traducido «Reintentar el alta», sin pedir texto al operador. Un retry de alta incompleta puede recuperar al miembro del registrante autoservicio activo; no adivina una empresa ni asigna el rol al operador. Si no puede identificar un responsable, devuelve 409 sin abandonar Provisioning. Los cambios que encolan avisos exigen Idempotency-Key; replay conserva respuesta/eventos/envíos. E5 permanece abierta.

`TenantProvisioner.StageBusinessOwnerAsync` completa los roles de sistema, la configuración y las asignaciones del Dueño invitado o activo, dentro del alcance Business y la UoW del llamador. Reutiliza lo existente y rechaza preferencias incompatibles; no crea identidades ni membresías Personal. La entrada `StageAsync` de E3/E6 conserva sus restricciones. El repositorio transaccional puede releer una organización Added antes del único guardado final.

## Módulos por organización

El catálogo `Domain/Features/Features.All` está vacío en la plantilla; las piezas del núcleo siempre existen. Cada módulo real declara alcance Consumer/Business/Both y su default. `platform.TenantFeatures` conserva enabled, origen Plan/Manual/Trial, vencimiento UTC y versión xmin por espacio y clave. Trial exige fecha; en el límite exacto ya está apagado.

`GET /api/platform/tenants/{id}/features` requiere tenants.read y proyecta únicamente módulos compatibles con Business. `PUT /{featureKey}` requiere tenants.manage, motivo y versión (0 para la configuración inicial sin fila). Guarda la habilitación y SecurityEvent para el destino juntos; las ediciones viejas y carreras del alta devuelven 409 sin evento adicional. Personal e ids inexistentes responden 404. Support puede consultar y no modificar.

FeatureManagement es scoped. El filtro Tenant utiliza el contexto activo firmado y una caché por espacio con filas crudas, invalidada después del commit; el reloj se evalúa en cada consulta. El apagado global en configuración prevalece. El middleware de API oculta el módulo con 404 antes de comprobar permisos, después de validar autenticación/acceso/estado; la librería FeatureGate y su handler mantienen la misma respuesta. `/api/me.features` contiene solo claves efectivas del acceso activo y permanece presente con lista vacía cuando corresponde. El consumo front de módulos sigue pendiente en T08.

## Moderación de páginas públicas

`GET /api/platform/tenants/{id}/public-site` exige `platform.tenants.manage` y devuelve solo metadatos de publicación, nombre, slug y versión; no consulta contenido, logo ni contacto. Confirma el SecurityEvent de consulta antes de entrar al alcance privado. Support conserva el listado global de organizaciones, pero no entra a los datos privados de moderación.

`POST .../unpublish` y `POST .../allow-publish` requieren motivo, versión e Idempotency-Key. Reutilizan la página de E6: despublicar deja borrador bloqueado con operador/fecha/motivo; permitir retira el bloqueo y conserva el borrador hasta que la organización decida publicarlo. El cambio, su SecurityEvent, la auditoría de organización y los avisos a sus Dueños activos se confirman juntos. Una edición vieja devuelve 409 sin aviso ni evento de cambio adicional; el registro previo del intento de entrada permanece. El replay no duplica cambios ni correos. Una organización no puede publicar mientras el bloqueo siga vigente.

## Dominios de correo de una organización

`GET /api/platform/tenants/{id}/domains` lista los dominios vigentes (pendientes y verificados) de una organización Business, con su desafío TXT mientras estén pendientes. `POST /{domain}/verify` (con Idempotency-Key) y `DELETE /{domain}` reciben motivo y versión en el cuerpo; `{domain}` es el nombre normalizado. Si el DNS no responde, verificar da 503 `Settings.TenantDomain.DnsUnavailable`. Las tres rutas exigen `platform.tenants.manage`: Support y los accesos Consumer/Business reciben 403, Personal o un id inexistente 404, y un dominio de otra organización 404.

Reutilizan la lógica de E6: verificar consulta el TXT por el puerto de DNS fuera de la transacción y, adentro, toma el lock del dominio, relee con la versión vista y vincula a la organización los métodos verificados existentes; los nuevos se vinculan al verificarse. Quitar detiene las vinculaciones futuras y conserva `ManagedByTenantId` en los métodos ya vinculados, así que un exmiembro no vuelve a ingresar con ese correo. La consulta confirma su SecurityEvent de entrada antes de `ITenantScope.Enter`; el cambio, su SecurityEvent (`OrganizationDomainVerified` u `OrganizationDomainRemoved`, con el diff de `domain.verified` y `domain.present`, sin el nombre del dominio) y la auditoría de la organización se confirman juntos. La versión vieja da 409 sin evento de cambio; sin TXT, 409 `Settings.TenantDomain.TxtNotFound`; un dominio ya verificado, 409 `Settings.TenantDomain.AlreadyVerified`.

## Verificación de accesos y flujos

Los `SecurityEvent` de configuración, módulos, cambios de estado y moderación conservan el diff técnico de claves con `before`/`after`, el destino de organización o cuenta y la IP enmascarada. El diff solo admite booleanos, cantidades, enums y UTC; omite valores iguales y no recibe nombre, correo, dominio ni contenido de la página. El motivo interno va aparte. Las entradas técnicas sin modificación conservan un diff vacío; un retry que no cambia el estado no inventa una transición.

IPv4 conserva tres octetos; IPv6 conserva los primeros 48 bits y la IPv4 mapeada se normaliza. El interceptor existente agrega esa evidencia solo a eventos nuevos, sin reemplazar el diff; bootstrap y workers sin petición conservan IP null. La migración mantiene los eventos históricos sin inventar valores desconocidos. El seed identifica la cuenta destinataria del alta de operador. La lectura de «Ingresos» desde identity.LoginAudits, sin direcciones ni códigos OTP, se conecta en T17.

El acceso privado pasa por `PlatformScopeAccessService`: valida acceso Platform, permiso persistido `platform.tenants.manage` y motivo; confirma el SecurityEvent previo en una UoW. `PlatformActionGuard` entra con `ITenantScope.Enter` después, sin transacción abierta, y restaura el alcance al disponer la lease. La operación privada usa luego su única UoW. Una falla posterior conserva el registro de entrada.

- `PlatformRoleTests`: catálogo completo de Owner, alcance de Support, inmutabilidad y asignación global/versionada.
- `PlatformPermissionServiceTests`: permisos por rol, cuenta inactiva, asignación ausente y cuenta no operadora.
- `PlatformOperatorPersistenceTests`: versión, transacción, unicidad y actualización real desde operador legado en PostgreSQL aislado, sin Personal.
- `ProductionSeedTests`: Owner inicial, repetición del seed y preservación de una sola asignación.
- `PlatformSettingsTests` de dominio y servicio: validación sin mutación, configuración versionada, evento y caché después del commit.
- `PlatformSettingsTests` de integración: permisos reales, validación, lectura actualizada y 409 sin evento adicional en PostgreSQL aislado.
- `PlatformTenantTests`: registro global inmutable/UTC y eventos con destino y operador.
- `PlatformTenantAdministrationTests`: paginado/filtros/búsqueda/contacto, 401/403/404, transiciones y 409, revocación Business con Personal operativo, y conteos actualizados sin lectura de tablas privadas.
- `PlatformScopeTests` de servicio e integración: sin permiso/motivo no registra ni entra; el evento está confirmado antes de consultar filas privadas, Support no entra y una excepción restaura el alcance.
- `PlatformProvisioningTests`: Dueño invitado sin identidad, replay sin duplicados y preferencias incompatibles sin reemplazar lo existente.
- `PlatformTenantProvisioningHttpTests`: 401/403, creación/aceptación/replay, aprobación/retry, reparación y rollback, versiones viejas, idempotencia y concurrencia sin duplicados.
- `FeatureGateTests`, `PlatformFeatureAdministrationTests` y `ModuleControllersTests`: apagado, emergencia, permisos, expiración con caché, aislamiento de acceso, edición y carrera, y núcleo sin gates. El contrato de `/api/me.features` lee el backend y el tipo generado consumido por el front.
- `PublicPageModerationTests`: HTTP/PG real, 401/403/404, metadatos sin contenido, permisos Owner/Support, motivo, versión, replay, avisos sin duplicados, auditoría y aislamiento del borrador; `PublicPagesTests` conserva la regresión E6.
- `PlatformOrganizationNoticeTests`: rechazo y moderación en PostgreSQL/outbox real; principal seguro, alternativa disponible, correo administrado ajeno, registrante retirado e identidad suspendida. Ningún motivo interno se incluye en los correos. El recorrido de estados comprueba la ausencia de avisos no previstos.
