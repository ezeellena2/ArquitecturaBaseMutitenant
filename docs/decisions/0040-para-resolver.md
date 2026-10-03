# ADR 0040 · «Para resolver» en el inicio: pendientes calculados, sin guardar

Estado: aceptada el 2026-10-03 por el dueño del producto, junto con tres tableros del lienzo v77:
- Inicio-Org (variante A);
- Inicio-Miembro (variante A);
- M-Inicio-Org (variante C, app).

Los tableros están en `../ArquitecturaBaseMutitenantFront/docs/design/lienzo`, front `88af3c4`, generador `generadores/inicio_1004.py`.

## Contexto

El inicio de una organización muestra arriba las áreas en tarjetas, cada una con un dato real, y abajo la lista «Para resolver». Había que decidir:
- qué entra en esa lista;
- para quién;
- si se guarda o se calcula, sin que el núcleo conozca los módulos.

## Decisión

1. **Entra solo un estado que ya existe y pide una acción:**
   - invitación vencida;
   - baja pedida;
   - dominio sin verificar;
   - página pública en borrador o despublicada por la plataforma;
   - número de WhatsApp sin medio de pago, cuando exista el módulo (Etapa 11).
2. **Solo lo ve quien tiene el permiso** para resolverlo.
3. **Solo entra si tiene una acción concreta:** cada fila lleva el enlace que lo resuelve (Cargar, Reenviar, Ver o Verificar).
4. **No se guarda: no hay tabla de pendientes.**
   - Se calcula al abrir el inicio, en un solo pedido de lectura.
   - Cada área aporta sus pendientes por un puerto del núcleo.
   - Los módulos, como WhatsApp, se enchufan por el mismo puerto, sin que el núcleo los conozca.
5. **La fila se va sola cuando el estado cambia.** No hay «descartar».

**Cómo se muestra:**
- **Orden:** primero lo urgente (en rojo) y después por fecha.
- **Columnas en la compu:** Pendiente (con su detalle), Área, Estado, Desde y la acción.
- **En Inicio-Miembro:** se suma la columna Empresa, y la tabla aparece solo si la persona puede resolver algo.
- **Sin pendientes:** dice «Nada para resolver».

## Alternativas descartadas

- **Una tabla de pendientes o de notificaciones guardadas:** duplica el estado, obliga a sincronizarlo y pide descartarlo a mano.
- **Que el inicio consulte cada área por su nombre:** el núcleo conocería a los módulos.

## Consecuencias

- **El puerto.** Hay un puerto nuevo en `Application/Interfaces`, por ejemplo `IPendingItemSource`.
  - Tiene una implementación por área, que devuelve solo lo que el usuario actual puede resolver.
  - Un servicio del inicio junta los pendientes y los ordena.
  - Es solo lectura: no abre una transacción de escritura.
- **Un pendiente nuevo** se agrega como un caso más en su área, sin tocar el inicio.
- **El aislamiento es el de siempre:** cada fuente lee con el tenant del claim y sus filtros RLS. Un pendiente de otra organización nunca aparece, y lo verifica un test de aislamiento.
