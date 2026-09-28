# El arnés: cómo se guía a quien programa (persona o IA)

> **Objetivo:** que nadie invente. Un agente que abre un archivo tiene que encontrar, en la misma carpeta o a un enlace de distancia, **la regla, el archivo modelo para copiar y el test que lo va a frenar si se equivoca**. El arnés del front es espejo de este: `../ArquitecturaBaseMutitenantFront/docs/architecture/arnes.md`.

## 1. Los cuatro niveles

| Nivel | Qué es | Dónde | Tamaño |
|---|---|---|---|
| 0. Índice | Reglas que no se negocian, "dónde va cada cosa" y la tabla "si vas a tocar X, leé Y" | `AGENTS.md` de la raíz (`CLAUDE.md` = `@AGENTS.md`) | ≤ 120 líneas |
| 1. Fichas | Una por tema, con formato fijo (§2) | `docs/rules/<tema>.md` | 20 a 60 líneas cada una |
| 2. Punteros por carpeta | Qué va acá, qué NO va, qué fichas leer y qué archivo copiar | `AGENTS.md` + `CLAUDE.md` (`@AGENTS.md`) en cada carpeta del mapa (§3) | 3 a 8 líneas en las carpetas de capa; una línea, a `docs/features/<área>.md`, en las carpetas de un área |
| 3. Verificación | Tests de arquitectura, analizadores, `BannedSymbols.txt`, `TreatWarningsAsErrors` | `tests/*.ArchitectureTests`, `.editorconfig` | una verificación por regla |

Además hay **recetas** (`docs/guides/`: prefijo de backend (E1), migración (E2), agregar un área y permiso nuevo (E4), quitar WhatsApp (E8)) y un **área de referencia** (Roles): código real para copiar, no ejemplos inventados.

**Cómo llega cada nivel al agente:**
- **Claude Code** carga el `CLAUDE.md` de la raíz al empezar y el de cada subcarpeta cuando lee archivos de ella.
- **Codex** lee los `AGENTS.md` desde la raíz hasta la carpeta en la que trabaja.

Por eso cada carpeta lleva los dos archivos: el `CLAUDE.md` solo importa el `AGENTS.md`, así el texto está escrito una sola vez.

**Por qué los punteros son cortos:** se cargan en cada sesión que toca la carpeta. El detalle vive en la ficha, que se lee solo si hace falta, y la ficha a su vez enlaza la sección de `backend.md`. Ninguna regla está escrita dos veces: un puntero **nombra** la ficha, no la copia.

## 2. Formato de una ficha (`docs/rules/<tema>.md`)

```markdown
# <Tema>

**Regla:** una o dos oraciones.

## Cómo se hace
- pasos concretos, con nombres reales de tipos y métodos
- un bloque de código mínimo, copiado del área de referencia (no inventado)

## Prohibido
- lo que rompe la regla, con el motivo en media línea

## Copiá de
- `ruta/al/archivo/real.cs`: qué mirar ahí

## Lo verifica
- `NombreDelTest` / analizador / BannedSymbols: qué falla y cómo se ve el error

## Detalle
- enlace a la sección de backend.md o multitenancy.md
```

Una ficha sin "Lo verifica" está **incompleta**. Un archivo de "Copiá de" o un test de "Lo verifica" que nace en una etapa posterior lleva la marca `(E#)` de la etapa donde nace, y `HarnessTests` lo exige recién cuando esa etapa cierra. Si la regla todavía no tiene un test previsto en ninguna etapa, se escribe `Pendiente: <test que falta>` y se suma a una etapa del plan, y desde ahí pasa a llevar `(E#)`.

## 3. Mapa de carpetas → punteros

Cada fila es una carpeta que lleva `AGENTS.md` + `CLAUDE.md`. Los punteros se escriben **al crear la carpeta**, en la misma tarea, en la etapa del plan donde nace. Rutas relativas a `src/ArquitecturaBaseMultitenant.<Proyecto>/`.

| Carpeta | Qué va / qué no | Fichas | Copiá de |
|---|---|---|---|
| `Domain/` | entidades, value objects, `<X>Errors`, catálogos. Sin paquetes, sin EF ni Identity | capas-y-flujo, result-y-errores, persistencia-ef | `Domain/Authorization/Role.cs` |
| `Domain/Common/` | `Entity`, `ValueObject`; marcas `IAuditable`, `ISoftDeletable`, `IVersioned` y las de las tres clases de datos: `ITenantOwned` (+ `ICompanyOwned`), `IPublishedByBusiness`, `IConsumerBusinessShared`; `Party`/`PartyPolicy`, `NotAuditedAttribute` y `TextLimits`. **No** se agregan marcas sin ADR | multitenancy, auditoria, concurrencia, textos-libres | — |
| `Domain/ValueObjects/` | `Money`, `Email`, `PhoneNumber`, `TaxId`: un dato con forma propia es un value object, nunca un `string` suelto | numeros-y-moneda, emails, telefonos, identificacion-fiscal | `Money.cs`, `Email.cs` |
| `Domain/Features/` | catálogo de módulos | modulos-habilitados | `Features.cs` |
| `Domain/<Módulo>/` | entidades y `<X>Errors` del módulo (hoy `Domain/WhatsApp/`); se borra junto con sus `Modules/` | modulos | — |
| `Application/Services/` | servicio + helpers (`Policy`, `Guard`, `Issuer`, `Verifier`, `Linker`) | guardado, result-y-errores, validacion, logs, multitenancy | `Services/Roles/RoleService.cs` |
| `Application/Interfaces/Services/` | una interfaz por servicio; lo único que inyecta un controller | capas-y-flujo | `IRoleService.cs` |
| `Application/Interfaces/Persistence/` | `I<X>Repository` (escribe), `I<X>Reader` (lee), `IUnitOfWork`, `ITenantScope`. Nada de `IQueryable` | persistencia-ef, paginado-y-busqueda | `IRoleRepository.cs`, `IRoleReader.cs` |
| `Application/Interfaces/Integrations/` | puertos a lo externo, por tema | capas-y-flujo, modulos | — |
| `Application/Models/` | `*Request`, `*Response`, `ReadModels/*Row`; montos en `Money`, fechas `*Utc`/`DateOnly` | numeros-y-moneda, fechas-y-zonas, paginado-y-busqueda | `Models/Roles/` |
| `Application/Validation/` | un validador por request, con `ValidationRules` | validacion, textos-y-traducciones | `CreateRoleRequestValidator.cs` |
| `Application/Resources/` | `.resx` es + en; la clave de un error es su código | textos-y-traducciones | `Errors.resx` |
| `Application/Common/Formatting/` | `DisplayFormatter` y perfiles de cultura. **Único** lugar que formatea en el back | numeros-y-moneda, fechas-y-zonas | — |
| `Application/Modules/<Módulo>/` | un módulo quitable; el núcleo no lo referencia | modulos | `Modules/WhatsApp/` |
| `Infrastructure/Persistence/Configurations/<Esquema>/` | una `IEntityTypeConfiguration` por entidad; `decimal` con precisión; enums como texto | persistencia-ef, numeros-y-moneda, multitenancy | `Tenant/RoleConfiguration.cs` |
| `Infrastructure/Persistence/Migrations/` | se generan con el comando de la guía; cada tabla nueva llama al helper RLS de su clase (`EnableTenantRls`, `EnablePublicRls` o `EnablePartiesRls`) + índices del `SortMap` | persistencia-ef, multitenancy, paginado-y-busqueda | `docs/guides/migracion.md` |
| `Infrastructure/Persistence/Repositories/` | EF para escribir un agregado; exige la transacción del caso de uso | guardado, persistencia-ef | `RoleRepository.cs` |
| `Infrastructure/Persistence/Readers/` | proyecciones `AsNoTracking` → `*Row`; `SortMap`, `ApplySearch`, `ToPagedResultAsync` | paginado-y-busqueda, multitenancy | `RoleReader.cs` |
| `Infrastructure/Persistence/Readers/Platform/` | **única** lista blanca para ignorar el filtro `"Tenant"` | multitenancy | — |
| `Infrastructure/Features/` | filtro de módulos por tenant | modulos-habilitados | `TenantFeatureFilter.cs` |
| `Infrastructure/Modules/<Módulo>/` | adaptadores del módulo | modulos | — |
| `Api/Modules/<Módulo>/` | lo HTTP del módulo (su `*ApiModule`, controllers como el del webhook, convención de rutas condicional); el núcleo no lo referencia | modulos | — |
| `Api/Controllers/` | controllers finos: contrato → servicio → `ToActionResult`; `[Access]` + permiso, `[PublicSite]` (subdominio de una organización publicada) o solo `[AllowAnonymous]` si el controller está en la lista de `AccessDeclarationTests` | api-http, permisos, multitenancy | `Organization/RolesController.cs` |
| `Api/Contracts/` | `*HttpRequest` / `*Query`, props nullable, `ToString()` sin datos personales; los de edición y borrado traen `version` | api-http, paginado-y-busqueda, concurrencia | `Organization/CreateRoleHttpRequest.cs` |
| `Api/Idempotency/` | `[Idempotent]` y su filtro; nada más va acá | idempotencia | `IdempotencyFilter.cs` |
| `Api/Json/` | conversores globales (UTC, `Money`, texto normalizado) | textos-libres, fechas-y-zonas, numeros-y-moneda | `NormalizedStringJsonConverter.cs` |
| `tests/*.Application.UnitTests/Services/` | tests del servicio con dobles a mano | tests | `Services/Roles/RoleServiceWriteTests.cs` |
| `tests/*.Api.IntegrationTests/` | rutas + aislamiento entre tenants | tests, multitenancy | `Organization/RolesTests.cs` |

**Ejemplo de puntero** (`Application/Services/AGENTS.md`):

```markdown
Servicios de casos de uso. Uno por área, con sus helpers (`Policy`, `Guard`, `Issuer`, `Verifier`, `Linker`).
No va acá: EF, HttpContext ni tipos de Infrastructure o Api.
Antes de escribir, leé: docs/rules/guardado.md · result-y-errores.md · validacion.md · logs.md · multitenancy.md
Copiá de: Services/Roles/RoleService.cs (el área de referencia).
```

**Carpeta de un área** (`Services/Roles/`, `Models/Roles/`, `Validation/Roles/`, `Domain/Authorization/`…): no es una fila del mapa. Lleva un `AGENTS.md` de una línea, "Antes de tocar esto, leé `docs/features/<área>.md`", más su `CLAUDE.md`, y se escribe cuando nace ese documento: Roles es la primera, en la Etapa 4. Las carpetas que se agrupan por acceso y no por área (`Api/Controllers/Organization/`, `Api/Contracts/Organization/`, `tests/*.Api.IntegrationTests/Organization/`) juntan varias áreas: su puntero lleva una línea por área, cada una con su `docs/features/<área>.md`, como `Api/Controllers/AGENTS.md` de ArquitecturaBase.

## 4. La tabla "si vas a tocar X, leé Y" (vive en el `AGENTS.md` raíz)

La tabla está solo en el [`AGENTS.md` raíz](../../AGENTS.md#antes-de-escribir-código-el-arnés) y no se copia en otro lado. Cada fila lleva a una ficha de `docs/rules/` o, si el tema es operativo, a una guía de `docs/guides/` o `docs/operations/`.

## 5. El arnés se verifica a sí mismo

`HarnessTests`, en `ArchitectureTests`, falla si:
1. una carpeta del mapa (§3) que ya existe, o una carpeta de un área que ya tiene su `docs/features/<área>.md`, no tiene `AGENTS.md` y `CLAUDE.md`, o su `CLAUDE.md` no es `@AGENTS.md`;
2. un `AGENTS.md` o una ficha tiene un enlace roto, sea a una ficha, a un archivo "Copiá de" o a una sección (salvo los marcados `(E#)` de una etapa que todavía no cerró);
3. una ficha no tiene las secciones del formato (§2), o su "Lo verifica" nombra un test que no existe, salvo que lleve `(E#)` de una etapa que todavía no cerró o diga `Pendiente:`;
4. el `AGENTS.md` raíz pasa las 120 líneas, o un puntero pasa las 8.

La última etapa cerrada la conoce `HarnessTests`, y se actualiza en la puerta de cada etapa (plan, puerta 7).

Así, mover un archivo modelo o renombrar un test rompe el build hasta que se actualiza la guía: la documentación no puede quedar vieja en silencio.

## 6. Mantenimiento

- **Regla nueva** → ficha (o sección de una ficha existente) + su verificación + fila en la tabla "si vas a tocar X, leé Y" del `AGENTS.md` raíz si corresponde, **en el mismo commit** que el código que la introduce.
- **Carpeta nueva del mapa** → sus dos punteros en la misma tarea.
- **Un agente inventó algo** → no se corrige solo el código: se busca qué nivel del arnés no lo guió (falta puntero, la ficha no era clara o falta test) y se arregla ese nivel.
- Al cerrar cada etapa, la puerta de documentación a ciegas (plan, Etapa 4) se repite con las preguntas de las áreas nuevas.
