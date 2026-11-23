# Costuras de módulos (spec)

**Estado:** pendiente · spec de la etapa «Costuras de módulos», que va después de la etapa de acciones del asistente y antes de la E12, sin pantallas ([ADR 0069](../../decisions/0069-modulos-y-suscripciones.md), aceptada por el usuario el 2026-11-23). Arma las cuatro costuras que necesita la etapa del cobro para no rehacer la 12 a la 25. Su plan ejecutable es el [del 2026-11-23](../2026-11-23-etapa-costuras-de-modulos.md); lo que deja la segunda parte de la base lo confirma su K0.

## Qué leer ✓

1. **Lo anterior:**
   - de la [segunda parte de la base](base-de-la-12-segunda-parte.md), la B18 (la configuración de WhatsApp con `chatRetentionDays` y `maxOrganizationChatRetentionDays`, y la purga de los chats, 272) y la B14 (el cupo del día de la IA, 271);
   - del núcleo, los módulos por organización (`TenantFeatures`, `TenantFeatureFilter` y la pestaña «Módulos» de Plataforma) y la caché compartida;
   - de la [etapa de acciones](acciones-del-asistente.md), nada: va antes solo por orden.
2. **Decisiones:**
   - la ADR [0069](../../decisions/0069-modulos-y-suscripciones.md), que manda;
   - la 0028 del [índice](../../decisions/README.md) (módulo ≠ permiso, 404 apagado), la [0060](../../decisions/0060-dos-versiones-conviven.md) (se lee un despliegue antes de escribir), la [0051](../../decisions/0051-precision-tecnica-de-costos-ia.md) (el costo de la IA) y la [0052](../../decisions/0052-cache-compartida-redis.md) (la caché);
   - de la hoja de ruta, la 123 (todavía no hay despliegue), la 270 (ninguna regla de negocio fija en el código) y la 272 (los plazos de los datos).
3. **Fichas:**

| Ficha | Por qué |
| --- | --- |
| [modulos-habilitados](../../rules/modulos-habilitados.md) | el permiso de uso, el filtro, la pestaña y los trabajos de fondo; suma las tres reglas nuevas |
| [modulos](../../rules/modulos.md) | el núcleo no nombra módulos: cada uno aporta sus topes por un puerto |
| [capas-y-flujo](../../rules/capas-y-flujo.md) | el resolvedor es un `Resolver`; el aporte de un módulo toma el nombre de su puerto |
| [persistencia-ef](../../rules/persistencia-ef.md) y [migración](../../guides/migracion.md) | el índice nuevo, su `Down` y la convivencia |
| [concurrencia](../../rules/concurrencia.md) | el lock del writer de Plataforma y el 409 |
| [cache](../../rules/cache.md) | `t:{id}:features` no cambia de forma; el resolvedor no suma caché |
| [numeros-y-moneda](../../rules/numeros-y-moneda.md) | un tope de dinero va como `Money` |
| [configuracion](../../operations/configuracion.md#reglas-de-negocio-en-pantalla-270) | valor de entrada, mínimo y máximo de cada tope |
| [tests](../../rules/tests.md) | los de arquitectura con su control negativo |

4. **Código para copiar:**
   - el aporte de un módulo, de `Application/Modules/Assistant/Services/AssistantFeatureDefinitionContributor.cs` y su puerto `IFeatureDefinitionContributor`;
   - el lock, de `Infrastructure/Persistence/Repositories/PersonalSpaceLock.cs` y `AdvisoryLockKeys`;
   - el test de arquitectura, de `tests/ArquitecturaBaseMultitenant.ArchitectureTests/CacheArchitectureTests.cs`, con `Support/CallSites.cs`;
   - el test de una migración con filas guardadas antes, de `Api.IntegrationTests/Messaging/OutboxMigrationTests.cs`;
   - prender y apagar un módulo en un test, de `Api.IntegrationTests/Modules/Assistant/AssistantFeatureTests.cs`.
5. **Referencia de producto:** la investigación del 2026-11-23 que resume la 0069 (Stripe, Chargebee, Recurly y Mercado Pago). ArquitecturaBase no tiene nada de esto.

## Qué se construye

Nada que vea una persona. Son cuatro piezas por dentro, para que la etapa del cobro sume pedir, probar y suscribirse sin tocar los módulos.

**1. El permiso de uso lleva una fila por origen.**
- `TenantFeatures.Source` suma `Approved` (pedido aprobado) y `Subscription` (pago). `Plan` queda solo para leer filas viejas.
- **Un módulo está prendido si alguna fila de ese módulo está vigente.** Sin filas, vale su valor de entrada, como hoy. Una prueba vencida no apaga una habilitación manual.
- Lo leen así el filtro (`[FeatureGate]`), `/api/me`, los trabajos de fondo y la pestaña de Plataforma.
- **Nadie escribe todavía las filas nuevas** (0060): el writer de Plataforma sigue dejando una sola fila por módulo, con un lock para que dos altas a la vez no dejen dos. La etapa del cobro empieza a escribirlas un despliegue después.
- **La pestaña «Módulos» y su contrato no cambian.** Leen y editan la fila de Plataforma (`Plan`, `Manual` o `Trial`). Si hubiera varias, que en esta etapa solo arma un test, toman la más reciente.

**2. Un solo lugar da los topes.**
- **Cada módulo declara sus topes:** la clave, el valor de entrada, el mínimo y de dónde sale el máximo de la plataforma.
- **El resolvedor** da el tope de una organización: en esta etapa, el máximo de la plataforma. La etapa del cobro le suma el valor que fije Plataforma para una organización y el del plan, sin tocar los módulos.
- **Los topes de hoy pasan por él:** el presupuesto del mes y el cupo del día de la IA con la clave de la plataforma, y el plazo de los chats de WhatsApp de la base 2. Lo que la organización elige se valida contra ese tope, y en cada uso vale el menor de los dos.
- **Un test de arquitectura** frena un tope con máximo de la plataforma que no pase por el resolvedor, y un módulo que lea la configuración de otro.

**3. Apagar un módulo no borra datos.** Un test por módulo instalado (WhatsApp y el Asistente) lo apaga, ve el 404, comprueba que sus datos siguen y lo vuelve a prender. El plazo, la exportación y el borrado de un módulo apagado son de la etapa del cobro.

**4. El uso de la IA ya tiene su clave y su mes.** `AssistantFinancialCalls` es único por `(OperationId, Sequence)`, y cada operación con la clave de la plataforma en una organización apunta a su `AssistantBudgetMonths`, único por `(OrganizationId, Month)`, con su zona y su intervalo UTC. Se verificó en el código el 2026-11-23: no hay tarea, solo la fila de K0.

## Pantallas ✓

Sin pantallas ni tableros. La pestaña «Módulos» de Plataforma (`OrganizationModulesTab.tsx`) se ve y se comporta igual.

## Contrato ✓

**Rutas:** ninguna nueva ni cambiada. `docs/contracts/openapi.json` queda igual byte a byte: el esquema `TenantFeatureSource` sigue con `Plan`, `Manual` y `Trial`. Para eso, las rutas de Plataforma usan un enum propio, `PlatformFeatureSource`, con esos tres valores, publicado con el nombre de esquema `TenantFeatureSource` y la misma descripción. Un `Approved` o `Subscription` en el `PUT` da el 400 de siempre.

**Errores:** ninguno nuevo.

**Idempotencia y concurrencia:** el `PUT` de Plataforma sigue con versión y 409. El writer toma un lock por organización y módulo antes de leer.

**Las piezas internas** que usan la E14 a la E25 (`Application`):

| Pieza | Dónde | Qué es |
| --- | --- | --- |
| `IModuleLimitContributor` | `Interfaces/Integrations/Features/` | el puerto de cada módulo: `Limits` y `ReadPlatformMaximumAsync(key, ct)` |
| `ModuleLimitDefinition` | `Models/Features/` | `(Key, Kind, Default, Minimum)`; la clave es `<módulo>.<campo>` |
| `ModuleLimitKind` | `Models/Features/` | `Count` (un entero: veces, días, horas, MiB, caracteres) o `Amount` (`Money`) |
| `ModuleLimitValue` | `Models/Features/` | `(Count, Amount)`, con uno solo cargado |
| `ModuleLimitCeiling` | `Models/Features/` | el tope: `(Key, Count, Amount)` y `Effective(int? organizationValue)`, el menor entre lo elegido (o el de entrada) y el tope |
| `ModuleLimitResolver` | `Services/Features/` | `ResolveAsync(key, organizationId, ct)`; `organizationId` nulo (una persona o Plataforma) da el máximo de la plataforma |

En un `Count`, `Minimum` va de 0 en adelante y `Default`, si está, no es menor que el mínimo; nulo dice que la organización no elige y vale el tope. En un `Amount`, los dos van nulos: lo valida el validador del módulo.

## Datos ✓

**`platform.TenantFeatures`:**
- el índice único pasa de `(TenantId, FeatureKey)` a `(TenantId, FeatureKey, Source)`, `IX_TenantFeatures_TenantId_FeatureKey_Source`, y `UniqueKeys.TenantFeature` toma ese nombre;
- `Source` sigue como texto de hasta 60, y los dos valores nuevos van al final del enum;
- sin columnas nuevas ni RLS nuevo: es global, como hoy.

**La migración** (`<fecha>_TenantFeaturesBySource`):
- cambia el índice; las filas guardadas siguen igual;
- su `Down` se niega con un error si hay dos filas de un mismo módulo, y si no, vuelve al índice viejo;
- lleva la excepción de la 0060 anotada, como la base 2: todavía no hay despliegue (123), así que la versión anterior nunca convive con el índice nuevo.

**La caché** `t:{id}:features` no cambia de forma: guarda las filas, ahora quizá varias por módulo. **Los topes** no suman tablas: se leen de donde están hoy.

## Pasos ✓

**Back,** en orden y con un solo carril:

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | El índice por origen, su migración y el lock del writer | `TenantFeatureConfiguration.cs`, `UniqueKeys.cs`, la migración, `ITenantFeatureRepository.LockAsync`, `AdvisoryLockKeys.TenantFeature`, `PlatformFeatureWriter.cs` | `PersonalSpaceLock.cs`, `OutboxMigrationTests.cs` | filas viejas, dos orígenes de un módulo, el `Down` que se niega, dos altas a la vez dejan una fila y un 409 |
| B2 | Leer varias filas | `TenantFeatureRow.IsModuleEnabled`, `TenantFeatureSource.IsPlatformManaged()`, `TenantFeatureFilter.cs`, `WhatsAppModulePolicy.cs`, `PlatformFeatureListBuilder.cs`, `TenantFeatureRepository.cs` | `TenantFeatureFilter.cs` | sin filas, una, una manual con una prueba vencida, una aprobada vencida, otra organización; `[FeatureGate]`, `/api/me`, el trabajo de fondo y la pestaña |
| B3 | El resolvedor | `ModuleLimitResolver.cs` y su registro | `FeatureCatalog.cs` | tope de la plataforma, clave desconocida, clase equivocada, claves repetidas, `Effective` |
| B4 | Los topes de la IA | `AssistantModuleLimitContributor.cs`; `AssistantConfigurationPolicy`, `AssistantSettingsBuilder`, `AssistantStateConfigurationBuilder`, `AssistantUsageBuilder` y `AssistantSettingsService` | `AssistantFeatureDefinitionContributor.cs` | los mismos valores que hoy en los tres accesos; bajar el máximo baja el cupo |
| B5 | El plazo de los chats de WhatsApp | `WhatsAppModuleLimitContributor.cs`; la configuración de WhatsApp de la organización y la purga de la B18 | B4 | el `PUT` sobre el tope da 400; bajar el máximo purga con el menor, sin cruzar organizaciones |
| B6 | El test de arquitectura y la regla de los topes | `ArchitectureTests/ModuleLimitArchitectureTests.cs`; `modulos-habilitados.md` y la sección 270 de `configuracion.md` | `CacheArchitectureTests.cs` | cada máximo, declarado y leído solo por su aporte y su pantalla de Plataforma; ningún módulo nombra la configuración de otro; control negativo |
| B7 | Apagar no borra | `Modules/WhatsApp/WhatsAppModuleOffKeepsDataTests.cs`, `Modules/Assistant/AssistantModuleOffKeepsDataTests.cs`; `modulos-habilitados.md` | `AssistantFeatureTests.cs` | apagar da 404 y los datos siguen; prender los muestra igual |

**Front:** sin pasos. Corre `npm run contracts:check` sin regenerar, en la integración.

## Acciones del asistente ✓

Ninguna: la etapa no tiene pantallas que crean, editan o eliminan. La pestaña «Módulos» es de Plataforma, que todavía no tiene acciones (0068).

## Qué verificar al arrancar ✓

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| Base 2 · B18 | `WhatsAppPlatformSettings.MaxOrganizationChatRetentionDays` (3650), `WhatsAppOrganizationSettings.ChatRetentionDays` (90, de 1 al máximo), quién los valida y cómo purga | B5 los pasa por el resolvedor con esos nombres |
| Base 2 · B14 | el cupo del día con la clave de la plataforma sigue leyendo `DailyMessageLimit` de la plataforma | B4 lo pasa por el resolvedor |
| Topes de hoy | los que tienen máximo de la plataforma | tres: el presupuesto del mes y el cupo del día de la IA, y el plazo de los chats |
| Lectores del permiso | quién lee `ITenantFeatureCache`, `ITenantFeatureReader` o `TenantFeatureRow` | los cuatro de B2; uno que sumaron la base 2 o las acciones va a B2 |
| Uso de la IA | `AssistantFinancialCalls` y `AssistantBudgetMonths` siguen como el 2026-11-23 | sin tarea |
| Migraciones | la última migración y el snapshot | B1 arma la suya sobre el `HEAD` del momento |

## Decisiones tomadas

- **Todo lo de producto** lo decidió el usuario en la 0069, el 2026-11-23.
- **Una fila por origen y prendido si alguna está vigente;** el writer sigue con una (0060).
- **El contrato de Plataforma no cambia:** un enum propio con el nombre de esquema de hoy.
- **El resolvedor no suma caché:** lee cada máximo donde se lee hoy, en cada uso (270). Si la etapa del cobro cachea el valor de una organización, lo suma al inventario de [cache](../../rules/cache.md).
- **Con clave propia, la IA no tiene tope de la plataforma:** paga la organización, y sus valores siguen siendo los suyos.

## Fuera de esta etapa

Todo va en la etapa «Módulos y suscripciones», después de la E25:
- escribir `Approved`, `Subscription` y la prueba del sistema;
- qué fila edita Plataforma cuando haya varias, y la pestaña con el estado de todas;
- el valor de Plataforma para una organización y el del plan, dentro del resolvedor;
- el plazo, la exportación y el borrado de un módulo apagado;
- el catálogo, los precios, los pedidos, las suscripciones, Mercado Pago y su factura de ARCA.

## Cómo se cierra ✓

La puerta de la etapa, con la lista de control de `/puerta`, suma lo propio:
- **los tres tests de la 0069:** varias filas por módulo con una prueba vencida que no apaga una manual, el de arquitectura de los topes y el de apagar sin borrar;
- **el contrato igual:** `openapi.json` sin cambios y `contracts:check` del front en verde sin regenerar;
- **la migración** con filas guardadas antes y su `Down`.

No hay galería ni recorrido nuevos, ni prueba con Meta o con un proveedor real.

## Requisitos previos

- La etapa de acciones del asistente, cerrada.
- La segunda parte de la base, cerrada, con la B18 y la B14.
