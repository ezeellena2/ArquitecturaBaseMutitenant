# E14 · Formularios: crear, publicar, mandar y recibir · plan ejecutable

**Estado:** pendiente · Spec: [`formularios/14-formularios.md`](formularios/14-formularios.md), con sus partes de [decisiones y flujos](formularios/14-parte-decisiones-y-flujos.md), [datos y contrato](formularios/14-parte-datos-y-contrato.md) [archivos, tests y Meta](formularios/14-parte-archivos-tests-y-meta.md) y [premisas, criterios y acciones](formularios/14-parte-premisas-criterios-y-acciones.md). Tableros: los de la v175, salvo los que la spec marca «Se redibujan con el traspaso 18, tanda 2»: la tarea que usa uno de esos espera la versión del lienzo que lo trae, y donde un tablero contradiga la 261 a la 264 o la 275, manda la decisión.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B12 | Pendiente |
| Front | F1–F9 | Pendiente |
| Integración y puerta | I1–I4 | Pendiente |

## Forma de trabajo

Modo rápido: contrato primero; después los carriles del back y el front a la vez; una sola puerta al final ([etapa en paralelo](../guides/etapa-en-paralelo.md) y [ADR 0071](../decisions/0071-conductor-en-carriles.md)). Un arreglo corre solo lo que toca. En el back, cada tarea espera a la anterior, porque usa lo que deja. La etapa no declara acciones del asistente (§17 de la spec).

**Reglas de cada chat:** todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con `git commit -- <rutas>`, en español y conventional, con la línea `Tarea: 14-formularios/<id>`; con la app levantada, nadie edita el `src` del front; TDD donde hay lógica. Cada tarea corre su test dirigido y, al terminar, el build y los tests rápidos enteros, sin Docker ni filtrar por módulo, con `node scripts/arneses/verificar.mjs` en el back y `node scripts/verificar.mjs` en el front; los de integración y de seguridad corren una vez, al cerrar cada carril del back. El contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md)): la llevan las tareas **(R)**, completa y antes de su commit.

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

## Contrato

### K0 · Lo que dejaron la base, la E12 y la E13

El chat del contrato confirma cada nombre en el código. Lo que difiera va a «Cambios al contrato» antes de programar.

- **Base de la 12:** `INotificationSource`, `NotificationContext`, `NotificationItem` y el sufijo `NotificationSource`; en el front, `notifications` (`{kind, describe}`) en `registration.ts`, `InfoTip`, `PageLoading`, `FormSkeleton`, `ConfirmDialog` con `pending`, los botones, `FilterBar` con un solo «Filtros» y la escala.
- **Segunda parte de la base (214):** «Sin confirmar» (`OutboxStatus.Uncertain`) con su revisión por el id del envío (ADR 0066); `WhatsAppOutboundMessage.QueueOptions(OutboxPriority.Reply)`; los códigos de `WhatsAppSendFailures` en «No se pudo mandar»; el cifrado del chat, con `WhatsAppChatContentConverter` y el propósito de `engagement` (ADR 0065); el control del anillo de claves (B15, `IKeyRingReadiness`); las fechas con coma (205), los avisos (210), el tooltip también en el teléfono (211), los roles en áreas plegables con lo incluido (260), la pieza `Picker` (262), el orden por columnas (264), la configuración de WhatsApp de la organización y la de la plataforma (B18 y F13, 272), `PersonalDataPurgeTests` (B2) y `CoreTextsNeutralTests` (B9).
- **Costuras de módulos** ([0069](../decisions/0069-modulos-y-suscripciones.md)): `IModuleLimitContributor`, `ModuleLimitDefinition`, `ModuleLimitResolver.ResolveAsync`, `ModuleLimitCeiling.Effective`, `ModuleLimitArchitectureTests` y la regla «Topes (0069)» de modulos-habilitados; `WhatsAppModuleLimitContributor`, con `whatsapp.chatRetentionDays`, al que esta etapa suma `whatsapp.flowSessionHours`; `TenantFeatureRow.IsModuleEnabled` y «Apagar no borra datos (0069)».
- **E12:** lo que nombra «Deja para las siguientes» de su plan, sobre todo el módulo (`AddFormsModule`, `Permissions.Forms`, `FormsTexts` y `FormsContractContributor`), `FormCatalog`, `FormOptionTitle` y `FormCatalogErrors` (`ItemInUse` con `uses`), `IFormCatalogRepository` con `LockAsync`, `IFormCatalogReader`, `FormCatalogService`, `MembersCatalogSource`, `IMemberDirectoryReader`, `IStructuredContactSource`, la guarda de ejemplos, `FormsPersonalDataPurgeTests`, `FormsModuleOffKeepsDataTests`, los scripts de formularios y, en el front, `src/modules/forms/` y la lista de vistas previas de `picker-usage.test.ts`.
- **E13:** `FormCatalogField`, `FormCatalogPresentation` (`{title[], description[]}`), `FormCatalogPresentationBuilder` y `FormCatalogFieldEditor`; en el front, `CatalogCombo.tsx`, `CatalogFieldsDialog.tsx` y `PresentationEditor.tsx`.

### K1 · Publicar el contrato (R: permisos)

Las rutas responden 501 hasta su tarea. K1 publica controllers, contratos, permisos, códigos y textos, y genera `openapi.json`; el Front corre `npm run contracts`. Cada tarea del Back exporta los ejemplos reales de sus rutas.

**Forms:** `[Access(Business)]`, `[FeatureGate("forms")]`, `/api/organization/forms`. Los errores van sin `Forms.`.

| Ruta | Permiso | Idempotencia y versión | Errores |
| --- | --- | --- | --- |
| GET `/` (`sort`, 264) y `/filter-counts` (`{all}`, 262) | `forms.read` | — | — |
| POST `/` | `forms.manage` | `[Idempotent]`, 201 | `Definition.PrefixTaken`, `PrefixInvalid`; `Draft.*` |
| GET `/{id}` | `forms.read` | — | `Definition.NotFound` |
| PUT `/{id}/draft` | `forms.manage` | `version`, 409; `[ResponseVersion("Version")]` | `Definition.NotFound`, `PrefixTaken`, `PrefixLocked`, `PrefixInvalid`; `Draft.QuestionInvalid`, `TooManyQuestions`, `TooManyOptions`, `CatalogUnavailable` |
| POST `/{id}/publication-checks` | `forms.publish` | 200 | `Definition.NotFound` |
| POST `/{id}/publications` | `forms.publish` | `[Idempotent]`, 202 | `Publication.DraftChanged`, `InProgress`, `NothingToPublish`, `Incomplete`, `ChannelIssues`, `ChannelUnavailable`; `Draft.QuestionInvalid`, `CatalogUnavailable`, `CatalogTooLarge`, `CatalogPresentationInvalid` |
| GET `/{id}/publications/{publicationId}` | `forms.read` | — | `Publication.NotFound` |
| POST `…/{publicationId}/check` | `forms.publish` | 200 | `Publication.NotFound`; 429 `Meta.TooManyCalls` |
| POST `…/{publicationId}/resolution` | `forms.publish` | 200; lo protege el estado | `Publication.NotFound`, `NotResolvable` |
| POST `…/{publicationId}/discard` | `forms.publish` | 200, como `resolution`; lo protege el estado | `Publication.NotFound`, `NotDiscardable` |
| GET `/{id}/versions/{versionId}/preview` | `forms.read` | `no-store` | `Preview.NotAvailable`; `WhatsApp.Flow.PreviewUnavailable` (503); 429 `Meta.TooManyCalls` |
| GET `/catalogs/{id}/used-by` | `forms.read` | paginado | `Catalog.NotFound` |

**WhatsApp:** `GET` y `POST /api/organization/whatsapp/{customers|internal}/conversations/{id}/forms`, en `OrganizationWhatsAppConversationsControllerBase`, nueva, que heredan las dos bandejas y no Plataforma, con `whatsapp.customers` o `whatsapp.internal`. El `POST` lleva `[Idempotent]` y responde 202. Errores: `WhatsApp.Conversation.NotFound`, `TakenByOther`, `WindowClosed`, `PaymentRequired` y `ReadOnly`; `WhatsApp.Chat.NotEmployee`, `FormUnavailable` y `SendPending`; `WhatsApp.Flow.NotPublishedForNumber`, y `Forms.Offer.*`.

Ninguna ruta declara cuota propia. Cada lista anidada declara su tope y su fila en `GrowingCollectionContractTests`.

**Cuerpos y tipos:** los de la §5 y la §7 de [datos y contrato](formularios/14-parte-datos-y-contrato.md), con estos cambios:
1. **POST `/` recibe el primer borrador,** `{name, prefix?, audience, submitLabel, questions[], titles[]}`, como «Guardar borrador» de `FRM-Editor`. Sin `prefix`, el servidor propone uno libre.
2. **Preguntas y títulos:** una pregunta nueva viaja sin `fieldId`, y una opción, sin `choiceId`; la respuesta los devuelve en el mismo orden. Un título es `{titleId?, text, start}`, con `start` igual a cuántas preguntas van antes. Dos con el mismo `start`, o uno al final, no tienen preguntas debajo (T24). El puerto cambia `BeforeFieldId` por `Start`. `presentation` es `null` (la del catálogo) o la forma de la E13.
3. **El mensaje del chat** suma `form?: {sessionId?, formName?, versionNumber?, state, errorCode?, errorFieldLabel?, displayNumber?, values?, canReadValues}`. Al salir, `state` es `Waiting`, `Answered` o `Unanswered`; al llegar, `Saving`, `Saved`, `Rejected`, `Late` o `Unrecognized`.
4. **La fila del chat** suma `lastForm?: {formName?, state}` (`Sent`, `SendFailed`, `Unconfirmed` (240), `Saving`, `Saved`, `NotSaved` o `Unrecognized`), con `lastMessagePreview` en `null`. La de «Mensajes» suma `lastForm?: {formName}` («Vos: Respondiste …»).
5. **La campana:** `formPublicationFailed`, con `{formName, versionNumber}`, `link` `/org/formularios/{id}` e `id` `formPublicationFailed:{publicationId}`, a quien tiene `forms.publish`.
6. **Los catálogos de la E12** suman `usedByCount`, `inUse` y `usedIn`; su `PUT` suma `Forms.Catalog.FieldInUse` (409, con `uses` en `presentation` y `forms[]`, nulo sin `forms.read`), e `ItemInUse`, `uses: records`, que se lee «No se puede borrar: la eligió una respuesta. Podés darla de baja.» (*A response chose it; you can deactivate it.*).

**Permisos:** los cinco de la spec, en `Permissions.Forms`, con los textos de la §2 de la hoja de ruta recortados a lo que existe (78): `forms.manage`, «Crear y editar formularios en la pestaña «Diseño».» (*Create and edit forms in the Design tab.*); `forms.publish`, «Publicar un formulario en WhatsApp.» (*Publish a form on WhatsApp.*); `formrecords.create`, «Completar formularios por WhatsApp.» (*Fill in forms on WhatsApp.*). Sus áreas en los permisos de un rol (260): `Area.forms`, «Formularios» (*Forms*), y `Area.formrecords`, «Respuestas» (*Responses*). `forms.manage` y `forms.publish` incluyen `forms.read` (`Includes`); `formrecords.create` no incluye nada, porque sirve solo. El Dueño los recibe en el seed y en `<fecha>_SystemRoleFormsPermissions.cs`. `forms.publish` y `formrecords.create` se suman a `CatalogPermissionCodes` de `CoreTextsNeutralTests`, porque sus descripciones nombran WhatsApp.

**Errores:** los de la §4 de la spec, con sus textos es y en en [«Textos de los errores»](formularios/14-parte-premisas-criterios-y-acciones.md#textos-de-los-errores).

**Texto para el chat del Contrato**

```text
Leé AGENTS.md, este plan y la parte de datos y contrato de la spec de la E14. Hacé K0 y después K1, como dice el plan, y avisá a la Guía para que el Front corra npm run contracts. No programes la lógica ni toques el front. Reglas: las de «Forma de trabajo» de este plan.
```

## Back

Cada tarea copia sus moldes, con una UoW por método que escribe, RLS, auditoría y nada de Meta adentro de una UoW. Los tests salen de la §10; abajo, lo que no puede faltar.

1. **B1 · Dominio y tablas de Forms (R: datos).** `D/Forms/`: `FormDefinition`, `FormDefinitionState`, `FormVersion`, `FormPublication`, `FormRecord`, `FormRecordValue`, `FormQuestionType`, `FormDraft` y sus `*Errors.cs`; `FormPublicationWork` en `platform`; sus configuraciones, como `FormRecordValueConfiguration`, y `<fecha>_FormDefinitions.cs`, con los índices de la §3 y `platform.FormMetaCallCounters`. Copia `AssistantOrganizationSettings.cs`, `AssistantTurnWork.cs` y `20261010011328_AssistantModule.cs`. Tests: los de dominio de la §10; RLS y migración, también hacia atrás.
2. **B2 · Puertos neutrales.** Los cuatro de la §7, sus modelos y `I/StructuredData/Disabled*.cs` con `TryAdd`. Copia `IStructuredContactSource` [E12]. Tests: los apagados y `ModuleIsolationTests`.
3. **B3 · Crear y guardar el borrador.** `IFormDefinitionService`, `FormDefinitionService`, `FormPrefixResolver`, `FormDraftEditor`, los validadores, `IFormDefinitionRepository`, `IFormReader` y `FormReader`, y las rutas de la lista, el alta, el detalle y el borrador. Copia `AssistantConversationService.cs` y `RoleReader.cs`. Tests: «elegir varias» y títulos; guardar sin cambios escribe solo `ModifiedAtUtc`, como la E12, sin emitir identidades ni auditar preguntas; ningún 409 falso por una respuesta o una publicación; 401, 403, feature apagada, idempotencia y paginado.
4. **B4 · La pregunta de catálogo y «Lo usan» (R: permisos).** `catalogId` y `presentation` en el borrador (§6b), `FormOfferBuilder` (una fila más que el tope y `FormOptionTitle`) y lo que suma a `FormCatalogService` [E12] y `FormCatalogFieldEditor` [E13], leído con `IFormReader` sobre el índice de `FormRecordValues`. Tests: los cinco problemas de la oferta y los nombres largos; `FieldInUse`, con y sin `forms.read`; `uses: records`; `used-by` en su segunda página; 10.000 opciones y 20 campos con EXPLAIN.
5. **B5 · Comprobar, publicar y el worker (R: Meta, trabajos de fondo y cuotas por alcance).** `IFormPublicationService` y `FormPublicationService` (`CheckAsync`, `ClaimAsync`, `CompleteAsync`, `ResolveAsync` y `DiscardAsync`, §6, punto 4), `FormVersionCreator`, el `ContentHash` canónico, `I/Modules/Forms/FormPublicationWorker.cs` con `ExecutionActor`, que no toma una publicación con `forms` apagada para su organización (`IFeatureService` con el actor; 0069 §5) y recibe `IEnumerable<IKeyRingReadiness>`: como `WhatsAppTemplateSyncWorker`, no toma nada en la vuelta si uno dice que no (0064 §11; nunca `AiKeyRingReadiness`); `FormPublicationNotificationSource` y sus rutas. Copia `AssistantTurnService.cs`, `AssistantTurnWorker.cs` y `AssistantTurnRepository.ListReadyAsync`. Tests: con un canal de prueba, `Published`, `Uncertain`, `Failed` y `RequiresReview`; «Salió», «No salió» y `NotResolvable`; descartar una `Unconfirmed` que el worker consultó en el medio, `NotDiscardable` y `InProgress` con el id de la abierta; la espera que crece del worker; el tope de consultas a Meta por organización, contado en `FormMetaCallCounters` entre copias, que no frena a otra; una copia con otro anillo no toma publicaciones (copia `AiKeyRingWhatsAppInboundTests`); `DraftChanged`; la misma clave; la 103; sin `forms.publish`, `Failed`; la campana.
6. **B6 · El Flow en Meta (R: Meta y datos).** `WhatsAppFlowBuilder` (el perfil `flow-static-7.3-r9` de la §14, con `Check`, el nombre del Flow y el patrón del número con la coma primero, O29), `D/WhatsApp/WhatsAppFlowBinding.cs`, `IWhatsAppFlowPublicationService` y su servicio, `I/Modules/WhatsApp/WhatsAppFlowClient.cs` (141006 es `AccountUnavailable`), `WhatsAppStructuredFormChannel` y `<fecha>_WhatsAppFlowBindings.cs`. Copia `WhatsAppTemplateClient.cs`, `WhatsAppTemplateIssuer.cs` y `20261014031304_WhatsAppInboxes.cs`. Tests: cada componente y sus límites en UTF-16; ninguna «,]» ni «,}» en el JSON armado; sin `data` vacío; 2xx, 4xx, 5xx, 429, tiempo y Create sin id; `InFlight` antes de cada POST; ante una duda, verificar.
7. **B7 · La vista oficial (R: Meta).** `GetPreviewAsync` en Forms y `WhatsAppFlowPreviewLinkBuilder` (§6, punto 5), con el tope de 30 s. Tests: los de la §10, y `WhatsAppContentSecurityPolicyTests` con `business.facebook.com`.
8. **B8 · Mandar desde el chat (R: Meta, datos y permisos).** `IWhatsAppFormSendService` y `WhatsAppFormSendService`, `D/WhatsApp/WhatsAppFlowSession.cs` (con `SentAtUtc`, por `TimeProvider`, y el vencimiento con `ModuleLimitCeiling.Effective(FlowSessionHours)`, leído al mandar, 270) y `<fecha>_WhatsAppFlowSessions.cs`, con la excepción de la 0060 de T27 anotada; `WhatsAppModuleLimitContributor` suma `whatsapp.flowSessionHours` (`Count`, entrada 24, mínimo 1) con su máximo `MaxFlowSessionHours`, cuyo getter entra en `ModuleLimitArchitectureTests`; `FlowSessionHours` va en `WhatsAppOrganizationSettings` y `MaxFlowSessionHours` en `WhatsAppPlatformSettings` (B18 de la base 2), en esa migración, con sus constantes en `WhatsAppSettingsLimits` (24, de 1 a 168; máximo 168), y el `PUT` de la organización valida contra `ModuleLimitResolver.ResolveAsync`; el mensaje sale con `QueueOptions(OutboxPriority.Reply)`, como `WhatsAppReplyIssuer`; `FormStructuredSource`, la variante Flow de `WhatsAppCloudClient.BuildPayload`, `IAccountCultureReader` en `Infrastructure/Identity` (en `IdentityAccessTests` e `IdentityStorageAccessTests`) y las dos rutas en `OrganizationWhatsAppConversationsControllerBase`. Copia `WhatsAppConversationService.SendAsync`, `WhatsAppReplyIssuer` y `ActiveMemberPhoneReader`. Tests: botón y motivos por bandeja, y sin ruta en Plataforma; `TakenByOther`, `PaymentRequired`, `ReadOnly`, `SendPending` (que «Sin confirmar» no da) y `NotPublishedForNumber`; la misma clave; el mensaje por defecto en el idioma de quien recibe; el tiempo para contestar, con el 409 y el tope leído por el resolvedor; bajar el máximo en Plataforma acorta las sesiones nuevas de una organización que había elegido más, sin tocar lo guardado ni a otra organización; una sesión vieja conserva su vencimiento.
9. **B9 · Recibir y capturar (R: datos, cifrado y aislamiento).** El webhook lee `interactive.nfm_reply` y guarda `ProtectedFlowResponse` (24 KiB); el procesador lo pasa a `IWhatsAppFlowReplyService.ClaimAsync`, a la captura y a `CompleteAsync`, sin `ProcessAsync` ni bot; `WhatsAppFlowReplyVerifier`; `FormRecordSink` → `IFormRecordService.CaptureAsync`, con `FormRecordNumberIssuer` y `FormRecordWriter`, que sella también el texto de la opción fuente de un campo «De un catálogo», leído por lote; `FailAsync` con los intervalos de la §16, y lo que esta versión no entiende, para otra copia (§6.7); `<fecha>_WhatsAppFlowReplies.cs`; el `nfm_reply` sigue T27. Copia `WhatsAppInboundProcessor.cs`, `WhatsAppInboundService.cs` y `WhatsAppWebhookReader.cs`. Tests: los de captura de la §10, con dos capturas a la vez sin saltos y el texto sellado; «1.500», «1,5», «1.5» y «1500» en es y en (207); un perfil desconocido no queda `Rejected` antes del día.
10. **B10 · La tarjeta, la lista y «Mensajes» (R: datos, cifrado, permisos y aislamiento).** `FormRecordSource`, el `form` del mensaje, `lastForm` (el chat guarda su último mensaje de formulario, y el estado se deriva al leer) y `FormCard` en `WhatsAppLinkedMessage`, un JSON en `bytea` con `WhatsAppChatContentConverter.Linked`, como el mensaje «Formulario» con `.Organization` (ADR 0065), con `<fecha>_WhatsAppFormCards.cs`. Copia `WhatsAppConversationReader.cs` y `WhatsAppConversationLinkWriter.cs`. Tests: con y sin `formrecords.read`; la fila sin valores; la tarjeta en «Mensajes»; un cifrado copiado a otra fila se lee vacío.
11. **B11 · Limpieza, baja y retiro (R: trabajos de fondo y datos).** `WhatsAppFlowSessionCleanupWorker` (copia `WhatsAppBroadcastWorker.cs`; una vez entre copias, por lotes con `FOR UPDATE SKIP LOCKED`), la retención de `ProtectedFlowResponse` (`ClearText` y `ClearPersonalData` la vacían, y `ListExpiredTextAsync` toma además los mensajes que solo la tienen y ya se procesaron), `FormsAccountDeletionParticipant` con su repositorio de limpieza (copia los del Asistente) y `FormsMaintenanceLayerTests` (copia `AssistantMaintenanceLayerTests`), las tablas de Forms en `FormsPersonalDataPurgeTests` [E12] (`FormRecords`, `FormRecordValues`, `FormVersions`, `FormPublications` y `FormPublicationWorks`); lo de WhatsApp, sin guarda: la baja olvida `SentByUserId` y `RecipientUserId` de las sesiones en `WhatsAppAccountDataRemover.ForgetOrganizationAuthorAsync`, como `ForgetAuthor`; `docs/guides/quitar-formularios.md` (copia `quitar-asistente.md`) y las guardas del módulo. Tests: la oferta vencida se borra, también con dos corridas a la vez; la purga vacía la respuesta de un `nfm_reply` sin texto; la baja deja `RespondentUserId` en null; `FormsModuleOffKeepsDataTests` [E12] suma un formulario publicado, una publicación en curso y una respuesta: con `forms` apagada el worker no toma la publicación ni la pasa a `Failed`, y al prenderla sigue donde estaba.
12. **B12 · Meta simulado.** En `TestFeatures/WhatsAppBrowser/RecorridoWhatsAppModule.cs`: `FakeWhatsAppFlowClient`, que publica al primer intento; en lugar de `ForbiddenWhatsAppSender`, `FlowTokenRecordingSender`, que no sale a Meta y guarda el `flow_token`; y la respuesta de un formulario por `WhatsAppFileBridge`. Copia `WhatsAppBrowserControl.cs` y `RecorridoWhatsAppModule.cs`.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan, la spec de la E14 con sus tres partes, las decisiones 207, 261 a 264, 269, 270, 274 y 275 de la hoja de ruta y las fichas de su «Qué leer». Esperá K1 y hacé B1 a B12 en orden, con sus moldes; las tareas (R) llevan la revisión adversarial antes del commit. Forms y WhatsApp se hablan solo por los puertos de la §7. No toques el front. Reglas: las de «Forma de trabajo» de este plan. No edites la tabla de estado.
```

## Front

Cada pantalla copia su tablero (la v175 o el que dice «Estado») con todos sus estados (§9 de la spec), con i18n, tipos generados, `shared/format`, TanStack Query y axe.

1. **F1 · Tipos, API y textos.** `npm run contracts`; `src/modules/forms/api/forms.ts`, `errors.ts` y los locales `forms` y `enums`; en WhatsApp, `chats/api/chats.ts` y `personal/api/messages.ts`. Copia `api/catalogs.ts` [E12]. Tests: zod estricto con los ejemplos y paridad es/en.
2. **F2 · Navegación, lista y pestañas.** `registration.ts` suma `/org/formularios`, `/org/formularios/nuevo` y `/org/formularios/:id` con `forms.read`, «Formularios» en el panel y los cinco permisos, que `Rol` muestra; `pages/FormsPage.tsx`; `pages/FormPage.tsx`, con las pestañas por `Page tabs` desde `lib/formTabs.ts`, al que cada etapa suma la suya (con una sola, sin barra, 193). Tableros: `FRM-Formularios` y su teléfono. Tests: permisos; el filtro en la pieza, sin cantidades (262); cada columna ordena (264); el panel en dos grupos y el nombre que abre el editor (275); vacío y 320.
3. **F3 · El editor.** `pages/FormEditorPage.tsx`, `components/QuestionEditor.tsx`, los títulos (88), «Para quién es», el prefijo, `FormPreviewPhone.tsx` al costado, que suma su entrada a la lista de vistas previas de `picker-usage.test.ts` [E12] si dibuja radios o un select como WhatsApp, y «otra persona guardó» lado a lado (138). Un corte al guardar relee y compara solo el contenido: esa excepción a concurrencia va en `docs/rules/formularios.md`, y las preguntas que se abren en el lugar, en `paginado-y-listados.md`. Tableros: `FRM-Editor` y su teléfono. Copia `CatalogEditorPage.tsx` [E12] y `useEditForm`. Tests: cada estado, los botones (43 y 115) y el corte; el tipo y «Obligatoria» en sus columnas (263), y lo que se elige, en la pieza (262).
4. **F4 · La pregunta de catálogo.** `components/CatalogQuestionFields.tsx`, con `PresentationEditor.tsx` [E13], «Permitir elegir varias» y «En WhatsApp» plegada (145). Suma a `CatalogCombo.tsx` [E13] el origen, el estado (`status=all`) y la consulta como parámetros, con lo que no se puede usar apagado y su `unusableReason`. Tests: los tres orígenes y lo apagado; se abre sola con un error; apagada sin `formcatalogs.read`.
5. **F5 · Publicar y cómo termina.** `components/PublishDialog.tsx` (las zonas de la 112, con «Ir a la pregunta» e «Ir al título») y `components/PublicationBand.tsx` (la etiqueta de la 175, «Consultar publicación», «Repetir el mismo intento» con la misma clave, «Salió» y «No salió» con su confirmación, «Descartar esta publicación» con la suya (225), y el motivo de lo apagado en un tooltip sobre el botón, también en el pie fijo del teléfono (211)). Tableros: `FRM-Publicar`, `FRM-Publicando` y `M-FRM-Editor`. Tests: cada estado, también dónde va «Descartar esta publicación»; un corte relee y repite con la misma clave.
6. **F6 · La vista oficial.** `components/OfficialPreviewDialog.tsx`: el marco de 480 × 840 achicado; cargando, lista, vencida y con error; «Abrir en otra pestaña»; cerrar cancela el pedido. Tableros: `FRM-Vista-Oficial` y su teléfono. Tests: la medida a 1440, 390 y 320.
7. **F7 · «Lo usan».** La columna del listado, la banda y la sección del catálogo con `used-by`, «Quitar «…»» con «Ir a la pregunta», la opción que eligió una respuesta y la advertencia de la baja, sobre `CatalogsPage`, `CatalogEditorPage` y `CatalogFieldsDialog`. Tableros: `FRM-Catalogos-14`, `FRM-Catalogo-14` y sus teléfonos. Tests: con y sin `forms.read`.
8. **F8 · Enviar formulario y la tarjeta.** En `src/modules/whatsapp/chats/`: el botón, solo en las bandejas de la organización, en `ChatComposer.tsx` y en el ⋮ del teléfono, `components/SendFormDialog.tsx` (el formulario en la pieza, 262, y el mensaje que propone sin pisar lo escrito), `components/FormMessageCard.tsx` en `MessageThread.tsx`, con el código y el detalle de un valor de catálogo en su renglón (263) y «Mandar de nuevo», y `lastForm` en `ConversationItem.tsx`. Copia `TemplateDialog.tsx`. Tableros: `WA-Chats-Formulario`, `WA-Chats-Formulario-Cliente` y `WA-Chats-Respuesta`, con sus teléfonos. La tarjeta de un envío «Sin confirmar» lleva la revisión del chat (240, ADR 0066). Tests: cada estado, también «Sin confirmar» y su revisión, que no reenvía; no aparece en Plataforma; un corte al mandar repite con la misma clave; «Enviar» no llama a Meta (H33).
9. **F9 · «Mensajes», campana y galería.** La tarjeta en `personal/pages/MessagesPage.tsx` y «Vos: Respondiste …»; el texto de `formPublicationFailed`, con `notifications` en `registration.ts`; `scripts/capturas/etapa-14.json` con lo nuevo o cambiado. Tableros: `WA-Mensajes-14`, `M-WA-Mensaje-14`, `Campana-14` y su teléfono. Suma el tiempo para contestar a la configuración de WhatsApp de la organización y su máximo a la de la plataforma (270). Tests: la tarjeta en «Mensajes», el aviso con su enlace, el tiempo para contestar con su 409 y axe.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, este plan, la §9 de la spec de la E14, las decisiones 261 a 264, 269, 270 y 275 de la hoja de ruta y las fichas de UI del front. Esperá K1, corré npm run contracts y hacé F1 a F9 contra los tipos generados y los tableros de cada tarea, con todos sus estados. No toques el lienzo, las specs ni el back. Reglas: las de «Forma de trabajo» de este plan. No edites la tabla de estado.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real, con los tipos regenerados y los ejemplos con zod estricto; cada diferencia vuelve a su dueño.
2. **I2 · Puerta.** `/puerta`, una vez sobre copias del corte: las suites de los dos repos, con Docker, y `contracts:check`; el E2E real (`test-e2e-real-forms.mjs`) con el Meta de B12 y los casos de la fila «E2E real y recorrido» de la §10; `realinear-docs` y `estado-docs` en 0; la galería de lo nuevo o cambiado, a 1440, 390 y 320; `/recorrido` (`recorrido-usuario-forms.mjs`) con `publicar-un-formulario`, `mandar-un-formulario` y `usar-un-catalogo`; quitar Forms una vez con la base vacía, con `quitar-formularios.md`; y la lista de control de `/puerta`, con los criterios de la §16 y los riesgos de la [prueba integral de ArquitecturaBase](../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase): la coma de la ayuda del número, con `[,.]` y ninguna coma antes de `]` o `}` (O29); el número ambiguo, con su pregunta nombrada (O30); «1.500,5», que frena Meta, con la ayuda «sin separador de miles» (O42), lo que se guarda del webhook para diagnosticar una respuesta de Flow que se pierde (O49), y el tope de un texto de Forms, el mismo en la pantalla y en el servidor, que al llegar se ve (O13, 269).
3. **I3 · Al cerrar.** `docs/features/formularios.md` suma los formularios, y `docs/features/whatsapp.md`, los Flows; cada carpeta nueva, su `AGENTS.md`, su `CLAUDE.md` y su fila en `arbol.md`.
4. **I4 · Pendiente para la I1 de la 10:** la prueba con Meta de la §15, con el número de una organización, en Android y iPhone.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido / Retiro | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan y /puerta. Integrá contra el contrato, devolvé cada diferencia a su dueño y corré la puerta una vez sobre copias del corte. No manejes la PC del usuario; la prueba con Meta queda para la I1 de la 10. Dejá solo la tabla corta del resultado. Reglas: las de «Forma de trabajo» de este plan.
```

## Relación con otras etapas

**Usa de las anteriores:**
- **Base:** la campana (`INotificationSource` y `notifications`) y las piezas del front que nombra K0.
- **E11:** `WhatsAppChannel`, `WhatsAppChat`, `WhatsAppChatMessage`, `SendAsync` y `WhatsAppReplyIssuer`, la cola de entrada, `IOutbox`, `WhatsAppLinkedMessage` e `IActiveMemberPhoneReader`; en el front, `ChatComposer`, `MessageThread`, `ConversationItem` y `MessagesPage`.
- **E12 y E13:** la lista de K0.

**Deja para las siguientes:**
- **Dominio y tablas:** las de B1, B6, B8, B9 y B10, con `SourceKey` (`whatsapp:<sessionId>`), `ChoiceFields` (con el texto de la opción fuente), `SentAtUtc`, `ProtectedFlowResponse` y `FormCard`.
- **Back:** los servicios, helpers, workers y puertos de B2 a B11 (`CompleteAsync` y `ResolveAsync` activan la versión en una UoW), `OrganizationWhatsAppConversationsControllerBase`, `IAccountCultureReader` y `FlowSessionHours`.
- **Contrato:** las rutas de arriba; `FieldInUse` con `uses`, al que suman la E16 y la E19; `publication-checks` sin `warnings` (los suma la E16); `form` y `lastForm`; `formPublicationFailed`; los cinco permisos, cuyos textos completan la E17 y la E20.
- **Front:** los componentes de F2 a F9, con `FormPage.tsx` y `formTabs.ts`.
- **Pruebas y guías:** el Meta simulado de B12 y `quitar-formularios.md`.

## Cambios al contrato


## Para decidir

Nada.
