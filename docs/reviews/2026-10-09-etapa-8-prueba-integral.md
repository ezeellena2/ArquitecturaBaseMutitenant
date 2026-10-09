# E8 · Prueba integral provisional de Back y Front

Pedido: comprobar el encaje actual de la Parte 2a con el Front, sin cerrar E8 ni sustituir Meta real. Resultado: los recorridos automatizados ejecutados pasan. No se modificó el Front ni el contrato T01.

Código probado: Back `53d29eb6a9386d190133eb6598920a6094f31cfd` (Parte 2a más aislamiento del AppHost E2E); Front `44ef1092f25987d1ec3c0d806964eced0d226d4e` (Parte 2b). Se preservaron los cambios ajenos de la guía y los recorridos del Front.

## Resultados

| Verificación actual | Resultado |
|---|---|
| `dotnet build ArquitecturaBaseMultitenant.slnx -m:1 -p:BuildInParallel=false` | Código 0; 0 advertencias y 0 errores |
| Front `npm run build` | Código 0 |
| Front `npm run lint` | Código 0; conserva advertencias de las áreas y lienzos existentes |
| Front `npm run contracts:check` | Código 0; tipos coinciden con OpenAPI |
| Front `npm run test:e2e:real`, sin filtro | **36/36**, 0 fallos, 0 omitidos, 0 cancelados; 30 min 15 s |
| Sonda puntual E8 con Chromium, Kestrel y PostgreSQL reales | **18/18 grupos**, 9 en 1440 y 9 en 390; código 0 |
| Integración Back `--filter-class '*WhatsApp*'`, Docker | **128/128**, 0 fallos y 0 omitidos |
| Front: PhoneAuthentication, LoginLinkPage, RecoverAccountPage, AddLoginMethodDialog y ChangeLoginMethodDialog | **111/111**, cinco archivos |

El recorrido general usa el AppHost real, PostgreSQL efímero y correo pickup. Comprueba identidad, sesiones anteriores, ambos accesos, Plataforma, métodos de cuenta, baja/cancelación, recuperación por correo, roles, invitaciones, legales, publicación, directorio y regreso desde el sitio público, también en móvil. Su Aspire quedó apagado al finalizar.

La sonda E8 usa el Front existente, la API sobre Kestrel y `ApiFactory` con PostgreSQL migrado de Testcontainers. No simula API, base, cookie ni sesión. Lee los OTP emitidos en el **outbox cifrado real**, bajo la fixture Testing; no despacha a Meta. Inyecta un evento de webhook firmado de prueba y procesa la entrada mediante el servicio real. En cada ancho comprueba:

1. Registro personal Phone y autorización OIDC.
2. Agregado de otro Phone y verificación del OTP.
3. Principal y quitar con confirmación directa, sin reautenticación.
4. Ingreso Phone como persona y conservación de sesión al recargar.
5. Webhook persistente, bot, preview/redeem del enlace y OIDC nuevo con state, PKCE y canje de token.
6. Rechazo del enlace ya usado.
7. Solicitud de recuperación con Phone anterior y Phone nuevo verificado.
8. Registro empresarial Phone, creación de organización, OIDC y recarga.
9. Ingreso Phone por la puerta empresa.

La aprobación/rechazo de recuperación, carreras de ownership, outbox, privacidad, observer y retención se revalidaron en los 128 tests de integración del Back; la sonda de navegador no acredita el recorrido completo de aprobación Phone en Plataforma.

## Cambio y decisiones

- Commit `53d29eb6`: el AppHost aislado fija `WhatsApp__PhoneNumberId` vacío. El E2E general usa identidades ficticias y debe conservar el transporte Meta de Development fuera de su entorno, como ya hace con correo y Phone del operador.
- La sonda es un artefacto temporal bajo `.local/browser-e8/`, no un nuevo test de CI ni la implementación completa de T13. Usa el puerto 5176 con callback **y silent-renew** registrados; el certificado temporal se importa como PFX para Schannel y sus archivos se eliminan al terminar. Los fallos de preparación y selectores se corrigieron en la sonda; no exigieron cambios de producto.
- Se conserva la configuración y el recorrido productivos. No se cambió ningún callback de Meta ni se enviaron mensajes a números ficticios.

## Contrato

Diff vacío desde T01 en OpenAPI, los tres puertos neutrales de enlaces/observer y el schema del Front. SHA-256 con saltos LF:

- OpenAPI: `9bb3828bcb0ebe4e3217b8b70e3e153bd618e857cbb4223234c5c70967fc4078`.
- Schema: `fa516abe965ded09a8ffad6041f3a3183dcbe3e1bf31495f2391991f81a0c22d`.

## Lo que conserva Integración

E8 permanece abierta. Faltan Meta/túnel/delivered reales, prueba de retiro del módulo en copia descartable, puerta documental y recorrido final T13–T14. Los 18 grupos con outbox controlado no prueban Cloud API ni entrega al teléfono.

Integración debe incorporar estos casos al arnés versionado de T13, completar aprobación Phone en Plataforma y sus demás escenarios de error/carrera, y coordinar las propuestas de **Cambios al contrato**: metadata 501 de T01, retorno tras cancelar baja desde enlace y selección de WhatsApp desde el CTA externo de registro. Ninguna propuesta se aplicó en esta prueba.

No se repitió la suite completa de 2247 tests en este sondeo. Su última ejecución sigue siendo la [verificación de Parte 2a](2026-10-08-etapa-8-back.md): 2246 correctos y un fallo documental de `HarnessTests` en la ficha de teléfonos del Front. Esta prueba no declara esa puerta verde ni documentación en 0.

Evidencias locales: `.local/reviews/2026-10-09-integral-e2e.log`, `2026-10-09-integral-e8-probe.log`, `2026-10-09-integral-back-whatsapp.log`, `2026-10-09-integral-front-e8.log`, `2026-10-09-integral-build-final.log`, `2026-10-09-integral-front-build.log` y `2026-10-09-integral-front-lint.log`. Los certificados temporales y los procesos de la sonda se limpiaron.
