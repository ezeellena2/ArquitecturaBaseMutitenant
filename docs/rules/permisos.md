# Permisos

**Regla:** las rutas piden **permisos, nunca roles**. Hay cuatro catálogos: organización, empresa, personal (implícitos del dueño) y plataforma.

## Cómo se hace
- **Organización:** `[TenantKind(Business)] [HasPermission(Permissions.Users.Manage)]`.
- **Empresa** (con `{companyId}` en la ruta): `[HasCompanyPermission(Permissions.Company.Members.Manage)]`.
- **Plataforma:** `[HasPlatformPermission(PlatformPermissions.Tenants.Manage)]`.
- **Personal:** `[TenantKind(Personal)]` sin permiso, porque el dueño los tiene todos.
- **Un permiso nuevo:**
  1. se declara en `Permissions.cs` (o `PlatformPermissions.cs`) y en su lista `All` / `OrganizationScoped` / `CompanyScoped`;
  2. lleva `Permission.<código>` y `PermissionDescription.<código>` en `Permissions.resx` y `.en.resx`;
  3. el seed se lo da a TenantAdmin (o CompanyAdmin, si es de empresa);
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
