# Cinco correctivos focales devueltos por la puerta E8

Fecha: 2026-10-10. Fuentes base: `853b5aed3fb9df1f22f5fe5287e4bbb6fbd437dc` (main, después de A2/M5). La puerta fija de Integración había informado 2759/2767, ocho fallas y cero omitidos: cinco corresponden a esta entrega; las otras tres de arnés/documentación siguen con Coordinación. Este informe no cierra E8 ni E8b ni acredita la puerta integral.

## Cambios y decisiones tomadas

1. [ActiveTenantAdminEmail](../../src/ArquitecturaBaseMultitenant.Application/Models/Tenancy/ActiveTenantAdminEmail.cs) usa `Domain.ValueObjects.Email` y conserva `ToString` redactado. [UserTenantAccessReader](../../src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/UserTenantAccessReader.cs) materializa una proyección anónima `(UserId, Value)`, convierte fuera de EF y ordena por `Email.Value`. Conserva los filtros de método verificado, organización/membresía/cuenta activas y propiedad del correo administrado, y las dos lecturas en lote; no agrega N+1. La fixture de [Settings de Application](../../tests/ArquitecturaBaseMultitenant.Application.UnitTests/Modules/Assistant/AssistantSettingsTests.cs) crea su correo sintético con `Email.Create`. [UserTenantAccessReaderTests](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy/UserTenantAccessReaderTests.cs) ejerce destinatarios reales, correo pendiente excluido, cuentas inactivas, membresía inactiva, organización pendiente/cerrada y redacción.
2. [AssistantContractTests](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/AssistantContractTests.cs) prueba defaults con una factory nueva. Primero registra la cuenta para ejecutar el seed real: `InitializeAsync` migra, pero `Testing` no siembra automáticamente. Comprueba `PersonalEnabled=false`, clave cifrada ausente, chat personal 404, configuración de plataforma 200 sin clave, uso 403 y capability no disponible en `/api/me`. Conserva AvailablePolicy explícita, stubs 501 y operaciones privadas durables. No modifica la política de disponibilidad ni implementa State.
3. [AssistantNoticeMigrationTests](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Modules/Assistant/AssistantNoticeMigrationTests.cs) selecciona exactamente el sufijo `_AssistantNoticeUserVersion`, encuentra su índice y predecesora, y ejecuta Up/Down/Up con esos IDs en base descartable. Conserva evidencia de primer timestamp, desempate UUID, acceso original, versión 2 independiente e índice único. No modifica historia, designers ni snapshot.
4. [CompaniesTests](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization/CompaniesTests.cs) espera exactamente la proyección neutral `Permissions.OrganizationProjectedFromCompany` y, para la única empresa, `CompanyScoped` más esa proyección, en orden. Conserva 403 de otra empresa y roles organizacionales, y roles asignables limitados. No incorpora símbolos concretos del módulo: al retirarlo, la colección neutral puede quedar vacía según la receta.
5. [PlatformOperatorPersistenceTests](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/PlatformOperatorPersistenceTests.cs) ancla el upgrade a `20261004005437_PlatformOperatorPermissions`. Declara los nueve strings históricos Owner y cuatro Support de ADR 0042; prepara Down con esos nueve y conserva roundtrip, ausencia de organizaciones y pruebas de transacción/unicidad. El catálogo vivo y la migración posterior M1 no deciden la expectativa histórica.

## Verificación focal

Todas las ejecuciones finales usan `--no-build` después de builds correctos de los respectivos proyectos. Las pruebas HTTP, reader y migraciones usan ApiFactory + PostgreSQL Testcontainers descartable, con correos de prueba en archivos y dobles de proveedor existentes. No se usó Aspire ni proveedor real.

| Filtro final | Correctos | Fallas | Omitidos | Duración runner | Log en `.local/reviews/e8b-contract/` |
|---|---:|---:|---:|---|---|
| EmailPropertyTests | 3 | 0 | 0 | 5.078 s | `e8-five-email-green.log` |
| Application Modules.Assistant.AssistantSettingsTests | 6 | 0 | 0 | 4.452 s | `e8-five-settings-unit.log` |
| Contracts.AssistantContractTests | 4 | 0 | 0 | 62.056 s | `e8-five-contracts.log` |
| AssistantNoticeMigrationTests | 1 | 0 | 0 | 27.826 s | `e8-five-notice-migration.log` |
| PlatformOperatorPersistenceTests | 3 | 0 | 0 | 33.027 s | `e8-five-operators.log` |
| CompaniesTests.A_company_admin_without_organization_roles_gets_its_company_permissions_and_assignable_roles | 1 | 0 | 0 | 31.464 s | `e8-five-company-admin-2.log` |
| UserTenantAccessReaderTests | 2 | 0 | 0 | 23.001 s | `e8-five-recipients.log` |
| API Modules.Assistant.AssistantSettingsTests | 8 | 0 | 0 | 94.902 s | `e8-five-settings-http.log` |

Resultado final: **28 tests distintos correctos, cero fallas y cero omitidos** en estos filtros. Sin repeticiones de cohortes verdes. Contando el rojo previo hay 31 ejecuciones de casos: 30 correctos observados y una falla histórica del guard; dos correctos del rojo ya están incluidos en los 28 distintos finales.

Builds finales seriales, cero advertencias y cero errores: ArchitectureTests 40.55 s (`e8-five-architecture-build-2.log`), Application.UnitTests 7.62 s (`e8-five-application-build.log`), Api.IntegrationTests 52.32 s (`e8-five-integration-build.log`). El último build incluye las fuentes finales del contrato y la verificación del reader.

TDD real del guard: build baseline correcto (53.69 s), `EmailPropertyTests` dio tres casos, dos correctos y una falla de propiedad string (`e8-five-email-red-authorized.log`, exit 2), antes de cambiar el tipo. Después, 3/3 verdes sin whitelist ni cambios al guard. Los dos correctos del rojo repiten casos del verde: no son casos distintos adicionales.

Intentos históricos preservados: `e8-five-email-red.log` falló al compilar, sin casos; `e8-five-email-real-red.log` falló por IPC NamedPipe bloqueado en sandbox, sin casos; `e8-five-company-admin.log` seleccionó cero casos por falta del prefijo wildcard en el filtro de método (exit 8). Ninguno cuenta como rojo de conducta ni como verde. `e8-five-architecture-build.log` tuvo un CS1529 por ubicación de un using durante la edición; se corrigió antes del build final. La generación de OpenAPI dentro del sandbox registró acceso denegado a DataProtection, pero los builds terminaron correctos y los bytes exportados conservaron el hash esperado; los contratos autenticados pasaron en las ejecuciones autorizadas con IPC.

## Comandos literales y evidencia

Cada archivo `e8-five-<nombre>.command.json` conserva comando literal, argumentos cuando aplica, UTC de inicio/fin y código de salida, junto al log raw homónimo. El [inventario local](../../.local/reviews/e8b-contract/e8-integral-five-focal-files.json) contiene exclusivamente las ocho fuentes y este informe, con ruta, acción, SHA-256 raw y cantidad de bytes, commit base, Git blob SHA y SHA-256 de sus bytes exactos. También incluye los logs/metadatos y sus hashes. El SHA físico conserva LF/CRLF; el SHA del blob Git se calcula sobre los bytes originales, sin normalizarlos.

```powershell
dotnet build tests/ArquitecturaBaseMultitenant.ArchitectureTests/ArquitecturaBaseMultitenant.ArchitectureTests.csproj --no-restore -m:1
dotnet build tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj --no-restore -m:1
dotnet build tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj --no-restore -m:1
dotnet test --no-build --project tests/ArquitecturaBaseMultitenant.ArchitectureTests/ArquitecturaBaseMultitenant.ArchitectureTests.csproj -- --filter-class '*EmailPropertyTests'
dotnet test --no-build --project tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj -- --filter-class ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant.AssistantSettingsTests
dotnet test --no-build --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts.AssistantContractTests
dotnet test --no-build --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*AssistantNoticeMigrationTests'
dotnet test --no-build --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*PlatformOperatorPersistenceTests'
dotnet test --no-build --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*CompaniesTests' --filter-method '*A_company_admin_without_organization_roles_gets_its_company_permissions_and_assignable_roles'
dotnet test --no-build --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*UserTenantAccessReaderTests'
dotnet test --no-build --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant.AssistantSettingsTests
```

## Frontera de entrega

No hay delta de rutas ni contratos HTTP ni migraciones productivas. `docs/contracts/openapi.json` y el export de Api mantienen SHA-256 `86428a7254f5e7f6f412fb06c63e05287655e03f81e17c49ce4a8c88f81928a0`. Role.cs es modificación ajena, intocable, y mantiene SHA-256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`.

Sin stage, commit, push, Front, arnés global, suite global, tags, cortes ni planes de Coordinación. Root audita y publica exactamente estas rutas; Integración traslada ese cambio a su corte fijo sin importar A2/M1. El inventario registra el HEAD real de entrega. Fuentes finales inmóviles, índice vacío y ninguna ejecución propia de build/test en curso; `git diff --check` de fuentes propias da cero. Coordinación conserva las revisiones finales, las tres fallas de arnés/documentación, retirada física de módulos y puerta integral pendientes.
