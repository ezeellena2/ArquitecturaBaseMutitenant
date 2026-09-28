# Módulos habilitados por organización (feature flags)

**Regla:** una funcionalidad **vendible u opcional** es un **módulo** (feature). Su controller lleva `[FeatureGate]`, y la plataforma decide qué módulos tiene cada organización (y cuáles están disponibles para el acceso B2C). **Módulo ≠ permiso:** primero se pregunta si la organización tiene el módulo, y después si la persona tiene permiso.

| | Módulo | Permiso |
|---|---|---|
| Pregunta | ¿**esta organización** tiene "Reportes"? | ¿**esta persona** puede ver reportes? |
| Lo decide | la plataforma (plan, prueba, habilitación) | el administrador de la organización (roles) |
| Si no | **404** (el módulo "no existe") | **403** |

## Cómo se hace
- **Librería:** `Microsoft.FeatureManagement.AspNetCore`.
- **Catálogo en código:** `Domain/Features/Features.cs`, con constantes como `Features.Reportes`. Cada módulo declara de qué lado vive (`Consumer`, `Business` o los dos, si usa datos compartidos) y si arranca prendido.
- **Por tenant:** `platform.TenantFeatures` (`TenantId`, `FeatureKey`, `Source` = `Plan` | `Manual` | `Trial`, `ExpiresAtUtc?`).
- **Filtro:** `TenantFeatureFilter` (el filtro `"Tenant"` de la librería) lee los módulos del tenant activo con caché `t:{tenantId}:features`, que se invalida al cambiarlos.
- **Configuración** (`appsettings`): cada módulo se declara con `EnabledFor: [{ "Name": "Tenant" }]`. Ponerlo en `false` es el **apagado de emergencia** para todos.
- **Controller de un módulo:** `[FeatureGate(Features.Reportes)]` + `[Access]` + permiso. Apagado, responde 404 ProblemDetails (`DisabledFeatureHandler`).
- **Workers y correos del módulo:** preguntan `IFeatureService.IsEnabledAsync(Features.X)` (un puerto de Application, sobre `IFeatureManager`) antes de hacer algo por un tenant.
- **`GET /api/me`** devuelve `features: ["reportes", …]` del acceso activo. Un módulo de datos compartidos está prendido para una persona si lo está para la organización con la que interactúa.
- **Plataforma:** la ficha de una organización tiene la sección "Módulos", para prenderlos, apagarlos y fijar la fecha de fin de una prueba, con motivo y `SecurityEvent`.
- **El núcleo no es un módulo:** usuarios, roles, empresas, configuración, auditoría y la página pública (con el directorio) están siempre prendidos. Que una organización publique su página o no es su estado (`Draft`/`Published`), no un módulo.

## Prohibido
- `if (tenant.Plan == "Pro")` en el código.
- Chequear el módulo solo en el front.
- Usar un permiso para simular un plan, o un módulo para simular un permiso.
- Un módulo sin su entrada en el catálogo.
- Dejar flags viejos: un módulo que ya está prendido para todos se saca del catálogo con su ADR.

## Copiá de
- `Infrastructure/Features/TenantFeatureFilter.cs` (E5) · la guía [`agregar-un-area.md`](../guides/agregar-un-area.md) (E4), paso "¿es un módulo?" (E5).

## Lo verifica
- `FeatureGateTests` (E5): un módulo apagado da 404 y prendido da 200; el apagado de emergencia gana sobre la organización.
- `ModuleControllersTests` (E5): todo controller fuera del núcleo (el núcleo incluye `PublicSite/` y `Organization/PublicPageAdminController`) tiene `[FeatureGate]` con una clave del catálogo (test de arquitectura).

## Detalle
[backend.md §20](../architecture/backend.md#20-reglas-de-datos-que-se-aplican-solas) · front: `docs/rules/accesos-y-permisos.md`
