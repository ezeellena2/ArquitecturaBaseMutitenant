# Textos y traducciones

**Regla:** todo texto que ve el usuario sale de resources, en **es** (neutral) y **en**. La cultura (`es-AR`, `en-US`) define el idioma y el formato.

**Excepción:** los términos y la política de privacidad no van en resources; su texto está en `platform.LegalDocumentContents`, una fila por cultura ([datos-personales](datos-personales.md)).
Los nombres de monedas, países, ciudades horarias y tipos fiscales son **datos de referencia traducidos**, no textos de interfaz: salen de los JSON E1 y de las tablas E2 ([ADR 0036](../architecture/datos-de-referencia.md)).

## Cómo se hace
- **Archivos:** `Application/Resources/{Errors,Validation,Permissions,Notifications,Audit}.resx` + `.en.resx`. Los módulos tienen los suyos (`Modules/WhatsApp/Resources/`).
- **Acceso:** con los envoltorios `ErrorTexts`, `ValidationTexts`, `PermissionTexts`, `NotificationTexts` y `AuditTexts`. Sin `IStringLocalizer`.
- **En un request**, la cultura sale de `Accept-Language` y se acepta si está habilitada en `ICultureCatalog`. **En segundo plano** (correo, WhatsApp), se pasa explícita: la de la cuenta; si no hay, la de la organización; si no, la cultura marcada como predeterminada (`es-AR` en E1).
- **Claves:**
  - errores: el código (`Roles.Role.HasUsers`) y `Title.<ErrorType>`;
  - permisos: `Permission.<código>`;
  - auditoría: `AuditAction.<código>`.
- **Estilo:** español rioplatense con voseo ("Ingresá", "Revisá"). "Tenant" nunca aparece: se dice "Organización". TenantAdmin es "Dueño" (nunca "Administrador general") y CompanyAdmin, "Administrador". El acceso B2C se llama "Personal". Persona y empresa son los dos "lados" de la cuenta; la plataforma no es un lado. También se dice "Empresa", "Usuario" y "Miembro". Sobre usuarios: "Deshabilitar" / "Habilitar" (nunca "Activar") y "Revocar invitación". La lista completa está en el tablero "Palabras" del [lienzo versionado](../../../ArquitecturaBaseMutitenantFront/docs/design/lienzo/README.md).
- Identificadores, logs y mensajes de excepción, en inglés.

## Prohibido
- Literales en español o en inglés que vea el usuario.
- Una clave en un solo idioma, o placeholders distintos entre idiomas.
- Mandar textos de enums: el backend manda el valor (`"Active"`) y el front lo traduce.

## Copiá de
- `Application/Resources/ErrorTexts.cs` y `Errors.resx` (E1)

## Lo verifica
- `ResourceParityTests` (E1): claves y placeholders iguales en es y en.
- `ReferenceDataCatalogTests` (E1): traducciones de los catálogos para cada cultura habilitada y cadena de caída.
- `ErrorCodeTests` (E1), `PermissionTextsTests` (E4), `LocalizationTests` (E1): fallback a es-AR.

## Detalle
[backend.md §12](../architecture/backend.md#12-idioma-traducciones-y-resources)
