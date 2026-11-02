# E12 · Catálogos base (spec)

**Estado:** pendiente · spec de la primera etapa del bloque (decisiones 34, 78, 117, 119 y 120). Es el ABM de catálogos completo, sin formularios: lo que ArquitecturaBase llama E1 (tipo de valor, carga de una vez y hasta 10.000 opciones), sobre los catálogos a mano, de los miembros y de los contactos. Acá nace el módulo `Forms`; la E14 le suma los formularios y la pregunta «Elegir de un catálogo». `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E11]** y **[E9]** contra el código real.

**Fuentes:**
- alcance: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E12, y la [evaluación del traspaso 2](../2026-10-24-formularios-evaluacion-traspaso-2.md), §3;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- referencia de producto: el traspaso 2 de ArquitecturaBase, anexo C §10 y anexo D (CA1-A a CA3). Es solo referencia, no código para copiar.

**Requisitos:** la E11 (los contactos de la bandeja Clientes y sus etiquetas) y la E9 (cuotas y fuzz). La E10 no frena (123). Antes de esta etapa va la base del [plan de desarrollo](../2026-09-27-plan-de-desarrollo.md#etapas-12-a-25-formularios-por-whatsapp): los botones (la F5 que sale de la 10), la campana en el núcleo y el tamaño general.

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros, con los mismos números de sección. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| §2, §3, §4, §4b, §5, §6, §7 y §8 | [datos y contrato](12-parte-datos-y-contrato.md) |

## Qué leer

Antes de empezar:
1. **Decisiones** de la hoja de ruta: las de la §13 de esta spec.
2. **Fichas:** [persistencia-ef](../../rules/persistencia-ef.md), [multitenancy](../../rules/multitenancy.md), [auditoria](../../rules/auditoria.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [api-http](../../rules/api-http.md), [permisos](../../rules/permisos.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [result-y-errores](../../rules/result-y-errores.md), [validacion](../../rules/validacion.md), [textos-libres](../../rules/textos-libres.md), [numeros-y-moneda](../../rules/numeros-y-moneda.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [logs](../../rules/logs.md), [cuotas](../../rules/cuotas.md), [modulos](../../rules/modulos.md) y [tests](../../rules/tests.md).
3. **Código para copiar:** el módulo Asistente, pieza por pieza, en la §9.
4. **Pantallas:** los tableros de la §10, con el recorrido `armar-un-catalogo` del front.
5. **Referencia de producto:** la §14.

## 1. Alcance

**Entra:**
- **El módulo `Forms`:** quitable, con su feature `forms`, sus permisos de catálogos y la entrada «Gestión de formularios» del menú, que en esta etapa tiene solo «Catálogos» (34 y 78).
- **Catálogos de la organización:** listado, alta con el origen fijo (a mano, de los miembros o de los contactos), datos y estado «Activo», «De baja» o «No se puede usar».
- **Dar de baja y reactivar un catálogo** desde su pantalla, con su versión, sin borrarlo. Dar de baja uno con filtros no necesita vista previa, y reactivarlo sí.
- **A mano:**
  - **tipo de valor:** texto, entero, decimal o fecha, que se elige al crear y no cambia;
  - opciones con texto, detalle, código y, en un catálogo tipado, valor. Cada texto es uno solo, tal como lo escribe la persona (35). Se agregan y se editan en un modal y, en la compu, también en la tabla (117);
  - una opción se da de baja con su Estado. Una guardada que nada usa también se puede borrar: se marca, se borra al guardar y hasta entonces se restaura (129). Se reordenan con ↑ y ↓, o de la A a la Z al guardar;
  - **la carga de una vez:** un solo «Importar», con dos orígenes: pegar desde Excel (agrega) o subir un XLSX o un CSV (reemplaza), con vista previa (120);
  - **exportar** y bajar la plantilla. El Excel se lee como en pantalla (119);
  - **hasta 10.000 opciones** por catálogo, contando las dadas de baja.
- **De los contactos** (55): las opciones son los contactos de WhatsApp de la organización, los de la bandeja Clientes, con su nombre. Su vista previa elige un cliente de ejemplo, igual que la de los miembros. Ver la §4b.
- **El nombre de un contacto** (81): un contacto sin nombre toma el de su perfil de WhatsApp cuando escribe, y se puede cambiar. El teléfono nunca se muestra.
- **Con filtros:**
  - sobre la fuente «Miembros de la organización», con condiciones armadas por el sistema y «Sin incluir a quien lo recibe»;
  - vista previa con una persona de ejemplo, elegida a propósito: el teléfono, con solo el desplegable «Persona» arriba, que se actualiza sola;
  - el valor es siempre de texto.
- **Permisos:** `formcatalogs.read` y `formcatalogs.manage`.

**No entra:**
- los formularios y la pregunta «Elegir de un catálogo»: elegir un catálogo en una pregunta, mandar, responder y «Lo usan» (E14);
- campos extra y qué viaja a WhatsApp (E13);
- datos de las personas y filtros por destinatario (E16);
- catálogos con una consulta SQL;
- productos u otras fuentes externas, que no existen en la plantilla (los clientes son la fuente «Contactos», 55);
- la búsqueda en vivo o las listas dependientes, porque necesitan el intercambio cifrado con Meta;
- límites por plan: los topes son iguales para todas (42).

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
| `F/src/modules/forms/{api,components,hooks,pages}/`, `errors.ts`: el listado, el editor con la tabla editable, el modal de opciones, el diálogo de importar, el editor de filtros y la vista previa. La grilla de pegar es una pieza nueva de `F/src/shared/ui/`, y su excepción a «sin tabla en un diálogo» ya está en la ficha de formularios del front | `F/src/modules/assistant/`. La tabla editable es una pieza nueva: su excepción a las filas de solo lectura se escribe en la ficha de tablas del front |
| `F/src/modules/forms/locales/{es,en}/forms.json` y `enums.json` | `F/src/modules/assistant/locales/{es,en}/assistant.json` y `enums.json` |
| `F/src/app/moduleComposition.ts`: suma `formsRegistration` | la línea del Asistente |

**Punteros:** cada carpeta nueva lleva su `AGENTS.md` de una a tres líneas («qué va acá · Leé ficha · Copiá de archivo») y un `CLAUDE.md` con `@AGENTS.md`, más su fila en `docs/architecture/arbol.md`. Se crean con la carpeta, no antes (`HarnessTests`). Al cerrar la etapa nacen `docs/features/formularios.md`, con la sección de catálogos, y la fila de Forms en «Dónde va cada cosa» de `AGENTS.md`; cada etapa siguiente suma su sección.

## 10. Pantallas (tableros)

**Navegación (34 y 78):** «Gestión de formularios» llega con esta etapa, y su panel tiene solo «Catálogos». Los tableros de la etapa muestran solo eso.

**Aprobados y publicados en el lienzo v141** (2026-10-25, front `93cb7d01`), con sus teléfonos, sin «Textos en» ni «Cambios sin guardar»:
- `FRM-Catalogos`: el listado, sin la columna «Lo usan», que llega con los formularios;
- `FRM-Catalogo-Nuevo`: el origen, con la tarjeta «De los contactos de WhatsApp», y el tipo de valor;
- `FRM-Catalogo-Tipo`: el tipo de valor se elige al crear, con la columna «Valor» y el error en el campo;
- `FRM-Catalogo-Filtros`: un catálogo de los miembros, con sus filtros y la vista previa;
- `FRM-Catalogo-Contactos`: un catálogo «De los contactos», con sus filtros por etiqueta y la vista previa (55).

**Redibujados** (117, 120 y 125 a 129; v150):
- `FRM-Catalogo` y `M-FRM-Catalogo`: el editor de un catálogo a mano, con el armado de ArquitecturaBase. «El catálogo» dice de dónde salen y el tipo, y debajo van Nombre y Descripción; al costado, «Así lo ve quien lo recibe», con «Una opción | Varias» y el teléfono. La banda trae «Dar de baja», «Descartar» y «Guardar cambios». «Opciones» va a todo el ancho: «+ Agregar opción», «Importar», «Exportar» y un ⋮ con Descargar la plantilla, Ordenar de la A a la Z y Generar los códigos que faltan; abajo, solo «Buscar opción»; la tabla editable en la compu, la selección de filas para dar de baja o reactivar, el Estado como casilla y ↑ ↓ ✎ 🗑 a la vista;
- `FRM-Catalogo-Importar`: «Origen» y «Revisión» a la vista, sin pasos numerados.

**Nuevos** (v148): `FRM-Catalogo-Opciones` y `M-FRM-Catalogo-Opciones`, el modal de opciones. Importar no va en el teléfono, como hasta ahora.

**El modal de opciones (117):**
- **Cómo se abre:** «Agregar opción» lo abre con las opciones agregadas en él, y ✎, con las de la página. Cada lado va en su caja: la lista a la izquierda (número o «—», texto, «Nueva» y código o «Sin código») y la opción elegida a la derecha, con su posición («Opción 2 de 5» o «Nueva opción») y «Quitar opción» en su banda. «Agregar opción», al pie de la lista, suma otra.
- **Las filas:** Texto con su contador, Código con «Generar desde el texto», Valor si el catálogo es tipado, Detalle y, solo en una guardada, Estado «Activa | De baja». La ayuda va en la (i) de cada una.
- **Guardar:** escribe en el borrador, y se guarda con «Guardar cambios» de la página. «Cancelar» o Esc devuelven todo como estaba al abrirlo. «Listo» conserva todo, menos una opción nueva sin nada escrito.
- **Errores:** si «Guardar cambios» encuentra uno, el modal se abre en la primera opción con problemas, y la lista las marca con un triángulo.
- **Quitar:** en una nueva, «Quitar opción» no pregunta. En una guardada pregunta y la marca: se borra al guardar, y hasta entonces se puede restaurar (129). Si algo la usa, al guardar se dice qué y se puede dar de baja (`Forms.Catalog.ItemInUse`).
- **Celular:** ocupa la pantalla. Primero la lista y, al tocar una opción, la opción con «Volver a la lista».

**Importar (120):** un solo diálogo, de arriba abajo.
- **«Origen»:** «Desde» («Subir un archivo | Pegar desde Excel») y, para un archivo, su nombre con «Elegir archivo» o «Cambiar». La plantilla va en la banda. Qué acepta el archivo y qué hace cada origen van en sus (i).
- **La grilla:** lo pegado se ve con los encabezados del catálogo, en el orden de la plantilla. Sin encabezados, las filas van en ese orden. Si lo copiado los trae, por nombre, cada columna va a su lugar y esa fila no se importa. Una columna que no es del catálogo queda al final, en rojo, con su motivo, y no se manda. Pegar sobre la grilla llena la reemplaza.
- **«Revisión»:** aparece debajo al tocar «Revisar», con el archivo, la hoja, las filas leídas y las que no cambian, y Errores, Altas, Cambios y Bajas con su cantidad, por fila y columna. «Antes de aplicar» aparece solo si hay algo para confirmar, con la confirmación de bajas y «Aplicar sin las N filas con errores».
- **Las esperas:** «Revisando…» muestra la revisión como un esqueleto, y Cancelar sigue andando. «Aplicando…» deja la revisión atenuada, y Cancelar y la ✕ apagados, con «Esperá a que termine de aplicar.» en su tooltip.

**Estados que se dibujan** (cada uno tiene que poder producirse; si no, es un bug):
- **Listado:** filtro abierto, sin coincidencias, cargando, vacío y error.
- **Catálogo a mano:**
  - código repetido, texto repetido (avisa sin frenar), texto de más de 30 y valor de otro tipo, en la tabla y en el modal;
  - guardado, no se guardó y la revisión si otra persona guardó;
  - sin opciones, 1.200 opciones con búsqueda y páginas, 10.000 opciones, nuevo y cargando;
  - importar grisado en un catálogo nuevo o con cambios sin guardar, con el motivo en su tooltip;
  - una opción nueva, borrar una guardada (pregunta), «Se borra al guardar» con «Restaurar», una dada de baja con «Reactivar», movimientos pendientes y orden de la A a la Z pendiente, con cómo descartarlos, y las dos formas de la vista previa;
  - con solo `formcatalogs.read`: la tabla de lectura, sin casillas, con Exportar y la plantilla.
- **Modal de opciones:** una nueva, una guardada, una dada de baja, una por borrar, volver a la lista en el celular, y errores con el triángulo.
- **Importar:** la grilla vacía y con una columna ajena; «Revisar» sin archivo o sin filas, que marca el campo; revisando, aplicando, errores por fila y columna, muchas bajas que piden confirmar, muchas filas con error, un valor ambiguo, el 409 y otra importación en curso.
- **Catálogo con filtros:**
  - con más de 200 opciones, se guarda y avisa con ⓘ (decisiones 22 y 37);
  - sin persona de ejemplo, no se crea;
  - un rol que ya no existe deja el catálogo en «No se puede usar».
- **Catálogo de los contactos:** sin WhatsApp, «No se puede usar».

**Mandan sobre esta spec en:**
- **Las fichas del front** (pedidos del usuario del 2026-10-25 y del 2026-10-26):
  - ninguna sección lleva cantidades en su encabezado: nada de «N opciones · N activas»;
  - un dato por celda, en un renglón;
  - las fechas, completas, con fecha y hora en la zona de quien mira; nunca «hoy», «ayer» ni «hace…»;
  - sin leyendas (109): la vista previa es el teléfono al costado, la ayuda que sirve va en la (i) (116), y lo que no ayuda a decidir no va;
  - lo dado de baja es una opción del filtro «Estado», no una vista aparte, en plural y de acuerdo con lo que se lista: en la lista de catálogos, «Activos · Dados de baja». En la tabla de opciones no hay filtro: cada una muestra su casilla (126).
- **La tabla de opciones no se aplasta:** cada columna tiene su ancho mínimo (Texto y Detalle 12rem, Código 6rem, el valor y cada campo 8rem). Si no entra, la tabla se desplaza a lo ancho dentro de su caja, con la casilla, el número y «Texto» fijos a la izquierda y las acciones fijas a la derecha, sin ningún «hay más columnas a la derecha».
- **Guardado (C3):** todo se guarda junto en un `PUT` del catálogo, con su versión, y viaja solo lo que cambió. Suma `deleteItems[]`, las opciones por borrar. Una que algo usa responde 409 `Forms.Catalog.ItemInUse`, con qué la usa por opción, y no se borra ninguna; en esta etapa nada usa una opción, y cada etapa suma su motivo.
- **Nuevo catálogo:** primero se elige el origen, y para los escritos a mano, el tipo de valor; el nombre se escribe en el editor y el `POST` sale de «Crear catálogo».
- **Persona de ejemplo:** sin ella no se crea un catálogo de miembros con condiciones (`Forms.Catalog.SampleRequired`, «Elegí una persona.»).
- **La vista previa de un catálogo de miembros o de contactos** es el teléfono, con solo el desplegable «Persona» arriba, y se actualiza sola; no hay «Probar». Se ve con `formcatalogs.read`, como el catálogo.

## 11. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | origen y tipo fijos; código único sin distinguir mayúsculas; intercambio de posiciones en los extremos; valor obligatorio y del tipo; el estado «Activo» o «No se puede usar» y sus motivos |
| `tests/…Application.UnitTests/Modules/Forms/` | validación de filtros contra la fuente; la lectura de archivos y de lo pegado con «1.500», «1,500», «12.345» y «3/4/2026» en es y en; topes del archivo con celdas vacías con formato; el plan de importación (altas, cambios, bajas, columna ausente y columna vacía); exportar y volver a importar sin diferencias, con celdas de Excel en es y en, y con un número de 16 cifras y una fecha anterior a 1900 como texto canónico; el corte de nombres largos en la vista previa |
| `tests/…Api.IntegrationTests/Modules/Forms/` | cada ruta con 401, 403, feature apagada = 404, validación, 409, idempotencia y paginado; guardar sin cambios compara la versión sin tocar ni auditar opciones; guardar una opción entre 10.000 escribe y audita solo esa, y sube la versión del catálogo; un código repetido en un intercambio da el error de validación, nunca un 500 en el commit; subir y bajar con dos personas a la vez; aplicar con otro archivo = 409; dos importaciones de la misma persona a la vez = `Busy`, y una barrera vencida se vuelve a tomar; vista previa sin escribir; ejemplos exportados de cada respuesta |
| `tests/…Api.IntegrationTests/Modules/WhatsApp/` | un contacto sin nombre toma el de su perfil al escribir; uno con nombre puesto por una persona no se pisa (C14) |
| Volumen | un catálogo de 10.000 opciones: listar, buscar, guardar una opción y exportar, sin traer todo para quedarse con poco, con EXPLAIN |
| Aislamiento | la fuente «Miembros» nunca devuelve miembros de otra organización ni correos; la de «Contactos», ni contactos de otra ni teléfonos; la persona o el cliente de ejemplo de otra organización da error; cada ruta nueva le responde 404 a otra organización |
| Guardas (partials del módulo) | `IdentityAccessTests`, `ExplicitRouteInventoryTests`, `ModuleControllersTests`, `ApplicationServicesTests`, `ModuleIsolationTests`, `ResourceParityTests` (suma los resx de Forms) y `PermissionTextsTests` |
| Front | páginas de catálogos, la tabla editable, el modal de opciones (Cancelar, Listo, errores y celular), el diálogo de importar (la grilla con y sin encabezados, una columna ajena, revisando y aplicando), el editor de filtros y la vista previa, con MSW, axe y los ejemplos exportados validados con zod estricto; `registration`, `navigation` y `composition` del módulo; los botones según la decisión 43 |
| E2E y recorrido | pegar opciones desde una planilla, importar un archivo que da de baja algunas, exportar; un catálogo de miembros con filtros y su vista previa; uno de los contactos filtrado por etiqueta |

## 12. Puntos de contacto

Al planificar la E12 se verifican contra el código real.

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E11] | Los contactos de la bandeja Clientes (`WhatsAppCustomer`): las etiquetas son `text[]` y el nombre es opcional. | `IStructuredContactSource` los lista por página, con filtros por el texto de la etiqueta, sin teléfonos; la entrada completa el nombre (C14). |
| [E11] | La planilla de contactos (`IWhatsAppContactSpreadsheet`, ClosedXML). | `IFormCatalogSpreadsheet` copia su forma. |
| [E9] | Las cuotas compartidas ([cuotas](../../rules/cuotas.md)). | Las rutas de Forms cuentan en las cuotas generales; no se definen perfiles propios. |
| [E9] | Redis. | La E12 no cachea nada nuevo. |
| #51 | `Enter` borra `TenantKind`. | La E12 no tiene workers. |

## 13. Decisiones de producto de la etapa

Están en la §5 de la hoja de ruta y no se vuelven a preguntar:
- **12, 22 y 37:** un catálogo con filtros que da más de 200 opciones se puede guardar. La vista previa avisa, y el bloqueo es al mandar (E14).
- **35:** el contenido no tiene idioma.
- **36:** un nombre de más de 30 caracteres se corta en 29 y se le agrega «…».
- **42:** hasta 10.000 opciones, igual para todas las organizaciones.
- **43:** botones y avisos.
- **55:** el origen «De los contactos».
- **78:** los catálogos van primero, sin formularios.
- **81:** un contacto sin nombre toma el de su perfil de WhatsApp, y se puede cambiar; el teléfono nunca se muestra.
- **109, 110, 115 y 116:** sin leyendas, lo apagado dice por qué, toda espera a la vista y la ayuda en la (i).
- **117:** las opciones en un modal; una guardada se da de baja, no se quita.
- **119:** el Excel se lee como en pantalla, y se reimporta sin diferencias.
- **120:** un solo «Importar», con la grilla.

## 14. Referencia tomada de ArquitecturaBase

Se toma solo como referencia, del traspaso 2 (anexo C §10 y anexo D). Las rutas son de `ArquitecturaBase`.

| Tema | Lo que sirve |
| --- | --- |
| Opciones a mano | El código es la identidad y no se ve en WhatsApp. Las opciones no se borran. La posición es del servidor, con restricción diferida. El cliente nunca manda posiciones. |
| Carga de una vez | Pegar agrega e importar reemplaza; vista previa sin transacción; SHA-256 bajo los locks; confirmación con más del 25 % de bajas y del 10 % de errores; los topes de archivo, incluidas las celdas vacías con formato; CSV UTF-8 estricto; exportar sin diferencias al reimportar. |
| Tipo de valor | Se elige al crear y no cambia; canónico; Excel y PDF muestran el valor junto al texto. |
| Filtros | Valores siempre como parámetros, columnas de una lista cerrada, timeout de 3 s y una fila más que el tope. La vista previa no toma la identidad de la persona de ejemplo. |
| Errores pagados | Posiciones del cliente; una página tardía de otro catálogo cargada en el editor; cada guardado reescribía las 10.000 opciones; los números leídos en la cultura equivocada al importar. |
| Pantallas | CA1-A a CA3, CO1-B e IM1-A a IM3-A. Sirven para el contenido; el dibujo es de nuestro lienzo, con la decisión 43 y las piezas de acá (108). |
| No se toma | La vista SQL sobre `AspNetUsers` y roles globales; los permisos y el bloqueo globales; el idioma original y las traducciones; la política `forms-export` por proceso. |

## 15. Cómo se cierra

La puerta de la etapa ([etapa en paralelo](../../guides/etapa-en-paralelo.md)), con la lista de control de `/puerta`. No hay prueba con Meta, porque la E12 no manda nada por WhatsApp. El recorrido prueba:
1. **A mano:** «Proveedores» pegando desde una planilla, y «Recargo por zona», decimal, con el valor en su columna.
2. **Carga de una vez:** importar una planilla real de unas 1.000 filas, con algunas bajas, y exportarla de nuevo sin diferencias.
3. **Miembros:** un catálogo con filtros y su vista previa con una persona de ejemplo, vacío y con opciones, con «Sin incluir a quien lo recibe».
4. **Contactos:** un catálogo de los contactos de Clientes, filtrado por etiqueta, sin teléfonos a la vista. Un contacto nuevo que escribe sin nombre aparece con el de su perfil (81).
5. **Volumen:** 10.000 opciones: buscar, guardar una y exportar, con EXPLAIN.

## 16. Criterios

Salen de los reviews de ArquitecturaBase (E1) y del traspaso del 17/10. El plan los toma como lista de control, junto con la de `/puerta`:
- **Los límites del canal se validan al guardar** (30 y 300), no al mandar; la vista previa marca lo que no entra.
- **Referencias por id,** nunca por nombre: un rol renombrado no vacía el catálogo, y dar de baja un rol tampoco lo invalida.
- **Ninguna lectura trae todo para quedarse con poco:** listar, buscar, exportar y la vista previa leen en páginas o lotes, probado con 10.000 opciones.
- **Guardar sin cambios no toca las opciones,** y guardar una opción no reescribe las demás.
- **Lo que viene de un archivo** se lee con la cultura del pedido y rechaza lo ambiguo. Una columna opcional que no se pegó conserva sus valores.
- **El Excel no redondea nada:** lo que no entra en una celda numérica va como texto canónico.
- **Contratos:** `""` no es `null`; una entrada inválida es 400, nunca 500; un campo omitido no apaga nada.
- **En el front:**
  - la búsqueda espera 300 ms y conserva el editor y el foco;
  - una página tardía de otro catálogo se descarta;
  - el archivo vive solo en el diálogo y se descarta al salir;
  - el front no tapa la validación del servidor;
  - un 403 en una acción no oculta la pantalla de lectura.
