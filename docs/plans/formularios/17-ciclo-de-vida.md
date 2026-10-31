# E17 · Ciclo de vida: dar de baja, reactivar, borrar y copiar (spec)

**Estado:** pendiente · spec de la etapa 17 del bloque (decisiones 34, 74 y 78), con el [formato](plantilla.md) de las etapas. En ArquitecturaBase está programado y revisado; copiar no existe allá. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E16.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T4, T7, T8, la §3 (`FormDefinitionStates`, `FormPublications` y `WhatsAppFlowBindings`), la §6, puntos 4 y 6, y la §14 (errores de Meta); la [E15](15-respuestas.md): las dos rutas de la lista, y la [E16](16-datos-de-las-personas.md): los filtros por destinatario, que se copian.
2. **Decisiones** de la hoja de ruta: 23, 34, 35, 43, 74, 78 y 79.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [guardado](../../rules/guardado.md) y [concurrencia](../../rules/concurrencia.md) | todo bajo el bloqueo de la fila de estado; el 409 de borrar |
   | [multitenancy](../../rules/multitenancy.md) | el worker entra por organización, sin transacción abierta |
   | [modulos](../../rules/modulos.md) | Forms y WhatsApp, por puertos neutrales |
   | [persistencia-ef](../../rules/persistencia-ef.md) | el borrado lógico y las columnas nuevas |
   | [idempotencia](../../rules/idempotencia.md) | copiar crea; dar de baja y borrar no se repiten por el estado |
   | [auditoria](../../rules/auditoria.md) | la baja vive en una fila sin auditoría: se registra a mano |
   | [api-http](../../rules/api-http.md), [permisos](../../rules/permisos.md) y [result-y-errores](../../rules/result-y-errores.md) | rutas y errores |
   | [textos-y-traducciones](../../rules/textos-y-traducciones.md) | «Dar de baja», «Reactivar» y «Borrar», nunca «Eliminar» |
   | [logs](../../rules/logs.md) y [tests](../../rules/tests.md) | el worker y qué lleva test |

4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §4 (V-01 a V-07), el anexo D (`BA1-A` a `BA5`) y el anexo E §2.4 (R1021a). Lo que dice de Meta está en `src/ArquitecturaBase.Domain/Modules/WhatsApp/WhatsAppFlowBinding.Deprecation.cs` y en `WhatsAppFlowClient.Deprecation.cs`, solo para leer.
6. **La maqueta,** en `../MaquetaMultitenant/src/areas/business/forms/`: `components/LifecycleDialogs.tsx`, `CopyFormDialog.tsx` y `api/copies.ts`. Se copia lo que se ve; lo del asistente, las sugerencias, el PDF y las reglas llega después.

## Qué se construye

**Dar de baja:**
- guarda quién y cuándo, y deja de ofrecer el formulario. Pide `forms.publish`;
- conserva la versión activa, que usa la planilla. Los envíos abiertos conservan su versión y se responden hasta vencer. Las respuestas y la numeración siguen igual: una respuesta que llega después de la baja se guarda;
- con una publicación abierta, responde `Forms.Publication.InProgress`;
- **un solo filtro de disponibilidad** deja de ofrecer los dados de baja y los borrados. Hoy lo usa el envío desde el chat, y toda etapa que ofrezca formularios usa el mismo;
- no borra nada, y se puede seguir editando el borrador.

**Reactivar** es la publicación de siempre con el borrador actual: versión nueva y Flows nuevos. Usa las rutas de publicar, y `NothingToPublish` no aplica, porque los Flows viejos no se pueden recuperar. Sigue dado de baja hasta que se confirma la versión nueva, y si falla, sigue dado de baja. La numeración continúa. Pide `forms.publish`.

**Borrar:**
- dice «Borrar» en el ⋮, «¿Borrar «…»?» en el diálogo y «Borramos «…».» en el aviso. Nunca «Eliminar»;
- es lógico, y solo se puede sin respuestas, sin envíos abiertos ni respuestas en proceso, y sin publicación en curso. Pide `forms.manage`;
- no borra versiones ni evidencias del canal. Si llegó a publicarse, sus Flows se deprecan;
- el prefijo queda reservado dentro de la organización. Con respuestas, Borrar no aparece;
- un formulario borrado no frena quitar un campo ni dar de baja un catálogo, y sale de «Lo usan». Uno dado de baja sigue contando.

**Copiar** (74): crea uno nuevo en borrador con el borrador del original, nombre y prefijo propuestos, que se pueden cambiar. Pide `forms.manage`, y sirve también con uno dado de baja. **Copia el borrador tal cual, con sus identidades:** un `FieldId` vale solo dentro de su formulario, así todo lo que apunta a una pregunta sigue apuntando bien. Las preguntas de catálogo apuntan a los mismos catálogos, sin duplicarlos, con su presentación propia y sus filtros. No copia versiones, respuestas ni publicaciones. Las etapas siguientes suman lo suyo a la copia. Copiar no exige que el borrador esté completo: eso se comprueba al publicar.

**Deprecar los Flows en Meta:**
- **es de WhatsApp,** por cada WABA de la organización y con su token. Forms no llama a Meta ni escribe en WhatsApp;
- **Forms dice qué versiones ya no se usan:** las reemplazadas y las de un formulario dado de baja o borrado. Una publicación abierta del formulario suspende esa respuesta: si falla, la versión activa sigue en uso;
- WhatsApp espera a que no queden sesiones vigentes ni respuestas recibidas a tiempo sin capturar. Nunca lo deduce porque no ve sesiones: lo comprueba bajo el bloqueo del binding;
- cada binding se deprecia con su reserva, su número de turno y su estado. Lo incierto queda «Sin confirmar» y se consulta por id, nombre técnico y WABA antes de repetir. Todo 4xx también se consulta;
- cada respuesta de Meta se clasifica en definitiva, transitoria o desconocida. Un rechazo no se repite para siempre: después de esperar 5, 15, 30 y 60 minutos queda «No se pudo», a la vista, y no frena nada del formulario;
- corre por organización, como las difusiones, sin una persona detrás: el último envío puede vencer días después de la baja, cuando quien la hizo ya no tiene permisos.

**La lista «Formularios»** (79): el filtro «Estado» suma «Dados de baja», junto a Activos (publicados y borradores), Publicados y Borradores. No es una vista aparte. Quien solo ve respuestas también lo tiene, porque si no, no llegaría a las respuestas de un dado de baja.

## Pantallas

Se dibujaron antes de las reglas del 25 y el 26/10, y Diseño los revisa con esta etapa, con el [texto de la etapa](17-texto-diseno.md). `BA5` va como recorrido de la guía. Muestran solo lo que existe hasta acá (78).

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Formularios-17` y `M-FRM-Formularios-17` | la lista, con «Dados de baja» y el ⋮ de cada fila (`BA1-A`) |
| `FRM-Editor-Baja` y `M-FRM-Editor-Baja` | el formulario dado de baja: el aviso, lo que pasa en WhatsApp, «Reactivar» en el lugar de «Publicar» y el ⋮ (`BA2`) |
| `FRM-Dar-De-Baja` | el diálogo: qué deja de pasar, los envíos abiertos, WhatsApp y lo que se conserva (`BA3`) |
| `FRM-Reactivar-Eliminar` | reactivar, con o sin cambios o con algo que corregir, y borrar (`BA4`) |
| `FRM-Copiar` | el diálogo, con el nombre y el prefijo propuesto |

**Lo que se corrige en lo dibujado:**
- **«Eliminar» pasa a «Borrar»** en todos: es regla del sistema. El nombre del tablero queda.
- **Sale lo de etapas posteriores:** el asistente, las sugerencias, las fotos, el PDF y las aprobaciones, en `FRM-Dar-De-Baja`, `FRM-Editor-Baja`, `M-FRM-Editor-Baja` y `FRM-Reactivar-Eliminar`.
- **«Quien solo ve respuestas»** suma el filtro «Estado», con «Activos» y «Dados de baja».

**Estados dibujados:** la lista con sus filtros y las acciones de un publicado, un borrador, un dado de baja y mientras se publica; «Solo puede publicar», «Solo lectura» y «Quien solo ve respuestas». El dado de baja, ya deprecado, sin confirmar y «No se pudo deprecar». Los diálogos, con y sin envíos abiertos, mientras trabajan y con error. Copiar, sin nombre y con el prefijo ocupado.

**Mandan sobre esta spec en:**
- **Una acción que no se puede usar** queda grisada, con el motivo en su tooltip, también en el ⋮ (43). Sin el permiso, no se dibuja.
- **Si no se sabe si la baja o el borrado salió,** la pantalla vuelve a leer sola, sin pisar lo escrito (43).
- **Lo dado de baja** es una opción del filtro «Estado», no una vista aparte.
- **Las fichas del front:** quién y cuándo, cada uno en su lugar, con fecha y hora completas, y sin rótulos que expliquen.
- **Copiar** abre el editor del formulario nuevo.

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Van en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /{id}/lifecycle` | `forms.read` | — | `{openSends, lastExpiresAtUtc?, retirement?{state, deprecatedAtUtc?}}`: lo que muestran los diálogos, leído al abrirlos |
| `POST /{id}/deactivation` | `forms.publish` | — | 200 `{deactivatedAtUtc}` |
| `DELETE /{id}` | `forms.manage` | `version` | 204 |
| `GET /{id}/copy-proposal` | `forms.manage` | — | 200 `{name, prefix}` |
| `POST /{id}/copies` | `forms.manage` | `{name, prefix}`, con `[Idempotent]` | 201 `FormDetail` + Location |

`retirement.state` es `Pending`, `Uncertain`, `Deprecated` o `NeedsAttention`; `null` si nunca hubo un Flow. Entre varias versiones prima `NeedsAttention`, después `Uncertain` y después `Pending`.

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| `GET /` y `GET /filter-counts` | `status` suma `deactivated`, y `all` son los activos. Cada fila suma `deactivatedAtUtc?`, y si está dado de baja, `openSends` y `retirement?` |
| `GET /overview` | suma `status` (`active`, `deactivated`), y cada fila, `deactivatedAtUtc?` |
| `GET /{id}` | suma `deactivated?{byName, atUtc}` y `retirement?` |
| `POST /{id}/publication-checks` y `POST /{id}/publications` | sobre un dado de baja, `NothingToPublish` no aplica. Publicar sobre un dado de baja es reactivar |
| `GET /{id}/publications/{publicationId}` | sin cambios: reactivar se sigue igual |

**Puertos** (en `Application/Interfaces/Integrations/StructuredData/`):

```csharp
// WhatsApp → Forms (suma a IStructuredFormSource, implementa Forms)
Task<IReadOnlyList<Guid>> FilterRetiredAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken ct);
// Forms → WhatsApp (suma a IStructuredFormChannel, implementa WhatsApp)
Task<IReadOnlyList<StructuredRetirementState>> GetRetirementAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken ct);
```

`StructuredRetirementState(VersionId, OpenSends, LastExpiresAtUtc?, State, DeprecatedAtUtc?)`. Sin WhatsApp devuelve una lista vacía; sin Forms, ninguna versión está retirada y no se depreca nada. Las dos se leen fuera de toda transacción.

**Errores** (en `Domain/Forms/FormDefinitionErrors.cs`, con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Definition.AlreadyDeactivated` | Conflict | ya está dado de baja |
| `Forms.Definition.NotPublished` | Conflict | dar de baja uno sin versión activa |
| `Forms.Definition.HasRecords` | Conflict | borrar uno con respuestas |
| `Forms.Definition.HasOpenSends` | Conflict | borrar con envíos abiertos o respuestas en proceso |

`Forms.Publication.InProgress` vale también para dar de baja y borrar. En copiar valen los de la E14: `Forms.Definition.PrefixTaken` y `PrefixInvalid`. Borrar con otra versión da `General.ConcurrencyConflict`, y un formulario borrado responde `Forms.Definition.NotFound`.

## Datos

Una migración, `<fecha>_FormLifecycle.cs`, aditiva.

| Tabla | Columna | Tipo | Regla |
| --- | --- | --- | --- |
| `FormDefinitionStates` | `DeactivatedAtUtc`, `DeactivatedByUserId` | timestamptz?, uuid? | van acá por T7: dar de baja no le da un 409 falso a quien edita. Reactivar los limpia en la UoW que activa la versión |
| `FormDefinitions` | `DeletedAtUtc`, `DeletedByUserId` | timestamptz?, uuid? | `ISoftDeletable`. El índice único del prefijo no se filtra: queda reservado |
| `WhatsAppFlowBindings` | `DeprecationState` | texto? | `Pending`, `Uncertain`, `Deprecated` o `NeedsAttention` |
| `WhatsAppFlowBindings` | `DeprecationInFlight` | bool | se commitea antes de llamar a Meta |
| `WhatsAppFlowBindings` | `DeprecationFence` | bigint | el número de turno de cada reserva |
| `WhatsAppFlowBindings` | `DeprecationLeaseExpiresAtUtc`, `DeprecationNextCheckAtUtc`, `DeprecatedAtUtc` | timestamptz? | — |
| `WhatsAppFlowBindings` | `DeprecationFailureCount` | int | elige la espera: 5, 15, 30 y 60 minutos |

Índice parcial en `WhatsAppFlowBindings` sobre `(TenantId, DeprecationNextCheckAtUtc)`, solo lo publicado y sin deprecar, para que el worker no recorra lo que ya terminó. La baja no pasa por el interceptor de auditoría, así que el servicio la registra con `IAuditLog`.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front; `M/`, la maqueta, y `[E14]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Columnas, dominio y errores | suma a `D/Forms/FormDefinition.cs`, `FormDefinitionState.cs` y `FormDefinitionErrors.cs` [E14]; `I/…/Configurations/`; la migración | `D/Assistant/AssistantOrganizationSettings.cs` | transiciones; prefijo reservado después de borrar; hacia atrás |
| B2 | El filtro de disponibilidad, uno solo | `A/Modules/Forms/Services/FormAvailabilityPolicy.cs`; suma a `FormStructuredSource.cs` y `IFormReader.cs` [E14] | `FormCatalogFiltersPolicy.cs` [E12] | dado de baja y borrado no se ofrecen; la captura de un envío abierto se guarda |
| B3 | Dar de baja, bajo el bloqueo de la fila de estado, con `IAuditLog` | suma a `FormDefinitionService.cs` [E14] | `FormPublicationService.cs` [E14] | publicación abierta; dos a la vez; un editor abierto no recibe 409 |
| B4 | Reactivar | suma a `FormPublicationService.cs` y `FormVersionCreator.cs` [E14] | esos mismos | sin cambios publica igual; sigue de baja hasta confirmar; si falla, sigue de baja; numeración continua |
| B5 | Borrar: lo del canal se lee antes de la UoW, y lo de Forms se comprueba bajo el bloqueo | suma a `FormDefinitionService.cs` | esos mismos | con respuestas, con envíos, con publicación; borrador y publicado; `usedByCount` y `FieldInUse` no cuentan lo borrado; un envío que se cruza queda `Rejected`, a la vista |
| B6 | Copiar | `A/Modules/Forms/Services/FormDefinitionCopier.cs`; suma a `FormPrefixResolver.cs` [E14] | `FormVersionCreator.cs` [E14] | identidades, catálogos, presentación y filtros iguales; prefijo ocupado; idempotencia; de un dado de baja; sin versiones ni respuestas |
| B7 | Decir qué versiones ya no se usan | suma a `FormStructuredSource.cs` e `IFormReader.cs` | `FormStructuredSource.cs` [E14] | reemplazada, de baja y borrada; la activa no; una publicación abierta suspende |
| B8 | Binding: reservar, marcar, registrar y verificar | suma a `D/WhatsApp/WhatsAppFlowBinding.cs` [E14]; `WhatsAppFlowBindingErrors.cs` | las transiciones de publicación del binding [E14] | un turno viejo no escribe; `InFlight` se vuelve «Sin confirmar»; las cuatro esperas |
| B9 | La deprecación en sus fases, cada una con una UoW | `A/Modules/WhatsApp/Interfaces/Services/IWhatsAppFlowDeprecationService.cs`, `WhatsAppFlowDeprecationService.cs` | `IWhatsAppFlowPublicationService.cs` [E14] | sesión vigente; respuesta a tiempo sin capturar; otra WABA; sin token; el token no entra en la identidad |
| B10 | Hablar con Meta | suma a `I/Modules/WhatsApp/WhatsAppFlowClient.cs` [E14] | el mismo | 2xx, 4xx, 5xx, 429, timeout y cuerpo ilegible; clasificación; ya deprecado |
| B11 | El worker, por organización | `I/Modules/WhatsApp/WhatsAppFlowDeprecationWorker.cs` | `WhatsAppBroadcastWorker.cs` y `TenantJobRunner.cs` | una organización con falla no frena a las demás; con el reloj de prueba al vencer |
| B12 | Rutas y puertos | suma a `P/Modules/Forms/Controllers/BusinessFormsController.cs`, `Contracts/*`, `FormsContractContributor.cs`, los apagados de `I/StructuredData/` y `WhatsAppStructuredFormChannel.cs` | esos mismos [E14] | cada ruta con 401, 403, feature apagada = 404, 409; dos permisos distintos; ejemplos exportados; aislamiento |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` | `M/…/api/forms.ts` y `copies.ts` | zod estricto con los ejemplos |
| F2 | La lista: filtro y ⋮ | suma a la lista [E14] | `M/…/pages/FormsOverviewPage.tsx` | las acciones por estado y permiso; Borrar no aparece con respuestas; quien solo ve respuestas |
| F3 | Los diálogos | `components/LifecycleDialogs.tsx`, sin sugerencias ni reglas | `M/…` | releer si no se sabe si salió; error en el diálogo; botones según la 43 |
| F4 | Copiar | `components/CopyFormDialog.tsx` | `M/…` | prefijo ocupado en su campo; abre el editor |
| F5 | El formulario dado de baja | suma al editor [E14]: el aviso, «Reactivar» y el ⋮ | `M/…/pages/FormEditorPage.tsx` | los cuatro estados de WhatsApp; se edita y se guarda |

**Al cerrar:** `docs/features/formularios.md` suma la sección, y `docs/features/whatsapp.md`, la deprecación. `AGENTS.md` de la carpeta del módulo nombra el worker.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | La fila de estado y el flujo de publicar. | `CompleteAsync` activa la versión en una UoW, y ahí se limpia la baja. |
| [E14] | Las transiciones de `WhatsAppFlowBinding` y las rutas de sesión. | Se puede saber si hay sesiones `Pending` vigentes y respuestas sin capturar. |
| [E14] | Cómo se lee una sesión al capturar. | Un formulario dado de baja se captura, y uno borrado se rechaza. |
| [E15] | Las dos rutas de la lista. | `GET /` y `GET /overview`, con sus filtros en una sola función. |
| [E16] | Dónde viven los filtros de una pregunta. | En el borrador, así que se copian solos. |
| [Meta] | `POST {flow}/deprecate` y el estado `DEPRECATED`, contra la documentación vigente. | El Flow tiene que estar publicado, y un Flow deprecado no vuelve. |
| #51 | `Enter` borra `TenantKind`. | El worker no consulta features ni permisos: `TenantJobRunner` alcanza. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Un 4xx al deprecar, leído como «sin efecto»,** repetía el POST cada pocos segundos, sin que se viera (R1021a #1). Todo 4xx se consulta antes de repetir.
2. **Definiciones borradas** rompían una lectura o se contaban (#2, #3 y #12).
3. **Falta de invalidación** después de borrar o dar de baja (#4 y #7): el front invalida la lista, el formulario y el chat.
4. **El texto enviado** quedaba en la caché de mutaciones (#14).

## Decisiones tomadas (no se vuelven a preguntar)

- **El ciclo de vida entra al bloque** como E17 (34).
- **Permisos:** dar de baja y reactivar piden `forms.publish`; borrar y copiar, `forms.manage`.
- **La deprecación es por WABA de la organización,** con su token.
- **No se borran respuestas desde un formulario.**
- **Reactivar no tiene ruta propia:** es publicar.
- **El worker no entra con `ExecutionActor`:** deprecar no depende de los permisos de una persona, que pueden cambiar antes de que venza el último envío.
- **La cola es de WhatsApp,** sin un registro global: Forms contesta qué versiones ya no se usan.

## Fuera de esta etapa

- Borrar respuestas por antigüedad: rige la decisión 23.
- Lo que las etapas siguientes suman a la copia, al filtro de disponibilidad y al diálogo de dar de baja.
- Un botón para reintentar lo que quedó «No se pudo».

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba y `WhatsApp:FlowSessionHours` en 1:
1. dar de baja un formulario con un envío abierto, y responder ese envío;
2. al vencer, ver los Flows deprecados en el administrador de Meta;
3. reactivar: versión nueva, Flow nuevo y el número de respuesta siguiente;
4. borrar un formulario en borrador, y borrar uno publicado sin respuestas, que también deprecia sus Flows.

El recorrido suma copiar un formulario con un catálogo y un filtro, y publicar la copia.

## Requisitos previos

La E16 cerrada.

## En el multitenant

- **Datos de la organización:** la baja y el borrado van en tablas `ITenantOwned` con RLS.
- **Workers:** el de deprecación es de WhatsApp y entra por organización con `TenantJobRunner`.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** sin permisos nuevos.
- **WhatsApp (E11):** la deprecación usa la WABA y el token de cada organización.
- **Cuotas:** las rutas cuentan en las cuotas generales.
