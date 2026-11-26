# Acciones del asistente · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/acciones-del-asistente.md` (contrato, datos y pasos con sus tests). Va después de la puerta de la segunda parte de la base, que cerró el 2026-11-26 (el usuario decidió arrancar sin esperar el recorrido de sus cuatro pasos nuevos de la I3), a la vez que Costuras de módulos y antes de la E12 ([ADR 0068](../decisions/0068-acciones-del-asistente.md); decisión 268). Las decisiones de producto las tomó el usuario el 2026-11-23: el lugar, el alcance (Usuarios y Roles enteros) y que se ofrezcan calificadas antes de la prueba integral. Los diez tableros de la spec («Pantallas») están en el lienzo v179, aprobados por el usuario el 2026-11-23.

**Fuente:** la ADR 0068, sus dos revisiones adversariales del 2026-11-23 y la sección «Acciones del asistente» de ArquitecturaBase (`docs/plans/2026-11-19-procesos-del-campo.md`, solo para leer).

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B8 | Pendiente |
| Front | F1–F5 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: primero el contrato; después, a la vez, el back en carriles y un chat del front; una sola puerta al final ([etapa en paralelo](../guides/etapa-en-paralelo.md)). Cada tarea lleva su test dirigido (de integración, filtrado por su clase, si lo es) y, al terminar, el build y los tests rápidos enteros de su repo, sin Docker y sin filtrar por módulo, con `node scripts/arneses/verificar.mjs` en el back y `node scripts/verificar.mjs` en el front. Los de integración y de seguridad de las áreas que tocan rutas, persistencia, autenticación o aislamiento corren una vez por carril, al cerrarlo ([0071](../decisions/0071-conductor-en-carriles.md)). Un arreglo corre solo lo que toca.

Todo va en `main`, sin ramas, worktrees, stash, reset ni push. Cada chat commitea solo sus rutas con `git commit -- <rutas>`, con la línea `Tarea: acciones-del-asistente/<id>`. Con Vite levantado nadie edita el `src` del front. La tabla de estado la actualiza el cierre del conductor ([conducir una etapa](../guides/conducir-una-etapa.md)).

**El back va en carriles** ([0071](../decisions/0071-conductor-en-carriles.md)). Las dos migraciones, la de la B4 (`OfferedActions` del turno) y la de la B5 (la propuesta), van en ese orden y en el mismo carril, armadas sobre el `HEAD` del momento y con `ModelSnapshotTests` en verde.

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md), «Forma de trabajo»). La llevan las tareas marcadas con **(R)**, completa y antes del commit: un subagente nuevo recibe el código y los escenarios, sin la conclusión de quien programó. Los escenarios de cada una están en la ADR 0068 («Modelo de amenazas»).

**Reglas de orden:**
- K0 y K1 van antes que todo, y nada corre si K1 no quedó hecha.
- B4 espera a B2; B5, a B4; B6 y B7, a B1, B3 y B5; B8, a B6 y B7.
- F1 arranca con K1. F2, F3 y F4 siguen sus tableros, aprobados en el lienzo v179; F3 y F4 esperan además a F1. F5 va al final del front.
- I1 espera a B8 y a F5; I2, a I1; I3, a I2.

## Qué se verificó al arrancar

La segunda parte de la base ya programó casi todo; faltan la B18, la F12 y la F13, que esta etapa no usa. Lo que usa de ella lo confirma K0 contra el código. Lo demás se miró en el código el 2026-11-23, y la base 2, el 2026-11-25.

| Fila | Qué se encontró | Qué cambia |
| --- | --- | --- |
| Herramientas | 19 lecturas con el actor firmado. Releen los permisos antes y después, y reciben servicios enteros que también escriben | B2: el alcance de solo lectura |
| El turno | Un error de una herramienta corta el turno (`AssistantTurnWorker`). Un dato desconocido falla cerrado (`AssistantTurnToolBuilder`) | B4: el rechazo vuelve como dato |
| La transacción | `ExecuteToolAsync` corre la herramienta y después una UoW en el mismo contexto de EF, que guarda todo lo que quedó pendiente | B2: un alcance aparte y descartado |
| RLS público | `EnablePublicRls` deja ver lo publicado de todas las organizaciones | B6 y B7: lo público, filtrado por el claim |
| `Resolver` | Hay `Resolver` con repositorios y escritores (`GoogleAccountResolver`) | B1: la lista de lo permitido se mira en todo el árbol |
| Usuarios | Invitar es `POST` con `[Idempotent]`; editar y quitar son `PUT` y `DELETE` con versión, y `UpdateOrganizationUserRequest` reemplaza listas enteras | B6 y F3: operaciones sobre lo leído |
| Roles | Crear es `POST` con `[Idempotent]`; editar y dar de baja, con versión. Alcance y empresa no cambian en la pantalla (`RoleScopeField` en solo lectura), aunque la ruta lo admite sin personas | B7 y F4: el adaptador manda los leídos |
| La 0061 | Programada en la B1 de la segunda parte: las `Policy` son puras, y lo que tiene quien pide se lee en `OrganizationDelegationGuard` | B3 saca esa lectura del `Guard` |
| Lienzo | Los diez tableros de la spec, aprobados en el lienzo v179 (página «Organización · Acciones del asistente») | F2 a F4 los siguen |

## Contrato

### K0 · Lo que dejó la segunda parte de la base

El chat del contrato confirma contra el código y la tabla de la puerta de la segunda parte:
- **la puerta cerrada;**
- **`OrganizationDelegationGuard` y `CompanyDelegationGuard`,** con sus `Policy`, y los 403 de la 0061 en invitar, editar y roles;
- **lo incluido (260)** con `Permissions.Inclusions` (`Effective` y `Of`), en la proyección efectiva y en `GET /api/permissions`;
- **`grantable`,** lo que el front recibe para apagar los roles y permisos que no se pueden dar (269), y **la F5:** `PermissionPicker` y `RoleEditorPage` con lo incluido y lo que no se puede dar apagados;
- **`Picker`** con `selectedOptions`, que muestra lo elegido que no está en la página cargada;
- **la B9:** `CoreTextsNeutralTests` y `ModelSnapshotTests`; **la B14:** `AssistantTurnWork.CurrentEngineVersion` (1) y su filtro en `AssistantTurnRepository`.

Lo que difiera va a «Cambios al contrato» antes de programar.

### K1 · Publicar el contrato

El contrato completo está en la spec (§Contrato). En resumen:
- **las tres rutas de la propuesta,** con `AssistantProposalResponse` y `{idempotencyKey}`, respondiendo 501 hasta la B5;
- **`proposal?`** en el `Message` del historial y del SSE;
- **los cuatro códigos `Assistant.Proposal.*`** con sus textos es y en, y los de `AssistantActionToolData`;
- **los modelos del núcleo** (`AssistantActionDefinition`, `AssistantActionRequirement`, `AssistantActionField` y sus enums) en `Application/Models/AssistantActions/`, que entra en `docs/architecture/arbol.md` con su fila del mapa y su `AGENTS.md`, y el puerto `IAssistantActionSource`, sin fuentes todavía;
- **las seis declaraciones,** estáticas, en `OrganizationUserActionSource` y `RoleActionSource`, registradas como `IAssistantActionSource` en `OrganizationUsersRegistration.cs` y `RolesRegistration.cs` (como `IPendingItemSource` en `OrganizationSettingsRegistration.cs`), con sus textos `Action.*` en `Application/Resources/Actions.resx` y `.en.resx` y su lector `ActionTexts.cs`, como `PermissionTexts.cs`, que no nombran al asistente (`CoreTextsNeutralTests`, B9 de la base 2). Su resolución y su validación responden «no disponible» hasta la B6 y la B7. Como son los primeros helpers con ese sufijo, la fila `ActionSource` entra en la tabla de capas-y-flujo en el mismo commit (`ApplicationServicesTests`);
- **el generador** de `docs/contracts/assistant-actions.json` en el build;
- **`AssistantActionScreen`,** la lista cerrada de pantallas del núcleo, con su traducción a `AssistantRouteKey` en el módulo.

El back genera `openapi.json` y `assistant-actions.json`, y el front corre `npm run contracts`. No hay permiso nuevo.

### Decisiones técnicas del contrato

- **Las seis declaraciones viven con su área** (`OrganizationUserActionSource` y `RoleActionSource`), no en el asistente. El núcleo no nombra al módulo.
- **`attempts` va sin `[Idempotent]`:** repetirlo devuelve la misma clave, y responde 200, así que `IdempotentActionsTests` no lo exige; el motivo va en un comentario de la acción.
- **«No disponible» y «Sin confirmar» se calculan en cada lectura** y no se guardan: dependen de los permisos de ese momento y de las claves de la propuesta.
- **La lista de calificadas** es un archivo del módulo, vacío; los tests cargan la suya desde `TestFeatures`.
- **Los nombres que muestra la tarjeta** salen de la fuente al leer, y nunca se guardan en la propuesta.

**Texto para el chat de Contrato**

```text
Leé AGENTS.md, la ADR docs/decisions/0068-acciones-del-asistente.md, este plan y la spec docs/plans/formularios/acciones-del-asistente.md. Hacé K0 y después K1, sin adelantar lógica: los modelos del núcleo y el puerto IAssistantActionSource, las seis declaraciones estáticas con sus textos Action.* es/en en Actions.resx, AssistantActionScreen, las tres rutas de la propuesta con 501, proposal? en Message, los códigos Assistant.Proposal.* con sus textos, el generador de docs/contracts/assistant-actions.json y la fila ActionSource en la tabla de sufijos de docs/rules/capas-y-flujo.md (las dos fuentes son sus primeros helpers). No toques entidades, configuraciones de EF ni migraciones. Generá openapi.json y avisá a la Guía para que el front corra npm run contracts. Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo tuyo con git commit -- <rutas> y la línea Tarea: acciones-del-asistente/<id>. Aplicá capas-y-flujo, api-http, permisos, result-y-errores, textos y tests. Al terminar: node scripts/arneses/verificar.mjs (build y tests rápidos enteros, sin filtrar por módulo); como suman rutas, su test dirigido son las guardas de rutas: node scripts/arneses/verificar.mjs --integracion --clase ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts.ExplicitRouteInventoryTests --clase ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy.RouteIsolationMatrixTests.
```

## Back

Los pasos, con sus archivos, lo que copian y sus tests, están en la spec (§Pasos). Valen:
- `Result` y ProblemDetails, `OperationLog.RunAsync`, `LoggerMessage` y `TimeProvider`;
- una UoW por escritura, RLS, auditoría y textos es y en;
- el asistente sigue siendo quitable: el núcleo no lo nombra;
- ninguna fuente ni herramienta escribe.

1. **B1 · El núcleo y sus tests de arquitectura.**
   - **Qué lleva:** los endurecimientos de la 0068: `FromSqlRaw`, `ExecuteSqlRaw` y `SqlQueryRaw` en `BannedSymbols.txt`, que vale también para los tests, y `RuntimeRoleTests` en todos los esquemas. Todos los usos de `ExecuteSqlRaw` en los tests pasan a `ExecuteSqlAsync` interpolado: hoy son ocho en seis archivos (`AccountDeletionParticipantsTests`, `AssistantEditableCatalogMigrationTests`, dos en `PlatformOperatorAssistantUpgradeTests`, dos en `AiKeyRingControlTests`, `AssistantUnreadableConversationTests` y `AssistantUnreadableConversationAdversarialTests`), y K0 los vuelve a contar.
   - **`AssistantActionDeclarationTests`,** en `ArchitectureTests/`, porque las declaraciones son del núcleo; la parte que mira la traducción a `AssistantRouteKey` va en `ArchitectureTests/Modules/Assistant/`, que sale con el módulo:
     - el id con versión, único;
     - el requisito igual al atributo de la `SaveRoute`, leído por reflexión, con `[Access]` y `organizationPermission`;
     - la ruta escribe y, si crea, es `[Idempotent]`;
     - los campos de la lista cerrada, sin nombres prohibidos, y un `Items` con un solo nivel, su tope de filas y su clave;
     - `Company` con su campo, y `Remove` con `RemoveConfirmation`;
     - ninguna acción guarda por una ruta de la lista de lo que no se declara;
     - cada `AssistantActionScreen` con su traducción a `AssistantRouteKey`;
     - los textos y los casos;
     - el árbol de dependencias de cada fuente, contra la lista de lo permitido, también a través de la implementación de cada puerto lector del núcleo (`I<X>Reader`), con un detector probado con una clase de prueba.
2. **B2 · El alcance de solo lectura (R: aislamiento y datos).**
   - **Qué hace:** el puerto `IAssistantReadOnlyScope` (`Application/Modules/Assistant/Interfaces/Integrations/`, implementado en `Infrastructure/Modules/Assistant/`) abre un alcance de DI propio. Entra con el actor revalidado (en el worker) o con el tenant y el usuario del claim (en la petición), nunca con `ITenantScope.Enter`. Abre la transacción `READ ONLY` y descarta el alcance al terminar.
   - **Dónde corre:** el ejecutor de herramientas y las fuentes corren adentro; las 19 lecturas, sin cambios de código.
   - **Tests:**
     - una fuente de prueba que llama a un repositorio y guarda no deja filas;
     - el tenant es el correcto en el worker y en una consulta rápida;
     - un lector de otra organización da vacío;
     - las lecturas de hoy siguen verdes.
3. **B3 · Las `Policy` de la 0061 y lo que tiene quien pide (R: permisos).** `OrganizationDelegationPolicy` y `CompanyDelegationPolicy` ya son puras (B1 de la base 2), pero lo que tiene quien pide lo lee el `Guard`, y una fuente no puede depender de un `Guard` (0068). `ReadHeldAsync` y `ReadAssignedAsync` pasan de `OrganizationDelegationGuard` a `Application/Services/Roles/DelegationPermissionsResolver.cs` (sufijo `Resolver`, solo con `ICurrentUser` e `IRoleReader`), que usan los dos `Guard`, `RoleService` y las fuentes, sin cambiar la conducta de la ruta. **Tests:** `DelegatedAdministrationTests` sin cambios; cada `Policy` y su `Guard` deciden igual, en la organización y en una empresa, y el `Guard` y el resolvedor leen lo mismo.
4. **B4 · El registro y las tres piezas (R: datos, por la migración; aislamiento y permisos; efectos, por lo que va al proveedor de IA).**
   - **El registro:** `AssistantActionCatalog` filtra por acceso, módulo, requisito (en su empresa), calificación y modelo del turno, con una lectura de permisos por llamada. `Application/Modules/Assistant/Actions/` entra en `arbol.md`.
   - **El turno:** sella la lista en `AssistantTurns.OfferedActions` (jsonb, nulo), con su migración. Un turno que ofrece acciones se sella con `EngineVersion` 2 (`AssistantTurnWork.CurrentEngineVersion` pasa a 2; uno sin acciones sigue en 1): una copia anterior no lo toma (0064 §6). La lista precargada va en `AssistantTurnPreparationBuilder` como dato, con las áreas si son más de 40.
   - **Las herramientas:** `read_action` y `propose_action` corren en el alcance de B2. `AssistantActionToolData` entra en el `switch` cerrado, con sujetos vacíos probados.
   - **El rechazo** vuelve como dato y no corta el turno; también una segunda propuesta.
   - **Lo armado como dato:** una acción puede declarar que sus campos salen de la definición de su destino (las opciones de un catálogo, E12); `read_action` con el destino los da con su tipo y sus límites. La batería lo prueba con una declaración de ejemplo.
   - **Tests:**
     - sin calificación no se ofrece;
     - con la de prueba del modelo falso sí;
     - una acción fuera del sello se rechaza;
     - lo que va al proveedor no trae registros ni personas;
     - un turno con acciones queda con la versión 2, y uno sin acciones, con la 1;
     - `ModelSnapshotTests` en verde.
5. **B5 · La propuesta (R: datos y aislamiento, con migración).**
   - **Los datos:** `AssistantProposal` y su configuración, con RLS forzado y la migración aditiva. Una nueva reemplaza a la anterior sin aplicar de su clase.
   - **Los sujetos y el borrado:** los sujetos van al mensaje, y la propuesta se borra con la conversación, la retención y la baja. Suma su lugar en el inventario del módulo (`AssistantModelContributor`). No entra en `PersonalDataPurgeTests`, que mira solo el modelo del núcleo (0062).
   - **La historia del turno siguiente** (`AssistantTurnWriter`): el mensaje con una propuesta suma la acción y lo que escribió el modelo, nunca lo resuelto ni los nombres (0068 §7; 0064 §9 y §10). El pedido lleva la conversación entera (si no entra, `Assistant.Provider.ContextTooLarge`), así que la última propuesta nunca queda afuera.
   - **Las tres rutas:**
     - el `GET` vuelve a resolver todo con la fuente;
     - `attempts` lleva la clave por intento, y con una clave que ya terminó en 2xx responde `AlreadyApplied`;
     - `applied` se comprueba con `IIdempotencyStore.FindAsync`, que se suma.
   - **«Sin confirmar»** se calcula al leer: hay claves, ninguna terminó en 2xx y pasó la validez de la clave (`IdempotencyOptions.ValidityHours`, 24 h de entrada) desde la última. Vencida, `IdempotencyStore` la borra y la vuelve a reservar, y otro guardado ya no sería único.
   - **Tests:**
     - la propuesta de otra persona o de otra organización da 404;
     - cada estado y cada motivo de «no disponible»;
     - dos pestañas usan la misma clave;
     - después de un 4xx sale otra, y después de un 2xx, `AlreadyApplied`;
     - aplicada sin un 2xx da 409;
     - la retención de las conversaciones del Inicio y la baja de la cuenta borran la propuesta;
     - el pedido del turno siguiente trae la propuesta, sin ids resueltos ni nombres;
     - `ModelSnapshotTests` en verde.
6. **B6 · Usuarios (R: permisos y efectos fuera, por la invitación).** `OrganizationUserActionSource`, con `users.invite@1`, `users.edit@1` y `users.remove@1`.
   - **Resolver:** resuelve la empresa, los roles y la persona por texto, con sus readers, la `Policy` de B3 y el permiso en esa empresa.
   - **Validar:** valida armando `InviteOrganizationUserRequest` y `UpdateOrganizationUserRequest` con sus validadores.
   - **El efecto:** `users.invite@1` declara `SendsMessage`.
   - **Tests:** los escenarios de la batería para las tres.
7. **B7 · Roles (R: permisos).** `RoleActionSource`, con `roles.create@1`, `roles.edit@1` y `roles.remove@1`.
   - **Qué propone:** el alcance y la empresa al crear; al editar, sumar y quitar permisos.
   - **Los permisos:** solo los del catálogo de ese alcance que quien pide tiene, con lo incluido. Quitar un permiso que sigue incluido por otro que queda (260, `Permissions.Inclusions.Of`) se rechaza con su ruta (`Invalid`) y vuelve al modelo.
   - **Tests:** los escenarios de la batería para las tres, también quitar un permiso incluido.
8. **B8 · La batería genérica.** `AssistantActionContractTests`, una teoría por acción registrada, con los escenarios de `TestFeatures/AssistantActions/` (la organización, quién pide, el destino, otra empresa, otra organización y el cuerpo HTTP del caso válido). Prueba la lista de la ADR 0068 («Verificación»), y `assistant-actions.json` queda al día con el build. `TestFeatures/AssistantActions/` entra en `arbol.md` y en el paso 5 de `docs/guides/quitar-asistente.md`.

**Texto para el chat del Back**

```text
Leé AGENTS.md, la ADR docs/decisions/0068-acciones-del-asistente.md, este plan y la spec docs/plans/formularios/acciones-del-asistente.md. Esperá K1. Cada tarea respeta las «Reglas de orden»; las migraciones son la de B4 y la de B5, en ese orden y en el mismo carril. Las tareas con (R) llevan su revisión adversarial completa antes del commit, con los escenarios del «Modelo de amenazas» de la 0068. Ninguna fuente ni herramienta escribe: corren en el alcance de solo lectura de B2. Sin proveedores reales ni secretos: el modelo es el FakeChatClient con su calificación de prueba. Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo tuyo con git commit -- <rutas> y la línea Tarea: acciones-del-asistente/<id>. Por tarea: el test dirigido y, al terminar, node scripts/arneses/verificar.mjs (build y tests rápidos enteros, sin filtrar por módulo); los de integración y seguridad del asistente, de Usuarios y de Roles corren una vez, al cerrar el carril (ADR 0071).
```

## Front

Las tareas siguen las fichas del front (`formatos.md`, `pantallas-y-ui.md`, `docs/architecture/tema.md`, `accesibilidad.md` y `formularios.md`), con i18n, tipos generados y axe donde cambia una pantalla. Los pasos están en la spec. El módulo del asistente no importa áreas, y las áreas no importan el módulo: se hablan por `src/shared/assistant-actions/`.

1. **F1 · El registro y el hook.** `src/shared/assistant-actions/`, que entra en el `arbol.md` del front: el registro de adaptadores por acción y `useAssistantProposal`. «Lo que cambia» muestra también las filas de un `Items` que se suman, se quitan o cambian.
   - **El hook:** lee la propuesta por id, pone «Propuesta del asistente» y «Revisá», y aplica las operaciones sobre lo leído.
   - **Al crear:** pide la clave de cada intento a `attempts`. Ante un 422 con una clave de la propuesta vuelve a llamar a `attempts`: un 409 `AlreadyApplied` con `location` es «Ya se guardó», y una clave nueva es otro intento. Después de un 2xx, llama a `applied`.
   - **Tests:** del hook, de los límites del módulo y del 422 que se guardó y el que no.
2. **F2 · La tarjeta.** `AssistantActionCard` en el hilo, con sus seis estados y el botón rojo contorneado para quitar y dar de baja. Abre la pantalla de la acción con `?proposal=<id>`, y el diálogo, sobre el Inicio. Su tablero está aprobado (v179). **Tests:** cada estado, teclado y axe.
3. **F3 · Los adaptadores de Usuarios.**
   - **Las pantallas:** `InviteUserDialog`, con el aviso de que manda la invitación; `UserDetailPage`, con «Lo que cambia» y el aviso si cambió; y la confirmación de quitar.
   - **Los selectores** muestran lo propuesto con el `label` que trae la propuesta, como `selectedOptions` de `Picker`, aunque no esté en la página cargada.
   - **Si un valor no entra,** la pantalla no abre la propuesta.
   - Sus tableros están aprobados (v179).
   - **Tests:** todo lo propuesto se ve, y lo que se manda es lo que se ve.
4. **F4 · Los adaptadores de Roles.** `RoleEditorPage`, para crear y para cambiar, con lo incluido apagado y «Lo que cambia», y la confirmación de dar de baja de `RolesPage`. Sus tableros están aprobados (v179). **Tests:** como F3.
5. **F5 · La cobertura.** Un test que lee los tipos generados de `assistant-actions.json` y falla si una acción no tiene adaptador.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, la ADR 0068 del back, este plan y la spec docs/plans/formularios/acciones-del-asistente.md, con las fichas del front y los tableros que nombra la spec. Esperá K1, corré npm run contracts y hacé F1. F2, F3 y F4 siguen sus tableros, aprobados en el lienzo v179; nunca dibujes ni inventes esas pantallas. F5 va al final. El módulo del asistente y las áreas se hablan solo por src/shared/assistant-actions. No toques el lienzo, las specs ni el back, ni el src si Vite está levantado. Usá i18n, shared/ui (Picker), TanStack Query y las reglas de accesibilidad; no escribas tipos generados. Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo tuyo con git commit -- <rutas> y la línea Tarea: acciones-del-asistente/<id>. Por tarea: los tests focales con node scripts/verificar.mjs --archivos <tests> y, al terminar, node scripts/verificar.mjs (tipos, lint, todos los tests y el contrato), sin filtrar por módulo; al cerrar la parte, también npm run build.
```

## Integración y puerta

1. **I1 · Integrar.** El front consume el contrato real y regenera los tipos; una diferencia vuelve a su dueño y se anota abajo. Se suma un escenario del `FakeChatClient` por acción, que propone y deja la tarjeta, para el recorrido y el E2E.
2. **I2 · Puerta.** Se corre `/puerta` una vez, sobre copias del corte:
   - `dotnet build` sin advertencias y `dotnet test` con Docker, con la batería genérica;
   - en el front, build, lint, test y `contracts:check`;
   - el E2E real, con «Invitar usuario» desde una propuesta y «Ya se guardó» en una segunda pestaña;
   - `realinear-docs --detectar` y `estado-docs --detectar` en 0;
   - la galería de los tableros nuevos a 1440, 390 y 320;
   - `/recorrido`.

   No hay prueba con Meta ni con un proveedor real: la calificación va antes de la prueba integral del bloque.
3. **I3 · Recorrido.** Las seis acciones con el modelo falso:
   - invitar con un rol que no se puede dar, que no se resuelve;
   - sumar y quitar un rol a una persona, con «Lo que cambia»;
   - quitar a una persona;
   - crear un rol de empresa;
   - cambiar los permisos de un rol;
   - dar de baja un rol con personas, que la confirmación frena;
   - una tarjeta reemplazada;
   - una tarjeta sin el permiso, que se ve «Ya no disponible»;
   - en el teléfono, la tarjeta y el diálogo.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, la ADR 0068, este plan, la spec y la skill /puerta. Integrá contra K1, devolvé cada diferencia real a su dueño y anotala en «Cambios al contrato». Sumá un escenario del FakeChatClient por acción para el recorrido y el E2E. Corré la puerta una sola vez sobre copias del corte; un arreglo corre solo lo que toca. Sin proveedores reales, sin push y sin tocar el lienzo. Dejá en este plan solo la tabla final corta.
```

## Relación con otras etapas

**Usa de las anteriores:**
- **de la segunda parte de la base:** la 0061 con sus `Guard`, sus `Policy` y `grantable` (B1), lo incluido con `Permissions.Inclusions` (B16), lo apagado en roles (F5 y 269), `Picker` con `selectedOptions` (F11), `CoreTextsNeutralTests` y `ModelSnapshotTests` (B9) y `EngineVersion` (B14);
- **de la E8b:** el asistente del Inicio, `AssistantTurnWorker`, `AssistantTurnToolBuilder`, `AssistantMessage` y sus sujetos, la retención y la baja;
- **del núcleo:** `IIdempotencyStore`, Usuarios y Roles.

**Deja para las siguientes:**
- **la E12 y todo el bloque:**
  - el puerto `IAssistantActionSource` y el sufijo `ActionSource`;
  - el registro y las tres piezas;
  - la batería genérica;
  - `assistant-actions.json`, y en el front `src/shared/assistant-actions/` y la tarjeta.

  Cada etapa que suma una pantalla que crea, edita o elimina declara sus acciones con la [receta](../guides/agregar-una-accion-al-asistente.md). Las opciones de un catálogo se declaran una vez por tipo, en la E12;
- **la E23:** el alcance de solo lectura (`IAssistantReadOnlyScope`, B2), al que suma la entrada del turno de canal, con la organización y sin persona (0068 §5), y `AssistantTurnWork.CurrentEngineVersion` 2 (B4), que la E23 sube para el turno de canal;
- **la E25:** `proposal` en el `Message` y la tabla `AssistantProposals`, a las que suma sus clases (`form`, `catalog` y `catalogImport`), con el reemplazo por clase;
- **antes de la prueba integral del bloque:** la calificación de todas las acciones con un modelo real, con el visto bueno del usuario, que llena `qualified-actions.json`.

**Queda afuera:**
- **Plataforma y Personal;**
- **WhatsApp** (0068 §11);
- **los roles y miembros de una empresa desde su pantalla;**
- **dar de baja o reactivar a una persona.**

## Cambios al contrato

## Para decidir

Nada abierto. Las de producto las tomó el usuario el 2026-11-23 (el lugar, el alcance y cuándo se ofrecen). Lo demás es técnico y está anotado en la ADR 0068 y en «Decisiones técnicas del contrato». Los diez tableros de la spec los aprobó el usuario en el lienzo v179, el 2026-11-23.
