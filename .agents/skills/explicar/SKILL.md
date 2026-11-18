---
name: explicar
description: Explica en lenguaje llano cómo funciona un área o un caso del sistema (por ejemplo identidad, multitenancy, permisos o «qué pasa cuando alguien entra como empresa»), leyendo en el momento su ficha, sus reglas y el código real del back y del front. Usala cuando pidan entender, repasar o que les expliquen una parte del sistema; solo lee, no cambia nada.
---

# /explicar <área, tema o pregunta>

Sirve para que una persona entienda el sistema. La explicación sale cada vez de los documentos y del código vigentes, y esta skill no guarda contenido propio: así nunca queda vieja. Si un documento y el código no coinciden, manda el código y se avisa.

## Qué hacer

1. **Ubicá la fuente.** Si no está claro de qué área es, buscá con Grep en `docs/features/` y `docs/rules/` antes de preguntar.

   | Si preguntan por… | Empezá por |
   |---|---|
   | un área: identidad, organizaciones, roles, personal, plataforma, sitio público, inicio, WhatsApp o asistente | `docs/features/<área>.md` |
   | organizaciones, accesos de persona y de empresa, subdominios o aislamiento | `docs/architecture/multitenancy.md` |
   | capas, carpetas o el recorrido de una petición | `docs/architecture/backend.md` y `docs/guides/leer-backend.md` |
   | un tema transversal: guardado, errores, fechas, montos, caché, cuotas, permisos… | su ficha en `docs/rules/`, con la tabla de `AGENTS.md` |
   | pantallas o cómo está armado el front | `docs/architecture/frontend.md` del front y su `src/areas/`, que se divide por acceso (`personal`, `business`, `platform`, `public`, `storefront`) |
   | por qué se decidió algo | `docs/decisions/README.md` y la ADR |
   | algo que todavía no está programado, como los formularios (E12 a E25) | su spec en `docs/plans/formularios/` y el plan de desarrollo, aclarando que es lo planeado |
   | cómo se configura o se opera (Google, Gmail, WhatsApp, secretos) | `docs/operations/` |

2. **Leé solo lo que hace falta:** las secciones de la ficha que tocan la pregunta. Para un caso concreto («qué pasa cuando…»), seguí en el código real el recorrido de `leer-backend.md`: controller → servicio → regla o error de Domain → repositorio o puerto. Sumá la pantalla del front si la pregunta la incluye. Si el área es grande, como Identidad o Plataforma, pedile a un subagente de búsqueda que devuelva solo el recorrido con `archivo:línea`.
3. **Confirmá cada conducta** que vayas a afirmar en el código o en un test de `tests/`. No expliques de memoria ni a partir de otra charla.

## Qué mostrar

Por defecto, para alguien que conoce el producto pero no el código de esa parte, en unas 400 a 800 palabras. Si piden detalle, más.

- **Para qué existe**, en una frase.
- **Cómo funciona**, en pasos cortos y con palabras de producto: qué ve y qué hace la persona, y qué hace el sistema. El nombre técnico va entre paréntesis la primera vez.
- **Un diagrama corto** cuando hay un recorrido: un ingreso, una invitación, una petición entre capas.
- **Dónde está:** de 3 a 6 archivos clave como enlaces `ruta:línea`, y la ficha que lo amplía.
- **Lo que no se puede romper:** las reglas que sostienen esa parte y el test que las verifica.
- **Lo que no coincide** entre la documentación y el código, si apareció.
- Al final, una línea que ofrezca profundizar en una parte concreta.

## Nunca

- **Cambiar archivos,** commitear ni levantar la app: esta skill solo lee.
- **Guardar la explicación** en un documento o en una skill nueva: sería una segunda copia que se desalinea («Ninguna regla está escrita dos veces», `docs/architecture/arnes.md`). Si la explicación muestra que a una ficha le falta algo, se propone el cambio en esa ficha.
- **Mostrar secretos ni datos reales:** de la configuración se nombran las claves, nunca sus valores.
