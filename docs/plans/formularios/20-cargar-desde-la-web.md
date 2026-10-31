# E20 · Cargar una respuesta desde la web (spec)

**Estado:** pendiente · spec de la etapa 20 del bloque (decisiones 73, 78, 83 a 90 y 94 a 98), con el [formato](plantilla.md) de las etapas. Corresponde a la Etapa 1 de «Cargar formularios desde la app» de ArquitecturaBase, programada y revisada allá el 2026-10-30, y a sus etapas 2 (lo propio), 4 (listas dependientes) y 5 (títulos de sección), decididas y sin programar. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E19.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T4, T22, T23, T24 y la §6, punto 7; la [E15](15-respuestas.md): rutas y filtros; la [E16](16-datos-de-las-personas.md): los filtros por destinatario; la [E17](17-ciclo-de-vida.md): la disponibilidad; la [E18](18-fotos-y-documentos.md): verificar y guardar archivos, y la [E19](19-condiciones-calculos-y-lineas.md): la captura.
2. **Decisiones** de la hoja de ruta: 9, 35, 37, 43, 50, 54, 73, 78, 83 a 90 y 94 a 98.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [idempotencia](../../rules/idempotencia.md) | el `POST` con archivos, en modo durable |
   | [guardado](../../rules/guardado.md) y [multitenancy](../../rules/multitenancy.md) | subir fuera de la UoW; quien carga sale de la sesión |
   | [permisos](../../rules/permisos.md) y [api-http](../../rules/api-http.md) | rutas aparte para cada permiso |
   | [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) | las opciones, de a 50, y lo propio |
   | [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | cada error en su pregunta |
   | [textos-y-traducciones](../../rules/textos-y-traducciones.md) y [tests](../../rules/tests.md) | «App», y qué lleva test |

4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en ArquitecturaBase, `docs/plans/2026-10-30-traspaso-multitenant-carga-desde-la-app.md`, `docs/plans/2026-10-29-cargar-formularios-desde-la-app.md` y `docs/features/formularios.md`, «Carga desde la app». En `ArquitecturaBaseFront`, `docs/design/carga-desde-la-app.md` y los tableros CW0 a CW16. Los commits están en la copia local. Se toman las pantallas y las reglas; el código sale de este repo (18).
6. **La maqueta no sirve de base:** tiene una versión anterior, con «Cargar respuesta» en el ⋮ y el canal «Web».

## Qué se construye

**Un canal más para responder:**
- un empleado completa un formulario desde la web, a su nombre. Es un miembro activo con `formrecords.create`, sin teléfono verificado (89), y solo en los formularios activos que son para empleados o para los dos (50). Un cliente sigue respondiendo por WhatsApp;
- la respuesta guarda el canal `web`, el `SourceKey` `web:<clave de idempotencia>` y las empresas activas de quien carga (9);
- **la misma captura que WhatsApp:** un solo lugar valida y numera contra la versión sellada, con catálogos, archivos, condiciones, cálculos y líneas;
- **solo la versión activa.** Si se publicó otra, el front ofrece pasar a la nueva sin perder lo cargado;
- **la oferta sella solo lo elegido:** cada opción tiene que estar activa y pasar los filtros para esa persona, también en un catálogo de miembros o de contactos. La lista completa no se guarda;
- **los filtros por persona:** si le falta el dato, la pregunta frena y lo nombra. Si los filtros dejan una sola opción, ya viene elegida. Las opciones no tienen el tope de 200: se piden de a 50, con búsqueda, y los filtros corren en SQL antes de paginar.

**Las listas dependientes** (94): un tercer filtro, «según otra respuesta»:
- la pregunta de la que depende va antes, es de elegir una y elige del mismo catálogo que el campo de papel «Filtro». No oculta la pregunta: achica sus opciones;
- al guardar se comprueba que la opción corresponda a esa respuesta. Si la pregunta de la que depende quedó oculta, la lista también;
- va en una pregunta suelta, no en una columna de un grupo. Copiar un formulario la copia.

**Archivos:** viajan en el mismo `POST`, con los límites de la E18, y se verifican por contenido, igual que los de WhatsApp. Completa o nada.

**Reintentos:** la misma clave con las mismas respuestas devuelve el primer resultado, aunque después se haya publicado otra versión o se haya dado de baja el formulario. El número y los cálculos son privados, por eso no va en `BufferedJson`. La misma clave con otras respuestas es 422; para el front, otras respuestas son otro envío.

**Formularios solo para la app** (94, 96 y 98). No se pueden mandar por WhatsApp en tres casos: la organización no tiene un número conectado, usa una lista dependiente, o usa en «elegir una» un catálogo a mano de más de 200 opciones. En los tres:
- si es para empleados o para los dos, se publica con un aviso que no frena (37). No pasa por Meta: sella la versión, la marca y la activa en la misma UoW, sin trabajo durable ni Flow;
- si es solo para clientes, no se publica, porque ellos responden solo por WhatsApp;
- el envío desde el chat no lo ofrece;
- para mandarlo cuando se pueda, hay que volver a publicar: `NothingToPublish` no aplica si la versión activa es solo para la app y ahora se puede mandar. Cambia lo que la E14 dice de `ChannelUnavailable` y `CatalogTooLarge`.

**Lo propio** (85): quien tiene `formrecords.create` y no `formrecords.read` lee solo sus respuestas, en la planilla, el detalle y los archivos, también lo que respondió por WhatsApp. El servidor filtra por `RespondentUserId`, y lo ajeno responde 404, también en una dirección directa. Quien solo carga ve «Formularios» con sus formularios y «Cargar», sin conteos; el nombre abre «Respuestas» con lo suyo.

**Las entradas** (83): «Cargar» al final de cada fila de «Formularios». No aparece en los dados de baja, y en uno solo para clientes queda grisado, con el motivo en su tooltip (43). «Agregar respuesta» va en el encabezado de la planilla: en la compu abre el panel, y en el teléfono, la página.

**La página «Nueva respuesta»** (84), en `/org/formularios/:id/cargar`, para la compu y el teléfono:
- Cancelar y el botón del formulario van en la banda; en el teléfono, ese botón queda fijo al pie;
- se valida al enviar: cada error en su pregunta y el foco en la primera. Después, en vivo. Salir con algo cargado pide confirmación;
- mientras sube, el botón queda grisado con su mismo texto y una barra de avance, sin palabras, muestra cuánto falta (43 y 95);
- lo cargado vive en la página: nunca va a la URL, al storage ni a la caché.

**El panel** (84), al costado de la planilla, solo en la compu: «Mantener las respuestas para la siguiente» conserva todo menos los archivos, y «Pantalla completa» pasa lo cargado a la página. Al enviar, el aviso dice el número y la planilla se relee con la respuesta marcada.

**Las listas de catálogo:** una pregunta de catálogo es siempre una lista con búsqueda en el servidor, con el total, la descripción y «Mostrar 50 más». En el teléfono ocupa la pantalla, con «Listo» al pie al elegir varias. Una lista dependiente espera la respuesta de la que depende y se limpia si esa respuesta cambia.

**Las fotos y los documentos:** se arrastran o se eligen, con un contador. Tipo, tamaño y cantidad se revisan antes de enviar. En el teléfono, «Tomar foto» va primero, y «Solo cámara» no ofrece la galería.

**«Enviada»:** lo que se mandó, los cálculos, el número y la hora, con «Cargar otra» y «Ver en la planilla». Se arma con lo enviado y con lo que devuelve el `POST`.

**En lo que ya existe:** «App» en «Canal» y en su filtro, en los conteos, la navegación y el Excel (90); en el detalle, «Recibida · … · en la app, la cargó …», sin «Abrir conversación»; en el editor, «Según otra respuesta»; al publicar, que queda solo para la app y por qué, y «Solo en la app» en «En WhatsApp» (98).

### Cómo se arma cada pregunta (86 y 87)

El autor decide el contenido. La app decide la forma, con una tabla fija por tipo:

| Ocupa | Tipos de pregunta |
| --- | --- |
| Una columna | Número, fecha, sí o no, texto corto de hasta 30 caracteres y elegir una con lista desplegable |
| Dos columnas | De un catálogo (una o varias), elegir una con opciones en fila (hasta cinco, de hasta 20 caracteres) y texto corto de hasta 80 |
| El renglón entero | Texto largo, texto corto de más de 80 o sin largo máximo, elegir varias con opciones propias, foto, documento y grupo de líneas |

- **Columnas:** tres en la compu, dos en la tableta y una en el teléfono y en el panel. Salen del ancho de la zona: tres desde 46 rem, dos desde 30 rem y una por debajo.
- **Orden:** las preguntas no se reordenan. Una que no entra baja de renglón, con una línea entre cada renglón visible.
- **Zonas** (88): cada título de sección arma una zona, y lo que va antes del primero queda en «Datos». Las fotos y los documentos van al final, en «Fotos y documentos». Todas se pliegan y arrancan abiertas; plegada, la banda dice «N de M respondidas», o lo adjuntado, como «2 fotos · 1 archivo». Al enviar con errores, la zona plegada que los tiene se abre sola.
- **Controles:** número y fecha son angostos; sí o no arranca sin nada marcado; hasta cinco opciones cortas van en fila, y con más, una lista; elegir varias muestra «Elegí hasta N»; el texto largo tiene de 2 a 6 renglones.
- **La unidad** va en la etiqueta que escribe el autor (86).
- **Lo condicional:** una pregunta aparece en su lugar y los renglones se recalculan. Lo oculto no se manda. «N de M» cuenta solo lo visible, y una zona sin preguntas visibles no se ve. **Un cálculo** no se ve al cargar, y aparece en «Enviada» (54 y 97).

## Pantallas

Ninguno está dibujado. Diseño los dibuja con el [texto de la etapa](20-texto-diseno.md). Las referencias, entre paréntesis, son del lienzo de ArquitecturaBase.

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Formularios-20` y su teléfono (CW1-B) | «Cargar», «Solo en la app» y lo que ve quien solo carga |
| `FRM-Cargar-Respuesta` y su teléfono (CW2-A, CW12, CW13-A y CW16) | la página, con zonas, grilla, títulos, lo condicional, líneas, errores y salir |
| `FRM-Cargar-Panel` (CW2-B) | el panel junto a la planilla |
| `FRM-Cargar-Lista` y su teléfono (CW3 y CW7) | la lista, una sola opción, sin el dato, elegir varias y la que espera otra respuesta |
| `FRM-Cargar-Archivos` y su teléfono (CW4) | agregar, los límites, la barra de avance, el corte y la cámara |
| `FRM-Cargar-Enviada` y su teléfono (CW5) | «Enviada», versión nueva, dado de baja, opción dada de baja y sin conexión |
| `FRM-Respuestas-20` y `FRM-Respuesta-20`, con sus teléfonos (CW6-A) | «App», su filtro y el origen en el detalle |
| `FRM-Respuestas-Propias` y su teléfono (CW6-C) | la misma planilla con lo suyo |
| `FRM-Editor-Listas` y su teléfono (CW7) | «Según otra respuesta» |
| `FRM-Publicar-20` y su teléfono | solo para la app, en los tres casos, y uno solo para clientes que no se publica |

**Mandan sobre esta disposición:** el teléfono es una app: «Cargar» va en la lista con nuestra forma de fila. Valen las fichas del front y la decisión 43 en cada botón, incluso donde el lienzo de allá muestre otra cosa.

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `formrecords.create`. Son rutas aparte de las de `forms.read` y `formrecords.read`, porque `[HasPermission]` pide un solo permiso. La organización y quien carga salen de la sesión.

| Ruta | Pide | Devuelve |
| --- | --- | --- |
| `GET /entry` | `page`, `pageSize`, `search` | `PagedResult<{id, name}>`, solo lo que puede cargar, con el filtro de disponibilidad, sin conteos |
| `GET /{id}/entry` | — | `{versionId, versionNumber, name, submitLabel, maxFileBytes, questions[], titles[]}`. Cada pregunta de catálogo trae `options?{total, single?, missingData?}` |
| `GET /{id}/entry/questions/{fieldId}/options` | `page`, `pageSize` (50), `search`, `dependsOn?` | `PagedResult<{id, text, detail?}>` |
| `POST /{id}/records` | multipart: `versionId`, `answers` y los archivos, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]`, `[IdempotencyDurable]` y `[RequestSizeLimit]` de 101 MiB | 201 `{recordId, displayNumber, submittedAtUtc, calculations[]}` + Location |
| `GET /{id}/own-records/columns`, `POST …/search`, `POST …/facets`, `GET …/{recordId}`, `POST …/{recordId}/navigation` y `GET …/{recordId}/files/{fileId}/{thumbnail\|content}` | los de la E15 y la E18 | lo mismo, solo con lo de esa persona |

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| `GET /` y `GET /overview` | `canLoad` y `appOnly` |
| `GET /{id}` y `PUT /{id}/draft` | `recipientFilters[].mode` suma `answer`, con `sourceFieldId` |
| `POST /{id}/publication-checks` | `warnings[]` suma `AppOnly`, con `reason`: `NoNumber`, `DependentList` o `LargeCatalog` |
| `POST /{id}/publications` | 200 si queda solo para la app, porque termina en la misma UoW; si no, 202, como siempre |
| `POST …/records/facets` y `search` | la dimensión y el filtro `channel` |
| `GET …/records/{recordId}` | `enteredBy?{name, atUtc}` |

**Puerto:** `IStructuredFormChannel` suma `HasConnectedNumberAsync`. `ListSendableAsync` deja afuera las versiones solo para la app.

**Errores:**

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Entry.VersionChanged` | Conflict | se publicó otra versión |
| `Forms.Entry.Unavailable` | Conflict | el formulario se dio de baja o no es para empleados |
| `Forms.Entry.QuestionNotFound` | NotFound | una pregunta que no es de la versión |
| `Forms.Entry.AnswersInvalid` | Validation (`questions.<id>`) | cada error en su pregunta, con los códigos de la captura |
| `Forms.Draft.DependentListInvalid` | Validation (`questions[i].recipientFilters[j]`) | la pregunta de la que depende no va antes, no es de elegir una o elige de otro catálogo |
| `Forms.Draft.DependentListForCustomers` | Validation (`questions[i]`) | una lista dependiente en un formulario solo para clientes |

Valen `Forms.Record.OptionWithdrawn`, en su pregunta, y `Forms.Offer.RecipientDataMissing`, con el nombre del dato.

## Datos

Una migración aditiva, `<fecha>_FormEntry.cs`. Sin tablas nuevas.

| Tabla | Columna | Tipo | Regla |
| --- | --- | --- | --- |
| `FormVersions` | `AppOnly`, `AppOnlyReason` | bool, texto? | se sellan al publicar |
| `FormRecords` | `Channel` | texto | admite `web` |

No hace falta una columna como `EnteredInApp`: lo dice el canal. Quien cargó es `RespondentUserId`. `SourceKey` es `web:<clave>`, con el mismo índice único.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E19]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Una sola captura: separar lo común de WhatsApp | suma a `FormRecordService.cs` y `FormRecordWriter.cs` [E14] | esos mismos | WhatsApp sigue igual; numeración correlativa entre los dos canales |
| B2 | Leer para cargar: versión, opciones y filtros en SQL | `A/Modules/Forms/Services/FormEntryService.cs`, `FormEntryOptionsResolver.cs`; suma a `FormOfferBuilder.cs` [E16] | `FormStructuredSource.cs` [E14] | una sola opción; sin el dato; 10.000 opciones con búsqueda; miembros y contactos; otra organización |
| B3 | Listas dependientes | suma a `FormRecipientFilterPolicy.cs` [E16] y a `FormDraftEditor.cs` | esos mismos | cada motivo; opción que no corresponde; oculta; copiar |
| B4 | Enviar: archivos fuera de la UoW, captura, confirmar o borrar | `FormEntrySubmitService.cs`; `P/Modules/Forms/Controllers/BusinessFormEntryController.cs`, `FormEntryDurableAdapter.cs` | `AssistantAttachmentService.cs`, `AssistantControllerBase.Upload` | versión nueva; dado de baja; opción dada de baja; reintento con otra versión; otra respuesta = 422; 101 MiB; archivo falso; sin permiso dentro de la UoW |
| B5 | Publicar solo para la app | suma a `FormPublicationService.cs` [E14] y `WhatsAppStructuredFormChannel.cs` | esos mismos | los tres casos; solo clientes; sin Meta; volver a publicar con número; `ListSendableAsync` |
| B6 | Lo propio | suma a `FormRecordQueryService.cs`, `FormRecordFileService.cs` y las lecturas [E15 y E18]; las rutas `own-records` | esos mismos | lo ajeno = 404 en cada ruta; también lo de WhatsApp; sin `formrecords.read` |
| B7 | El canal en lo que ya existe | suma a `FormRecordQueryFilters.cs`, `FormRecordColumnsBuilder.cs` y `FormRecordSheetBuilder.cs` [E15] | esos mismos | filtro, conteos, navegación y Excel |
| B8 | Contratos y ejemplos | suma a `Contracts/*` y `FormsContractContributor.cs` | esos mismos | cada ruta con 401, 403, feature apagada = 404; `RouteIsolationMatrixTests`; ejemplos exportados |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos, llamadas y subida con avance | suma a `F/src/modules/forms/api/` y `F/src/shared/api/httpClient.ts`, que hoy no tiene avance | `httpClient.ts` | zod estricto; avance y corte; `sameOperation` |
| F2 | Entradas y lista | suma a la lista [E17] y a `FormRecordsPage.tsx` [E15] | esos mismos | quien solo carga; grisado; dados de baja |
| F3 | El armado de una pregunta | `F/src/modules/forms/components/entry/EntryGrid.tsx`, `EntryZone.tsx` | el tablero | la tabla de anchos; `ResizeObserver`; lecciones 1 a 3 |
| F4 | Los controles y la lista de catálogo | `components/entry/EntryField.tsx`, `EntryCatalogList.tsx` | `QuestionEditor.tsx` [E14] | búsqueda, «Mostrar 50 más» y lista dependiente; axe |
| F5 | Lo condicional | suma a `EntryGrid.tsx`, con `lib/conditions.ts` [E19] | el mismo | lo oculto no se manda; «N de M» |
| F6 | Los archivos | `components/entry/EntryFiles.tsx` | el tablero | límites antes de enviar; cámara; barra sin palabras |
| F7 | La página, el panel y «Enviada» | `pages/LoadResponsePage.tsx`, `components/entry/EntryPanel.tsx`, `EntrySent.tsx`; `registration.ts` | `useEditForm` y `useUnsavedChangesGuard` | salir; versión nueva sin perder nada; mantener; lo cargado fuera del storage |
| F8 | Publicar, editor y lo propio | suma a `PublishDialog.tsx` [E16], `CatalogQuestionFields.tsx` [E16] y `FormRecordsPage.tsx` | esos mismos | los tres casos; «Según otra respuesta»; una dirección ajena |

**Al cerrar:** `docs/features/formularios.md` suma la sección.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | La captura y la publicación. | Se pueden separar lo común y una publicación sin trabajo durable. |
| [E15] | Las lecturas. | Admiten un alcance «solo lo mío», sin copiar las consultas. |
| [E16] | La preparación de la oferta. | Sirve también paginada, sin el tope de 200. |
| [E18] | `IFileContentInspector` y el diario. | Sirven igual para un archivo que llega por multipart. |
| [E19] | `FormConditionEvaluator` y `lib/conditions.ts`. | El front y el servidor dan lo mismo. |
| Front | `useIdempotentMutation`, `useUnsavedChangesGuard` y el panel al costado. | Están, o llegan con la base de la 12. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Los renglones se arman con las columnas en uso.** Se armaban siempre de a tres, y en el teléfono dos preguntas quedaban sin línea entre ellas.
2. **Las columnas salen del ancho de la zona, no del de la pantalla.** Se mide con `ResizeObserver`.
3. **En el panel, número y fecha siguen angostos.**
4. **Las migas también miden su contenedor.**

## Decisiones tomadas (no se vuelven a preguntar)

- **Un empleado, a su nombre** (73), sin permisos nuevos ni teléfono verificado (89).
- **La misma captura que WhatsApp.**
- **Sin borradores guardados:** lo cargado no sobrevive a la página.
- **El canal se llama «App»** (90); el valor técnico sigue siendo `web`.
- **Se publica sin WhatsApp** (98).
- **Las listas dependientes** van acá y solo en la web (94).
- **Lo propio tiene sus rutas:** `[HasPermission]` pide un solo permiso, y el mismo servicio les da el alcance.

## Fuera de esta etapa

- Cargar a nombre de otra persona, importar muchas respuestas desde un Excel, borradores guardados y carga sin conexión.
- La carga en filas (CW2-C), y ofrecer en la web qué cargar después.
- Las listas dependientes en WhatsApp, con el intercambio cifrado con Meta.
- El estado, el PDF y el motivo de rechazo en lo propio, y corregir y anular, que suma la E21.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más un **recorrido real**:
1. cargar una respuesta con una condición, dos renglones, un cálculo y una foto, y ver que queda igual que una por WhatsApp, con el canal «App»;
2. repetir el envío después de un corte: devuelve el primer número, sin saltos;
3. publicar otra versión con la página abierta: pasa a la nueva sin perder lo cargado;
4. entrar con una cuenta que solo tiene `formrecords.create`: ve sus formularios, carga y ve solo lo suyo. Una dirección directa a una respuesta ajena da 404;
5. en una organización sin número conectado, publicar, cargar desde la web y, al conectar un número, volver a publicar y mandarlo por WhatsApp;
6. elegir en una lista dependiente, cambiar la respuesta de la que depende y ver que se limpia;
7. sacar una foto con la cámara de un teléfono real.

## Requisitos previos

La E19 cerrada.

## En el multitenant

- **Datos de la organización:** las tablas de la E14, con RLS. Cada ruta nueva lleva su test de aislamiento, y lo propio, el suyo: otra persona de la misma organización recibe 404.
- **Idioma:** el contenido no tiene idioma (35), así que `PresentationCulture` de allá no se toma.
- **Datos de las personas:** los de esa persona en esa organización (42).
- **Feature `forms`:** las rutas la llevan.
- **Permisos:** `formrecords.create`, sin permisos nuevos.
- **Almacenamiento:** los archivos van en `IFileStore`, como los de la E18.
- **Cuotas:** las rutas cuentan en las cuotas generales.
