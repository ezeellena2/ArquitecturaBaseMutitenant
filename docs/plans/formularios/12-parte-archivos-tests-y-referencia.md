# E12 · Catálogos base: archivos, tests y referencia (spec, parte)

**Estado:** pendiente · parte de la [spec de la E12](12-catalogos.md), que dice qué leer y cómo se cierra.

## 9. Archivos

Cada pieza nueva copia la **estructura** del archivo modelo de este repo; no se copia código de ArquitecturaBase. **Abreviaturas:** `D/`, `A/`, `I/`, `P/` son `src/ArquitecturaBaseMultitenant.{Domain,Application,Infrastructure,Api}/`, y `F/` es `ArquitecturaBaseMutitenantFront/`.

| Archivo nuevo | Modelo en este repo |
| --- | --- |
| `D/Forms/FormCatalog.cs`, `FormCatalogItem.cs`, `FormCatalogSource.cs`, `FormCatalogValueType.cs`, `FormCatalogErrors.cs`, `FormCatalogImportErrors.cs` | `D/Assistant/AssistantOrganizationSettings.cs`, `D/Assistant/AssistantErrors.cs` |
| `D/Authorization/Permissions.cs`: clase `Forms` en `OrganizationScoped`, con `formcatalogs.read` y `formcatalogs.manage` | la clase `Assistant` del mismo archivo |
| `A/Modules/Forms/FormsModule.cs`; `I/Modules/Forms/FormsModule.cs`; `P/Modules/Forms/FormsApiModule.cs`; `Program.cs`: `AddFormsModule` en las tres capas | `A/Modules/Assistant/AssistantModule.cs` y sus pares; la línea del Asistente en `Program.cs` |
| `A/Modules/Forms/Services/FormsFeatureDefinitionContributor.cs` (`new("forms", FeatureScope.Business, false)`) | `AssistantFeatureDefinitionContributor.cs` |
| `A/Modules/Forms/Resources/FormsTexts{,.en}.resx`, `FormsErrorTextSource.cs`; `P/Modules/Forms/FormsContractContributor.cs` | los del Asistente |
| `A/Modules/Forms/Interfaces/Services/IFormCatalogService.cs`, `IFormCatalogImportService.cs` y sus servicios, con los helpers `FormCatalogFiltersPolicy`, `FormCatalogItemEditor`, `FormCatalogPositionGuard`, `FormCatalogFileResolver` (lee el archivo o lo pegado), `FormCatalogImportPolicy` (decide altas, cambios, bajas y errores, sin escribir) y `FormCatalogExportBuilder` (arma las filas que se exportan) | `AssistantConversationService.cs` y sus helpers; los sufijos son los de la lista cerrada de [capas-y-flujo](../../rules/capas-y-flujo.md) |
| `A/Modules/Forms/Interfaces/Integrations/IFormCatalogSpreadsheet.cs` e `I/Modules/Forms/FormCatalogSpreadsheet.cs` (ClosedXML o CSV, solo celdas) | `IWhatsAppContactSpreadsheet.cs` y `I/Modules/WhatsApp/WhatsAppContactSpreadsheet.cs` |
| `A/Interfaces/Persistence/IMemberDirectoryReader.cs` e `I/Persistence/Readers/MemberDirectoryReader.cs` (C2) | los readers del núcleo de `I/Persistence/Readers/` |
| WhatsApp: el nombre del perfil (C14): suma a `A/Modules/WhatsApp/Models/WhatsAppWebhookBatch.cs`, `D/WhatsApp/WhatsAppMessage.cs`, `D/WhatsApp/WhatsAppCustomer.cs` y `A/Modules/WhatsApp/Services/WhatsAppOrganizationInboundWriter.cs`, donde se crea el contacto, con su migración | esos mismos |
| `A/Interfaces/Integrations/StructuredData/IStructuredContactSource.cs`, su modelo y `I/StructuredData/DisabledStructuredContactSource.cs`; en WhatsApp, `WhatsAppStructuredContactSource.cs` | `A/Interfaces/Integrations/Messaging/ILoginCodeChannel.cs`, `I/Messaging/Email/EmailLoginCodeChannel.cs` y `A/Modules/WhatsApp/Services/WhatsAppLoginCodeChannel.cs` |
| `A/Modules/Forms/Catalogs/MembersCatalogSource.cs` (columnas, operadores y versión) | — (es una definición, sin servicios) |
| `A/Modules/Forms/Interfaces/Persistence/IFormCatalogRepository.cs`, `IFormCatalogReader.cs`, `IFormWorkLease.cs`, `UniqueKeys.cs` | `A/Modules/Assistant/Interfaces/Persistence/` |
| `I/Modules/Forms/Persistence/FormsModelContributor.cs`; `Configurations/FormCatalogConfiguration.cs`, `FormCatalogItemConfiguration.cs`, `FormWorkLeaseConfiguration.cs`; `Readers/FormCatalogReader.cs` | `I/Modules/Assistant/Persistence/` (`AssistantModelContributor.cs`) |
| `I/Persistence/Migrations/<fecha>_FormCatalogs.cs` (con las restricciones diferidas en `Sql`, en una línea y sin `\r`) y `<fecha>_SystemRoleFormCatalogPermissions.cs` | `20261010011328_AssistantModule.cs`, `20261010203618_SystemRoleAssistantPermissions.cs` |
| `P/Modules/Forms/Controllers/BusinessFormCatalogsController.cs`, `Contracts/*HttpRequest.cs` | `P/Modules/Assistant/Controllers/BusinessAssistantController.cs` |
| `IdentityAccessTests`: el `MemberDirectoryReader` en la lista cerrada del núcleo | las entradas del núcleo de esa guarda |

**Front:**

| Archivo nuevo | Modelo |
| --- | --- |
| `F/src/modules/forms/registration.ts`: rutas `/org/formularios/catalogos` y `/org/formularios/catalogos/:id`; guarda con `features.includes("forms")` y `formcatalogs.read`; la entrada «Gestión de formularios» del menú (34), en el teléfono y en la compu; aporta `resources` es/en de `forms` y sus entradas de `enums` | `F/src/modules/assistant/registration.ts` |
| `F/src/modules/forms/{api,components,hooks,pages}/`, `errors.ts`: el listado, el editor con la tabla editable, el modal de opciones, el diálogo de importar, el editor de filtros y la vista previa. La grilla de pegar es una pieza nueva de `F/src/shared/ui/`, y su excepción a «sin tabla en un diálogo» ya está en la ficha de formularios del front | `F/src/modules/assistant/`. La tabla editable es una pieza nueva, con sus columnas fijas al desplazar y la línea entre columnas también en ellas (137): su excepción a las filas de solo lectura se escribe en la ficha de tablas del front |
| `F/src/modules/forms/locales/{es,en}/forms.json` y `enums.json` | `F/src/modules/assistant/locales/{es,en}/assistant.json` y `enums.json` |
| `F/src/app/moduleComposition.ts`: suma `formsRegistration` | la línea del Asistente |

**Punteros:** cada carpeta nueva lleva su `AGENTS.md` de una a tres líneas («qué va acá · Leé ficha · Copiá de archivo») y un `CLAUDE.md` con `@AGENTS.md`, más su fila en `docs/architecture/arbol.md`. Se crean con la carpeta, no antes (`HarnessTests`). Al cerrar la etapa nacen `docs/features/formularios.md`, con la sección de catálogos, y la fila de Forms en «Dónde va cada cosa» de `AGENTS.md`; cada etapa siguiente suma su sección.

## 11. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | origen y tipo fijos; código único sin distinguir mayúsculas; intercambio de posiciones en los extremos; valor obligatorio y del tipo; el estado «Activo» o «No se puede usar» y sus motivos |
| `tests/…Application.UnitTests/Modules/Forms/` | validación de filtros contra la fuente; la lectura de archivos y de lo pegado con «1.500», «1,500», «12.345» y «3/4/2026» en es y en; topes del archivo con celdas vacías con formato; el plan de importación (altas, cambios, bajas, columna ausente y columna vacía); exportar y volver a importar sin diferencias, con celdas de Excel en es y en, y con un número de 16 cifras y una fecha anterior a 1900 como texto canónico; el corte de nombres largos en la vista previa |
| `tests/…Api.IntegrationTests/Modules/Forms/` | cada ruta con 401, 403, feature apagada = 404, validación, 409, idempotencia y paginado; guardar sin cambios compara la versión sin tocar ni auditar opciones; guardar una opción entre 10.000 escribe y audita solo esa, y sube la versión del catálogo; un código repetido en un intercambio da el error de validación, nunca un 500 en el commit; borrar una opción que nada usa la saca, libera su código y su lugar, y las demás cierran el hueco; una de otro catálogo da `Forms.Catalog.ItemNotFound`, y una que también va en `items` o en `moves`, 400; subir y bajar con dos personas a la vez; aplicar con otro archivo = 409; dos importaciones de la misma persona a la vez = `Busy`, y una barrera vencida se vuelve a tomar; vista previa sin escribir; ejemplos exportados de cada respuesta |
| `tests/…Api.IntegrationTests/Modules/WhatsApp/` | un contacto sin nombre toma el de su perfil al escribir; uno con nombre puesto por una persona no se pisa (C14) |
| Volumen | un catálogo de 10.000 opciones: listar, buscar, guardar una opción y exportar, sin traer todo para quedarse con poco, con EXPLAIN |
| Aislamiento | la fuente «Miembros» nunca devuelve miembros de otra organización ni correos; la de «Contactos», ni contactos de otra ni teléfonos; la persona o el cliente de ejemplo de otra organización da error; cada ruta nueva le responde 404 a otra organización |
| Guardas (partials del módulo) | `IdentityAccessTests`, `ExplicitRouteInventoryTests`, `ModuleControllersTests`, `ApplicationServicesTests`, `ModuleIsolationTests`, `ResourceParityTests` (suma los resx de Forms) y `PermissionTextsTests` |
| Front | páginas de catálogos, la tabla editable con sus columnas fijas al desplazar, borrar una opción (pregunta en una guardada y queda «Se borra al guardar» con «Restaurar»; una nueva se quita sin preguntar; con movimientos o el orden pendientes, el 🗑 apagado con su motivo, y con una por borrar, ↑ ↓ apagados con el suyo), el modal de opciones (Cancelar, Listo, errores y celular), el diálogo de importar (la grilla con y sin encabezados, una columna ajena, revisando y aplicando), el editor de filtros y la vista previa, con MSW, axe y los ejemplos exportados validados con zod estricto; `registration`, `navigation` y `composition` del módulo; los botones según la decisión 43 |
| E2E y recorrido | pegar opciones desde una planilla, importar un archivo que da de baja algunas, exportar; un catálogo de miembros con filtros y su vista previa; uno de los contactos filtrado por etiqueta |

## 14. Referencia tomada de ArquitecturaBase

Se toma solo como referencia, del traspaso 2 (anexo C §10 y anexo D). Las rutas son de `ArquitecturaBase`.

| Tema | Lo que sirve |
| --- | --- |
| Opciones a mano | El código es la identidad y no se ve en WhatsApp. Una opción que nada usa se borra y deja su código, su lugar y su posición; si algo la usa, se dice qué y se da de baja (129). La posición es del servidor, con restricción diferida. El cliente nunca manda posiciones. |
| Carga de una vez | Pegar agrega e importar reemplaza; vista previa sin transacción; SHA-256 bajo los locks; confirmación con más del 25 % de bajas y del 10 % de errores; los topes de archivo, incluidas las celdas vacías con formato; CSV UTF-8 estricto; exportar sin diferencias al reimportar. |
| Tipo de valor | Se elige al crear y no cambia; canónico; Excel y PDF muestran el valor junto al texto. |
| Filtros | Valores siempre como parámetros, columnas de una lista cerrada, timeout de 3 s y una fila más que el tope. La vista previa no toma la identidad de la persona de ejemplo. |
| Errores pagados | Posiciones del cliente; una página tardía de otro catálogo cargada en el editor; cada guardado reescribía las 10.000 opciones; los números leídos en la cultura equivocada al importar. |
| Pantallas | CA1-A a CA3, CO1-B e IM1-A a IM3-A. Sirven para el contenido; el dibujo es de nuestro lienzo, con la decisión 43 y las piezas de acá (108). |
| No se toma | La vista SQL sobre `AspNetUsers` y roles globales; los permisos y el bloqueo globales; el idioma original y las traducciones; la política `forms-export` por proceso. |
