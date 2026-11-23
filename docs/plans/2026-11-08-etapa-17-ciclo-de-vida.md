# E17 · Ciclo de vida y copiar · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/17-ciclo-de-vida.md`, en un solo archivo. Tableros: lienzo v175, salvo los que la spec redibuja con el traspaso 18, que se dibujan antes del front. No queda nada para decidir.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0, K1 | Pendiente |
| Back | B1–B13 | Pendiente |
| Front | F1–F6 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: el contrato primero; después Back y Front en paralelo; una sola puerta al final. Cada chat sigue las reglas de su texto, y un arreglo corre solo lo que toca. La prueba con Meta queda para la I1 de la 10 (123).

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md)): la llevan las tareas **(R)**, completa y antes de su commit; un subagente nuevo recibe el código y los escenarios, sin la conclusión de quien programó.

## Qué se verificó al arrancar

| Fila | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [E14], [E15] y [E16] | todavía no existen | van a K0 |
| [Meta] deprecar | la API de Flows vigente: `POST /{flow-id}/deprecate` → `{success: true}`; un Flow `DEPRECATED` no se manda, no se abre ni vuelve | confirma la spec: sin sesiones vigentes; «ya deprecado» se confirma con `GET /{flow-id}`. La prueba real va a I3 |
| #51 | `TenantContext.Enter` deja `TenantKind` en nulo y falla con una transacción abierta | el worker escribe solo filas `ITenantOwned`: alcanza `TenantJobRunner` |
| `TenantJobRunner.RunAsync` | no aísla fallas: una excepción corta a las organizaciones que siguen | B11 envuelve cada organización con `try/catch` y `ExceptionLog.Write`, como `AssistantRetentionWorker` |
| `IWhatsAppChannelReader.ListOrganizationIdsAsync` | lista solo números vigentes; desconectar conserva `EncryptedToken` | B11 suma una lista que incluye los desconectados |
| Borrado lógico | `ISoftDeletable`, el filtro `SoftDelete` y `SoftDeleteInterceptor`; `QueryFilterBypassTests` deja omitir solo ese filtro | `FormDefinition` se borra con `Remove`; lo borrado se lee con `IgnoreQueryFilters(["SoftDelete"])` |
| Auditoría | `IAuditLog.Record` y `AuditAction` (`Created`, `Updated`, `Deleted`, `Restored`, `Custom`) | la baja y la reactivación van con `Updated` y su `AuditSummary` |
| Worker de modelo | `WhatsAppBroadcastWorker`: `PeriodicWorker` de un minuto, apagado sin `ProcessInboundInBackground` | B11 lo copia |

## Contrato

### K0 · Lo que dejó la etapa anterior

El chat del contrato confirma esta lista cerrada contra el código. Lo que difiera va a «Cambios al contrato» antes de programar.

- **Base de la 12:** `ConfirmDialog` con el foco en «Cancelar» (F13), `RowAction.disabledReason` (F10), los botones que trabajan (F1) y `PageLoading` (F12). De su segunda parte (214), el motivo de `RowAction.disabledReason` y de `Button.disabledReason` se abre con un clic o un toque, también en la hoja del teléfono (211), y una confirmación dice en una frase lo que se afecta (213); la pieza `Picker` (262) y el orden por columnas (264).
- **E12 y E13:** `FormCatalogFiltersPolicy.cs` y `FormCatalogService`, con `Forms.Catalog.ItemInUse`.
- **E14, back:** `FormDefinition` (`IAuditable`, `IVersioned`, `UniqueKeys.FormPrefix`); `FormDefinitionState` (`ActiveVersionId`, `PrefixLockedAtUtc`, `LastRecordNumber`, `Activate`); `FormVersion`; `FormPublication`, con sus ocho estados y `UniqueKeys.FormPublicationOpen`; `FormDefinitionErrors` (`NotFound`, `PrefixTaken`, `PrefixInvalid`), `Forms.Publication.InProgress` y `NothingToPublish`; `FormDefinitionService`; `FormPublicationService` (`CheckAsync`, `CompleteAsync`, `ResolveAsync` y `DiscardAsync`, con `POST …/publications/{publicationId}/discard`, 225); `FormVersionCreator`, `FormPrefixResolver`, `FormStructuredSource` y `FormReader`; los puertos `IStructuredFormSource` e `IStructuredFormChannel`, con sus apagados en `I/StructuredData/`; `WhatsAppFlowBinding` (`State`, `WabaId`, `MetaFlowId`, `VersionId`); `WhatsAppFlowSession` (`State`, `ExpiresAtUtc`, `VersionId` y `ResponseMessageId`, que se fija al recibir); `IWhatsAppFlowPublicationService`, `WhatsAppFlowClient` y `WhatsAppStructuredFormChannel`; `BusinessFormsController`, `FormsContractContributor` y `FormsTexts{,.en}.resx`; los permisos `forms.read`, `forms.manage` y `forms.publish`; en los catálogos, `FieldInUse` (con `forms[]`), `usedByCount`, `usedIn` y la advertencia al dar de baja.
- **E14, front:** en `src/modules/forms/`, `api/`, `FormsPage.tsx`, `FormPage.tsx` con `formTabs.ts`, `FormEditorPage.tsx`, `PublicationBand.tsx` y `PublishDialog.tsx`; en `src/modules/whatsapp/chats/`, `SendFormDialog.tsx`.
- **E15:** `GET /` con `recordCount?` y `lastRecord?`, `GET /overview`, una sola función de filtros de la lista en `FormReader` y las pestañas del formulario.
- **E16:** `questions[].recipientFilters` en el borrador; `FormPersonDataReader`, que lee el uso de un dato y su «Lo usan»; `Forms.PersonData.InUse`; `FieldInUse` con `uses`, e `ItemInUse` con `uses: forms` y `uses: people`.
- **Las consultas de valores** de la E14 a la E16 van siempre con la definición de su respuesta, nunca solo por `FieldId`: copiar repite los `FieldId`.

### Rutas

Todas en `/api/organization/forms`, con `[Access(Access.Business)]`, `[FeatureGate("forms")]` y las cuotas generales, en `BusinessFormsController` y `FormsContractContributor.cs`.

| Método y ruta | Permiso | Pide → devuelve | Errores |
| --- | --- | --- | --- |
| GET `/{id}/lifecycle` | `forms.read` | → 200 `{openSends, lastExpiresAtUtc?, recordCount?, retirement?{state, deprecatedAtUtc?}}` | 404 `Forms.Definition.NotFound` |
| POST `/{id}/deactivation` | `forms.publish` | sin cuerpo, sin `version` ni `[Idempotent]`: lo protege el estado → 200 `{deactivatedAtUtc}` | 409 `Forms.Definition.AlreadyDeactivated`, `NotPublished` y `Forms.Publication.InProgress`; 404 |
| DELETE `/{id}?version=` | `forms.manage` | → 204 | 400 sin `version`; 409 `Forms.Definition.HasRecords`, `HasOpenSends`, `Forms.Publication.InProgress` y `General.ConcurrencyConflict`; 404 |
| GET `/{id}/copy-proposal` | `forms.manage` | → 200 `{name, prefix}` | 404 |
| POST `/{id}/copies` | `forms.manage` | `{name, prefix}`, con `[Idempotent]` → 201 `FormDetailResponse` + Location | 400 `Forms.Definition.PrefixInvalid` (`prefix`) y el nombre (`name`); 409 `PrefixTaken`; 404 |

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| GET `/` y `/filter-counts` | `status` suma `deactivated`, y `all` son los activos; `filter-counts` suma `deactivated`, para el resumen (262); `sort=status` pone los dados de baja al final (264). Cada fila suma `deactivatedAtUtc?` y `hasRecords`; una publicada o dada de baja, `openSends`, y una dada de baja, `retirement?` |
| GET `/overview` | `status` (`active` por omisión o `deactivated`); cada fila suma `deactivatedAtUtc?` |
| GET `/{id}` | `deactivated?{byName, atUtc}`, `hasRecords` y `retirement?` |
| POST `/{id}/publication-checks` | sobre un dado de baja, sin `NothingToPublish`; suma `nextRecordNumber`, armado como `displayNumber` (`REL-000013`) |
| POST `/{id}/publications` | sobre un dado de baja, reactiva: sella una versión nueva aunque el borrador no cambie |
| GET `/{id}/publications/{publicationId}` | sin cambios: reactivar se sigue igual |

- `openSends` cuenta las sesiones `Pending` vigentes y las que recibieron su respuesta a tiempo sin capturarla; `lastExpiresAtUtc` es el vencimiento más lejano.
- `recordCount` es `null` sin `formrecords.read`, y `hasRecords` es `LastRecordNumber > 0`.
- `retirement` viene solo con un dado de baja, sobre todas sus versiones con Flow: `Pending`, `Uncertain`, `Deprecated` o `NeedsAttention`, y prima `NeedsAttention`, después `Uncertain` y después `Pending`. Es `null` si nunca tuvo un Flow.
- `byName` sale del lector de miembros; sin dato, «Persona sin información».

**Puertos:** `FilterRetiredAsync` y `GetRetirementAsync`, con `StructuredRetirementState`, tal como en la sección «Contrato» de la spec. Se leen fuera de toda transacción. Sin WhatsApp, la lista vacía; sin Forms, ninguna versión retirada.

**Errores**, en `Domain/Forms/FormDefinitionErrors.cs`, con sus textos en `FormsTexts{,.en}.resx`:

| Código | Tipo | es | en |
| --- | --- | --- | --- |
| `Forms.Definition.AlreadyDeactivated` | Conflict | Este formulario ya está dado de baja. | This form is already deactivated. |
| `Forms.Definition.NotPublished` | Conflict | Este formulario no tiene una versión publicada. | This form has no published version. |
| `Forms.Definition.HasRecords` | Conflict | Tiene respuestas y no se puede borrar. | It has responses and can't be deleted. |
| `Forms.Definition.HasOpenSends` | Conflict | Tiene envíos abiertos y no se puede borrar. | It has open sends and can't be deleted. |

Un formulario borrado responde `Forms.Definition.NotFound`. Copiar usa `PrefixTaken` y `PrefixInvalid` de la E14.

**Permisos:** ninguno nuevo. `forms.manage` dice «Crear y editar formularios en la pestaña «Diseño», copiarlos y borrarlos.» («Create and edit forms in the Design tab, copy them and delete them.»), y `forms.publish`, «Publicar un formulario en WhatsApp, darlo de baja y reactivarlo.» («Publish a form on WhatsApp, deactivate it and reactivate it.»).

### K1 · Publicar el contrato

El Back publica con 501 lo de esta sección y genera `docs/contracts/openapi.json`; el Front corre `npm run contracts` y no escribe tipos a mano. Los ejemplos reales salen en B12.

**Texto para el chat del Contrato**

```text
Leé AGENTS.md, este plan y la spec docs/plans/formularios/17-ciclo-de-vida.md. Primero K0: confirmá contra el código cada nombre de la lista; lo que difiera anotalo en «Cambios al contrato» y avisá a la Guía antes de seguir. Después K1: publicá con 501 las rutas, los DTO, los errores con sus textos es/en y los textos de los permisos de «Contrato»; generá openapi.json y avisá para que el front corra npm run contracts. No programes la lógica ni toques el front. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros del back, sin Docker y sin filtrar por módulo; si tocaste rutas, persistencia, autenticación o aislamiento, también la integración y la seguridad del área; el contrato no se cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto, por la Guía.
```

## Back

Todas aplican Result y ProblemDetails, `OperationLog.RunAsync`, `[LoggerMessage]`, `TimeProvider`, resources es/en, una UoW por escritura, RLS y «Dar de baja», «Reactivar» y «Borrar», nunca «Eliminar». Ningún módulo llama al otro adentro de su transacción.

1. **B1 · Dominio, columnas y textos (R: datos, migración).** Suma a `D/Forms/FormDefinition.cs` (`ISoftDeletable`), a `FormDefinitionState.cs` (`Deactivate(usuario, ahora)`; `Activate` limpia la baja) y a `FormDefinitionErrors.cs`; suma a `D/WhatsApp/WhatsAppFlowBinding.cs` las columnas de la spec. Suma a `FormDefinitionConfiguration.cs` y `FormDefinitionStateConfiguration.cs` [E14], en `I/Modules/Forms/Persistence/Configurations/`, y a la de los bindings; la migración `<fecha>_FormLifecycle.cs`, aditiva, con la excepción de la 0060 de la spec anotada y el índice parcial `(TenantId, DeprecationNextCheckAtUtc)` sobre lo publicado sin deprecar; `Permissions{,.en}.resx`; `AuditSummary.FormDefinition.Deactivated` y `.Reactivated` en `Audit{,.en}.resx`. Copiá `D/Assistant/AssistantOrganizationSettings.cs` y `20261010011328_AssistantModule.cs`. Tests: las transiciones; el prefijo de un borrado sigue ocupado; la migración hacia atrás; `PermissionTextsTests` y `ResourceParityTests`.
2. **B2 · Un solo filtro de disponibilidad.** `A/Modules/Forms/Services/FormAvailabilityPolicy.cs` decide en memoria: versión activa y sin baja. El mismo filtro en SQL es privado del reader, en `I/Modules/Forms/Persistence/Readers/FormAvailabilityQueryFilters.cs`, como `FormRecordQueryFilters` [E15], porque Application no expone expresiones ([capas-y-flujo](../rules/capas-y-flujo.md)); lo usan `ListSendableAsync` y `FindSendableAsync` de `FormStructuredSource.cs`, por `IFormReader.cs`. Lo borrado lo saca el filtro `SoftDelete`. Copiá `FormCatalogFiltersPolicy.cs`. Tests: la política y el filtro dan lo mismo; uno dado de baja y uno borrado no se ofrecen ni se encuentran; la respuesta a un envío abierto de un dado de baja se guarda con su número; la de uno borrado queda `Rejected` con `Forms.Definition.NotFound`.
3. **B3 · Dar de baja.** `DeactivateAsync` en `IFormDefinitionService` y `FormDefinitionService.cs`, en una UoW: bloqueo de la fila de estado, publicación abierta (`InProgress`), versión activa (`NotPublished`), ya dada de baja (`AlreadyDeactivated`), `Deactivate` e `IAuditLog.Record`. No escribe la fila del borrador. Copiá `FormPublicationService.cs`. Tests: con una publicación abierta; dos a la vez, y la segunda da `AlreadyDeactivated`; quien edita el borrador guarda sin 409; la entrada de auditoría.
4. **B4 · Reactivar.** Suma a `FormPublicationService.cs` y a `FormVersionCreator.cs`. Sobre un dado de baja, `publication-checks` no da `NothingToPublish` y suma `nextRecordNumber`, y `publications` sella una versión nueva aunque el borrador no cambie. La UoW que activa la versión, en `CompleteAsync` con `Published` y en «Salió» de `ResolveAsync`, limpia la baja y registra la reactivación; `Failed`, «No salió» y «Descartar esta publicación» (225) la dejan dada de baja. Copiá esos mismos. Tests: sin cambios publica igual; sigue de baja hasta confirmar; «Salió» también la confirma; si falla o se descarta, sigue de baja; la respuesta siguiente es `LastRecordNumber + 1`.
5. **B5 · Borrar (R: datos).** `DeleteAsync` en `FormDefinitionService.cs`. Fuera de la UoW, `GetRetirementAsync` con todas sus versiones: con `OpenSends`, `HasOpenSends`. En la UoW: bloqueo de la fila de estado, `version` (`General.ConcurrencyConflict`), `HasRecords`, publicación abierta (`InProgress`) y `Remove`, que el interceptor vuelve lógico. Los lectores de usos (`usedByCount`, `usedIn` y `FieldInUse`, `ItemInUse` con `uses: forms`, `Forms.PersonData.InUse`, «Lo usan» del catálogo y del dato, y la advertencia al dar de baja un catálogo) unen la definición, así el filtro saca lo borrado; un dado de baja sigue contando. Copiá `FormPublicationService.cs`. Tests: con respuestas, con envíos y con publicación; un borrador y un publicado; un test por lector; un envío que se cruza con el borrado queda `Rejected`, a la vista.
6. **B6 · Copiar.** `A/Modules/Forms/Services/FormDefinitionCopyCreator.cs`; suma a `FormPrefixResolver.cs` y a `FormDefinitionService.cs` (`ProposeCopyAsync` y `CopyAsync`). La propuesta es «<nombre> (copia)» en la cultura del pedido, cortado con «…» para entrar en 80, y el primer prefijo libre desde el original (`VIS2`, `VIS3`…), contando los borrados. Copiar valida afuera; en la UoW crea la definición con el borrador idéntico (identidades, catálogos, presentación, filtros por destinatario, títulos, para quién es y botón), sin versiones, publicaciones ni respuestas, y su fila de estado en cero; el choque del índice es `PrefixTaken`. Copiá `FormVersionCreator.cs`. Tests: identidades, catálogos, presentación y filtros iguales; prefijo ocupado; idempotencia; de un dado de baja; sin versiones ni respuestas; dos formularios con los mismos `FieldId` no se mezclan en la planilla, los conteos, el tablero ni `ItemInUse`.
7. **B7 · Las versiones que ya no se usan.** `FilterRetiredAsync` en `FormStructuredSource.cs`, con su lectura en `IFormReader.cs`: las reemplazadas, la de una publicación `Failed` y todas las de un dado de baja o un borrado, nunca la activa y nada de un formulario con una publicación abierta. Copiá `FormStructuredSource.cs`. Tests: reemplazada, `Failed`, de baja y borrada; la activa no; una publicación abierta suspende la respuesta.
8. **B8 · El binding.** Suma a `D/WhatsApp/WhatsAppFlowBinding.cs` `MarkRetired` (`Pending`), `ReserveDeprecation` (`InFlight`, con el turno siguiente y su reserva), `CompleteDeprecation(turno, resultado)` y `ExpireDeprecationLease` (un `InFlight` vencido pasa a `Uncertain`). Cada rechazo suma `DeprecationFailureCount` y espera 5, 15 y 30 minutos; el cuarto deja `NeedsAttention`. `D/WhatsApp/WhatsAppErrors.cs` suma `WhatsApp.FlowBinding.StaleFence`. Copiá las transiciones de publicación del binding [E14]. Tests: un turno viejo no escribe; el `InFlight` vencido; los cuatro intentos y sus tres esperas.
9. **B9 · La deprecación, en fases (R: Meta).** `A/Modules/WhatsApp/Interfaces/Services/IWhatsAppFlowDeprecationService.cs` y `Services/WhatsAppFlowDeprecationService.cs`, cada fase con su UoW:
   1. `MarkRetiredAsync`: los bindings publicados, sin deprecar y con la mirada vencida se le preguntan a Forms fuera de la UoW; los retirados pasan a `Pending` y los demás se vuelven a mirar a los 15 minutos;
   2. `ReserveAsync`: bajo el bloqueo del binding, con sesiones vigentes o respuestas a tiempo sin capturar, la mirada siguiente es el último vencimiento; si no, `InFlight`;
   3. fuera de la UoW, el cliente llama a Meta con el token de un número de esa WABA. Lo incierto se consulta por id, nombre y WABA antes de repetir;
   4. `CompleteAsync` guarda el resultado con su turno.

   Sin un número de esa WABA con token, es un rechazo. Copiá `IWhatsAppFlowPublicationService.cs`. Tests: sesión vigente; respuesta a tiempo sin capturar; otra WABA; sin token; rotar el token no cambia la identidad; los Flows de una publicación descartada (225) se retiran.
10. **B10 · Meta (R: Meta).** Suma a `I/Modules/WhatsApp/WhatsAppFlowClient.cs` `DeprecateAsync` (`POST /{flow-id}/deprecate`) y `GetFlowStatusAsync` (`GET /{flow-id}?fields=id,name,status,whatsapp_business_account`). Un 2xx con `success` es definitivo; todo 4xx se consulta, y `DEPRECATED` es éxito y otro estado, un rechazo; 5xx, 429, timeout y cuerpo ilegible son inciertos. Copiá el mismo cliente. Tests: 2xx, 4xx, 5xx, 429, timeout, cuerpo ilegible y «ya deprecado»; un 4xx nunca se repite sin consultar (lección 1).
11. **B11 · El worker (R: aislamiento, trabajo de fondo por organización).** `I/Modules/WhatsApp/WhatsAppFlowDeprecationWorker.cs`, cada minuto: lista las organizaciones con un número, también desconectado (`ListOrganizationIdsWithNumbersAsync`, en `IWhatsAppChannelReader`), y en cada una corre las fases con `TenantJobRunner`, sin `ExecutionActor` y en su `try/catch`. Copiá `WhatsAppBroadcastWorker.cs` y `TenantJobRunner.cs`. Tests: una organización con falla no frena a las demás; con el reloj de prueba, al vencer el último envío se depreca; dos workers a la vez llaman una sola vez.
12. **B12 · Rutas, puertos y ejemplos.** Suma a `P/Modules/Forms/Controllers/BusinessFormsController.cs`, `Contracts/CopyFormHttpRequest.cs`, `FormsContractContributor.cs`, `I/StructuredData/DisabledStructuredFormSource.cs` y `DisabledStructuredFormChannel.cs` y `GetRetirementAsync` en `WhatsAppStructuredFormChannel.cs`. Copiá esos mismos [E14]. Tests: cada ruta con 401, 403, feature apagada = 404, 400 y 409; los dos permisos; `sort=status` con los dados de baja al final (264); aislamiento en `Modules/Forms/RouteIsolationMatrixTests.cs`; `ExplicitRouteInventoryTests` y `VersionedContractTests`; un formulario borrado no rompe ninguna lectura (lección 2); los ejemplos exportados de una corrida real, con cada estado de `retirement`.
13. **B13 · Acciones del asistente (R: permisos)** ([0068](../decisions/0068-acciones-del-asistente.md) y su [receta](../guides/agregar-una-accion-al-asistente.md)). Suma a `A/Modules/Forms/Services/FormDefinitionActionSource.cs`, registrada en `FormsModule.cs` (la crea si la E14 no la trae), `forms.deactivate@1` (`Dialog`, `FRM-Dar-De-Baja`, con `forms.publish`) y `forms.remove@1` (`RemoveConfirmation`, con `forms.manage`), con su efecto en WhatsApp declarado; sus textos `Action.*` en `FormsTexts{,.en}.resx` y su escenario en `TestFeatures/AssistantActions/`. Copiar no es una acción (nace en borrador, 0057), y reactivar sigue lo que la E14 declare para publicar. Copiá `RoleActionSource.cs`. Tests: los de arquitectura de las acciones y la batería genérica.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan, la spec docs/plans/formularios/17-ciclo-de-vida.md, las decisiones 264 y 270 de la hoja de ruta y las fichas de su «Qué leer». Después de K1, hacé B1 a B13 en orden, contra ese contrato, copiando los moldes de cada tarea; Forms y WhatsApp, solo por los puertos, y nada de Meta adentro de una UoW; las tareas (R) llevan la revisión adversarial antes del commit. No toques el front ni edites la tabla de estado. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros del back, sin Docker y sin filtrar por módulo; si tocaste rutas, persistencia, autenticación o aislamiento, también la integración y la seguridad del área; el contrato no se cambia sin anotarlo en «Cambios al contrato» y avisar a la Guía; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto, por la Guía.
```

## Front

Las tareas siguen los tableros v175, o los redibujados que dice la spec, y las fichas de UI, formatos, formularios, listados y accesibilidad, con i18n en es y en, los tipos generados y axe donde cambia una pantalla.

1. **F1 · Tipos y llamadas.** Suma a `src/modules/forms/api/forms.ts` [E14] las rutas nuevas y las que cambian. Copiá la API de la E16. Tests: zod estricto con los ejemplos de B12.
2. **F2 · La lista.** Suma a `FormsPage.tsx` [E14 y E15]: «Dados de baja» en «Estado», la pieza con su botón y sin cantidades (262), y al ordenar por el estado, los dados de baja al final (264); «De baja · versión 2» en «En WhatsApp» con su ⓘ, y el ⋮ por estado y permiso (Editar o Ver, Copiar, Dar de baja, Reactivar y Borrar). Lo que no se puede usar queda grisado con su motivo (43): Borrar con respuestas, «Tiene respuestas y no se puede borrar.» (132), o con envíos abiertos, «Tiene envíos abiertos y no se puede borrar.» (lo decidió la Guía, por la 43 y la 132); Dar de baja mientras se publica, «Se está publicando la versión 4.». «Reactivar» lleva al formulario y abre ahí su diálogo (131). Quien solo ve respuestas filtra Activos y Dados de baja. Copiá `FRM-Formularios-17` y `M-FRM-Formularios-17`. Tests: las acciones por estado y permiso; sus motivos, que en la hoja del teléfono se abren con un toque sobre la opción, sin texto debajo (211); adónde lleva «Reactivar»; quien solo ve respuestas; axe.
3. **F3 · Los diálogos.** `components/LifecycleDialogs.tsx`: «Dar de baja «…»», que lee `lifecycle` al abrir, en dos columnas, «Deja de pasar» y «Se conserva», cada una una lista de líneas, con los envíos abiertos en «Se conserva» y debajo lo que pasa en WhatsApp, con y sin envíos abiertos (213); «¿Borrar «…»?», en una frase, con la línea de WhatsApp si llegó a publicarse y la franja si cambió la situación. Reactivar es `PublishDialog.tsx` [E14] con «Próxima respuesta» y «Reactivar con la versión N» (131). Si no se sabe si salió, relee sin pisar nada (43); el error queda en el diálogo, y el foco, en «Cancelar» (161). Copiá `FRM-Dar-De-Baja` y `FRM-Reactivar-Eliminar`. Tests: los estados de los tableros; releer después de un corte; el error en el diálogo; el foco; axe.
4. **F4 · Copiar.** `components/CopyFormDialog.tsx`: lee `copy-proposal` al abrir; nombre y prefijo con sus ⓘ; `PrefixTaken` en su campo; «Copiando…»; al terminar abre el editor de la copia. Una clave de idempotencia por diálogo, que se conserva al reintentar, y la mutación no deja el nombre en la caché de mutaciones (lección 4). Copiá `FRM-Copiar`. Tests: sin nombre; prefijo ocupado; reintento con la misma clave; abre el editor; la caché de mutaciones, vacía.
5. **F5 · El formulario dado de baja.** Suma a `FormEditorPage.tsx` y `PublicationBand.tsx` [E14]: «De baja · versión 2» con su ⓘ, que dice la baja en una frase y lo que pasa en WhatsApp (175); «Sin confirmar en WhatsApp» y «No se pudo deprecar en WhatsApp» al lado; «Reactivar» en lugar de «Publicar», con el motivo cuando está apagado, y al reactivar, la etiqueta y las acciones de la publicación de la E14 (175 y 186), con «Descartar esta publicación», que confirma con su texto propio, «¿Descartar la publicación de la versión N?» y «El formulario sigue de baja y se retiran sus Flows de WhatsApp.» (225); el ⋮ con Copiar y Borrar. Con `Pending` y sin envíos abiertos, la ⓘ dice «No hay envíos abiertos: se deprecan ahora sus Flows.», el texto de `FRM-Dar-De-Baja`. Se edita y se guarda como siempre. Dar de baja, borrar, copiar y reactivar invalidan la lista, el formulario y los formularios de `SendFormDialog.tsx` (lección 3). Copiá `FRM-Editor-Baja` y `M-FRM-Editor-Baja`. Tests: los cuatro estados de WhatsApp; cada estado de la publicación al reactivar y el motivo de «Reactivar» apagado (175); descartar al reactivar, con su texto; el motivo de «Borrar» apagado, con un toque en el teléfono (211); editar y guardar; la invalidación.
6. **F6 · Las propuestas del asistente (B13).** `useAssistantProposal` en `LifecycleDialogs.tsx`, para dar de baja y borrar. Copiá el adaptador de Roles. Tests: todo lo propuesto se ve, y se manda lo que se ve.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, este plan y la spec ../ArquitecturaBaseMutitenant/docs/plans/formularios/17-ciclo-de-vida.md, con las decisiones 261 a 264, 269 y 275 de la hoja de ruta. Esperá K1, corré npm run contracts y hacé F1 a F6 contra los tipos generados, copiando los tableros v175 de cada tarea, o los redibujados que dice la spec, con i18n, shared/ui y TanStack Query, y «Dar de baja», «Reactivar» y «Borrar», nunca «Eliminar». No toques el lienzo, las specs ni el back, ni edites la tabla de estado. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src; TDD donde hay lógica; al terminar cada tarea, npx tsc -b y npm test enteros, sin filtrar, y al cerrar la parte, build, lint y contracts:check; si tocaste rutas, autenticación o aislamiento, también sus pruebas de integración y seguridad; el contrato no se cambia sin anotarlo en «Cambios al contrato» y avisar a la Guía; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto, por la Guía.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real de B12; los ejemplos validados con zod. Una diferencia vuelve a su dueño y se anota abajo.
2. **I2 · Puerta.** `/puerta` una vez, sobre copias del corte:
   - back: build sin advertencias y `dotnet test` completo; front: build, lint, test y `contracts:check`;
   - E2E real con el canal de prueba de la E14: dar de baja con un envío abierto y responderlo; reactivar, con versión nueva y el número siguiente; borrar un borrador y un publicado sin respuestas; copiar uno con un catálogo y un filtro, y publicar la copia;
   - `realinear-docs --detectar` y `estado-docs --detectar` en 0;
   - la galería (`scripts/capturas/etapa-17.json`) a 1440, 390 y 320, solo de `FRM-Formularios-17`, `M-FRM-Formularios-17`, `FRM-Editor-Baja`, `M-FRM-Editor-Baja`, `FRM-Dar-De-Baja`, `FRM-Reactivar-Eliminar` y `FRM-Copiar`;
   - `/recorrido`, con `dar-de-baja-y-reactivar` y copiar un formulario con un catálogo y un filtro, y la lista de control de `/puerta`;
   - al cerrar: `docs/features/formularios.md` suma la sección, `docs/features/whatsapp.md` la deprecación, y el `AGENTS.md` de `Infrastructure/Modules/WhatsApp` nombra el worker.

   El retiro del módulo no corresponde: se probó en la E14 y esta etapa no cambia la composición.
3. **I3 · Meta, pendiente para la I1 de la 10.** Con el número de una organización de prueba y el tiempo para contestar en 1 hora, en su Configuración de WhatsApp (270), los cuatro pasos de «Cómo se cierra» de la spec. El usuario hace lo que pide su cuenta, con los pasos clic por clic; nadie maneja su PC ni pega secretos.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |
| Meta (I3) | Pendiente para la I1 de la 10 |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan y /puerta. Integrá el front contra la API real, devolvé cada diferencia a su dueño y corré la puerta una sola vez sobre copias del corte; después de un arreglo, solo lo que toca. No toques el lienzo. La prueba con Meta queda para la I1 de la 10, con los pasos clic por clic para el usuario y sin manejar su PC. Dejá solo la tabla final corta en este plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo con git commit -- <rutas>, en español y con conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada arreglo, build y tests rápidos enteros de su repo, sin Docker y sin filtrar; si tocó rutas, persistencia, autenticación o aislamiento, también la integración y la seguridad del área; el contrato no se cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario se le pregunta solo lo de producto, por la Guía.
```

## Relación con otras etapas

**Usa de las anteriores:**
- **Base de la 12:** `ConfirmDialog`, `RowAction.disabledReason`, los botones que trabajan y `PageLoading`.
- **E11:** `WhatsAppChannels` (`WabaId`, `EncryptedToken`), `IWhatsAppChannelReader`, `TenantJobRunner`, `PeriodicWorker` y `WhatsAppBroadcastWorker`.
- **E12 a E16:** los nombres de K0.

**Deja para las siguientes:**
- `FormAvailabilityPolicy`, el único filtro de disponibilidad, con su par privado del reader, `FormAvailabilityQueryFilters`, y `FormDefinitionCopyCreator`.
- `FilterRetiredAsync`, `GetRetirementAsync` y `StructuredRetirementState`; `IWhatsAppFlowDeprecationService` y `WhatsAppFlowDeprecationWorker`.
- `FormDefinitionStates.DeactivatedAtUtc` y `DeactivatedByUserId`, `FormDefinitions.IsDeleted` y las columnas `Deprecation*` de `WhatsAppFlowBindings`.
- Las rutas `lifecycle`, `deactivation`, `copy-proposal` y `copies`, el `DELETE`, el estado `deactivated` y los campos `hasRecords`, `openSends`, `retirement`, `deactivated` y `nextRecordNumber`.
- Los errores `AlreadyDeactivated`, `NotPublished`, `HasRecords` y `HasOpenSends`.
- `LifecycleDialogs.tsx`, con el diálogo de dar de baja en dos columnas, «Deja de pasar» y «Se conserva», cada una una lista de líneas a la que las etapas siguientes suman las suyas. Y `CopyFormDialog.tsx`.

## Cambios al contrato


## Para decidir

Nada. «Borrar» en un formulario publicado con envíos abiertos lo decidió la Guía (2026-11-08): queda grisado en el ⋮, con su motivo, como con respuestas (43 y 132). Por eso cada fila publicada trae `openSends`.
