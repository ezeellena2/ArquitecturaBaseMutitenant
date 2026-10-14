# Quitar WhatsApp de una instalación

Ejecutá esta receta en la copia descartable de la puerta, con una única base propia vacía. El retiro de este módulo se verifica una vez en su etapa: build, tests núcleo e ingreso.

Antes de retirarlo de una instalación con usuarios, cada cuenta cuyo único método disponible sea Phone necesita verificar un método alternativo ([ADR 0033](../decisions/0033-metodos-de-ingreso.md)). Phone, Email, Google, LoginLink y sus puertos pertenecen al núcleo. Apagar `PhoneNumberId` conserva el modelo instalado: no representa una retirada física.

## Preparar la copia y los modelos

Registrá los commits del back y del front. Copiá fuentes, proyectos, migraciones y archivos de referencia del Front que consumen las guardas cruzadas, en un directorio corto propio bajo TEMP. Usá `git archive <commit>` para copiar exclusivamente archivos versionados del corte. Conservá las skills versionadas de `.agents/skills/` que enlazan `AGENTS.md` y las fichas: las guardas del núcleo comprueban esos destinos aun sin módulos. Excluí `.git`, `.local`, `.codex`, configuración privada no versionada de `.agents`, secretos, `bin`, `obj`, `node_modules`, conexiones y directorios de datos. No uses bases productivas. Antes de cualquier borrado/movimiento recursivo en Windows, resolvé cada ruta absoluta y comprobá que sea descendiente del directorio **exacto** de esta copia; usá `Remove-Item`/`Move-Item -LiteralPath` en PowerShell de extremo a extremo, sin espejado con borrado ni pasar rutas a `cmd`.

Prepará una base PostgreSQL descartable vacía y aplicá la historia completa del corte. Conservá temporalmente `Domain/WhatsApp` y las tres carpetas `Modules/WhatsApp` para comparar el snapshot EF anterior con el modelo nuevo. Sólo en la copia, quitá la sentencia WhatsApp de `Api/Program.cs` y excluí temporalmente `WhatsAppModelContributor` de `PersistenceModelContributors.Discover()`. La exclusión debe alcanzar **runtime DI, DesignTime y DatabaseBootstrap**; retirar sólo DI no alcanza. No se incorpora un filtro productivo ni una excepción permanente al núcleo.

Construí y compará las tres vías reales: deben faltar exclusivamente las entidades del contribuidor retirado y conservarse las del núcleo y otros módulos. Desde la E11, WhatsApp declara:

- en `identity` (globales): `WhatsAppContact`, `WhatsAppMessage`, `WhatsAppCodeQuota`, `WhatsAppCodeQuotaIp`, `WhatsAppChannel`, `WhatsAppChannelUsage` y `WhatsAppTemplateState`;
- en `platform`: `PlatformWhatsAppChat`, `PlatformWhatsAppChatMessage`, `PlatformWhatsAppBot`, `PlatformWhatsAppTeam`, `PlatformWhatsAppTeamNotice`, `PlatformWhatsAppTemplate` y `WhatsAppRate`;
- en `tenant` (con RLS de organización): `WhatsAppChat`, `WhatsAppChatMessage`, `WhatsAppCustomer`, `WhatsAppBot`, `WhatsAppTeam`, `WhatsAppTeamNotice`, `WhatsAppTemplate`, `WhatsAppBroadcast` y `WhatsAppBroadcastRecipient`;
- en `engagement` (dato compartido, RLS de partes): `WhatsAppConversationLink` y `WhatsAppLinkedMessage`.

## Generar y revisar una migración nueva

Seguí [migración](migracion.md), dentro de la copia:

```text
dotnet ef migrations add DropWhatsApp --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output-dir Persistence/Migrations -- --environment Development --Logging:EventLog:LogLevel:Default None
dotnet ef migrations script <ULTIMA_MIGRACION_DEL_CORTE> <ID_GENERADO_DropWhatsApp> --idempotent --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output drop-whatsapp.sql -- --environment Development --Logging:EventLog:LogLevel:Default None
```

Antes de agregar DropWhatsApp, obtené el ID real de la última migración del corte desde `Persistence/Migrations` o con `dotnet ef migrations list --no-connect` usando los mismos proyectos y ambiente. Reemplazá `<ULTIMA_MIGRACION_DEL_CORTE>` por ese ID completo para exportar sólo el retiro; no uses un ID de otro corte ni el de la nueva DropWhatsApp. Generá la migración y Designer por CLI; no escribas el snapshot a mano ni alteres las históricas o sus Designers.

Revisá `Up`, `Down`, snapshot y SQL completos. Sólo se retiran las 25 tablas de esas entidades (`identity.WhatsApp*`, `platform.PlatformWhatsApp*`, `platform.WhatsAppRates`, `tenant.WhatsApp*` y `engagement.WhatsApp*`) con sus relaciones, políticas RLS, índices de búsqueda (`IX_*_Search_*`, creados por SQL) y grants exclusivos. Los permisos `whatsapp.*` y `platform.whatsapp.*` viven en el catálogo del núcleo desde K1 (`Permissions.cs`, `PlatformPermissions.cs`, sus resources y el seed de roles): en la copia se quitan de esos archivos y DropWhatsApp agrega el SQL que saca esas claves de `tenant."Roles"` y de `platform."PlatformPermissionAssignments"` (las sumó la migración de datos `WhatsAppAttentionPermissions`), con el `SecurityEvent` de los operadores como en aquella. Conservá LoginMethods, LoginLinks, cuentas, outbox, Data Protection, legales y todas las tablas de otros módulos. Las columnas durables `ReferenceKind`, `ReferenceId` y `ResponseLocation` de `platform.IdempotencyKeys` son neutrales y permanecen.

Las históricas describen las entidades por nombre y deben compilar sin las clases actuales. Revisá grants/RLS del `Down` nuevo: EF no reconstruye el SQL manual de creación. Si hay reversión estructural, reponé grants `mt_app`, políticas, índices y relaciones pertinentes. El `Down` no recupera datos eliminados. El factory DesignTime tiene un placeholder de localhost para describir el proveedor; todo `dotnet ef database update` requiere `--connection <CONEXION_POSTGRES_DESCARTABLE>` explícita. No uses el placeholder ni una conexión heredada del equipo y no publiques credenciales en logs.

## Retirar fuentes y bindings exclusivos

Después del scaffolding, quitá sólo en la copia:

- `Domain/WhatsApp` y `Modules/WhatsApp` de Application, Infrastructure y Api, incluidas opciones, resources propios, la tabla de tarifas (`Infrastructure/Modules/WhatsApp/ReferenceData`) y sus workers (cola de entrada, retención, «Chats esperando», sincronización de plantillas y difusiones).
- Los artículos de ayuda del módulo, que viven en el árbol del asistente: `Infrastructure/Modules/Assistant/Help/Articles/whatsapp/` y sus entradas `whatsapp.*` de `index.json`.
- La excepción de `meta-rates-*.json` en `ReferenceDataHardcodeTests`, que nombra solo esa carpeta.
- `Domain.UnitTests/WhatsApp` y `Modules/WhatsApp` de Application.UnitTests, ArchitectureTests y Api.IntegrationTests. Estos árboles contienen bindings partial de DI, rutas/acceso anónimo, probes de error/cuota, casos Phone de avisos/recuperación y el caso de cambio de países de enlaces. Las otras partes de las clases conservan las pruebas neutrales.
- `Api.IntegrationTests/TestFeatures/WhatsAppBrowser` y sus pruebas exclusivas `WhatsAppBrowserFixtureTests`/`WhatsAppBrowserControlTests`; exclusivamente la rama `--whatsapp-browser` y sus referencias CLR en `tools/ArquitecturaBaseMultitenant.RealE2ESetup/Program.cs`.
- En `tools/ArquitecturaBaseMultitenant.RealE2ESetup/RecorridoHost.cs`, retirás el `using` de `TestFeatures.WhatsAppBrowser`, la llamada `RecorridoWhatsAppModule.Configure(builder)`, la construcción `new WhatsAppFileBridge(...)` y `bridge.TryExecuteAsync(...)`. Conservá `RecorridoHost`, `RecorridoApiHost`, `TestFeatures/Recorrido`, `ApiFactory.ForIsolatedDatabase`, la preparación E7, el certificado efímero, el bombeo de correo y todos los comandos núcleo. Compilá también RealE2ESetup en la copia para comprobar estos bindings.

**Conservá el ProjectReference de RealE2ESetup a Api.IntegrationTests.** `RecorridoHost`, `ApiFactory` con base externa y `E7Preparation` sostienen el recorrido núcleo Email/Google. Quitá la exclusión temporal del descubrimiento EF: el contribuidor ya no existe y el modelo debe seguir coincidiendo con el snapshot nuevo.

## Guardas que permanecen

- `ModuleIsolationTests` sigue escaneando assemblies productivos; sus controles sintéticos usan prefijos explícitos y funcionan sin módulos. No agregues whitelists ni Skip.
- `ExplicitRouteInventoryTests`, `AccessDeclarationTests` e `IdentityAccessTests` comparan listas cerradas; sus partial quitables (`Modules/WhatsApp/IdentityAccessTests.WhatsApp.cs` aporta los lectores globales del módulo) aportan exactamente las entradas del módulo. `WhatsAppHelpArticleTests` sale con `Modules/WhatsApp`.
- `IActiveMemberPhoneReader` (Application/Infrastructure de identidad) es un puerto neutral del núcleo y permanece. `Etapa8ContractTests` conserva Phone sin canal, enlaces sin prueba y todo el inventario HTTP E8 del núcleo.
- `OutboxDeletionRaceTests` conserva sus pruebas y `OutboxPrivacyFixture`: reloj 2030-10-08, workers retirados, UoW, locks, cancelación, lease y preservación de Sent, sin seed WhatsApp.
- `PlatformOrganizationPhoneNoticeTests` conserva cuatro casos Email; `PlatformRecoveryDestinationConflictTests`, cuatro Email/Google en es/en y snapshots reales. Phone sale sólo con los partial de WhatsApp. `Support/PendingMethodJourney` y las pruebas Email de posesión permanecen.
- `LoginLinkTests` conserva identidad, lockout, baja, cookies y replay mediante Phone técnico sólo de test; `Host(false)` usa la instalación normal sin ese canal. La instalación retirada normal debe rechazar Phone. Esto no acredita transporte Meta.
- `AccountDeletionParticipantsTests` exige los participantes reales del núcleo y exactamente los módulos instalados, con privacidad e idempotencia. `ResourceParityTests` conserva los managers y placeholders del núcleo más los resources instalados, incluso vacío.
- `RolePermissionCacheIsolationTests` conserva key legacy/current, invalidación de `roles.manage` y RLS entre organizaciones. La guarda privada de `AssistantPersistenceModelTests` permanece en núcleo; sólo el mapa concreto instalado está en `Modules/Assistant`.

El build regenera OpenAPI sólo en la copia. Ajustá allí las referencias documentales a tests exclusivos retirados y conservá las referencias del Front que necesitan las guardas cruzadas, sin editar el Front compartido. Los hosts mantienen Google deshabilitado por defecto y operador sintético de `UseDevelopmentTestOwner`; los tests Google fijan sus opciones propias.

## Verificación final de Integración

Corré build, suites núcleo y `has-pending-model-changes`. Aplicá Drop a la base ya migrada y toda la historia con Drop en la vacía. En ambas verificá Email, Google, registro personal/empresarial, legales, recuperación y gestión de métodos; cero rutas/capacidades/canales WhatsApp y Phone no disponible. Compará las tres vías EF y verificá que el otro contribuidor no se aplique dos veces ni pierda tablas.

El retiro no relaja permisos ni guardas de los módulos que permanecen. Las salidas quedan en .local/ y en el plan se registra sólo la tabla final.
