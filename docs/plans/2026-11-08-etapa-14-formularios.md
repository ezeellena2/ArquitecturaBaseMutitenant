# E14 · Formularios: crear, publicar, mandar y recibir · plan ejecutable

**Estado:** pendiente · Spec: [`formularios/14-formularios.md`](formularios/14-formularios.md), con sus partes de [decisiones y flujos](formularios/14-parte-decisiones-y-flujos.md), [datos y contrato](formularios/14-parte-datos-y-contrato.md) y [archivos, tests y Meta](formularios/14-parte-archivos-tests-y-meta.md). Tableros aprobados: lienzo v169. No hay decisión de producto pendiente.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B12 | Pendiente |
| Front | F1–F9 | Pendiente |
| Integración y puerta | I1–I4 | Pendiente |

## Forma de trabajo

Contrato primero; después Back y Front en paralelo, un chat por repo; una sola puerta al final. El Back va en orden: cada tarea usa la anterior. Las reglas de cada chat van en su texto, y la tabla de estado la actualiza la Guía.

**Lo que pide Meta queda para la I1 de la 10** (123): la prueba de la §15 con el número de una organización. La etapa cierra con el Meta simulado (B12).

## Qué se verificó al arrancar

| Fila de la spec (§12) | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [E11] Números | `WhatsAppChannel`, global, con `WabaId` y token por número; `IWhatsAppChannelReader.ListTemplateAccountsAsync(false)` da una fila por WABA | publicar lo reusa |
| [E11] Chats | es `WhatsAppChat`, sin «conversación»; `WhatsAppChatMessage.Kind` no tiene formulario | la sesión guarda el id del chat; `Kind` suma `Form` |
| [E11] Tomar y sin pago | `WhatsAppConversationService.SendAsync` bloquea, toma y pasa por `WhatsAppReplyIssuer` (`ReadOnly`, `WindowClosed`, `PaymentRequired`); no hay `WhatsApp.Chat.*` | mandar copia `SendAsync`, con `ReadOnly` |
| [E11] Entrada | `SetFromAccess`; `ProcessAsync` corre consumidores y bot en una UoW; `Fail` espera 6·n s, hasta 8, con reserva de 120 s; el `nfm_reply` se descarta | el `nfm_reply` va a sus fases (B9) |
| [E11] Outbox y vista previa | sin Flow; reintento solo; `LastMessagePreview` es un texto | la variante Flow; `lastForm` |
| [E11] Rutas y front | `WhatsAppConversationsControllerBase` la hereda también Plataforma; `ChatComposer`, `MessageThread`, `ConversationItem` y el ⋮ por `Page moreActions` | nace `OrganizationWhatsAppConversationsControllerBase`; `FormCard` |
| [Meta] | la documentación vigente (2026-11-08) confirma los límites de la §12, Flow JSON 7.3, `flow_name` en lugar de `flow_id` y `data` no vacío | mandar con un número, en la I1 |
| [E9], #51 y CSP | cuotas cerradas; sin caché nueva; `Enter` deja `TenantKind` en null; `frame-src` trae `https://*.facebook.com` | worker con `ExecutionActor`, captura con `SetFromAccess`; test de `business.facebook.com` |
| Guardas | un `I*Service` por servicio; `[Idempotent]` en cada `POST` con 201 o 202; ningún lector del idioma de una cuenta | `IWhatsAppFormSendService` e `IAccountCultureReader` |
| Front | solo `Page` arma pestañas, sin barra con una sola | pestañas por `Page tabs` |
| Base, E12 y E13 | todavía no existen | K0 |

## Contrato

### K0 · Lo que dejaron la base, la E12 y la E13

El chat del contrato confirma cada nombre en el código. Lo que difiera va a «Cambios al contrato» antes de programar.

- **Base de la 12:** `INotificationSource`, `NotificationContext`, `NotificationItem` y el sufijo `NotificationSource`; en el front, `notifications` (`{kind, describe}`) en `registration.ts`, `InfoTip`, `PageLoading`, `FormSkeleton`, `ConfirmDialog` con `pending`, los botones, `FilterBar` con un solo «Filtros» y la escala.
- **E12:** `AddFormsModule` y `FormsApiModule`; la feature `forms`; `Permissions.Forms`; `FormsTexts{,.en}.resx`, `FormsErrorTextSource`, `FormsContractContributor` y `FormsModelContributor`; `UniqueKeys`; `FormCatalog`, `FormCatalogItem`, `FormsTextLimits`, `FormOptionTitle` y `FormCatalogErrors` (`ItemInUse` con `uses`); `IFormCatalogRepository` con `LockAsync`, `IFormCatalogReader` y `FormCatalogService`; `MembersCatalogSource` e `IMemberDirectoryReader`; `IStructuredContactSource` y su apagado; `BusinessFormCatalogsController`; la guarda de ejemplos, `PersistenceNamingTests` y las partes de Forms de las guardas; `test-e2e-real-forms.mjs` y `recorrido-usuario-forms.mjs`; en el front, `src/modules/forms/` con `registration.ts`, `api/catalogs.ts`, `errors.ts`, locales, `CatalogsPage`, `CatalogEditorPage` y `CatalogPreviewPhone`.
- **E13:** `FormCatalogField`, `FormCatalogPresentation` (`{title[], description[]}`), `FormCatalogPresentationBuilder` y `FormCatalogFieldEditor`; en el front, `CatalogCombo.tsx`, `CatalogFieldsDialog.tsx` y `PresentationEditor.tsx`.

### K1 · Publicar el contrato

Las rutas responden 501 hasta su tarea. K1 publica controllers, contratos, permisos, códigos y textos, y genera `openapi.json`; el Front corre `npm run contracts`. Cada tarea del Back exporta los ejemplos reales de sus rutas.

**Forms:** `[Access(Business)]`, `[FeatureGate("forms")]`, `/api/organization/forms`. Los errores van sin `Forms.`.

| Ruta | Permiso | Idempotencia y versión | Errores |
| --- | --- | --- | --- |
| GET `/` y `/filter-counts` | `forms.read` | — | — |
| POST `/` | `forms.manage` | `[Idempotent]`, 201 | `Definition.PrefixTaken`, `PrefixInvalid`; `Draft.*` |
| GET `/{id}` | `forms.read` | — | `Definition.NotFound` |
| PUT `/{id}/draft` | `forms.manage` | `version`, 409; `[ResponseVersion("Version")]` | `Definition.NotFound`, `PrefixTaken`, `PrefixLocked`, `PrefixInvalid`; `Draft.QuestionInvalid`, `TooManyQuestions`, `TooManyOptions`, `CatalogUnavailable` |
| POST `/{id}/publication-checks` | `forms.publish` | 200 | `Definition.NotFound` |
| POST `/{id}/publications` | `forms.publish` | `[Idempotent]`, 202 | `Publication.DraftChanged`, `InProgress`, `NothingToPublish`, `Incomplete`, `ChannelIssues`, `ChannelUnavailable`; `Draft.QuestionInvalid`, `CatalogUnavailable`, `CatalogTooLarge`, `CatalogPresentationInvalid` |
| GET `/{id}/publications/{publicationId}` | `forms.read` | — | `Publication.NotFound` |
| POST `…/{publicationId}/check` | `forms.publish` | 200 | `Publication.NotFound` |
| POST `…/{publicationId}/resolution` | `forms.publish` | 200; lo protege el estado | `Publication.NotFound`, `NotResolvable` |
| GET `/{id}/versions/{versionId}/preview` | `forms.read` | `no-store` | `Preview.NotAvailable`; `WhatsApp.Flow.PreviewUnavailable` (503) |
| GET `/catalogs/{id}/used-by` | `forms.read` | paginado | `Catalog.NotFound` |

**WhatsApp:** `GET` y `POST /api/organization/whatsapp/{customers|internal}/conversations/{id}/forms`, en `OrganizationWhatsAppConversationsControllerBase`, nueva, que heredan las dos bandejas y no Plataforma, con `whatsapp.customers` o `whatsapp.internal`. El `POST` lleva `[Idempotent]` y responde 202. Errores: `WhatsApp.Conversation.NotFound`, `TakenByOther`, `WindowClosed`, `PaymentRequired` y `ReadOnly`; `WhatsApp.Chat.NotEmployee`, `FormUnavailable` y `SendPending`; `WhatsApp.Flow.NotPublishedForNumber`, y `Forms.Offer.*`.

Ninguna ruta declara cuota propia. Cada lista anidada declara su tope y su fila en `GrowingCollectionContractTests`.

**Cuerpos y tipos:** los de la §5 y la §7 de [datos y contrato](formularios/14-parte-datos-y-contrato.md), con estos cambios:
1. **POST `/` recibe el primer borrador,** `{name, prefix?, audience, submitLabel, questions[], titles[]}`, como «Guardar borrador» de `FRM-Editor`. Sin `prefix`, el servidor propone uno libre.
2. **Preguntas y títulos:** una pregunta nueva viaja sin `fieldId`, y una opción, sin `choiceId`; la respuesta los devuelve en el mismo orden. Un título es `{titleId?, text, start}`, con `start` igual a cuántas preguntas van antes. Dos con el mismo `start`, o uno al final, no tienen preguntas debajo (T24). El puerto cambia `BeforeFieldId` por `Start`. `presentation` es `null` (la del catálogo) o la forma de la E13.
3. **El mensaje del chat** suma `form?: {sessionId?, formName?, versionNumber?, state, errorCode?, errorFieldLabel?, displayNumber?, values?, canReadValues}`. Al salir, `state` es `Waiting`, `Answered` o `Unanswered`; al llegar, `Saving`, `Saved`, `Rejected`, `Late` o `Unrecognized`.
4. **La fila del chat** suma `lastForm?: {formName?, state}` (`Sent`, `SendFailed`, `Saving`, `Saved`, `NotSaved` o `Unrecognized`), con `lastMessagePreview` en `null`. La de «Mensajes» suma `lastForm?: {formName}` («Vos: Respondiste …»).
5. **La campana:** `formPublicationFailed`, con `{formName, versionNumber}`, `link` `/org/formularios/{id}` e `id` `formPublicationFailed:{publicationId}`, a quien tiene `forms.publish`.
6. **Los catálogos de la E12** suman `usedByCount`, `inUse` y `usedIn`; su `PUT` suma `Forms.Catalog.FieldInUse` (409, con `uses` en `presentation` y `forms[]`, nulo sin `forms.read`), e `ItemInUse`, `uses: records`, que se lee «No se puede borrar: la eligió una respuesta. Podés darla de baja.» (*A response chose it; you can deactivate it.*).

**Permisos:** los cinco de la spec, en `Permissions.Forms`, con los textos de la §2 de la hoja de ruta recortados a lo que existe (78): `forms.manage`, «Crear y editar formularios en la pestaña «Diseño».» (*Create and edit forms in the Design tab.*); `forms.publish`, «Publicar un formulario en WhatsApp.» (*Publish a form on WhatsApp.*); `formrecords.create`, «Completar formularios por WhatsApp.» (*Fill in forms on WhatsApp.*). El Dueño los recibe en el seed y en `<fecha>_SystemRoleFormsPermissions.cs`.

**Errores**, con los tipos de la §4 de la spec, en es y *en*:
- **`Forms.Definition`:** `NotFound`, «No encontramos el formulario.» (*We couldn't find the form.*); `PrefixTaken`, «Otro formulario ya usa ese prefijo.» (*Another form already uses this prefix.*); `PrefixLocked`, «El prefijo queda fijo al publicar.» (*The prefix is fixed once published.*); `PrefixInvalid`, «De 1 a 12 mayúsculas o números, primero una letra.» (*1 to 12 capital letters or digits, a letter first.*).
- **`Forms.Draft`:** `QuestionInvalid`, «Revisá esta pregunta.» (*Check this question.*); `TooManyQuestions`, «Hasta 40 preguntas.» (*Up to 40 questions.*); `TooManyOptions`, «Hasta 50 opciones; 20 si se eligen varias.» (*Up to 50 options; 20 for several.*); `CatalogUnavailable`, «Este catálogo no se puede usar.» (*This catalog can't be used.*); `CatalogTooLarge`, «Pasa el tope de WhatsApp.» (*It exceeds WhatsApp's limit.*); `CatalogPresentationInvalid`, «Una opción queda vacía o no entra.» (*An option is empty or too long.*).
- **`Forms.Publication`:** `Incomplete`, «Falta completar esto.» (*This is missing.*); `ChannelIssues`, «WhatsApp no puede mostrar parte.» (*WhatsApp can't show part of it.*); `InProgress`, «Ya hay una publicación en curso.» (*A publication is in progress.*); `NothingToPublish`, «No hay nada nuevo.» (*There's nothing new.*); `DraftChanged`, «El borrador cambió; revisalo.» (*The draft changed; check it.*); `ChannelUnavailable`, «No hay un número conectado.» (*No number is connected.*); `NotFound`, «No encontramos la publicación.» (*We couldn't find the publication.*); `NotResolvable`, «Ya no necesita revisión.» (*It no longer needs review.*). `Forms.Preview.NotAvailable`, «Esta versión no tiene vista oficial.» (*This version has no official preview.*).
- **`Forms.Record`:** `InvalidResponse`, «La respuesta no coincide.» (*The response doesn't match.*); `ReplayConflict`, «La respuesta llegó otra vez, distinta.» (*The response arrived again, different.*); `NotAllowed`, «Ya no puede responder formularios.» (*They can no longer answer forms.*); `OptionWithdrawn`, «Eligió una opción que se dio de baja.» (*They chose a deactivated option.*).
- **`Forms.Offer`:** `TooManyOptions`, «Pasa el tope de WhatsApp.» (*It exceeds WhatsApp's limit.*); `TooFewOptions`, «No llega al mínimo.» (*It's below the minimum.*); `NoOptions`, «No hay opciones.» (*There are no options.*); `CatalogUnavailable`, «El catálogo ya no se puede usar.» (*The catalog can no longer be used.*); `PresentationInvalid`, «Una opción queda vacía o no entra.» (*An option is empty or too long.*). `Forms.Catalog.FieldInUse`, «Un formulario usa este campo.» (*A form uses this field.*).
- **`WhatsApp.Flow`:** `OptionNotOffered`, «La opción no estaba en la oferta.» (*The option wasn't offered.*); `TextTooLong`, «Hasta {0} caracteres; tiene {1}.» (*Up to {0} characters; it has {1}.*); `TooManyComponents`, «Hasta 50 elementos.» (*Up to 50 items.*); `ResponseTooLarge`, «La respuesta puede pasar el tope.» (*The response may exceed the limit.*); `UnsupportedConfiguration`, «No se puede mostrar así.» (*It can't be shown this way.*); `ProviderRejected`, «WhatsApp rechazó la publicación.» (*WhatsApp rejected the publication.*); `ValidationFailed`, «WhatsApp no aceptó el formulario.» (*WhatsApp didn't accept the form.*); `AccountUnavailable`, «La cuenta de WhatsApp no está disponible.» (*The WhatsApp account isn't available.*); `PreviewUnavailable`, «WhatsApp no respondió. Probá de nuevo.» (*WhatsApp didn't respond. Try again.*); `NotPublishedForNumber`, «No está publicado para este número.» (*It isn't published for this number.*); `SessionExpired`, «La respuesta llegó vencida.» (*The response arrived too late.*).
- **`WhatsApp.Chat`:** `NotEmployee`, «Ya no es miembro.» (*They're no longer a member.*); `FormUnavailable`, «Ya no se puede mandar.» (*It can no longer be sent.*); `SendPending`, «Hay un envío en curso.» (*A message is being sent.*).

**Texto para el chat del Contrato**

```text
Leé AGENTS.md, este plan y la parte de datos y contrato de la spec de la E14. Hacé K0 y después K1, como dice el plan, y avisá a la Guía para que el Front corra npm run contracts. No programes la lógica ni toques el front. Todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo (git commit -- <rutas>), en español y con conventional commits. Con la app levantada, nadie edita src del front. TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtro, más integración y seguridad del área si tocás rutas, persistencia, autenticación o aislamiento. El contrato cambia solo anotado en «Cambios al contrato». Ninguna clave real pasa por vos; al usuario, solo lo de producto.
```

## Back

Cada tarea copia sus moldes, con una UoW por método que escribe, RLS, auditoría y nada de Meta adentro de una UoW. Los tests salen de la §10; abajo, lo que no puede faltar.

1. **B1 · Dominio y tablas de Forms.** `D/Forms/`: `FormDefinition`, `FormDefinitionState`, `FormVersion`, `FormPublication`, `FormRecord`, `FormRecordValue`, `FormQuestionType`, `FormDraft` y sus `*Errors.cs`; `FormPublicationWork` en `platform`; sus configuraciones, como `FormRecordValueConfiguration`, y `<fecha>_FormDefinitions.cs`. Copia `AssistantOrganizationSettings.cs`, `AssistantTurnWork.cs` y `20261010011328_AssistantModule.cs`. Tests: los de dominio de la §10; RLS y migración, también hacia atrás.
2. **B2 · Puertos neutrales.** Los cuatro de la §7, sus modelos y `I/StructuredData/Disabled*.cs` con `TryAdd`. Copia `IStructuredContactSource` [E12]. Tests: los apagados y `ModuleIsolationTests`.
3. **B3 · Crear y guardar el borrador.** `IFormDefinitionService`, `FormDefinitionService`, `FormPrefixResolver`, `FormDraftEditor`, los validadores, `IFormDefinitionRepository`, `IFormReader` y `FormReader`, y las rutas de la lista, el alta, el detalle y el borrador. Copia `AssistantConversationService.cs` y `RoleReader.cs`. Tests: «elegir varias» y títulos; guardar sin cambios no escribe, como la E12: no cambian `ModifiedAtUtc`, la versión ni la auditoría; ningún 409 falso por una respuesta o una publicación; 401, 403, feature apagada, idempotencia y paginado.
4. **B4 · La pregunta de catálogo y «Lo usan».** `catalogId` y `presentation` en el borrador (§6b), `FormOfferBuilder` (una fila más que el tope y `FormOptionTitle`) y lo que suma a `FormCatalogService` [E12] y `FormCatalogFieldEditor` [E13], leído con `IFormReader` sobre el índice de `FormRecordValues`. Tests: los cinco problemas de la oferta y los nombres largos; `FieldInUse`, con y sin `forms.read`; `uses: records`; `used-by` en su segunda página; 10.000 opciones y 20 campos con EXPLAIN.
5. **B5 · Comprobar, publicar y el worker.** `IFormPublicationService` y `FormPublicationService` (`CheckAsync`, `ClaimAsync`, `CompleteAsync` y `ResolveAsync`, §6, punto 4), `FormVersionCreator`, el `ContentHash` canónico, `I/Modules/Forms/FormPublicationWorker.cs` con `ExecutionActor`, `FormPublicationNotificationSource` y sus rutas. Copia `AssistantTurnService.cs`, `AssistantTurnWorker.cs` y `AssistantTurnRepository.ListReadyAsync`. Tests: con un canal de prueba, `Published`, `Uncertain`, `Failed` y `RequiresReview`; «Salió», «No salió» y `NotResolvable`; `DraftChanged`; la misma clave; la 103; sin `forms.publish`, `Failed`; la campana.
6. **B6 · El Flow en Meta.** `WhatsAppFlowBuilder` (el perfil `flow-static-7.3-r9` de la §14, con `Check` y el nombre del Flow), `D/WhatsApp/WhatsAppFlowBinding.cs`, `IWhatsAppFlowPublicationService` y su servicio, `I/Modules/WhatsApp/WhatsAppFlowClient.cs` (141006 es `AccountUnavailable`), `WhatsAppStructuredFormChannel` y `<fecha>_WhatsAppFlowBindings.cs`. Copia `WhatsAppTemplateClient.cs`, `WhatsAppTemplateIssuer.cs` y `20261014031304_WhatsAppInboxes.cs`. Tests: cada componente y sus límites en UTF-16; sin `data` vacío; 2xx, 4xx, 5xx, 429, tiempo y Create sin id; `InFlight` antes de cada POST; ante una duda, verificar.
7. **B7 · La vista oficial.** `GetPreviewAsync` en Forms (las primeras 20 activas con los filtros fijos; ninguna sin `formcatalogs.read`) y `WhatsAppFlowPreviewLinkBuilder` (§6, punto 5), con el tope de 30 s. Tests: los de la §10, y `WhatsAppContentSecurityPolicyTests` con `business.facebook.com`.
8. **B8 · Mandar desde el chat.** `IWhatsAppFormSendService` y `WhatsAppFormSendService`, `D/WhatsApp/WhatsAppFlowSession.cs` (con `SentAtUtc`, por `TimeProvider`, y `WhatsApp:FlowSessionHours`) y `<fecha>_WhatsAppFlowSessions.cs`, `FormStructuredSource`, la variante Flow de `WhatsAppCloudClient.BuildPayload`, `IAccountCultureReader` en `Infrastructure/Identity` (en `IdentityAccessTests` e `IdentityStorageAccessTests`) y las dos rutas en `OrganizationWhatsAppConversationsControllerBase`. Copia `WhatsAppConversationService.SendAsync`, `WhatsAppReplyIssuer` y `ActiveMemberPhoneReader`. Tests: botón y motivos por bandeja, y sin ruta en Plataforma; `TakenByOther`, `PaymentRequired`, `ReadOnly`, `SendPending` y `NotPublishedForNumber`; la misma clave; el mensaje por defecto en el idioma de quien recibe.
9. **B9 · Recibir y capturar.** El webhook lee `interactive.nfm_reply` y guarda `ProtectedFlowResponse` (24 KiB); el procesador lo pasa a `IWhatsAppFlowReplyService.ClaimAsync`, a la captura y a `CompleteAsync`, sin `ProcessAsync` ni bot; `WhatsAppFlowReplyVerifier`; `FormRecordSink` → `IFormRecordService.CaptureAsync`, con `FormRecordNumberIssuer` y `FormRecordWriter`, que sella también el texto de la opción fuente de un campo «De un catálogo», leído por lote; `FailAsync` espera 30, 90, 210, 450 y 930 s en un Flow; `<fecha>_WhatsAppFlowReplies.cs`. Copia `WhatsAppInboundProcessor.cs`, `WhatsAppInboundService.cs` y `WhatsAppWebhookReader.cs`. Tests: los de captura de la §10, con dos capturas a la vez sin saltos y el texto sellado.
10. **B10 · La tarjeta, la lista y «Mensajes».** `FormRecordSource`, el `form` del mensaje, `lastForm` (el chat guarda su último mensaje de formulario, y el estado se deriva al leer) y `FormCard` en `WhatsAppLinkedMessage`, con `<fecha>_WhatsAppFormCards.cs`. Copia `WhatsAppConversationReader.cs` y `WhatsAppConversationLinkWriter.cs`. Tests: con y sin `formrecords.read`; la fila sin valores; la tarjeta en «Mensajes».
11. **B11 · Limpieza, baja y retiro.** `WhatsAppFlowSessionCleanupWorker` (copia `WhatsAppBroadcastWorker.cs`), la retención de `ProtectedFlowResponse`, `FormsAccountDeletionParticipant` con su repositorio de limpieza (copia los del Asistente y extiende `AssistantMaintenanceLayerTests`), `docs/guides/quitar-formularios.md` (copia `quitar-asistente.md`) y las guardas del módulo. Tests: la oferta vencida se borra; la baja deja `RespondentUserId` en null.
12. **B12 · Meta simulado.** En `TestFeatures/WhatsAppBrowser/`: un `WhatsAppFlowClient` de prueba que publica al primer intento; en lugar de `ForbiddenWhatsAppSender`, un emisor que no sale a Meta y guarda el `flow_token`; y la respuesta de un formulario por `WhatsAppFileBridge`. Copia `WhatsAppBrowserControl.cs` y `RecorridoWhatsAppModule.cs`.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan, la spec de la E14 con sus tres partes y las fichas de su «Qué leer». Esperá K1 y hacé B1 a B12 en orden, con sus moldes. Forms y WhatsApp se hablan solo por los puertos de la §7. No toques el front. Todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo (git commit -- <rutas>), en español y con conventional commits. Con la app levantada, nadie edita src del front. TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtro, más integración y seguridad del área si tocás rutas, persistencia, autenticación o aislamiento. El contrato cambia solo anotado en «Cambios al contrato». Ninguna clave real pasa por vos; al usuario, solo lo de producto. No edites la tabla de estado.
```

## Front

Cada pantalla copia su tablero v169 con todos sus estados (§9 de la spec), con i18n, tipos generados, `shared/format`, TanStack Query y axe.

1. **F1 · Tipos, API y textos.** `npm run contracts`; `src/modules/forms/api/forms.ts`, `errors.ts` y los locales `forms` y `enums`; en WhatsApp, `chats/api/chats.ts` y `personal/api/messages.ts`. Copia `api/catalogs.ts` [E12]. Tests: zod estricto con los ejemplos y paridad es/en.
2. **F2 · Navegación, lista y pestañas.** `registration.ts` suma `/org/formularios`, `/org/formularios/nuevo` y `/org/formularios/:id` con `forms.read`, «Formularios» en el panel y los cinco permisos, que `Rol` muestra; `pages/FormsPage.tsx`; `pages/FormPage.tsx`, con las pestañas por `Page tabs` desde `formTabs.ts`, al que cada etapa suma la suya (con una sola, sin barra, 193). Tableros: `FRM-Formularios` y su teléfono. Tests: permisos, filtro sin cantidades, vacío y 320.
3. **F3 · El editor.** `pages/FormEditorPage.tsx`, `components/QuestionEditor.tsx`, los títulos (88), «Para quién es», el prefijo, `FormPreviewPhone.tsx` al costado y «otra persona guardó» lado a lado (138). Un corte al guardar relee y compara solo el contenido: esa excepción a concurrencia va en `docs/rules/formularios.md`, y las preguntas que se abren en el lugar, en `paginado-y-listados.md`. Tableros: `FRM-Editor` y su teléfono. Copia `CatalogEditorPage.tsx` [E12] y `useEditForm`. Tests: cada estado, los botones (43 y 115) y el corte.
4. **F4 · La pregunta de catálogo.** `components/CatalogQuestionFields.tsx`, con `PresentationEditor.tsx` [E13], «Permitir elegir varias» y «En WhatsApp» plegada (145). Suma a `CatalogCombo.tsx` [E13] el origen, el estado (`status=all`) y la consulta como parámetros, con lo que no se puede usar apagado y su `unusableReason`. Tests: los tres orígenes y lo apagado; se abre sola con un error; apagada sin `formcatalogs.read`.
5. **F5 · Publicar y cómo termina.** `components/PublishDialog.tsx` (las zonas de la 112, con «Ir a la pregunta» e «Ir al título») y `components/PublicationBand.tsx` (la etiqueta de la 175, «Consultar publicación», «Repetir el mismo intento» con la misma clave, «Salió» y «No salió» con su confirmación, y el motivo de lo apagado; en el teléfono, el pie y la franja de la 122). Tableros: `FRM-Publicar`, `FRM-Publicando` y `M-FRM-Editor`. Tests: cada estado; un corte relee y repite con la misma clave.
6. **F6 · La vista oficial.** `components/OfficialPreviewDialog.tsx`: el marco de 480 × 840 achicado; cargando, lista, vencida y con error; «Abrir en otra pestaña»; cerrar cancela el pedido. Tableros: `FRM-Vista-Oficial` y su teléfono. Tests: la medida a 1440, 390 y 320.
7. **F7 · «Lo usan».** La columna del listado, la banda y la sección del catálogo con `used-by`, «Quitar «…»» con «Ir a la pregunta», la opción que eligió una respuesta y la advertencia de la baja, sobre `CatalogsPage`, `CatalogEditorPage` y `CatalogFieldsDialog`. Tableros: `FRM-Catalogos-14`, `FRM-Catalogo-14` y sus teléfonos. Tests: con y sin `forms.read`.
8. **F8 · Enviar formulario y la tarjeta.** En `src/modules/whatsapp/chats/`: el botón, solo en las bandejas de la organización, en `ChatComposer.tsx` y en el ⋮ del teléfono, `components/SendFormDialog.tsx` (con el mensaje que propone sin pisar lo escrito), `components/FormMessageCard.tsx` en `MessageThread.tsx`, con «Mandar de nuevo», y `lastForm` en `ConversationItem.tsx`. Copia `TemplateDialog.tsx`. Tableros: `WA-Chats-Formulario`, `WA-Chats-Formulario-Cliente` y `WA-Chats-Respuesta`, con sus teléfonos. Tests: cada estado; no aparece en Plataforma; un corte al mandar repite con la misma clave.
9. **F9 · «Mensajes», campana y galería.** La tarjeta en `personal/pages/MessagesPage.tsx` y «Vos: Respondiste …»; el texto de `formPublicationFailed`, con `notifications` en `registration.ts`; `scripts/capturas/etapa-14.json` con lo nuevo o cambiado. Tableros: `WA-Mensajes-14`, `M-WA-Mensaje-14`, `Campana-14` y su teléfono. Tests: la tarjeta en «Mensajes», el aviso con su enlace y axe.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, este plan, la §9 de la spec de la E14 y las fichas de UI del front. Esperá K1, corré npm run contracts y hacé F1 a F9 contra los tipos generados y los tableros v169, con todos sus estados. No toques el lienzo, las specs ni el back. Todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo (git commit -- <rutas>), en español y con conventional commits. Con la app levantada, nadie edita src del front. TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros (npx tsc -b y npm test), sin Docker ni filtro, más integración y seguridad del área si tocás rutas, persistencia, autenticación o aislamiento. El contrato cambia solo anotado en «Cambios al contrato». Ninguna clave real pasa por vos; al usuario, solo lo de producto. No edites la tabla de estado.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real, con los tipos regenerados y los ejemplos con zod estricto; cada diferencia vuelve a su dueño.
2. **I2 · Puerta.** `/puerta`, una vez sobre copias del corte: las suites de los dos repos, con Docker, y `contracts:check`; el E2E real (`test-e2e-real-forms.mjs`) con el Meta de B12 y los casos de la fila «E2E real y recorrido» de la §10; `realinear-docs` y `estado-docs` en 0; la galería de lo nuevo o cambiado, a 1440, 390 y 320; `/recorrido` (`recorrido-usuario-forms.mjs`) con `publicar-un-formulario`, `mandar-un-formulario` y `usar-un-catalogo`; quitar Forms una vez con la base vacía, con `quitar-formularios.md`; y la lista de control de `/puerta`, con los criterios de la §16.
3. **I3 · Al cerrar.** `docs/features/formularios.md` suma los formularios, y `docs/features/whatsapp.md`, los Flows; cada carpeta nueva, su `AGENTS.md`, su `CLAUDE.md` y su fila en `arbol.md`.
4. **I4 · Pendiente para la I1 de la 10:** la prueba con Meta de la §15, con el número de una organización, en Android y iPhone.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido / Retiro | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan y /puerta. Integrá contra el contrato, devolvé cada diferencia a su dueño y corré la puerta una vez sobre copias del corte. No manejes la PC del usuario; la prueba con Meta queda para la I1 de la 10. Dejá solo la tabla corta del resultado. Todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo (git commit -- <rutas>), en español y con conventional commits. Con la app levantada, nadie edita src del front. TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtro, más integración y seguridad del área si tocás rutas, persistencia, autenticación o aislamiento. El contrato cambia solo anotado en «Cambios al contrato». Ninguna clave real pasa por vos; al usuario, solo lo de producto.
```

## Relación con otras etapas

**Usa de las anteriores:**
- **Base:** la campana (`INotificationSource` y `notifications`) y las piezas del front que nombra K0.
- **E11:** `WhatsAppChannel`, `WhatsAppChat`, `WhatsAppChatMessage`, `SendAsync` y `WhatsAppReplyIssuer`, la cola de entrada, `IOutbox`, `WhatsAppLinkedMessage` e `IActiveMemberPhoneReader`; en el front, `ChatComposer`, `MessageThread`, `ConversationItem` y `MessagesPage`.
- **E12:** el módulo, los catálogos, `FormOptionTitle`, `IMemberDirectoryReader`, `IStructuredContactSource`, `PersistenceNamingTests`, los scripts de formularios y el front de catálogos.
- **E13:** la presentación, los campos, `CatalogCombo` y `PresentationEditor`.

**Deja para las siguientes:**
- **Dominio y tablas:** `FormDefinitions`, `FormDefinitionStates`, `FormVersions` (`Questions`, `Titles`), `FormPublications`, `FormRecords` (`SourceKey` `whatsapp:<sessionId>`), `FormRecordValues` (`ChoiceFields`, con el texto de la opción fuente), `platform.FormPublicationWorks`, `WhatsAppFlowBindings`, `WhatsAppFlowSessions` (`SentAtUtc`), `ProtectedFlowResponse` y `FormCard`.
- **Back:** `FormDefinitionService`, `FormPublicationService` (`CompleteAsync` y `ResolveAsync` activan la versión en una UoW), `FormRecordService`, `FormPrefixResolver`, `FormDraftEditor`, `FormVersionCreator`, `FormRecordNumberIssuer`, `FormRecordWriter`, `FormOfferBuilder`, `FormStructuredSource`, `FormRecordSink`, `FormRecordSource`, `IFormReader`, `FormsAccountDeletionParticipant`, `WhatsAppFlowBuilder`, `WhatsAppFlowPreviewLinkBuilder`, `WhatsAppFlowReplyVerifier`, `IWhatsAppFlowPublicationService`, `IWhatsAppFlowReplyService`, `IWhatsAppFormSendService`, `WhatsAppFlowClient`, `WhatsAppStructuredFormChannel`, `OrganizationWhatsAppConversationsControllerBase`, los workers, `IAccountCultureReader` y los cuatro puertos.
- **Contrato:** las rutas de arriba; `FieldInUse` con `uses`, al que suman la E16 y la E19; `publication-checks` sin `warnings` (los suma la E16); `form` y `lastForm`; `formPublicationFailed`; los cinco permisos, cuyos textos completan la E17 y la E20.
- **Front:** `FormsPage.tsx`, `FormPage.tsx` con `formTabs.ts`, `FormEditorPage.tsx`, `QuestionEditor.tsx`, `CatalogQuestionFields.tsx`, `PublishDialog.tsx`, `PublicationBand.tsx`, `OfficialPreviewDialog.tsx`, `FormPreviewPhone.tsx`, `SendFormDialog.tsx` y `FormMessageCard.tsx`.
- **Pruebas y guías:** el Meta simulado de B12 y `quitar-formularios.md`.

## Cambios al contrato


## Para decidir

Nada: lo resuelven los tableros v169 y las decisiones de la §13 de la spec.
