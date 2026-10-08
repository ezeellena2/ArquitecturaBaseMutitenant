# Etapa 8 · Consolidación y contrato

## Alcance

Parte 1 del [plan ejecutable](../plans/2026-10-07-etapa-8-whatsapp.md), sobre lienzo v115 `bb7c78de`. T00 se verifica y commitea antes de T01. No cierra E8 ni implementa el módulo o las pantallas. Trabajo en main, sin push.

## T00 · Consolidación verificada

- Cuenta, método, Personal y consentimiento son pasos separados; altas correo/Google/invitación/operador usan los helpers comunes. Invitación por OTP no consiente legales; operador no recibe Personal.
- Intentos activos fallidos y lockout se verifican en un helper común, manteniendo propósitos, auditoría y CommitPolicy de cada llamador.
- Identidad externa/correo → cuenta → destino de código; Mi cuenta, invitación de operador y recuperación usan el mismo lock que Google y releen propiedad. Vínculo verificado y copia del principal se comparten.
- `AccountLifecycle` concentra estados y fechas en Domain; Infrastructure aplica y anonimiza. Avisos usan `LoginMethodChange` y el renderer mantiene el mapeo localizado existente.
- Convención `Recorder` agregada a la lista cerrada, sin excepción por clase. No hay ReauthTicket ni alta de Phone.

## Validación T00

- Domain: 4 casos nuevos de ciclo de vida verdes, después del rojo por ausencia de la regla.
- Application: 514 tests existentes verdes y 7 nuevos focales verdes.
- Arquitectura: 149 tests verdes, incluyendo sufijos, capas y DI.
- Integración inicial: 116/117 verdes. La barrera del test de concurrencia se adaptó al lock temprano; luego sus 2 casos pasan. No se relajó la comprobación de consumo único.
- Google, operadores y seguridad: 30 casos existentes verdes; las 4 integraciones nuevas de carreras/rollback pasan después de corregir la configuración de redirecciones de sus clientes. Invitación sin consentimiento ya se verifica por las dos puertas en `InvitedLoginCodeTests`.
- Build de solución sin advertencias; no se afirma la puerta completa de E8, E2E, Meta ni túnel.

## Decisiones tomadas

- El reader transaccional `UserRepository.GetByIdAsync` observa primero las cuentas locales del contexto: una alta o estado preparado todavía no es visible en la base hasta el commit. Se conserva una sola forma de guardar.
- El linker permite omitir la marca de dominio en el alta de operador para conservar la conducta anterior; no amplía su acceso.

## T01

Pendiente de fijar y commitear el contrato HTTP/OpenAPI, puertos neutrales y tipos del front. No abrir los chats paralelos hasta registrar aquí ambos hashes.
