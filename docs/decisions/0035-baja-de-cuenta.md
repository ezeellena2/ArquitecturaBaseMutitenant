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
- **Copia compartida (Etapa 7):** `IConsumerBusinessShared` exige `ReplaceConsumerCopy()`. Cada entidad borra todos los datos personales copiados y conserva su resumen y su estado empresarial; repetirlo no cambia el resultado. Un marcador técnico distingue la copia borrada de un nombre que nunca se informó. El lector traduce `Legal.AccountDeletion.DeletedDisplayName`; el texto traducido no se guarda en la copia. Los datos copiados y el marcador llevan `[NotAudited]`.
- **Retención legal:** el módulo registra una política `IRetainedOnConsumerDeletion` en Application con el tipo compartido, un código de motivo y un plazo positivo. Domain no depende de ese puerto. Infrastructure comprueba que el tipo tenga una tabla compartida única en el modelo.
- **Ledger durable:** `identity.ConsumerDataRetentions` conserva sólo la clave `engagement.<tabla>`, los identificadores de la fila y sus partes, el motivo técnico, `RetainUntilUtc` y `CopyReplacedAtUtc`. El vencimiento y el motivo se fijan una vez; una política posterior no los modifica. Se conservan también los registros finalizados para que una reejecución no reprograme la obligación.
- **Purga al vencer:** el worker lista hasta 100 pendientes por corrida y procesa cada una en un scope nuevo, entrando al espacio Personal antes de abrir su UoW, aunque esté cerrado y sin membresía. Bloquea los metadatos con `FOR UPDATE SKIP LOCKED`, relee el vencimiento, reemplaza la copia y marca la finalización en el mismo commit. La lectura conserva EF/RLS `Parties` y omite sólo `SoftDelete`, para incluir copias de filas borradas lógicamente. No abre un lease adicional.
