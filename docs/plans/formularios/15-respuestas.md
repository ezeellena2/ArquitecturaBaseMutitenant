# E15 · Respuestas: planilla, detalle, Excel y tablero (spec)

**Estado:** pendiente · spec de la etapa 15 del bloque (decisiones 34, 57, 78 y 79), con el [formato](plantilla.md) de las etapas. Es lo que ArquitecturaBase llama etapa 3, con el menú `MF1`, el detalle completo (`DC0` y `DC1` ajustado) y el tablero `DOC7-A`. **Solo lee,** salvo los ajustes del tablero de cada persona y los campos sellados de cada pregunta, que la captura de la E14 suma a `FormRecordCatalogFields`. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E14. El contrato, los datos y lo que ya se pagó allá están en su [parte de datos y contrato](15-parte-datos-y-contrato.md).

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T7, T11 y T22, §3 (`FormVersions`, `FormRecords` y `FormRecordValues`) y §7 (puertos); y la [E12](12-catalogos.md): C5, C8, C11 y la §4.
2. **Decisiones** de la hoja de ruta: 9, 23, 24, 35, 43, 49, 57, 78, 79, 109, 115, 139, 143, 144, 163, 226, 260, 261, 262, 263, 264, 266, 267, 269 y 275.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) | la planilla, sus conteos y la variante con filtros en el cuerpo, que nace acá |
   | [multitenancy](../../rules/multitenancy.md) y [permisos](../../rules/permisos.md) | todo se lee con RLS; lo ajeno y lo excluido dan el mismo 404 |
   | [modulos](../../rules/modulos.md) | el nombre del cliente y su conversación, solo por puertos neutrales |
   | [numeros-y-moneda](../../rules/numeros-y-moneda.md) y [fechas-y-zonas](../../rules/fechas-y-zonas.md) | el Excel, los totales y los períodos en la zona de quien mira |
   | [concurrencia](../../rules/concurrencia.md) | la barrera de una exportación por persona |
   | [persistencia-ef](../../rules/persistencia-ef.md) y [guardado](../../rules/guardado.md) | los índices y los ajustes del tablero |
   | [datos-personales](../../rules/datos-personales.md) y [logs](../../rules/logs.md) | los ajustes se van con la baja; ningún valor ni filtro a los logs |
   | [api-http](../../rules/api-http.md), [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | rutas, filtros inválidos y errores |
   | [textos-y-traducciones](../../rules/textos-y-traducciones.md) | los encabezados fijos en el idioma de quien mira; las preguntas, como se escribieron (35) |
   | [tests](../../rules/tests.md) | qué lleva test y dónde va |

4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer:
   - en el traspaso del 17/10, retirado el 2026-11-08 (`git show e9f1a599:docs/plans/2026-10-17-formularios-traspaso/`): anexo C, «Etapa 3» y §3.5, y anexo B, «Etapa 3»;
   - en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`: anexo C §5 (G-01 a G-08), §6 (L-01 a L-04), C1-05 y C2-07, y anexo D (`MF1`, `DC0`, `DC1` ajustado, `CA5`, `CA6`, `CE5`, `CE6` y `DOC7-A`).

## Qué se construye

**La lista «Formularios» (79):**
- con `formrecords.read`, Preguntas y Último cambio dan lugar a tres columnas, un dato en cada una: Respuestas (de `LastRecordNumber`), Última respuesta (su número, que la abre) y Recibida. Las tres ordenan (264), con los formularios sin respuestas al final. Sin valores, en una sola consulta paginada. Sin ese permiso, la lista sigue como en la E14;
- quien ve respuestas y no tiene `forms.read` usa otra ruta, porque `[HasPermission]` pide un solo permiso. Ve solo los formularios publicados, con el nombre de su versión activa y nunca el del borrador, sin «En WhatsApp»;
- el nombre abre «Diseño» y el número de Respuestas, la planilla (275). Quien no tiene `forms.read` no tiene «Diseño»: su nombre abre «Respuestas».

**Las pestañas:** «Respuestas», con su cantidad, y «Tablero» van antes de «Diseño» (79). Se ven con `formrecords.read` y una versión publicada.

**La planilla:**
- columnas fijas: N°, Recibida, Cargó, Canal y Empresas. Cargó es un empleado o un cliente (49); si alguna fila es de un cliente, la columna «Es cliente», después de Cargó, dice «Sí» o «—» (no «Cliente», que puede ser una pregunta del formulario), sin una marca al lado del nombre (206). Canal dice «WhatsApp». Empresas son las de quien respondió (24): un cliente no tiene;
- una columna por pregunta, unidas por `FieldId` entre versiones: primero el orden de la versión activa y después las retiradas;
- «—» si la pregunta no existía en esa versión, y «Sin respuesta» si quedó vacía. Cero y `false` se distinguen;
- **«elegir varias»** muestra las opciones en el orden en que se eligieron, separadas por coma;
- **los campos sellados** van en subcolumnas después de su pregunta, «pregunta · campo». Ordenan, pero no se filtran ni se cuentan. «Columnas» las prende o las apaga;
- cada celda dice un dato, sin nada debajo (263);
- **ordena toda columna con un dato** (264), desde el encabezado y desde «Ordenar por»: N°, Recibida, «Es cliente», Canal, cada pregunta y cada dato sellado, comparando lo guardado. No ordenan Cargó, porque el nombre no vive en Forms, Empresas, «elegir varias» ni un dato con varios valores. Lo vacío va al final, y sin orden elegido va Recibida, de la más reciente a la más vieja, con su flecha. El primer toque ordena en el sentido útil (el texto de la A a la Z, los números de mayor a menor, las fechas de la más reciente a la más vieja, el «Sí» primero) y el segundo lo invierte.

**La barra (126, 143, 144, 262 y 264):** «Exportar a Excel» en la banda; en la barra, «Buscar en textos largos» (si el formulario los tiene), «Empresa», «Filtros», con la cantidad de aplicados, «Columnas» y, último y separado de los filtros, «Ordenar por», que dice el orden con palabras («Recibida, más reciente primero») y abre la pieza con «Columna» y «Orden»; en el teléfono ocupa el ancho. Cambiar el orden vuelve a la página 1, y «Limpiar todo» lo conserva. Debajo, lo aplicado en chips con su × y «Limpiar todo». Sin «N respuestas»: el total está al pie. «Filtros» se despliega como «Columnas»: a la izquierda, Recibida, cada pregunta en orden, Cargó y Versión; a la derecha, lo que se elige en la marcada: «Desde» y «Hasta» para un número o una fecha, «Contiene» para un texto y, para una pregunta de elegir, sí o no, Cargó y Versión, la pieza de una opción (262), sin cantidades; con más de diez opciones o si vienen del servidor (un catálogo, Cargó), se busca adentro de la lista, con «Mostrar N más». Cada cambio se aplica enseguida, y en el teléfono ocupa la pantalla. El chip nombra lo elegido («Proveedor: Aceros Rosario»). El canal suma su filtro a la vista cuando haya más de uno.

**Exportar (126 y 226):** «Exportar respuestas a Excel» dice qué exporta en `DetailGrid`: «Respuestas», con la cantidad, y «Filtros», con los mismos chips de la planilla sin la ✕, o «Ninguno». Debajo, «Columnas», la pieza (262) con su (i), con «Todas las preguntas» y «Solo las visibles»; sin columnas ocultas, «Solo las visibles» queda apagada con «Se ven todas las columnas.». Al pie, «Archivo», con el nombre. Sin la frase con las cantidades ni «(13 columnas)». En el teléfono no hay «Columnas»: exporta todas las preguntas.

**Los nombres, los filtros, los conteos y una sola lectura** van en su [parte](15-parte-datos-y-contrato.md#nombres-filtros-y-conteos).

**El Excel (`.xlsx`)** va en su [parte](15-parte-datos-y-contrato.md#el-excel): hasta 10.000 respuestas, con los filtros aplicados y `formrecords.export`, sin trabajos de fondo.

**El tablero (57):** la pestaña «Tablero», armada sola desde los tipos de las preguntas de la versión activa, sin nada escrito en el código:
- arriba, «Respuestas» y, de las tres primeras preguntas de número, el total, el promedio, el mínimo y el máximo, cada uno con su nombre y ninguno debajo de otro (263);
- abajo, una tarjeta por gráfico: cuántas llegaron por día, semana o mes, con los días vacíos en tono suave; el total de cada número por período; el reparto de cada pregunta de elegir o de sí/no, y quién cargó. Una pregunta de fecha no tiene tarjeta. Las opciones y las personas ocupan hasta seis filas: con más, las cinco primeras y «Otras»;
- el eje por día lleva la fecha completa (27/09/2026) en cada marca y en el cartel de cada barra, con menos marcas para que entren, como dicen los [formatos del front](../../../../ArquitecturaBaseMutitenantFront/docs/rules/formatos.md) (decisión del usuario del 2026-11-10). Por semana y por mes, la marca lleva el primer día y el cartel, el rango («01/09/2026 – 30/09/2026»): el mes nunca va en letras (205);
- usa los filtros de la planilla, y todo se calcula en SQL en la zona de quien mira, hasta los últimos 366 períodos;
- cada tarjeta se ve como tabla, que ordena por sus columnas en el navegador (264), o se quita, y las de opciones y personas cuentan cantidad o el total de un número. «Agregar gráfico» devuelve lo quitado, y «Volver al automático» lo deja como al principio;
- lo quitado y qué cuenta cada tarjeta se guardan por persona y formulario. Los filtros, no.

**El detalle (266 y 267):**
- **la respuesta se lee como el formulario,** en la vista rápida y en la pantalla completa: las zonas del formulario, el orden del autor, la grilla de hasta tres por renglón y el ancho de cada tipo, con la versión con que se cargó, como dice su [parte](15-parte-datos-y-contrato.md#cómo-se-lee-una-respuesta-263-266-y-269). Cada pregunta es su control, con lo respondido adentro y sin poder tocarlo, y cada dato de una opción de catálogo, su campo en el mismo renglón, «Pregunta · Dato», en gris. Debajo del título va solo la línea gris de quién y cuándo (263), y la versión, en la banda («Versión 4»); los momentos pasan a la actividad;
- **la vista rápida,** al costado de la planilla: en el renglón del título, el número, «Abrir en pantalla completa» y «Abrir conversación» como íconos con su tooltip, ↑ ↓ (también con el teclado), ✕ y Esc. Debajo, la línea gris y las pestañas «Respuesta», con las zonas, y «Actividad». Al pie dice en qué respuesta va («2 de 4»);
- **la pantalla completa,** en `/org/formularios/:id/respuestas/:recordId/completa`: arriba, el número, la línea gris, ↑ ↓ y «Abrir conversación»; las zonas a todo el ancho y, debajo, «Actividad», abierta y sin plegar (163). El formulario queda en las migas;
- **la actividad (267),** renglón por renglón: una tabla con las columnas Fecha, Hora, Qué pasó, Quién, Canal, Detalle, Estado y Acciones (en el ⋮ del renglón, 261), con «—» donde no aplica. Las bandas por momento son renglones que separan: en «Al recibirla», la mandó y la respondió, con quién en cada una, por WhatsApp y «Hecho», sin acciones. En la vista rápida y en el teléfono, un recuadro por renglón, con «Qué pasó» arriba y los demás datos con su nombre, de a dos;
- **ubicar una respuesta:** la posición, la anterior y la siguiente salen de una sola consulta con ventanas de SQL, con el filtro y el orden de la planilla. Lo ausente, lo excluido y lo ajeno dan el mismo 404;
- **la dirección compartible** lleva solo el orden, las versiones y las columnas. Una entrada directa arranca sin filtros privados. «Volver» regresa a la misma fila, página, filtros y orden;
- **«Abrir conversación»** aparece solo si quien mira puede atender esa bandeja, y no manda nada al abrir.

## Pantallas

**Redibujados y aprobados el 2026-11-26** (lienzo v187; decisión 286).

Aprobados en el lienzo v150. Con el traspaso 5, los filtros (143 y 144) y el detalle (139) están redibujados, aprobados y publicados en el lienzo v160. El recorrido guiado suma dos circuitos: de pegar los proveedores a verlos en la planilla y el Excel, y de armar los campos a ver el CUIT sellado en la planilla. Muestran solo lo que existe hasta acá (78). Con los traspasos 17 y 18 se redibujan los que dice la tabla, con su decisión.

| Tablero | Qué muestra | Estado |
| --- | --- | --- |
| `FRM-Formularios-15` | la lista, con las columnas de respuestas y «Quien solo ve respuestas» | Aprobado. **Se redibuja:** el nombre abre «Diseño» y el número de respuestas, la planilla (275), y el encabezado que ordena (264) |
| `FRM-Respuestas` | la planilla, con «Columnas», «Empresa» y sus estados; suma «Elegir varias» y los campos sellados | Aprobado en la v160: «Filtros» y los chips (144). **Se redibuja:** «Ordenar por» y el encabezado que ordena, también en las subcolumnas (264), y «Empresa» en la pieza (262) |
| `FRM-Respuestas-Filtros` | «Filtros», que se despliega como «Columnas» | Aprobado en la v160 (143 y 144). **Se redibuja:** la pieza de una opción, con el buscador adentro (262) |
| `FRM-Respuesta` | la vista rápida | Aprobado en la v160 (139). **Se redibuja:** la respuesta como el formulario, con cada dato del catálogo en su campo (263 y 266), y la pestaña «Actividad» en recuadros (267) |
| `FRM-Respuestas-Exportar` | el diálogo de exportar | Redibujado con la 226 en la v175. **Se redibuja:** «Columnas» en la pieza (262) |
| `FRM-Tablero` | el tablero | Aprobado. **Se redibuja:** el total, el promedio, el mínimo y el máximo, ninguno debajo de otro (263), y la tabla de una tarjeta, que ordena (264) |
| `M-FRM-Respuestas`, `M-FRM-Respuesta`, `M-FRM-Tablero` | los tres, en el teléfono. Tocar una fila abre `M-FRM-Respuesta`: no hay vista rápida | `M-FRM-Respuestas` y `M-FRM-Respuesta`, aprobados en la v160 (139 y 144), y `M-FRM-Respuesta`, redibujado sin «Las dos plegadas» y aprobado en la v168 (163); `M-FRM-Tablero`, aprobado. **Se redibujan:** `M-FRM-Respuestas`, con «Ordenar por» a todo el ancho (264); `M-FRM-Respuesta`, como el formulario en una columna y la actividad en recuadros (266 y 267), y `M-FRM-Tablero`, como `FRM-Tablero` (263) |
| `FRM-Respuesta-Completa` | la pantalla completa: la respuesta y su actividad | Redibujado y aprobado en la v168 (163 y 164). **Se redibuja:** las zonas del formulario (266) y la actividad en ocho columnas, con su banda (267) |
| `M-FRM-Formularios-15` | la lista en el teléfono, con «N respuestas · estado» | Aprobado. **Se redibuja:** sin datos juntados con «·» debajo del nombre (263) |

**Corregidos en el lienzo v169,** porque chocaban con una decisión: la hoja «Filtros» de `M-FRM-Formularios-15` va a pantalla completa y sin cantidades (144 y 164), y `M-FRM-Respuestas` muestra el nombre entero (174) y exporta sin leyenda (109).

**Estados que se dibujan:**
- un cliente, con «Sí» en «Es cliente», y «Persona sin información»;
- «Elegir varias»: «Fallas» con dos opciones y su filtro de una opción, en «Filtros» (143);
- los campos sellados: las subcolumnas con «—» en una respuesta de antes de los campos, y cada dato en su campo en el detalle;
- una zona plegada, con «N de M respondidas», y la respuesta en tres, dos y una columna;
- con `forms.read` y sin `formrecords.read`: la lista sin las columnas de respuestas.

**Mandan sobre esta spec en:**
- **Las fichas del front:** un dato por columna y nada debajo de otro dato (206 y 263); fechas completas, con la hora después de una coma (205), en la zona de quien mira; porcentajes pegados (208); sin leyendas (109). Toda tabla lleva una línea fina entre columnas, con los números a la derecha, y cierra abajo (137).
- **La planilla que vuelve a leer** atenúa sus filas y corre una línea bajo el encabezado, y la lectura anterior no se puede tocar (115).
- **La navegación (34 y 79):** el menú dice «Gestión de formularios», y su panel tiene Formularios y Catálogos, sin «Respuestas».
- **La planilla** se desplaza a lo ancho dentro de su caja, con N° fijo a la izquierda. Es la tabla con columnas fijas de la E12.
- **«Ya tenés una exportación en curso»** es la barrera de exportar. **«No pudimos actualizar»** conserva lo que se ve.
- **Exportar** no aparece sin `formrecords.export`, y sin respuestas queda grisado, con el motivo en su tooltip (43).
- **Valen las de toda la app** (160 a 164, 173 a 175, 180, 261 a 264 y 269): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero, el estado dicho una vez, lo que se elige en la pieza, toda columna con un dato ordena y ninguna fila lleva un botón con texto.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E12]` y `[E14]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | La condición, sus operadores por tipo, los errores y el permiso | `D/Forms/FormRecordCondition.cs`, `FormRecordFilterErrors.cs`, `FormRecordExportErrors.cs`, `FormDashboardErrors.cs`; suma a `Permissions.cs`, `Permissions{,.en}.resx` y `FormRecordErrors.cs` | `D/Forms/FormCatalogErrors.cs` [E12] | operadores por tipo; `PermissionTextsTests`; un rol con solo `formrecords.export` ve la planilla (260) |
| B2 | Tablas, índices y migraciones | `I/Modules/Forms/Persistence/Configurations/FormDashboardSettingsConfiguration.cs` y `FormRecordCatalogFieldConfiguration.cs`; las dos migraciones | `AssistantOrganizationSettingsConfiguration.cs`, `20261010203618_SystemRoleAssistantPermissions.cs` | aislamiento; `SearchIndexTests`; hacia atrás |
| B3 | Las columnas de todas las versiones, deserializadas una vez | `A/Modules/Forms/Services/FormRecordColumnsBuilder.cs`; suma a `FormRecordWriter.cs` [E14] | `FormCatalogExportBuilder.cs` [E12] | orden activo y retiradas; subcolumnas; publicación concurrente; una captura repetida no duplica |
| B4 | Filtros y orden en una sola función, con la planilla | `A/Modules/Forms/Interfaces/Persistence/IFormRecordReader.cs`, `I/Modules/Forms/Persistence/Readers/FormRecordReader.cs`, `FormRecordQueryFilters.cs`; `A/Modules/Forms/Services/FormRecordFiltersPolicy.cs`, `FormRecordQueryService.cs` | `I/Persistence/Readers/CompanyReader.cs`, `FormCatalogFiltersPolicy.cs` [E12] | la tabla de verdad de `unanswered` y `notAsked`; cero y `false`; `anyOf`; opción retirada; orden por opción y por un dato sellado, con lo vacío al final (264); una segunda página |
| B5 | Nombres por lote | suma a `IMemberDirectoryReader.cs`, `MemberDirectoryReader.cs`, `IStructuredContactSource.cs`, su apagado y el de WhatsApp | esos mismos [E12] | un solo pedido por página; ex miembro, sin dato y sin WhatsApp; `IdentityAccessTests` |
| B6 | Conteos | suma a `FormRecordReader.cs` y `FormRecordQueryService.cs` | `CompanyReader.cs` | cada dimensión ignora la suya; históricas en cero; una respuesta en dos opciones |
| B7 | Detalle, origen y navegación | `A/Interfaces/Integrations/StructuredData/IStructuredRecordOriginSource.cs`, su modelo, `I/StructuredData/DisabledStructuredRecordOriginSource.cs`, `A/Modules/WhatsApp/Services/WhatsAppStructuredRecordOriginSource.cs`; suma al reader | `IStructuredContactSource.cs` [E12] | la forma de la versión de la respuesta (266); sin permiso de la bandeja no hay `conversationId`; sin WhatsApp; `ROW_NUMBER`, `LAG` y `LEAD`; los tres 404 iguales |
| B8 | Excel | `A/Modules/Forms/Interfaces/Integrations/IFormRecordSpreadsheet.cs`, `I/Modules/Forms/FormRecordSpreadsheet.cs`, `A/Modules/Forms/Services/FormRecordExportService.cs`, `FormRecordSheetBuilder.cs` | `IWhatsAppContactSpreadsheet.cs`, `WhatsAppContactSpreadsheet.cs` | 10.000 y 10.001; `Busy` entre dos exportaciones, y una carga de catálogo no la frena; presupuesto; tipos de celda; 16 dígitos como texto; un texto con `=`; «; »; bytes abiertos con ClosedXML |
| B9 | Tablero y ajustes | `A/Modules/Forms/Services/FormDashboardService.cs`, `FormDashboardBuilder.cs`; repositorio de los ajustes; suma a `FormsAccountDeletionParticipant.cs` y a su cleanup | `AssistantAccountDeletionParticipant.cs` | agrupar en otra zona; días vacíos; hasta seis filas, con «Otras»; sin tarjeta para una fecha; un `countBy` por tarjeta; sumas sin redondeo; la baja de la cuenta borra los ajustes |
| B10 | La lista | suma a `IFormReader.cs` y `FormReader.cs` [E14] | esos mismos | sin consultas por fila; `overview` con el nombre de la versión activa; borrador cambiado; cada orden, sin respuestas al final |
| B11 | Rutas y ficha | `P/Modules/Forms/Controllers/BusinessFormRecordsController.cs`, `FormRecordFileResults.cs`, `Contracts/*`, `FormsContractContributor.cs`; suma la variante a `paginado-y-busqueda.md`; suma las relaciones a `GrowingCollectionContractTests` | `BusinessAssistantController.cs`, `WhatsAppFileResults.cs` | cada ruta con 401, 403, feature apagada = 404, 400 y paginado; `ToString` sin filtros; los filtros privados nunca por query; ejemplos exportados |
| B12 | Medir el volumen | prueba de volumen del módulo | la de la E12 | EXPLAIN sobre el SQL real de EF con 10.000 respuestas |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` | la API de la E14 | zod estricto con los ejemplos |
| F2 | La lista y las pestañas | suma a `FormsPage.tsx` y a las pestañas [E14]; `registration.ts` | su tablero | las tres combinaciones de permisos; adónde abren el nombre y el número (275); el orden (264) |
| F3 | La planilla | `pages/FormRecordsPage.tsx`, `lib/recordFilters.ts`, `lib/values.ts` | su tablero | «—», «Sin respuesta», cero y `false`; «Es cliente» solo si alguna fila de la página es de un cliente, sin marca al lado del nombre (206); el encabezado y «Ordenar por», el primer toque en el sentido útil y la página 1 (264); lo privado fuera de la URL; 403 retira todo; axe |
| F4 | «Filtros» y los chips | `components/records/RecordFiltersMenu.tsx`, `RecordFacetControl.tsx` | su tablero | cada cambio se aplica enseguida; la pieza de una opción, sin cantidades, con el buscador adentro y «Mostrar N más» (262); el chip nombra lo elegido; «Limpiar todo»; una faceta que falla no frena |
| F5 | La respuesta como el formulario y la vista rápida | `components/records/RecordForm.tsx`, `RecordValues.tsx`, `lib/recordForm.ts`, `QuickView.tsx`, `RecordActivity.tsx`; la lectura de `Picker` (varias enteras, sin «y N más» ni «…», y el campo baja de renglón, 174 y 266), excepción escrita en las fichas de pantallas y de formatos del front | su tablero | los dos anchos, las columnas según el ancho de la zona, una pregunta con sus datos en un renglón, «N de M respondidas» y lo que no aporta (266); teclado, Esc y foco útil; una respuesta tardía se descarta; la actividad en recuadros (267) |
| F6 | La pantalla completa | `pages/RecordFullPage.tsx` | su tablero | entrada directa sin filtros privados; «Volver» a la misma fila; el teléfono; la actividad en ocho columnas, con «—» y su banda (267) |
| F7 | Exportar | `components/records/ExportDialog.tsx` | su tablero | más de 10.000; `Busy`; «Exportando…» y el diálogo abierto hasta la respuesta; «Columnas» en la pieza (262) |
| F8 | El tablero | `components/records/FormDashboard.tsx`, sin biblioteca de gráficos | su tablero | quitar, agregar, contar y volver al automático; ver como tabla, que ordena (264); ningún número debajo de otro (263); axe |

**Al cerrar:** `docs/features/formularios.md` suma la sección de las respuestas.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | `FormRecordValues`, `FormVersions.Questions` y `ChoiceFields`. | Tal como dicen la §3 y T22: filas tipadas y el texto sellado también en las opciones propias. |
| [E14] | Los nombres de la lista, las pestañas y el participante de la baja. | Los de la columna «Archivos». La cuenta dada de baja deja `RespondentUserId` en `null`. |
| [E14] | La sesión de WhatsApp a partir del `SourceKey`. | `whatsapp:<sessionId>` llega a la conversación, a quien mandó y a cuándo. |
| [E12] | La barrera `IFormWorkLease` y `IMemberDirectoryReader`. | La barrera admite otro carril; el lector suma nombres por id, también de quien ya no es miembro, y empresas. |
| [E12] | La tabla con columnas fijas. | Es la de las opciones, que la planilla usa con N° fijo. |
| [E11] | `/org/whatsapp/chats/:conversationId`. | Abre las dos bandejas, y no hace falta decir cuál. |
| [E9] | Las cuotas. | Las rutas cuentan en las generales, sin perfil propio. |
| Front | El panel al costado. | Si ninguna etapa anterior lo trajo, entra acá. |
| EF | Agrupar en SQL en otra zona horaria. | Se puede con la versión de Npgsql del repo, sin SQL a mano. |

## Decisiones tomadas (no se vuelven a preguntar)

- **La planilla pagina por páginas y con total,** aunque las respuestas solo crezcan: es un listado de negocio, con filtros y conteos, no un historial como la auditoría o los mensajes, que paginan por cursor. Lo sostienen sus índices, y se prueba con volumen.
- **La navegación** (34), con una sola lista «Formularios» y pestañas por formulario (79).
- **Sin migrar:** las respuestas de las etapas anteriores aparecen tal cual.
- **Exportar:** `.xlsx`, con permiso propio, hasta 10.000 respuestas y sin trabajos de fondo.
- **«Columnas»** vive en la URL y no se guarda en el servidor; los ajustes del tablero, sí.
- **El tablero no compara** con el período anterior, porque el tablero aprobado no lo tiene.
- **Cargó no se ordena:** el nombre vive en la cuenta o en WhatsApp, y Forms no los lee.
- **Los topes de esta etapa** (10.000 respuestas, 30 s, 366 períodos, 20 condiciones, 100 valores y 100 claves) son técnicos o de capacidad y quedan en el código (270).

## Acciones del asistente

Ninguna ([0068](../../decisions/0068-acciones-del-asistente.md)): la etapa solo lee, y los ajustes del tablero son la vista de cada persona.

## Fuera de esta etapa

- Importar respuestas desde Excel, exportar los archivos, exportar en segundo plano y los permisos por formulario.
- Quién usa `answered`, `unanswered` y `notAsked`, que el contrato ya trae: las reglas (E21) y las sugerencias (E23).
- Lo que suman las etapas siguientes: el papel «Filtro» de un campo (E16); las fotos, los documentos y «Descargar ▾» (E18); las líneas, los cálculos, «no se mostró» y los montos por moneda del tablero (E19); «Cargar», «Agregar respuesta», el canal «App», su filtro y lo propio de quien solo carga (E20); y el estado, «Por aprobar», la aprobación, la tarjeta «Por estado», el PDF, los avisos, corregir y anular (E21).
- En la lectura (266 y 267), cada etapa suma lo suyo a la forma y renglones a la actividad: la E20 vuelve editable la forma que arma esta, y la E21 corrige en el lugar.

## Cómo se cierra

La revisión, con su lista de control; el recorrido, en la prueba integral. No hace falta una prueba nueva con Meta: lee lo que ya llegó. El recorrido prueba:
1. respuestas reales de la E14, con un catálogo con campos, «elegir varias» y una opcional vacía, en la lista, la planilla, ordenada por un dato sellado, los filtros, la vista rápida, la pantalla completa y el tablero;
2. un cliente con su nombre y «Abrir conversación»;
3. un enlace a una respuesta abierto en otra pestaña, sin la búsqueda ni los filtros privados;
4. el `.xlsx` abierto en Excel, sin reparación;
5. EXPLAIN sobre unas 10.000 respuestas, en la lista, la planilla, los conteos, la navegación, el Excel y el tablero.

## Requisitos previos

La E14 cerrada.

## En el multitenant

- **Datos de la organización:** todo se lee dentro de RLS. Las tablas nuevas son `FormDashboardSettings` y `FormRecordCatalogFields`, que escribe la captura de la E14.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** suma `formrecords.export`; `formrecords.read` es de la E14.
- **Sin WhatsApp:** Cargó dice «Cliente», en lugar del nombre, y no hay «Abrir conversación».
- **Cuotas:** las rutas cuentan en las cuotas generales.
