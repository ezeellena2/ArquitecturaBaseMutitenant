# Etapa 8 · B6 · Registro del bot para países habilitados

El bot ofrece el registro personal por WhatsApp a un desconocido sólo cuando el registro está abierto y el país del número admite altas. Con país excluido usa `Bot.Closed` y los botones existentes de ingreso y recuperación. Corrige el hallazgo B6 de la [revisión de E8](2026-10-09-revision-e8.md).

## Cambio y decisiones tomadas

- La rama de alta de `WhatsAppBotWriter` consulta `IWhatsAppChannelSettings.ValidateNewDestination`, con `LoginCodeDestination.ForPhone` y el `PhoneNumber` del contacto E.164 ya validado por el reader. Se reutiliza la política del módulo, sin prefijos ni parser nuevo.
- Las selecciones explícitas `bot.sign-in`/`bot.recover` siguen llevando a la web aunque el país esté excluido. Los snapshots de cuenta/método originales conservan su revalidación; un Phone verificado de Uruguay sigue recibiendo su enlace de Persona con sólo Argentina habilitada. El operador conserva exclusivamente su respuesta neutra, y el canal apagado no produce reply ni prueba.
- Se reutiliza `Bot.Closed` en es/en; no hay textos, rutas, errores HTTP, opciones ni cambios al contrato nuevos. La ficha funcional precisa los países y el CTA real con `channel=whatsapp`.
- Las pruebas integradas recorren webhook firmado, ACK sin cookie y entrada ya comprometida visible en un scope nuevo, reclamo/procesamiento real y outbox pendiente cifrado. No usan sender Meta. Cada host configura explícitamente Argentina; ninguna prueba modifica las opciones compartidas de otra clase. Las fechas y vencimientos salen del reloj inyectado y de la preparación del propio caso.

## Validación

Todos los comandos fueron seriales. Los builds usaron `-m:1 -nr:false -p:UseSharedCompilation=false`; los tests se ejecutaron sobre esos binarios con `--no-build --no-restore`. Los stdout originales, comandos, tiempos, exit codes y hashes están en `.local/reviews/e8b-contract/e8-bot-country-files.json`.

| Ejecución | Resultado original | Exit |
|---|---|---:|
| RED dirigido: desconocido UY, registro abierto, países `[AR]` | 1 ejecutado, 1 fallo de `Assert.Null(LinkUrl)`: recibía el CTA de alta | 2 |
| GREEN `WhatsAppBotTests` integrado completo | 15 ejecutados, 15 correctos, 0 fallos, 0 omitidos | 0 |
| GREEN `WhatsAppBotTests` y `WhatsAppChannelsTests` unitarios completos | 6 ejecutados, 6 correctos, 0 fallos, 0 omitidos | 0 |
| GREEN seguridad de enlaces: preview/canje Persona, rechazo de Business/Platform, Phone verificado tras reducción de países y lockout | 3 ejecutados, 3 correctos, 0 fallos, 0 omitidos | 0 |
| Build IT, incluidas dependencias Application y API | 0 advertencias, 0 errores | 0 |
| Build unitarias Application | 0 advertencias, 0 errores | 0 |
| `git diff --check` | Sin errores de whitespace | 0 |

El GREEN final cubre **24 casos distintos**, sin sumar repeticiones ni el RED. El caso de alta AR comprueba query sin teléfono, ausencia de fragmento y ninguna identidad, prueba o código nuevos; UY excluido comprueba texto cerrado, ambos botones, ausencia de enlace/CTA y ninguna identidad, prueba o código nuevos. Las regresiones incluyen registro cerrado AR, elecciones explícitas UY, propietarios originales AR/UY, operadores AR/UY, entradas anónimas o de antiguo dueño, suspensión UY, vencimiento de lockout y canal apagado.

Dos intentos previos no ejecutaron casos: el sandbox bloqueó el pipe del runner (exit `-532462766`) y el primer filtro de método no coincidió (exit 8). Se conservan como diagnóstico de infraestructura/filtro y se excluyen del RED y del total. El RED posterior sí ejecutó el caso sobre PostgreSQL Docker real. Los dos builds previos del test RED también quedaron retenidos, sin contarlos como casos.

## Alcance y pendientes

Entrega focal B6, con cinco fuentes propias e inventario SHA256. Inicio en `38febf92702426ef030141b51d14cd7785d47cea`; al entregar se observó `efa33c1a3092972be18f6c8cb68ffb650b04a38b`. El implementador no hizo stage, commit ni push; Root publica estas rutas. `Role.cs` ajeno conserva SHA256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`; OpenAPI conserva `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`.

No se ejecutó puerta integral, E2E, navegador, galería, retirada del módulo, detector global ni envío Meta. Los otros correctivos y el cierre de E8 siguen pendientes en su coordinación. Todos los comandos propios terminaron; no se levantó Aspire y los contenedores de las fixtures se dispusieron al finalizar cada test runner. Las fuentes quedan inmóviles al entregar.
