# El arnés: cómo se guía a quien programa (persona o IA)

> **Objetivo:** que nadie invente. Un agente que abre un archivo tiene que encontrar, en la misma carpeta o a un enlace de distancia, **la regla, el archivo modelo para copiar y el test que lo va a frenar si se equivoca**. El arnés del front es espejo de este: `../ArquitecturaBaseMutitenantFront/docs/architecture/arnes.md`.

## 1. Los cuatro niveles

| Nivel | Qué es | Dónde | Tamaño |
|---|---|---|---|
| 0. Índice | Reglas que no se negocian, "dónde va cada cosa" y la tabla "si vas a tocar X, leé Y" | `AGENTS.md` de la raíz (`CLAUDE.md` = `@AGENTS.md`) | ≤ 140 líneas |
| 1. Fichas | Una por tema, con formato fijo (§2) | `docs/rules/<tema>.md` | 20 a 60 líneas cada una |
| 2. Punteros por carpeta | Responsabilidad y enlace a su ficha o feature | `AGENTS.md` + `CLAUDE.md` (`@AGENTS.md`) en cada carpeta del mapa (§3) | 1 a 3 líneas en las carpetas de capa; una línea, a `docs/features/<área>.md`, en las carpetas de un área |
| 3. Verificación | Tests de arquitectura, analizadores, `BannedSymbols.txt`, `TreatWarningsAsErrors` | `tests/*.ArchitectureTests`, `.editorconfig` | una verificación por regla |

Además hay **recetas** (`docs/guides/`: prefijo de backend (E1), migración (E2), agregar un área y permiso nuevo (E4), quitar WhatsApp (E8)) y un **área de referencia** (Roles): código real para copiar, no ejemplos inventados.

**Cómo llega cada nivel al agente:**
- **Claude Code** carga el `CLAUDE.md` de la raíz al empezar y el de cada subcarpeta cuando lee archivos de ella.
- **Codex** lee los `AGENTS.md` desde la raíz hasta la carpeta en la que trabaja.

Cada raíz del mapa lleva los dos archivos; sus subcarpetas heredan ese puntero.

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

Una ficha sin "Lo verifica" está **incompleta**. Todo test de "Lo verifica" lleva la marca `(E#)` de la etapa donde nace; también la lleva un archivo de "Copiá de" que nace en una etapa posterior. `HarnessTests` exige su existencia recién cuando esa etapa cierra. Si la regla todavía no tiene un test previsto en ninguna etapa, se escribe `Pendiente: <test que falta>` y se suma a una etapa del plan, y desde ahí pasa a llevar `(E#)`.

## 3. Mapa de carpetas → punteros

El mapa de responsabilidades y punteros vive en [arbol.md](arbol.md#mapa-de-carpetas-punteros).

## 4. La tabla "si vas a tocar X, leé Y" (vive en el `AGENTS.md` raíz)

La tabla está solo en el [`AGENTS.md` raíz](../../AGENTS.md#antes-de-escribir-código-el-arnés) y no se copia en otro lado. Cada fila lleva a una ficha de `docs/rules/` o, si el tema es operativo, a una guía de `docs/guides/` o `docs/operations/`.

## 5. El arnés se verifica a sí mismo

`HarnessTests`, en `ArchitectureTests`, falla si:
1. una raíz de proyecto, área o módulo del mapa que ya existe, no tiene `AGENTS.md` y `CLAUDE.md`, o su `CLAUDE.md` no es `@AGENTS.md`;
2. un `AGENTS.md` o una ficha tiene un enlace roto, sea a una ficha, a un archivo "Copiá de" o a una sección (salvo los marcados `(E#)` de una etapa que todavía no cerró);
3. una ficha no tiene las secciones del formato (§2), o su "Lo verifica" nombra una clase C# `*Tests` sin método `[Fact]`/`[Theory]` o un archivo `.test.mjs`, `.test.ts` o `.test.tsx` que no existe, salvo que lleve `(E#)` de una etapa que todavía no cerró o diga `Pendiente:`; revisa el repo hermano cuando está presente;
4. el `AGENTS.md` raíz pasa las 140 líneas, o un puntero pasa las 3.

Las etapas cerradas viven en una lista explícita de `HarnessStage`, que usan `HarnessTests` y se actualiza en la puerta de cada etapa (plan, puerta 7). Las etapas pueden cerrarse fuera de orden; una referencia o test se exige solo cuando su etapa está en la lista. En particular, cerrar E6 después de E4 no cierra E5.

Así, mover un archivo modelo o renombrar un test rompe el build hasta que se actualiza la guía: la documentación no puede quedar vieja en silencio.

Hasta que ambos repos estén en GitHub, cada CI usa solo su propio checkout. Los enlaces al repo hermano se verifican cuando ese checkout está presente; el checkout cruzado y su verificación obligatoria se incorporan cuando ambos repos estén en GitHub.

## 6. Mantenimiento

- **Convención transversal nueva o modificada** → ficha (o sección de una ficha existente) + verificación pertinente, mediante un test existente o nuevo cuando haga falta, y fila en la tabla "si vas a tocar X, leé Y" del `AGENTS.md` raíz si corresponde, **en el mismo commit** que el código que la introduce. Una decisión local se registra en la feature o en una línea del plan de la etapa y, si agrega lógica, se cubre con su test de conducta; no crea una ficha ni una guarda de arquitectura por sí sola.
- **Carpeta nueva del mapa** → sus dos punteros en la misma tarea.
- **Un agente inventó algo** → se corrige el código. Si el error se puede repetir, conviene aclarar la ficha o el puntero que no lo guió, pero no es un paso del cierre.
- Al cerrar la etapa alcanza con el detector en 0 dentro de la puerta (`node scripts/arneses/arnes.mjs realinear-docs --detectar`). El arnés completo ([arneses ejecutables](../../scripts/arneses/README.md), [ADR 0037](../decisions/0037-arneses-ejecutables.md)), que arregla lo mecánico y deja en una rama aparte lo que hace falta mirar, se corre solo si el detector encuentra algo. `HarnessTests` sigue siendo la red final.

Durante una etapa, cada cambio coherente se verifica con su test focal y el build del proyecto afectado; las integraciones relevantes se suman al tocar rutas, persistencia, autenticación o aislamiento. La puerta completa de ambos repos, el E2E real y la comparación visual se ejecutan al cerrar la etapa, según el plan maestro. No se repiten por cada paso interno.
