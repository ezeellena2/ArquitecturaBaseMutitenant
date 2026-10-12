# ADR 0042 · Operadores de plataforma

Estado: aceptada el 2026-10-03 por el dueño del producto, con el tablero «Cuentas y operadores» del lienzo. Las decisiones 16 y 17 de la Etapa 5 la completan. Reemplaza los roles Owner y Support del plan original.

## Contexto

El plan original daba a los operadores dos roles fijos, Owner y Support. El tablero aprobado elige los permisos de cada operador uno por uno. Además, una cuenta de operador mezclada con perfiles de persona o con organizaciones complica la auditoría y abre un camino para saltear permisos.

## Decisión

- **Permisos:** cada operador tiene sus permisos elegidos uno por uno entre las nueve claves de `PlatformPermissions`. No hay roles de plataforma.
- **Alta:** a un operador se lo invita por correo. La invitación crea una identidad exclusiva de Plataforma. Si el correo ya tiene cuenta, se rechaza: es la excepción de la [0041](0041-no-revelar-cuentas.md), porque «Invitar operador» pide `platform.operators.manage`, un permiso de confianza.
- **La invitación vence (precisión del 2026-10-04, la eligió el dueño del producto con el lienzo v90):**
  - queda pendiente hasta el primer ingreso con código a Plataforma, y vence a los 7 días, como la invitación a una organización;
  - mientras está pendiente, la cuenta figura en «Cuentas» como «Invitación pendiente» y no tiene sesiones; vencida, como «Invitación vencida», y no se puede entrar con ella;
  - «Reenviar invitación» renueva los 7 días y manda el correo de nuevo; «Revocar invitación» la borra y libera el correo;
  - el correo dice quién invita, los permisos y el vencimiento (Mensajes › «Invitación como operador»). Sale solo por correo.
  - Así, un correo mal escrito no deja una cuenta de operador viva para siempre.
- **Cuenta exclusiva:** un operador nunca tiene perfiles de persona, nunca entra como persona y nunca entra a una organización (decisión 17 de la Etapa 5). Para actuar sobre una organización usa las rutas de Plataforma, con `ITenantScope.Enter` y su `SecurityEvent`.
- **Último administrador:** el último operador activo con `platform.operators.manage` no se puede quitar, suspender, dar de baja ni quedarse sin ese permiso. Un lock global serializa la regla.
- **Baja:** un operador no puede darse de baja ([0035](0035-baja-de-cuenta.md)).

## Alternativas descartadas

- **Roles Owner y Support:** no alcanzan para los permisos que muestra el tablero, y cada cambio de permisos obligaría a tocar los roles.
- **Un IdP externo para los operadores:** en la Etapa 9 tienen TOTP propio.

## Consecuencias

- **Modelo:** `PlatformPermissionAssignment` reemplaza a `PlatformRole` y `PlatformRoleAssignment`, con la migración `PlatformOperatorPermissions`.
- **Protección del último administrador:** está hecha (T12, `PlatformOperatorGuard`).
- **La invitación con vencimiento (2026-10-04)** cambia lo que entregó T16, que crea la cuenta activa en el momento: hace falta el estado pendiente, el vencimiento, reenviar y revocar, con sus tests. La [decisión 23 del plan de la Etapa 5](../features/plataforma.md) queda corregida.
- **T16** suma la invitación de operadores. También los controles de que un operador no entra como persona ni a una organización: en `ConnectService`, en la elección del acceso y en las invitaciones, con sus tests.
- **Docs que todavía hablan de «rol Owner» o «rol de plataforma»:** `arbol.md` y `backend.md` §11. Se corrigen en la consolidación.
