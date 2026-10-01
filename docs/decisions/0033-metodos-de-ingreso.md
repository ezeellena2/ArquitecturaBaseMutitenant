# ADR 0033 · Varios métodos de ingreso por cuenta

Estado: aceptada el 2026-09-27. La [fila 0033 del índice](README.md) y [multitenancy.md §3.1](../architecture/multitenancy.md#31-métodos-de-ingreso-la-cuenta-no-depende-de-un-solo-correo) son la especificación funcional completa.

## Contexto

Una persona puede ingresar a una organización con un correo que administra esa empresa y tener también un espacio personal. Si el correo deja de pertenecerle, la identidad personal no debe perderse ni quedar accesible para quien controle ese buzón.

## Decisión

- La identidad es global y admite varios métodos verificados: `Email`, `Phone` y `Google`. El valor de cada método es único en todo el sistema por `(Type, Value)`. Uno es el principal para avisos; su elección debe conservar al menos un método propio o activo.
- `identity.LoginMethods` es la fuente de correos y teléfonos. `AspNetUsers.Email` y `PhoneNumber` solo reflejan el método principal y no tienen unicidad propia. Un registro de Google puede no tener esas copias; la existencia de un método verificado se garantiza en la transacción de registro.
- Crear un método `Phone` no habilita un canal de ingreso. En 3a solo se registran los canales de correo y Google; el módulo de WhatsApp habilita el teléfono en la E8. Un método solo sirve para ingresar cuando está verificado y el canal está disponible.
- Los correos de dominios verificados por una organización pueden marcarse con `ManagedByTenantId`. Mientras la membresía siga activa sirven para ambos accesos; al terminar, dejan de servir. Sin otro método activo, «Necesita recuperación» es un estado derivado de consulta, no un valor de `UserStatus`.
- La gestión de varios métodos y los avisos por `IAccountNoticeChannel` llegan en 3b. Sumar correo, vincular Google, cambiar el principal y quitar un método requieren un `ReauthTicket` específico, de un solo uso y de menos de cinco minutos. Se prueba posesión con un código enviado a un método verificado antes del inicio de la sesión actual; al cambiar o quitar debe ser distinto del objetivo. Ni la emisión ni la verificación permiten usar un método agregado después de ese inicio. El servidor sella el instante UTC en la cookie protegida y lo conserva al renovar tokens o cambiar acceso (`session_started_at`); el cliente no puede elegirlo. El alta de correo consume el ticket en su transacción y Google antes de iniciar OAuth, conservando antiforgery y el estado protegido. Ambos usan el diálogo existente. La verificación de dominio y revocación por fin de membresía llegan en E6; la recuperación asistida, en E5.

## Alternativas descartadas

- Una cuenta separada por cada correo, teléfono, organización o acceso: duplicaría a la persona y pondría en riesgo su espacio personal al cambiar de empresa.
- Usar `AspNetUsers.Email` o `PhoneNumber` como clave única: impediría varios métodos del mismo tipo y ataría la identidad a una copia mutable.
- Habilitar automáticamente teléfono por existir en el modelo: el canal es opcional y se registra con el módulo que lo implementa.

## Consecuencias

El registro y la gestión de métodos coordinan la identidad y sus métodos en una transacción. La unicidad se protege en la base, y las consultas de ingreso buscan por `LoginMethods`, no por las copias de Identity. Los avisos y la recuperación siguen disponibles cuando una empresa deja de administrar un correo.
