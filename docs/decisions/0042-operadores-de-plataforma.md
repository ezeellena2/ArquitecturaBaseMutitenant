# ADR 0042 · Operadores de plataforma

Estado: aceptada el 2026-10-03 por el dueño del producto, con el tablero «Cuentas y operadores» del lienzo. Las decisiones 16 y 17 de la Etapa 5 la completan. Reemplaza los roles Owner y Support del plan original.

## Contexto

El plan original daba a los operadores dos roles fijos, Owner y Support. El tablero aprobado elige los permisos de cada operador uno por uno. Además, una cuenta de operador mezclada con perfiles de persona o con organizaciones complica la auditoría y abre un camino para saltear permisos.

## Decisión

- **Permisos:** cada operador tiene sus permisos elegidos uno por uno entre las nueve claves de `PlatformPermissions`. No hay roles de plataforma.
- **Alta:** a un operador se lo invita por correo. La invitación crea una identidad exclusiva de Plataforma. Si el correo ya tiene cuenta, se rechaza: es la excepción de la [0041](0041-no-revelar-cuentas.md), porque «Invitar operador» pide `platform.operators.manage`, un permiso de confianza.
- **Cuenta exclusiva:** un operador nunca tiene perfiles de persona, nunca entra como persona y nunca entra a una organización (decisión 17 de la Etapa 5). Para actuar sobre una organización usa las rutas de Plataforma, con `ITenantScope.Enter` y su `SecurityEvent`.
- **Último administrador:** el último operador activo con `platform.operators.manage` no se puede quitar, suspender, dar de baja ni quedarse sin ese permiso. Un lock global serializa la regla.
- **Baja:** un operador no puede darse de baja ([0035](0035-baja-de-cuenta.md)).

## Alternativas descartadas

- **Roles Owner y Support:** no alcanzan para los permisos que muestra el tablero, y cada cambio de permisos obligaría a tocar los roles.
- **Un IdP externo para los operadores:** en la Etapa 9 tienen TOTP propio.

## Consecuencias

- **Modelo:** `PlatformPermissionAssignment` reemplaza a `PlatformRole` y `PlatformRoleAssignment`, con la migración `PlatformOperatorPermissions`.
- **Protección del último administrador:** está hecha (T12, `PlatformOperatorGuard`).
- **T16** suma la invitación de operadores. También los controles de que un operador no entra como persona ni a una organización: en `ConnectService`, en la elección del acceso y en las invitaciones, con sus tests.
- **Docs que todavía hablan de «rol Owner» o «rol de plataforma»:** `arbol.md` y `backend.md` §11. Se corrigen en la consolidación.
