# Etapa 6: área B2B (organización) — plan ejecutable

> Plan de implementación para esta etapa. Seguir el orden, completar las casillas con evidencia y commitear cada cambio coherente y verificado con rutas explícitas.

**Objetivo:** completar el acceso B2B desde el alta de una empresa hasta su administración diaria: usuarios e invitaciones, empresas y membresías, configuración y dominio de correo, página pública, auditoría y navegación.

**Arquitectura:** respetar `docs/architecture/backend.md`, `docs/architecture/multitenancy.md`, `docs/features/identidad.md`, `docs/features/roles.md`, `docs/guides/agregar-un-area.md` y las fichas de `docs/rules/`. En el front copiar los tableros aprobados de `../../../ArquitecturaBaseMutitenantFront/docs/design/lienzo/` v60. `Registro-Empresa` y su móvil están validados; el usuario aprobó en conjunto el 2026-10-02 las pantallas de Organización pendientes, incluidos móviles y diálogos. Roles/Rol y sus móviles ya fueron aprobados en E4.

**Restricciones de etapa:** trabajar en `main`, sin push; no crear cuentas ni organizaciones de ejemplo en Development; pruebas con sus propias bases; conservar los cambios de invitaciones `51f604f`, `feb67ec` y `7c83835`; no crear guardas de arquitectura ni tests nuevos del arnés salvo la verificación explícita del salto de etapas no contiguas; no marcar E5 cerrada al cerrar E6.

## Seguimiento

### 0. Línea de base y plan

- [x] Confirmar `main`, árbol limpio y último commit en ambos repos.
- [x] Confirmar Aspire apagado; no repetir la puerta de E4.
- [x] Leer las fichas, decisiones, informes, plan maestro y aprobaciones v60 requeridas.
- [x] Revisar este plan con un revisor de documentos y resolver las observaciones de secuencia, E2E canónico, formatos, arnés y puerta manual antes del código.
- [ ] Guardar la decisión de que E6 puede cerrarse antes de E5 en el arnés existente: la lista de etapas cerradas debe permitir `{0,1,2,3,4,6}` y E5 debe seguir abierta. Actualizar las comprobaciones existentes de back/front sin añadir un arnés paralelo.

### 1. Alta B2B y provisionamiento reutilizable

- [x] Escribir tests para límite `MaxOwnedOrganizations` y validación de los campos de `BusinessSignup`; quedan pendientes la prueba de conducta del alta real y de repetición con PostgreSQL.
- [x] Implementar `TenantProvisioner` reutilizable por alta B2B y futura plataforma, conservando las transacciones/UoW existentes y roles de sistema; la repetición de la misma operación no duplica tenant, owner, empresa ni settings. Commit `813ff0c`.
- [x] Agregar catálogo persistido de slugs reservados con los nombres validados por el tablero: `www`, `app`, `api`, `auth`, `admin`, `empresas` y `plataforma`; el lector distingue disponibles, reservados y tomados. Commits `bd91f9e` y `fb12872`.
- [x] Exponer `GET /api/public-site/slug-availability` con estados disponible/reservado/tomado y formato validado.
- [x] Traducir la carrera de alta contra el índice único de `Tenants` en `Tenancy.TenantSlug.Taken`.
- [x] Crear la primera empresa con CUIT opcional y zona inicial; validar el CUIT mediante `TaxId`/catálogo y guardarlo normalizado.
- [x] Implementar el alta por correo y por Google, con aceptación vigente y sin crear acceso Personal. Aplicar modo de alta y máximo de organizaciones propias; devolver el estado de aprobación.
- [x] Agregar ruta HTTP con antiforgery y cookie de identidad, declarar la ruta anónima existente, textos es/en, actualizar OpenAPI y prueba de integración para operación/replay y ausencia de Personal. El cliente generado y test de contrato back/front quedan pendientes del front.
- [ ] Extender y ejecutar `test:e2e:real`: comprobar alta por correo y Google donde el proveedor local esté configurado, sesión B2B, `/org`, F5, cambio a Personal y que no exista espacio Personal creado por el alta empresarial. El daemon Docker no está disponible ahora, así que falta correr la prueba PostgreSQL.
- [ ] Verificar el build y los tests dirigidos del alcance; commitear plan, modelo/API y recorrido en cambios coherentes con rutas explícitas.

### 2. Empresas y roles por empresa

- [x] Crear `Company`, configuración EF/migración e índices tenant-owned; la prueba de persistencia quedó bloqueada por falta del daemon Docker y se ejecutará en la puerta.
- [x] Crear `TaxId` y `ArgentineCuitValidator` según `docs/rules/identificacion-fiscal.md`; agregar `TaxIdTests` y pruebas de propiedades para dígito, normalización, valor inválido y datos históricos nulos. Commits `234fc72`.
- [x] Implementar servicio y API de listado paginado, conteos por estado, ficha, alta, edición concurrente (`version` y 409) y cambio de estado activo/inactivo; validar zona horaria y normalizar CUIT desde catálogos. Pruebas unitarias dirigidas para listado, conteos, alta, primer Administrador y concurrencia de estado. Commit pendiente.
- [ ] Agregar integración con PostgreSQL para tenant isolation, estado/versiones, TaxId único y conteos/listados con 1 y 50 empresas.
- [ ] Implementar membresías de empresa y asignación de roles mediante `RoleAssignment.CompanyId`, reutilizando catálogos y `RoleService`; filtrar empresas/roles por tenant y nunca aceptar un ID ajeno.
- [ ] Proteger quitar o deshabilitar al último `CompanyAdmin` activo; cubrir concurrencia en dos solicitudes y miembros con baja pedida.
- [ ] Conectar el catálogo de empresas y roles a los flujos de Invitaciones, manteniendo los estados individuales de cada invitación.
- [ ] Actualizar inventario de rutas, OpenAPI, contrato back/front, permisos existentes, textos bilingües y pruebas de aislamiento; build y pruebas focales.

### 3. Usuarios e invitaciones de la organización

- [ ] Implementar lectura paginada de miembros con filtros, conteos y ficha; incluir estados activo, invitado, vencido, deshabilitado, baja pedida y correo administrado. Probar 1 y 50 registros y los límites de página.
- [ ] Implementar edición concurrente con `version`; habilitar, deshabilitar y quitar según ficha, registrando auditoría sin exponer correos/tokens en logs.
- [ ] Implementar `LastTenantAdminGuard` sobre la lectura de Dueños activos de E4; una cuenta con baja pedida no cuenta como Dueño. Cubrir última persona, concurrencia y recursos ajenos.
- [ ] Extender `InvitationIssuer` y su solicitud para incluir empresa/roles cuando corresponda; crear `POST /api/users/invitations`, reenviar y revocar bajo la ruta del área. No duplicar emisión, envío ni estados ya resueltos en 3c.
- [ ] Al quitar a un miembro, desactivar sus métodos de correo administrados por la organización, preservar la seguridad de otros métodos, registrar el evento y encolar el aviso requerido; cubrir `ManagedEmailTests` y el efecto de un dominio no verificado/verificado.
- [ ] Verificar que los cambios ya cerrados de invitación sigan intactos: ingresar para aceptar no crea Personal y el estado es por invitación.
- [ ] Agregar pruebas API/integración/aislamiento y actualizar contratos generados; build focal.

### 4. Configuración y dominio de correo

- [ ] Extender settings por organización con nombre, cultura, zona horaria y moneda desde catálogos, validando recursos; edición con concurrencia cuando la ficha lo requiera.
- [ ] Crear `platform.TenantDomains`, dominio global único y normalizado, token TXT de un solo propósito, estados `Pending | Verified`, persistencia, reader y auditoría.
- [ ] Crear `TenantDomainService`, puerto `IDnsTxtLookup` y adaptador; tests deterministas del puerto para registro, verificación correcta/incorrecta, dominio en uso, retiro y reintentos. Nunca imprimir el token.
- [ ] Al verificarse un dominio, asignar `ManagedByTenantId` a métodos existentes; al crear/verificar un nuevo LoginMethod, consultar dominio verificado y asignarlo sin abrir otro acceso.
- [ ] Cubrir que los métodos administrados no permitan iniciar sesión si la membresía dejó de estar activa, que retirar el dominio limpie la administración de métodos según la política escrita y que otra organización no vea datos.
- [ ] Probar rutas nuevas de integración/aislamiento, actualizar contratos y textos bilingües; build focal.

### 5. Página pública y moderación

- [ ] Crear `PublicPage` y configuración bajo clasificación `public_site`; nombre, descripción, logo, contacto, slug de tenant, estado borrador/publicado y campos de bloqueo (`PublishBlockedAtUtc`, `PublishBlockedReason`, `PublishBlockedByUserId`).
- [ ] Implementar logo como carga validada de imagen con tipo/tamaño acotados y lectura protegida por permiso; no almacenar rutas externas arbitrarias. Registrar en decisiones la solución de almacenamiento elegida y sus límites.
- [ ] Implementar lectura/edición/publicación/despublicación del área bajo `publicpage.manage`; slug reutiliza `ReservedSlugs` y verificación de disponibilidad. Una página bloqueada queda en borrador y no se puede republicar; respetar `PublicSite.PublicPage.PublishBlocked`.
- [ ] Añadir tests de validación, conducta, permisos, aislamiento y bloqueo de moderación; rutas, OpenAPI, textos y contrato.

### 6. Auditoría, inicio y navegación B2B

- [ ] Implementar consulta paginada y traducida de auditoría de organización, con filtros que muestre el tablero y sin filtrar campos sensibles.
- [ ] Completar `AdminPanel` únicamente con áreas disponibles en E6 y sus permisos; no mostrar navegación a módulos futuros que aún no existen.
- [ ] Implementar servicios, datos y rutas de Inicio-Org e Inicio-Miembro que no dependan de la presentación; las pantallas, sus enlaces y los estados visibles esperan la aprobación indicada en §7. No conservar ni inventar estados que contradigan o no estén cubiertos por v60: si falta un estado necesario, mostrarlo para aprobación antes de programarlo.
- [ ] Agregar pruebas de ruta/permisos, textos es/en, formato de fechas y 1/50 elementos en listados.

### 7. Frontend por grupos aprobados

- [ ] Implementar `/registro/empresa` desde `Registro-Empresa` y `M-Registro-Empresa`: canal disponible de correo/Google, aceptación de términos, slug y disponibilidad/reservados, primera empresa/CUIT opcional, límite, espera de aprobación y alta cerrada. El canal WhatsApp queda para E8 porque todavía no está implementado.
- [x] Aprobación agrupada recibida el 2026-10-02 para `Inicio-Org`, `M-Inicio-Org`, `Inicio-Miembro`; `Usuarios`, `Usuario`, `M-Usuarios`, `M-Usuario`; `Empresas`, `Empresa`, `M-Empresas`, `M-Empresa`; `Configuracion`, `M-Configuracion`, alta/verificación/retiro de dominio; `Pagina-Org`, `Pagina-Org-Despublicar`; `Auditoria-Org`, `M-Auditoria-Org`, incluidos móviles y diálogos. Implementar copiando lienzo v60. No volver a pedir visto bueno de Roles/Rol ni Registro-Empresa.
- [ ] Con la aprobación, implementar las pantallas, móviles, diálogos y estados de la lista anterior copiando lienzo v60; usar `shared/ui`, fichas front, clientes generados, permisos y componentes del área Roles. No redibujar pantallas existentes.
- [ ] Hacer el flujo real de 1 y 50 filas en Usuarios, Empresas y Auditoría con PostgreSQL de prueba propia; comparar orden, filtros, conteos, estados y paginado contra las maquetas.

### 8. Recorridos reales, documentación y puertas

- [ ] Completar E2E real sin mocks y con PostgreSQL y pickup de `.eml` aislados: alta de organización por correo, alta por Google donde el proveedor local esté configurado, sesión B2B, F5, cambio a Personal; invitación y aceptación con cuenta existente y sin cuenta; dominio gestionado; empresa/rol por invitación; edición/deshabilitación; página borrador/publicada/bloqueada; aislamiento de otra organización. Sin revelar correos, tokens, códigos ni datos personales en la salida.
- [ ] Agregar `docs/features/organizaciones.md`, actualizar sección de registro B2B de `docs/features/identidad.md` y documentar conducta/decisiones pertinentes en Roles/Invitaciones; actualizar AGENTS/CLAUDE de carpetas nuevas y guías necesarias sin duplicar la receta de agregar área.
- [ ] Actualizar informes de etapa y backlog solo para mejoras que no rompan un flujo real ni la seguridad.
- [ ] Ejecutar puerta de back: `dotnet build ArquitecturaBaseMultitenant.slnx` con cero advertencias, `dotnet test`, integración/rutas/aislamiento y generadores de referencia aplicables. Actualizar `ExplicitRouteInventoryTests` con una declaración explícita por cada ruta nueva y ejecutar las pruebas de aislamiento de cada ruta con recursos propios y ajenos.
- [ ] Ejecutar puerta de front: `npm run build`, `npm run lint`, `npm test`, `npm run contracts:check` y generadores aplicables. Todo tipo nuevo que se muestre debe usar `shared/ui/format` y `shared/ui/fields`, con casos agregados a `format-cases.json`.
- [ ] Ejecutar `HarnessTests` y `harness.test.ts` existentes en ambos repos; revisar que los “Copiá de” de las fichas modificadas apunten a archivos reales; confirmar lista cerrada `{0,1,2,3,4,6}` y que E5 siga abierta sin nuevas guardas fuera del plan.
- [ ] Ejecutar `npm run test:e2e:real` en verde con PostgreSQL exclusivo y `Email:Delivery=PickupDirectory`, exigiendo `E2E_PICKUP_DIR` absoluto. Preservar el flujo canónico: el runner crea una persona y una organización en su base, registra la cuenta personal con el código leído del `.eml`, sale, entra con su cuenta por la puerta empresa, verifica `/org`, F5, cambio a Personal y logout; sumar los recorridos E6 sin mocks. Conservar salidas sin datos personales ni secretos.
- [ ] Comparar visualmente cada tablero aprobado/estado afectado en 1440×900 y 390×844, guardar capturas de maqueta y producto en `docs/design/capturas/etapa-6/` y anotar cada diferencia con su motivo.
- [ ] Ejecutar el recorrido manual requerido: registrar la organización, ingresar, comprobar `/org`, F5, cambiar a Personal; invitar desde Usuarios a una persona con cuenta y a otra sin cuenta, recibir ambos correos de invitación en Gmail y aceptar ambos enlaces. Si falta una acción/credencial personal, pedir solo esa intervención al usuario y registrar la prueba como pendiente; no declarar aprobada ni cerrar la puerta de E6 hasta que se complete.
- [ ] Comparar cada estado codificado con el lienzo v60 y su contraparte móvil. Corregir cualquier diferencia; si la maqueta no cubre un estado de producto necesario o una diferencia no puede corregirse sin cambiarlo, pausar esa pantalla para aprobación del usuario y registrar el motivo en el informe, sin inventar una solución visual.
- [ ] Subir `HarnessStage` de 4 a 6 en back/front usando etapas cerradas explícitas; E5 permanece pendiente.
- [ ] Apagar Aspire si se levantó y comprobar ambos repos limpios en `main`, sin push.

## Decisiones tomadas

- El lienzo v60 es la fuente visual; el texto del plan maestro que cita lienzo v35/67 tableros está desactualizado y no se usará como catálogo visual.
- El salto `4 → 6` no cierra E5: el arnés debe representar etapas cerradas como conjunto explícito en las comprobaciones existentes, no con una comparación ordinal que marque etapas intermedias.
- La pantalla de alta empresarial soporta solo los canales que publica el backend. El botón de WhatsApp no se conecta antes de E8; se usará correo y Google.
- La aprobación agrupada del 2026-10-02 habilita la implementación de todo el grupo Organización incluido móviles y diálogos; no se redibujan tableros existentes.
- Las empresas no se borran físicamente desde E6: se puede cambiar su estado a Inactiva con `version`, de acuerdo con el diálogo validado, que conserva datos y miembros.
- Al crear una empresa, el miembro activo que la creó queda como miembro de la empresa y recibe el rol de sistema `CompanyAdmin`, para que la empresa nazca con un Administrador.
- La estrategia de almacenamiento del logo se definirá inspeccionando las capacidades reales ya presentes en los repos; se documentará antes de cerrar esa tarea.
