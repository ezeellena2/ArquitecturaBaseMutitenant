# Quitar el Asistente de una instalación

Usá una copia descartable y dos bases propias: una vacía y otra con la historia original completa aplicada. Los resguardos de copia/Windows, conexiones explícitas y tres vías EF están en [quitar-whatsapp](quitar-whatsapp.md). La retirada física de Asistente, WhatsApp y ambos se ejecuta una sola vez en Integración sobre el par fijo; M2 no la da por aprobada.

## Modelo, fuentes e historia

1. Registrá los commits. Conservá temporalmente `Domain/Assistant` y las tres carpetas `Modules/Assistant`. Quitá en la copia la sentencia Assistant de `Api/Program.cs` y excluí temporalmente `AssistantModelContributor` de `PersistenceModelContributors.Discover()`. Compará runtime DI, DesignTime y bootstrap: permanecen todas las entidades núcleo y, si sigue instalado, WhatsApp.
2. Generá por CLI una migración nueva siguiendo [migración](migracion.md): `dotnet ef migrations add DropAssistant --project src/ArquitecturaBaseMultitenant.Infrastructure --startup-project src/ArquitecturaBaseMultitenant.Api --output-dir Persistence/Migrations -- --environment Development --Logging:EventLog:LogLevel:Default None`. La última histórica de M2 es `20261010203618_SystemRoleAssistantPermissions`; confirmá la del corte antes de exportar SQL entre esa migración y la nueva.
3. Revisá modelo, Up, Down, Designer y SQL. Retirá exclusivamente tablas, relaciones y grants aportados por `AssistantModelContributor`: privadas de conversaciones/mensajes/adjuntos/turnos, operaciones durables/limpieza, configuración y uso/cuotas; globales de historial del operador, configuración/catálogo/aviso y ledgers financieros. Su inventario y configuraciones reales son la lista cerrada: no borres esquemas por prefijo. WhatsApp y núcleo permanecen.
4. Conservá todas las históricas y sus Designers intactos; una base vacía recorre creación y retiro. Revisá grants `mt_app`, RLS, FKs e índices manuales del Down nuevo; la reversión estructural no restaura contenido eliminado.
5. Después del scaffolding quitá Domain/Assistant, Modules/Assistant de Application, Infrastructure y Api, los árboles Modules/Assistant de Application.UnitTests, ArchitectureTests y Api.IntegrationTests, y Domain.UnitTests/Assistant. Retirá opciones/assets exclusivos y el filtro temporal del descubrimiento. El modelo debe coincidir con el snapshot generado.

Los partial retirados contienen DI, inventario HTTP, assertions de modelos concretos, casos exclusivos del inicio y la migración de permisos Assistant. No borres `ModuleIsolationTests`, `AssistantAiBoundaryTests`, la guarda privada de `AssistantPersistenceModelTests`, `RolePermissionCacheIsolationTests`, los casos núcleo de Home/ServiceProviderValidation ni `TestControllerApplicationPart`.

## Permisos persistidos y aprovisionamiento

La migración aditiva `SystemRoleAssistantPermissions` conserva los datos en Down. Retirar constantes no quita tokens persistidos: `TenantSpaceProvisioner` compara exactamente las plantillas de sistema. No relajes esa guarda ni borres roles/asignaciones para aparentar compatibilidad.

En la copia retirás de `Permissions` el grupo Assistant y sus entradas en `OrganizationScoped` y `OrganizationProjectedFromCompany`; esta colección neutral permanece vacía. `SystemRoles.CreateCompanyAdmin` vuelve a usar sólo `Permissions.CompanyScoped`. De `PlatformPermissions` y `Permissions.resx`/`.en.resx` salen sólo entradas Assistant. `LegacyBootstrap`, el resto de catálogos y la proyección neutral permanecen.

La nueva migración de retiro normaliza roles existentes: dentro de cada Business y bajo su scope RLS, elimina sólo `assistant.use` y `assistant.configure` de TenantAdmin Organization sin empresa, y sólo `assistant.use` de CompanyAdmin AnyCompany sin empresa, ambos IsSystemRole. Preservá ID, nombre, alcance, asignaciones, permisos ajenos y sus duplicados. Roles personalizados, nombres reservados no system y espacios Personal permanecen intactos. Copiá loop/scope/restauración de la aditiva sin deshabilitar RLS ni usar BYPASSRLS.

Aplicá esa normalización también en la base vacía: la historia pudo agregar tokens antes del Drop. Comprobá approve/retry real y provisión núcleo en ambas bases, además de IDs/asignaciones/custom intactos. El rollback estructural no reconstruye permisos previos arbitrarios; una compensación debe estar definida y revisada para el corte, sin editar una histórica.

La caché role-permissions:v2 y su prueba neutral permanecen. Migrá antes de aceptar tráfico, reiniciá instancias e invalidá/vaciá las entradas activas del catálogo anterior; SQL no invalida L1. No resucites el namespace legacy.

## Núcleo y verificación

Conservá Phone, LoginLink, outbox, Data Protection, `platform.IdempotencyKeys` con `ReferenceKind`, `ReferenceId`, `ResponseLocation` y los puertos neutrales de replay/proyección. No retires documentos legales v2 publicados ni sus aceptaciones: esa historia es núcleo. Retirar el seed futuro del módulo no borra una versión publicada.

Paridad exige los cinco managers núcleo y resources realmente instalados. Baja exige seis participantes núcleo más instalados: con WhatsApp quedan 7, sin ambos 6. Legal conserva documentos vigentes reales, versión, cultura, texto y privacidad. El inventario HTTP mantiene igualdad exacta; el partial Assistant contiene sus 56 operaciones exclusivas. Regenerá OpenAPI sólo en la copia y ajustá allí referencias documentales exclusivas, conservando guardas y assets cruzados del Front. RealE2ESetup y su ProjectReference a Api.IntegrationTests siguen sosteniendo Email/Google.

El factory DesignTime usa un placeholder: todo database update requiere `--connection <CONEXION_POSTGRES_DESCARTABLE>` explícita. Corré build, suites núcleo, has-pending-model-changes, migraciones de ambas bases, ausencia de rutas/capacidades, Email/Google, legales, recuperación, métodos y aprovisionamiento. Si WhatsApp permanece, sus tablas y pruebas siguen vigentes.

Para ambos, excluí los dos contribuidores mientras existen sus fuentes y generá una única `DropOptionalModules`; luego retirás ambos árboles y filtros temporales. Incluí la normalización anterior y verificá seis participantes y cero resources de módulos. Entregá SQL revisado, inventarios antes/después, hashes y resultados de ambas bases por variante. La evidencia focal M2 no reemplaza esa prueba integral.
