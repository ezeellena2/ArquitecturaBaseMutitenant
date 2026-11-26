# Costuras de módulos · plan ejecutable

**Estado:** pendiente · Spec: [`docs/plans/formularios/costuras-de-modulos.md`](formularios/costuras-de-modulos.md) (contrato, datos y pasos con sus tests). Va a la vez que la etapa de acciones del asistente (decisión del usuario del 2026-11-26: no usa nada de ella), después de la puerta de la base 2 y antes de la E12, sin pantallas ([ADR 0069](../decisions/0069-modulos-y-suscripciones.md)). Las decisiones de producto las tomó el usuario el 2026-11-23, en la 0069. Arma solo las cuatro costuras: el permiso de uso por origen, el resolvedor de topes, apagar sin borrar y la clave del uso de la IA.

**Fuente:** la ADR 0069 y la investigación del 2026-11-23 que resume.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Hecha |
| Back | B1–B7 | Hecha |
| Front | — | Sin tareas |
| Integración y puerta | I1–I2 | Pendiente: I2, el E2E real (46/47) y el recorrido (58/254) se cortan en el Asistente, por e465d7e5 de acciones del asistente |

## Forma de trabajo

Modo rápido: primero el contrato; después el back, en carriles, y ningún chat del front; una sola puerta al final ([etapa en paralelo](../guides/etapa-en-paralelo.md)). El front no programa: la etapa no tiene pantallas y su contrato no cambia. Cada tarea lleva su test dirigido (de integración, filtrado por su clase, si lo es) y, al terminar, el build y los tests rápidos enteros del back con `node scripts/arneses/verificar.mjs`, sin Docker y sin filtrar por módulo. Los de integración y de seguridad de las áreas que tocan rutas, persistencia, autenticación o aislamiento corren una vez por carril, al cerrarlo ([0071](../decisions/0071-conductor-en-carriles.md)). Un arreglo corre solo lo que toca.

Todo va en `main`, sin ramas, worktrees, stash, reset ni push. Cada chat commitea solo sus rutas con `git commit -- <rutas>`, con la línea `Tarea: costuras-de-modulos/<id>`. La tabla de estado la actualiza el cierre del conductor ([conducir una etapa](../guides/conducir-una-etapa.md)).

**El back va en carriles** ([0071](../decisions/0071-conductor-en-carriles.md)); la única migración es la de la B1.

**Código primero** (pedido del usuario del 2026-11-26): lo que este plan y los textos de los chats piden por tarea (los tests rápidos enteros, la integración de las áreas y la revisión adversarial antes del commit) queda sin efecto. Cada tarea hace su código, su test dirigido y el build; la revisión adversarial va una vez por tema de riesgo, en el cierre, y los tests enteros y la integración, en la puerta ([AGENTS.md](../../AGENTS.md)).

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md), «Forma de trabajo»). La llevan las tareas marcadas con **(R)**, completa y antes del commit: un subagente nuevo recibe el código y los escenarios, sin la conclusión de quien programó. Los escenarios de cada una van en su tarea.

**Reglas de orden:**
- K0 y K1 van antes que todo, y nada corre si K1 no quedó hecha.
- B2 espera a B1; B4 y B5, a B3; B6, a B4 y B5; B7, a B2.
- I1 espera a B7; I2, a I1.

## Qué se verificó al arrancar

La segunda parte de la base ya programó casi todo; faltan la B18, la F12 y la F13, y esta etapa usa la B18. Lo que usa de ella y de la etapa de acciones lo confirma K0 contra el código. Lo demás se miró en el código el 2026-11-23.

| Fila | Qué se encontró | Qué cambia |
| --- | --- | --- |
| `TenantFeatures` | Único por `(TenantId, FeatureKey)` (`IX_TenantFeatures_TenantId_FeatureKey`, en `UniqueKeys.TenantFeature`). `Source` es texto de hasta 60: `Plan`, `Manual` o `Trial`, y nada usa `Plan` | B1 |
| Lectores | `TenantFeatureFilter` y `PlatformFeatureListBuilder` buscan la fila con `SingleOrDefault`, y `TenantFeatureRepository.GetForUpdateAsync` también: con dos filas de un módulo, fallan. `WhatsAppModulePolicy` usa `Any` sin el valor de entrada. `FeatureService` y `ProfileModulesBuilder` pasan por el filtro | B2 |
| El writer | `PlatformFeatureWriter` crea la primera fila con versión 0, y la carrera da 409 por el índice. Con el índice por origen, dos altas a la vez con distinto origen dejarían dos filas | B1: un lock por organización y módulo |
| Contrato | `TenantFeatureSource` está en `PlatformFeatureResponse` y en `UpdatePlatformFeatureHttpRequest`, y el validador acepta todo valor definido. Dos valores nuevos cambiarían `openapi.json`, el `EnumText` del front y lo que Plataforma puede escribir | K1: un enum propio del contrato de Plataforma |
| Caché | `t:{id}:features` guarda las filas 5 min, con la etiqueta de módulos; el vencimiento se evalúa al leer | Sin cambio de forma |
| Topes de la IA | `DailyMessageLimit` y `MonthlyOrganizationBudget` de `AssistantPlatformSettings` se leen, con la clave de la plataforma, en `AssistantConfigurationPolicy`, `AssistantSettingsBuilder`, `AssistantStateConfigurationBuilder`, `AssistantUsageBuilder` y `AssistantSettingsService` (el valor de entrada del presupuesto propio). Con clave propia, la organización tiene los suyos | B4 |
| Otros topes | Ninguno más con máximo de la plataforma. El plazo de los chats de WhatsApp llega con la B18 de la base 2 | K0 y B5 |
| Uso de la IA | `AssistantFinancialCalls` es único por `(OperationId, Sequence)`. La operación lleva `BudgetMonthId` a `AssistantBudgetMonths`, único por `(OrganizationId, Month)`, con su zona y su intervalo UTC; con la clave de la plataforma en una organización, siempre lo tiene | Sin tarea: la costura 4 ya está; K0 lo confirma |
| Lock | `AdvisoryLockKeys` y `AcquireAdvisoryLocksAsync`, como en `PersonalSpaceLock` | B1 |
| IL | `ArchitectureTests/Support/CallSites.cs` lee llamadas y tipos usados | B6 |

## Contrato

### K0 · Lo que dejaron la segunda parte de la base y la etapa de acciones

El chat del contrato confirma contra el código y las tablas de sus puertas:
- **la puerta de la base 2 cerrada.** Las acciones corren a la vez (decisión del 2026-11-26): lo que sumen a los lectores del permiso o a los topes lo vuelve a mirar la I1;
- **la B18:** `WhatsAppPlatformSettings.MaxOrganizationChatRetentionDays` (3650 de entrada) y `WhatsAppOrganizationSettings.ChatRetentionDays` (90, de 1 al máximo), con los lectores que nombra la B5. Ese máximo es uno de «los máximos de hoy» (0069 §4), y la B5 lo pasa por el resolvedor;
- **la B14:** el cupo del día con la clave de la plataforma sigue leyendo `DailyMessageLimit` de la plataforma, ahora por persona en la organización;
- **los topes con máximo de la plataforma:** la lista cerrada que va al resolvedor. Se esperan tres: el presupuesto del mes y el cupo del día de la IA, y el plazo de los chats. Uno más va a la B4 o la B5, según su módulo;
- **los lectores del permiso de uso:** quién usa `ITenantFeatureCache`, `ITenantFeatureReader` o `TenantFeatureRow`. Uno que sumaron la base 2 o las acciones va a la B2;
- **el uso de la IA:** que `AssistantFinancialCalls` y `AssistantBudgetMonths` siguen como dice «Qué se verificó». Si no, la columna o el índice mínimo va a la B4;
- **la última migración y el snapshot,** para armar la de la B1 sobre el `HEAD` del momento, con `ModelSnapshotTests` (B9).

Lo que difiera va a «Cambios al contrato» antes de programar.

### K1 · Las piezas, con el contrato igual

- **El enum del dominio:** `TenantFeatureSource` suma `Approved` y `Subscription`, al final. `Domain/Features/TenantFeatureSourceExtensions.cs` suma `IsPlatformManaged()`: `Plan`, `Manual` o `Trial`.
- **El contrato de Plataforma:** `Application/Models/Platform/PlatformFeatureSource.cs`, con `Plan`, `Manual` y `Trial`, y la misma descripción que hoy.
  - lo usan `PlatformFeatureResponse`, `UpdatePlatformFeatureRequest` y `UpdatePlatformFeatureHttpRequest`, y se traduce al del dominio en el modelo;
  - `OpenApiExtensions` lo publica con el nombre de esquema `TenantFeatureSource` (`CreateSchemaReferenceId`);
  - el validador sigue igual sobre el enum nuevo.
- **Los modelos y el puerto de los topes,** sin lógica:
  - `Application/Interfaces/Integrations/Features/IModuleLimitContributor.cs`, con `IReadOnlyList<ModuleLimitDefinition> Limits` y `Task<ModuleLimitValue> ReadPlatformMaximumAsync(string key, CancellationToken ct)`;
  - en `Application/Models/Features/`: `ModuleLimitDefinition(string Key, ModuleLimitKind Kind, int? Default, int? Minimum)`, `ModuleLimitKind` (`Count` o `Amount`), `ModuleLimitValue(int? Count, Money? Amount)` y `ModuleLimitCeiling(string Key, int? Count, Money? Amount)`.
- **La prueba del contrato:** se regenera `openapi.json` y `git diff --exit-code docs/contracts/openapi.json` sale limpio.

No hay ruta, error ni permiso nuevo. No toca entidades, configuraciones de EF ni migraciones.

### Decisiones técnicas del contrato

- **El contrato de Plataforma no cambia.** El enum propio deja afuera los dos valores nuevos, y el front no se entera. La pestaña la rehace la etapa del cobro.
- **La clave de un tope es `<módulo>.<campo>`:** `assistant.dailyMessages`, `whatsapp.chatRetentionDays`. El módulo es la clave de su feature.
- **Un tope de dinero es `Amount`, con `Money`;** los demás son `Count`, un entero en su unidad.
- **El aporte de cada módulo** toma el nombre de su puerto (`AssistantModuleLimitContributor`), como `AssistantFeatureDefinitionContributor`: no es un helper.
- **El resolvedor** es un helper del núcleo con sufijo `Resolver`, que ya está en la tabla de [capas-y-flujo](../rules/capas-y-flujo.md). No nombra módulos: recibe los aportes.

**Texto para el chat de Contrato**

```text
Leé AGENTS.md, la ADR docs/decisions/0069-modulos-y-suscripciones.md, este plan y la spec docs/plans/formularios/costuras-de-modulos.md. Hacé K0 y después K1, sin adelantar lógica: los dos valores nuevos al final de TenantFeatureSource con IsPlatformManaged(), el enum PlatformFeatureSource del contrato de Plataforma publicado con el nombre de esquema TenantFeatureSource, y los modelos y el puerto de los topes (IModuleLimitContributor, ModuleLimitDefinition, ModuleLimitKind, ModuleLimitValue y ModuleLimitCeiling). Regenerá docs/contracts/openapi.json y comprobá que no cambió: si cambia, no sigas y anotalo en «Cambios al contrato». No toques entidades, configuraciones de EF ni migraciones, ni el front. Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo tuyo con git commit -- <rutas> y la línea Tarea: costuras-de-modulos/<id>. Aplicá capas-y-flujo, modulos-habilitados, api-http y tests. Al terminar: node scripts/arneses/verificar.mjs (build y tests rápidos enteros, sin filtrar por módulo) y git diff --exit-code docs/contracts/openapi.json; los de integración corren una vez por carril (ADR 0071).
```

## Back

Los pasos, con sus archivos, lo que copian y sus tests, están en la spec (§Pasos). Valen:
- `Result` y ProblemDetails, `OperationLog.RunAsync`, `LoggerMessage` y `TimeProvider`;
- una UoW por escritura;
- el núcleo no nombra módulos: cada módulo aporta lo suyo por su puerto;
- los valores de hoy no cambian para nadie: una organización, una persona y Plataforma ven lo mismo antes y después de cada tarea.

1. **B1 · El índice por origen y el lock del writer (R: datos).**
   - **El índice:** `TenantFeatureConfiguration` pasa a único por `(TenantId, FeatureKey, Source)`, `IX_TenantFeatures_TenantId_FeatureKey_Source`, y `UniqueKeys.TenantFeature` toma ese nombre.
   - **La migración** `<fecha>_TenantFeaturesBySource`:
     - cambia el índice sin tocar las filas;
     - su `Down` levanta un error si hay dos filas de un mismo módulo, y si no, vuelve al índice viejo;
     - anota la excepción de la 0060, como la base 2: todavía no hay despliegue (123).
   - **El lock:** `ITenantFeatureRepository.LockAsync(tenantId, key, ct)`, con `AdvisoryLockKeys.TenantFeature(tenantId, key)`, como `PersonalSpaceLock`. `PlatformFeatureWriter` lo toma antes de leer, así sigue dejando una sola fila por módulo (0060).
   - **Tests:**
     - una fila guardada antes de la migración sigue andando;
     - la base admite `Manual` y `Subscription` del mismo módulo, y rechaza dos `Manual`;
     - el `Down` se niega con dos filas;
     - dos altas a la vez con distinto origen dejan una fila y un 409;
     - `ModelSnapshotTests` (B9 de la base 2) sigue en verde.
   - **Escenarios del revisor:** una migración portable, el `Down` que no pierde datos, la carrera de dos operadores, la de un operador con su propio reintento y el 409 que sigue siendo 409.
2. **B2 · Leer varias filas (R: aislamiento y permisos).**
   - **La regla, en un solo lugar:** `TenantFeatureRow.IsModuleEnabled(rows, key, defaultEnabled, nowUtc)`. Sin filas del módulo, vale el de entrada; con filas, alguna vigente.
   - **Quién la usa:** `TenantFeatureFilter` y `WhatsAppModulePolicy`, y lo que haya sumado K0. `FeatureService` y `ProfileModulesBuilder` pasan por el filtro y no cambian.
   - **Plataforma:** `PlatformFeatureListBuilder` y `TenantFeatureRepository.GetForUpdateAsync` leen la fila de Plataforma (`IsPlatformManaged()`). Si hubiera varias, toman la más reciente. El reader ordena por clave y fecha de cambio.
   - **Las fichas:** `modulos-habilitados.md` (los cinco orígenes, una fila por origen y `Plan` solo para leer) y `docs/features/plataforma.md`.
   - **Tests** (las filas nuevas se cargan directo en la base, porque nadie las escribe):
     - unitarios de la regla: sin filas, una, una manual con una prueba vencida, una manual apagada con una aprobada vigente, una aprobada vencida;
     - de integración: `[FeatureGate]` da 200 o 404, `/api/me` trae el módulo, la política de WhatsApp sin sesión, y la pestaña y su `PUT` con una fila `Subscription` al lado, que no tocan;
     - la fila de otra organización no prende nada.
   - **Escenarios del revisor:** la fila de otra organización o de otro módulo; un acceso que no corresponde (Consumer en un módulo Business, Plataforma sin su política); el vencimiento justo en el instante, con la fila en la caché; el apagado de emergencia, que sigue mandando; y que la pestaña no muestre ni edite una fila que no es de Plataforma.
3. **B3 · El resolvedor.** `Application/Services/Features/ModuleLimitResolver.cs`, registrado en `Application/DependencyInjection.cs`, recibe todos los `IModuleLimitContributor`. `Application/Services/Features/` entra en `docs/architecture/arbol.md`, con su fila del mapa y su `AGENTS.md`.
   - **`ResolveAsync(key, organizationId, ct)`** da el `ModuleLimitCeiling`. En esta etapa es el máximo de la plataforma, también con `organizationId` nulo (una persona o Plataforma).
   - **Lo que es un bug:** una clave desconocida o repetida, o un aporte que devuelve la otra clase, tiran una excepción.
   - **`ModuleLimitCeiling.Effective(int? organizationValue)`** da el menor entre lo que eligió la organización (o el de entrada) y el tope.
   - **Tests unitarios:** cada caso, con aportes de prueba; y que las declaraciones reales tengan claves únicas con el prefijo de una feature de su módulo, y mínimo y entrada coherentes.
4. **B4 · Los topes de la IA (R: efectos y aislamiento, por el cupo de cada organización).**
   - **El aporte:** `Application/Modules/Assistant/Services/AssistantModuleLimitContributor.cs`, registrado en `AssistantModule.cs`, declara `assistant.dailyMessages` (`Count`, sin entrada y con mínimo 1) y `assistant.monthlyBudget` (`Amount`). Los máximos salen de `IAssistantSettingsReader.FindPlatformAsync`.
   - **Quién lee por el resolvedor,** con la clave de la plataforma: los cinco lectores de «Qué se verificó» (la organización del claim; una persona y Plataforma, con nulo). Con clave propia, siguen los valores de la organización.
   - **Tests:**
     - los mismos valores que hoy, en la organización, la persona y Plataforma;
     - bajar el máximo en la configuración de Plataforma baja el cupo del día (el 429 llega antes) y el presupuesto del mes de la organización;
     - con clave propia no cambia nada;
     - `AssistantFinanceTests` sigue en verde.
   - **Escenarios del revisor:** que ningún camino pague con el máximo viejo o sin tope, con la reserva hecha antes de llamar al proveedor; el turno de un worker, que lee el tope con el actor, y nunca con datos de otra organización; y el presupuesto en su moneda, como `Money`.
5. **B5 · El plazo de los chats de WhatsApp (R: datos y aislamiento).**
   - **El aporte:** `Application/Modules/WhatsApp/Services/WhatsAppModuleLimitContributor.cs`, registrado en `WhatsAppModule.cs`, declara `whatsapp.chatRetentionDays` (`Count`, 90 de entrada, mínimo 1). Su máximo sale de `IWhatsAppSettingsReader.FindPlatformAsync` (`MaxOrganizationChatRetentionDays`; sin fila, `WhatsAppSettingsLimits.DefaultMaxOrganizationChatRetentionDays`).
   - **Quién lee por el resolvedor:**
     - `WhatsAppSettingsService.UpdateAsync`, que compara con el máximo adentro de la UoW (el validador del pedido mira solo de 1 a 3650): más que el tope da el 400 de siempre, en su campo;
     - `WhatsAppSettingsResolver.OrganizationChatRetentionDaysAsync`, que hoy recorta con `Math.Min` y pasa a `Effective`. Lo usan el `GET` de la organización, que devuelve el plazo ya recortado y no el máximo, y la purga de cada organización (`WhatsAppMessageRetentionService.ClearExpiredOrganizationChatsAsync`): si Plataforma baja el máximo, vale el menor, sin tocar lo guardado.
   - **Tests:**
     - el `PUT` sobre el tope da 400;
     - bajar el máximo purga con el menor en esa organización y no en otra;
     - los chats de la plataforma siguen con su propio plazo.
   - **Escenarios del revisor:** `WhatsAppMessageRetentionWorker` entra con su organización (`TenantJobRunner`) y es idempotente y por lotes, como la B18, hasta que la E10 le dé el lease; nunca vacía un mensaje antes del plazo elegido, salvo que el tope sea menor; dos organizaciones sin cruce; el lote que falla registra y sigue.
6. **B6 · El test de arquitectura y la regla de los topes.**
   - **`ArchitectureTests/ModuleLimitArchitectureTests.cs`**, con `CallSites`:
     - una lista cerrada del getter de cada máximo de la plataforma, por clave declarada, que se cruza en los dos sentidos con las declaraciones reales;
     - cada getter lo leen solo su aporte y las clases de la pantalla de Plataforma de su módulo, nombradas con su motivo;
     - ningún tipo de un módulo nombra la configuración de otro;
     - un control negativo con una clase de prueba.
   - **La regla,** con su test: un punto «Topes (0069)» en `modulos-habilitados.md` (declarar en el aporte del módulo, leer por `ModuleLimitResolver`, validar contra el tope y usar `Effective`), su fila en «Lo verifica» y una línea en la sección 270 de `docs/operations/configuracion.md`.
7. **B7 · Apagar no borra.**
   - **Los tests,** cada uno en el árbol de tests de su módulo:
     - `Modules/WhatsApp/WhatsAppModuleOffKeepsDataTests.cs`, con chats, mensajes y contactos;
     - `Modules/Assistant/AssistantModuleOffKeepsDataTests.cs`, con conversaciones, uso y configuración.
   - **Qué prueba cada uno:** prende el módulo, carga datos y lo apaga con el `PUT` de Plataforma. Sus rutas dan 404 y sus filas siguen en la base; los trabajos de fondo no lo atienden, y apagarlo no borra nada: lo que vence lo sigue borrando la purga de siempre. Al prenderlo, se ven igual.
   - **La regla:** la línea «Apagar no borra datos (0069)» en `modulos-habilitados.md`, junto a «Apagar no toca los roles», con los dos tests en «Lo verifica». El plazo de los datos que siguen corriendo es la purga de siempre, no el apagado.

**Texto para el chat del Back**

```text
Leé AGENTS.md, la ADR docs/decisions/0069-modulos-y-suscripciones.md, este plan y la spec docs/plans/formularios/costuras-de-modulos.md. Esperá K1. Cada tarea respeta las «Reglas de orden»; la única migración es la de B1, armada sobre el HEAD del momento. Las tareas con (R) llevan su revisión adversarial completa antes del commit, con los escenarios de su tarea. Nadie escribe filas Approved ni Subscription: los tests las cargan directo en la base. El contrato HTTP no cambia: si openapi.json cambia, pará y anotalo en «Cambios al contrato». Los valores de hoy no cambian para nadie. Sin proveedores reales ni secretos: la IA es el FakeChatClient, y WhatsApp, sus fakes. Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo tuyo con git commit -- <rutas> y la línea Tarea: costuras-de-modulos/<id>. Por tarea: el test dirigido y, al terminar, node scripts/arneses/verificar.mjs (build y tests rápidos enteros, sin filtrar por módulo); los de integración y seguridad de Features, del Asistente y de WhatsApp corren una vez, al cerrar el carril (ADR 0071). Las fichas que cambian (modulos-habilitados, plataforma, configuracion) van en el commit de su tarea.
```

## Front

Sin tareas. La etapa no tiene pantallas, y `openapi.json` no cambia. La pestaña «Módulos» (`OrganizationModulesTab.tsx`) y sus tests siguen igual. En la I1, el front corre `npm run contracts:check` sin regenerar.

## Integración y puerta

1. **I1 · Integrar.** El front corre `npm run contracts:check` y los tests de `src/areas/platform/tenants/tabs/`, sin regenerar ni tocar nada. Si algo difiere, vuelve al back y se anota abajo.
2. **I2 · Puerta.** Se corre `/puerta` una vez, sobre copias del corte:
   - `dotnet build` sin advertencias y `dotnet test` con Docker, con los tres tests de la 0069;
   - en el front, build, lint, test y `contracts:check`;
   - el E2E real de siempre, sin casos nuevos;
   - `realinear-docs --detectar` y `estado-docs --detectar` en 0;
   - el recorrido de siempre, sin pasos nuevos.

   No hay galería (no hay pantallas), ni prueba con Meta o con un proveedor real.

| Paso | Resultado | Número |
| --- | --- | --- |
| Build del back | Pasa en 3a4ca9f5: 0 errores y 0 advertencias | 11 proyectos |
| Tests del back (con Docker) | Pasa con los arreglos, en 3a4ca9f5: pasan los 59 que fallaban. Corrí el área Modules.Assistant de la integración (426/426): están los 57 del Asistente, con los 24 que vencían, AssistantModuleOffKeepsDataTests y AssistantPersistenceTests con sus 13 tablas. También corrí WhatsAppModuleOffKeepsDataTests, RouteIsolationMatrixTests y RuntimeRoleTableGrantsTests, que es nuevo (7/7). La suite completa no se repitió porque los arreglos tocan solo tests y una ficha. El resto quedó en verde en el corte (3998) | 433/433 (59/59 de los que fallaban) |
| Build del front | Pasa: sale 0 | exit 0 |
| Lint | Pasa: 0 errores (11 advertencias) | 0 errores |
| Tests del front (corridos solos) | Pasa: 373 archivos | 3956/3956 |
| Contrato (contracts:check) | Pasa: el esquema TypeScript generado coincide con openapi.json | exit 0 |
| E2E real | No pasa. «Asistente real E8B» ya pasa E8B-20-2 en 1440 y 390 (la causa era la falta del GRANT), pero en los dos anchos se corta en el caso técnico attachments.five-six: un mensaje con cinco imágenes termina en Assistant.Provider.ContextTooLarge (assistant-real-ui.mjs:205). Desde history-rename, el resto del caso pasa en los dos anchos (15 comprobaciones por ancho). Probé el entorno aislado sin calificar las acciones, solo en la copia, y así el paso pasa: lo rompe e465d7e5, de acciones del asistente. Los demás grupos no se repitieron y siguen en verde desde el corte. Sin proveedores reales | 46/47 |
| Documentación (realinear-docs --detectar) | Pasa: ccde3fe3 resolvió los 2 hallazgos (arbol.md:330 y persistencia-ef.md:40). Con los dos repos en main (front 2b719e81) da 0. Con el front en su corte (7c5cfa3c) da 4 para el modelo, todos en agregar-una-accion-al-asistente.md:66: la guía nombra hooks y users/lib/proposal.ts del front de acciones del asistente (dcbbd07d), que todavía no estaban en ese corte | auto 0 · modelo 0 · humano 0 |
| Estado de los documentos (estado-docs --detectar) | Pasa: organizaciones.md quedó al día con 83323c0a | auto 0 · modelo 0 · humano 0 |
| Lo que no está en la spec (conformidad --detectar) | Pasa en los dos repos: 11 commits del back, con los 2 de la puerta, y 1 del front con la línea Tarea | auto 0 · modelo 0 · humano 0 |
| Galería | No aplica: la etapa no tiene pantallas nuevas ni cambiadas. El plan, en la I2, dice «No hay galería» | — |
| Recorrido de usuario | No pasa: se corta en E8B-20-2 a 1440 (TimeoutError, el mismo paso del Asistente que el E2E). Los pasos que siguen no corrieron. Índice: C:\e2e-cm\ArquitecturaBaseMutitenantFront\docs\design\capturas\recorrido\2026-11-26\index.html | 58/254 |
| Quitar el módulo | No aplica: no es la etapa de un módulo | — |
| Lista de control (revisión de código del cierre) | 9 revisores por premisa: textos, arquitectura, caché/cuotas/módulos, seguridad, fuera de la spec y las 4 adversariales. Juntaron 1 hallazgo y no confirmaron ninguno. Los tres tests de la 0069 pasan. El de apagar sin borrar de WhatsApp quedó en verde con el arreglo del test, que cuenta WhatsAppCustomer y sigue comprobando que el webhook guardó al remitente. El del Asistente quedó en verde con el GRANT; Pantallas contra el tablero: 0 hallazgos, 0 confirmados | 0 confirmados |
| Commits que se pisan | Pasa: lo de cada commit de la etapa sigue en el código. La B3 cambió el record ModuleLimitCeiling al sumarle cuerpo. Los 2 arreglos de la puerta tocan dos tests: el conteo de AssistantPersistenceTests (de 12 a 13) y la cuenta del test de la B7, que mantiene su comprobación | 12/12 |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, la ADR 0069, este plan, la spec y la skill /puerta. En el front, corré npm run contracts:check y los tests de la pestaña Módulos sin regenerar ni tocar nada; una diferencia vuelve al back y se anota en «Cambios al contrato». Corré la puerta una sola vez sobre copias del corte; un arreglo corre solo lo que toca. Sin galería, sin proveedores reales, sin push y sin tocar el lienzo. Dejá en este plan solo la tabla final corta.
```

## Relación con otras etapas

**Usa de las anteriores:**
- **de la segunda parte de la base:** la configuración de WhatsApp y la purga de los chats (B18, 272) y el cupo del día de la IA (B14, 271);
- **de la E8b:** `AssistantPlatformSettings`, `AssistantFinancialCalls`, `AssistantFinancialOperations` y `AssistantBudgetMonths`;
- **del núcleo:** `TenantFeatures` y su filtro (E5), la caché con generaciones (E9) y los advisory locks;
- **de la etapa de acciones:** nada. Corren a la vez desde el 2026-11-26: las dos K1 no corren juntas en `main` (`openapi.json`), y cada migración se arma sobre el `HEAD` del momento con `ModelSnapshotTests`.

**Deja para las siguientes:**
- **Para la E12, que suma el módulo `forms`, y todo el bloque:**
  - el permiso de uso con una fila por origen (`TenantFeatureSource` con `Approved` y `Subscription`, que nadie escribe todavía) y su regla en un solo lugar, `TenantFeatureRow.IsModuleEnabled`. Quien lea `TenantFeatures` sin pasar por `[FeatureGate]` ni `IFeatureService` (un trabajo de fondo, una política como `WhatsAppModulePolicy`) usa esa regla, nunca `SingleOrDefault`;
  - el módulo se gatea solo por su funcionalidad (`[FeatureGate("forms")]`), sin mirar planes ni suscripciones (0069 §1);
  - apagar no borra datos: cada módulo nuevo suma su `<Módulo>ModuleOffKeepsDataTests`, como los de la B7, y su línea en «Lo verifica» de `modulos-habilitados.md`.
- **Desde la E14** ([0069](../decisions/0069-modulos-y-suscripciones.md) §4), el resolvedor de topes:
  - cada módulo declara sus topes en su `IModuleLimitContributor` (`WhatsAppModuleLimitContributor`, `AssistantModuleLimitContributor` o el de Formularios);
  - lee cada tope por `ModuleLimitResolver.ResolveAsync` y valida lo que elige la organización contra él, con `ModuleLimitCeiling.Effective` en cada uso;
  - suma el getter de su máximo a la lista de `ModuleLimitArchitectureTests`.
- **Los topes de cada etapa** (lo dicen sus specs desde el 2026-11-23):

| Etapa | Topes | Su máximo de la plataforma | Aporte |
| --- | --- | --- | --- |
| E14 | `whatsapp.flowSessionHours` (`FlowSessionHours`) | `MaxFlowSessionHours` | `WhatsAppModuleLimitContributor` |
| E18 | `forms.maxFileMiB` (`MaxFileMiB`) | el de `FormPlatformSettings` | `FormsModuleLimitContributor` |
| E23 (la E24 los usa en cada asistente) | `assistant.maxReplyLength`, `assistant.repliesPerDay` y `assistant.formOffersPerDay` | los tres de `AssistantPlatformSettings` | `AssistantModuleLimitContributor` |
| E25 | `forms.loadsPerSpreadsheet`, `forms.pdfPages`, `forms.rereadsPerPersonPerDay`, `forms.similarDocumentsToSuggest`, `forms.proposedExpiryDays` y `forms.discardedRetentionDays` | los seis de `FormPlatformSettings` | `FormsModuleLimitContributor` |

- **Para la etapa «Módulos y suscripciones»,** después de la E25:
  - escribe `Approved`, `Subscription` y la prueba del sistema, un despliegue después de esta;
  - decide qué fila edita Plataforma cuando haya varias, y rehace la pestaña;
  - suma al resolvedor el valor de Plataforma para una organización y el del plan; si lo cachea, lo agrega al inventario de [cache](../rules/cache.md);
  - suma el plazo, la exportación y el borrado de un módulo apagado;
  - usa la clave y el mes del uso de la IA, como están.

**Queda afuera:** el catálogo, los precios, los pedidos, las suscripciones, Mercado Pago y su factura de ARCA (0069).

## Cambios al contrato

- **K0 (2026-11-26):** el código coincide con lo que asumen este plan y la spec: la puerta de la base 2 cerrada, la B18 y la B14, los tres topes con máximo de la plataforma, los cuatro lectores del permiso de la B2, el uso de la IA y la última migración (`20261125153522_SettingsRules`). Un detalle para la B5: el `GET` de WhatsApp de la organización devuelve el plazo recortado y también `maxOrganizationChatRetentionDays`, los dos de `WhatsAppSettingsResolver.OrganizationChatRetentionAsync`. La B5 sigue devolviendo los dos, con el tope en `ModuleLimitCeiling.Count`, y el contrato no cambia.

- **B3:** pieza interna, no HTTP: `ModuleLimitCeiling` suma `public int? Default { get; init; }`, que el resolvedor carga desde `ModuleLimitDefinition.Default`; los tres valores del constructor (`Key`, `Count`, `Amount`) no cambian. `Effective(int? organizationValue)` devuelve `int`: el menor entre (lo elegido o `Default`) y `Count`, o `Count` si los dos son nulos; en un `Amount` tira `InvalidOperationException`.
- **B2:** pieza interna, no HTTP: `TenantFeatureRow` suma `static bool IsModuleEnabled(IEnumerable<TenantFeatureRow> rows, string key, bool defaultEnabled, DateTime nowUtc)`.
- **B2:** pieza interna, no HTTP: `ITenantFeatureReader.ListAsync` (y la caché `t:{id}:features`, que guarda lo que devuelve) trae las filas ordenadas por clave y, dentro de cada clave, de la más reciente a la más vieja. Para la fila de Plataforma se busca la primera con `Source.IsPlatformManaged()`.
- **B2:** pieza interna, no HTTP: `WhatsAppModulePolicy` recibe además `IFeatureCatalog` en su constructor (por DI, sin cambios en el registro).
- **B5:** pieza interna, no HTTP: `WhatsAppSettingsResolver` recibe además `ModuleLimitResolver` e `ITenantContext` en su constructor (por DI, sin cambios en el registro) y suma `Task<ModuleLimitCeiling> OrganizationChatRetentionCeilingAsync(ct)`, el tope de `whatsapp.chatRetentionDays` para la organización activa.
- **B5:** pieza interna, no HTTP: `WhatsAppModuleLimitContributor.ChatRetentionDaysKey = "whatsapp.chatRetentionDays"` (`internal const`), registrado como `IModuleLimitContributor` en `WhatsAppModule`.
- **Integración:** esta etapa no cambió el contrato HTTP: `openapi.json` de `HEAD` coincide con el que genera el build del último commit de código (5ee8b96a), y la B7 (bcac74f1) solo agrega tests y una línea en `modulos-habilitados.md`. Todos los cambios anotados son piezas internas.
- **Integración:** en el front, el tipo generado se puso al día con la B5 de la etapa de acciones del asistente (las tres rutas de la propuesta ya no tienen 501), porque la puerta de costuras necesita `contracts:check` en verde. Si la etapa de acciones corre `npm run contracts`, le va a salir lo mismo.

## Para decidir

Nada abierto. Las de producto las tomó el usuario el 2026-11-23, en la 0069: una fila por origen y prendido si alguna está vigente, un solo resolvedor de topes y apagar sin borrar. Lo demás es técnico y está anotado en «Decisiones técnicas del contrato» y en cada tarea.
