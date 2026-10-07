# ADR 0035 · Baja de una cuenta

Estado: aceptada el 2026-09-27; precisada el 2026-10-03 por el dueño del producto. La [0045](0045-mi-cuenta-sin-codigo-de-confirmacion.md) modifica el requisito para pedir la baja: alcanza con la sesión iniciada. El cuerpo conserva la decisión original. El detalle de la conducta vigente está en [`multitenancy.md` §3.2](../architecture/multitenancy.md) y en la ficha [`identidad.md`](../features/identidad.md).

## Contexto

Una persona tiene una sola cuenta con dos accesos y puede ser Dueña de organizaciones. Darse de baja no puede dejar una organización sin quien la administre, ni borrar de golpe lo que una empresa necesita conservar.

## Decisión

- **Ciclo:** la baja da 30 días de gracia: `Active → PendingDeletion → Deleted`. Pedirla exige reautenticarse. Revoca todas las sesiones, y si la persona ingresa durante la gracia se le ofrece cancelarla.
- **Baja iniciada por la plataforma:** sobre una cuenta activa, la cuenta pasa a `PendingDeletion` y la persona puede cancelar ingresando antes de la fecha. Sobre una cuenta suspendida, sigue `Suspended` con la fecha y no puede cancelar. Si la reactivan antes de la fecha, pasa a `PendingDeletion`.
- **Quién no puede darse de baja:**
  - el único Dueño de una organización **no cerrada**, incluidas las suspendidas (precisión del 2026-10-03, para la baja que pide la persona y para la que inicia la plataforma);
  - los operadores de plataforma ([0042](0042-operadores-de-plataforma.md)).
- **Qué hace la eliminación:**
  - anonimiza la identidad (la fila queda);
  - borra los métodos de ingreso y el espacio personal;
  - quita las membresías;
  - anonimiza la copia en los datos compartidos;
  - conserva la auditoría.
- **Extensión:** cada módulo suma lo suyo con `IAccountDeletionParticipant`.

## Alternativas descartadas

- **Que bloqueen solo las organizaciones activas:** al reactivar una organización suspendida, nadie podría administrarla.
- **Borrar al instante:** no deja corregir un error ni cancelar.

## Consecuencias

- La baja que pide la persona hoy mira solo las organizaciones activas. Se unifica con la regla «no cerrada» en la consolidación previa a la Etapa 7 (D5), con un test que use una organización suspendida.
- Los avisos de la baja salen por `IAccountNoticeChannel`. El de la baja iniciada por la plataforma tiene dos variantes y su texto lo define el lienzo (Mensajes).
