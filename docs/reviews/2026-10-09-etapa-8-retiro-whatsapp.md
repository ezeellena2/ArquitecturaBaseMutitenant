# E8: prueba efectiva de retiro de WhatsApp

Prueba del Back en una copia descartable: **2084/2084 tests correctos, exit 0**, build sin advertencias, dos historias de base y reversión estructural verificadas. Este informe acredita la remoción técnica; no acredita Meta real ni cierra E8. La ejecución sigue la [receta de retiro](../guides/quitar-whatsapp.md).

## Fuente y aislamiento

- Commit de origen: `605be253381f0faf563765fd8ce4d5b65230e916`; código final de Parte 2a: `8e0bb63c1638b7e5a5e52d89c28de91562b24cf6`.
- Copia: `.local/module-removal/effective-605be253/source`, creada con `git archive`, sin secretos, archivos de datos ni directorios bin/obj del checkout compartido. `UserSecretsId` retirado de los proyectos de la copia antes de compilar. No se arrancó Aspire.
- PostgreSQL `postgres:18.3` propio: contenedor `mt-e8-removal-605be253`, ID `8af88fcf6b9a22d4e62c7dc46dd7e3543cd0cd3eb767d9b8a89457c38bb23ef1`, etiqueta `codex.task=e8-removal-605be253`, puerto de loopback `60419`, sin volúmenes compartidos. Bases ICU `es-AR` `e8_existing` y `e8_empty`, roles `mt_owner`/`mt_app` propios de ese contenedor.
- Todos los builds y tests ocurrieron dentro de la copia. El Back principal conservó su módulo y sus DLL para el runner del Front. El Front compartido no se editó: para las guardas cruzadas se archivaron fuentes y documentos de `31ee77edce9b9ac2cd958d2716064f120c729616` en una copia de referencia, sin ejecutar allí su runner. El commit se comprobó en el comentario de ambos ZIP de `git archive`.
- El contrato compartido conserva SHA256 LF `cf80064358a63daa3be361dc4a5308d0ee52e27a31824953061eb1d44b77727d`. La copia regenerada conserva las **119 rutas del núcleo** y pierde sólo el webhook y metadata de las contribuciones WhatsApp (descripciones, ejemplos y extensiones en rutas Phone). Se verificó que no cambian las estructuras de esas rutas ni los **183 esquemas DTO comunes**; sólo desaparece `WhatsAppWebhookHttpRequest`, sin schemas nuevos. Los tres puertos neutrales originales coinciden byte a byte.

## Migración, datos y otra contribución

Se aplicó toda la historia original a `e8_existing` y se prepararon datos por HTTP, OTP/OIDC, repositorios y outbox reales. La preparación terminó **1/1, exit 0**. Después se retiró la composición de Program y se excluyó temporalmente el contribuidor WhatsApp del descubrimiento neutral, también usado por DesignTime y bootstrap.

La migración nueva de la copia es `20261009154856_DropWhatsApp`. Su `Up` sólo elimina `identity.WhatsAppCodeQuotas`, `identity.WhatsAppMessages` e `identity.WhatsAppContacts`; no contiene Drops del núcleo. El rango SQL del retiro comienza en la última migración histórica, `LoginLinks`. Un primer rango desde `DurableWhatsAppInbound` incluía la creación de `LoginLinks`: se detectó al revisar el SQL y **no se ejecutó** ese rango.

Las **128 migraciones históricas y designers** coinciden byte a byte con el archivo del commit de origen. Sólo se agregaron DropWhatsApp y su designer y se actualizó el snapshot. Una vez generada la migración se retiraron las fuentes, opciones, resources y soporte exclusivo, y se quitó la exclusión temporal del descubrimiento.

Se aplicó el retiro a `e8_existing`. Inmediatamente antes/después, sin escrituras intermedias del host, los conteos y hashes del contenido completo de estas tablas fueron idénticos:

| Tabla | Filas | MD5 de representación ordenada antes = después |
|---|---:|---|
| identity.AspNetUsers | 2 | `95d0f78fba49854e79fe5baf28f1f59c` |
| identity.LoginMethods — incluye Email y Phone | 3 | `91f80961e5ef8687a79a5a3ce0377cf7` |
| identity.LoginLinks | 1 | `d1ff0d0c4f372e23a3d77bac7a24a727` |
| platform.OutboxMessages — payloads cifrados | 3 | `7969033da2b84d53dac7e021f0693cca` |
| platform.DataProtectionKeys | 1 | `d9b9245ba7e8b365c0927d0a79521328` |
| identity.TechnicalRecords — contribución técnica de test | 1 | `a6e65c0b15439064697ce68e8c95afec` |

En `e8_empty` se ejecutó toda la historia, incluidas creación y retiro de WhatsApp. Se compararon columnas, constraints, índices y políticas RLS de ambos esquemas del núcleo: **idénticos**, SHA256 del inventario `04ab3d7e1bda60d9c7e17f30e261f1fc9a41ac1b9fd5d2c3e393486b72034022`. La tabla TechnicalRecords se excluye de esa comparación porque sólo está físicamente en la base existente.

También se revisó y ejecutó `Down` en la base descartable vacía, seguida nuevamente de `Up`. EF no restituye los grants manuales: se agregaron los cuatro a la migración nueva. La consulta inicial agrupada dio `t|t|t`, pero acredita algún permiso por tabla, según [PostgreSQL 18](https://www.postgresql.org/docs/18/functions-info.html#FUNCTIONS-INFO-ACCESS-TABLE). Después de la suite se repitió `Down` y se verificó cada combinación tabla/permiso: **12/12 verdaderas**, con una guarda SQL que falla si falta cualquiera; `Up` se reaplicó con exit 0. Ambos inventarios finales del núcleo/RLS conservan el SHA256 anterior, normalizados a LF. Es reversión estructural con tablas vacías, no recuperación de datos WhatsApp eliminados.

## Adaptaciones explícitas de la copia

No fue «borrar carpetas y listo». Además de las cuatro carpetas productivas, se retiraron los tests exclusivos, probes del módulo y fixture privada WhatsAppBrowser, el modo `--whatsapp-browser` y su referencia del tool al proyecto de integración. Se conservaron Phone, LoginLink, OIDC, outbox y puertos neutrales; los comandos Email/Google del tool permanecen.

Los siguientes ajustes afectan solamente soporte de tests/documentos de la instalación retirada:

- Se quitaron las entradas del webhook en `AccessDeclarationTests`/`TestControllerApplicationPart` y los registros WhatsApp en `ApplicationServicesTests`, `ModuleControllersTests` y `DependencyInjectionTests`. La prueba exclusiva de registro duplicado de ese módulo se retiró; las guardas del núcleo siguen ejecutándose. El probe prohibido de controller usa ahora `IRoleRepository` y conserva la misma assertion de rechazo.
- `OutboxDeletionRaceTests` conservó sus **tres tests y assertions**. `Host`, `NowUtc` y `TransactionAsync`, antes alojados junto a `WhatsAppPrivacyFixture.SeedAsync`, se conservaron como `Support/OutboxPrivacyFixture`, sin el seed ni opciones WhatsApp.
- `PlatformOrganizationPhoneNoticeTests` conserva los **cuatro casos Email** de acciones reales de Plataforma con Phone primario y Email de respaldo, incluyendo replay sin duplicar y protección del motivo interno; se retiraron sólo los cuatro casos WhatsApp.
- `Etapa8ContractTests` conserva **siete casos neutrales**: Phone sin canal, enlace desconocido sin sesión y rechazo de Email+Phone. Se retiraron las expectativas de webhook/OpenAPI exclusivas del módulo.
- `LoginLinkIssuerTests` conserva el rechazo del operador; se retiró sólo la assertion del texto del bot perteneciente a los resources borrados.
- `LoginLinkTests` conserva sus **16 casos de reglas y seguridad**, con un canal técnico `removal-proof` únicamente en su host privado. Implementa el puerto neutral de disponibilidad Phone, disponible/apagado; cualquier intento de emitir/encolar falla antes de transporte. No registra `whatsapp`, no se instala en el host normal ni en las pruebas de ambas bases, y no sustituye Meta.
- Paridad de resources exige ausencia de resources WhatsApp y mantiene las comparaciones de claves/placeholders para los módulos restantes. No exige que exista siempre algún módulo.
- `AccountDeletionParticipantsTests` verifica los seis nombres exactos de participantes del núcleo; se retira el séptimo, exclusivo WhatsApp. Las assertions de limpieza, bloqueo por último administrador, privacidad e idempotencia de cada participante se conservan.
- `ExplicitRouteInventoryTests` retira únicamente GET/POST del webhook y conserva la comparación exacta del inventario restante.
- En los documentos de la copia se quitaron referencias a tests exclusivos borrados. Para las guardas de enlaces/CI/OIDC entre repos se conservó un snapshot del Front de sólo lectura. No se relajaron los guards, assertions de seguridad, reglas del núcleo ni el inventario de migraciones.
- `ApiFactory` de la copia desactiva Google por defecto y los tests del proveedor declaran credenciales sintéticas; `SpaHostingTests` usa `UseDevelopmentTestOwner` al abrir Development. No se copió el secreto Google de la máquina. El build regenera el contrato de la copia sin webhook, manteniendo intacto el contrato del Back compartido.

El primer build tras retirar fuentes detectó el helper del outbox dentro de WhatsApp. Una primera suite se interrumpió tras detectar los presupuestos anteriores de recursos/canal instalado y referencias documentales incompletas del snapshot Front. La segunda se interrumpió por tres casos Development (SPA/OpenAPI) que requerían el secreto Google de la máquina, ausente en esta copia. La tercera terminó con **2084 total, 2082 correctas, 2 errores, 0 omitidas, exit 2, 15m20.137s**: conteo fijo de siete participantes de baja e inventario de rutas del webhook retirado. La copia verifica ahora los seis participantes del núcleo por nombre y conserva el inventario exacto sin esas dos rutas. El focal de baja pasó **1/1, exit 0**, 25.517s. Sus logs se conservan y **no cuentan como suites completas verdes**. Los intentos de preparar la fixture detectaron configuración OIDC/HTTPS incompleta y fueron corregidos sólo en el probe descartable. La revisión de grants se repitió mediante archivo SQL después de un error de quoting de una consulta; ese error no afectó bases compartidas.

## Verificación

Comandos ejecutados desde la raíz de la copia:

```text
dotnet build ArquitecturaBaseMultitenant.slnx -m:1 -p:BuildInParallel=false
dotnet ef migrations add DropWhatsApp --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output-dir Persistence/Migrations -- --environment Development --Logging:EventLog:LogLevel:Default None
dotnet ef migrations script LoginLinks DropWhatsApp --no-build --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output ../drop.sql
dotnet ef migrations script DropWhatsApp LoginLinks --no-build --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output ../down.sql
dotnet ef migrations script --idempotent --no-build --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output ../all-after-removal.sql
dotnet ef migrations has-pending-model-changes --no-build --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api
dotnet test --no-build
```

Los scripts se revisaron antes de aplicarlos mediante `docker cp` y `docker exec ... psql -U mt_owner -d <base propia> -v ON_ERROR_STOP=1 -f <script>`. La copia guarda SQL, logs, manifiesto y comprobaciones; los comandos EF de revisión usan la configuración de ambiente/EventLog de la receta.

- Build final de la copia: **exit 0, 0 advertencias, 0 errores**, 12.56s.
- `has-pending-model-changes`: **exit 0**, sin cambios pendientes tras la migración de retiro.
- Architecture: **165/165, exit 0**, incluye dependencia IL del núcleo hacia módulos y todo el arnés documental de la copia.
- Application: **536/536, exit 0**; LoginLink: **16/16, exit 0**.
- Hosting: **20/20**, OpenAPI: **3/3**, arranque: **5/5**, avisos Email con respaldo: **4/4**; todos exit 0, sin omitidos. La [receta precisada](../guides/quitar-whatsapp.md) quedó en `734fe9096ff74ab71787b9b6dd71f9216e9ea858`.
- Participantes de baja/privacidad: **1/1**, 25.517s; inventario de rutas: **3/3**, 17.207s; ambos exit 0, sin omitidos.
- Ambas bases: **2/2, exit 0**, 25.411s, con runtime/DesignTime/bootstrap iguales, con y sin contribución técnica adicional, configuraciones aplicadas una vez. La contribución adicional es explícita y exclusiva de test; no amplía el descubrimiento de assemblies productivos.
- En cada base se recorrieron Email/OIDC, registro personal/empresarial, legales, verificación de un Email adicional y recuperación por código. Google se verificó con persistencia y el doble de proveedor habitual de integración, sin red Google. El host normal no registra canales/capacidades/rutas WhatsApp: Phone devuelve `Auth.LoginMethod.ChannelUnavailable` sin cookie, el webhook 404 sin sesión y el inventario de entidades no incluye WhatsApp.
- Cuarta suite completa de la copia adaptada al retiro: **2084/2084, 0 fallos, 0 omitidos, exit 0**, 14m54.120s. API 14m53.632s, Architecture 24.290s, Application 4.215s y Domain 1.827s. Se ejecutaron los cuatro proyectos completos, sin filtros.

Evidencias locales bajo `.local/module-removal/effective-605be253/`: `manifest.json`, `historical-migration-hashes.json`, `source-changes.json`, SQL revisado, `rows-before.txt`/`rows-after.txt`, inventarios iniciales/finales, contratos comparados, auditoría `down-privileges-individual.txt`, `removed-build-final.log`, focales `removed-*-focal.log`, suites interrumpidas `removed-test-full.log`/`removed-test-second-interrupted.log`, tercera roja `removed-test-third-failed.log` y final `removed-test-final.log`. Los datos de estas bases son sintéticos y los logs publicados sólo registran conteos/hashes, nunca códigos, enlaces ni claves.

El inventario final registra **28 archivos modificados, 5 agregados y 125 retirados** respecto del commit de origen. `removed-source-final.zip` conserva 2165 archivos, sin bin/obj, secretos ni datos; SHA256 `94782f13ed77c4300a54f2f9ffa3928b6a5699ca552e55f9c93663221f07d8da`. `artifact-hashes.json` permite comprobar las evidencias. El contenedor propio fue detenido y eliminado después de verificar otra vez su ID, nombre y etiqueta; se conservaron los archivos de la copia.

## Límites de esta entrega

La migración Drop existe exclusivamente en la copia; **no se incorpora ni se ejecuta en main**. La prueba efectiva no altera el contrato de Parte 1 del Back compartido. Integración conserva el recorrido real Front/Back, Meta, las entradas del árbol pendientes de autorización y el cierre de E8. Ninguna prueba de esta copia acredita entrega por Meta ni «documentación en cero».
