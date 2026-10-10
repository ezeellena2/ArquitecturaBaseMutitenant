# Permisos

**Regla:** las rutas piden **permisos, nunca roles**. Hay cuatro catálogos: organización, empresa, personal (implícitos de la persona en su espacio personal) y plataforma.

## Cómo se hace
- **Organización:** `[Access(Business)] [HasPermission(Permissions.Users.Manage)]`.
- **Asignados y efectivos:** `IPermissionService.GetAssignedOrganizationPermissionsAsync` devuelve los permisos de roles organizacionales asignados, sin proyecciones. Se usa para el modo Inicio-Org/Inicio-Miembro. La autorización, el perfil y el contexto de tarjetas/pendientes usan `GetOrganizationPermissionsAsync`, que agrega las proyecciones explícitas permitidas. No se filtra un código de módulo para decidir el modo. `HomeTests` verifica permisos proyectados con feature apagada/encendida y un rol organizacional explícito que solo concede `assistant.use`.
- **Proyección desde empresa:** `Permissions.OrganizationProjectedFromCompany` es una lista explícita de permisos de organización que puede aportar una asignación empresarial activa. En E8b contiene solo `assistant.use`; conserva su alcance de organización y nunca aporta `assistant.configure` ni otros permisos. `PermissionService` intersecta esa lista con permisos de empresas, membresías, miembros y asignaciones activos del tenant vigente; no autoriza nombres de roles ni une todo el catálogo empresarial. Se verifica en `AssistantAuthorizationTests`, incluyendo revocación sin depender de un caché de asignaciones.
- **Empresa** (con `{companyId}` en la ruta): `[HasCompanyPermission(Permissions.Company.Members.Manage)]`. Si un permiso de la organización también alcanza, va como segundo argumento: la ficha de una empresa lleva `[HasCompanyPermission(Permissions.Company.Members.Read, organizationPermission: Permissions.Companies.Read)]`.
- **Plataforma:** `[HasPlatformPermission(PlatformPermissions.Tenants.Manage)]`.
- **Persona (B2C):** `[Access(Consumer)]` sin permiso, porque la persona los tiene todos sobre su espacio personal. Sobre un dato compartido, lo que puede hacer cada parte lo decide `PartyPolicy`, no un permiso.
- **Catálogo de la organización, sin prefijo** (como ArquitecturaBase): `users.read`, `users.invite`, `users.manage`, `roles.read`, `roles.manage`, `roles.assign`, `companies.read`, `companies.manage`, `settings.read`, `settings.manage` (incluye verificar el dominio de correo), `audit.read` y `publicpage.manage`; por empresa, `company.members.read` y `company.members.manage` ([backend.md §14](../architecture/backend.md#14-autorización)). Nunca `tenant.users.read`.
- **Catálogo de plataforma:**
  - `platform.tenants.read`;
  - `platform.tenants.manage`: aprobar, rechazar, suspender, reactivar y cerrar una organización, prender y apagar sus módulos, ver su dominio verificado y moderar su página pública (despublicar y "Permitir publicar");
  - `platform.accounts.read` (buscar y ver cuentas) y `platform.accounts.manage` (suspender, reactivar, cerrar sesiones e iniciar la baja con motivo);
  - `platform.recoveries.manage` (aprobar o rechazar "Recuperar mi cuenta");
  - `platform.legal.manage` (publicar una versión nueva de términos o privacidad);
  - `platform.operators.manage`, `platform.audit.read` y `platform.settings.manage`.
  
  Son nueve claves y se asignan individualmente a operadores exclusivos de Plataforma, sin roles Owner/Support. El correo invitado no puede pertenecer a una cuenta previa con perfiles de persona. La gestión de operadores protege al último operador activo con `platform.operators.manage` al quitar permisos, suspender o iniciar baja. `platform.whatsapp.manage` entra recién en la Etapa 11, porque antes ninguna ruta lo usa. R00 del plan E5 migra las asignaciones de T01/T03 conservando sus permisos efectivos.
- **Un permiso nuevo:**
  1. se declara en `Permissions.cs` (organización y empresa), `PersonalPermissions.cs` (`personal.*`) o `PlatformPermissions.cs` (`platform.*`), y en su lista `All` / `OrganizationScoped` / `CompanyScoped` (el personal y el de plataforma, en su `All`);
  2. lleva `Permission.<código>` y `PermissionDescription.<código>` en `Permissions.resx` y `.en.resx`;
  3. el seed se lo da a TenantAdmin (o CompanyAdmin, si es de empresa; o al operador técnico inicial, si es `platform.*`); uno `personal.*` no va al seed ni a ningún rol, porque la persona lo tiene implícito en su espacio personal;
  4. cuando cambian los permisos de un rol, se llama a `IPermissionService.InvalidateRoleAsync`.

## Prohibido
- `[Authorize(Roles = …)]`, `[Authorize(Policy = …)]` a mano, o preguntar por el rol en el código.
- Un permiso que no está en su catálogo.
- Prefijar los permisos de la organización (`tenant.users.read`) o sumar al catálogo uno que ninguna ruta usa todavía.
- Decidir el acceso en el front.

## Copiá de
- `Api/Controllers/Organization/RolesController.cs` (E4) · la guía [`permiso-nuevo.md`](../guides/permiso-nuevo.md) (E4)

## Lo verifica
- `PermissionAuthorizationTests` (E4): sin `Policy` ni roles a mano, y cada `[Has*Permission]` nombra un permiso de su catálogo; el permiso de empresa con la alternativa de la organización acepta cualquiera de los dos, siempre con la empresa en la ruta.
- `PermissionTextsTests` (E4): textos en los dos idiomas.
- Tests de integración (E4): 401 sin sesión y 403 sin el permiso.

## Detalle
[backend.md §14](../architecture/backend.md#14-autorización)
