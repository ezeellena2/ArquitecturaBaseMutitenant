# Revisión de código de las etapas 1 a 6: guía para Codex

> **Para Codex, que hace la revisión:**
> - **Qué busca el usuario:** saber cómo viene el código. Pidió esto: «solo realmente si hay código que esté realmente mal o mal arquitecturado, mal diseñado, haya cosas hardcodeadas o cosas que se puedan refactorizar para mejorar el código, así si en algún momento tengo que leerlo lo entiendo… no nos volvamos locos buscando cada fuga».
> - **No es una caza de bugs.** No recorras cada camino buscando fallas de seguridad: el aislamiento, los permisos y el guardado ya tienen guardas y tests. Si al leer ves algo claramente roto, anotalo; no lo salgas a buscar.
> - **No arreglás nada.** Solo reportás. El usuario decide qué se cambia y cuándo.
> - **Pocos hallazgos y que valgan.** No más de 30. Lo que se repite va en un solo hallazgo, con la lista de lugares. Nada de detalles de estilo que no cambian cuánto se entiende el código.
> - **Lo ya decidido no es un hallazgo:** las ADR, las fichas de `docs/rules/` y las «Decisiones tomadas» de los planes. Sí lo es si el código las contradice, o si una decisión deja el código difícil de entender: en ese caso proponé el cambio y decí qué decisión toca.
> - **Commits:** solo el informe, en español, con `git commit -m "…" -- <ruta>`. Todo en `main`, sin push. No arranques la Etapa 7.
> - **En paralelo con los arreglos.** Otro chat de Codex hace los [arreglos antes de la Etapa 7](2026-10-06-arreglos-antes-de-la-7.md) en la misma copia, así que vas a ver archivos modificados que no son tuyos.
>   - No los toques, no los commitees y no esperes a que la copia esté limpia.
>   - Identidad e ingreso, junto con Mi cuenta y el registro, va al final, recién cuando los arreglos A1 a A7 estén commiteados. Antes, ese código está cambiando.

## Alcance

- **Back:** `src/` de este repo; `tests/`, solo si un test es muy difícil de leer.
- **Front:** `src/` y `scripts/` de `../ArquitecturaBaseMutitenantFront`.
- **Afuera:**
  - las migraciones;
  - los archivos generados (`schema.d.ts`, `openapi.json`);
  - `docs/design/`;
  - WhatsApp, que es de las etapas 8 y 11.

## Cómo se trabaja

1. **Antes de empezar,** leé `docs/architecture/backend.md` y `docs/rules/capas-y-flujo.md` del back, y `docs/rules/estructura-y-features.md` del front. Son la forma acordada: una cosa que la sigue no es un hallazgo.
2. **Usá Roles como vara.** Roles es el área de referencia, en `areas/business/roles/` del front y en los servicios de Roles del back. Lo que está escrito muy distinto de Roles sin un motivo es una pista.
3. **Por área del producto,** en este orden:
   1. Roles y permisos;
   2. Organización: Usuarios, Empresas, Configuración, Página pública y Auditoría;
   3. Plataforma;
   4. las piezas comunes: `shared/` del front, y `Common` y `Persistence` del back;
   5. Identidad e ingreso, con Mi cuenta y el registro, cuando los arreglos estén commiteados.
4. **El informe crece por área:** al terminar cada una, sumala y commitealo.

## Qué buscar

1. **Mal arquitecturado:**
   - lógica de negocio en un controller, un repositorio o un reader;
   - un servicio que hace demasiadas cosas;
   - clases que solo pasan la llamada a otra;
   - un área que no sigue el recorrido `Controller → Service → Repository/Reader`;
   - en el front, una feature que importa de otra, o lógica de datos metida en un componente de pantalla.
2. **Mal diseñado:**
   - la misma regla escrita en dos o más lugares;
   - dos formas distintas de resolver lo mismo en áreas distintas, como paginar, traducir un error o abrir un diálogo;
   - abstracciones que no se entienden o que sobran;
   - métodos o componentes muy largos que conviene partir.
3. **Hardcodeado:**
   - números y textos mágicos;
   - listas en el código que tendrían que salir de los datos de referencia;
   - direcciones, puertos, plazos, topes y tiempos fijos que tendrían que ser configuración;
   - textos al usuario fuera de los recursos;
   - claves de permisos o de módulos escritas como texto suelto;
   - medidas o colores puestos a mano en el front, en lugar de los tokens del tema.
4. **Difícil de leer:**
   - nombres que confunden;
   - código muerto o comentado;
   - comentarios que no dicen la verdad, o lógica no evidente sin una línea que la explique;
   - condicionales anidados que se aplanan.
5. **Claramente roto**, si lo ves al pasar: un error evidente de lógica. No lo busques especialmente.

## El informe

En el back: `docs/reviews/<fecha>-revision-de-codigo.md`, con la fecha del día en que empezás. Lleva:

1. **Cómo venimos:** un resumen corto para el usuario, en castellano llano.
   - Qué está bien hecho.
   - Los 3 a 5 problemas que más pesan.
   - Si el código se entiende o no.
2. **Hallazgos**, agrupados por tema y del más importante al menos importante. Cada uno con:
   - **la importancia:**
     - **Importante:** mal arquitecturado o mal diseñado; cuesta entenderlo o cambiarlo;
     - **Conviene:** mejora clara de lectura o de orden;
     - **Menor:** se hace si sobra tiempo;
   - **dónde:** `archivo:línea`, o «en N lugares, por ejemplo …»;
   - **qué pasa** y por qué molesta al leerlo o al cambiarlo;
   - **la propuesta:** el cambio concreto, sin aplicarlo;
   - **el esfuerzo:** chico, mediano o grande.
3. **Lo que está bien:** las áreas que quedaron prolijas, en pocas líneas.

## Después

La Guía revisa el informe con el usuario. Él elige qué se arregla. Los arreglos van en un plan aparte, con sus tests como siempre, antes de la Etapa 7.
