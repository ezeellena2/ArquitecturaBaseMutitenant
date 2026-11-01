# E<N> · <Nombre> (spec)

Copiá esta plantilla para escribir la spec de una etapa, y borrá estas dos líneas. Sirve para programar sin adivinar: quien la toma no tiene que preguntar qué leer, qué pantallas copiar ni qué hacer primero. Tiene unas 4.000 palabras como mucho. `alinear-etapas` controla que estén las siete secciones marcadas con ✓.

**Estado:** pendiente · spec de la etapa N del bloque (decisiones …). Se contrasta con el código real con `/plan-etapa` cuando le toque, después de la E<N-1>.

## Qué leer ✓

Lo que tiene que leer quien programa antes de empezar, en orden:
1. **Lo anterior:** las secciones de las specs de las que depende esta etapa.
2. **Decisiones:** las de la §5 de la hoja de ruta que aplican.
3. **Fichas:** una fila por ficha de `docs/rules/` que aplica, con por qué.
4. **Código para copiar:** el archivo real de este repo, por pieza.
5. **Referencia de producto:** la maqueta y, solo para leer, ArquitecturaBase, de donde nunca se toma código. Mandan esta spec y las reglas y los diseños de este proyecto (108).

## Qué se construye

Las reglas de producto, por tema. Lo que hace cada cosa, no cómo se programa.

## Pantallas ✓

| Tablero | Qué muestra |
| --- | --- |

Cada tablero con su nombre real del lienzo, los estados que se dibujan, el recorrido guiado y lo que manda sobre el dibujo. El tablero muestra solo lo que existe hasta esta etapa (78).

## Contrato ✓

- **Rutas:** método, ruta, permiso, qué pide y qué devuelve. Las que cambian dicen qué suman.
- **Errores:** código, tipo y cuándo.
- **Idempotencia y concurrencia:** qué `POST` lleva `[Idempotent]` y qué lleva versión.

## Datos ✓

Tablas y columnas nuevas, con su tipo, regla, RLS, índices y migración. Si no hay, se dice: «Sin tablas nuevas».

## Pasos ✓

Back y front, por separado y en orden. Cada paso dice qué hace, sus archivos, el archivo que copia y su test.

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |

## Qué verificar al arrancar ✓

Lo que esta spec supone de etapas anteriores que todavía no existen en el código. Al arrancar, `/plan-etapa` lo contrasta y anota solo lo que cambió.

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |

## Decisiones tomadas

Lo que no se vuelve a preguntar.

## Fuera de esta etapa

Lo único, junto con «Requisitos previos» y «Qué verificar», que puede nombrar una etapa posterior (78).

## Cómo se cierra ✓

La puerta de la etapa, con la lista de control de `/puerta`, más lo propio: el recorrido real y, si la etapa manda algo por WhatsApp, la prueba con Meta.

## Requisitos previos

Las etapas cerradas y lo que hay que probar antes.
