# E8 · Revisión del pase de Back y Front

Revisión independiente del estado entregado por los chats «Back - Implementar WhatsApp Parte 2a» y «Front - Implementá WhatsApp T09–T12». El encaje probado funciona. No se encontró un fallo nuevo del backend en los recorridos ejecutados; quedan continuaciones compartidas y la puerta T13–T14. E8 permanece abierta.

Back revisado: `0374aede0f50d90dedcaefa5f1eb917904ea1cfd`; su código y tests no cambian desde `53d29eb6`. Front revisado: `44ef1092f25987d1ec3c0d806964eced0d226d4e`; los cambios sin commit en `docs/design/guia/` y `docs/design/recorridos/` son ajenos y se preservaron. No se modificaron fuentes, pantallas, configuración productiva ni contrato.

## Prueba repetida en esta revisión

| Comprobación | Resultado |
|---|---|
| Sonda E8 existente: Chromium + Front + Kestrel + PostgreSQL migrado | 18/18 grupos, nueve a 1440 y nueve a 390; salida 0 |
| Front: PhoneAuthentication, LoginLinkPage, RecoverAccountPage, AddLoginMethodDialog y ChangeLoginMethodDialog | 111/111; cinco archivos; salida 0 |
| `npm run contracts:check` | Salida 0; tipos coincidentes |
| Back `HarnessTests`, sin rebuild | 9/10; un fallo documental reproducido; cero omitidos |
| `realinear-docs --detectar`, ambos repos | 12 automáticos, 9 modelo, 1 humano; cero tokens; sin rama ni cambios del arnés |

La sonda repitió registro personal y empresarial Phone, agregado y OTP, principal/quitar sin reautenticación, ingreso por ambos accesos y recarga, webhook firmado persistente → bot → preview/redeem → OIDC con state/PKCE, enlace usado y solicitud de recuperación Phone. Usa el outbox cifrado real como transporte controlado de prueba; no despacha a Meta. El contenedor PostgreSQL y los certificados temporales quedaron eliminados al terminar.

Evidencia local: `.local/reviews/2026-10-09-pase-probe.log`, `2026-10-09-pase-front.log` y `2026-10-09-pase-harness.log`; detector `.local/arneses/realinear-docs/20261009-1050.md`.

El [informe integral anterior](2026-10-09-etapa-8-prueba-integral.md), cotejado con sus logs y los mismos commits de código, acredita además 36/36 E2E generales reales y 128/128 integraciones WhatsApp. La entrega Front acredita 2323/2323 tests completos y 100/100 comparaciones de galería. Esas suites completas no se repitieron en esta revisión, porque no cambió el código. La última suite completa Back sigue teniendo un fallo y no se declara verde.

## Continuaciones confirmadas en código

1. **Baja pedida desde Enlace.** Back ya devuelve `Identity.Account.PendingDeletion`, `cancelTicket` y retorno fijo consumer `/login/enlace`; cancelación restaura la cookie correspondiente. `LoginLinkPage.tsx` y `loginLinkState.ts` sólo presentan el error como reintento, sin la acción de cancelar. Front debe reutilizar la conducta de baja vigente, cancelar con el ticket y comenzar OIDC nuevo como Persona con state/PKCE; no volver a canjear el enlace consumido ni abrir una ruta protegida sólo con la cookie. Back conserva sus tests de retorno fijo, consumo, replay y revalidación. Es el encaje ya anotado en «Cambios al contrato», no un cuerpo nuevo.
2. **Entrada desde Crear mi cuenta del bot.** `WhatsAppBotWriter.cs` emite `/registro`; `SignupPage.tsx:66` sólo selecciona Phone si lo recibe por estado de navegación interno. El botón externo inicia Email y obliga a elegir WhatsApp manualmente. Recomendación: coordinar `/registro?channel=whatsapp` como marcador público de canal. Front lo interpreta sólo si la capacidad está disponible; conserva registro cerrado, términos y OTP. Back lo emite después de sincronizar esa conducta. No incluir teléfono, OTP, nonce, token ni identidad en la query; no cambiar OpenAPI por este marcador. Registrar el acuerdo antes de aplicarlo.
3. **Metadata temporal del contrato.** Runtime implementado conserva las descripciones/respuestas 501 de T01 por el freeze. La limpieza debe coordinarse en ambos repos: Back exporta desde código y Front regenera/checkea tipos; registrar los hashes nuevos. No introducir cambios de cuerpos o permisos aprovechando esa limpieza.
4. **Fallo documental reproducido.** `Front/docs/rules/telefonos.md`, líneas 49, 53 y 54, nombra `PhoneField.test.tsx`, `personalData.test.ts`, `RecoveriesPage.test.tsx`, `AuthCodeStep.test.tsx` y `PhoneAuthentication.test.tsx` sin `(E#)`. Front debe indicar la etapa real de cada referencia; no moverlas artificialmente a una etapa abierta para eludir la guarda. Después se repite Harness desde Back. El detector también pregunta qué ficha documenta los códigos `General`: Back debe documentar su alcance existente o justificar con precisión si son transversales. No silenciar el grupo sin revisar esos códigos.

## Continuación por chat

### Texto para «Back - Implementar WhatsApp Parte 2a»

```text
Revisá docs/reviews/2026-10-09-etapa-8-revision-del-pase.md y el informe integral anterior. La revisión independiente repitió 18/18 grupos E8 reales y confirmó que contrato y código entregados siguen iguales. No rehagas T02–T08 ni edites el Front.

Continuá con el alineamiento documental del Back mediante el arnés de su repo, incluido el hallazgo General sin feature. Conservá las guardas y documentá cualquier descarte preciso. Coordinemos los tres encajes ya anotados en Cambios al contrato: CTA externo /registro?channel=whatsapp, cancelación de baja desde Enlace con OIDC consumer nuevo y limpieza de metadata 501. No cambies contrato ni CTA unilateralmente; prepará el delta y registrá el acuerdo/hashes antes de aplicarlo. La cancelación del backend ya está implementada: mantené sus tests de retorno fijo, ticket, cookie y replay; verificá el caso completo después del ajuste del Front.

Dejá listos los apoyos del Back para T13: OTP/outbox accesible sólo en fixture aislada, eventos firmados y datos para aprobación/rechazo Phone en Plataforma, errores y carreras. Confirmá qué reutilizar de la sonda temporal al versionar el E2E. Después de las correcciones coordinadas, repetí las pruebas afectadas y la suite completa con Docker cuando el Front haya corregido su ficha. Entregá commits y resultados reales en main sin push, preservando cambios ajenos. Meta/túnel/delivered y retirar el módulo en copia siguen siendo T14; no declares E8 cerrada ni envíes a números ficticios.
```

### Texto para «Front - Implementá WhatsApp T09–T12»

```text
Leé C:\Users\ezequ\source\repos\ArquitecturaBaseMutitenant\docs\reviews\2026-10-09-etapa-8-revision-del-pase.md. Tu Parte 2b ya está entregada y la revisión independiente repitió 111/111 tests focales y 18/18 grupos contra API/PostgreSQL reales. No rehagas T09–T12 ni edites el Back.

Corregí las cinco referencias sin (E#) en docs/rules/telefonos.md con su etapa real y corré el detector de tu repo; mantené la guarda intacta. Completá en LoginLinkPage la continuación Identity.Account.PendingDeletion: mostrar la conducta vigente de baja, cancelar con cancelTicket e iniciar OIDC consumer nuevo con state/PKCE; no repetir redeem, guardar secretos ni hacer logout de la cookie recién restaurada. Cubrí ticket vencido, error/reintento y éxito con tests dirigidos. Copiá la conducta de LoginPage y el tablero vigente; si falta un estado visual, señalalo concretamente antes de inventarlo.

Prepará y coordinemos la selección de WhatsApp desde /registro?channel=whatsapp, sin número ni secreto en query; sólo cuando exista la capacidad, conservando términos, OTP, registro cerrado y el funcionamiento sin módulo. Backend debe emitirlo después del acuerdo registrado. Coordiná además la regeneración de tipos cuando se acepte limpiar metadata 501: no cambies schema a mano ni el contrato por tu cuenta.

Versioná en el arnés E2E de tu repo los casos E8 hoy presentes sólo en la sonda temporal, coordinando el soporte de fixture con Back. Completá el recorrido de aprobación/rechazo Phone en Plataforma y los errores relevantes a 1440/390. Actualizá los pares de galería afectados, verificaciones e informe; commits propios en main sin push. Preservá los cambios ajenos de docs/design/guia y recorridos. No declares E8 cerrada: Meta real y la puerta final quedan para Integración.
```

Ambos mensajes son propuestas para que el usuario los pase a los chats. No se enviaron automáticamente. Los cambios compartidos se registran y sincronizan mediante el usuario, conforme a [etapa-en-paralelo](../guides/etapa-en-paralelo.md).

## Puerta que sigue pendiente

T13 debe volver reproducibles en el arnés versionado los recorridos nuevos y completar aprobación/rechazo Phone en Plataforma, errores y carreras, además del encaje completo de cancelación y CTA. T14 conserva suites completas después de los cambios, documentos en cero, galería final, sesiones anteriores, plantillas Utility aprobadas, túnel y OTP/estados de entrega Meta reales, y retirada del módulo en copia con base migrada y vacía. Una galería simulada o los 18 grupos controlados no acreditan esa puerta.

El contrato continúa en los SHA-256 LF congelados: OpenAPI `9bb3828bcb0ebe4e3217b8b70e3e153bd618e857cbb4223234c5c70967fc4078`; schema Front `fa516abe965ded09a8ffad6041f3a3183dcbe3e1bf31495f2391991f81a0c22d`.
