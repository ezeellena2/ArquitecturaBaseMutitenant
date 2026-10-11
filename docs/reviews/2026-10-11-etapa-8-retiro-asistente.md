# Etapa 8 · Fallos al retirar el asistente

## Corte y resultado

Medido en copias descartables de `puerta-8-5`: Back `ff5f4d9333ba040693d9dc409a8becfc67ab29c5` y Front `ad092b6586fac9d33d5416e1ff60d20bf03887b4`, con la preparación de tests autorizada `90`/`96`. No se incorporaron las fuentes nuevas M3 del asistente ni su SDK de main. Sin push.

| Retiro | Resultado |
|---|---|
| WhatsApp | **2556/2556**, cero fallidos y omitidos; bases vacía/previa, modelo, Up → Down → Up, conservación de datos/RLS y núcleo correctos (`101`, `113`, `115`). |
| Asistente | Build completo sin advertencias ni errores; ambas bases, modelo, Up → Down → Up, conservación de datos/RLS y probes del núcleo correctos. Suite retenida: **2555/2562**, siete fallidos, cero omitidos (`105`–`108`, `116`). |
| Ambos | **No iniciado**, detenido por el resultado del asistente. |

Los dos contenedores propios del asistente están eliminados, comprobados por nombre exacto (`117`). Los resultados anteriores y sus logs se conservan; no se reemplaza un fallo histórico por el resultado de un focal.

## Los siete fallos

Todos vuelven a Back. El diagnóstico no cambió producto, assertions, migraciones ni datos de las copias.

| Casos | Archivo y escenario | Causa y propuesta |
|---|---|---|
| 1 | [UniqueKeys.cs:11](../../src/ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/UniqueKeys.cs:11), detectado por [DbContextModelTests.cs:117](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Persistence/DbContextModelTests.cs:117). El inventario del núcleo exige un índice que desaparece con el módulo. | **Dependencia real del producto:** `AssistantModel` sigue en `UniqueKeys` del núcleo, aunque su tabla pertenece sólo al asistente. Mover la constante al árbol del módulo copiando [UniqueKeys de WhatsApp](../../src/ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/Interfaces/Persistence/UniqueKeys.cs); actualizar sus referencias en `AssistantModelService`, `AssistantModelPriceConfiguration` y `AssistantPersistenceTests`. Conservar el literal del índice y la traducción del choque: sin cambio HTTP ni migración. **Pendiente de respuesta del usuario antes de programar.** |
| 1 | [IdentityAccessTests.cs:24](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/IdentityAccessTests.cs:24). El inventario cerrado de consumidores exige `AssistantSessionPolicy`, ya retirado. | La comprobación del núcleo contiene una entrada exclusiva del módulo. Pasarla a una contribución partial del árbol de tests del asistente, como [AccessDeclarationTests de WhatsApp](../../tests/ArquitecturaBaseMultitenant.ArchitectureTests/Modules/WhatsApp/AccessDeclarationTests.cs). Mantener exactos los cuatro consumidores del núcleo y el rechazo de consumidores no declarados. |
| 2 | [PlatformLegalPublicationTests.cs:36](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/PlatformLegalPublicationTests.cs:36) y [línea 110](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/PlatformLegalPublicationTests.cs:110). Publicación futura y dos publicaciones concurrentes. | Esperan versión inicial 2 y siguientes 3/4; sin `AssistantLegalDocumentSeed` el seed del núcleo trae correctamente versión 1. Comprobar publicación, concurrencia, idempotencia y aceptación respecto de la revisión inicial leída; conservar una comprobación separada de la revisión 2 cuando el módulo está instalado. No cambiar los documentos productivos ni exigir al núcleo el seed del asistente. |
| 2 | [ProductionSeedTests.cs:116](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Identity/ProductionSeedTests.cs:116), variantes `separateNames=true/false`. Seed inicial en Production. | Exigen tres eventos, incluido `Initial assistant reference seed`; sin módulo hay los dos eventos propios del núcleo. La misma prueba también exige más abajo cuatro documentos, versiones 1/2, ocho contenidos y textos Anthropic: esas comprobaciones deben quedar en el árbol del módulo. Mantener exactos los eventos, legales, culturas, operador, permisos y repetición del seed del núcleo; contribuir las expectativas exclusivas desde tests quitables del asistente. |
| 1 | [PlatformAccountPrimaryPhoneTests.cs:104](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/PlatformAccountPrimaryPhoneTests.cs:104), envío de recuperación desde teléfono principal. | El focal terminó de nuevo en 400, con `Users.Phone.Invalid` (`119/03`, línea 3052 del log). El [seed de la línea 172](../../tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Platform/PlatformAccountPrimaryPhoneTests.cs:172) concatena dígitos aleatorios; `PhoneNumber.Create` comprueba forma, pero [AccountRecoveryGuard.cs:21](../../src/ArquitecturaBaseMultitenant.Application/Services/Auth/AccountRecoveryGuard.cs:21) valida teléfono móvil mediante el parser real. Usar datos sintéticos que pasen ese parser antes de guardarlos, conservando el 202, la aprobación y las máscaras exigidas. Este fallo de datos no acredita una rotura de recuperación provocada por retirar el asistente. |

## Diagnóstico focal terminado

`119-assistant-completed-failure-focals/result.json` registra clases completas ejecutadas solas, sobre los mismos binarios actuales de `105`, sin ampliar plazos ni modificar fuentes:

- `IdentityAccessTests`: **9/10**, reproduce el inventario exclusivo del asistente.
- `DbContextModelTests`: **6/7**, reproduce exactamente `IX_AssistantModelPrices_Provider_ProviderModelId` ausente del modelo.
- `PlatformAccountPrimaryPhoneTests`: **9/10**, reproduce el 400; el log identifica `Users.Phone.Invalid` antes de registrar el pedido.

Son **24/27** focales, con los tres fallos originales reproducidos y cero omitidos. Los otros cuatro fallos se explican por las expectativas explícitas de seed y revisión legal en sus fuentes; todavía no se ejecutó un arreglo.

## Límite y continuación propuesta

Se detuvo el retiro conforme a la instrucción del usuario: «Solo pará si al quitar un módulo se rompe el sistema: el build, los tests o el ingreso». No se corrigió la constante del núcleo ni se inició el retiro de ambos. La autorización para preparar scripts, chequeos y datos de prueba sigue vigente; el cambio de producto se consulta porque apareció una dependencia real durante el retiro.

Recomendación: autorizar a Back el cambio mínimo de ubicación de `AssistantModel`; corregir las expectativas modulares y los datos de prueba conservando su cobertura, con focales/build del alcance y un commit coherente. Antes de publicarlo, probar la configuración instalada para no perder el test del índice ni del seed del asistente. No mezclarlo con M3 ni con una migración de retención.

Después de ese commit, Integración debe crear un nuevo corte Back sin alterar las etiquetas anteriores, trasladar sólo el correctivo y repetir lo invalidado: build, focales y retiro Asistente; si pasa, retiro de ambos. El retiro WhatsApp aprobado sigue como evidencia del corte anterior y sólo se repite en lo que el correctivo invalide.

**E8 sigue abierta.** Además del retiro, faltan la repetición del E2E real con el runner móvil corregido y Tools recompilado, detector documental final, recorrido guiado de usuario y comprobación Meta sobre el corte final. La aprobación de plantillas y los tests de fixture no sustituyen entrega real.

## Evidencia local

En `.local/puerta-8/20261010-1/`: `105-assistant-nonincremental`, `108-assistant-full-repeat.json`, `116-assistant-completed-summary.json`, `117-assistant-cleanup-confirmed.json` y `119-assistant-completed-failure-focals/`. Estos archivos no se versionan; contienen las salidas originales. No se incluye un origen fijo de túnel ni secretos en este informe.
