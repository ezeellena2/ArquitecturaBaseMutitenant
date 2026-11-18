# Revisar la arquitectura de un desarrollo

Esta rutina toma un desarrollo entero (el núcleo, WhatsApp, el Asistente, una parte de Formularios…) y lo pasa por los
mismos 36 criterios: los 30 de ArquitecturaBase y un bloque propio del multitenant, «Que no se mezclen las
organizaciones». Pregunta si se mezclan los datos de dos personas o de dos organizaciones, si aguanta varias copias de la
app y mucho volumen, qué pasa cuando falla un tercero, y cómo resuelve la seguridad, los datos, el idioma, las fechas, los
errores y la operación. Es de solo lectura: no cambia código ni reglas. La decisión está en la
[ADR 0059](../decisions/0059-revision-de-arquitectura.md).

No reemplaza la lista de control de la puerta (skill `/puerta`), que mira lo de una etapa, ni la revisión adversarial de
cada tarea riesgosa (`AGENTS.md`, «Forma de trabajo»). Las reglas siguen en `docs/rules/`: esta revisión comprueba que el
desarrollo las cumple y mira lo que ninguna regla cubre, que es cómo se comporta con carga, con varias copias, con fallas y
con entradas hostiles.

## Cuándo

- **Una vez por desarrollo terminado:** el núcleo, WhatsApp y el Asistente antes del primer despliegue (E10); Formularios
  una vez al cerrar el bloque E12–E25, antes de la prueba integral, partido en piezas.
- **Otra vez** cuando el desarrollo cambia mucho o antes de pasar a «Gran escala», comparando con la revisión anterior.
- **Con su costo a la vista.** En ArquitecturaBase cada revisión usó de 11 a 20 agentes, y las cinco, con sus arreglos,
  llevaron el cupo semanal al 98 %. Antes de empezar se mira el cupo (`AGENTS.md`, «El cupo de la semana») y se corre una
  revisión por vez.

## Horizontes

Cada criterio se contesta en dos horizontes. El veredicto dice hasta dónde aguanta el desarrollo; no obliga a construir para
el más alto ([robustez proporcional](../rules/guardado.md)). Qué horizonte se paga lo elige el usuario.

| Horizonte | Qué supone |
| --- | --- |
| **Hoy** | Varias organizaciones en las mismas copias de la Api, con dos o más copias detrás del balanceador (el E10 no fija un máximo de réplicas), la misma base PostgreSQL y el mismo Redis, con el volumen de unas decenas de organizaciones chicas o medianas. Lo que en ArquitecturaBase era «Varias copias» acá es «Hoy». |
| **Gran escala** | Muchas organizaciones, millones de filas en las tablas principales, miles de personas activas en la misma hora y cientos de pedidos o mensajes por segundo en los picos. |

En cualquier horizonte, cada despliegue hace convivir un rato las dos versiones: la migración corre antes, y la versión vieja
atiende hasta que la nueva pasa readiness ([ADR 0060](../decisions/0060-dos-versiones-conviven.md)).

## Veredictos

| Veredicto | Cuándo |
| --- | --- |
| Cumple | Hay evidencia: el código que lo garantiza y, si existe, el test que lo prueba. |
| Con reservas | Se cumple en lo principal, con un hueco acotado (poco frecuente o de efecto chico), o falta evidencia para afirmarlo. |
| No cumple | Hay un camino concreto, con archivo y línea, que falla en ese horizonte en la operación normal o con un efecto serio. |
| No aplica | El desarrollo no tiene esa pieza; se dice por qué. |

- Sin evidencia no hay «Cumple». El nombre de un test solo orienta: se lee su cuerpo.
- Cada «No cumple» y «Con reservas» lleva un escenario concreto: qué hace alguien, de qué organización, con qué datos o qué
  carga, y qué pasa. Se escribe para que lo entienda quien no programa.
- Si el código muestra el camino que falla (un recorrido de toda la tabla, un lock global, un worker que recorre todas las
  organizaciones en cada copia), es «No cumple», aunque cuándo empieza a fallar solo lo diga una prueba real, que se nombra.
  Queda «Con reservas» lo que el código no alcanza a mostrar: cómo responde el proveedor de verdad o qué configura el
  despliegue.
- Lo que acepta una decisión documentada se registra como su costo, citando la fuente; cambiarla lo decide el usuario.

## La ficha del desarrollo

Delimita qué se revisa. La arma un agente leyendo el código; la primera vez la confirma el usuario.

- **Entradas:** rutas HTTP con su acceso (`consumer`, `business`, `platform`, `[PublicSite]` o anónimas), webhooks, workers
  y pantallas del front.
- **Datos:** entidades y tablas con su clase (`tenant`, `public_site`, `engagement` o global), claves de Redis, archivos.
- **Terceros:** proveedores externos, otros módulos y qué se les manda.
- **Reglas:** fichas de `docs/rules/`, la de `docs/features/`, ADR, decisiones de la hoja de ruta y tableros que lo rigen.
- **Pruebas:** carpetas de tests de cada nivel, incluida su parte de `RouteIsolationMatrixTests`.

Un desarrollo grande se parte en piezas con su propia ficha; Formularios, por ejemplo, en catálogos y publicación, carga,
respuestas y planilla, reglas, documentos e IA.

## Los 36 criterios

Cada criterio tiene su pregunta y dónde mirar. «Ya lo cuida» nombra la regla o el test que cubre parte del criterio en todo
el sistema; hay que confirmar que alcanza al desarrollo y que este no lo esquiva.

### 1. Que no se mezclen los pedidos

1. **Estado por pedido.** ¿Queda algún dato de una persona o de un pedido en memoria compartida, donde otro pedido lo vea o lo
   pise? Mirar campos `static` que cambian, singletons con estado y singletons que capturan servicios scoped. Ya lo cuida:
   [capas-y-flujo](../rules/capas-y-flujo.md).
2. **Ediciones simultáneas.** Si dos personas guardan lo mismo a la vez, ¿una pisa a la otra sin enterarse? Mirar la versión
   con 409 ([concurrencia](../rules/concurrencia.md)), los locks ([guardado](../rules/guardado.md)) y las escrituras que leen
   y guardan sin ninguno de los dos. Ya lo cuida: `VersionedContractTests`.
3. **Repeticiones.** Si el proveedor reenvía un webhook, alguien hace doble clic o un worker reintenta, ¿el efecto ocurre una
   sola vez? Mirar la deduplicación por el id del proveedor, las claves de [idempotencia](../rules/idempotencia.md) y los
   únicos de la base que las respaldan. Ya lo cuida: `IdempotentActionsTests`.
4. **Orden.** Si llegan juntos dos eventos de la misma persona o del mismo recurso, ¿se procesan en orden y sin pisarse? Mirar
   los locks por contacto o por recurso, `SKIP LOCKED` y que la reserva no tome lo siguiente de alguien con una salida en
   curso.
5. **Todo o nada.** Si el proceso se corta a la mitad, ¿queda algo a medias? Mirar que cada método que escribe abra un solo
   límite, que no haya HTTP dentro de una transacción y que cada efecto externo se reserve antes y se confirme después. Ya lo
   cuida: `TransactionBoundaryTests`.
6. **Cada uno lo suyo.** ¿Se controla el permiso de la ruta y también que el recurso sea de quien lo pide, dentro de su
   organización y de su empresa? Ya lo cuidan: `[HasPermission]`, `[HasCompanyPermission]`, `[HasPlatformPermission]` y
   `PermissionAuthorizationTests`; la pertenencia a una empresa se revisa a mano.

### 2. Varias copias de la app

7. **Sin estado propio.** ¿Sesiones, caché, archivos temporales, colas y claves de Data Protection viven fuera de la memoria y
   del disco de una copia? Mirar `Channel<T>`, `Path.GetTempPath` y lo que se escribe en disco local. Ya lo cuidan:
   [cache](../rules/cache.md) (Redis como única caché), `IFileStore` y las claves en `platform.DataProtectionKeys`.
8. **Trabajos de fondo.** Con tres copias, ¿cada trabajo que borra o avisa corre una sola vez y se retoma si una copia se cae?
   Mirar cada `PeriodicWorker`: lease, `SKIP LOCKED` o reserva por fila que vence ([concurrencia](../rules/concurrencia.md),
   «Varias copias»). Ya lo cuida: `PeriodicWorkerArchitectureTests` (el ciclo común).
9. **Arranque, apagado y configuración.** ¿Avisa si está viva y lista (`/alive` y `/health`), valida sus opciones al
   arrancar, se apaga sin perder lo que estaba haciendo y toma de variables lo que cambia por entorno? Mirar
   `ServiceDefaults`, `ValidateOnStart`, el token de apagado y las opciones con valores de una máquina.
10. **Despliegue sin corte.** ¿La base migrada funciona con la versión vieja y la nueva a la vez? Mirar las migraciones del
    desarrollo, los valores nuevos de enums guardados como texto, los datos serializados que la app reabre y un `Down` que no
    pierde datos ([migración](migracion.md#convivencia-de-versiones)).

### 3. Volumen

11. **Consultas.** ¿Cada filtro y cada orden tienen índice con `TenantId` adelante, sin una consulta por fila y sin traer
    tablas enteras? Mirar `HasIndex`, los bucles que llaman a repositorios y los `ToListAsync` sin tope.
12. **Paginado y exportaciones.** ¿Todo listado tiene tope y orden estable, y una exportación grande se arma sin cargarla
    entera? Mirar `PagedRequest`, el desempate único y los topes de Excel y PDF
    ([paginado-y-busqueda](../rules/paginado-y-busqueda.md)). Ya lo cuida: `GrowingCollectionContractTests`.
13. **Filtros y búsqueda.** ¿Se filtra solo por campos permitidos, en SQL y antes del tope, y la búsqueda usa su índice
    trigram? Mirar los `SortMap` y los `ILIKE '%…%'` sobre columnas sin índice.
14. **Caché.** ¿Qué se guarda, cuánto dura, cómo se invalida y qué pasa si Redis no responde? Ya lo cuida:
    `CacheArchitectureTests`.
15. **Trabajo pesado fuera del pedido.** ¿IA, PDF, Excel, archivos y llamadas a proveedores van a una cola o a una operación
    durable con estado consultable? Mirar las rutas que esperan a un proveedor y sus plazos.
16. **Filas únicas.** ¿Hay algo que atienda de a uno para todas las organizaciones (un worker en serie, un lock global, una
    espera dentro de un ciclo) y que con muchas haga cola? Mirar los ciclos de los workers y los locks que no dependen del
    recurso.

### 4. Terceros

17. **Plazos y reintentos.** ¿Cada llamada externa tiene timeout, reintenta solo lo seguro de repetir, con espera creciente, y
    deja de insistir con el proveedor caído? Mirar los `HttpClient` tipados y qué métodos reintentan.
18. **Estado incierto.** Si el proceso muere justo después de llamar, ¿se sabe si el efecto ocurrió, sin repetirlo a ciegas?
    Mirar reserva → efecto → confirmación y qué se hace con lo incierto.
19. **Degradación.** Si un proveedor o Redis se cae, ¿el resto sigue andando, para todas las organizaciones, y el usuario
    entiende qué pasa? Mirar las versiones `Disabled*`, los chequeos de salud y los mensajes que ve el usuario.
20. **Límites y costo.** ¿Hay topes de pedidos y de gasto (tokens de IA, mensajes del proveedor), por persona y por
    organización, medidos antes de pagar? Ya lo cuida: [cuotas](../rules/cuotas.md).

### 5. Seguridad

21. **Acceso.** ¿Las sesiones vencen y se revocan, los códigos sirven una sola vez, y los permisos se revalidan también en
    webhooks, workers y en lo que propone la IA? Mirar quién actúa en cada trabajo de fondo y con qué permisos.
22. **Entradas hostiles.** ¿Se valida todo lo que entra? Mirar validadores, la firma y el tamaño de los webhooks, los archivos
    subidos (tipo real y tamaño), el SQL armado a mano, las fórmulas en exportaciones a Excel y los textos que llegan a un
    modelo de IA.
23. **Datos sensibles.** ¿Los logs están limpios y los secretos van cifrados? Mirar los `[LoggerMessage]`, el `ToString()` de
    los contratos y los protectores de Data Protection. Ya lo cuidan: [logs](../rules/logs.md) y
    `SensitiveToStringLoggingTests`.

### 6. Datos

24. **Integridad en la base.** ¿Claves foráneas, únicos y checks respaldan las reglas, también con el borrado lógico? Ya lo
    cuida: `EntityConfigurationTests`; las restricciones se revisan a mano.
25. **Privacidad y respaldo.** ¿Cuánto se guardan teléfonos, mensajes, audios y documentos, cómo se borran, y el backup se
    restaura de verdad? Mirar los workers de retención y [datos-personales](../rules/datos-personales.md).

### 7. Lo que ve el usuario

26. **Idioma.** ¿Cada texto sale de un `.resx` con su `.en.resx`, y quien recibe lo lee en su idioma también por WhatsApp,
    correo, PDF y Excel? Ya lo cuida: `ResourceParityTests`.
27. **Fechas, zonas y números.** ¿Se guarda en UTC con el reloj inyectado, y «hoy» o «vence» se calculan en la zona efectiva
    (cuenta → empresa → organización)? Ya lo cuidan: `BannedSymbols.txt` y `NoManualFormattingTests`.
28. **Errores.** ¿Cada error tiene un código estable y un mensaje que le sirve a quien lo ve, igual en la web y en el canal?
    Ya lo cuidan: `ErrorCodeTests` y `ValidationMessageTests`.

### 8. Operación y evolución

29. **Observabilidad.** ¿Se puede seguir un mismo pedido por la web, el worker y el webhook, y saber quién hizo qué y en qué
    organización? Mirar las trazas, el scope de los logs y la [auditoría](../rules/auditoria.md).
30. **Separabilidad.** Si mañana el módulo fuera un servicio aparte, ¿qué se rompería? Ya lo cuida: `ModuleIsolationTests`.
    La guía de quitar el módulo se contrasta con sus migraciones, también lo que se creó por SQL a mano
    ([modulos](../rules/modulos.md)).

### 9. Que no se mezclen las organizaciones

31. **Lo de cada organización.** ¿Cada tabla nueva tiene su clase y su RLS forzado, y un id de otra organización, persona u
    operador responde 404 igual que uno inventado? Ya lo cuidan: `DataClassificationTests`, `QueryFilterBypassTests` y la
    parte del desarrollo en `RouteIsolationMatrixTests`.
32. **Trabajos de fondo con su organización.** ¿Cada worker que toca datos de una organización entra con `TenantJobRunner` o
    `ITenantScope.Enter`, sin transacción abierta, y uno global no lee tablas `tenant`? Ya lo cuida: `TenantScopeUsageTests`.
33. **Colas, caché y cuotas por alcance.** ¿Lo que sale por el outbox lleva su `TenantId` y usa la configuración de su
    organización (su número de WhatsApp, su tope de IA)? ¿Cada clave de caché y cada cuota tiene su alcance (`t:`, `s:`,
    `u:`, `p:`)?
34. **Archivos.** ¿Su metadata vive bajo RLS, la clave es opaca y una descarga comprueba la organización antes de leer?
35. **Plataforma y lo público.** ¿Un operador entra a una organización solo después de autorizar y registrar el
    `SecurityEvent`? ¿El subdominio muestra solo lo `IPublishedByBusiness` de una organización publicada, y lo compartido
    (`engagement`) lo ve cada lado con su helper?
36. **Módulos por organización.** ¿Una organización sin el módulo no lo usa por ninguna puerta (ruta, webhook, worker,
    asistente), y prenderlo de nuevo no devuelve permisos que nadie quitó sin que se sepa? Ya lo cuidan: `[FeatureGate]` y
    [modulos-habilitados](../rules/modulos-habilitados.md).

## Recorrido

1. **Preparar.** Leer los `AGENTS.md` del desarrollo y su ficha en `docs/features`. Anotar el commit de cada repositorio y su
   `git status --short`: la revisión vale para esos commits. Los revisores leen una copia de cada commit (`git archive`
   extraído dentro de `.local/`), para no ver lo que otras sesiones tienen a medias; se borra al terminar.
2. **Ficha.** Armarla y, la primera vez, confirmarla con el usuario.
3. **Revisar por bloque.** Un revisor por bloque, con la ficha y esta guía; en un desarrollo grande, los bloques largos se
   reparten entre dos. Solo leen: no editan, no compilan, no corren tests ni levantan la app. Citan archivo y línea, y nombran
   un test solo después de leer su cuerpo.
4. **Verificar.** Cada «No cumple» y «Con reservas» pasa por un verificador que recibe el escenario y el código, no la
   conclusión, e intenta refutarlo. Si lo refuta, el veredicto se corrige. Lo que encuentra fuera de los criterios, como un
   bug, se registra aparte.
5. **Contrastar.** Un revisor lee el informe entero contra el código, para corregir lo que los demás leyeron mal.
6. **Consolidar.** La matriz de los 36 criterios en los dos horizontes; los hallazgos, cada uno con su escenario, evidencia,
   arreglo, esfuerzo y el horizonte que destraba; lo que está bien; y lo que se puede controlar con código, propuesto como test
   de arquitectura.
7. **Registrar.** En `docs/reviews/AAAA-MM-DD-arquitectura-<desarrollo>.md`, con los commits revisados. Al usuario se le
   preguntan solo las decisiones de producto y de horizonte, de a una y con la recomendada primero. Los arreglos que elija se
   anexan al plan del área; si sale una regla nueva, va a su ficha.

## Pedido reutilizable

> Revisá la arquitectura de <desarrollo> siguiendo `docs/guides/revisar-arquitectura.md`. Armá la ficha (la primera vez,
> confirmala conmigo), contestá los 36 criterios en los dos horizontes con evidencia de archivo y línea, verificá cada «No
> cumple» y «Con reservas» con un verificador que intente refutarlo, y registrá la matriz y los arreglos en `docs/reviews`.
> Es de solo lectura: no cambies código ni reglas.
