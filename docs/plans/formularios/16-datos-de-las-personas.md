# E16 · Datos de las personas y filtros por destinatario (spec)

**Estado:** pendiente · spec de la etapa 16 del bloque (decisiones 34, 78 y 105), con el [formato](plantilla.md) de las etapas. Es la segunda mitad de lo que ArquitecturaBase llama E2, entera: los datos de las personas y los filtros que los usan, juntos en una etapa, así nada queda sin quién lo use. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E15.

**Partes.** El contrato, los datos y lo que ya se pagó allá siguen en la [parte de datos y contrato](16-parte-datos-y-contrato.md); lo demás está acá.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E13](13-campos.md): datos, contrato y las lecciones; la [E14](14-formularios.md): T22, T23, la §6b y `publication-checks`; y la [E15](15-respuestas.md): «Columnas» y los campos sellados.
2. **Decisiones** de la hoja de ruta: 35, 37, 42, 43, 50, 78, 88, 105, 109, 111, 112, 115, 116, 129, 130, 134, 138, 145, 157, 165, 176, 261 (lo que se hace con una fila, en su ⋮), 262 (una sola pieza para elegir), 263 (nada debajo de otro dato), 264 (el orden de las tablas) y 269 (lo que el servidor rechaza, la pantalla lo frena antes).
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [concurrencia](../../rules/concurrencia.md) y [guardado](../../rules/guardado.md) | versión del dato y de los valores; los bloqueos, siempre en el mismo orden |
   | [persistencia-ef](../../rules/persistencia-ef.md) y [multitenancy](../../rules/multitenancy.md) | las dos tablas nuevas, y una fuente dentro de la organización |
   | [permisos](../../rules/permisos.md) | el permiso nuevo; una ruta con dos `[HasPermission]` exige los dos |
   | [datos-personales](../../rules/datos-personales.md) y [auditoria](../../rules/auditoria.md) | los valores se van con la membresía |
   | [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | errores por campo y los que dicen qué formulario usa algo |
   | [api-http](../../rules/api-http.md) y [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) | las rutas nuevas |
   | [textos-libres](../../rules/textos-libres.md) y [textos-y-traducciones](../../rules/textos-y-traducciones.md) | un solo nombre por dato (35) |
   | [modulos](../../rules/modulos.md) | el núcleo no conoce a Forms |
   | [tests](../../rules/tests.md) | qué lleva test y dónde va |

4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §11 (C2-05, C2-06 y C2-09), el anexo D (`CE4-A`) y el anexo E §2.7 (review R1023, puntos 8 y 18 a 20).

## Qué se construye

**Los datos de las personas,** como «Zona»:
- los define cada organización: nombre (un solo texto, 35), clave, tipo (Texto o «De un catálogo», con una fuente a mano y activa), posición, estado y versión. Hasta 20 activos (42 y 105): dar de baja uno libera su lugar. Se dan de baja y se reactivan, sin borrarse;
- **la clave se muestra, con su (i), y queda con candado al guardar** (130): nace del nombre, como la de un campo de la E13, se puede escribir hasta guardar y no se repite en la organización, tampoco con un dato dado de baja. Los valores van por la identidad del dato, no por la clave;
- son de la organización y del miembro, nunca de la cuenta (42): una persona puede ser «Centro» en una organización y no tener el dato en otra;
- un dato que usa un filtro del borrador o de la versión activa de un formulario no cambia de tipo ni de fuente, ni se da de baja. El error dice qué formulario y qué pregunta;
- cambiar el tipo o la fuente de un dato con valores pide confirmar cuántos, y borra esos valores;
- un catálogo fuente de un dato activo no se da de baja, igual que el de un campo.

**Los valores:**
- una fila por miembro, con su propia versión y un `PUT` delta: ausente conserva y `null` limpia;
- un texto tiene hasta 300 caracteres. Uno de catálogo guarda la identidad de la opción y exige una opción activa de la fuente. **Un valor guardado que apunta a una opción dada de baja se conserva y sigue filtrando;**
- no van en «Exportar mis datos», porque son de la organización. **Al terminar una membresía, sus valores se borran con ella:** al sacar a alguien de la organización, al revocar la invitación de quien todavía no entró y en la baja de la cuenta. El núcleo suma para eso un punto de aviso neutral, que hoy no existe.

**El papel de un campo:** los campos de la E13 suman su papel, «Dato» (el de siempre) o «Filtro». Los dos se sellan al responder. En la planilla, las subcolumnas de un campo «Filtro» salen apagadas y se prenden en «Columnas»; el teléfono no tiene columnas, como en la E15. El detalle y el Excel los tratan como a cualquier campo sellado: el detalle los muestra cada uno en su campo, nunca debajo del valor (263), «Todas las preguntas» los trae y «Solo las visibles» sigue a «Columnas». Un campo que usa un filtro del borrador o de la versión activa no se quita ni cambia de papel, tipo o fuente.

**Los filtros por destinatario,** en una pregunta de un catálogo a mano («Qué opciones ve cada persona», una zona que arranca plegada, con los filtros en su banda, y se abre sola con un error o un aviso, 145):
- filtros de igualdad, uno por campo de papel «Filtro», unidos con «y»;
- **fijo:** un valor elegido, que se valida contra su fuente. Por ejemplo, «Rubro es Frío»;
- **según quien lo recibe:** un dato de la persona, de tipo catálogo, con la misma fuente que el campo. Por ejemplo, «Zona es la zona de quien lo recibe»;
- se guardan en el borrador y se sellan con la versión. Editarlos conserva el `FieldId` si no cambia el catálogo.

**Al mandar,** la preparación de la oferta de la E14 resuelve al miembro real y filtra en SQL, antes del tope de 200 o 20:
- **a quien no tiene el dato no se le manda, y se dice cuál le falta.** También si el valor guardado no es opción de la fuente actual del dato;
- un filtro que dejó de funcionar frena el envío. **Nunca se retira el filtro ni se manda la lista entera;**
- **un cliente no tiene datos** (42): una pregunta con un filtro según quien lo recibe no se le manda, y un filtro fijo sí;
- al operador le llega el motivo y la cantidad, nunca los nombres de las opciones.

**La vista previa «Como si lo recibiera»:** con una persona de ejemplo, pide `formcatalogs.read`, no guarda ni toma bloqueos, y distingue «Lista», «Le falta el dato» y «El filtro no funciona». No nombra opciones de un catálogo que quien mira no puede leer. Lo que tiene la persona probada se lee en dos columnas, el dato y su valor, nunca «Zona: Centro» corrido (138). Debajo, sobre los filtros sin guardar, el máximo por persona y cuántas personas no tienen el dato, con la misma cuenta que los avisos de publicar.

**La vista oficial** de la E14 no usa los filtros según quien lo recibe: cada lista trae sus primeras 20 opciones activas con los filtros fijos de la pregunta (176).

**Al publicar** se separan los problemas, que frenan, de los avisos, que no (37):
- **frenan:** un filtro inválido; un filtro fijo que deja más opciones que el tope, si la pregunta no tiene filtros según quien lo recibe; y uno según quien lo recibe en un formulario solo para clientes;
- **avisan:** el máximo de opciones por combinación de datos, solo cuando pasa el límite de WhatsApp, 200 en una elección o 20 en varias, o da cero: «Con estos filtros nadie ve ninguna opción.» (157); cuántas personas activas con `formrecords.create` no tienen el dato; y uno según quien lo recibe en un formulario para los dos, que no se le va a poder mandar a un cliente;
- con un filtro según quien lo recibe, un catálogo de más de 200 opciones se publica: cambia lo que la E14 dice de `CatalogTooLarge`.

## Pantallas

Aprobados y publicados en el lienzo v150, con su recorrido guiado. Muestran solo lo que existe hasta acá (78). Con el traspaso 5, redibujados, aprobados y publicados en el lienzo v162: `FRM-Editor-16` y su teléfono, con las dos zonas plegadas en «Zonas plegadas» (145) y lo de la persona probada en dos columnas (138), y `FRM-Respuestas-16`, con «Filtros» (144); su teléfono lo tiene desde la v160.

**Redibujados con el traspaso 10, aprobados y publicados en el lienzo v168:**
- `FRM-Publicar-16`, con el aviso de opciones por persona solo cuando no entra o da cero (157): «Como mucho 240 opciones por persona (Centro), de un límite de 200.» o «Con estos filtros nadie ve ninguna opción.». Si entra, no dice nada;
- `FRM-Editor-16` y `M-FRM-Editor-16`, con «Agregar título» al lado de «Agregar pregunta» (88) y un solo «Agregar filtro ▾» (134);
- `FRM-Usuario-Datos` y `M-FRM-Usuario-Datos`, con «Agregar empresa», como las demás altas;
- `WA-Chats-Formulario-16` y `M-WA-Chats-Formulario-16`, con «Completá el formulario «Visita a cliente».» propuesto al elegir el formulario (165).

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Datos-Personas` y `M-FRM-Datos-Personas` | «Datos de las personas», en Administración: los datos, la tabla de personas y el diálogo para definir un dato |
| `FRM-Usuario-Datos` y `M-FRM-Usuario-Datos` | la ficha del usuario, con las pestañas «Acceso» y «Datos» |
| `FRM-Catalogo-16` y `M-FRM-Catalogo-16` | el modal de campos de la E13, con «Papel» |
| `FRM-Editor-16` y `M-FRM-Editor-16` | «Qué opciones ve cada persona» en la pregunta |
| `FRM-Publicar-16` | la zona «Avisos», entre «Problemas» y «Qué se publica» (112) |
| `WA-Chats-Formulario-16` y `M-WA-Chats-Formulario-16` | el formulario que no se puede mandar, con el motivo |
| `FRM-Respuestas-16` y `M-FRM-Respuestas-16` | «Columnas», con las subcolumnas de los filtros apagadas; el teléfono no tiene columnas |

**Se redibujan con el traspaso 18, tanda 2** (261 a 264):
- `FRM-Datos-Personas` y su teléfono: el tipo y el catálogo de un dato, cada uno en su columna; en el teléfono, ningún dato junto a otro con «·» debajo del nombre (263); las dos tablas ordenan por sus columnas (264); el tipo del diálogo, en la pieza (262);
- `FRM-Catalogo-16` y su teléfono: «Papel», en la pieza (262), y en la lista, el tipo y el papel en sus columnas (263);
- `FRM-Editor-16` y su teléfono: el campo, el dato y el valor de cada filtro y la persona de «Como si lo recibiera», en la pieza (262), y las preguntas con el tipo y «Obligatoria» en sus columnas (263), como el editor de la E14;
- `WA-Chats-Formulario-16` y su teléfono: el formulario, en la pieza, como en la E14 (262);
- `FRM-Respuestas-16` y su teléfono siguen lo que la E15 redibuja de la planilla.

**Corregidos en el lienzo v169,** porque chocaban con una decisión, una ficha del front o esta spec: la (i) de «Clave» de `FRM-Datos-Personas` y su teléfono, con las filas sin rótulos (138); la (i) de «Papel» de `FRM-Catalogo-16`; `M-FRM-Respuestas-16`, sin «Columnas» y con el nombre entero (174); el editor de atrás de `FRM-Publicar-16`, y «A un cliente» de `WA-Chats-Formulario-16`, en Clientes.

**«Datos de las personas»:** dos secciones, como en ArquitecturaBase. «Datos», con su (i) y «+ Agregar dato» en el encabezado: Dato, Clave, Tipo, Catálogo («—» en uno de texto), Lo usan, Estado («Activo» o «De baja», con su punto, en su columna y nunca debajo del nombre, 206) y ✎: cada dato en su columna, nunca «De un catálogo · Zonas» (263). Con el tope de 20, «Agregar dato» queda grisado, con el motivo en su tooltip. «Personas», con el resumen «6 personas · 2 sin Zona» y «Buscar personas»: Persona, una columna por dato activo, «—» si no tiene y ✎ con `users.manage`. Sin `users.read`, una línea lo dice. Las dos tablas ordenan desde el encabezado y desde «Ordenar por», salvo «Lo usan», que tiene varios valores; en «Personas», un dato ordena por su valor (el texto de su opción, en uno de catálogo), con lo vacío al final en los dos sentidos (264). En el teléfono, una persona no junta sus datos con «·» debajo del nombre: lleva menos datos o es una tarjeta con cada dato nombrado (263).

**El diálogo para definir un dato:** «Agregar dato» o «Editar dato», con Nombre, Clave con su (i), Tipo, en la pieza (262), y, si es de un catálogo, Catálogo, en el combo de la E13 (111). La clave sigue al nombre hasta que se escribe a mano, y una guardada no cambia, con su candado (130). Editando va «Dato activo». En uso, tipo, catálogo y «Dato activo» quedan grisados y se dice qué formularios lo usan. Si hay valores, cambiar el tipo pide marcar «Sí, cambiar el dato y borrar los valores de N personas». Mientras guarda, «Guardando…», y la tabla que vuelve a leer atenúa.

**La ficha del usuario:** las pestañas aparecen solo si un módulo aporta una; si no, queda como hoy. «Datos» tiene sus botones y se guarda aparte del acceso, solo si cambió. Salir con cambios pregunta antes. Un error queda en la pestaña.

**«Qué opciones ve cada persona»:** una zona de la pregunta que arranca plegada, con lo configurado en su banda (145). Abierta, la banda trae «Agregar filtro ▾», con «Según quien lo recibe» y «Filtro fijo» (134), y la (i) va junto al título. Cada filtro va en su recuadro: «Según quien lo recibe» (Campo y Dato de la persona) o «Filtro fijo» (Campo y Valor fijo), con ✕; cada uno se elige con la pieza (262), y guardar un filtro sin completar se frena en la pantalla, marcando solo lo que falta (269). Debajo, «Como si lo recibiera», con la persona y el resultado, el máximo por persona («Como mucho 18 por persona (Centro, la más grande): entra en el desplegable (200).») y cuántas personas no tienen el dato («2 personas sin Zona»), con «Cargar los datos» para quien tiene `formpersondata.manage`. Sin campos de filtro, «Agregar filtro ▾» queda grisado, con el motivo en su tooltip; «Según quien lo recibe» queda apagada, con el suyo, si ningún dato de las personas elige del catálogo de esos campos.

**Borrar una opción (129):** `Forms.Catalog.ItemInUse` suma dos motivos, y la opción se puede dar de baja:
- `uses: forms`, si la nombra un filtro fijo del borrador o de la versión activa de un formulario: «No se puede borrar: la usa un formulario. Podés darla de baja.»;
- `uses: people`, si la tiene una persona en sus datos: «No se puede borrar: la tiene una persona en sus datos. Podés darla de baja.».

**«Papel»:** en el modal de campos, en la pieza, con «Dato» y «Filtro» (262), y la ayuda en la (i). En la lista, el tipo y el papel van cada uno en su columna, nunca «De un catálogo · Filtro» (263). Cambiarlo en un campo en uso se rechaza, con el motivo debajo.

**Estados:** para cada tablero, cargando, vacío y error, más:
- **Datos:** 20 datos, un dato en uso, un dato dado de baja, cambiar el tipo con valores, buscar personas, ninguna coincide, sin `users.read` y sin `users.manage`;
- **Filtros:** «Le falta el dato», «El filtro no funciona», más opciones que el tope, un catálogo sin campos de filtro y una opción de «Agregar filtro ▾» apagada;
- **Publicar:** problemas y avisos juntos, solo avisos, y el máximo que no entra y el que da cero (157);
- **Enviar:** le falta el dato, el filtro no funciona y es un cliente.

**Mandan sobre esta spec en:** las fichas del front (un dato por columna, 206; sin leyendas; botones según la 43 y la 115). «Dar de baja» y «Reactivar» son las palabras de siempre.

**Valen las de toda la app** (160 a 164, 173 a 175 y 180): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E12]` a `[E15]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Dominio, errores y permiso | `D/Forms/FormPersonDataDefinition.cs`, `FormPersonDataValues.cs`, `FormRecipientFilter.cs`, `FormPersonDataErrors.cs`; suma a `Permissions.cs` y `Permissions{,.en}.resx` | `D/Forms/FormCatalog.cs` [E12] | 20 contando solo los activos (105); la clave: su formato, repetida y fija al guardar (130); `PermissionTextsTests` |
| B2 | Tablas y migraciones | `I/Modules/Forms/Persistence/Configurations/FormPersonData*Configuration.cs`; las dos migraciones | `FormCatalogConfiguration.cs` [E12], `20261010203618_SystemRoleAssistantPermissions.cs` | aislamiento y migración, también hacia atrás |
| B3 | Definir: tope, en uso y confirmar el borrado. Bloquea definiciones, dato y fuente, en ese orden | `A/Modules/Forms/Interfaces/Services/IFormPersonDataService.cs`, `A/Modules/Forms/Services/FormPersonDataService.cs`, `FormPersonDataEditor.cs` | `FormCatalogService.cs` [E12] | dos personas crean el dato 20 a la vez; en uso por el borrador y por la versión activa; `count` exacto |
| B4 | Valores por delta. Bloquea dato, persona y fuente, en ese orden | `FormPersonDataValueEditor.cs`, `I/Modules/Forms/Persistence/Readers/FormPersonDataReader.cs` | `FormCatalogFieldValuePolicy.cs` [E13] | delta y `null`; opción dada de baja conservada; dos guardados = 409; un miembro de dos organizaciones; la tabla paginada, sin correos |
| B5 | El aviso de fin de membresía | `A/Interfaces/Integrations/Legal/IMembershipEndParticipant.cs`; suma a `OrganizationUserRemover.cs`, `OrganizationUserAccessRemover.cs` (revocar la invitación de quien todavía no entró) y `MembershipDeletionParticipant.cs`; `A/Modules/Forms/Services/FormsMembershipEndParticipant.cs` | `IAccountDeletionParticipant.cs` | sacar a alguien, revocar la invitación de quien no entró y la baja de la cuenta borran los valores, en la misma UoW; sin el módulo, nada |
| B6 | El papel de un campo, sellado y columnas | suma a `FormCatalogField.cs`, `FormCatalogFieldEditor.cs` [E13], `FormRecordWriter.cs` [E14] y `FormRecordColumnsBuilder.cs` [E15] | esos mismos | no cambia en uso (lección 1); una respuesta vieja se lee como `Data` |
| B7 | Filtros en el borrador | `A/Modules/Forms/Services/FormRecipientFilterPolicy.cs`; suma a `FormDraft.cs` y `FormDraftEditor.cs` [E14] | `FormCatalogFiltersPolicy.cs` [E12] | cada motivo de `RecipientFilterInvalid`; lección 3; conserva el `FieldId` |
| B8 | La oferta filtrada, en SQL | suma a `FormOfferBuilder.cs` [E14] y `FormCatalogReader.cs` [E12] | esos mismos | las dos zonas; sin el dato; valor de otra fuente; filtro roto; cliente; nunca la lista entera; con 201 opciones; 10.000 opciones con EXPLAIN; la vista oficial, con los filtros fijos y sin los según quien lo recibe (176) |
| B9 | La vista previa y el máximo por persona | suma a `FormCatalogService.cs` y a `FormPersonDataReader.cs`, que cuenta el máximo por combinación y las personas sin el dato | `POST …/previews` [E12] | los tres estados; sin escribir; lección 4; persona de otra organización; el máximo y las personas sin el dato, con los filtros sin guardar, iguales a los avisos de publicar |
| B10 | Publicar: problemas y avisos | suma a `FormPublicationService.cs` [E14] y a `IMemberDirectoryReader.cs`, con la cuenta de B9 | esos mismos | lección 2; el máximo por combinación, también el que da cero (157); personas sin el dato; para clientes y para los dos |
| B11 | Una opción en uso no se borra | suma al guardado de un catálogo, `FormCatalogService.cs` [E12], leído con `IFormReader.cs` [E14] y `FormPersonDataReader.cs` | `uses: records`, que suma la E14 | `uses: forms` con un filtro fijo del borrador y con uno de la versión activa; `uses: people`; las dos se dan de baja |
| B12 | Rutas y contratos | `P/Modules/Forms/Controllers/BusinessFormPersonDataController.cs`, `Contracts/*`, `FormsContractContributor.cs`; `AuditSummary` en `Audit{,.en}.resx`; `GrowingCollectionContractTests` | `BusinessFormCatalogsController.cs` [E12] | cada ruta con 401, 403, feature apagada = 404, validación, 409 y paginado; los dos permisos de `values`; ejemplos exportados |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` | la API de la E15 | zod estricto con los ejemplos |
| F2 | «Datos de las personas» | `F/src/modules/forms/pages/PersonDataPage.tsx`; suma a `registration.ts` y a sus recursos | su tablero y `CatalogCombo.tsx` [E13] | permisos, tope, en uso y confirmar; el mismo combo; axe |
| F3 | La pestaña «Datos», con el registro neutral | `F/src/modules/forms/components/MemberDataTab.tsx`; suma a `F/src/shared/modules/types.ts`, `useModules.ts` y `UserDetailPage.tsx` | su tablero | sin el módulo no hay pestañas; guarda aparte; salir con cambios; composición |
| F4 | «Papel» en el modal de campos | suma a `CatalogFieldsDialog.tsx` [E13] | su tablero | en uso, el motivo debajo; «De un catálogo · Filtro» en la lista |
| F5 | «Qué opciones ve cada persona» | suma a `CatalogQuestionFields.tsx` [E14] | su tablero | un solo «Agregar filtro ▾», con sus dos opciones y sus motivos; fijo y según quien lo recibe; la vista previa, el máximo y las personas sin el dato se actualizan solos; sin `formcatalogs.read` |
| F6 | Publicar, enviar y «Columnas» | suma a `PublishDialog.tsx`, al diálogo de enviar [E14] y a `FormRecordsPage.tsx` [E15] | su tablero | avisos que no frenan; el máximo, solo si no entra o da cero (157); motivo al mandar; subcolumnas apagadas; el teléfono sin «Columnas» |

**Al cerrar:** `docs/features/formularios.md` suma la sección, y `docs/features/organizaciones.md`, el aviso de fin de membresía.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | La forma de las preguntas en el borrador, `FormOfferBuilder` y el armado de `offerProblems`. | Una pregunta suma una lista, y la oferta filtra antes del tope, con una fila más. |
| [E14] | Cómo quedó `publication-checks`. | Hoy no tiene `warnings`: nacen acá. |
| [E13] | `FieldsJson`, el índice GIN y el modal de campos. | `FieldValuesJson @> …` filtra por igualdad con ese índice. |
| [E12] | `IMemberDirectoryReader`. | Suma las ids de los miembros activos con un permiso. |
| [E15] | Cómo «Columnas» guarda lo oculto en la URL. | Admite columnas apagadas por defecto. |
| Núcleo | La ficha del usuario del front, que hoy no tiene pestañas, y `OrganizationUserRemover`. | Si la base de la 12 no trae el registro de pestañas, entra acá. |
| Núcleo | Otras formas de terminar una membresía. | Solo los tres caminos de B5 llaman a `Member.Remove`. |

## Decisiones tomadas (no se vuelven a preguntar)

- **Por organización y por miembro** (42). Un dato no depende de la empresa.
- **Hasta 20 datos activos de las personas,** iguales para todas las organizaciones (42 y 105).
- **Los avisos de publicar** no frenan (37), y van en su zona (112).
- **El dato tiene clave** (130), como un campo: nace del nombre y queda fija al guardar. Los valores van por la identidad del dato, no por la clave.
- **Un formulario para los dos** con un filtro según quien lo recibe se publica con un aviso, y se manda solo a empleados.
- **La pestaña «Datos»** se ve con `users.manage`: quien solo lee las ve en la tabla, con los permisos de esa pantalla.
- **Un solo «Agregar filtro ▾»** en la banda de la zona (134 y 145).
- **La vista oficial** usa los filtros fijos y no los según quien lo recibe (176).
- **Las filas, la pieza, nada debajo y el orden** (261 a 264): lo que se hace con una fila va en su ⋮; todo lo que se elige es la pieza `Picker`; cada dato va en su columna, y toda columna con un dato ordena.

## Fuera de esta etapa

- Datos para clientes, un dato que dependa de la empresa, varios valores de un mismo dato y operadores distintos de «es igual a».
- Una columna de datos en la lista de usuarios.
- Completar otras preguntas con los datos de la opción.
- El filtro «según otra respuesta»: no funciona en un Flow de WhatsApp, así que en esta etapa no hay dónde usarlo. Va en la E20 (94).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta**, que queda pendiente hasta la I1 de la 10 (123), con el número de una organización de prueba:
1. dos personas con distinta «Zona» reciben el mismo formulario, cada una con los proveedores de su zona;
2. una persona sin «Zona» no lo recibe, y el operador ve el motivo;
3. una persona que es miembro de dos organizaciones, con «Zona» en una sola, recibe según cada organización.

El recorrido suma: definir «Zona», cargarla en la ficha de dos personas, armar el filtro, verlo con «Como si lo recibiera» y publicar con el aviso de quién no tiene el dato; y sacar a una persona de la organización, y revocar la invitación de otra a la que se le cargó «Zona», con sus valores borrados.

## Requisitos previos

La E15 cerrada.

## En el multitenant

- **Datos de la organización:** definiciones y valores son `ITenantOwned` con RLS, y los valores van por membresía. Cada ruta nueva lleva su test de aislamiento.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** suma `formpersondata.manage`.
- **El núcleo no conoce a Forms:** la pestaña «Datos» y el aviso de fin de membresía entran por registros neutrales.
- **Cuotas:** las rutas cuentan en las cuotas generales.
