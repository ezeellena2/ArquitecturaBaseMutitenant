# Calidad de diseño · Back · identidad e ingreso

> Informe de un revisor, solo lectura, sobre el commit `1202433 del back`. Los números de línea son de ese commit: buscá por símbolo. Antes de corregir un hallazgo, confirmalo en el código actual. El resumen, los bugs confirmados y el orden de trabajo están en [`../2026-10-03-calidad-de-diseno.md`](../2026-10-03-calidad-de-diseno.md).

# Revisión de diseño · Back · Identidad e ingreso (commit 1202433)

**Rutas.** `Domain/`, `App/`, `Infra/` y `Api/` abrevian `src\ArquitecturaBaseMultitenant.{Domain,Application,Infrastructure,Api}\`. `Tests/` abrevia `...\ArquitecturaBaseMutitenant\tests\`. Todo fue lectura: no corrí build ni tests.

**Contexto.** `App/Services/{Auth,Identity,Legal,Invitations,Profile}` suma 84 clases para 20 puntos de entrada `I<X>Service`, o sea unos 64 helpers. Roles, el área de referencia, tiene 3 servicios y ningún helper.

## 1. Puntajes

| # | Criterio | Nota | Justificación |
|---|---|---|---|
| 1 | Responsabilidad única y cohesión | 2 | Muchos helpers se partieron para no pasar de 8 dependencias. Hay clases que solo reenvían y el alta de empresa está repartida en 10 clases. |
| 2 | Inyección de dependencias | 4 | Los lifetimes están bien: todo scoped y ningún singleton captura un scoped. Detalles: una dependencia opcional por segundo constructor, `new CultureProfiles(...)` 9 veces y un registro duplicado sin uso. |
| 3 | Capas | 3 | La dirección de dependencias se respeta. Pero el ciclo de vida de la cuenta y la salida de membresías viven en Infrastructure, y la navegación del front se decide en Infra y en Api. |
| 4 | Dominio | 3 | `LoginCode`, `ReauthTicket` y `LegalAcceptance` tienen comportamiento y setters privados. En cambio, el estado de la cuenta, el motivo de un código inactivo y las reglas de `LoginMethod` se reimplementan en los servicios. |
| 5 | Duplicación | 2 | Alta de identidad ×5, consentimiento legal ×5, verificación con bloqueo ×3, sincronización del principal ×8, chequeo de estado de cuenta ×7. |
| 6 | Sobreingeniería | 2 | Capas de reenvío sin valor, tres provisionadores con validación y replay repetidos, Result que nunca falla y OperationLog anidado dentro de helpers. |
| 7 | Clean code | 3 | Buenos nombres y summaries. En contra: un parámetro bandera, ternarios anidados, un efecto lateral dentro de un `if`, ventanas de 5 minutos mágicas y 25 sufijos fuera de los documentados. |
| 8 | Manejo de errores | 4 | Las excepciones son solo para bugs y los catch están justificados. Manchas: un ticket viaja en la metadata de un Error, y el "código equivocado" se detecta leyendo esa metadata. |
| 9 | Tests | 3 | Dobles escritos a mano, nombres que describen conducta y unas 8.500 líneas de integración. Faltan tests unitarios de Application para Identity, Legal y las altas, y los stubs gordos se copian de un archivo a otro. |
| 10 | Consistencia con Roles y la Base | 2 | Roles es un servicio sin helpers. ArquitecturaBase resolvió auditoría e intentos con `LoginAuditRecorder` y un solo `LoginCodeVerifier`. `backend.md` §4.2 no coincide con las carpetas reales. |

## 2. Hallazgos

### 1. [alta] Crear una identidad está programado 5 veces, y el consentimiento legal otras 5
- **Evidencia:**
  - `App/Services/Auth/SignupAccountRegistrar.cs:25-54`: `RegisterAsync` y `RegisterBusinessAsync` son iguales salvo `personalSpaces.Stage`.
  - Más copias: `App/Services/Auth/GoogleAccountRegistrar.cs:25-64` (dos), `App/Services/Invitations/InvitationAccountRegistrar.cs:15-29` y `App/Services/Auth/BusinessSignupLegalAcceptor.cs:10-18`.
  - `GetCurrentDocumentAsync(LegalDocumentKind.Terms|Privacy)` aparece escrito 10 veces. Solo `App/Services/Legal/LegalAcceptanceWriter.cs:18` recorre «`Enum.GetValues<LegalDocumentKind>()`».
- **Problema:** cada puerta tiene su propia secuencia: documentos vigentes → usuario → método verificado → principal → aceptaciones. Las puertas son correo personal, correo empresa, Google personal, Google empresa e invitación.
  - Si se suma un tipo de documento, el gate lo va a exigir pero las altas no lo van a aceptar: toda cuenta nueva nacería bloqueada.
  - E8 (WhatsApp) agregaría la sexta copia.
- **Refactor:**
  - Un `AccountRegistrar` en `App/Services/Identity` con `RegisterAsync(NewAccount)`, donde `NewAccount` lleva método inicial, nombre, cultura y zona, y el borrador Personal opcional.
  - Un `LegalConsentRecorder.AcceptCurrentAsync(userId, nowUtc)` que recorra todos los tipos de documento. `LegalAcceptanceWriter` lo reutiliza.
- **Refutación:** ninguna regla pide un registrador por puerta, y la divergencia entre el writer y las altas es concreta.
- **Esfuerzo:** M · ahora, antes de E8.

### 2. [alta] La verificación de código con bloqueo y auditoría está triplicada, y el registro persona/empresa está copiado entero
- **Evidencia:**
  - `App/Services/Auth/SignupVerificationCore.cs:16-65` y `:67-117`: dos métodos casi idénticos en la misma clase.
  - Tercera copia del bloque de intentos fallidos en `LoginCodeVerificationFlow.cs:33-42`.
  - `IsFailedCodeAttempt` está igual en `SignupVerificationCore.cs:119-122` y `LoginCodeVerificationFlow.cs:76-79`. Decide leyendo «`error.Metadata?.ContainsKey(LoginCodeErrors.AttemptsLeftKey)`».
  - `Fail(...)` con `LoginAudit` está tres veces: `SignupVerificationCore.cs:124`, `LoginCodeAttemptCoordinator.cs:32` y `ExternalLoginService.cs:68`.
  - `SignupCodeRequester.cs:13-30` es igual a `:32-49`.
  - El motivo de un código inactivo se calcula igual en `App/Services/Identity/LoginMethodIssuer.cs:50-62` y `LoginMethodVerifier.cs:25-37`.
- **Problema:** la regla contra fuerza bruta vive en tres lugares. Las diferencias entre persona y empresa (orden del chequeo de política, re-aceptación de documentos legales) no tienen nombre: quedaron escondidas en el copy-paste.
- **Refactor:**
  - Un `LoginAttemptRecorder`, como el `LoginAuditRecorder` de la Base.
  - Un `LoginCodeAttemptVerifier.VerifyAsync(destination, purpose, code, knownUserId)` que encapsule código activo → verificación → contar intento → bloqueo.
  - Un `LoginCode.InactiveReason(nowUtc)` en Domain, o un resultado tipado que diga si el intento cuenta.
  - Un solo `VerifyAsync(request, SignupKind)` con la diferencia persona/empresa explícita.
- **Refutación:** la Base ya lo resolvió con una sola pieza, y las tres copias ya difieren en el orden de los chequeos.
- **Esfuerzo:** M · ahora.

### 3. [alta] Helpers partidos para esquivar la guarda de 8 dependencias, y guardas de arquitectura debilitadas con listas blancas
- **Veredicto: cohesivos, conservar.** `LoginCodeIssuer`, `LoginCodeVerifier`, `AccessSwitchPolicy`, `SignupPolicy`, `ReauthIssuer`, `ReauthVerifier`, `ReauthTicketConsumer`, `LoginMethodGuard`, `InvitationIssuer`, `InvitationIssuingGuard`, `InvitationDeliveryIssuer`, `AccountDeletionRequester`, `AccountDeletionCanceller`, `AccountDeletionProcessor` y los `*Notifier`.
- **Artificiales que solo reenvían:**
  - `SignupVerificationFlow.cs:10-15`: dos métodos que delegan; existe para que `AccountService` no pase de 8.
  - `LoginCodeAttemptCoordinator.cs:12-30`: 5 de sus 7 miembros reenvían.
  - `LegalAcceptanceGuard.cs:9-10`: un "Guard" que solo es una lectura.
  - `AccountDeletionSessionRestorer.cs:9`, `GoogleMethodLinkFinalizer.cs:21-23`, `LoginAccountGuard.cs:19`, `LoginCodeIssuer.cs:42-47`, `BusinessSignupOrganizationProvisioner.cs:18-19`, `TenantProvisioningReplayVerifier.cs:14-15` e `InvitationAcceptanceResolver.cs:38`.
  - Cadena de dos saltos: `GoogleAccountResolver.cs:20-21` → `GoogleAccountRegistrar.cs:21-23` → `IPersonalSpaceProvisioner`.
- **Artificiales que agrupan cosas sin relación:**
  - `InvitationAcceptanceCredentials`: métodos de ingreso y hash de nonce.
  - `BusinessSignupRequestResolver`: CUIT y valores regionales por defecto.
  - `BusinessSignupTransactionRunner.EnsureNoActiveTenant`: no tiene que ver con la transacción.
- **Un caso de uso en 10 clases:** crear organización recorre `BusinessSignupService` → `BusinessOrganizationCreator` → `BusinessSignupTransactionRunner`, `BusinessSignupPolicyReader`, `BusinessSignupOrganizationProvisioner`, `BusinessSignupLegalAcceptor`, `BusinessSignupRequestResolver` → `TenantProvisioner` → `TenantSpaceProvisioner` y `TenantProvisioningReplayVerifier`.
  - `Response(...)` está duplicado (`BusinessSignupOrganizationProvisioner.cs:55-58`, `BusinessOrganizationCreator.cs:73-76`).
  - La validación de argumentos está duplicada (`TenantProvisioner.cs:45-60`, `TenantSpaceProvisioner.cs:62-77`), y el replay se comprueba en tres lugares.
  - Cada request deja tres OperationLog anidados (`BusinessSignupService.cs:35`, `BusinessOrganizationCreator.cs:24`, `TenantProvisioner.cs:43`).
  - `BusinessSignupPolicyReader.cs:20-21` arma un `PlatformSettings` ficticio para reutilizar una regla.
- **Guardas debilitadas:**
  - `Tests/ArquitecturaBaseMultitenant.ArchitectureTests/TransactionBoundaryTests.cs:24-40` exceptúa 14 helpers que reciben `IUnitOfWork`; 3 son de esta área. Eso contradice `guardado.md` («Prohibido: que un helper reciba IUnitOfWork») y `backend.md:109`.
  - `TenantScopeUsageTests.cs:60-65` hace lo mismo con `ITenantScope.Enter`.
  - Unos 40 helpers usan 25 sufijos que no están entre los 5 documentados: Flow, Core, Coordinator, Finalizer, Restorer, Credentials, Runner, Resolver, entre otros.
- **Problema:** para cambiar un flujo hay que recorrer entre 5 y 10 archivos, y la guarda de transacciones ya no protege lo que dice la documentación.
- **Refactor:**
  - No subir el tope de 8.
  - Bajar dependencias con colaboradores que tengan una regla completa; los de los hallazgos 1, 2 y 5 absorben varios reenvíos.
  - Borrar los reenvíos que sobren.
  - Devolver el límite de UoW a `BusinessSignupService` y sacar las 3 entradas de la lista blanca.
  - Fijar en la ficha qué sufijos valen.
- **Refutación:** la guarda y los sufijos son intencionales y no los objeto; objeto las clases sin regla propia. Que el test haya tenido que exceptuar 14 helpers muestra que no es gusto.
- **Esfuerzo:** L en total (S por cada reenvío) · al tocarlo, empezando por el alta de empresa.

### 4. [alta] El ciclo de vida de la cuenta vive en Infrastructure y se reimplementa en Application; el estado se chequea en 7 lugares
- **Evidencia:**
  - `Infra/Identity/ApplicationUser.cs:105-157` tiene `RequestDeletion`, `CancelDeletion` y `CompleteDeletion`.
  - `Infra/Persistence/Repositories/UserRepository.cs:16-37` ejecuta esas reglas y además cambia el SecurityStamp en `:22`. Se volvió una fachada de comandos.
  - Copias sobre `UserAccountRow`:
    - `AccountDeletionPolicy.cs:18-24` repite `ApplicationUser.cs:109-111`.
    - `AccountDeletionCancelIssuer.cs:18-22` y `AccountDeletionCanceller.cs:31-33` repiten `:125-127`.
    - `AccountDeletionRequester.cs:44` recalcula «`nowUtc.AddDays(platform.AccountDeletionGraceDays)`» (`:115`).
  - El mapeo de estado a error, con variantes, está en `SignupExistingMethodVerifier.cs:29-32`, `BusinessSignupTransactionRunner.cs:35-38`, `LoginCodeVerificationFlow.cs:49-52`, `GoogleAccountGuard.cs:19-22`, `ConnectService.cs:82-95`, `LoginMethodGuard.cs:19-21` e `InvitationAcceptanceGuard.cs:35-36`. También en Api: `Api/RequestContext/InvitationFlowContext.cs:84`.
- **Problema:** `identidad.md` pide las reglas que no dependen de Identity en `Domain/Users`, y el ciclo de baja no depende de Identity.
  - Un estado nuevo (por ejemplo, con «Recuperar mi cuenta») obliga a unas 10 ediciones.
  - Síntoma: `Tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity/ApplicationUserRulesTests.cs:4` referencia Infrastructure.
- **Refactor:** crear `Domain/Users/AccountLifecycle`.
  - Es un value object owned con el estado y las fechas y el motivo de la baja, con `RequestDeletion`, `CancelDeletion`, `Complete` y `CheckCanSignIn(status) → Error?`.
  - `ApplicationUser` lo contiene y delega; `UserAccountRow` lo expone.
  - En Infra queda solo la anonimización de los campos de Identity.
- **Refutación:** que `ApplicationUser` viva en Infra está decidido y no lo discuto; lo que pide sacar las reglas puras es la regla escrita. Las variantes ya difieren: `Deleted` da NotFound en `ConnectService` y Suspended en `LoginMethodGuard`.
- **Esfuerzo:** M · ahora, o al empezar la baja iniciada por un operador (E5).

### 5. [media] "Agregar un método verificado" es un protocolo de 4 o 5 pasos repetido en 8 lugares
- **Evidencia:**
  - `MakePrimary` + `SetPrimaryEmail/ContactAsync` está en `SignupAccountRegistrar.cs:32-35,48-51`, `SignupExistingMethodVerifier.cs:46-50`, `GoogleAccountMethodFinalizer.cs:20-24`, `GoogleMethodLinkFinalizer.cs:13-17`, `LoginMethodVerifier.cs:44-48`, `LoginMethodChanger.cs:31-36` e `InvitationAccountRegistrar.cs:58-60`.
  - `AssignIfVerifiedAsync` aparece en 7 lugares.
  - El Result de `MakePrimary` se chequea y lanza en 4 sitios y se ignora en otros 4.
  - `GoogleAccountMethodFinalizer.AddGoogleMethodAsync` es lo mismo que `GoogleMethodLinker.cs:43-48` + `GoogleMethodLinkFinalizer`.
  - "Quitar el pendiente sin verificar" se repite en `GoogleMethodLinker.cs:32-33,39-40`, `LoginMethodIssuer.cs:32-33`, `SignupExistingMethodVerifier.cs:23-24` e `InvitationAccountRegistrar.cs:46-50`.
- **Problema:** la copia del principal en AspNetUsers se mantiene a mano en 8 sitios, y E8 suma teléfonos.
- **Refactor:** un `LoginMethodLinker` (el sufijo previsto para esto) con:
  - `AddVerifiedAsync`: verifica, asigna el dominio, elige principal si falta, sincroniza la copia, agrega el método y avisa.
  - `MakePrimaryAsync` y `ReleasePendingAsync`.
- **Refutación:** la inconsistencia con el Result de `MakePrimary` ya existe; no es gusto.
- **Esfuerzo:** M · antes de E8.

### 6. [media] La salida de una membresía por baja de cuenta aplica reglas de negocio en Infrastructure con EF directo, y ya difiere de la de Usuarios
- **Evidencia:**
  - `Infra/Identity/Deletion/MembershipDeletionParticipant.cs:29-59` revoca invitaciones, borra asignaciones, hace `member.Remove` y audita. En `:47` lee `database.Members` directamente.
  - `PersonalSpaceDeletionParticipant.cs:19-23` cierra el tenant y hace `RemoveRange`.
  - En Application, `OrganizationUserAccessCleaner.cs:31-37` + `OrganizationUserAssignmentManager.cs:134-142` hacen lo mismo y además inactivan las membresías de empresa; el participante no.
  - `backend.md:214-216` ubica los Participants en `Application/Services/Legal/Participants/`.
- **Refactor:**
  - Mover Membership y PersonalSpace a Application, con repositorios y un helper común con Usuarios.
  - Dejar en Infra solo los participantes técnicos (aceptaciones y outbox).
- **Refutación:** los técnicos están bien donde están; estos dos deciden reglas de negocio.
- **Esfuerzo:** M · verificar ya si la diferencia con las empresas es intencional (S); mover al tocarlo.

### 7. [media] UserManager guarda por su cuenta dentro de la UoW: punto ciego de «una sola forma de guardar»
- **Evidencia:**
  - `Infra/Persistence/Repositories/UserRepository.cs:50` usa `manager.CreateAsync`, y `:68,96,104` usan `manager.UpdateAsync`.
  - `IdentityRegistration.cs:60-67` no configura `AutoSaveChanges`, así que el store llama a `SaveChangesAsync` en cada una.
  - `UpdateProfileAsync` esquiva UserManager con un comentario de workaround (`:82-84`).
  - `TransactionBoundaryTests.cs:107-122` solo inspecciona el IL del proyecto.
- **Problema:** un alta hace varios SaveChanges por transacción, aunque `backend.md` §5 dice «un solo SaveChanges», y hay dos caminos para guardar.
- **Refactor:** poner `AutoSaveChanges = false` o no usar UserManager para los campos propios. Agregar un test de integración que cuente los SaveChanges.
- **Refutación:** es una regla escrita. No lo pude ejecutar, por eso pido el test.
- **Esfuerzo:** S · al tocarlo.

### 8. [media] El ticket para cancelar la baja viaja dentro de un Error, y su persistencia depende de la política del llamador
- **Evidencia:**
  - `App/Services/Legal/AccountDeletionCancelIssuer.cs:16-37` hace `tickets.Add(ticket)` y devuelve «`AccountErrors.PendingDeletion with { Metadata = … ["cancelTicket"] = secret … }`».
  - El ticket se guarda solo porque `LoginCodeService.cs:39` y `ExternalLoginService.cs:46` usan `OnAnyResult`.
  - `Api/Authentication/PendingDeletionCookie.cs:22-24` lo reconstruye con casts («`(DateTime)metadata["scheduledForUtc"]!`»), aunque `PendingDeletionState` ya existe.
- **Problema:** si alguien cambia la política a OnSuccess, el front recibe un ticket que no existe.
- **Refactor:**
  - Una fábrica tipada `AccountErrors.PendingDeletion(PendingDeletionState)` y un `PendingDeletionState.FromError`, con las claves como constantes.
  - Un test que fije que el ticket persiste en los dos ingresos.
- **Refutación:** devolver datos junto con un 403 es válido; el problema es la escritura escondida en el camino de error.
- **Esfuerzo:** S · al tocarlo.

### 9. [media] Contratos internos como strings: uno ya tiene un fallo programado para E8
- **Evidencia:**
  - `"Added"`, `"Removed"` y `"Primary"` (`LoginMethodChanger.cs:38`, `LoginMethodVerifier.cs:49`, `GoogleAccountMethodFinalizer.cs:27`, `GoogleMethodLinkFinalizer.cs:23`) viajan por `App/Models/Notifications/AccountNotice.cs:10` (`string Change`).
  - Terminan en `Infra/Messaging/Email/EmailTemplateRenderer.cs:102-112`, cuyo switch cierra con «`_ => throw new NotSupportedException`». ("Removed", Phone) y ("Primary", Phone) van a romper la transacción cuando E8 habilite teléfonos.
  - La ruta "/cuenta" se repite en `LoginMethodNotifier.cs:19`, `AccountDeletionNotifier.cs:24`, `IdentityRegistration.cs:173` y `ExternalLoginController.cs:127-128`.
  - La ruta "/login/empresa" se repite en `IdentityRegistration.cs:175`, `ExternalLoginController.cs:143` y `ConnectController.cs:93`.
  - Hay códigos de error como literal en `IdentityRegistration.cs:177` y `ExternalLoginController.cs:140`, y `BusinessSignupController.cs:76-77` arma a mano el mismo error que `BusinessSignupErrors.IdentityRequired`.
  - La conversión entre `Access` y texto está en 6 lugares (`ReturnUrls.cs:50-56`, `ConnectController.cs:207-225`, `TokenRevoker.cs:52-58`, `ConnectAuthorizationStore.cs:37-38`, `ExternalLoginController.cs:104,134`, `IdentityRegistration.cs:174`).
  - La clave "Access" del state de OAuth está como constante en `ExternalLoginController.cs:42` y como literal en `IdentityRegistration.cs:174`.
- **Refactor:**
  - `enum LoginMethodChange` en Domain, más un test que recorra todas las combinaciones.
  - Constantes `FrontRoutes` junto a `ReturnUrls`.
  - `AccessNames.ToWire` y `TryParse`.
  - Un `GoogleChallengeState` tipado.
  - Usar las constantes de error que ya existen.
- **Esfuerzo:** S · ahora.

### 10. [media] PersonalSpaceProvisioner hace dos trabajos, y la resolución de valores regionales está triplicada
- **Evidencia:**
  - `App/Services/Auth/PersonalSpaceProvisioner.cs:31-70` resuelve los valores regionales y además siempre crea «`Tenant.CreatePersonal("Personal")`».
  - El alta de empresa lo usa solo por esos valores (`BusinessSignupCodeFlow.cs:19`, `BusinessSignupRequestResolver.cs:27-28`, y `BusinessOrganizationCreator.cs:47-50` toma `draft.Settings` para una organización). Plataforma también (`PlatformTenantProvisioning.cs:10`).
  - `InvitationIdentityDefaults.cs:11-22` repite el mismo fallback.
- **Refactor:** un `RegionalDefaultsResolver` que devuelva `RegionalDefaults(culture, zone, currency)`. El provisionador Personal solo arma y agrega el espacio.
- **Esfuerzo:** S · al tocarlo.

### 11. [media] Las reglas de un método de ingreso están duplicadas entre la entidad y la proyección, y ya divergen
- **Evidencia:**
  - `Domain/Authentication/LoginMethod.cs:32` define `IsPersonalBackup` sin exigir verificación; `App/Models/Identity/LoginMethodRow.cs:11-12` la exige.
  - `LoginMethodAvailability.cs:18-34` y `:43-53` repiten el switch de canales, y hay dos `CanReceiveCode` (`:36-41`).
  - La máscara es idéntica en `AccountLoginMethodsService.cs:48-53` y `LoginMethodNotifier.cs:28-34`.
  - "Contacto" (`Google ? ContactEmail : Value`) se calcula en 4 lugares.
- **Refactor:** `Contact`, `IsPersonalBackup` y `CanSignIn` como funciones de Domain que use también la fila, y un `ContactMask` en `App/Common`.
- **Esfuerzo:** S · al tocarlo.

### 12. [media] Los servicios del área casi no tienen tests de Application, y los dobles se copian por archivo
- **Evidencia:**
  - No existen `Application.UnitTests/Services/Identity` ni `/Legal`.
  - Solo se prueban por HTTP: `LoginMethodManagementService`, `ReauthService`, `AccountGoogleService`, `AccountDeletionService`, `LegalAcceptanceService`, `AccountService`, `BusinessSignupService`, `ExternalLoginService`, `SignupVerificationCore`, `LoginCodeVerificationFlow` y `BusinessOrganizationCreator`.
  - `tests.md` pide `InMemory*` compartidos, pero `TestDoubles/` tiene 3 archivos.
  - `StubUsers` con 7 `NotSupportedException` está copiado en `ConnectServiceTests.cs:239-261` y en `ProfileServiceTests.cs:228-257`.
  - `ProfileServiceTests.cs:45-48` arma 13 objetos para probar un booleano.
  - `LoginCodeFlowTests.cs:66-98` prueba 4 escenarios en un solo Fact.
- **Refactor:** al consolidar los hallazgos 1, 2 y 5, escribir sus tests unitarios con repositorios en memoria compartidos y un escenario por test.
- **Refutación:** la integración amplia es buena y la conservaría; lo que falta es el nivel unitario que la ficha promete.
- **Esfuerzo:** M · junto con 1, 2 y 5.

### 13. [baja] Una vista previa inicia sesión dentro de un `if`; hay métodos largos y un parámetro bandera
- **Evidencia:**
  - `App/Services/Invitations/InvitationPreviewBuilder.cs:32-36` cierra su condición con «`&& await flow.RestoreSessionAsync(newlyCreated, ct)`», que inicia la sesión (escribe la cookie) en `Api/RequestContext/InvitationFlowContext.cs:82-88`. El summary de `:17` dice «no consume ni modifica». `BuildAsync` tiene 51 líneas.
  - `LoginMethodChanger.cs:13` recibe `bool remove`, con un ternario anidado en `:26-28`.
  - Hay otro ternario anidado en `ExternalLoginController.cs:141-143`.
- **Refactor:**
  - Hacer de `TryRestoreBootstrapSessionAsync` un paso explícito.
  - Partir `BuildAsync` por estado.
  - Separar `MakePrimaryAsync` y `RemoveAsync`.
- **Esfuerzo:** S · al tocarlo.

### 14. [baja] EmailTemplateRenderer: dependencia opcional por constructor y presentación repartida entre plantillas y C#
- **Evidencia:**
  - `Infra/Messaging/Email/EmailTemplateRenderer.cs:15-19` declara `DisplayFormatter? _formatter` y un segundo constructor.
  - Hay cuatro «`_formatter ?? throw`»: `:98,123,139` y `EmailTemplateRenderer.Invitation.cs:14`.
  - Estilos y colores en strings de C# (`:75`, `:184`).
  - Los avisos de organización y de página pública usan `AccountDeleted.html` y `AccountDeletionCancelled.html` (`:47,56,61`).
  - Tuplas posicionales de 6 elementos.
- **Refactor:**
  - Un solo constructor con el formateador obligatorio.
  - Plantillas parciales para el botón y la nota.
  - Plantillas genéricas con nombre propio.
  - Un record `NoticeContent`.
- **Esfuerzo:** S · al tocarlo.

### 15. [baja] Detalles de DI, contratos y valores mágicos
- `App/DependencyInjection.cs:110` y `:119` registran `LastTenantAdminGuard` dos veces; nadie inyecta la clase concreta.
- `new CultureProfiles(...)` aparece 9 veces (por ejemplo, `UserCultures.cs:16` y `LegalService.cs:25`). Conviene registrarlo en DI o volverlo estático.
- `ConnectController.cs:147-157` lee con `IConfiguration` la misma sección que `WebClientOptions` ya valida, pero esa clase es internal en Infra. Moverla a `App/Configuration/Auth` e inyectar `IOptions`.
- Result que nunca falla: `LoginMethod.Verify` (`Domain/Authentication/LoginMethod.cs:75-84`) y `ConnectAuthorizationService.cs:22-24`, cuyo fallo `ConnectController.cs:68-71` convierte en excepción. `UserRepository.cs:43-47,76-79` lanza a partir del Result de `ApplicationUser.Create/Rename`.
- El centinela `(ReauthAction)(-1)` en `ReauthController.cs:28,37`: conviene volver nullable la acción en el request de Application.
- La ventana de 5 minutos de la invitación tiene que coincidir en `Domain/Invitations/Invitation.cs:96`, `InvitationPreviewFlow.cs:26-28` e `InvitationSessionRestorer.cs:13`.
- Hay otros 5 minutos en `ReauthTicket.cs:36` y en `PendingDeletionCookie.cs:17`, y un lease de 15 minutos en `AccountDeletionProcessingService.cs:21`. Conviene pasarlos a constantes con nombre u opciones.
- `backend.md` §4.2 no coincide con el código: Services/Organizations, Profile/DestinationCodeVerifier y Legal/Participants. Conviene correr el arnés realinear-docs.
- **Esfuerzo:** S · al tocarlo.

## 3. Lo bien hecho, para conservar como patrón
- **Entidades de Domain** (`LoginCode`, `ReauthTicket`, `LoginAudit`, `LegalAcceptance`): fábricas que validan invariantes con excepción y reglas que devuelven Result, setters privados, UTC verificado, comparación en tiempo constante y sin datos personales en `ToString` (`LoginCodeDestination`, `IssuedLoginCode`).
- **Catálogos de errores** con códigos estables y metadata útil para el front: `LoginCodeErrors.Invalid(attemptsLeft)` y `ResendTooSoon(retryAfter)`.
- **Esqueleto de los servicios de entrada:** validar afuera → un solo límite → cookie y caché después del commit. Lo siguen `LoginCodeService`, `AccountDeletionService`, `ProfileService` y `ReauthService`, igual que `RoleService`.
- **`AccessSwitchPolicy`:** estática, pura, con desempate determinista y 155 líneas de tests. `ConnectService` también es limpio.
- **`LoginCodeIssuer` y `LoginCodeVerifier`:** lock por destino, espera para reenviar y ventana de pedidos con retryAfter, probados con fakes que fijan el orden de los locks.
- **Puertos de módulo** (`ILoginCodeChannel`, `IAccountNoticeChannel`, `IInvitationChannel`) y outbox cifrado, con un scope y un commit por mensaje y SKIP LOCKED.
- **`IAccountDeletionParticipant`** como punto de extensión de la baja.
- **`UserLookup`** como único adaptador de búsqueda global (ADR 0011).
- **Controllers de `Account/`** finos.
- **Tests de arquitectura** con casos de control ("Rogue") que prueban que el detector funciona.

## 4. Veredicto
Las piezas base están al nivel de Roles y de ArquitecturaBase: entidades, Result y errores, disciplina de UoW, puertos, outbox y controllers finos.
La orquestación no: unos 64 helpers para 20 servicios, muchos solo para no pasar de 8 dependencias, y las mismas reglas copiadas entre 3 y 8 veces (algunas ya divergen), con el ciclo de vida de la cuenta en Infrastructure.
Conviene consolidar antes de E8 en cuatro piezas cohesivas (`AccountRegistrar` con `LegalConsentRecorder`, un verificador de intentos, `LoginMethodLinker` y `AccountLifecycle` en Domain) y después cerrar las listas blancas de las guardas.
