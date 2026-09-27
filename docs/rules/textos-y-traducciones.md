# Textos y traducciones

**Regla:** todo texto que ve el usuario sale de resources, en **es** (neutral) y **en**. La cultura (`es-AR`, `en-US`) define el idioma y el formato.

## Cómo se hace
- **Archivos:** `Application/Resources/{Errors,Validation,Permissions,Notifications,Audit}.resx` + `.en.resx`. Los módulos tienen los suyos (`Modules/WhatsApp/Resources/`).
- **Acceso:** con los envoltorios `ErrorMessages`, `ValidationMessages`, `PermissionTexts`, `NotificationTexts` y `AuditTexts`. Sin `IStringLocalizer`.
- **En un request**, la cultura sale de `Accept-Language`. **En segundo plano** (correo, WhatsApp), se pasa explícita: la de la cuenta; si no hay, la de la organización; si no, `es-AR`.
- **Claves:**
  - errores: el código (`Roles.Role.HasUsers`) y `Title.<ErrorType>`;
  - permisos: `Permission.<código>`;
  - auditoría: `AuditAction.<código>`.
- **Estilo:** español rioplatense con voseo ("Ingresá", "Revisá"). "Tenant" nunca aparece: se dice "Organización". TenantAdmin es "Dueño" y CompanyAdmin, "Administrador".
- Identificadores, logs y mensajes de excepción, en inglés.

## Prohibido
- Literales en español o en inglés que vea el usuario.
- Una clave en un solo idioma, o placeholders distintos entre idiomas.
- Mandar textos de enums: el backend manda el valor (`"Active"`) y el front lo traduce.

## Copiá de
- `Application/Resources/ErrorMessages.cs` y `Errors.resx` (E1)

## Lo verifica
- `ResourceParityTests`: claves y placeholders iguales en es y en.
- `ErrorCodeTests`, `PermissionTextsTests`, `LocalizationTests` (fallback a es-AR).

## Detalle
[backend.md §12](../architecture/backend.md#12-idioma-traducciones-y-resources)
