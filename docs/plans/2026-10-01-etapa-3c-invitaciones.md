# Etapa 3c · Invitaciones — plan de implementación

> **Para quien ejecuta:** usar `executing-plans` para ejecutar este plan en este chat, tarea por tarea. La revisión previa del documento usa el revisor de `writing-plans`. Casillas para registrar avances, rojo/verde y commit; no se agregan guardas de arquitectura ni tests del arnés nuevos.

**Objetivo:** emitir por un helper interno una invitación a una organización y aceptarla desde `/invitacion`, con identidad previa o creando una identidad sin espacio Personal. La prueba final usa correo pickup, navegador, API y PostgreSQL reales y aislados.

**Arquitectura:** controller → `IInvitationService` → `InvitationService` → helpers/repositorios/puertos → Infrastructure. `InvitationIssuer` es un helper que trabaja dentro de la UoW de su llamador; no abre transacciones ni tiene ruta. `Invitation` y `Member` son privados de la organización, con filtros EF y RLS. Un token opaco protegido autoriza resolver el alcance antes de abrir la transacción; no se ignoran filtros.

**Tecnologías:** .NET 10, EF Core/PostgreSQL 18, Aspire 13.5, Identity/OpenIddict, outbox cifrado, React 19/Vite, TanStack Query y Playwright.

## Alcance, condiciones previas y fuentes

- [x] Git limpio en ambos repos, `main`; el usuario retiró el ZIP ajeno. Frenar de nuevo si aparece un cambio ajeno. Commits con rutas explícitas, nunca `git add .`, `commit -a` ni push.
- [x] Leer plan maestro, Etapa 3c/punto 7; `backend.md` §13, `multitenancy.md` §3/§3.1/§3.2 y las reglas de guardado, Result, validación, persistencia, aislamiento, auditoría, HTTP, idempotencia, fechas, correos, datos personales, logs, recursos y tests.
- [x] Leer `Invitacion`, `M-Invitacion` y la invitación por correo de `Mensajes`. Revisar escritorio 1440×900 y teléfono 390×844 con una invitación: composición breve, sin tabla de listado ni desborde horizontal. Repetir con el contrato real antes de programar el front.
- [x] **Condición de entorno:** el usuario confirmó que Aspire está apagado. Verificarlo antes del E2E; no detener un AppHost ajeno si aparece uno mientras se trabaja.
- [x] **Decisión de producto resuelta:** los campos de Empresa, Roles y rol del invitador se ocultan hasta que nazcan sus etapas (E6/E4), conforme a la decisión previa del usuario de ocultar los controles de rutas que todavía no existen. No se crean textos, roles ni empresas temporales.
- Cuenta y su rediseño quedan fuera. La emisión administrativa, reenvío y revocación HTTP pertenecen a E5/E6; el recorrido manual desde Usuarios se conserva para E6. WhatsApp no se habilita antes de E8.
- Leer el `AGENTS.md` local antes de editar cada carpeta. Cada carpeta nueva del mapa incorpora `AGENTS.md` y `CLAUDE.md` breves con responsabilidad y fichas reales, sin nuevas comprobaciones del arnés.

## Decisiones técnicas tomadas

1. **Invitado sin identidad:** `Member.UserId` permite `null` hasta vincularse; `Member.Invite()` crea esa reserva y `AssignUser` la vincula al aceptar. Una reserva retirada puede conservar `null`, pero nunca se activa sin identidad ni se vincula después de retirarse. No se crea una cuenta vacía en la emisión. Las membresías existentes conservan su conducta. El trigger del índice de accesos omite filas sin usuario y sincroniza al vincularlas; los readers de identidades proyectan solo miembros con usuario.
2. **Token y aislamiento:** un puerto `IInvitationTokenProtector` usa Data Protection con propósito exclusivo y un secreto aleatorio de 256 bits. El token protege TenantId, InvitationId y el secreto; en `Invitation` se persiste solo el hash. Antes de `Enter` se valida el token protegido; dentro del alcance se comprueba el hash y el estado reales. No se incorpora un índice global de invitaciones ni una excepción a los filtros RLS.
3. **Transporte:** los endpoints reciben el token en JSON, nunca query/path. El enlace del correo lleva la referencia opaca en el fragmento del SPA, que no viaja en la URL HTTP ni en Referer; el SPA lo retira con `history.replaceState` antes de consultar y no lo conserva en almacenamiento JS ni estado OIDC. Un adaptador de continuación guarda una copia protegida en cookie HttpOnly/Secure/SameSite Lax, limitada al flujo y al vencimiento, para volver del ingreso y recargar. Sin fragmento ni continuación válida se muestra «Ya no sirve».
4. **Sesión:** las rutas son anónimas y no reciben alcance del token bearer actual. Un puerto de contexto del flujo lee la identidad autenticada válida por bearer o cookie de Identity. La posesión de la invitación crea sesión solo para una identidad nueva; si el correo ya pertenece a una cuenta, exige ingresar como esa cuenta. Una sesión ajena no puede aceptar ni vincular el correo. Se relee esta propiedad después de los locks para cubrir una cuenta creada entre preview y accept. El lookup reconoce también el `ContactEmail` verificado de Google; un resultado ambiguo se rechaza, nunca crea otra identidad ni elige una arbitraria.
5. **Límites:** vencimiento configurable (`InvitationOptions`, siete días por defecto); todos los instantes salen de `TimeProvider`. Vista previa no consume; aceptar consume una sola vez, con locks y transacción única. Los métodos verificados ajenos se protegen y los pendientes ajenos se recuperan únicamente después de demostrar posesión mediante el token y la autorización de identidad requerida.
6. **Aviso de respaldo:** guardar el origen de invitación de un correo como metadata separada de `ManagedByTenantId`; sin dominio verificado no se marca administrado. El aviso de respaldo usa ese origen y desaparece al verificar un método propio adicional. Se conserva un método principal verificado y no se altera una cuenta previa que ya dispone de respaldo.
7. **Legal:** nueva cuenta exige `acceptedTerms: true`, sin casilla nueva; el botón y leyenda del lienzo son la aceptación. Guardar las dos versiones vigentes en la misma transacción, sin crear Personal ni aplicar ConsumerSignup al acceso empresarial. Las cuentas previas conservan el gate legal existente.
8. **Referencia ArquitecturaBase:** su invitación avisa a una cuenta ya creada y no contiene token; no sirve para copiar el modelo multitenant. Se reutilizan su separación de issuer/canal y la estructura de correo, respetando el outbox persistente de este proyecto.
9. **Ingreso antes de activar la membresía:** el ingreso admite el retorno local exacto `/invitacion`, exclusivamente por código o Google sin registro. Prueba la identidad y crea su cookie, sin iniciar OIDC ni seleccionar acceso: Business todavía rechaza `Invited` y Consumer crearía Personal. Tras el accept se autoriza `business` para la organización ya activa. `ReturnUrls`, los validadores, `LoginCodeVerificationFlow`, el desafío Google y `LoginPage` distinguen ese retorno fijo de `/connect/authorize`; no se amplía a rutas arbitrarias. Cancelar una baja conserva también ese retorno.
10. **Respuesta de accept perdida:** preview prepara un nonce aleatorio del navegador en la continuación HttpOnly, distinto del token. Solo el accept que crea una identidad guarda su hash y un permiso de bootstrap de cinco minutos. Después de accept o replay, el front refresca preview antes de authorize; dentro de esa ventana y con el nonce original puede restaurar la cookie de esa identidad activa. Otro navegador, token solo, cuenta previa o grant vencido no abren sesión. Logout común limpia la continuación y su nonce. Para la acción concreta «Salir y seguir» se conserva únicamente la continuación protegida de la invitación pendiente y se rota su nonce, de modo que no pueda restaurar la sesión cerrada. Una marca no secreta de retorno al flujo puede atravesar el logout; nunca contiene el token. No se guardan ni reproducen cookies Identity en el almacén idempotente; el filtro genérico conserva su conducta. Preview y accept validan antiforgery cuando puedan emitir/restaurar sesión, usando la emisión de token antiforgery ya disponible, sin ruta nueva.
11. **Invitaciones recibidas antes de registrarse:** la baja no puede descubrirlas desde `UserTenantAccesses`, porque `Member.UserId` sigue vacío. El reader de trabajo de baja agrega el catálogo global de organizaciones a los alcances ya vinculados; el worker entra una por una, antes de cada UoW, y el participante revoca solo destinos verificados de esa cuenta antes de purgar métodos. Sin índice global de invitaciones ni filtro ignorado. El barrido tiene un coste lineal aceptable para esta plantilla; su optimización queda para el backlog si no rompe el flujo.
12. **Locks compatibles:** emisión y aceptación comparten `google:email:` con el registro Google y `login-code:` con el alta por correo. La aceptación toma correo Google → cuenta existente conocida → destino → invitación/miembro. Luego relee el dueño; si apareció o cambió después de la búsqueda inicial devuelve el rechazo correspondiente y no adquiere un lock de cuenta nuevo después del destino. La emisión no modifica la cuenta y no necesita su lock. Emisión y aceptación comparten además el lock privado organización/usuario para que dos destinos verificados de una cuenta reserven un único Member. Se verifica la carrera alta/accept con una sola identidad resultante.
13. **Origen y respaldo:** `LoginMethod.InvitationOriginTenantId` es metadata de procedencia global, nullable y sin FK: no concede acceso ni convierte el método en administrado. Un respaldo personal exige método propio verificado y disponible en la configuración actual; el aviso de aceptar y el perfil reutilizan la misma regla. Si Google está deshabilitado, un método Google previo no cuenta como vía disponible hasta habilitar el canal.

## Mapa de archivos y responsabilidades

- Domain: `Invitations/{Invitation,InvitationStatus,InvitationChannel,InvitationErrors}.cs`; `Tenancy/Member.cs` y sus errores; metadata de origen en `Authentication/LoginMethod.cs`.
- Application: `Configuration/Invitations/InvitationOptions.cs`; `Models/Invitations/{IssueInvitationRequest,PreviewInvitationRequest,AcceptInvitationRequest,InvitationPreviewResponse,AcceptInvitationResponse,InvitationTokenData,InvitationNotice}.cs`; `Validation/Invitations/`; `Interfaces/Services/IInvitationService.cs`; puertos en `Interfaces/Persistence/{IInvitationRepository,IInvitationReader}.cs` e `Interfaces/Integrations/{Security,Request,Messaging}/`; `Services/Invitations/{InvitationIssuer,InvitationService,InvitationGuard,InvitationAcceptor,InvitationAccountRegistrar}.cs`.
- Infrastructure: `Persistence/Configurations/Tenant/InvitationConfiguration.cs`, `Persistence/{Repositories,Readers}/Invitation*.cs`, migración nueva y snapshot; token protector en `Security/`; `Messaging/Email/{EmailInvitationChannel,EmailTemplateRenderer}.cs` y `Templates/Invitation.html`; participante de bajas en `Identity/Deletion/`.
- API: `Controllers/Auth/InvitationsController.cs`; `Contracts/Auth/{PreviewInvitationHttpRequest,AcceptInvitationHttpRequest}.cs`; adaptador de identidad/continuación en `RequestContext/`; política de rate limit y registros DI de la capa dueña.
- Front: `areas/public/auth/api/invitations.ts`, `pages/InvitationPage.tsx`, componentes de estados; continuidad de ingreso/logout en `auth/` sin tokens JS persistidos; `shared/api/invitationContract.ts`, alias generados en `types.ts`, `locales/{es,en}/invitation.json`, ruta pública en `app/routes.tsx`.
- Ingreso compartido: `Application/Models/Auth/ReturnUrls.cs`, `Validation/Auth/{VerifyLoginCodeRequestValidator,ExternalSignInRequestValidator}.cs`, `Services/Auth/LoginCodeVerificationFlow.cs`, `Api/Controllers/Auth/ExternalLoginController.cs` y front `areas/public/auth/{pages/LoginPage.tsx,lib/returnUrl.ts}`. Ampliar `UserLookup` y su puerto para destinatarios por Email/Google; reutilizar el lock de correo compartido por código y Google, evitando órdenes de locks incompatibles.
- Tests: `Domain.UnitTests/Invitations/InvitationTests.cs`, `Application.UnitTests/Services/Invitations/InvitationServiceTests.cs`, `Api.IntegrationTests/Tenancy/InvitationsTests.cs`; tests front colocados junto al código y `src/test/invitation-contract.test.ts`; ampliar runner y preparador E2E existentes.
- Evidencia: `docs/reviews/2026-10-01-etapa-3c-invitaciones.md`; front `docs/design/capturas/etapa-3c/{manifest.json,informe.md,…}` y `scripts/capturar-etapa-3c.mjs` reutilizando helpers existentes.

## Ejecución por cambios coherentes

Cada tarea con lógica sigue: test dirigido → observar rojo → implementación mínima → verde → build del proyecto afectado (y lint del front) → integración/seguridad afectadas si corresponde → commit con las rutas de esa tarea. Registrar el rojo y el verde en el informe; no repetir la solución completa por cada paso.

### T01 · Reglas de invitación y miembro sin identidad (back)

- [x] Escribir `InvitationTests` y ampliar tests de `Member`: preview no consume; aceptación válida, repetida, revocada, límite exacto de vencimiento; token/hash no aparece en `ToString`; miembro sin usuario no se activa y solo se vincula una vez.
- [x] Ejecutar `dotnet test --project tests/ArquitecturaBaseMultitenant.Domain.UnitTests/ArquitecturaBaseMultitenant.Domain.UnitTests.csproj -- --filter-class "*InvitationTests"`; observar error por entidad ausente. Implementar fábrica/estados y `Member.Invite()`/`AssignUser`, preservando `Invite(Guid)`.
- [x] Verde del grupo y tests de Member: 13/13, sin omitidos; build Domain: 0 advertencias/0 errores. Commit `feat: modelar invitaciones y membresías pendientes` con archivos de Domain, tests y reglas locales nuevos.

### T02 · Persistencia, RLS e índice de accesos (back)

- [x] Test focal de `InvitationsTests`: reserva persistida Member Invited sin identidad no aparece en el índice global; fila privada de otra organización sigue invisible incluso con SQL runtime. T03 agrega emisión por issuer.
- [x] Agregar configuración, DbSet, puertos/adaptadores y migración. FK de Member.UserId nullable; índice único sigue protegiendo miembros vinculados. Invitation referencia Member dentro del mismo TenantId y tiene índice de hash único y fechas UTC. Índice único parcial `(TenantId, Destination)` para estado Pending; una segunda emisión pendiente devuelve conflicto (T03). Reenvío y reemplazo administrativo quedan para E5/E6. Aplicar `EnableTenantRls` y grants mínimos.
- [x] Adaptar el trigger `sync_user_tenant_access` mediante una migración nueva: eliminar entrada anterior al cambiar vínculo y retornar sin INSERT si NEW.UserId es null; nunca editar la migración ya aplicada. Revisar readers que requieren Guid y SQL Up/Down.
- [x] Comandos de `docs/guides/migracion.md`, SQL revisado, `has-pending-model-changes` limpio; 10/10 integraciones de invitaciones, índice, RLS y migraciones. Inventario existente de readers de identidad actualizado. Build Infrastructure 0 advertencias. Commit `feat: persistir invitaciones con aislamiento por organización`.

### T03 · Emisor, token protegido y correo real (back)

- [x] Tests dirigidos: token adulterado no autoriza un alcance, hash persistido sin token claro; canal no disponible no guarda; invitación a identidad existente guarda su Member Invited; nunca crea identidades en emisión; transacción revertida no deja invitación/outbox.
- [x] Implementar `InvitationIssuer.IssueAsync` dentro del alcance/UoW del llamador, lock de destino y miembro, cuenta/método previos (Email y Google verificados), Member Invited, Invitation, token protegido y `IInvitationChannel.EnqueueAsync` con datos reales y cultura/zona de organización.
- [x] Implementar correo HTML y texto desde resources es/en, usando DisplayFormatter para vencimiento y escapando nombres/URL. Canal cifra por el outbox existente; registrar DI. Sin datos literales de empresa/roles inexistentes.
- [x] Integración emisión/outbox y tests de plantillas verdes: 28/28 con regresiones de código y migraciones. Build Application/Infrastructure 0 advertencias; modelo EF sin pendientes. Commit `feat: emitir invitaciones por el outbox de correo`.

### T04 · Dos recorridos E2E reales rojos (back + front)

- [x] Ampliar `tools/ArquitecturaBaseMultitenant.RealE2ESetup/Program.cs` con orden local por archivo, solo bajo `MT_E2E_ISOLATED=1`, para que un servicio de soporte E2E abra scope/UoW y llame al issuer real. Sin ruta de emisión productiva; test prepara su organización, invitador y destinatario con cuenta.
- [x] Ampliar `scripts/test-e2e-real.test.mjs`: caso sin cuenta y caso con cuenta; leer enlaces reales del .eml sin imprimirlos; abrir `/invitacion`, comprobar preview, aceptar y entrar a la organización. Un comando de inspección del preparador confirma cero espacios personales para la identidad nueva; inspeccionar antes de cualquier cambio a Personal.
- [x] Correr `npm run test:e2e:real` con la preparación/issuer ya existentes y el accept/front todavía ausentes: los dos casos 3c fallaron al esperar la vista previa; los siete anteriores pasaron, sin skip ni mocks. Guardar salida en el informe.
- [x] Build preparador y AppHost 0 advertencias; front lint y sintaxis del runner limpios. Commit back `test: preparar invitaciones en la base E2E aislada`; front `test: agregar recorridos reales de invitaciones`.

### T05 · Vista previa, contexto de sesión y continuación (back)

- [x] `InvitationServiceTests` con dobles existentes: token vacío/adulterado, estado válido, vencido, revocado/aceptado, organización suspendida; sin cuenta, cuenta sin sesión, sesión propia y ajena. Preview no cambia ningún estado ni acepta términos.
- [x] Implementar el guard de token, `IInvitationReader`, contexto de identidad y continuación HttpOnly protegida. La cookie no concede sesión y un token nuevo reemplaza la continuación anterior. Resolver scope únicamente desde el token validado, antes de leer privado; no abrir UoW de lectura ni conservar una conexión abierta al cambiar alcance.
- [x] Proyectar nombres actuales y fechas desde contratos reales; cuenta eliminada del invitador se muestra con recurso «Cuenta eliminada». El estado de organización se relee, no se acepta un slug como autorización.
- [x] Verde focal: 15 unitarios y 9 integraciones; API build 0 advertencias. Commit `feat: consultar invitaciones sin consumirlas`.

### T06 · Aceptación atómica con y sin cuenta (back)

- [x] Integración roja: alta sin Personal con método verificado/principal y dos consentimientos; cuenta previa por correo y Google-only sin duplicación; dueño ambiguo rechazado; sin sesión previa 401/403; sesión ajena rechazada; usuario Suspendido/PendingDeletion/Deleted no se puede vincular; cuenta creada entre preview/accept exige ingreso; pendiente ajeno recuperado sin activar la cuenta anterior.
- [x] Implementar `InvitationAcceptor` y `InvitationAccountRegistrar`, validación afuera, Enter afuera, locks de invitación/destino/cuenta antes de reglas/escrituras, una UoW OnSuccess. Activar Member, consumir Invitation y actualizar acceso global por trigger en la misma transacción. Revalidar organización y miembro; activo/inactivo/removido no se reactiva mediante una invitación improcedente.
- [x] Emitir cookie Identity solo después del commit. Si ya había cuenta, conservar identidad y principal existente; no usar el enlace como ingreso automático a ella. No habilitar registro B2C ni provisionar Personal. Para alta nueva, registrar hash del nonce original y ventana bootstrap; preview restaura cookie solo para ese browser/identidad y revalida su estado.
- [x] Integración cubre concurrencia de dos accept: exactamente un ganador, sin identidad/membresía/consentimiento duplicados; replay idempotente HTTP no vuelve a emitir cambios. Cache de estado/perfiles invalidado después del commit.
- [x] Verde, build y controles de seguridad afectados. Commit `feat: aceptar invitaciones sin duplicar cuentas`.

### T07 · Respaldo de cuenta y baja (back)

- [x] Rojo inicial: `LoginMethodTests` no compiló porque faltaba `CreateInvitedEmail` y su metadata. La primera corrida de aceptación detectó además una expectativa que ignoraba la disponibilidad real de Google en Testing y el cliente de registro HTTP que OpenIddict rechazaba; se corrigieron la expectativa derivada de configuración y el HTTPS explícito del helper.
- [x] Agregar metadata de origen separada de dominio administrado; reutilizar una sola regla de método propio verificado/disponible en cuenta, perfil y respuesta de aceptación. Sin controles de WhatsApp.
- [x] `AccountDeletionTenantReader` agrega todas las organizaciones Business al catálogo de trabajo además de los alcances propios. El worker entra en cada alcance antes de la UoW. El participante bloquea destinos verificados, revoca solo invitaciones recibidas pendientes y conserva las enviadas; el reader presenta al invitador eliminado anonimizado.
- [x] Verde: `LoginMethodTests` 7/7; `InvitationAcceptanceTests` 20/20; `AccountDeletionProcessingTests` 3/3; `AccountDeletionParticipantsTests` 1/1; build API e integración 0 advertencias/0 errores; `has-pending-model-changes` sin cambios. PostgreSQL de Testcontainers aislado; commit `9163854 feat: conservar respaldo y ciclo de vida de invitaciones`.

### T08 · HTTP, rate limit y contratos cruzados (back + front)

- [x] Tests HTTP de preview/accept con cuerpos reales, cookies/bearer, antiforgery en accept y preview/bootstrap, ausencia de token en Location/URL/logs, rate limit 429, tokens de otra invitación y cambios de sesión; preview no requiere Idempotency-Key, accept sí. Pérdida de respuesta tras commit → replay misma clave → preview con nonce original → cookie/authorize business; otro browser y nonce/grant inválidos no autentican. Tests de ingreso con retorno exacto `/invitacion`: código y Google crean cookie sin tokens ni Personal aun con Member Invited; retorno externo o alterado se rechaza, estados de cuenta siguen bloqueados y baja cancelada vuelve al flujo.
- [x] Controller anónimo, contratos HTTP con ToString seguro, recursos de errores, Produces y política `invitation-accept`; registrar controller/rutas en inventarios existentes, sin nueva guarda.
- [x] Generar OpenAPI por la herramienta existente y `npm run contracts`. Esto actualiza también los comentarios documentales que dejaron rojo `contracts:check` en la revisión de 3b.
- [x] `src/test/invitation-contract.test.ts` lee controller/modelos/códigos del back y cliente/ruta/state del front: verbos/rutas, token en body, campos/enums, acceptedTerms, acceso business/tenant, retorno a /invitacion y errores. No usar mocks de un contrato inventado.
- [x] HTTP/contrato cruzado verdes y builds; commits back `feat: exponer vista previa y aceptación de invitaciones` (`81a01fd`), front `feat: actualizar contratos de invitaciones` (`3849299`).

### T09 · Página pública y estados del lienzo (front)

- [x] Usar solo los campos disponibles en E3c. Empresa, Roles y rol del invitador permanecen ocultos hasta E6/E4 según la decisión de producto ya resuelta. Revisar escritorio/móvil con una invitación del contrato real, nombre/correo largo y un vencimiento; si la composición falla con un elemento, proponer alternativas antes de programar.
- [x] Tests rojos de `InvitationPage`: Sin cuenta; Con cuenta sin sesión; Sesión propia; Sesión ajena; Vencida; Ya no sirve; Organización suspendida; Aceptada sin respaldo. Interacciones por rol accesible, resource parity, axe y 390 px.
- [x] Implementar ruta pública `/invitacion` con AuthLayout, textos y orden del lienzo; TanStack Query/httpClient para preview y useIdempotentMutation para accept. Vaciar el fragmento antes de consultar; ninguna credencial en QueryKey, storage, URL de retorno ni state OIDC. Datos de API y capacidades, no reglas de identidad en el cliente.
- [x] La cuenta previa usa `LoginPage` con el retorno fijo `/invitacion`: ingreso por código/Google crea cookie y vuelve usando la continuación protegida, sin `signinRedirect` antes de aceptar. «Salir y seguir» revoca la sesión anterior por el logout real, preserva solo la invitación pendiente protegida y rota el nonce para invalidar cualquier bootstrap; retoma el flujo sin registrar Personal incidentalmente. Logout común borra toda continuación. «Seguir como…» vuelve al acceso propio sin consumir invitación.
- [x] Aceptar nueva identidad obtiene tokens business para el tenant invitado. Mostrar el estado aceptado/aviso antes de sus acciones; «Agregar ahora» conserva business en /cuenta, «Más tarde» /org. Cuenta con respaldo termina en /org. F5 no cambia al acceso consumer.
- [x] Tests dirigidos verdes, lint/build. Commit `feat: implementar la pantalla de invitación` (`6692111`).

### T10 · E2E verde y casos críticos adicionales

- [x] Completar los dos E2E reales de T04; cuenta previa entra y acepta, nueva acepta y no tiene Personal, ambas conservan business/tenant después de F5. La base Development no se consulta ni se modifica.
- [x] Integración dirigida cubre lectura después de revocar/vencer/suspender, organización ajena, token adulterado, sesiones cruzadas, legal y eliminación. No crear matrices de segundo orden ni pruebas que repitan DTOs.
- [x] Ejecutar `npm run test:e2e:real`, guardar salida literal sanitizada sin códigos/links/emails y sin skip. Verde: 10 tests contados, 10 correctos, 0 fallos, 0 omitidos; salida completa en el informe. Ajustes de cierre: back `7853d2a`, front `c4ace06` y `94ce987`.

### T11 · Comparación visual y correo (front + back)

- [x] Reutilizar startCanvasServer/capturePaths/installApiMocks del arnés de capturas existente para fotografiar estados; los mocks aquí sirven solo para presentación y no sustituyen T10.
- [x] `scripts/capturar-etapa-3c.mjs` y manifest: ocho estados en 1440×900/390×844, carga/envío/error aplicables de las acciones, correo de invitación en es/en con HTML del renderer real y Mensajes como referencia.
- [x] Revisar cada par app/lienzo y cada correo; corregir diferencias de estructura, controles, orden, textos y tokens. Anotar datos omitidos autorizados, fechas/nombres contractuales y ajustes técnicos de foco/renderizado con motivo en `informe.md`. Sin renovar capturas de Cuenta.
- [x] Verificar manifest con el mecanismo existente y archivos presentes; no agregar un test del arnés. Commit front `test: comparar invitaciones con el lienzo` (`ed1e7d4`).

### T12 · Documentación, arnés existente y puerta completa

- [x] Completar `docs/features/identidad.md`, arquitectura/fichas afectadas y punteros E3 con archivos reales; `docs/backlog.md` solo para observaciones que no rompen flujo/seguridad. El informe registra tareas, commits, rojo/verde, E2E, decisiones y diferencias visuales.
- [x] Cambiar únicamente valores de cierre `HarnessStage` a 3 en ambos repos. Completar requisitos E3 de los inventarios/tests ya existentes; no borrar pruebas ni bajar su alcance para obtener verde. No iniciar E4/E6.
- [x] Back: `dotnet build ArquitecturaBaseMultitenant.slnx` (0 advertencias/0 errores); `dotnet test` con Docker, sin suites omitidas; `node --test scripts/datos-de-referencia/generar.test.mjs`; HarnessTests incluido. Resultado: build 0/0 y tests 1182/1182.
- [x] Front: `npm run lint`, `npm test`, `npm run build`, `npm run contracts:check`; harness visual existente y verificación de capturas etapa-3c; `npm run test:e2e:real` completo. El lint termina correctamente; sus warnings quedan explicados en el informe y el único warning de `InvitationPage` se anota en backlog.
- [x] Si los checks alteran un contrato o hacen necesario un fix, correr de nuevo solo los afectados y la puerta pendiente por ese cambio. `aspire ps` después del E2E confirma que no hay AppHost activo; el runner detuvo su propio AppHost y no se detuvieron AppHosts ajenos.
- [x] Commit de cierre en cada repo con las rutas verificadas. Git limpio, main, sin push. El informe conserva el recorrido manual de Usuarios para E6 y clasifica las diferencias visuales con su motivo.

## Puerta de salida y evidencia de aceptación

La etapa no termina hasta pasar T12 y los dos casos reales de invitación. El informe final debe enlazar cada tarea con su commit, incluir la salida E2E real, clasificar diferencias con el lienzo con su motivo y registrar decisiones. Ninguna cuenta, empresa, correo o token de prueba se siembra en Development. La evidencia de ausencia de Personal se toma inmediatamente después del accept, antes de entrar como persona.
