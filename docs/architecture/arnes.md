# El arnés: cómo se guía a quien programa (persona o IA)

> **Objetivo:** que nadie invente. Un agente que abre un archivo tiene que encontrar, en la misma carpeta o a un enlace de distancia, **la regla, el archivo modelo para copiar y el test que lo va a frenar si se equivoca**. El arnés del front es espejo de este: `../ArquitecturaBaseMutitenantFront/docs/architecture/arnes.md`.

## 1. Los cuatro niveles

| Nivel | Qué es | Dónde | Tamaño |
|---|---|---|---|
| 0. Índice | Reglas que no se negocian, "dónde va cada cosa" y la tabla "si vas a tocar X, leé Y" | `AGENTS.md` de la raíz (`CLAUDE.md` = `@AGENTS.md`) | ≤ 120 líneas |
| 1. Fichas | Una por tema, con formato fijo (§2) | `docs/rules/<tema>.md` | 20 a 60 líneas cada una |
| 2. Punteros por carpeta | Qué va acá, qué NO va, qué fichas leer y qué archivo copiar | `AGENTS.md` + `CLAUDE.md` (`@AGENTS.md`) en cada carpeta del mapa (§3) | 3 a 8 líneas |
| 3. Verificación | Tests de arquitectura, analizadores, `BannedSymbols.txt`, `TreatWarningsAsErrors` | `tests/*.ArchitectureTests`, `.editorconfig` | una verificación por regla |

Además hay **recetas** (`docs/guides/`: agregar un área, permiso nuevo, migración…) y un **área de referencia** (Roles): código real para copiar, no ejemplos inventados.

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

Una ficha sin "Lo verifica" está **incompleta**. Si la regla todavía no tiene test, se escribe `Pendiente: <test que falta>` y se suma a la etapa que corresponde.

## 3. Mapa de carpetas → punteros

Cada fila es una carpeta que lleva `AGENTS.md` + `CLAUDE.md`. Los punteros se escriben **al crear la carpeta**, en la misma tarea, en la etapa del plan donde nace. Rutas relativas a `src/ArquitecturaBaseMultitenant.<Proyecto>/`.

| Carpeta | Qué va / qué no | Fichas | Copiá de |
|---|---|---|---|
| `Domain/` | entidades, value objects, `<X>Errors`, catálogos. Sin paquetes, sin EF ni Identity | capas-y-flujo, result-y-errores, persistencia-ef | `Domain/Authorization/Role.cs` |
| `Domain/Common/` | `Entity`, marcas `IAuditable`, `ISoftDeletable`, `ITenantOwned`. **No** se agregan marcas sin ADR | multitenancy, auditoria | — |
| `Domain/ValueObjects/` | `Money`, `Email`, `PhoneNumber`… | numeros-y-moneda | `Money.cs` |
| `Application/Services/<Área>/` | servicio + helpers (`Policy`, `Guard`, `Issuer`, `Verifier`, `Linker`) | guardado, result-y-errores, validacion, logs, multitenancy | `Services/Roles/RoleService.cs` |
| `Application/Interfaces/Services/` | una interfaz por servicio; lo único que inyecta un controller | capas-y-flujo | `IRoleService.cs` |
| `Application/Interfaces/Persistence/` | `I<X>Repository` (escribe), `I<X>Reader` (lee), `IUnitOfWork`, `ITenantScope`. Nada de `IQueryable` | persistencia-ef, paginado-y-busqueda | `IRoleRepository.cs`, `IRoleReader.cs` |
| `Application/Interfaces/Integrations/` | puertos a lo externo, por tema | capas-y-flujo, modulos | — |
| `Application/Models/<Área>/` | `*Request`, `*Response`, `ReadModels/*Row`; montos en `Money`, fechas `*Utc`/`DateOnly` | numeros-y-moneda, fechas-y-zonas, paginado-y-busqueda | `Models/Roles/` |
| `Application/Validation/<Área>/` | un validador por request, con `ValidationRules` | validacion, textos-y-traducciones | `CreateRoleRequestValidator.cs` |
| `Application/Resources/` | `.resx` es + en; la clave de un error es su código | textos-y-traducciones | `Errors.resx` |
| `Application/Common/Formatting/` | `DisplayFormatter` y perfiles de cultura. **Único** lugar que formatea en el back | numeros-y-moneda, fechas-y-zonas | — |
| `Application/Modules/<Módulo>/` | un módulo quitable; el núcleo no lo referencia | modulos | `Modules/WhatsApp/` |
| `Infrastructure/Persistence/Configurations/<Esquema>/` | una `IEntityTypeConfiguration` por entidad; `decimal` con precisión; enums como texto | persistencia-ef, numeros-y-moneda, multitenancy | `Tenant/RoleConfiguration.cs` |
| `Infrastructure/Persistence/Migrations/` | se generan con el comando de la guía; tabla nueva de `tenant` → `EnableTenantRls` + índices del `SortMap` | persistencia-ef, multitenancy, paginado-y-busqueda | `docs/guides/migracion.md` |
| `Infrastructure/Persistence/Repositories/` | EF para escribir un agregado; exige la transacción del caso de uso | guardado, persistencia-ef | `RoleRepository.cs` |
| `Infrastructure/Persistence/Readers/` | proyecciones `AsNoTracking` → `*Row`; `SortMap`, `ApplySearch`, `ToPagedResultAsync` | paginado-y-busqueda, multitenancy | `RoleReader.cs` |
| `Infrastructure/Persistence/Readers/Platform/` | **única** lista blanca para ignorar el filtro `"Tenant"` | multitenancy | — |
| `Infrastructure/Modules/<Módulo>/` | adaptadores del módulo | modulos | — |
| `Api/Controllers/<Área>/` | controllers finos: contrato → servicio → `ToActionResult`; `[TenantKind]` + permiso | api-http, permisos, multitenancy | `Organization/RolesController.cs` |
| `Api/Contracts/<Área>/` | `*HttpRequest` / `*Query`, props nullable, `ToString()` sin datos personales | api-http, paginado-y-busqueda | `Organization/CreateRoleHttpRequest.cs` |
| `Domain/ValueObjects/` (Email, PhoneNumber, TaxId) | un dato con forma propia es un value object, nunca un `string` suelto | emails, telefonos, identificacion-fiscal | `Email.cs` |
| `Domain/Legal/`, `Application/Services/Legal/` | términos y privacidad versionados, aceptación, exportar y dar de baja | datos-personales | `LegalAcceptance.cs` |
| `Domain/Features/`, `Infrastructure/Features/` | catálogo de módulos y el filtro por tenant | modulos-habilitados | `Features.cs` |
| `Api/Idempotency/` | `[Idempotent]` y su filtro; nada más va acá | idempotencia | `IdempotencyFilter.cs` |
| `Api/Json/` | conversores globales (UTC, `Money`, texto normalizado) | textos-libres, fechas-y-zonas, numeros-y-moneda | `NormalizedStringJsonConverter.cs` |
| `tests/*.Application.UnitTests/Services/<Área>/` | tests del servicio con dobles a mano | tests | `Services/Roles/RoleServiceWriteTests.cs` |
| `tests/*.Api.IntegrationTests/<Área>/` | rutas + aislamiento entre tenants | tests, multitenancy | `Organization/RolesTests.cs` |

**Ejemplo de puntero** (`Application/Services/AGENTS.md`):

```markdown
Servicios de casos de uso. Uno por área, con sus helpers (`Policy`, `Guard`, `Issuer`, `Verifier`, `Linker`).
No va acá: EF, HttpContext ni tipos de Infrastructure o Api.
Antes de escribir, leé: docs/rules/guardado.md · result-y-errores.md · validacion.md · logs.md · multitenancy.md
Copiá de: Services/Roles/RoleService.cs (el área de referencia).
```

## 4. La tabla "si vas a tocar X, leé Y" (vive en el `AGENTS.md` raíz)

| Si vas a… | Leé |
|---|---|
| guardar algo en la base | `docs/rules/guardado.md` |
| devolver un error o validar | `result-y-errores.md`, `validacion.md` |
| usar una fecha u hora | `fechas-y-zonas.md` |
| usar un monto, decimal o porcentaje | `numeros-y-moneda.md` |
| hacer un listado | `paginado-y-busqueda.md` |
| mostrar un texto al usuario | `textos-y-traducciones.md` |
| crear una entidad o una tabla | `persistencia-ef.md`, `multitenancy.md`, `auditoria.md` |
| exponer una ruta | `api-http.md`, `permisos.md` |
| loguear | `logs.md` |
| tocar WhatsApp u otro módulo | `modulos.md` |
| escribir tests | `tests.md` |
| agregar un área completa | `docs/guides/agregar-un-area.md` |

## 5. El arnés se verifica a sí mismo

`HarnessTests`, en `ArchitectureTests`, falla si:
1. una carpeta del mapa (§3) que ya existe no tiene `AGENTS.md` y `CLAUDE.md`, o su `CLAUDE.md` no es `@AGENTS.md`;
2. un `AGENTS.md` o una ficha tiene un enlace roto, sea a una ficha, a un archivo "Copiá de" o a una sección;
3. una ficha no tiene las secciones del formato (§2), o su "Lo verifica" nombra un test que no existe (salvo que diga `Pendiente:`);
4. el `AGENTS.md` raíz pasa las 120 líneas, o un puntero pasa las 8.

Así, mover un archivo modelo o renombrar un test rompe el build hasta que se actualiza la guía: la documentación no puede quedar vieja en silencio.

## 6. Mantenimiento

- **Regla nueva** → ficha (o sección de una ficha existente) + su verificación + fila en la tabla del §4 si corresponde, **en el mismo commit** que el código que la introduce.
- **Carpeta nueva del mapa** → sus dos punteros en la misma tarea.
- **Un agente inventó algo** → no se corrige solo el código: se busca qué nivel del arnés no lo guió (falta puntero, la ficha no era clara o falta test) y se arregla ese nivel.
- Al cerrar cada etapa, la puerta de documentación a ciegas (plan, Etapa 4) se repite con las preguntas de las áreas nuevas.
