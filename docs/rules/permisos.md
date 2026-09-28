# Permisos

**Regla:** las rutas piden **permisos, nunca roles**. Hay cuatro catálogos: organización, empresa, personal (implícitos de la persona en su espacio personal) y plataforma.

## Cómo se hace
- **Organización:** `[Access(Business)] [HasPermission(Permissions.Users.Manage)]`.
- **Empresa** (con `{companyId}` en la ruta): `[HasCompanyPermission(Permissions.Company.Members.Manage)]`.
- **Plataforma:** `[HasPlatformPermission(PlatformPermissions.Tenants.Manage)]`.
- **Persona (B2C):** `[Access(Consumer)]` sin permiso, porque la persona los tiene todos sobre su espacio personal. Sobre un dato compartido, lo que puede hacer cada parte lo decide `PartyPolicy`, no un permiso.
- **Un permiso nuevo:**
  1. se declara en `Permissions.cs` (organización y empresa), `PersonalPermissions.cs` (`personal.*`) o `PlatformPermissions.cs` (`platform.*`), y en su lista `All` / `OrganizationScoped` / `CompanyScoped` (el personal y el de plataforma, en su `All`);
  2. lleva `Permission.<código>` y `PermissionDescription.<código>` en `Permissions.resx` y `.en.resx`;
  3. el seed se lo da a TenantAdmin (o CompanyAdmin, si es de empresa); uno `personal.*` no va al seed ni a ningún rol, porque la persona lo tiene implícito en su espacio personal;
  4. cuando cambian los permisos de un rol, se llama a `IPermissionService.InvalidateRoleAsync`.

## Prohibido
- `[Authorize(Roles = …)]`, `[Authorize(Policy = …)]` a mano, o preguntar por el rol en el código.
- Un permiso que no está en su catálogo.
- Decidir el acceso en el front.

## Copiá de
- `Api/Controllers/Organization/RolesController.cs` (E4) · la guía [`permiso-nuevo.md`](../guides/permiso-nuevo.md) (E4)

## Lo verifica
- `PermissionAuthorizationTests`: sin `Policy` ni roles a mano, y cada `[Has*Permission]` nombra un permiso de su catálogo.
- `PermissionTextsTests`: textos en los dos idiomas.
- Tests de integración: 401 sin sesión y 403 sin el permiso.

## Detalle
[backend.md §14](../architecture/backend.md#14-autorización)
