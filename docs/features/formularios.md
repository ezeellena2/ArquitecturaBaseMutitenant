# Formularios

**Revisado:** 2026-11-27

Módulo quitable `Forms`: una organización arma catálogos de opciones y, desde la E14, formularios que manda por WhatsApp. Hoy tiene solo los catálogos (E12, [spec](../plans/formularios/12-catalogos.md)). Es un módulo aparte de WhatsApp: se hablan por puertos neutrales del núcleo, y el núcleo nunca referencia `*.Modules.Forms`. Las reglas generales están en [AGENTS.md](../../AGENTS.md), [modulos](../rules/modulos.md) y [modulos-habilitados](../rules/modulos-habilitados.md); las decisiones de producto, en la [hoja de ruta de formularios](../plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md).

## Catálogos

- **Rutas:** `/api/organization/forms/catalogs`, en `BusinessFormCatalogsController`, con `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `[HasPermission]`. Con la funcionalidad `forms` apagada responden 404, y los catálogos y sus opciones se conservan (0069). Un catálogo ajeno responde 404, igual que uno inexistente.
- **Permisos:** `formcatalogs.read` («Ver catálogos») y `formcatalogs.manage` («Administrar catálogos»), que incluye al primero, en el área «Catálogos» de un rol. Solo el Dueño los recibe por la migración `SystemRoleFormCatalogPermissions`.
- **Tres orígenes:**
  - **a mano:** hasta 10.000 opciones, con código, texto (30), detalle (300) y, en uno tipado, un valor entero, decimal o de fecha, guardado como texto canónico;
  - **de los miembros:** filtros por nombre, rol, empresa y estado, con una persona de ejemplo y «Sin incluir a quien lo recibe»; los lee `MembersCatalogSource` con `IMemberDirectoryReader`, del núcleo;
  - **de los contactos:** los de la bandeja Clientes, filtrados por etiqueta, por `IStructuredContactSource` (`Application/Interfaces/Integrations/StructuredData/`). Sin WhatsApp, el puerto queda en su versión apagada y el catálogo dice `ChannelUnavailable`. El filtro «Origen» del listado sigue ofreciendo este origen aunque la organización no tenga WhatsApp (decisión 288).
- **Guardar:** un `PUT` con la versión, las opciones nuevas, cambiadas, movidas o borradas, y «Ordenar por…» (224). Guardar sin cambios toca solo la fila del catálogo, para comparar la versión (409 común), sin reescribir ni auditar opciones. Una opción que algo usa no se borra (`Forms.Catalog.ItemInUse`, con sus motivos en `uses`). «Dar de baja» y «Reactivar» mandan el mismo `PUT`.
- **Importar y exportar:** una planilla `.xlsx`, o lo pegado como CSV, pasa por una revisión con sus altas, cambios, bajas y errores en páginas, y se aplica con la misma versión y el mismo archivo (`sha256`). Más de un 25 % de bajas o un 10 % de errores piden confirmación (`Forms.CatalogImport.ConfirmationRequired`), y otra operación de archivo de la misma persona da `Forms.CatalogImport.Busy`. Exportar y la plantilla nombran las columnas igual. La revisión, aplicar y exportar toman una barrera por persona (`FormWorkLeases`, `IFormWorkLease`), con un tope por organización y otro de la instalación (C11); los topes de archivo son técnicos y están en `appsettings`.
- **Vista previa:** `POST previews` arma hasta 200 opciones, con su texto y su detalle, y el total exacto, sin escribir.
- **Asistente:** `catalogoptions.edit@1` prepara cambiar las opciones de un catálogo a mano activo, y la persona confirma en el editor ([ADR 0068](../decisions/0068-acciones-del-asistente.md)). Queda registrada y sin ofrecer hasta que se califique.
- **Datos personales:** el nombre del perfil de WhatsApp se guarda cifrado en el mensaje, y un contacto sin nombre toma el de su perfil una sola vez (C14). `FormsPersonalDataPurgeTests` dice quién purga cada tabla de Formularios.

En el front, las pantallas están en `src/modules/forms` (listado, editor, modal de opciones, importar, filtros y la propuesta del asistente), aportadas por `registration.ts` al panel «Gestión de formularios».

## Carpetas

| Carpeta | Responsabilidad |
|---|---|
| `src/ArquitecturaBaseMultitenant.Domain/Forms` | catálogo, opción, barrera, topes de texto y errores |
| `src/ArquitecturaBaseMultitenant.Application/Modules/Forms` | servicios, helpers, fuentes, validadores, modelos y textos |
| `src/ArquitecturaBaseMultitenant.Infrastructure/Modules/Forms` | modelo EF, repositorios, lectores y la planilla |
| `src/ArquitecturaBaseMultitenant.Api/Modules/Forms` | rutas, contratos HTTP, la barrera de archivos y el aporte al contrato |
| `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Forms` | reglas puras |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Forms` | validadores, políticas, orden y la acción del asistente |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Forms` | HTTP, aislamiento, concurrencia, importar, volumen y migración |
| `tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/Forms` | forma del módulo, ejemplos del contrato y purga |
| `ArquitecturaBaseMutitenantFront/src/modules/forms` | pantallas, hooks y textos del front |

## Lo verifica

- Domain: `FormCatalogTests`, `FormCatalogItemTests` y `FormOptionTitleTests` (el corte de 30).
- Application: `CreateFormCatalogRequestValidatorTests`, `UpdateFormCatalogRequestValidatorTests`, `FormCatalogFiltersPolicyTests`, `FormCatalogImportPolicyTests`, `FormCatalogPositionEditorTests`, `FormCatalogPreviewBuilderTests` y `FormCatalogActionSourceTests`.
- API: `FormCatalogServiceTests`, `FormCatalogConcurrencyTests`, `FormCatalogImportServiceTests`, `FormCatalogSpreadsheetTests`, `FormCatalogIsolationTests`, `FormCatalogPermissionTests`, `FormCatalogPermissionUpgradeTests`, `FormCatalogPersistenceTests`, `FormCatalogVolumeTests` (10.000 opciones, con `EXPLAIN`), `FormCatalogsMigrationTests`, `FormWorkLeaseTests`, `FormCatalogFileSlotTests`, `FormsModuleOffKeepsDataTests`, `RouteIsolationMatrixTests` y `FormsContractExamplesTests`, que graba `docs/contracts/examples/forms.json`.
- Arquitectura: `FormsContractExamplesCoverageTests`, `FormsPersonalDataPurgeTests`, `ModuleControllersTests`, `ApplicationServicesTests` y `VersionedContractTests` de Forms.
- Front: los tests de `src/modules/forms` (páginas, `lib/` y `registration`) y `contracts:check`, que valida cada ejemplo de `forms.json` contra `openapi.json`.
