# Etapa 3b · La cuenta — plan de implementación

> **Para ejecución en este chat:** usar `writing-plans` y `executing-plans`, tarea por tarea. La instrucción explícita del usuario prevalece: un solo chat, ejecución directa en main, sin worktrees, subagentes ni push. Los pasos de 2–5 minutos son orientativos; cada cambio coherente cierra el ciclo rojo → mínimo código → verde → commit, sin exigir un commit por paso interno. T01–T30 conservan su registro histórico; esta cadencia rige las tareas pendientes.

**Objetivo:** completar solo La cuenta: perfil/idioma, correo y Google, reautenticación, términos nuevos y baja con gracia; demostrarlo con navegador, API, PostgreSQL y correo pickup reales.

**Arquitectura:** controllers → interfaces de servicio → servicios y helpers → puertos → Infrastructure. Una UoW por escritura pública; validación afuera, locks/lectura/reglas/escritura adentro y caché después. Identidad global; cuenta compartida entre accesos, tenant privado solamente del claim. Eliminación por pasos idempotentes, scope antes de transacción.

**Tecnologías:** .NET 10, EF Core/PostgreSQL, Identity/OpenIddict, Aspire 13.5; React/TypeScript/TanStack Query/i18n; xUnit v3/Testcontainers, Vitest/MSW para unidad, Playwright sin mocks para E2E.

## Fuentes y límites

- Leídos: Etapa 3 completa del plan maestro, multitenancy.md §§3.1, 3.2, 12; ADR 0033 y ADR 0035 (fila del índice y §3.2, no existe un archivo separado); ficha datos-personales y fichas de guardado, errores, validación, UTC, catálogos, correo, textos libres, concurrencia, idempotencia, persistencia, aislamiento, auditoría, HTTP, permisos, logs, tests y traducciones.
- Front: Cuenta/M-Cuenta, Aceptar-Terminos/M-Aceptar-Terminos, estado baja pedida de Ingreso/M-Ingreso y avisos pertinentes de Mensajes; tema.md y fichas de formularios, API, errores, accesos, UI, features, tests, i18n, accesibilidad y responsive.
- Sumar pendientes 3a: Mi cuenta en menú y navegación, formulario de idioma /cuenta, Google vincular/desvincular y paso real en-US.
- Exclusiones autorizadas: 3c/invitaciones; WhatsApp E8; exportar y sugerencia exportar E10; bloqueo único Dueño E4. No rutas/controles deshabilitados de etapas futuras. No nuevos tests de arnés/arquitectura salvo contrato cruzado y actualización de inventarios existentes exigidos por puerta.
- Al inicio ambos repos limpios y main. Cada stage usa rutas explícitas; nunca `git add .` o `commit -a`. No borrar cambios ajenos.
- Development: solo operador Seed:PlatformOwner:*. Los datos de integración/E2E quedan en sus bases. No cargar correos/nombres reales en código, capturas, logs o memoria.
- HarnessStage permanece en 2 hasta cerrar 3c, como decidió 3a. Actualizar punteros existentes y añadir los requeridos en carpetas de código nuevas, sin declarar cerrada E3 completa.

## Mapa de archivos y responsabilidades

Las rutas back abreviadas `Domain/`, `Application/`, `Infrastructure/`, `Api/` corresponden a `src/ArquitecturaBaseMultitenant.<capa>/`. Las rutas front corresponden a `../ArquitecturaBaseMutitenantFront/`.

| Unidad | Responsabilidad |
|---|---|
| Domain/Authentication/LoginMethod y ReauthTicket | estado de método y comprobante consumible ligado a acción |
| Application/Services/Identity | alta/verificación/cambio/Google, comprobantes y avisos |
| Application/Services/Legal | aceptación, policy y ciclos de baja |
| Infrastructure/Persistence | locks, proyecciones globales explícitas, tickets y pasos durables |
| Infrastructure/Identity/Deletion | participantes actuales sin acceder a otros tenants sin scope |
| Infrastructure/Messaging/Email | canal de avisos/validación, payload cifrado identificado por cuenta |
| Api/Controllers/Account y Auth | contratos y navegación Google/cancelación |
| Front personal/account | Page + draft perfil + tabla + diálogos; cuenta sirve ambos accesos |
| Front public/legal y auth | aceptación bloqueante, ingreso de gracia y continuación segura |
| tools/RealE2ESetup y scripts/test-e2e-real | datos y versiones legales únicamente de base propia |
| Capturas 3b | pares por estado y viewport y comparación revisada |

## Contrato propuesto y pruebas

- `GET /api/me/login-methods` devuelve filas con id, type, value visible, primary/verified/managed, acciones disponibles y respaldo enmascarado. Nunca expone el subject Google como correo.
- `POST /api/me/login-methods` crea correo pendiente y manda VerifyMethod; `POST /api/me/login-methods/{id}/code` reenvía; `POST /api/me/login-methods/{id}/verify` consume código de ese usuario/propósito.
- `POST /api/me/reauth` recibe action/target y retorna source/destination enmascarado; `POST /api/me/reauth/verify` recibe source/action/target/code y retorna reauthTicket. No aceptar source=target para quitar/principal.
- `PUT /api/me/login-methods/{id}/primary` y `DELETE /api/me/login-methods/{id}` reciben reauthTicket; toda acción controla cuenta y estado bajo lock.
- `POST /api/me/external/google` navegación autenticada antiforgery y callback protegido. Se reutilizan patrones de ExternalLoginController; adaptador OAuth en Api, regla y transacción en Application.
- `GET /api/me` añade version/pendingLegalDocuments/needsPersonalLoginMethod; `PUT /api/me` trae version.
- `POST /api/legal/accept` trae IDs y versiones mostrados y acceptedTerms=true. La publicación productiva llega E5; aquí solo pruebas publican nueva versión en su base.
- `POST /api/me/deletion` trae reason y reauthTicket. `Identity.Account.PendingDeletion` devuelve deletionScheduledForUtc y cancelTicket después de probar posesión sin sesión. `POST /api/auth/deletion/cancel` consume ticket y continúa returnUrl protegido.
- Todo cruce (rutas, tipos, claims/access, redirecciones/códigos/metadata) se verifica leyendo los dos repos en `src/test/account-contract.test.ts`.
- Los detalles técnicos que surjan se documentan al decidirlos en «Decisiones tomadas» del informe; no requieren aprobación salvo cambio de producto/regla.

## Ejecución TDD

En cada tarea se guarda evidencia de rojo y verde con el comando y resultado. Si el rojo es compilación por pieza inexistente, verificar que la causa sea esa pieza; luego debe pasar el caso funcional. En UI los tests recorren controles accesibles, errores, idioma, 390 px y axe. Las tareas de documentación/capturas no inventan tests que reflejen su implementación.

### T01 · Recorridos reales rojos de 3b

**Archivos:** front: `scripts/test-e2e-real.test.mjs`.
**Comportamiento exacto:** Extender el recorrido existente con cinco subrecorridos: correo personal + verificación .eml; quitarlo con código en el correo original; nueva versión legal; baja + ingreso + cancelación; guardar en-US en /cuenta. Cada uno usa cuenta propia y registra su fallo antes de su implementación. Mantener registro/empresa/F5/Personal/logout de 3a.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: La ruta /cuenta no existe; el subrecorrido correspondiente falla sin mocks.
- [x] 2. Ejecutar `npm run test:e2e:real` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "test: definir recorridos reales de la cuenta"`. Registrar hash back/front.

### T02 · Reglas de métodos y errores

**Archivos:** back: `Domain/Authentication/{LoginMethod,LoginMethodErrors}.cs`, `Application/Resources/{Errors,Errors.en}.resx`; test `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication/LoginMethodTests.cs`.
**Comportamiento exacto:** Agregar desmarcar principal y dirección Email de contacto verificada para Google (Value conserva subject). Probar que un método sin verificar no puede ser principal y que la dirección de Google nunca sustituye la clave única de ingreso.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Faltan el cambio de principal y el contacto de Google.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj -- --filter-class '*LoginMethodTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: completar reglas de métodos de ingreso"`. Registrar hash back/front.

### T03 · Reautenticación ligada a cuenta y acción

**Archivos:** back: `Domain/Authentication/ReauthTicket.cs`, `LoginCodePurpose.cs`; `Application/Services/Identity/{ReauthIssuer,ReauthVerifier}.cs`; `Application/Interfaces/Persistence/IReauthTicketRepository.cs`; test `tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication/ReauthTicketTests.cs`.
**Comportamiento exacto:** Ticket de una sola utilización, 5 minutos, UserId + Action + TargetMethodId + SourceMethodId. Para quitar/cambiar, source distinto del target y verificado. Al consumir revalidar propiedad, estado y vencimiento. Código usa propósito separado y RequestedByUserId, sin reutilizar Login/Signup.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: No existe el ticket; fallan vencimiento, replay, cuenta/acción/objetivo ajenos.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj -- --filter-class '*ReauthTicketTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: modelar reautenticación de acciones de cuenta"`. Registrar hash back/front.

### T04 · Persistencia y migración de cuenta

**Archivos:** back: `Infrastructure/Persistence/Configurations/Identity/{LoginMethodConfiguration,ReauthTicketConfiguration,ApplicationUserConfiguration}.cs`, `Repositories/{LoginMethodRepository,ReauthTicketRepository,UserRepository}.cs`, `ApplicationDbContext.cs`, migración `AccountManagement` y snapshot; puertos `ILoginMethodRepository`, `IUserRepository`; test `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/LoginMethodsTests.cs`.
**Comportamiento exacto:** Lock global de cuenta dentro de UoW; métodos propios por UserId; Remove; persistir tickets; contacto Google; xmin para edición del perfil. Índice único de principal por UserId (parcial). Probar rollback, unicidad global concurrente, 404 de método ajeno y copia del principal. Revisar SQL; no modificar migraciones aplicadas.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Tabla/campos/operaciones faltantes o regla sin implementar.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*LoginMethodsTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: persistir gestión y reautenticación de cuenta"`. Registrar hash back/front.

### T05 · Avisos por outbox

**Archivos:** back: `Infrastructure/Messaging/Email/EmailAccountNoticeChannel.cs`, `EmailRegistration.cs`; `Application/Services/Identity/AccountNoticeIssuer.cs`; `Application/Interfaces/Integrations/Messaging/IOutbox.cs` y payload/entidad outbox si requiere UserId; tests `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Messaging/AccountNoticeTests.cs`.
**Comportamiento exacto:** Encolar AccountNotice cerrado en todos los métodos verificados con canal disponible, incluidos método quitado y correo de Google, sin duplicar destino. Payload cifrado, UserId para cancelación de pendientes; URLs públicas obtenidas por IPublicOrigin. Al borrar, aviso final solo principal antes de eliminar métodos. Usar plantillas existentes y DisplayFormatter.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: No está registrado IAccountNoticeChannel ni se encolan los avisos.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*AccountNoticeTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: entregar avisos de cuenta por el outbox"`. Registrar hash back/front.

### T06 · Sumar y verificar correo

**Archivos:** back: `Application/Interfaces/Services/ILoginMethodManagementService.cs`, `Services/Identity/{LoginMethodManagementService,LoginMethodIssuer,LoginMethodVerifier}.cs`, `Models/Identity/*LoginMethod*Request.cs`, `Validation/Identity/*Validator.cs`; ampliar `LoginCodeIssuer`; test `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/LoginMethodsTests.cs`.
**Comportamiento exacto:** Alta Email normalizado pendiente, código VerifyMethod vinculado al usuario, plantilla VerifyEmail del lienzo, unicidad global incluso en gracia. Verificar una sola vez con intentos persistidos OnAnyResult; después SecurityEvent y aviso en todos. Método pendiente no ingresa. Unicidad atada a campo y sin filtrar existencia de cuentas desde login.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: No existen alta/verificación de método; código ajeno o de login no se acepta.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*LoginMethodsTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: sumar y verificar correos de la cuenta"`. Registrar hash back/front.

### T07 · Quitar y elegir principal con otro código

**Archivos:** back: `Services/Identity/{LoginMethodManagementService,LoginMethodGuard,ReauthIssuer,ReauthVerifier}.cs`, `Interfaces/Services/IReauthService.cs`, `Services/Identity/ReauthService.cs`, modelos y validadores; test `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/LoginMethodsTests.cs`.
**Comportamiento exacto:** Código enviado automáticamente al abrir acción al respaldo verificado y disponible. Ticket se consume en misma transacción que cambio. Evitar último método válido o último propio con solo administrados restantes. Si quita principal, elegir respaldo válido determinista y actualizar copias de Identity; emitir SecurityEvent y avisos sin datos sensibles. Cambio principal mantiene exactamente uno.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Cambios sin ticket, ticket del mismo método, ajeno, expirado/repetido y último método quedan rechazados.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*LoginMethodsTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: proteger cambios de métodos con otro código"`. Registrar hash back/front.

### T08 · Vincular y desvincular Google

**Archivos:** back: `Api/Controllers/Account/AccountGoogleController.cs`, `Api/Contracts/Account/*Google*`, `Interfaces/Services/IAccountGoogleService.cs`, `Services/Identity/{AccountGoogleService,GoogleMethodLinker}.cs`; ampliar `GoogleAccountResolver`, `GoogleAccountRegistrar`; tests `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/GoogleLoginTests.cs` y `Identity/LoginMethodsTests.cs`.
**Comportamiento exacto:** Inicio POST autenticado y antiforgery, OAuth state protege cuenta y retorno /cuenta. Callback valida que sesión/cuenta coincidan, subject único y correo verificado; no crea nueva cuenta. Devuelve /cuenta con estado estable sin tokens en query. Desvincular usa flujo quitar con código en otro método. Pruebas de proveedor doble solo en integración, no en E2E real.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Falta vínculo explícito y protección del callback para otra cuenta.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*GoogleLoginTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: vincular Google desde la cuenta"`. Registrar hash back/front.

### T09 · Contratos HTTP de métodos y reautenticación

**Archivos:** back: `Api/Controllers/Account/{AccountLoginMethodsController,ReauthController}.cs`, `Api/Contracts/Account/*HttpRequest.cs`; tests `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/LoginMethodsTests.cs`, inventarios existentes de rutas/accesos.
**Comportamiento exacto:** GET /api/me/login-methods; POST para alta y código/verificación; POST /api/me/reauth y /verify; PUT /api/me/login-methods/{id}/primary; DELETE /api/me/login-methods/{id}. Access Consumer/Business/Platform para cuenta global, Idempotent en POST, contratos RawText para tokens/códigos y ToString seguro; 404 ajeno. Actualizar solo inventarios existentes requeridos.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Rutas faltantes, 401 anónimo y 404 de método ajeno demostrados.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*LoginMethodsTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: exponer gestión de métodos de cuenta"`. Registrar hash back/front.

### T10 · Perfil versionado y aviso de método propio

**Archivos:** back: `Models/Profile/{MeResponse,UpdateMeRequest}.cs`, `Services/Profile/{ProfileService,ProfileSnapshotBuilder}.cs`, `Api/Contracts/Account/UpdateMeHttpRequest.cs`, repositorio/lector de cuenta; test `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/Profile/ProfileServiceTests.cs` y integración Identity.
**Comportamiento exacto:** GET /api/me incorpora version, pendingLegalDocuments y needsPersonalLoginMethod. PUT manda xmin y produce 409 ante edición vieja. Cultura/zona usan catálogos y validación actual. Aviso deriva solo de métodos propios verificados/disponibles; no agrega listas ni semilla.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Segundo guardado con la misma versión falla; el aviso desaparece al verificar método propio.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj -- --filter-class '*ProfileServiceTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: completar preferencias y avisos de la cuenta"`. Registrar hash back/front.

### T11 · Aceptación bloqueante de versiones vigentes

**Archivos:** back: `Interfaces/Services/ILegalAcceptanceService.cs`, `Services/Legal/{LegalAcceptanceService,LegalAcceptanceGuard}.cs`, `Interfaces/Persistence/{ILegalReader,ILegalRepository}.cs`, adaptadores; `Api/Legal/LegalAcceptanceMiddleware.cs`, `Controllers/Account/LegalAcceptanceController.cs`, `Contracts/Account/AcceptLegalHttpRequest.cs`, Program/DI; ampliar `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/LegalAcceptanceTests.cs`.
**Comportamiento exacto:** 403 Legal.AcceptanceRequired para API autenticada mientras faltan documentos vigentes; exenciones GET /api/me, GET /api/legal/*, POST /api/legal/accept y rutas anónimas. POST acepta exactamente documentos/versión mostrados: nueva publicación entre lectura y confirmación vuelve a bloquear. Append-only, timestamp/IP/UA en misma UoW, idempotencia. Prueba publica versiones solo en su propia base y limpia o usa fixture propia.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: API sigue permitida antes de implementar bloqueo; luego solo desbloquea al aceptar versión vigente.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*LegalAcceptanceTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: exigir aceptación de versiones legales nuevas"`. Registrar hash back/front.

### T12 · Reglas de baja y cancelación

**Archivos:** back: `Domain/Legal/AccountDeletionErrors.cs`, `Infrastructure/Identity/ApplicationUser.cs`, `Interfaces/Persistence/IUserRepository.cs`, repo; `Services/Legal/AccountDeletionPolicy.cs`; tests `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity/AccountDeletionPolicyTests.cs`.
**Comportamiento exacto:** Transiciones Active→PendingDeletion→Active y PendingDeletion/Suspended vencida→Deleted. Motivo TextLimits, fechas UTC, gracia de ajustes, rechazar operador/ya pedida y bloqueos de participantes. Dejar LastAdmin para E4. La anonimización conserva Id y auditoría y elimina preferencias/copias.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Faltan transiciones y bloqueos; ningún test agrega datos Development.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Application.UnitTests/ArquitecturaBaseMultitenant.Application.UnitTests.csproj -- --filter-class '*AccountDeletionPolicyTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: modelar baja y gracia de la cuenta"`. Registrar hash back/front.

### T13 · Participantes actuales de eliminación

**Archivos:** back: `Interfaces/Integrations/Legal/IAccountDeletionParticipant.cs`, `Models/Legal/AccountDeletionContext.cs`; `Infrastructure/Identity/Deletion/{PersonalSpaceDeletionParticipant,MembershipDeletionParticipant,LegalAcceptanceDeletionParticipant,OutboxDeletionParticipant}.cs`, puertos/adaptadores concretos; tests `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/AccountDeletionParticipantsTests.cs`.
**Comportamiento exacto:** Check/OnRequested/OnCancelled/Execute idempotentes. Planificar tenants por proyección propia de eliminación; Enter antes de UoW. Personal Closed + borrar privados actuales; Member Removed con AccountDeleted y AuditLog; anonimizar IP/UA legal; cancelar pendientes propios outbox. No implementar tablas futuras. Tabla UserTenantAccesses deriva por trigger. LoginCodes, tickets y datos técnicos de Identity se purgan en paso final.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Sin participantes, quedan datos personales y membresías activas; comprobar reejecución y auditoría conservada.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*AccountDeletionParticipantsTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: eliminar datos actuales con participantes de cuenta"`. Registrar hash back/front.

### T14 · Pedir baja transaccional

**Archivos:** back: `Interfaces/Services/IAccountDeletionService.cs`, `Services/Legal/{AccountDeletionService,AccountDeletionRequester}.cs`, modelos/validadores, `Api/Controllers/Account/AccountDeletionController.cs`, `Contracts/Account/RequestAccountDeletionHttpRequest.cs`; test `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/AccountDeletionTests.cs`.
**Comportamiento exacto:** POST /api/me/deletion con ticket de menos de cinco minutos y motivo. Dentro UoW lock→cuenta→policy→consumir ticket→fecha/estado→revocar tokens/seguridad cookie→participantes→avisos→SecurityEvent. Después commit invalidar cachés y cerrar sesión. Probar revocación de ambos accesos, rollback de bloqueo y métodos reservados.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: No existe ruta de baja; operador y ausencia de ticket deben bloquear sin cambios.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*AccountDeletionTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: pedir baja y revocar todas las sesiones"`. Registrar hash back/front.

### T15 · Ingreso de gracia y cancelTicket

**Archivos:** back: `Services/Auth/{LoginCodeRequester,LoginCodeVerificationFlow,LoginCodeService,ExternalLoginService,GoogleAccountResolver}.cs`; `Services/Legal/AccountDeletionCancelIssuer.cs`, repositorio/ticket; ampliar AccountErrors y recursos; tests `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/AccountDeletionTests.cs`.
**Comportamiento exacto:** Correo y Google prueban posesión pero no emiten cookie/tokens para PendingDeletion; devuelven fecha y cancelTicket opaco de cinco minutos ligado a cuenta y returnUrl/access protegidos. Suspended siempre prevalece. CancelTicket no pasa por logs ni URL: Google entrega referencia transitoria protegida/cookie HttpOnly para recuperar estado por POST, sin token query.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: PendingDeletion no recibe código o carece de ticket/fecha; ninguno recibe token antes de cancelar.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*AccountDeletionTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: permitir ingresar para cancelar la baja"`. Registrar hash back/front.

### T16 · Cancelar y continuar puerta elegida

**Archivos:** back: `Api/Controllers/Auth/AccountDeletionCancelController.cs`, `Contracts/Auth/CancelAccountDeletionHttpRequest.cs`; `Services/Legal/{AccountDeletionService,AccountDeletionCanceller}.cs`; tests `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/AccountDeletionTests.cs`.
**Comportamiento exacto:** POST /api/auth/deletion/cancel anónimo Idempotent, lock y ticket válido: limpiar baja, participantes, aviso todos, SecurityEvent en transacción; cookie solo después commit y returnUrl validado del ticket, conserva consumer/business. Ticket expirado/repetido/otra cuenta/fecha vencida rechazado. Refresh previo no revive.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Cancelación inexistente; se prueba éxito para ambas puertas y replay rechazado.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*AccountDeletionTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: cancelar baja durante la gracia"`. Registrar hash back/front.

### T17 · Worker reanudable y eliminación final

**Archivos:** back: `Interfaces/Services/IAccountDeletionProcessingService.cs`, `Services/Legal/AccountDeletionProcessingService.cs`; `Infrastructure/BackgroundJobs/AccountDeletionWorker.cs`; `Interfaces/Persistence/IAccountDeletionRepository.cs`, repo, DI; tests `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Legal/{AccountDeletionTests,AccountDeletionParticipantsTests}.cs`.
**Comportamiento exacto:** Cada hora, SKIP LOCKED reclama cuentas vencidas con lease durable, sin sostener transacción al Enter. Pasos tenant separados, luego identidad. Revalidar vencimiento/estado contra cancelación antes de paso irreversible; desde vencimiento no cancelar. Mantener PendingDeletion/Suspended hasta final. Aviso final cifrado antes de quitar métodos; purgar sesiones/códigos/tickets/contactos, anonimizar, SecurityEvent. Reanudar tras fallo; invalidar u:/t:.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Cuenta vencida sigue intacta; probar Suspended+fecha, corrida doble y recuperación tras fallo de participante.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/ArquitecturaBaseMultitenant.Api.IntegrationTests.csproj -- --filter-class '*AccountDeletion*Tests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: completar eliminación reanudable de cuentas"`. Registrar hash back/front.

### T18 · Contratos compartidos regenerados

**Archivos:** back: `docs/contracts/openapi.json`; front: `src/shared/api/generated/schema.d.ts`, `src/shared/api/types.ts`, `src/test/account-contract.test.ts`.
**Comportamiento exacto:** Regenerar por build back + npm run contracts. Test de contrato lee controllers/contratos/error codes/returnUrls back y rutas/clientes/claims/redirects front, verifica cada cruce 3b y metadatos de baja sin duplicar textos. Actualizar inventarios existentes. No crear guardas de arquitectura adicionales.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Test rojo antes de clientes/rutas front; schema generado coincide al terminar.
- [x] 2. Ejecutar `npm run contracts && npm run contracts:check` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "test: fijar contratos cruzados de la cuenta"`. Registrar hash back/front.

### T19 · Clientes API y recursos de cuenta

**Archivos:** front: `src/areas/personal/account/api/{account,loginMethods,reauth,deletion}.ts`, `src/locales/{es,en}/account.json`, `src/shared/api/types.ts`; tests colocalizados.
**Comportamiento exacto:** Clientes httpClient tipados, query keys; Idempotency-Key de formularios; errores por code. i18n copia textos/orden/tablero incluidos mensajes de Google y baja. Datos de servidor TanStack Query, solo draft local. Añadir AGENTS/CLAUDE en carpetas nuevas del mapa.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Requests/rutas/keys no existen y red/409 muestran estado correcto.
- [x] 2. Ejecutar `npm test -- src/areas/personal/account/api` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: conectar clientes de la cuenta"`. Registrar hash back/front.

### T20 · Mi cuenta y formulario de datos

**Archivos:** front: `src/areas/personal/account/pages/{AccountPage,AccountPage.test}.tsx`, `components/AccountProfileForm.tsx`; `src/app/routes.tsx`, `src/layouts/navigation/personal.ts`, `src/tenancy/{AccessMenu,AccessMenu.test}.tsx`.
**Comportamiento exacto:** /cuenta identidad compartida accesos, layout según acceso sin cambiarlo. Page y hoja completa, Nombre/Idioma y región/Zona con CultureSelect/TimeZoneSelect; guardar/descartar arriba y barra móvil, dirty guard, version/ConcurrencyBanner. Menú Mi cuenta activo también desde business; sin crear empresa en consumer.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Ruta/menú/formulario faltantes; guardar en-US actualiza i18n y formatos y rollback visible en error.
- [x] 2. Ejecutar `npm test -- src/areas/personal/account/pages/AccountPage.test.tsx src/tenancy/AccessMenu.test.tsx` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: reproducir formulario de Mi cuenta"`. Registrar hash back/front.

### T21 · Tabla, aviso propio y menús

**Archivos:** front: `components/{LoginMethodsTable,PersonalLoginMethodNotice}.tsx`, tests; `src/areas/personal/home/pages/PersonalHomePage.tsx` y test; `account/pages/AccountPage.tsx`.
**Comportamiento exacto:** Copiar Cuenta/M-Cuenta: tipo/valor/principal/administrado/estado/acciones en escritorio; móvil ícono+valor+pastillas+⋮ como tablero. Acciones según respuesta backend, no calcular reglas de seguridad. Aviso fijo también inicio Personal y botón Agregar a /cuenta. Ocultar WhatsApp sin canal.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Sin tabla ni aviso; último método no se ofrece para quitar, pendientes muestran Verificar.
- [x] 2. Ejecutar `npm test -- src/areas/personal/account src/areas/personal/home` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: mostrar métodos y aviso de correo personal"`. Registrar hash back/front.

### T22 · Diálogo sumar/verificar

**Archivos:** front: `components/{AddLoginMethodDialog,VerifyLoginMethodDialog}.tsx`, tests, AccountPage.
**Comportamiento exacto:** Copiar título Agregar correo o teléfono y botón, ocultando pestaña WhatsApp E8. EmailField → Enviar código → OTP de seis casillas → Verificar; misma hoja móvil. Error en campo/OTP, reintento idempotente, código incorrecto/vencido/sin intentos. Tras verificar invalidar queries y toast del lienzo.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Diálogo no existe; luego recorrido de alta/OTP y errores pasa.
- [x] 2. Ejecutar `npm test -- src/areas/personal/account/components` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: agregar correos desde Mi cuenta"`. Registrar hash back/front.

### T23 · Diálogo quitar/principal y Google

**Archivos:** front: `components/{ChangeLoginMethodDialog,AccountGoogleButton}.tsx`, tests; AccountPage/API.
**Comportamiento exacto:** Abrir acción solicita código al respaldo que decide backend; texto exacto del lienzo con destino enmascarado. Verificar genera ticket, mutación usa ese ticket. Quitar/Desvincular/Hacer principal según tipo. Google inicia formulario navegación con antiforgery; resultado estable toast, error por code; no login tokens en URL. Foco vuelve al trigger.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Acciones faltantes; código de otro método confirma y último propio queda protegido.
- [x] 2. Ejecutar `npm test -- src/areas/personal/account/components` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: cambiar métodos y vincular Google en la cuenta"`. Registrar hash back/front.

### T24 · Pantalla aceptación y bloqueo transversal

**Archivos:** front: `src/areas/public/legal/pages/{AcceptTermsPage,AcceptTermsPage.test}.tsx`, `api/acceptance.ts`; `src/auth/LegalAcceptanceGate.tsx` y test; `src/app/routes.tsx`, queryClient/httpClient donde corresponda; locales.
**Comportamiento exacto:** /aceptar-terminos copia Aceptar-Terminos/M-Aceptar-Terminos, tres casos y versiones reales; casilla desmarcada/botón bloqueado, Salir cierra sesión. GET me detecta pendingLegalDocuments al ingresar, 403 también dirige al gate; guardar returnTo seguro en memoria, continuar lado elegido después aceptar. Legales públicos accesibles sin gate.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Versión nueva no bloquea front; luego requiere aceptar y conserva puerta.
- [x] 2. Ejecutar `npm test -- src/areas/public/legal src/auth/LegalAcceptanceGate.test.tsx` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: reproducir aceptación bloqueante de términos"`. Registrar hash back/front.

### T25 · Diálogo baja y sesión cerrada

**Archivos:** front: `components/{AccountDeletionDialog,DeletionRequestedPage}.tsx`, tests; AccountPage.
**Comportamiento exacto:** Copiar motivo obligatorio + código enviado al abrir al principal disponible + confirmación. Ocultar sugerencia exportar E10 y bloqueo Dueño E4. Backend operador muestra error vigente sin cambiar producto. Al éxito limpiar sesión/caché, vista Cerramos tu sesión/fecha/Ir al inicio del tablero, sin persistir tokens. Fecha por shared/format.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Baja no existe; se exige motivo/6 números y se cierra sesión al éxito.
- [x] 2. Ejecutar `npm test -- src/areas/personal/account/components` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: pedir baja desde Mi cuenta"`. Registrar hash back/front.

### T26 · Ingreso con baja pedida

**Archivos:** front: `src/areas/public/auth/pages/{LoginPage,LoginPage.test}.tsx`, `api/deletion.ts`, locales auth; `src/shared/api/ApiError.ts` si falta metadata tipada.
**Comportamiento exacto:** Code/Google presentan Cuenta con la baja pedida con fecha, Cancelar la baja y entrar/Salir exactos. cancelTicket solo memoria (Google transporte seguro), sin iniciar OIDC antes de cancelar. Continuar returnUrl preservando puerta. Error ticket vencido vuelve al ingreso sin emitir sesión.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Error PendingDeletion genérico; luego botones/fecha y continuación pasan.
- [x] 2. Ejecutar `npm test -- src/areas/public/auth/pages/LoginPage.test.tsx` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "feat: cancelar la baja desde el ingreso"`. Registrar hash back/front.

### T27 · Publicación legal de pruebas y E2E verde

**Archivos:** back: `tools/ArquitecturaBaseMultitenant.RealE2ESetup/{Program,LegalVersionCommand}.cs`, AppHost solo si requiere pasar ruta; front: `scripts/test-e2e-real.test.mjs`.
**Comportamiento exacto:** Preparador recibe comando por archivo en pickup exclusivo E2E y publica versión 2 con textos es/en usando su conexión aislada, nunca endpoint productivo. Runner espera confirmación y ejerce gate real. Ejecutar cada subrecorrido inicialmente rojo (sin saltar los anteriores), y todos al final; aislar cuentas y respetar cooldown sin desactivar seguridad. Salida usa nombres de pasos y ningún dato personal/código.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Todos los subrecorridos de 3a/3b pasan contra navegador/front/API/Postgres/pickup reales.
- [x] 2. Ejecutar `npm run test:e2e:real` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "test: completar recorridos reales de la cuenta"`. Registrar hash back/front.

### T28 · Comparación visual de todos los estados

**Archivos:** front: `scripts/capturar-etapa-3b.mjs`, `src/test/accountCaptureHarness.tsx` y html si se requieren; `docs/design/capturas/etapa-3b/{manifest.json,informe.md,*/**.png}`.
**Comportamiento exacto:** Reusar renderer de lienzo y helpers de 3a. Capturas app y lienzo 1440×900/390×844 de cada caso de matriz visual abajo, incluyendo diálogos, menús, invalidaciones, avisos y mensajes. Inspeccionar imágenes, corregir geometría/textos/tokens. Arnés visual puede fijar datos para estados inalcanzables todavía; E2E real independiente. Reportar ocultaciones autorizadas, datos/versión reales y cualquier diferencia residual.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Todos los pares presentes; estructura/textos/orden/colores/controles coinciden salvo ocultaciones autorizadas.
- [x] 2. Ejecutar `node scripts/capturar-etapa-3b.mjs` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "test: comparar pantallas y avisos de la cuenta"`. Registrar hash back/front.

### T29 · Documentación funcional y recorrido manual

**Archivos:** back: `docs/features/identidad.md`, `docs/reviews/2026-09-30-etapa-3b-cuenta.md`, `docs/backlog.md`; front: punteros AGENTS/CLAUDE y docs de capturas.
**Comportamiento exacto:** Documentar rutas/contratos/códigos/tickets/participantes y 3b sin cerrar 3c. Recorrido real operador para perfil/métodos/términos; segunda persona /registro Gmail para baja, cancelación y pedir de nuevo. Explicar limpieza por worker al vencer gracia (no borrar Identity/auditoría ni acortar gracia); si quiere dejar solo operador activo, baja inmediata desactiva cuenta y definitiva tras gracia. No incluir secretos/datos reales.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Punteros/recetas/participantes actuales correctos; HarnessStage sigue 2 hasta cerrar 3c.
- [x] 2. Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.ArchitectureTests/ArquitecturaBaseMultitenant.ArchitectureTests.csproj -- --filter-class '*HarnessTests'` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "docs: documentar gestión y baja de cuenta"`. Registrar hash back/front.

### T30 · Puerta general y cierre 3b

**Archivos:** back: informe y este plan; front: informe de capturas.
**Comportamiento exacto:** Ejecutar puerta completa abajo. Si falla, corregir solo flujo/seguridad o checks obligatorios y commitear; demás backlog. Adjuntar salida literal saneada E2E, tarea→commit, diferencias y decisiones. Aspire stop al terminar. No declarar 3b cerrada hasta verde real.

- [x] 1. Escribir el caso que demuestra el comportamiento anterior. Rojo esperado: Build 0 advertencias, toda suite Docker/front/generador/arnés/contratos/capturas/E2E verdes.
- [x] 2. Ejecutar `dotnet build ArquitecturaBaseMultitenant.slnx; dotnet test; npm run lint; npm test; npm run build; npm run contracts:check; node --test scripts/datos-de-referencia/generar.test.mjs; npm run test:e2e:real` en el repo dueño y registrar el fallo esperado (2–5 min).
- [x] 3. Implementar el mínimo comportamiento descrito, copiando piezas existentes; dividir en pasos de 2–5 min si hace falta.
- [x] 4. Reejecutar el caso dirigido; esperado verde. Corregir antes de seguir.
- [x] 5. Documentar el comportamiento junto al código, stage con todas y solo las rutas explícitas de esta tarea, `git commit -m "docs: registrar puerta completa de la etapa 3b"`. Registrar hash back/front.

## Matriz visual obligatoria

Guardar manifest e informe en front `docs/design/capturas/etapa-3b/`; cada caso incluye app+lienzo en 1440×900 y 390×844, usando tablero móvil correspondiente. Revisar imágenes con herramientas de visualización.

- Cuenta: Solo correo de empresa / Con correo personal; formulario limpio/sucio/guardado/descartado; listas idioma/zona y menú usuario/móvil; métodos principal/verificado/sin verificar/administrado, ⋮, último método sin Quitar; vincular Google visible y Google vinculado.
- Diálogos: agregar valor/correo inválido/duplicado; código enviado/incorrecto/verificado; verificar pendiente; hacer principal; quitar correo; desvincular Google; toasts agregada/quitada/principal/Google/datos guardados.
- Baja: diálogo abierto, motivo requerido, código incompleto/incorrecto, política operador (texto resource), éxito sesión cerrada y fecha. Único Dueño y exportación excluidos por alcance explícito.
- Aceptar-Terminos: cambiaron ambos / términos / privacidad, casilla sin/con aceptar y botones; versión mostrada tomada de contrato.
- Ingreso: cuenta con baja pedida por puerta Persona/Empresa, cancelando, cancelación completada; desktop/mobile.
- Inicio-Personal: aviso de método propio con botón Agregar.
- Mensajes de correo: verificar método, agregado, quitado, principal cambiado, baja pedida/cancelada y cuenta eliminada; es/en y formato por DisplayFormatter. WhatsApp excluido E8.

## Puerta y evidencia requerida

1. `dotnet build ArquitecturaBaseMultitenant.slnx`: 0 warnings/0 errores.
2. `dotnet test` con Docker: suite completa, incluidos LoginMethodsTests/LegalAcceptanceTests/AccountDeletionTests/AccountDeletionParticipantsTests y aislamiento.
3. Front `npm run lint`, `npm test`, `npm run build`, `npm run contracts:check`: exit 0.
4. Generador referencias: `node --test scripts/datos-de-referencia/generar.test.mjs`; salidas intactas.
5. Inventarios existentes de rutas/contratos y arnés back/front verdes; contratos regenerados y commiteados en ambos repos si cambió la API, con `contracts:check` siempre verde.
6. Pares visuales de pantallas y estados nuevos o modificados en 3b presentes/revisados; los no tocados conservan la comparación aprobada. Diferencias corregidas o documentadas con motivo.
7. `npm run test:e2e:real`: registrar fallo inicial de cada recorrido 3b y salida final pasando todos, sin mocks ni Development. Obligatorio: sumar correo con .eml, quitar con código en otro, términos versión nueva, baja/cancelación al ingresar y en-US desde /cuenta; mantener puerta empresa 3a.
8. `aspire stop`: AppHost detenido; no push; repos sin cambios propios sin commit.
9. Informe `docs/reviews/2026-09-30-etapa-3b-cuenta.md`: cada cambio coherente/hash y rojo/verde donde hubo lógica, resultados puerta y salida E2E, diferencias de lienzo/motivos, «Decisiones tomadas», pendientes solo en backlog y pasos manuales exactos.
10. Declarar solo 3b cerrada si todo lo anterior pasa. 3c queda pendiente con su propia puerta.

## Decisiones tomadas (iniciales)

- Ejecutar inline en main y un chat por autorización expresa; revisar este plan directamente sin delegación.
- Mantener subject Google como clave única y agregar Email de contacto verificada para presentación/avisos, dentro de LoginMethods. No convertir el correo en clave OAuth.
- Publicación de versión legal de pruebas por preparador E2E separado y restringido a MT_E2E_ISOLATED; no endpoint de pruebas en producción ni modificación de Development.
- Preservar regla de no acortar gracia para limpieza manual: la cuenta de prueba se puede desactivar pidiendo baja y se anonimiza al vencer la gracia; test worker usa su propio reloj/base.
- No agregar Stage3bInventoryTests ni nuevas guardas de arquitectura; solo casos funcionales, contrato cruzado y actualización de inventarios ya exigidos.





## Estado de ejecución al terminar el trabajo técnico

T01–T30 se ejecutaron y commitearon primero; sus resultados históricos están en el informe. Después se completaron T31–T35 con los cambios de seguridad solicitados. Puerta automática final: backend 1117/1117, build con 0 advertencias/0 errores; front 658/658, lint/build/contracts:check verdes; generador 30/30, arnés 10/10 y 20/20; E2E real 8/8. Comparación visual: 122 pares app/lienzo completos y revisados, incluidos cuatro pares nuevos para reautenticación. La 3c no se inició y conserva su propia puerta. Informe y pasos manuales: `docs/reviews/2026-09-30-etapa-3b-cuenta.md`.

## Correcciones de seguridad solicitadas antes del cierre

Alcance exclusivo: impedir agregar métodos con solo una sesión abierta y liberar métodos pendientes ajenos. La puerta anterior se debe repetir completa. Los pasos de implementación y verificación dirigida se ejecutan en bloques de 2–5 minutos; las suites completas y el E2E real pueden durar más. No se agregan guardas ni tests del arnés nuevos.

### T31 · Regresiones reales de ambos problemas

- [x] Escribir un caso HTTP sin ticket para agregar correo y vincular Google; comprobar que no produce método ni desafío. Ejecutarlo rojo antes de la corrección.
- [x] Escribir un caso de reauth que excluye un método verificado después de la sesión, incluidos verificación manipulada y refresh/cambio de acceso. Ejecutarlo rojo antes de la corrección.
- [x] Cambiar el caso de registro con pendiente ajeno: el código crea otra cuenta y elimina el pendiente; no activa la cuenta anterior. Ejecutarlo rojo antes de la corrección.
- [x] Agregar ambos casos al runner real, con identidades y PostgreSQL propios, sin mocks; comprobar el rojo antes del código y el verde final.

### T32 · Reautenticación para agregar correo y vincular Google (back)

Archivos: sesión y claims (`SignInService`, `BrowserSessionKeys`, `ConnectController`, `OpenIdPrincipalFactory`, `CurrentUser`), servicios/modelos/validadores/contratos de cuenta y reauth, tests de Identity/Auth y contratos OpenAPI.

- [x] Sellar el inicio UTC en las propiedades protegidas de la cookie; emitirlo en el token y conservarlo al renovar/cambiar acceso. Ausencia o valor inválido no permite reauth.
- [x] Agregar acciones específicas para correo y Google; seleccionar y verificar solamente fuentes anteriores al inicio de sesión.
- [x] Consumir el ticket una vez y dentro de la UoW para agregar correo y preparar el desafío Google; conservar antiforgery y la identidad protegida del callback.
- [x] Tests dirigidos verdes y contratos cruzados. `datos-personales.md` y ADR 0033 quedaron en el mismo commit de back `1b607b9`.

### T33 · Diálogo existente de reautenticación para las dos altas (front)

Archivos: componentes/API/tests de Mi cuenta, resources es/en, contrato cruzado y schema generado.

- [x] Tests que exigen confirmar el código del método anterior antes del POST de alta o desafío; rojo previo y verde tras el cambio.
- [x] Reusar el diálogo, controles y estados de cambio de método para correo/Google; mantener tickets solo en memoria y cuerpo HTTP.
- [x] Adaptar el recorrido real y los contratos cruzados leyendo ambos repositorios.
- [x] Tests dirigidos, lint/build verdes; commit front `e5346e1`.

### T34 · Posesión y vencimiento de pendientes (back)

Archivos: entidad/configuración/migración/repositorio `LoginMethod`, registro, alta y vínculo Google, tests existentes de Signup/LoginMethods/GoogleLogin.

- [x] Casos rojos de pendiente ajeno en alta y Google, vencimiento con su código, reenvío, rechazo de código anterior y protección de métodos verificados ajenos.
- [x] Eliminar el pendiente ajeno bajo el mismo lock del destino antes de continuar; la cuenta del dueño del código prevalece y el borrado/escritura son atómicos.
- [x] Persistir el vencimiento UTC del pendiente desde su código y renovarlo al reenviar; un pendiente vencido no reserva el destino ni admite verificación. Migración aplicada a bases de prueba, sin sembrar Development.
- [x] Tests dirigidos verdes; mismo commit back `1b607b9`.

### T35 · E2E real, comparación visual y puerta completa

- [x] E2E real actualizado: bloquea la toma de cuenta, recupera el correo pendiente ajeno y prueba los cinco recorridos originales; 8/8 final.
- [x] Capturas de las dos reautenticaciones en escritorio/móvil, comparación con el diálogo existente y motivo documentado. Manifest final: 122 pares completos.
- [x] Build back 0 advertencias, `dotnet test` Docker 1117/1117; lint/test/build/contracts:check front 658/658; generador 30/30, arnés 10/10 y 20/20.
- [x] Aspire detenido; informe, salida E2E, diferencias, decisiones y pasos manuales actualizados. Evidencia en commit de documentación de cierre.

### Decisiones tomadas para las correcciones

- El inicio de sesión se obtiene de autenticación emitida por el servidor; refresh y cambio de acceso conservan ese instante. No se usa la fecha de emisión de cada token ni un dato enviado por el cliente.
- Las acciones de alta de correo y vínculo Google tienen tickets distintos y de un solo uso. La reauth existente conserva su selección automática de destino y cooldown.
- La posesión de un correo pendiente no otorga acceso a la cuenta que lo reservó: el registro crea la identidad del dueño comprobado. Un método verificado ajeno sigue protegido.
