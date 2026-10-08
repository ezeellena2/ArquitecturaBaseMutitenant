# ADR 0033 · Varios métodos de ingreso por cuenta

Estado: aceptada el 2026-09-27. La [0045](0045-mi-cuenta-sin-codigo-de-confirmacion.md) la modifica: Mi cuenta ya no pide el `ReauthTicket` para ninguna acción. La [fila 0033 del índice](README.md) y [multitenancy.md §3.1](../architecture/multitenancy.md#31-métodos-de-ingreso-la-cuenta-no-depende-de-un-solo-correo) son la especificación funcional completa.

## Contexto

Una persona puede ingresar a una organización con un correo que administra esa empresa y tener también un espacio personal. Si el correo deja de pertenecerle, la identidad personal no debe perderse ni quedar accesible para quien controle ese buzón.

## Decisión

- La identidad es global y admite varios métodos verificados: `Email`, `Phone` y `Google`. El valor de cada método es único en todo el sistema por `(Type, Value)`. Uno es el principal para avisos; su elección debe conservar al menos un método propio o activo.
- `identity.LoginMethods` es la fuente de correos y teléfonos. `AspNetUsers.Email` y `PhoneNumber` solo reflejan el método principal y no tienen unicidad propia. Un registro de Google puede no tener esas copias; la existencia de un método verificado se garantiza en la transacción de registro.
- Crear un método `Phone` no habilita un canal de ingreso. En 3a solo se registran los canales de correo y Google; el módulo de WhatsApp habilita el teléfono en la E8. Un método solo sirve para ingresar cuando está verificado y el canal está disponible.
- Los correos de dominios verificados por una organización pueden marcarse con `ManagedByTenantId`. Mientras la membresía siga activa sirven para ambos accesos; al terminar, dejan de servir. Sin otro método activo, «Necesita recuperación» es un estado derivado de consulta, no un valor de `UserStatus`.
- La gestión de varios métodos y los avisos por `IAccountNoticeChannel` llegan en 3b. **Regla vigente desde la 0045:** sumar correo o Phone, vincular Google, cambiar el principal y quitar un método requieren la sesión iniciada, sin `ReauthTicket` ni código a otro método. El método nuevo prueba su propia posesión con OTP o Google con OAuth, antiforgery y estado protegido ligado a la misma sesión. Se conservan las reglas de último método y los avisos. La verificación de dominio y revocación por fin de membresía llegan en E6; la recuperación asistida, en E5. El protocolo anterior de reautenticación fue reemplazado, no se copia en E8.

**Precisión vigente (0043; Phone también en 0049):** la unicidad `(Type, Value)` vale para los métodos verificados. Un método pendiente es único dentro de su cuenta y puede reclamarse al probar posesión. Si el destino ya está verificado en otra cuenta, se informa el error de campo antes de crear el pendiente/OTP y no se avisa al dueño. La precisión del 2026-10-04 basada en 0041 quedó reemplazada.

## Alternativas descartadas

- Una cuenta separada por cada correo, teléfono, organización o acceso: duplicaría a la persona y pondría en riesgo su espacio personal al cambiar de empresa.
- Usar `AspNetUsers.Email` o `PhoneNumber` como clave única: impediría varios métodos del mismo tipo y ataría la identidad a una copia mutable.
- Habilitar automáticamente teléfono por existir en el modelo: el canal es opcional y se registra con el módulo que lo implementa.

## Consecuencias

El registro y la gestión de métodos coordinan la identidad y sus métodos en una transacción. La unicidad se protege en la base, y las consultas de ingreso buscan por `LoginMethods`, no por las copias de Identity. Los avisos y la recuperación siguen disponibles cuando una empresa deja de administrar un correo.
